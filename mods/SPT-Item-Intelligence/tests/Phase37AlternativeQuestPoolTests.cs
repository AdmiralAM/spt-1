using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase37AlternativeQuestPoolTests
{
    public static int Run()
    {
        int assertions = 0;
        var profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = new object[] { Item("a", true), Item("b", false) } },
            ["Quests"] = new object[] { new Dictionary<string, object> { ["qid"] = "q", ["status"] = "Started" } },
            ["Hideout"] = new Dictionary<string, object> { ["Areas"] = Array.Empty<object>() }
        };
        var quests = new Dictionary<string, object>
        {
            ["q"] = new Dictionary<string, object>
            {
                ["_id"] = "q", ["QuestName"] = "Shared food",
                ["conditions"] = new Dictionary<string, object> { ["AvailableForFinish"] = new object[] { Condition(false) } }
            }
        };
        var emptyHideout = new Dictionary<string, object> { ["areas"] = Array.Empty<object>(), ["customAreas"] = Array.Empty<object>() };
        RequirementIndex any = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(new RequirementDataEnvelope(1, profile, quests, emptyHideout, Array.Empty<object>())));
        Expect(any.Get("a").OwnedCount == 1 && any.Get("b").OwnedCount == 1, "each accepted variant keeps its individual owned count", ref assertions);
        Expect(any.Get("a").Allocation.Owned == 2 && any.Get("b").Allocation.Owned == 2, "only requirement coverage uses the shared eligible pool", ref assertions);
        Expect(any.Get("a").Allocation.Coverage == RequirementCoverage.Enough && any.Get("b").Allocation.Coverage == RequirementCoverage.Enough, "one can of each fulfills one shared two-item condition", ref assertions);
        Expect(any.Get("a").Allocation.ExactOwned == 1 && any.Get("b").Allocation.ExactOwned == 1 &&
               any.Get("a").Allocation.Keep == 2 && any.Get("a").Allocation.Missing == 0,
            "each interchangeable item keeps its own stock count while the category quota is allocated once", ref assertions);

        var shortProfile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = new object[] { Stack("a", 2, false), Item("b", false) } },
            ["Quests"] = profile["Quests"], ["Hideout"] = profile["Hideout"]
        };
        var shortQuest = new Dictionary<string, object>
        {
            ["q"] = new Dictionary<string, object>
            {
                ["_id"] = "q", ["QuestName"] = "Shared food",
                ["conditions"] = new Dictionary<string, object> { ["AvailableForFinish"] = new object[] { ConditionWithCount(4, false) } }
            }
        };
        RequirementIndex shortPool = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(4, shortProfile, shortQuest, emptyHideout, Array.Empty<object>())));
        Expect(shortPool.Get("a").Allocation.Owned == 3 && shortPool.Get("a").Allocation.ExactOwned == 2 &&
               shortPool.Get("a").Allocation.Missing == 1 && shortPool.Get("a").Allocation.Coverage == RequirementCoverage.NeedMore,
            "three total alternatives cannot be reported Enough for a four-item category requirement", ref assertions);
        var presentation = ItemPresentationIndexBuilder.Build(ItemRequirementStateBuilder.Build(shortPool), null).Get("a");
        ItemHoverText full = new ItemHoverTextFormatter().Format(new ItemHoverState(presentation));
        Expect(Contains(full, "any of 2 item types 3/4 · this type owned ×2"),
            "Full explicitly separates category progress from this template's exact stock", ref assertions);

        ((object[])((Dictionary<string, object>)((Dictionary<string, object>)quests["q"])["conditions"])["AvailableForFinish"])[0] = Condition(true);
        RequirementIndex fir = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(new RequirementDataEnvelope(2, profile, quests, emptyHideout, Array.Empty<object>())));
        Expect(fir.Get("a").Allocation.OwnedFir == 1 && fir.Get("a").Allocation.Missing == 1, "non-FIR alternative cannot fill the shared FIR-only requirement", ref assertions);

        var separateProfile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = new object[] { Stack("water", 5, false) } },
            ["Quests"] = profile["Quests"], ["Hideout"] = profile["Hideout"]
        };
        var separateQuest = (Dictionary<string, object>)quests["q"];
        ((Dictionary<string, object>)separateQuest["conditions"])["AvailableForFinish"] = new object[]
        {
            SingleCondition("water", 5), SingleCondition("stew", 5)
        };
        RequirementIndex separate = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(new RequirementDataEnvelope(3, separateProfile, quests, emptyHideout, Array.Empty<object>())));
        Expect(separate.Get("water").Allocation.Coverage == RequirementCoverage.Enough,
            "five waters cover only the water condition", ref assertions);
        Expect(separate.Get("stew").Allocation.Missing == 5 && separate.Get("stew").Allocation.Coverage == RequirementCoverage.NeedMore,
            "a separate five-stew condition remains fully required", ref assertions);

        var mixedProfile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = new object[] { Stack("mre", 5, true), Stack("otherfood", 7, true) } },
            ["Quests"] = profile["Quests"], ["Hideout"] = profile["Hideout"]
        };
        var mixedQuest = new Dictionary<string, object>
        {
            ["q"] = new Dictionary<string, object>
            {
                ["_id"] = "q", ["QuestName"] = "Mixed exact and choice",
                ["conditions"] = new Dictionary<string, object> { ["AvailableForFinish"] = new object[]
                {
                    SingleCondition("mre", 5),
                    new Dictionary<string, object> { ["id"] = "mre-second", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "mre" }, ["value"] = 2 },
                    new Dictionary<string, object> { ["id"] = "food-choice", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "mre", "otherfood" }, ["value"] = 5, ["onlyFoundInRaid"] = true }
                } }
            }
        };
        RequirementIndex mixed = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(5, mixedProfile, mixedQuest, emptyHideout, Array.Empty<object>())));
        Expect(mixed.Get("mre").Allocation.ExactOwned == 5 && mixed.Get("mre").Allocation.Owned == 12 &&
               mixed.Get("mre").Allocation.Missing == 2 && mixed.Get("mre").Allocation.Coverage == RequirementCoverage.NeedMore,
            "other eligible food covers the choice but cannot replace two missing exact MREs", ref assertions);
        ItemHoverText mixedText = new ItemHoverTextFormatter().Format(new ItemHoverState(
            ItemPresentationIndexBuilder.Build(ItemRequirementStateBuilder.Build(mixed), null).Get("mre")));
        Expect(Contains(mixedText, "Need More ×2 · Keep ×7 this type · choice ×5"),
            "the card states exact-template and interchangeable quotas separately", ref assertions);
        ItemIntelligenceDecision oneRaidMre = ItemIntelligenceDecisionEngine.Evaluate(mixed.Get("mre").Allocation,
            candidateFoundInRaid: true, raidOwned: 1, raidFoundInRaid: 1);
        Expect(oneRaidMre.Allocation.Missing == 1 && oneRaidMre.Coverage == RequirementCoverage.NeedMore,
            "raid pickup updates the exact-template shortage without losing the shared-choice split", ref assertions);

        mixedProfile["Inventory"] = new Dictionary<string, object> { ["items"] = new object[] { Stack("mre", 7, true), Stack("otherfood", 7, true) } };
        RequirementIndex mixedEnough = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(6, mixedProfile, mixedQuest, emptyHideout, Array.Empty<object>())));
        Expect(mixedEnough.Get("mre").Allocation.Coverage == RequirementCoverage.Enough,
            "seven exact MREs plus distinct alternative stock cover all three consumptive demands", ref assertions);

        ((object[])((Dictionary<string, object>)((Dictionary<string, object>)mixedQuest["q"])["conditions"])["AvailableForFinish"])[0] = SingleCondition("mre", 7);
        ((object[])((Dictionary<string, object>)((Dictionary<string, object>)mixedQuest["q"])["conditions"])["AvailableForFinish"])[1] =
            new Dictionary<string, object> { ["id"] = "otherfood-exact", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "otherfood" }, ["value"] = 7 };
        RequirementIndex reservedOther = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(7, mixedProfile, mixedQuest, emptyHideout, Array.Empty<object>())));
        Expect(reservedOther.Get("mre").Allocation.Coverage == RequirementCoverage.NeedMore &&
               reservedOther.Get("mre").Allocation.Missing == 5,
            "an alternative variant reserved by its own exact quest cannot also cover the shared choice", ref assertions);
        for (int exact = 0; exact <= 3; exact++)
        for (int other = 0; other <= 3; other++)
        for (int exactFir = 0; exactFir <= exact; exactFir++)
        for (int otherFir = 0; otherFir <= other; otherFir++)
        for (int fixedNeed = 0; fixedNeed <= 3; fixedNeed++)
        for (int choiceNeed = 0; choiceNeed <= 3; choiceNeed++)
        for (int fixedFir = 0; fixedFir <= fixedNeed; fixedFir++)
        for (int choiceFir = 0; choiceFir <= choiceNeed; choiceFir++)
        {
            var allocation = new ItemRequirementAllocation(exact + other, exactFir + otherFir,
                fixedNeed + choiceNeed, 0, 0, fixedFir + choiceFir, 0,
                exact, exactFir, sharedAlternativePool: true,
                fixedNow: fixedNeed, fixedLater: 0, fixedHideout: 0,
                fixedNowFir: fixedFir, fixedLaterFir: 0, fixedHideoutFir: 0);
            bool possible = CanCoverMixed(exact, other, exactFir, otherFir,
                fixedNeed, choiceNeed, fixedFir, choiceFir);
            Expect((allocation.Coverage != RequirementCoverage.NeedMore) == possible,
                $"mixed exact/choice coverage differs from inventory feasibility for E={exact}, O={other}, EF={exactFir}, OF={otherFir}, F={fixedNeed}, A={choiceNeed}, FF={fixedFir}, AF={choiceFir}", ref assertions);
        }
        return assertions;
    }

    static bool CanCoverMixed(int exact, int other, int exactFir, int otherFir,
        int fixedNeed, int choiceNeed, int fixedFir, int choiceFir)
    {
        if (exactFir < fixedFir) return false;
        int fixedAny = fixedNeed - fixedFir;
        for (int exactFirForAny = 0; exactFirForAny <= Math.Min(exactFir - fixedFir, fixedAny); exactFirForAny++)
        {
            int exactNonFirForAny = fixedAny - exactFirForAny;
            if (exactNonFirForAny > exact - exactFir) continue;
            int remainingFir = exactFir - fixedFir - exactFirForAny + otherFir;
            int remainingAny = exact + other - fixedNeed;
            if (remainingFir >= choiceFir && remainingAny >= choiceNeed) return true;
        }
        return false;
    }

    static Dictionary<string, object> Item(string tpl, bool fir) => new Dictionary<string, object> { ["_tpl"] = tpl, ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = 1, ["SpawnedInSession"] = fir } };
    static Dictionary<string, object> Stack(string tpl, int count, bool fir) => new Dictionary<string, object> { ["_tpl"] = tpl, ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = count, ["SpawnedInSession"] = fir } };
    static Dictionary<string, object> Condition(bool fir) => new Dictionary<string, object> { ["id"] = "shared", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "a", "b" }, ["value"] = 2, ["onlyFoundInRaid"] = fir };
    static Dictionary<string, object> ConditionWithCount(int count, bool fir) => new Dictionary<string, object> { ["id"] = "shared", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "a", "b" }, ["value"] = count, ["onlyFoundInRaid"] = fir };
    static Dictionary<string, object> SingleCondition(string tpl, int count) => new Dictionary<string, object> { ["id"] = tpl, ["conditionType"] = "HandoverItem", ["target"] = new object[] { tpl }, ["value"] = count, ["onlyFoundInRaid"] = false };
    static bool Contains(ItemHoverText text, string expected)
    {
        for (int i = 0; i < text.GetLineCount(ItemTooltipMode.Full); i++)
            if (text.GetLine(ItemTooltipMode.Full, i).Contains(expected, StringComparison.Ordinal)) return true;
        return false;
    }
    static void Expect(bool value, string message, ref int assertions) { assertions++; if (!value) throw new InvalidOperationException("Phase 37 assertion failed: " + message); }
}

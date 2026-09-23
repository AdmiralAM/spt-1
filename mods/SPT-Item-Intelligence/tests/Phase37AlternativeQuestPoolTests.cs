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
        return assertions;
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

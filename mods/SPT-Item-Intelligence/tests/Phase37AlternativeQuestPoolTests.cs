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

        ((object[])((Dictionary<string, object>)((Dictionary<string, object>)quests["q"])["conditions"])["AvailableForFinish"])[0] = Condition(true);
        RequirementIndex fir = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(new RequirementDataEnvelope(2, profile, quests, emptyHideout, Array.Empty<object>())));
        Expect(fir.Get("a").Allocation.OwnedFir == 1 && fir.Get("a").Allocation.Missing == 1, "non-FIR alternative cannot fill the shared FIR-only requirement", ref assertions);
        return assertions;
    }

    static Dictionary<string, object> Item(string tpl, bool fir) => new Dictionary<string, object> { ["_tpl"] = tpl, ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = 1, ["SpawnedInSession"] = fir } };
    static Dictionary<string, object> Condition(bool fir) => new Dictionary<string, object> { ["id"] = "shared", ["conditionType"] = "HandoverItem", ["target"] = new object[] { "a", "b" }, ["value"] = 2, ["onlyFoundInRaid"] = fir };
    static void Expect(bool value, string message, ref int assertions) { assertions++; if (!value) throw new InvalidOperationException("Phase 37 assertion failed: " + message); }
}

using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase20FirQuestTests
{
    public static int Run()
    {
        int assertions = 0;
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["items"] = new object[]
                {
                    Item("A", 2, true),
                    Item("A", 3, false),
                    Item("B", 1, false)
                }
            },
            ["Quests"] = new object[]
            {
                new Dictionary<string, object> { ["qid"] = "q-now", ["status"] = "Started" }
            },
            ["Hideout"] = new Dictionary<string, object> { ["Areas"] = new object[0] }
        };

        Dictionary<string, object> quests = new Dictionary<string, object>
        {
            ["q-now"] = Quest("q-now", new object[]
            {
                Condition("HandoverItem", "A", 5, true),
                Condition("FindItem", "A", 5, true),
                Condition("PlaceBeacon", "A", 100, false)
            }),
            ["q-fir-later"] = Quest("q-fir-later", new object[]
            {
                Condition("HandoverItem", "A", 3, true)
            }),
            ["q-any-later"] = Quest("q-any-later", new object[]
            {
                Condition("HandoverItem", "A", 7, false)
            }),
            ["q-overlap"] = Quest("q-overlap", new object[]
            {
                Condition("FindItem", "B", 2, false),
                Condition("LeaveItemAtLocation", "B", 10, false)
            }),
            ["q-beacon-only"] = Quest("q-beacon-only", new object[]
            {
                Condition("PlaceBeacon", "C", 50, false)
            })
        };

        Dictionary<string, object> hideout = new Dictionary<string, object>
        {
            ["areas"] = new object[0],
            ["customAreas"] = new object[0]
        };

        RequirementDataEnvelope envelope = new RequirementDataEnvelope(777, profile, quests, hideout, new object[0]);
        RequirementProjection projection = new AqcQuestRequirementProjector().Project(envelope);
        RequirementIndex index = RequirementIndexBuilder.Build(projection);

        Expect(index.Get("a").OwnedCount == 5, "total owned remains compatible with the main requirement index", ref assertions);
        Expect(index.Get("a").QuestNeededNow == 5, "Find plus Handover is not double-counted", ref assertions);
        Expect(index.Get("a").QuestNeededLater == 7, "future reserve remains the largest single future quest requirement", ref assertions);
        Expect(index.Get("b").QuestNeededLater == 2, "LeaveItemAtLocation is suppressed when Find/Handover already represents the item", ref assertions);
        Expect(index.Get("c") == RequirementIndexEntry.Empty, "PlaceBeacon is not treated as an item reserve", ref assertions);

        FirRequirementState fir = FirRequirementRegistry.Get("A");
        Expect(fir.OwnedFoundInRaid == 2, "SpawnedInSession stacks are counted as FIR owned", ref assertions);
        Expect(fir.QuestNowFoundInRaid == 5, "current FIR requirement retains its FIR count", ref assertions);
        Expect(fir.QuestLaterFoundInRaid == 3, "future FIR reserve is tracked independently of larger non-FIR future requirement", ref assertions);

        ItemHoverText text = new ItemHoverText(
            "10,000 ₽ · Vendor", string.Empty, string.Empty, "A",
            5, 5, 7, 0, 7,
            requirementDetails: null,
            ownedFoundInRaid: fir.OwnedFoundInRaid,
            questNowFoundInRaid: fir.QuestNowFoundInRaid,
            questLaterFoundInRaid: fir.QuestLaterFoundInRaid);
        Expect(text.QuestNowOwned == 2 && text.QuestNowFoundInRaidOwned == 2,
            "non-FIR owned items cannot satisfy an FIR current quest requirement", ref assertions);
        Expect(text.QuestNowLine == "Quest Now: 2/5 · FIR 2/5", "current quest line exposes FIR shortfall", ref assertions);
        Expect(text.QuestLaterOwned == 3 && text.QuestLaterFoundInRaidOwned == 0,
            "remaining non-FIR inventory can satisfy only the unrestricted part of future reserve", ref assertions);
        Expect(text.OwnedLine == "Owned ×5 · FIR ×2", "Detailed/Full owned line exposes FIR stock", ref assertions);

        FirRequirementRegistry.Clear();
        return assertions;
    }

    static Dictionary<string, object> Item(string templateId, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["_tpl"] = templateId,
            ["upd"] = new Dictionary<string, object>
            {
                ["StackObjectsCount"] = count,
                ["SpawnedInSession"] = fir
            }
        };
    }

    static Dictionary<string, object> Quest(string id, object[] conditions)
    {
        return new Dictionary<string, object>
        {
            ["_id"] = id,
            ["QuestName"] = id,
            ["conditions"] = new Dictionary<string, object> { ["AvailableForFinish"] = conditions }
        };
    }

    static Dictionary<string, object> Condition(string kind, string target, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["conditionType"] = kind,
            ["target"] = new object[] { target },
            ["value"] = count,
            ["onlyFoundInRaid"] = fir
        };
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 20 assertion failed: " + message);
    }
}

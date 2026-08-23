using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase18TooltipIntelligenceTests
{
    public static int Run()
    {
        int assertions = 0;
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["items"] = new object[] { new Dictionary<string, object> { ["_tpl"] = "VALUE" } }
            },
            ["Quests"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["qid"] = "q-now", ["status"] = "Started",
                    ["completedConditions"] = new object[] { "done" }
                }
            },
            ["Hideout"] = new Dictionary<string, object>
            {
                ["Areas"] = new object[] { new Dictionary<string, object> { ["type"] = "10", ["level"] = 0 } }
            }
        };
        Dictionary<string, object> quests = new Dictionary<string, object>
        {
            ["q-now"] = new Dictionary<string, object>
            {
                ["_id"] = "q-now", ["QuestName"] = "Signal - Part 1",
                ["conditions"] = new Dictionary<string, object>
                {
                    ["AvailableForFinish"] = new object[]
                    {
                        Condition("done", "IGNORED", 9, false),
                        Condition("active", "VALUE", 2, true)
                    }
                }
            }
        };
        Dictionary<string, object> hideout = new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "10",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = new Dictionary<string, object>
                        {
                            ["requirements"] = new object[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["type"] = "Item", ["templateId"] = "VALUE", ["count"] = 3
                                }
                            }
                        }
                    }
                }
            }
        };

        RequirementDataEnvelope envelope = new RequirementDataEnvelope(123, profile, quests, hideout, new object[0]);
        RequirementIndex index = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(envelope));
        RequirementIndexEntry entry = index.Get("value");
        Expect(index.Get("ignored") == RequirementIndexEntry.Empty, "completed quest condition is excluded", ref assertions);
        Expect(entry.Details.Count == 2, "quest and hideout details are retained", ref assertions);
        Expect(entry.Details[0].Label == "Signal - Part 1" && entry.Details[0].FoundInRaidRequired, "quest name and FIR are retained", ref assertions);
        Expect(entry.Details[1].Label == "Workbench L1", "hideout area and target level are concrete", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(ItemRequirementStateBuilder.Build(index), ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("VALUE", 42000, "Therapist", 12000, 9000, 2, 1)
        }));
        ItemHoverText text = new ItemHoverTextFormatter().Format(new ItemHoverState(store.Get("value")));
        Expect(text.BestSourceLine == "Best: Therapist · 42,000 ₽/unit", "named winning trader is exposed", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Detailed, "Now: Signal - Part 1 ×2 · FIR"), "detailed mode names the active quest", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Detailed, "Hideout: Workbench L1 ×3"), "detailed mode names the hideout target", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("FLEA", 10000, "Prapor", 51000, 9000)
        }));
        ItemHoverText flea = new ItemHoverTextFormatter().Format(new ItemHoverState(store.Get("flea")));
        Expect(flea.BestSourceLine == "Best: Flea · 51,000 ₽/unit", "flea winning source is explicit", ref assertions);

        ItemHoverText bounded = new ItemHoverText("1 ₽", "", "", "bounded", 0, 0, 0, 0, 0, "", new[] { "A", "B", "C", "D" });
        Expect(Contains(bounded, ItemTooltipMode.Detailed, "Requirements: +1 more"), "detailed target list is bounded with a cached remainder line", ref assertions);
        Expect(Contains(bounded, ItemTooltipMode.Full, "D"), "full mode retains every concrete target", ref assertions);
        return assertions;
    }

    static Dictionary<string, object> Condition(string id, string target, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["id"] = id, ["conditionType"] = "HandoverItem", ["target"] = new object[] { target },
            ["value"] = count, ["onlyFoundInRaid"] = fir
        };
    }

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i) == expected) return true;
        return false;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 18 assertion failed: " + message);
    }
}

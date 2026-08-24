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
        Expect(text.Primary == "42,000 ₽ · Therapist", "vendor mode exposes named highest trader value", ref assertions);
        Expect(text.Secondary == "Flea: 12,000 ₽", "vendor mode retains alternate flea value for Full", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "Per slot: 21,000 ₽"), "full mode exposes value per slot", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Detailed, "Now: Signal - Part 1 ×2 · FIR"), "detailed mode names the active quest", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Detailed, "Hideout: Workbench L1 ×3"), "detailed mode names the hideout target", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("FLEA", 10000, "Prapor", 51000, 9000)
        }));
        ItemHoverText flea = new ItemHoverTextFormatter().Format(new ItemHoverState(store.Get("flea")), ItemValueMode.Flea);
        Expect(flea.Primary == "51,000 ₽ · Flea" && flea.Secondary == "Prapor: 10,000 ₽", "flea mode retains named trader alternate value for Full", ref assertions);

        ItemHoverText bounded = new ItemHoverText("1 ₽", "", "", "bounded", 0, 0, 0, 0, 0, "", new[] { "A", "B", "C", "D" });
        Expect(Contains(bounded, ItemTooltipMode.Detailed, "Requirements: +1 more"), "detailed target list is bounded with a cached remainder line", ref assertions);
        Expect(Contains(bounded, ItemTooltipMode.Full, "D"), "full mode retains every concrete target", ref assertions);

        const string bulbexId = "619cbfeb6b8a1b37a54eebfa";
        Dictionary<string, object> bulbexHideout = new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "4",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["2"] = new Dictionary<string, object>
                        {
                            ["requirements"] = new object[]
                            {
                                new Dictionary<string, object> { ["type"] = 1, ["templateId"] = bulbexId, ["count"] = 1 },
                                new Dictionary<string, object> { ["type"] = 0, ["templateId"] = "AREA_ONLY", ["count"] = 1 }
                            }
                        }
                    }
                }
            },
            ["customAreas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["_id"] = "custom-zone",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = new Dictionary<string, object>
                        {
                            ["requirements"] = new object[]
                            {
                                new Dictionary<string, object> { ["type"] = "Item", ["templateId"] = "CUSTOM", ["count"] = 2 }
                            }
                        }
                    }
                }
            }
        };
        Dictionary<string, object> bulbexProfile = ProfileWithHideout(
            new object[] { new Dictionary<string, object> { ["_tpl"] = bulbexId, ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = 2 } } },
            new object[]
            {
                new Dictionary<string, object> { ["type"] = "4", ["level"] = 1 },
                new Dictionary<string, object> { ["_id"] = "custom-zone", ["level"] = 0 }
            });
        RequirementIndex bulbexIndex = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(124, bulbexProfile, new object[0], bulbexHideout, new object[0])));
        ItemRequirementState bulbexState = ItemRequirementStateBuilder.Build(bulbexIndex).Get(bulbexId);
        Expect(bulbexState.HideoutNeeded == 1 && bulbexState.KeepCount == 1, "Bulbex is retained for hideout area type 4 stage 2", ref assertions);
        Expect(bulbexState.Decision == ItemRequirementDecision.Keep && bulbexState.SurplusCount == 1, "Bulbex keeps its requirement even when one owned item is surplus", ref assertions);
        ItemPresentationStore bulbexStore = new ItemPresentationStore();
        bulbexStore.Refresh(ItemRequirementStateBuilder.Build(bulbexIndex), ItemPriceIndex.Empty);
        ItemHoverText bulbexText = new ItemHoverTextFormatter().Format(new ItemHoverState(bulbexStore.Get(bulbexId)));
        Expect(bulbexText.HideoutLine == "Hideout: 1/1 ✓", "fulfilled Bulbex hideout quantity remains visible", ref assertions);
        Expect(ItemMarkerPresentation.From(bulbexText).Kind == ItemMarkerKind.Default, "fulfilled Bulbex requirement uses default color", ref assertions);
        Expect(bulbexIndex.Get("area_only") == RequirementIndexEntry.Empty, "numeric Area requirements are not projected as items", ref assertions);
        Expect(bulbexIndex.Get("custom").HideoutNeeded == 2, "custom hideout areas are projected", ref assertions);

        Dictionary<string, object> missingProfile = ProfileWithHideout(
            new object[0], new object[] { new Dictionary<string, object> { ["type"] = "4", ["level"] = 1 } });
        RequirementIndex missingIndex = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(125, missingProfile, new object[0], bulbexHideout, new object[0])));
        ItemPresentationStore missingStore = new ItemPresentationStore();
        missingStore.Refresh(ItemRequirementStateBuilder.Build(missingIndex), ItemPriceIndex.Empty);
        ItemHoverText missingBulbex = new ItemHoverTextFormatter().Format(new ItemHoverState(missingStore.Get(bulbexId)));
        Expect(missingBulbex.HideoutLine == "Hideout: 0/1" && ItemMarkerPresentation.From(missingBulbex).Kind == ItemMarkerKind.Hideout,
            "missing Bulbex uses the hideout marker", ref assertions);

        Dictionary<string, object> constructingProfile = ProfileWithHideout(
            new object[0], new object[] { new Dictionary<string, object> { ["type"] = "4", ["level"] = 1, ["constructing"] = true } });
        RequirementIndex constructingIndex = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(126, constructingProfile, new object[0], bulbexHideout, new object[0])));
        Expect(constructingIndex.Get(bulbexId) == RequirementIndexEntry.Empty, "a constructing hideout stage no longer requires already-consumed items", ref assertions);
        return assertions;
    }

    static Dictionary<string, object> ProfileWithHideout(object[] items, object[] areas)
    {
        return new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = items },
            ["Quests"] = new object[0],
            ["Hideout"] = new Dictionary<string, object> { ["Areas"] = areas }
        };
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

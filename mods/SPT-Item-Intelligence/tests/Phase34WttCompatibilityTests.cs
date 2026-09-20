using System;
using System.Collections.Generic;
using System.IO;
using SPTItemIntelligence;

static class Phase34WttCompatibilityTests
{
    const string ArmoryTemplate = "66875ecf64c1fb1896b2ddc1";
    const string BackportTemplate = "6a15ae2ae5267ba21c07f990";
    const string UnknownTemplate = "ffffffffffffffffffffffff";

    public static int Run()
    {
        int assertions = 0;
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["items"] = new object[]
                {
                    Item("armory-owned", ArmoryTemplate.ToUpperInvariant(), 2, true),
                    Item("backport-owned", BackportTemplate, 1, false),
                    Item("unknown-owned", UnknownTemplate, 1, false)
                }
            },
            ["Quests"] = new object[]
            {
                new Dictionary<string, object> { ["qid"] = "wtt-current", ["status"] = "Started" }
            },
            ["Hideout"] = new Dictionary<string, object>
            {
                ["Areas"] = new object[] { new Dictionary<string, object> { ["type"] = "77", ["level"] = 0 } }
            }
        };
        Dictionary<string, object> quests = new Dictionary<string, object>
        {
            ["wtt-current"] = Quest("wtt-current", ArmoryTemplate, 3, true),
            ["wtt-future"] = Quest("wtt-future", BackportTemplate, 2, false)
        };
        Dictionary<string, object> hideout = new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "77",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = Stage(BackportTemplate, 4)
                    }
                }
            }
        };
        object[] prices =
        {
            new ItemPriceSnapshotEntry(ArmoryTemplate.ToUpperInvariant(), 42000, "Mechanic", 51000, 20000, 1, 1, 2, 1, "blue"),
            new ItemPriceSnapshotEntry(BackportTemplate, 90000, "Ragman", 110000, 30000, 2, 1, 3, 4, "violet")
        };

        RequirementDataEnvelope envelope = new RequirementDataEnvelope(34, profile, quests, hideout, prices);
        RequirementProjection projection = new SptRequirementDataProjector().Project(envelope);
        RequirementIndex index = RequirementIndexBuilder.Build(projection);
        RequirementIndexEntry armory = index.Get(ArmoryTemplate);
        RequirementIndexEntry backport = index.Get(BackportTemplate);
        Expect(armory.OwnedCount == 2 && armory.Allocation.OwnedFir == 2, "Armory stock and FIR stock use the generic template path", ref assertions);
        Expect(armory.QuestNeededNow == 3 && armory.RequiresFoundInRaid && armory.Allocation.Missing == 1,
            "Armory FIR quest requirements allocate without a catalog entry", ref assertions);
        Expect(backport.OwnedCount == 1 && backport.QuestNeededLater == 2 && backport.HideoutNeeded == 4,
            "Backport future-quest and hideout requirements aggregate through the generic path", ref assertions);
        Expect(backport.Allocation.Missing == 5 && backport.KeepCount == 6,
            "Backport stock is allocated once across consumptive requirements", ref assertions);

        ItemPriceIndex priceIndex = new SptPriceDataProjector().Project(prices);
        ItemPriceState armoryPrice;
        ItemPriceState backportPrice;
        Expect(priceIndex.TryGet(ArmoryTemplate, out armoryPrice) && armoryPrice.BestSource == PriceSource.Flea && armoryPrice.BackgroundColor == "blue",
            "Armory value and background metadata survive projection", ref assertions);
        Expect(priceIndex.TryGet(BackportTemplate, out backportPrice) && backportPrice.ValuePerSlot == 55000 && backportPrice.BackgroundColor == "violet",
            "Backport value-per-slot and background metadata survive projection", ref assertions);

        new RelevanceSnapshotDecoder(new StubDecoder(envelope)).Decode("ignored");
        Expect(ItemRelevanceRegistry.Get(ArmoryTemplate).CraftCount == 2 && ItemRelevanceRegistry.Get(ArmoryTemplate).BarterCount == 1,
            "Armory craft and barter relevance use snapshot data", ref assertions);
        Expect(ItemRelevanceRegistry.Get(BackportTemplate).CraftCount == 3 && ItemRelevanceRegistry.Get(BackportTemplate).BarterCount == 4,
            "Backport craft and barter relevance use snapshot data", ref assertions);

        ItemRequirementStateIndex states = ItemRequirementStateBuilder.Build(index);
        Expect(states.Get(UnknownTemplate).OwnedCount == 1 && !states.Get(UnknownTemplate).HasRequirement,
            "unknown template IDs remain safe inventory entries", ref assertions);
        Expect(states.Get("not-present-anywhere") == ItemRequirementState.Empty,
            "unseen template IDs return the canonical empty state without throwing", ref assertions);

        string root = FindRepositoryRoot();
        string server = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "server", "ServerMod.cs"));
        string project = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "server", "SPT-Item-Intelligence.Server.csproj"));
        Expect(server.Contains("foreach (var (templateId, item) in templateTable.Items)"),
            "server enumerates the final SPT template table so custom items are discovered automatically", ref assertions);
        Expect(!server.Contains("com.wtt", StringComparison.OrdinalIgnoreCase) && !project.Contains("WTT-", StringComparison.OrdinalIgnoreCase),
            "WTT compatibility adds no runtime catalog or mandatory dependency", ref assertions);

        ItemRelevanceRegistry.Replace(null);
        return assertions;
    }

    static Dictionary<string, object> Item(string id, string templateId, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["_id"] = id,
            ["_tpl"] = templateId,
            ["upd"] = new Dictionary<string, object>
            {
                ["StackObjectsCount"] = count,
                ["SpawnedInSession"] = fir
            }
        };
    }

    static Dictionary<string, object> Quest(string id, string templateId, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["_id"] = id,
            ["conditions"] = new Dictionary<string, object>
            {
                ["AvailableForFinish"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = id + "-condition",
                        ["conditionType"] = "HandoverItem",
                        ["target"] = new object[] { templateId },
                        ["value"] = count,
                        ["onlyFoundInRaid"] = fir
                    }
                }
            }
        };
    }

    static Dictionary<string, object> Stage(string templateId, int count)
    {
        return new Dictionary<string, object>
        {
            ["requirements"] = new object[]
            {
                new Dictionary<string, object> { ["type"] = 1, ["templateId"] = templateId, ["count"] = count }
            }
        };
    }

    static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "mods", "SPT-Item-Intelligence"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 34 assertion failed: " + message);
    }

    sealed class StubDecoder : IRequirementSnapshotDecoder
    {
        readonly RequirementDataEnvelope envelope;
        public StubDecoder(RequirementDataEnvelope envelope) { this.envelope = envelope; }
        public RequirementDataEnvelope Decode(string json) { return envelope; }
    }
}

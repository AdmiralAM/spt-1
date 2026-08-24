using System.Collections.Generic;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class SerializationFreeStateProjectionTests
{
    [Fact]
    public void ProfileAndInventoryProjection_IgnoreUnrelatedNonStringKeyDictionaries()
    {
        FakeProfile profile = new()
        {
            Info = new FakeInfo { Level = 42 },
            Encyclopedia = new Dictionary<FakeMongoId, bool> { [new FakeMongoId("abc")] = true },
            Quests = new List<FakeQuestProgress>
            {
                new() { Qid = "quest-a", Status = 2, StartTime = 123, StatusTimer = 456 }
            },
            TaskConditionCounters = new Dictionary<string, FakeCounter>
            {
                ["counter-a"] = new FakeCounter { Id = "counter-a", Type = "Kills", Value = 3, SourceId = "quest-a" }
            },
            Inventory = new FakeInventory
            {
                Items = new List<FakeItem>
                {
                    new() { Tpl = "item-a", Upd = new FakeUpd { StackObjectsCount = 4, SpawnedInSession = true } },
                    new() { Tpl = "item-a", Upd = new FakeUpd { StackObjectsCount = 2, SpawnedInSession = false } }
                }
            }
        };

        PlayerProjection player = ProfileProjectionExtractor.Extract(profile);
        InventoryProjection inventory = InventoryProjectionExtractor.Extract(profile);

        Assert.Equal(42, player.Level);
        Assert.True(player.QuestStates.ContainsKey("quest-a"));
        Assert.Equal(3d, player.TaskConditionCounters["counter-a"].Value);
        Assert.Equal(6d, inventory.ByTemplate["item-a"].Total);
        Assert.Equal(4d, inventory.ByTemplate["item-a"].FoundInRaid);
    }

    private sealed class FakeProfile
    {
        public FakeInfo? Info { get; set; }
        public Dictionary<FakeMongoId, bool>? Encyclopedia { get; set; }
        public List<FakeQuestProgress>? Quests { get; set; }
        public Dictionary<string, FakeCounter>? TaskConditionCounters { get; set; }
        public FakeInventory? Inventory { get; set; }
    }

    private sealed class FakeInfo { public int Level { get; set; } }
    private sealed class FakeQuestProgress
    {
        public string? Qid { get; set; }
        public int Status { get; set; }
        public long StartTime { get; set; }
        public long StatusTimer { get; set; }
    }
    private sealed class FakeCounter
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public double Value { get; set; }
        public string? SourceId { get; set; }
    }
    private sealed class FakeInventory { public List<FakeItem>? Items { get; set; } }
    private sealed class FakeItem
    {
        public string? Tpl { get; set; }
        public FakeUpd? Upd { get; set; }
    }
    private sealed class FakeUpd
    {
        public double StackObjectsCount { get; set; }
        public bool SpawnedInSession { get; set; }
    }
    private sealed class FakeMongoId
    {
        private readonly string value;
        public FakeMongoId(string value) { this.value = value; }
        public override string ToString() => value;
    }
}

using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityGoalCatalogTests
    {
        [Fact]
        public void CatalogSeparatesOpenGoalsFromAlreadyUnlockedWithoutRankingThem()
        {
            PlannerTopologyIndex topology = Topology(
                Quest("fieldwork", Array.Empty<string>(), new[] { "ammo-gate" }),
                Quest("ammo-gate", new[] { "fieldwork" }, Array.Empty<string>()),
                Quest("labs-gate", Array.Empty<string>(), Array.Empty<string>()));
            PlannerClientIndex state = State(
                Active("fieldwork"),
                Locked("ammo-gate"),
                Completed("labs-gate"));
            PlannerClientDelayIndex delays = EmptyDelays();

            PlannerCapabilityGoalCatalog catalog = PlannerCapabilityGoalCatalogBuilder.Build(
                new[]
                {
                    Definition("labs-access", "labs-gate"),
                    Definition("controlled-ammo", "ammo-gate")
                },
                topology,
                state,
                delays);

            PlannerCapabilityGoalCatalogItem open = Assert.Single(catalog.OpenGoals);
            Assert.Equal("controlled-ammo", open.Definition.CapabilityId);
            Assert.Equal(PlannerCapabilityGoalCatalogState.Actionable, open.State);
            Assert.Equal(1, open.ActionableQuestCount);

            PlannerCapabilityGoalCatalogItem unlocked = Assert.Single(catalog.UnlockedGoals);
            Assert.Equal("labs-access", unlocked.Definition.CapabilityId);
            Assert.Equal(PlannerCapabilityGoalCatalogState.AlreadyUnlocked, unlocked.State);
        }

        [Fact]
        public void WaitingGoalIsVisibleWithoutInventingActionableWork()
        {
            PlannerTopologyIndex topology = Topology(
                Quest("wait", Array.Empty<string>(), new[] { "gate" }),
                Quest("gate", new[] { "wait" }, Array.Empty<string>()));
            PlannerClientIndex state = State(
                new PlannerQuestClientState("wait", 1, 1, true, true, 9),
                Locked("gate"));
            PlannerClientDelayIndex delays = new PlannerClientDelayIndex(
                1000,
                new Dictionary<string, PlannerClientQuestDelay>(StringComparer.Ordinal)
                {
                    ["wait"] = new PlannerClientQuestDelay("wait", PlannerClientDelayState.PendingKnown, 1300, 300)
                });

            PlannerCapabilityGoalCatalog catalog = PlannerCapabilityGoalCatalogBuilder.Build(
                new[] { Definition("capability", "gate") },
                topology,
                state,
                delays);

            PlannerCapabilityGoalCatalogItem item = Assert.Single(catalog.OpenGoals);
            Assert.Equal(PlannerCapabilityGoalCatalogState.Waiting, item.State);
            Assert.Equal(0, item.ActionableQuestCount);
            Assert.Equal(1, item.WaitingQuestCount);
        }

        [Fact]
        public void DuplicateCapabilityIdsFailClosed()
        {
            PlannerTopologyIndex topology = Topology(Quest("gate", Array.Empty<string>(), Array.Empty<string>()));
            PlannerClientIndex state = State(Locked("gate"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                PlannerCapabilityGoalCatalogBuilder.Build(
                    new[] { Definition("same", "gate"), Definition("SAME", "gate") },
                    topology,
                    state,
                    EmptyDelays()));

            Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CatalogSizeIsExplicitlyBounded()
        {
            PlannerTopologyIndex topology = Topology(
                Quest("a", Array.Empty<string>(), Array.Empty<string>()),
                Quest("b", Array.Empty<string>(), Array.Empty<string>()));
            PlannerClientIndex state = State(Locked("a"), Locked("b"));

            Assert.Throws<InvalidOperationException>(() =>
                PlannerCapabilityGoalCatalogBuilder.Build(
                    new[] { Definition("a", "a"), Definition("b", "b") },
                    topology,
                    state,
                    EmptyDelays(),
                    maxGoals: 1));
        }

        private static PlannerCapabilityGoalDefinition Definition(string capability, string gate)
        {
            return new PlannerCapabilityGoalDefinition(
                capability,
                gate,
                "test",
                PlannerCapabilitySupplyKind.OneTimeSample,
                evidenceSource: "explicit-test-contract");
        }

        private static PlannerTopologyQuest Quest(string id, IReadOnlyList<string> prerequisites, IReadOnlyList<string> dependents)
        {
            return new PlannerTopologyQuest(
                id,
                "trader",
                id,
                null,
                false,
                prerequisites,
                dependents,
                Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(values, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerClientIndex State(params PlannerQuestClientState[] quests)
        {
            Dictionary<string, PlannerQuestClientState> values = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState quest in quests) values[quest.QuestId] = quest;
            return new PlannerClientIndex(1000, values, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }

        private static PlannerQuestClientState Active(string questId)
        {
            return new PlannerQuestClientState(questId, 4, 3, true, true, 2);
        }

        private static PlannerQuestClientState Locked(string questId)
        {
            return new PlannerQuestClientState(questId, 1, 1, true, false, 0);
        }

        private static PlannerQuestClientState Completed(string questId)
        {
            return new PlannerQuestClientState(questId, 5, 4, true, true, 4);
        }

        private static PlannerClientDelayIndex EmptyDelays()
        {
            return new PlannerClientDelayIndex(
                1000,
                new Dictionary<string, PlannerClientQuestDelay>(StringComparer.Ordinal));
        }
    }
}

using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityGoalTests
    {
        [Fact]
        public void BoundedRenewableCapabilityRequiresExplicitFiniteCapacityEvidence()
        {
            Assert.Throws<ArgumentException>(() => new PlannerCapabilityGoalDefinition(
                "ammo-545",
                "munitions-545",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable));

            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "ammo-545",
                "munitions-545",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                itemTemplateId: "ammo-tpl",
                maxUnitsPerReset: 80,
                maxAcquisitionsPerReset: 2,
                evidenceSource: "explicit-adapter");

            Assert.True(definition.HasBoundedSupplyEvidence);
            Assert.Equal(80, definition.MaxUnitsPerReset);
            Assert.Equal(2, definition.MaxAcquisitionsPerReset);
        }

        [Fact]
        public void OneTimeSampleCannotBeFabricatedIntoRenewableCapacity()
        {
            Assert.Throws<ArgumentException>(() => new PlannerCapabilityGoalDefinition(
                "special-weapons-sample",
                "special-weapons",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.OneTimeSample,
                maxUnitsPerReset: 1));
        }

        [Fact]
        public void CapabilityGoalReusesQuestGoalEngineForItsGate()
        {
            PlannerTopologyQuest prerequisite = Quest("fieldwork", Array.Empty<string>(), new[] { "munitions" });
            PlannerTopologyQuest gate = Quest("munitions", new[] { "fieldwork" }, Array.Empty<string>());
            PlannerTopologyIndex topology = Topology(prerequisite, gate);
            PlannerClientIndex state = State(
                new PlannerQuestClientState("fieldwork", 4, 2, true, true, 2),
                new PlannerQuestClientState("munitions", 1, 0, true, false, 0));

            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "ammo-family",
                "munitions",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                maxUnitsPerReset: 80,
                evidenceSource: "explicit-adapter");

            PlannerCapabilityGoal goal = PlannerCapabilityGoalBuilder.Build(definition, topology, state);

            Assert.Equal("munitions", goal.QuestIntent.FocusQuestId);
            Assert.Contains("fieldwork", goal.QuestIntent.FocusPathQuestIds);
            Assert.Contains("fieldwork", goal.QuestIntent.FocusActionableQuestIds);
            Assert.True(goal.HasActionableQuestWork);
        }

        [Fact]
        public void MissingGateInFinalModdedTopologyFailsClosed()
        {
            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "labs-access",
                "missing-clearance",
                "Admiral Trader",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                maxAcquisitionsPerReset: 1);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                PlannerCapabilityGoalBuilder.Build(definition, Topology(), State()));

            Assert.Contains("absent from the final Planner topology", error.Message);
        }

        private static PlannerTopologyQuest Quest(
            string id,
            IReadOnlyList<string> prerequisites,
            IReadOnlyList<string> dependents)
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
            return new PlannerClientIndex(
                0,
                values,
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }
    }
}

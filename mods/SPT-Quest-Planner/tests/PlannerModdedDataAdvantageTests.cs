using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerModdedDataAdvantageTests
    {
        [Fact]
        public void CustomRuntimePrerequisiteChangesDecisionSignalsWithoutExternalDataset()
        {
            PlannerRaidPlan customsPlan = Plan("customs", "custom-current");
            PlannerRaidPlan reservePlan = Plan("reserve", "reserve-current");
            PlannerClientIndex state = EmptyState();

            PlannerTopologyIndex baseTopology = Topology(
                Quest("custom-current"),
                Quest("reserve-current"));

            PlannerRaidDecisionSignals customsBefore = PlannerRaidDecisionSignalBuilder.Build(customsPlan, baseTopology, state);
            PlannerRaidDecisionSignals reserveBefore = PlannerRaidDecisionSignalBuilder.Build(reservePlan, baseTopology, state);
            PlannerRaidDecision before = PlannerRaidDecisionPolicy.Decide(customsBefore, reserveBefore);
            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, before.Outcome);

            PlannerTopologyQuest customCurrent = new PlannerTopologyQuest(
                "custom-current", null, null, null, false,
                Array.Empty<string>(), new[] { "modded-followup" }, Array.Empty<string>());
            PlannerTopologyQuest moddedFollowup = new PlannerTopologyQuest(
                "modded-followup", "custom-trader", null, null, false,
                new[] { "custom-current" }, Array.Empty<string>(), Array.Empty<string>());
            PlannerTopologyIndex moddedTopology = Topology(
                customCurrent,
                moddedFollowup,
                Quest("reserve-current"));

            PlannerRaidDecisionSignals customsAfter = PlannerRaidDecisionSignalBuilder.Build(customsPlan, moddedTopology, state);
            PlannerRaidDecisionSignals reserveAfter = PlannerRaidDecisionSignalBuilder.Build(reservePlan, moddedTopology, state);
            PlannerRaidDecision after = PlannerRaidDecisionPolicy.Decide(customsAfter, reserveAfter);

            Assert.Equal(1, customsAfter.ImmediateUnlockCount);
            Assert.Equal(0, reserveAfter.ImmediateUnlockCount);
            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, after.Outcome);
        }

        private static PlannerRaidPlan Plan(string locationId, string questId)
        {
            return new PlannerRaidPlan(
                locationId,
                new[] { questId },
                new[]
                {
                    new PlannerRaidObjective(
                        questId,
                        "condition",
                        PlannerRaidObjectiveKind.Extract,
                        "Extract",
                        locationId,
                        Array.Empty<string>(),
                        false,
                        1,
                        0)
                },
                new PlannerRaidPreparation(
                    Array.Empty<PlannerRaidBringNeed>(),
                    Array.Empty<PlannerRaidUnresolvedBringNeed>()));
        }

        private static PlannerTopologyQuest Quest(string id)
        {
            return new PlannerTopologyQuest(
                id, null, null, null, false,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(
                values,
                new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerClientIndex EmptyState()
        {
            return new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }
    }
}

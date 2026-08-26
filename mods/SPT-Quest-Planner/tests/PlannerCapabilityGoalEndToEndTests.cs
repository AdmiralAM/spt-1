using System;
using System.Collections.Generic;
using System.Linq;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityGoalEndToEndTests
    {
        [Fact]
        public void PublishedCapabilityEvidenceCanDriveFocusedRaidDecisionWithoutRawDensityWinning()
        {
            const string gateId = "f6e51dc4e50e47ee9af50a4d";
            const string actionableId = "assault-fieldwork";

            PlannerCapabilityGoalDefinition capability = PlannerAdmiralCapabilityContractAdapter
                .Parse(AssaultCapabilityContract())
                .Single(value => value.CapabilityId == "assault-rifles");

            PlannerTopologyIndex topology = Topology(
                Quest(actionableId, Array.Empty<string>(), new[] { gateId }),
                Quest(gateId, new[] { actionableId }, Array.Empty<string>()));
            PlannerClientIndex state = State(
                new PlannerQuestClientState(actionableId, 4, 3, true, true, 2),
                new PlannerQuestClientState(gateId, 1, 1, true, false, 0));

            PlannerCapabilityGoal goal = PlannerCapabilityGoalBuilder.Build(capability, topology, state);
            Assert.Contains(actionableId, goal.QuestIntent.FocusActionableQuestIds);

            PlannerRaidDecisionSignals customs = Signals(
                new[] { actionableId, "setup" },
                immediateUnlockQuestIds: new[] { gateId },
                objectiveCount: 2);
            PlannerRaidDecisionSignals reserve = Signals(
                new[] { "unrelated-1", "unrelated-2", "unrelated-3", "unrelated-4" },
                objectiveCount: 12);

            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    new PlannerRaidDecisionCandidate("customs", customs),
                    new PlannerRaidDecisionCandidate("reserve", reserve)
                },
                goal.QuestIntent);
            PlannerRaidDecisionPresentation raid = PlannerRaidDecisionPresentationBuilder.Build(set);
            PlannerCapabilityGoalPresentation presentation = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                raid,
                EmptyDelay());

            Assert.True(set.HasUniqueRecommendation);
            Assert.Equal("customs", set.Recommendation.LocationId);
            Assert.Equal(PlannerRaidDecisionPresentationKind.BestNextRaid, raid.Kind);
            Assert.Equal(PlannerCapabilityGoalPresentationKind.RaidDecision, presentation.Kind);
            Assert.Equal("assault-rifles", presentation.CapabilityId);
            Assert.Equal(gateId, presentation.GateQuestId);
            Assert.Contains(actionableId, presentation.ActionableQuestIds);
            Assert.Contains("80 units/reset", presentation.ResultSummary);
            Assert.DoesNotContain("objective", presentation.ResultSummary, StringComparison.OrdinalIgnoreCase);
        }

        private static PlannerRaidDecisionSignals Signals(
            IReadOnlyList<string> nonRepeatableQuestIds,
            IReadOnlyList<string> immediateUnlockQuestIds = null,
            int objectiveCount = 0)
        {
            return new PlannerRaidDecisionSignals(
                nonRepeatableQuestIds.Count,
                0,
                Array.Empty<PlannerRaidActionOverlap>(),
                immediateUnlockQuestIds == null ? 0 : immediateUnlockQuestIds.Count,
                0,
                0,
                0,
                objectiveCount,
                objectiveCount,
                nonRepeatableQuestIds,
                Array.Empty<string>(),
                immediateUnlockQuestIds ?? Array.Empty<string>());
        }

        private static PlannerTopologyQuest Quest(
            string id,
            IReadOnlyList<string> prerequisites,
            IReadOnlyList<string> dependents)
        {
            return new PlannerTopologyQuest(
                id,
                "d5c27bb3169f8dfbc13f6b69",
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

        private static PlannerRaidFocusDelayEvidence EmptyDelay()
        {
            return new PlannerRaidFocusDelayEvidence(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static string AssaultCapabilityContract()
        {
            return "{" +
                "\"schemaVersion\":2," +
                "\"product\":\"Admiral Trader\"," +
                "\"owner\":\"Admiral Trader\"," +
                "\"targetSptVersion\":\"4.1.3\"," +
                "\"renewableOffers\":[{" +
                    "\"offerId\":\"b71182859e5958fd12c02e89\"," +
                    "\"itemTpl\":\"59e6906286f7746c9f75e847\"," +
                    "\"capabilityFamily\":\"assault-rifles\"," +
                    "\"sourceType\":\"TraderPurchase\"," +
                    "\"renewability\":\"Bounded\"," +
                    "\"permanent\":true," +
                    "\"questGateId\":\"f6e51dc4e50e47ee9af50a4d\"," +
                    "\"stockPerReset\":80," +
                    "\"buyRestrictionPerReset\":80" +
                "}]," +
                "\"oneTimeRewards\":[]" +
            "}";
        }
    }
}

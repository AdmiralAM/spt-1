using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionPolicyTests
    {
        [Fact]
        public void RawObjectiveDensityAloneDoesNotCreateRecommendation()
        {
            PlannerRaidDecisionSignals left = Signals(objectiveCount: 9, knownRemainingWork: 20d);
            PlannerRaidDecisionSignals right = Signals(objectiveCount: 2, knownRemainingWork: 2d);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(left, right);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.False(decision.HasRecommendation);
            Assert.Contains("No meaningful proven difference", decision.Reason);
        }

        [Fact]
        public void ParetoDominantSynergyAndUnlockCandidateIsPreferred()
        {
            PlannerRaidDecisionSignals left = Signals(
                nonRepeatable: 2,
                overlapGroups: 1,
                maxOverlap: 2,
                unlocks: 2,
                missing: 0,
                unresolved: 0,
                unknown: 0,
                objectiveCount: 2);
            PlannerRaidDecisionSignals right = Signals(
                nonRepeatable: 1,
                overlapGroups: 0,
                maxOverlap: 0,
                unlocks: 0,
                missing: 0,
                unresolved: 0,
                unknown: 0,
                objectiveCount: 7);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(left, right);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
            Assert.True(decision.HasRecommendation);
        }

        [Fact]
        public void PreparationCanCreateClearPreferenceWhenProgressionSignalsTie()
        {
            PlannerRaidDecisionSignals left = Signals(nonRepeatable: 2, missing: 0, unresolved: 0);
            PlannerRaidDecisionSignals right = Signals(nonRepeatable: 2, missing: 1, unresolved: 0);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(left, right);

            Assert.Equal(PlannerRaidDecisionOutcome.PreferLeft, decision.Outcome);
        }

        [Fact]
        public void ConflictingUnlockAndPreparationAdvantagesCauseAbstention()
        {
            PlannerRaidDecisionSignals left = Signals(nonRepeatable: 2, unlocks: 2, missing: 1);
            PlannerRaidDecisionSignals right = Signals(nonRepeatable: 2, unlocks: 0, missing: 0);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(left, right);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
            Assert.Contains("competing proven advantages", decision.Reason);
        }

        [Fact]
        public void BetterEvidenceDoesNotOverrideACompetingProgressionAdvantage()
        {
            PlannerRaidDecisionSignals left = Signals(nonRepeatable: 2, unknown: 1, objectiveCount: 2);
            PlannerRaidDecisionSignals right = Signals(nonRepeatable: 1, unknown: 0, objectiveCount: 2);

            PlannerRaidDecision decision = PlannerRaidDecisionPolicy.Decide(left, right);

            Assert.Equal(PlannerRaidDecisionOutcome.Abstain, decision.Outcome);
        }

        private static PlannerRaidDecisionSignals Signals(
            int nonRepeatable = 0,
            int repeatable = 0,
            int overlapGroups = 0,
            int maxOverlap = 0,
            int unlocks = 0,
            int missing = 0,
            int unresolved = 0,
            int unknown = 0,
            int objectiveCount = 0,
            double knownRemainingWork = 0d)
        {
            PlannerRaidActionOverlap[] overlaps;
            if (overlapGroups <= 0)
            {
                overlaps = Array.Empty<PlannerRaidActionOverlap>();
            }
            else
            {
                overlaps = new PlannerRaidActionOverlap[overlapGroups];
                for (int i = 0; i < overlaps.Length; i++)
                {
                    string[] questIds = new string[Math.Max(2, maxOverlap)];
                    for (int q = 0; q < questIds.Length; q++) questIds[q] = "q" + i + "-" + q;
                    overlaps[i] = new PlannerRaidActionOverlap("sig-" + i, PlannerRaidObjectiveKind.Kill, questIds, questIds.Length);
                }
            }

            return new PlannerRaidDecisionSignals(
                nonRepeatable,
                repeatable,
                overlaps,
                unlocks,
                missing,
                unresolved,
                unknown,
                objectiveCount,
                knownRemainingWork);
        }
    }
}

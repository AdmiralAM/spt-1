using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionSetTests
    {
        [Fact]
        public void UniqueUndominatedCandidateBecomesRecommendation()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(new[]
            {
                Candidate("Customs", nonRepeatable: 2, overlapGroups: 1, maxOverlap: 2, unlocks: 1),
                Candidate("Reserve", nonRepeatable: 1),
                Candidate("Shoreline", nonRepeatable: 1, missing: 1)
            });

            Assert.True(result.HasUniqueRecommendation);
            Assert.NotNull(result.Recommendation);
            Assert.Equal("Customs", result.Recommendation!.LocationId);
            Assert.Single(result.Contenders);
        }

        [Fact]
        public void ConflictingTradeoffsProduceFrontierAndAbstention()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(new[]
            {
                Candidate("Customs", nonRepeatable: 2, unlocks: 2, missing: 1),
                Candidate("Reserve", nonRepeatable: 2, unlocks: 0, missing: 0)
            });

            Assert.False(result.HasUniqueRecommendation);
            Assert.Null(result.Recommendation);
            Assert.Equal(2, result.Contenders.Count);
            Assert.Contains("undominated", result.Reason);
        }

        [Fact]
        public void RawDensityDoesNotRemoveCandidateFromFrontier()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(new[]
            {
                Candidate("ManyTasks", objectiveCount: 10),
                Candidate("FewTasks", objectiveCount: 2)
            });

            Assert.False(result.HasUniqueRecommendation);
            Assert.Equal(2, result.Contenders.Count);
        }

        [Fact]
        public void SingleCandidateIsReturnedWithoutInventedComparison()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(new[]
            {
                Candidate("Factory", nonRepeatable: 1)
            });

            Assert.True(result.HasUniqueRecommendation);
            Assert.Equal("Factory", result.Recommendation!.LocationId);
            Assert.Contains("Only one", result.Reason);
        }

        [Fact]
        public void ProgressionFocusCanCollapseMultiMapFrontierToFocusedRaid()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    FocusCandidate("Customs", "setup", unlocks: 2, missing: 1),
                    FocusCandidate("Reserve", "reserve-main", unlocks: 0, missing: 0),
                    FocusCandidate("Woods", "woods-main", unlocks: 1, missing: 0)
                },
                new PlannerRaidDecisionIntent("setup"));

            Assert.True(result.HasUniqueRecommendation);
            Assert.NotNull(result.Recommendation);
            Assert.Equal("Customs", result.Recommendation!.LocationId);
            Assert.Contains("progression focus", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MultipleFocusedMapsRemainHonestFrontierWhenGoalSpecificTradeoffsConflict()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "future-goal",
                new[] { "shared-focus", "next", "future-goal" },
                new[] { "shared-focus" },
                new[] { "shared-focus" },
                Array.Empty<string>());

            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    FocusCandidate("Customs", "shared-focus", unlocks: 1, missing: 1, unlockQuestId: "next"),
                    FocusCandidate("Shoreline", "shared-focus", unlocks: 0, missing: 0),
                    FocusCandidate("Reserve", "other", unlocks: 3, missing: 0, unlockQuestId: "unrelated")
                },
                intent);

            Assert.False(result.HasUniqueRecommendation);
            Assert.Equal(2, result.Contenders.Count);
            Assert.Contains(result.Contenders, value => value.LocationId == "Customs");
            Assert.Contains(result.Contenders, value => value.LocationId == "Shoreline");
            Assert.DoesNotContain(result.Contenders, value => value.LocationId == "Reserve");
            Assert.Contains("progression focus", result.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MissingFocusFallsBackToConservativeFrontier()
        {
            PlannerRaidDecisionSet result = PlannerRaidDecisionSetBuilder.Build(
                new[]
                {
                    FocusCandidate("Customs", "setup", unlocks: 2, missing: 1),
                    FocusCandidate("Reserve", "reserve-main", unlocks: 0, missing: 0)
                },
                new PlannerRaidDecisionIntent("not-in-current-raids"));

            Assert.False(result.HasUniqueRecommendation);
            Assert.Equal(2, result.Contenders.Count);
        }

        private static PlannerRaidDecisionCandidate FocusCandidate(
            string locationId,
            string questId,
            int unlocks = 0,
            int missing = 0,
            string unlockQuestId = null)
        {
            return new PlannerRaidDecisionCandidate(
                locationId,
                new PlannerRaidDecisionSignals(
                    1,
                    0,
                    Array.Empty<PlannerRaidActionOverlap>(),
                    unlocks,
                    missing,
                    0,
                    0,
                    1,
                    1d,
                    new[] { questId },
                    Array.Empty<string>(),
                    unlocks > 0 && !string.IsNullOrWhiteSpace(unlockQuestId)
                        ? new[] { unlockQuestId }
                        : Array.Empty<string>()));
        }

        private static PlannerRaidDecisionCandidate Candidate(
            string locationId,
            int nonRepeatable = 0,
            int repeatable = 0,
            int overlapGroups = 0,
            int maxOverlap = 0,
            int unlocks = 0,
            int missing = 0,
            int unresolved = 0,
            int unknown = 0,
            int objectiveCount = 0)
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
                    for (int q = 0; q < questIds.Length; q++) questIds[q] = locationId + "-q" + i + "-" + q;
                    overlaps[i] = new PlannerRaidActionOverlap("sig-" + i, PlannerRaidObjectiveKind.Kill, questIds, questIds.Length);
                }
            }

            return new PlannerRaidDecisionCandidate(
                locationId,
                new PlannerRaidDecisionSignals(
                    nonRepeatable,
                    repeatable,
                    overlaps,
                    unlocks,
                    missing,
                    unresolved,
                    unknown,
                    objectiveCount,
                    0d));
        }
    }
}

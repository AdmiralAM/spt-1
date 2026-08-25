using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionDeltaTests
    {
        [Fact]
        public void Compare_ExposesWhyRawDensityShouldNotBeEnough()
        {
            PlannerRaidDecisionSignals left = Signals(
                nonRepeatable: 2,
                overlapGroups: 1,
                maxOverlap: 2,
                unlocks: 1,
                missing: 0,
                unresolved: 0,
                repeatable: 0,
                objectiveCount: 2,
                unknown: 0);
            PlannerRaidDecisionSignals right = Signals(
                nonRepeatable: 1,
                overlapGroups: 0,
                maxOverlap: 0,
                unlocks: 0,
                missing: 0,
                unresolved: 0,
                repeatable: 0,
                objectiveCount: 9,
                unknown: 0);

            PlannerRaidDecisionDelta delta = PlannerRaidDecisionDeltaBuilder.Compare(left, right);

            Assert.True(delta.HasMeaningfulProvenDifference);
            Assert.Equal(1, delta.NonRepeatableQuestDelta);
            Assert.Equal(1, delta.OverlapGroupDelta);
            Assert.Equal(2, delta.MaxOverlapQuestDelta);
            Assert.Equal(1, delta.ImmediateUnlockDelta);
            Assert.DoesNotContain(delta.Evidence, value => value.Contains("objective", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Compare_CanRepresentNearTieWithoutInventingPreference()
        {
            PlannerRaidDecisionSignals left = Signals(1, 0, 0, 0, 0, 0, 0, 2, 0);
            PlannerRaidDecisionSignals right = Signals(1, 0, 0, 0, 0, 0, 0, 7, 0);

            PlannerRaidDecisionDelta delta = PlannerRaidDecisionDeltaBuilder.Compare(left, right);

            Assert.False(delta.HasMeaningfulProvenDifference);
            Assert.Empty(delta.Evidence);
        }

        [Fact]
        public void Compare_ReportsPreparationAndUncertaintyAsTradeoffs()
        {
            PlannerRaidDecisionSignals left = Signals(2, 1, 2, 2, 1, 1, 0, 3, 1);
            PlannerRaidDecisionSignals right = Signals(1, 0, 0, 0, 0, 0, 0, 3, 0);

            PlannerRaidDecisionDelta delta = PlannerRaidDecisionDeltaBuilder.Compare(left, right);

            Assert.True(delta.HasMeaningfulProvenDifference);
            Assert.Equal(1, delta.MissingPreparationDelta);
            Assert.Equal(1, delta.UnresolvedPreparationDelta);
            Assert.True(delta.EvidenceCoverageDelta < 0d);
            Assert.Contains(delta.Evidence, value => value.StartsWith("RIGHT:", StringComparison.Ordinal));
            Assert.Contains(delta.Evidence, value => value.StartsWith("LEFT:", StringComparison.Ordinal));
        }

        private static PlannerRaidDecisionSignals Signals(
            int nonRepeatable,
            int overlapGroups,
            int maxOverlap,
            int unlocks,
            int missing,
            int unresolved,
            int repeatable,
            int objectiveCount,
            int unknown)
        {
            PlannerRaidActionOverlap[] overlaps;
            if (overlapGroups <= 0)
            {
                overlaps = Array.Empty<PlannerRaidActionOverlap>();
            }
            else
            {
                overlaps = new PlannerRaidActionOverlap[overlapGroups];
                for (int i = 0; i < overlapGroups; i++)
                {
                    string[] questIds = new string[Math.Max(2, maxOverlap)];
                    for (int q = 0; q < questIds.Length; q++) questIds[q] = "q" + q;
                    overlaps[i] = new PlannerRaidActionOverlap("sig" + i, PlannerRaidObjectiveKind.Kill, questIds, questIds.Length);
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
                0d);
        }
    }
}

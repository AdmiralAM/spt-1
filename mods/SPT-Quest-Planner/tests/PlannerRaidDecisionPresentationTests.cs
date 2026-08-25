using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionPresentationTests
    {
        [Fact]
        public void UniqueFrontierBecomesBestNextRaid()
        {
            PlannerRaidDecisionCandidate customs = Candidate("customs", nonRepeatable: 2, overlap: true, unlocks: 1);
            PlannerRaidDecisionCandidate reserve = Candidate("reserve", nonRepeatable: 1, overlap: false, unlocks: 0);

            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(new[] { customs, reserve });
            PlannerRaidDecisionPresentation presentation = PlannerRaidDecisionPresentationBuilder.Build(set);

            Assert.Equal(PlannerRaidDecisionPresentationKind.BestNextRaid, presentation.Kind);
            Assert.NotNull(presentation.Primary);
            Assert.Equal("customs", presentation.Primary.LocationId);
            Assert.Equal("Best next raid", presentation.Headline);
        }

        [Fact]
        public void ConflictingFrontierBecomesSeveralGoodOptions()
        {
            PlannerRaidDecisionCandidate leverage = Candidate("customs", nonRepeatable: 2, overlap: true, unlocks: 2, missing: 1);
            PlannerRaidDecisionCandidate ready = Candidate("woods", nonRepeatable: 2, overlap: false, unlocks: 0, missing: 0);

            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(new[] { leverage, ready });
            PlannerRaidDecisionPresentation presentation = PlannerRaidDecisionPresentationBuilder.Build(set);

            Assert.Equal(PlannerRaidDecisionPresentationKind.SeveralGoodOptions, presentation.Kind);
            Assert.Null(presentation.Primary);
            Assert.Equal(2, presentation.Alternatives.Count);
            Assert.Equal("Several good options", presentation.Headline);
        }

        [Fact]
        public void EmptyCandidateSetBecomesNoMeaningfulRecommendation()
        {
            PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(Array.Empty<PlannerRaidDecisionCandidate>());
            PlannerRaidDecisionPresentation presentation = PlannerRaidDecisionPresentationBuilder.Build(set);

            Assert.Equal(PlannerRaidDecisionPresentationKind.NoRecommendation, presentation.Kind);
            Assert.Null(presentation.Primary);
            Assert.Equal("No meaningful recommendation", presentation.Headline);
        }

        private static PlannerRaidDecisionCandidate Candidate(
            string location,
            int nonRepeatable,
            bool overlap,
            int unlocks,
            int missing = 0)
        {
            PlannerRaidActionOverlap[] overlaps = overlap
                ? new[] { new PlannerRaidActionOverlap("kill|" + location + "|pmc", PlannerRaidObjectiveKind.Kill, new[] { "q1", "q2" }, 2) }
                : Array.Empty<PlannerRaidActionOverlap>();

            return new PlannerRaidDecisionCandidate(
                location,
                new PlannerRaidDecisionSignals(
                    nonRepeatable,
                    0,
                    overlaps,
                    unlocks,
                    missing,
                    0,
                    0,
                    Math.Max(1, nonRepeatable),
                    0,
                    nonRepeatableQuestIds: nonRepeatable >= 2 ? new[] { "q1", "q2" } : new[] { "q1" },
                    immediateUnlockQuestIds: unlocks > 0 ? new[] { "next" } : Array.Empty<string>()));
        }
    }
}

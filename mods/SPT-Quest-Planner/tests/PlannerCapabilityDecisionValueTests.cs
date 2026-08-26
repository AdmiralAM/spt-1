using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityDecisionValueTests
    {
        [Fact]
        public void PlainPrerequisiteLocationDoesNotProveKeepValue()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.BestNextRaid,
                    Explanation("customs"),
                    Array.Empty<PlannerRaidDecisionExplanation>(),
                    "Best next raid",
                    "Only the focused prerequisite is present."));

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.NavigationOnly, value.Kind);
            Assert.False(value.ProvesBeyondPrerequisiteNavigation);
            Assert.False(value.CountsTowardKeepCandidate);
        }

        [Fact]
        public void CrossQuestSynergyProvesDecisionChangingKeepValue()
        {
            PlannerRaidActionOverlap overlap = new PlannerRaidActionOverlap(
                "kill|customs|pmc",
                PlannerRaidObjectiveKind.Kill,
                new[] { "focused", "side" },
                2);
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.BestNextRaid,
                    Explanation("customs", overlaps: new[] { overlap }),
                    Array.Empty<PlannerRaidDecisionExplanation>(),
                    "Best next raid",
                    "Shared action changes the preferred raid."));

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.DecisionChanged, value.Kind);
            Assert.True(value.ProvesBeyondPrerequisiteNavigation);
            Assert.True(value.CountsTowardKeepCandidate);
            Assert.Contains(value.Evidence, item => item.Contains("combines compatible work", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void PreparationDifferenceAcrossFrontierClarifiesKeepWorthyTradeoff()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.SeveralGoodOptions,
                    null,
                    new[]
                    {
                        Explanation("customs", preparationReady: false, missing: 1),
                        Explanation("woods", preparationReady: true)
                    },
                    "Several good options",
                    "One option has lower preparation friction."));

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.TradeoffClarified, value.Kind);
            Assert.True(value.ProvesBeyondPrerequisiteNavigation);
            Assert.True(value.CountsTowardKeepCandidate);
        }

        [Fact]
        public void EvidenceCoverageDifferenceAloneDoesNotCountAsKeepWorthyTradeoff()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.SeveralGoodOptions,
                    null,
                    new[]
                    {
                        Explanation("customs", evidenceCoverage: 1d),
                        Explanation("woods", evidenceCoverage: 0.5d)
                    },
                    "Several good options",
                    "One option has less complete evidence."));

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.NavigationOnly, value.Kind);
            Assert.False(value.CountsTowardKeepCandidate);
        }

        [Fact]
        public void UnresolvedPreparationDifferenceAloneDoesNotCountAsKeepWorthyTradeoff()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.SeveralGoodOptions,
                    null,
                    new[]
                    {
                        Explanation("customs", unresolved: 1),
                        Explanation("woods")
                    },
                    "Several good options",
                    "One option has unresolved preparation evidence."));

            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.NavigationOnly, value.Kind);
            Assert.False(value.CountsTowardKeepCandidate);
        }

        [Fact]
        public void WaitingStateCountsAsAvoidingUnnecessaryRaidAndKeepEvidence()
        {
            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(
                Presentation(PlannerCapabilityGoalPresentationKind.WaitingForAvailability, null));

            Assert.Equal(PlannerCapabilityDecisionValueKind.UnnecessaryRaidAvoided, value.Kind);
            Assert.True(value.ProvesBeyondPrerequisiteNavigation);
            Assert.True(value.CountsTowardKeepCandidate);
        }

        [Fact]
        public void CompletedCapabilityIsCorrectButDoesNotByItselfJustifyKeepingMod()
        {
            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(
                Presentation(PlannerCapabilityGoalPresentationKind.CapabilityAlreadyUnlocked, null));

            Assert.Equal(PlannerCapabilityDecisionValueKind.GoalAlreadyResolved, value.Kind);
            Assert.True(value.ProvesBeyondPrerequisiteNavigation);
            Assert.False(value.CountsTowardKeepCandidate);
        }

        [Theory]
        [InlineData(PlannerCapabilityGoalPresentationKind.EvidenceIncomplete)]
        [InlineData(PlannerCapabilityGoalPresentationKind.ProgressionConflict)]
        public void UnsupportedRoutePreventionIsCorrectnessNotKeepProof(PlannerCapabilityGoalPresentationKind kind)
        {
            PlannerCapabilityDecisionValue value = PlannerCapabilityDecisionValueClassifier.Classify(Presentation(kind, null));

            Assert.Equal(PlannerCapabilityDecisionValueKind.UnsupportedDecisionPrevented, value.Kind);
            Assert.True(value.ProvesBeyondPrerequisiteNavigation);
            Assert.False(value.CountsTowardKeepCandidate);
        }

        private static PlannerCapabilityGoalPresentation Presentation(
            PlannerCapabilityGoalPresentationKind kind,
            PlannerRaidDecisionPresentation raid)
        {
            return new PlannerCapabilityGoalPresentation(
                kind,
                "capability",
                "gate",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                raid,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                "Unlocks bounded access.",
                string.Empty);
        }

        private static PlannerRaidDecisionExplanation Explanation(
            string location,
            PlannerRaidActionOverlap[] overlaps = null,
            string[] unlocks = null,
            bool preparationReady = true,
            int missing = 0,
            int unresolved = 0,
            double evidenceCoverage = 1d)
        {
            return new PlannerRaidDecisionExplanation(
                location,
                new[] { "focused" },
                overlaps ?? Array.Empty<PlannerRaidActionOverlap>(),
                unlocks ?? Array.Empty<string>(),
                preparationReady,
                missing,
                unresolved,
                evidenceCoverage,
                Array.Empty<string>());
        }
    }
}

using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityDecisionSnapshotTests
    {
        [Fact]
        public void SnapshotCarriesPrimaryRaidAndKeepValueWithoutLeaderboardSemantics()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.BestNextRaid,
                    Explanation("customs", synergy: true),
                    new[] { Explanation("reserve") },
                    "Best next raid",
                    "Customs advances the selected goal with shared-action synergy."),
                new[] { "fieldwork" });

            PlannerCapabilityDecisionSnapshot snapshot = PlannerCapabilityDecisionSnapshotBuilder.Build(presentation);

            Assert.Equal("assault-rifles", snapshot.CapabilityId);
            Assert.Equal("gate", snapshot.GateQuestId);
            Assert.Equal("customs", snapshot.PrimaryLocationId);
            Assert.Equal(new[] { "reserve" }, snapshot.AlternativeLocationIds);
            Assert.Equal(PlannerCapabilityDecisionValueKind.DecisionChanged, snapshot.DecisionValue);
            Assert.True(snapshot.CountsTowardKeepCandidate);
            Assert.DoesNotContain("#1", snapshot.DecisionEvidence);
        }

        [Fact]
        public void WaitingSnapshotCarriesNoRaidAndStillCountsAsDecisionValue()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.WaitingForAvailability,
                null,
                Array.Empty<string>(),
                waiting: new[] { "delay-node" },
                caution: "No raid action is required for the waiting prerequisite branch right now.");

            PlannerCapabilityDecisionSnapshot snapshot = PlannerCapabilityDecisionSnapshotBuilder.Build(presentation);

            Assert.False(snapshot.HasPrimaryRaid);
            Assert.False(snapshot.HasAlternatives);
            Assert.Equal(new[] { "delay-node" }, snapshot.WaitingQuestIds);
            Assert.Equal(PlannerCapabilityDecisionValueKind.UnnecessaryRaidAvoided, snapshot.DecisionValue);
            Assert.True(snapshot.CountsTowardKeepCandidate);
        }

        [Fact]
        public void NavigationOnlySnapshotDoesNotBecomeKeepEvidence()
        {
            PlannerCapabilityGoalPresentation presentation = Presentation(
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.BestNextRaid,
                    Explanation("woods"),
                    Array.Empty<PlannerRaidDecisionExplanation>(),
                    "Best next raid",
                    "Woods contains the prerequisite."),
                new[] { "prerequisite" });

            PlannerCapabilityDecisionSnapshot snapshot = PlannerCapabilityDecisionSnapshotBuilder.Build(presentation);

            Assert.Equal(PlannerCapabilityDecisionValueKind.NavigationOnly, snapshot.DecisionValue);
            Assert.False(snapshot.CountsTowardKeepCandidate);
        }

        private static PlannerCapabilityGoalPresentation Presentation(
            PlannerCapabilityGoalPresentationKind kind,
            PlannerRaidDecisionPresentation raid,
            string[] actionable,
            string[] waiting = null,
            string caution = "")
        {
            return new PlannerCapabilityGoalPresentation(
                kind,
                "assault-rifles",
                "gate",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                raid,
                actionable,
                waiting ?? Array.Empty<string>(),
                Array.Empty<string>(),
                "Unlocks a bounded renewable capability (80 units/reset).",
                caution);
        }

        private static PlannerRaidDecisionExplanation Explanation(string locationId, bool synergy = false)
        {
            return new PlannerRaidDecisionExplanation(
                locationId,
                new[] { "quest" },
                synergy
                    ? new[]
                    {
                        new PlannerRaidActionOverlap(
                            "kill|pmc",
                            PlannerRaidObjectiveKind.Kill,
                            new[] { "quest", "side" },
                            2)
                    }
                    : Array.Empty<PlannerRaidActionOverlap>(),
                Array.Empty<string>(),
                true,
                0,
                0,
                1d,
                Array.Empty<string>());
        }
    }
}

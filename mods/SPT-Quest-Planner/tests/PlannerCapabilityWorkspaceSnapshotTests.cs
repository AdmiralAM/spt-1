using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityWorkspaceSnapshotTests
    {
        [Fact]
        public void SelectedDecisionIsComposedWithoutLeaderboardSemantics()
        {
            PlannerCapabilityGoalCatalog catalog = new PlannerCapabilityGoalCatalog(
                new[]
                {
                    Item("ammo", "gate-ammo", PlannerCapabilityGoalCatalogState.Actionable, PlannerCapabilitySupplyKind.BoundedRenewable),
                    Item("labs", "gate-labs", PlannerCapabilityGoalCatalogState.Waiting, PlannerCapabilitySupplyKind.BoundedRenewable)
                },
                new[]
                {
                    Item("done", "gate-done", PlannerCapabilityGoalCatalogState.AlreadyUnlocked, PlannerCapabilitySupplyKind.OneTimeSample)
                });
            PlannerCapabilityDecisionSnapshot decision = new PlannerCapabilityDecisionSnapshot(
                "ammo",
                "gate-ammo",
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                PlannerCapabilityDecisionValueKind.DecisionChanged,
                true,
                "customs",
                new[] { "woods" },
                new[] { "fieldwork" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                "Unlocks bounded supply.",
                string.Empty,
                new[] { "Focused shared-action synergy changes the preferred raid." },
                42,
                123456);

            PlannerCapabilityWorkspaceSnapshot snapshot = PlannerCapabilityWorkspaceSnapshotBuilder.Build(
                catalog,
                "ammo",
                decision);

            Assert.True(snapshot.HasSelection);
            Assert.True(snapshot.HasSelectedDecision);
            Assert.Equal("ammo", snapshot.SelectedCapabilityId);
            Assert.Equal("customs", snapshot.SelectedDecision.PrimaryLocationId);
            Assert.Contains(snapshot.OpenGoals, value => value.CapabilityId == "ammo" && value.Selected);
            Assert.Contains(snapshot.OpenGoals, value => value.CapabilityId == "labs" && !value.Selected);
            Assert.Contains(snapshot.UnlockedGoals, value => value.CapabilityId == "done" && !value.Selected);
        }

        [Fact]
        public void ExplicitSelectionMayExistBeforeDecisionIsComputed()
        {
            PlannerCapabilityGoalCatalog catalog = new PlannerCapabilityGoalCatalog(
                new[] { Item("labs", "gate-labs", PlannerCapabilityGoalCatalogState.Waiting, PlannerCapabilitySupplyKind.BoundedRenewable) },
                Array.Empty<PlannerCapabilityGoalCatalogItem>());

            PlannerCapabilityWorkspaceSnapshot snapshot = PlannerCapabilityWorkspaceSnapshotBuilder.Build(catalog, "labs");

            Assert.True(snapshot.HasSelection);
            Assert.False(snapshot.HasSelectedDecision);
            Assert.Contains(snapshot.OpenGoals, value => value.CapabilityId == "labs" && value.Selected);
        }

        [Fact]
        public void MissingSelectedCapabilityFailsClosedInsteadOfAutoSelectingFirstGoal()
        {
            PlannerCapabilityGoalCatalog catalog = new PlannerCapabilityGoalCatalog(
                new[] { Item("ammo", "gate-ammo", PlannerCapabilityGoalCatalogState.Actionable, PlannerCapabilitySupplyKind.BoundedRenewable) },
                Array.Empty<PlannerCapabilityGoalCatalogItem>());

            Assert.Throws<InvalidOperationException>(() =>
                PlannerCapabilityWorkspaceSnapshotBuilder.Build(catalog, "missing"));
        }

        [Fact]
        public void DecisionForDifferentCapabilityFailsClosed()
        {
            PlannerCapabilityGoalCatalog catalog = new PlannerCapabilityGoalCatalog(
                new[] { Item("ammo", "gate-ammo", PlannerCapabilityGoalCatalogState.Actionable, PlannerCapabilitySupplyKind.BoundedRenewable) },
                Array.Empty<PlannerCapabilityGoalCatalogItem>());
            PlannerCapabilityDecisionSnapshot decision = new PlannerCapabilityDecisionSnapshot(
                "labs",
                "gate-labs",
                PlannerCapabilityGoalPresentationKind.RaidDecision,
                PlannerCapabilityDecisionValueKind.NavigationOnly,
                false,
                "customs",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                string.Empty,
                string.Empty,
                Array.Empty<string>());

            Assert.Throws<InvalidOperationException>(() =>
                PlannerCapabilityWorkspaceSnapshotBuilder.Build(catalog, "ammo", decision));
        }

        [Fact]
        public void NoSelectionDoesNotSilentlyPromoteAlphabeticalGoal()
        {
            PlannerCapabilityGoalCatalog catalog = new PlannerCapabilityGoalCatalog(
                new[]
                {
                    Item("ammo", "gate-ammo", PlannerCapabilityGoalCatalogState.Actionable, PlannerCapabilitySupplyKind.BoundedRenewable),
                    Item("labs", "gate-labs", PlannerCapabilityGoalCatalogState.Waiting, PlannerCapabilitySupplyKind.BoundedRenewable)
                },
                Array.Empty<PlannerCapabilityGoalCatalogItem>());

            PlannerCapabilityWorkspaceSnapshot snapshot = PlannerCapabilityWorkspaceSnapshotBuilder.Build(catalog, null);

            Assert.False(snapshot.HasSelection);
            Assert.False(snapshot.HasSelectedDecision);
            Assert.DoesNotContain(snapshot.OpenGoals, value => value.Selected);
        }

        private static PlannerCapabilityGoalCatalogItem Item(
            string capabilityId,
            string gateQuestId,
            PlannerCapabilityGoalCatalogState state,
            PlannerCapabilitySupplyKind supplyKind)
        {
            int? limit = supplyKind == PlannerCapabilitySupplyKind.BoundedRenewable ? 80 : null;
            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                capabilityId,
                gateQuestId,
                "test",
                supplyKind,
                null,
                limit,
                limit,
                "fixture");
            return new PlannerCapabilityGoalCatalogItem(definition, state, 0, 0, 0);
        }
    }
}

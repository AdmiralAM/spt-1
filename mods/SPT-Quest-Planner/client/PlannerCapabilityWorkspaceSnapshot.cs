using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerCapabilityWorkspaceGoal
    {
        public PlannerCapabilityWorkspaceGoal(
            string capabilityId,
            string gateQuestId,
            PlannerCapabilityGoalCatalogState state,
            PlannerCapabilitySupplyKind supplyKind,
            bool selected)
        {
            CapabilityId = capabilityId ?? string.Empty;
            GateQuestId = gateQuestId ?? string.Empty;
            State = state;
            SupplyKind = supplyKind;
            Selected = selected;
        }

        public string CapabilityId { get; private set; }
        public string GateQuestId { get; private set; }
        public PlannerCapabilityGoalCatalogState State { get; private set; }
        public PlannerCapabilitySupplyKind SupplyKind { get; private set; }
        public bool Selected { get; private set; }
    }

    public sealed class PlannerCapabilityWorkspaceSnapshot
    {
        public PlannerCapabilityWorkspaceSnapshot(
            IReadOnlyList<PlannerCapabilityWorkspaceGoal> openGoals,
            IReadOnlyList<PlannerCapabilityWorkspaceGoal> unlockedGoals,
            string selectedCapabilityId,
            PlannerCapabilityDecisionSnapshot selectedDecision)
        {
            OpenGoals = openGoals ?? Array.Empty<PlannerCapabilityWorkspaceGoal>();
            UnlockedGoals = unlockedGoals ?? Array.Empty<PlannerCapabilityWorkspaceGoal>();
            SelectedCapabilityId = selectedCapabilityId ?? string.Empty;
            SelectedDecision = selectedDecision;
        }

        public IReadOnlyList<PlannerCapabilityWorkspaceGoal> OpenGoals { get; private set; }
        public IReadOnlyList<PlannerCapabilityWorkspaceGoal> UnlockedGoals { get; private set; }
        public string SelectedCapabilityId { get; private set; }
        public PlannerCapabilityDecisionSnapshot SelectedDecision { get; private set; }
        public bool HasSelection { get { return !string.IsNullOrWhiteSpace(SelectedCapabilityId); } }
        public bool HasSelectedDecision { get { return SelectedDecision != null; } }
    }

    public static class PlannerCapabilityWorkspaceSnapshotBuilder
    {
        public static PlannerCapabilityWorkspaceSnapshot Build(
            PlannerCapabilityGoalCatalog catalog,
            string selectedCapabilityId,
            PlannerCapabilityDecisionSnapshot selectedDecision = null)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");

            string selected = string.IsNullOrWhiteSpace(selectedCapabilityId)
                ? string.Empty
                : selectedCapabilityId.Trim();

            PlannerCapabilityGoalCatalogItem selectedItem = null;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                selectedItem = Find(catalog.OpenGoals, selected) ?? Find(catalog.UnlockedGoals, selected);
                if (selectedItem == null)
                    throw new InvalidOperationException("Selected capability is absent from the bounded capability catalog: " + selected);
            }

            if (selectedDecision != null)
            {
                if (selectedItem == null)
                    throw new InvalidOperationException("A selected capability decision requires an explicit selected capability.");
                if (!string.Equals(selectedDecision.CapabilityId, selected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Selected capability decision does not match the selected capability ID.");
                if (!string.Equals(selectedDecision.GateQuestId, selectedItem.Definition.GateQuestId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Selected capability decision gate does not match the catalog definition.");
            }

            return new PlannerCapabilityWorkspaceSnapshot(
                Project(catalog.OpenGoals, selected),
                Project(catalog.UnlockedGoals, selected),
                selected,
                selectedDecision);
        }

        private static PlannerCapabilityGoalCatalogItem Find(
            IReadOnlyList<PlannerCapabilityGoalCatalogItem> items,
            string capabilityId)
        {
            for (int i = 0; i < items.Count; i++)
            {
                PlannerCapabilityGoalCatalogItem item = items[i];
                if (item != null && string.Equals(item.Definition.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private static IReadOnlyList<PlannerCapabilityWorkspaceGoal> Project(
            IReadOnlyList<PlannerCapabilityGoalCatalogItem> items,
            string selectedCapabilityId)
        {
            if (items == null || items.Count == 0) return Array.Empty<PlannerCapabilityWorkspaceGoal>();
            return items
                .Where(item => item != null)
                .Select(item => new PlannerCapabilityWorkspaceGoal(
                    item.Definition.CapabilityId,
                    item.Definition.GateQuestId,
                    item.State,
                    item.Definition.SupplyKind,
                    !string.IsNullOrWhiteSpace(selectedCapabilityId) &&
                    string.Equals(item.Definition.CapabilityId, selectedCapabilityId, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
    }
}

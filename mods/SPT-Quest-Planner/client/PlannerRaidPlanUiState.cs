using System;

namespace SPTQuestPlanner.Client
{
    public enum PlannerWorkspaceMode
    {
        RaidPlanner = 0,
        Progression = 1
    }

    public sealed class PlannerRaidPlanUiState
    {
        private string selectedLocationId;
        private string activeLocationId;
        private string progressionTargetQuestId;
        private PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst;
        private PlannerWorkspaceMode workspaceMode = PlannerWorkspaceMode.RaidPlanner;
        private bool includeAvailable;

        public event Action Changed;

        public string SelectedLocationId { get { return selectedLocationId; } }
        public string ActiveLocationId { get { return activeLocationId; } }
        public string ProgressionTargetQuestId { get { return progressionTargetQuestId; } }
        public bool HasActivePlan { get { return !string.IsNullOrWhiteSpace(activeLocationId); } }
        public bool HasProgressionTarget { get { return !string.IsNullOrWhiteSpace(progressionTargetQuestId); } }
        public PlannerRaidPlanRankingMode RankingMode { get { return rankingMode; } }
        public PlannerWorkspaceMode WorkspaceMode { get { return workspaceMode; } }
        public bool IncludeAvailable { get { return includeAvailable; } }

        public void RestoreDurableState(
            string activeLocation,
            string progressionTarget,
            PlannerRaidPlanRankingMode restoredRankingMode,
            PlannerWorkspaceMode restoredWorkspaceMode,
            bool restoredIncludeAvailable)
        {
            activeLocationId = NormalizeId(activeLocation);
            progressionTargetQuestId = NormalizeId(progressionTarget);
            rankingMode = restoredRankingMode;
            workspaceMode = restoredWorkspaceMode;
            includeAvailable = restoredIncludeAvailable;

            if (workspaceMode == PlannerWorkspaceMode.Progression && !HasProgressionTarget)
                workspaceMode = PlannerWorkspaceMode.RaidPlanner;
            if (workspaceMode == PlannerWorkspaceMode.RaidPlanner && HasActivePlan)
                selectedLocationId = activeLocationId;
        }

        public void SetRankingMode(PlannerRaidPlanRankingMode value)
        {
            if (rankingMode == value) return;
            rankingMode = value;
            NotifyChanged();
        }

        public void SetWorkspaceMode(PlannerWorkspaceMode value)
        {
            if (workspaceMode == value) return;
            workspaceMode = value;
            NotifyChanged();
        }

        public void SetIncludeAvailable(bool value)
        {
            if (includeAvailable == value) return;
            includeAvailable = value;
            NotifyChanged();
        }

        public void SelectLocation(string locationId)
        {
            selectedLocationId = NormalizeId(locationId);
        }

        public void ActivateSelected()
        {
            if (!string.IsNullOrWhiteSpace(selectedLocationId)) ActivateLocation(selectedLocationId);
        }

        public void ActivateLocation(string locationId)
        {
            string normalized = NormalizeId(locationId);
            bool changed = !string.Equals(activeLocationId, normalized, StringComparison.Ordinal) ||
                           workspaceMode != PlannerWorkspaceMode.RaidPlanner;
            activeLocationId = normalized;
            if (!string.IsNullOrWhiteSpace(activeLocationId))
            {
                selectedLocationId = activeLocationId;
                workspaceMode = PlannerWorkspaceMode.RaidPlanner;
            }
            if (changed) NotifyChanged();
        }

        public void ClearActivePlan()
        {
            if (activeLocationId == null) return;
            activeLocationId = null;
            NotifyChanged();
        }

        public void SelectProgressionTarget(string questId)
        {
            string normalized = NormalizeId(questId);
            bool changed = !string.Equals(progressionTargetQuestId, normalized, StringComparison.Ordinal) ||
                           (!string.IsNullOrWhiteSpace(normalized) && workspaceMode != PlannerWorkspaceMode.Progression);
            progressionTargetQuestId = normalized;
            if (!string.IsNullOrWhiteSpace(progressionTargetQuestId)) workspaceMode = PlannerWorkspaceMode.Progression;
            if (changed) NotifyChanged();
        }

        public void ClearProgressionTarget()
        {
            if (progressionTargetQuestId == null) return;
            progressionTargetQuestId = null;
            if (workspaceMode == PlannerWorkspaceMode.Progression) workspaceMode = PlannerWorkspaceMode.RaidPlanner;
            NotifyChanged();
        }

        public PlannerRaidPlanCard ResolveSelection(PlannerRaidPlanViewModel viewModel)
        {
            if (viewModel == null || viewModel.Cards.Count == 0)
            {
                selectedLocationId = null;
                return null;
            }

            if (!string.IsNullOrWhiteSpace(selectedLocationId))
            {
                for (int i = 0; i < viewModel.Cards.Count; i++)
                {
                    PlannerRaidPlanCard candidate = viewModel.Cards[i];
                    if (string.Equals(candidate.LocationId, selectedLocationId, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }

            PlannerRaidPlanCard fallback = viewModel.Cards[0];
            selectedLocationId = fallback.LocationId;
            return fallback;
        }

        public PlannerRaidPlanCard ResolveActivePlan(PlannerRaidPlanViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(activeLocationId)) return null;
            if (viewModel == null || viewModel.Cards.Count == 0)
            {
                activeLocationId = null;
                NotifyChanged();
                return null;
            }
            for (int i = 0; i < viewModel.Cards.Count; i++)
            {
                PlannerRaidPlanCard candidate = viewModel.Cards[i];
                if (string.Equals(candidate.LocationId, activeLocationId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            activeLocationId = null;
            NotifyChanged();
            return null;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }

    public sealed class PlannerRaidPlanPresentationController
    {
        private readonly PlannerRaidPlanProvider provider;
        private readonly PlannerRaidPlanUiState uiState;
        private long cachedRevision = -1;
        private PlannerRaidPlanRankingMode cachedRankingMode;
        private bool cachedIncludeAvailable;
        private int cachedMaxObjectivesPerCard = -1;
        private PlannerRaidPlanViewModel cachedViewModel;

        public PlannerRaidPlanPresentationController(PlannerRaidPlanProvider provider, PlannerRaidPlanUiState uiState = null)
        {
            this.provider = provider ?? throw new ArgumentNullException("provider");
            this.uiState = uiState ?? new PlannerRaidPlanUiState();
        }

        public PlannerRaidPlanUiState UiState { get { return uiState; } }

        public PlannerRaidPlanViewModel GetViewModel(long cacheRevision, int maxObjectivesPerCard = 12)
        {
            if (maxObjectivesPerCard <= 0) throw new ArgumentOutOfRangeException("maxObjectivesPerCard");
            if (cachedViewModel != null && cachedRevision == cacheRevision &&
                cachedRankingMode == uiState.RankingMode &&
                cachedIncludeAvailable == uiState.IncludeAvailable &&
                cachedMaxObjectivesPerCard == maxObjectivesPerCard)
                return cachedViewModel;

            PlannerRaidPlanCollection collection = provider.Get(uiState.RankingMode, uiState.IncludeAvailable);
            cachedViewModel = PlannerRaidPlanViewModelBuilder.Build(collection, maxObjectivesPerCard);
            cachedRevision = cacheRevision;
            cachedRankingMode = uiState.RankingMode;
            cachedIncludeAvailable = uiState.IncludeAvailable;
            cachedMaxObjectivesPerCard = maxObjectivesPerCard;
            uiState.ResolveSelection(cachedViewModel);
            uiState.ResolveActivePlan(cachedViewModel);
            return cachedViewModel;
        }

        public PlannerRaidPlanCard GetSelectedCard(long cacheRevision, int maxObjectivesPerCard = 12)
        {
            return uiState.ResolveSelection(GetViewModel(cacheRevision, maxObjectivesPerCard));
        }

        public PlannerRaidPlanCard GetActiveCard(long cacheRevision, int maxObjectivesPerCard = 12)
        {
            return uiState.ResolveActivePlan(GetViewModel(cacheRevision, maxObjectivesPerCard));
        }

        public PlannerSelectionSnapshot GetSelectionSnapshot(long cacheRevision)
        {
            return new PlannerSelectionSnapshot(cacheRevision, uiState.ActiveLocationId, uiState.ProgressionTargetQuestId);
        }

        public PlannerActivePlanSnapshot GetActivePlanSnapshot(long cacheRevision, int maxObjectivesPerCard = 32)
        {
            PlannerRaidPlanCard active = GetActiveCard(cacheRevision, maxObjectivesPerCard);
            if (active == null) return PlannerActivePlanSnapshot.Empty(cacheRevision);
            return new PlannerActivePlanSnapshot(
                cacheRevision,
                active.LocationId,
                PlannerDisplayNames.Location(active.LocationId),
                active.QuestCount,
                active.ObjectiveCount,
                active.PreparationReady,
                active.MissingBringTemplateCount,
                active.UnresolvedPreparationCount,
                active.Objectives,
                active.BringNeeds);
        }

        public void Invalidate()
        {
            cachedRevision = -1;
            cachedMaxObjectivesPerCard = -1;
            cachedViewModel = null;
        }
    }
}

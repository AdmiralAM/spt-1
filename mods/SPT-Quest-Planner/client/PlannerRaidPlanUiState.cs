using System;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanUiState
    {
        private string selectedLocationId;
        private string activeLocationId;
        private PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst;
        private bool includeAvailable;

        public string SelectedLocationId { get { return selectedLocationId; } }
        public string ActiveLocationId { get { return activeLocationId; } }
        public bool HasActivePlan { get { return !string.IsNullOrWhiteSpace(activeLocationId); } }
        public PlannerRaidPlanRankingMode RankingMode { get { return rankingMode; } }
        public bool IncludeAvailable { get { return includeAvailable; } }

        public void SetRankingMode(PlannerRaidPlanRankingMode value) { rankingMode = value; }
        public void SetIncludeAvailable(bool value) { includeAvailable = value; }

        public void SelectLocation(string locationId)
        {
            selectedLocationId = string.IsNullOrWhiteSpace(locationId) ? null : locationId.Trim();
        }

        public void ActivateSelected()
        {
            if (!string.IsNullOrWhiteSpace(selectedLocationId)) activeLocationId = selectedLocationId;
        }

        public void ActivateLocation(string locationId)
        {
            activeLocationId = string.IsNullOrWhiteSpace(locationId) ? null : locationId.Trim();
            if (!string.IsNullOrWhiteSpace(activeLocationId)) selectedLocationId = activeLocationId;
        }

        public void ClearActivePlan()
        {
            activeLocationId = null;
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
            if (viewModel == null || viewModel.Cards.Count == 0 || string.IsNullOrWhiteSpace(activeLocationId)) return null;
            for (int i = 0; i < viewModel.Cards.Count; i++)
            {
                PlannerRaidPlanCard candidate = viewModel.Cards[i];
                if (string.Equals(candidate.LocationId, activeLocationId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
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

        public void Invalidate()
        {
            cachedRevision = -1;
            cachedMaxObjectivesPerCard = -1;
            cachedViewModel = null;
        }
    }
}

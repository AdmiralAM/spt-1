using System;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidPlanUiState
    {
        private string selectedLocationId;
        private PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst;
        private bool includeAvailable;

        public string SelectedLocationId { get { return selectedLocationId; } }
        public PlannerRaidPlanRankingMode RankingMode { get { return rankingMode; } }
        public bool IncludeAvailable { get { return includeAvailable; } }

        public void SetRankingMode(PlannerRaidPlanRankingMode value)
        {
            rankingMode = value;
        }

        public void SetIncludeAvailable(bool value)
        {
            includeAvailable = value;
        }

        public void SelectLocation(string locationId)
        {
            selectedLocationId = string.IsNullOrWhiteSpace(locationId) ? null : locationId.Trim();
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
    }

    public sealed class PlannerRaidPlanPresentationController
    {
        private readonly PlannerRaidPlanProvider provider;
        private readonly PlannerRaidPlanUiState uiState;
        private long cachedRevision = -1;
        private PlannerRaidPlanViewModel cachedViewModel;

        public PlannerRaidPlanPresentationController(PlannerRaidPlanProvider provider, PlannerRaidPlanUiState uiState = null)
        {
            this.provider = provider ?? throw new ArgumentNullException("provider");
            this.uiState = uiState ?? new PlannerRaidPlanUiState();
        }

        public PlannerRaidPlanUiState UiState { get { return uiState; } }

        public PlannerRaidPlanViewModel GetViewModel(long cacheRevision, int maxObjectivesPerCard = 12)
        {
            if (cachedViewModel != null && cachedRevision == cacheRevision &&
                cachedViewModel.RankingMode == uiState.RankingMode)
                return cachedViewModel;

            PlannerRaidPlanCollection collection = provider.Get(uiState.RankingMode, uiState.IncludeAvailable);
            cachedViewModel = PlannerRaidPlanViewModelBuilder.Build(collection, maxObjectivesPerCard);
            cachedRevision = cacheRevision;
            uiState.ResolveSelection(cachedViewModel);
            return cachedViewModel;
        }

        public PlannerRaidPlanCard GetSelectedCard(long cacheRevision, int maxObjectivesPerCard = 12)
        {
            return uiState.ResolveSelection(GetViewModel(cacheRevision, maxObjectivesPerCard));
        }

        public void Invalidate()
        {
            cachedRevision = -1;
            cachedViewModel = null;
        }
    }
}

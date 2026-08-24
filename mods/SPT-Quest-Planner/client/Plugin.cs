using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTQuestPlanner.Client
{
    [BepInPlugin("com.admiralam.spt.questplanner", "Quest planner MOD SPT", "0.9.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static readonly PlannerCandidatePolicy RuntimeRecommendationPolicy =
            new PlannerCandidatePolicy(includeActive: true, includeAvailable: true, includeReachable: false, includeBlocked: false);

        private CancellationTokenSource cancellation;
        private PlannerRefreshCoordinator refresh;
        private PlannerRefreshScheduler scheduler;
        private Task initialLoad;
        private PlannerRaidPlanWindow window;
        private ConfigEntry<KeyboardShortcut> toggleWindowKey;
        private ConfigEntry<string> activeRaidLocation;
        private ConfigEntry<string> progressionTargetQuest;
        private ConfigEntry<PlannerRaidPlanRankingMode> rankingMode;
        private ConfigEntry<PlannerWorkspaceMode> workspaceMode;
        private ConfigEntry<bool> includeAvailableQuests;
        private int plannerStateDirty;

        internal static PlannerClientCache Cache { get; private set; }
        internal static PlannerRaidPlanProvider RaidPlans { get; private set; }
        internal static PlannerRaidPlanPresentationController Presentation { get; private set; }
        internal static PlannerRecommendationProvider Recommendations { get; private set; }
        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Cache = new PlannerClientCache();
            RaidPlans = new PlannerRaidPlanProvider(Cache);

            PlannerRaidPlanUiState uiState = ConfigurePlannerState();
            Presentation = new PlannerRaidPlanPresentationController(RaidPlans, uiState);
            Recommendations = new PlannerRecommendationProvider(Cache);
            refresh = new PlannerRefreshCoordinator(
                new ReflectionSptPlannerTransport(),
                new ReflectionNewtonsoftPlannerDecoder(),
                Cache);
            scheduler = new PlannerRefreshScheduler(RequestStateRefreshImmediate, TimeSpan.FromMilliseconds(500));
            cancellation = new CancellationTokenSource();
            toggleWindowKey = Config.Bind(
                "UI",
                "Toggle window",
                new KeyboardShortcut(KeyCode.F9),
                "Open or close Quest Planner.");
            window = new PlannerRaidPlanWindow(Presentation, () => Cache == null ? 0L : Cache.Revision);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            StartInitialLoad();
            Logger.LogInfo("Quest planner MOD SPT v0.9.4 loaded.");
        }

        private PlannerRaidPlanUiState ConfigurePlannerState()
        {
            activeRaidLocation = Config.Bind(
                "Planner state",
                "Active raid location",
                string.Empty,
                "Persisted active raid plan location. Managed by Quest Planner UI.");
            progressionTargetQuest = Config.Bind(
                "Planner state",
                "Progression target quest",
                string.Empty,
                "Persisted progression target quest. Managed by Quest Planner UI.");
            rankingMode = Config.Bind(
                "Planner state",
                "Raid ranking",
                PlannerRaidPlanRankingMode.ReadyFirst,
                "Raid recommendation ranking preference.");
            workspaceMode = Config.Bind(
                "Planner state",
                "Workspace",
                PlannerWorkspaceMode.RaidPlanner,
                "Last meaningful Quest Planner workspace.");
            includeAvailableQuests = Config.Bind(
                "Planner state",
                "Include available quests",
                false,
                "Include available-but-not-active quests in raid planning.");

            PlannerRaidPlanUiState state = new PlannerRaidPlanUiState();
            state.RestoreDurableState(
                activeRaidLocation.Value,
                progressionTargetQuest.Value,
                rankingMode.Value,
                workspaceMode.Value,
                includeAvailableQuests.Value);
            state.Changed += OnPlannerStateChanged;
            return state;
        }

        private void OnPlannerStateChanged()
        {
            Interlocked.Exchange(ref plannerStateDirty, 1);
            AlignProgressionFocusRaidSelection();
        }

        private void PersistPlannerStateIfDirty()
        {
            if (Interlocked.Exchange(ref plannerStateDirty, 0) == 0) return;
            PersistPlannerState();
        }

        private void PersistPlannerState()
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerRaidPlanUiState state = controller == null ? null : controller.UiState;
            if (state == null) return;

            if (activeRaidLocation != null) activeRaidLocation.Value = state.ActiveLocationId ?? string.Empty;
            if (progressionTargetQuest != null) progressionTargetQuest.Value = state.ProgressionTargetQuestId ?? string.Empty;
            if (rankingMode != null) rankingMode.Value = state.RankingMode;
            if (workspaceMode != null) workspaceMode.Value = state.WorkspaceMode;
            if (includeAvailableQuests != null) includeAvailableQuests.Value = state.IncludeAvailable;
        }

        private void Update()
        {
            PersistPlannerStateIfDirty();

            ConfigEntry<KeyboardShortcut> key = toggleWindowKey;
            PlannerRaidPlanWindow value = window;
            if (key == null || value == null || !key.Value.IsDown()) return;

            value.Toggle();
            if (value.Visible)
                RequestStateRefresh("ui-open");
        }

        private void OnGUI()
        {
            PlannerRaidPlanWindow value = window;
            if (value == null) return;
            try
            {
                value.Draw();
            }
            catch (Exception ex)
            {
                value.Hide();
                Logger.LogError("Quest planner MOD SPT UI disabled after render failure: " + ex.GetBaseException().Message);
            }
        }

        internal static PlannerRaidPlanCollection GetRaidPlans(
            PlannerRaidPlanRankingMode rankingMode = PlannerRaidPlanRankingMode.ReadyFirst,
            bool includeAvailable = false)
        {
            PlannerRaidPlanProvider provider = RaidPlans;
            return provider == null
                ? new PlannerRaidPlanCollection(0L, rankingMode, Array.Empty<PlannerRaidPlan>())
                : provider.Get(rankingMode, includeAvailable);
        }

        internal static PlannerRaidPlanViewModel GetRaidPlanViewModel(int maxObjectivesPerCard = 12)
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            if (controller == null || cache == null)
                return new PlannerRaidPlanViewModel(0L, PlannerRaidPlanRankingMode.ReadyFirst, Array.Empty<PlannerRaidPlanCard>());
            return controller.GetViewModel(cache.Revision, maxObjectivesPerCard);
        }

        internal static PlannerRaidPlanCard GetRaidForProgressionTarget(int maxObjectivesPerCard = 32)
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            if (controller == null || cache == null || controller.UiState == null || !controller.UiState.HasProgressionTarget)
                return null;
            PlannerRaidPlanViewModel viewModel = controller.GetViewModel(cache.Revision, Math.Max(1, maxObjectivesPerCard));
            return viewModel.BestForQuest(controller.UiState.ProgressionTargetQuestId);
        }

        internal static PlannerActivePlanSnapshot GetActiveRaidPlan(int maxObjectives = 32)
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            if (controller == null || cache == null) return PlannerActivePlanSnapshot.Empty(0L);
            return controller.GetActivePlanSnapshot(cache.Revision, Math.Max(1, maxObjectives));
        }

        internal static PlannerSelectionSnapshot GetPlannerSelection()
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            if (controller == null || cache == null) return new PlannerSelectionSnapshot(0L, string.Empty, string.Empty);
            return controller.GetSelectionSnapshot(cache.Revision);
        }

        internal static PlannerRecommendationSnapshot GetRecommendations(int topN = 5, PlannerCandidatePolicy policy = null)
        {
            PlannerRecommendationProvider provider = Recommendations;
            return provider == null
                ? new PlannerRecommendationSnapshot(0L, 0L, Math.Max(1, topN), Array.Empty<PlannerRecommendationViewModel>())
                : provider.Get(topN, policy ?? RuntimeRecommendationPolicy);
        }

        private void StartInitialLoad()
        {
            CancellationTokenSource source = cancellation;
            PlannerRefreshCoordinator coordinator = refresh;
            PlannerClientCache cache = Cache;
            if (source == null || coordinator == null || cache == null) return;

            CancellationToken token = source.Token;
            initialLoad = Task.Run(() =>
            {
                string error;
                if (coordinator.TryRefreshState(token, out error))
                {
                    Logger.LogInfo("Quest Planner topology/state cache initialized; revision=" + cache.Revision + ".");
                    AlignPlannerSelectionsAfterRefresh();
                }
                else if (!token.IsCancellationRequested)
                    Logger.LogWarning("Quest Planner initial cache load failed: " + error);
            }, token);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            PlannerRefreshScheduler value = scheduler;
            if (value == null) return;
            value.Request("scene:" + SafeName(previous.name) + "->" + SafeName(next.name));
        }

        internal void RequestStateRefresh(string reason)
        {
            PlannerRefreshScheduler value = scheduler;
            if (value != null) value.Request(NormalizeReason(reason));
        }

        private void RequestStateRefreshImmediate(string reason)
        {
            PlannerRefreshCoordinator coordinator = refresh;
            CancellationTokenSource source = cancellation;
            PlannerClientCache cache = Cache;
            if (coordinator == null || source == null || cache == null || source.IsCancellationRequested) return;

            CancellationToken token = source.Token;
            Task.Run(() =>
            {
                string error;
                if (coordinator.TryRefreshState(token, out error))
                {
                    PlannerRaidPlanPresentationController presentation = Presentation;
                    if (presentation != null) presentation.Invalidate();
                    AlignPlannerSelectionsAfterRefresh();
                    if (!token.IsCancellationRequested)
                        Logger.LogDebug("Quest Planner state refreshed (" + NormalizeReason(reason) + "); revision=" + cache.Revision + ".");
                }
                else if (!token.IsCancellationRequested && !string.Equals(error, "Refresh already in progress.", StringComparison.Ordinal))
                    Logger.LogWarning("Quest Planner state refresh failed (" + NormalizeReason(reason) + "): " + error);
            }, token);
        }

        private static void AlignPlannerSelectionsAfterRefresh()
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            if (controller == null || cache == null) return;
            controller.GetActivePlanSnapshot(cache.Revision, 32);
            AlignProgressionFocusRaidSelection();
        }

        private static void AlignProgressionFocusRaidSelection()
        {
            PlannerRaidPlanPresentationController controller = Presentation;
            PlannerClientCache cache = Cache;
            PlannerRaidPlanUiState state = controller == null ? null : controller.UiState;
            if (controller == null || cache == null || state == null || !state.HasProgressionTarget) return;

            PlannerRaidPlanViewModel viewModel = controller.GetViewModel(cache.Revision, 32);
            PlannerRaidPlanCard focusedRaid = viewModel.BestForQuest(state.ProgressionTargetQuestId);
            if (focusedRaid != null) state.SelectLocation(focusedRaid.LocationId);
        }

        private static string NormalizeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            PersistPlannerStateIfDirty();
            PlannerRaidPlanPresentationController presentation = Presentation;
            if (presentation != null && presentation.UiState != null)
                presentation.UiState.Changed -= OnPlannerStateChanged;
            if (scheduler != null) scheduler.Dispose();
            if (cancellation != null) cancellation.Cancel();
            if (window != null) window.Hide();
            initialLoad = null;
            window = null;
            toggleWindowKey = null;
            activeRaidLocation = null;
            progressionTargetQuest = null;
            rankingMode = null;
            workspaceMode = null;
            includeAvailableQuests = null;
            scheduler = null;
            refresh = null;
            cancellation = null;
            Recommendations = null;
            Presentation = null;
            RaidPlans = null;
            Cache = null;
            Instance = null;
        }
    }
}

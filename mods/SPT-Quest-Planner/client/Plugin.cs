using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTQuestPlanner.Client
{
    [BepInPlugin("com.admiralam.spt.questplanner", "Quest planner MOD SPT", "0.9.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const float VisibleRefreshIntervalSeconds = 15f;

        private CancellationTokenSource cancellation;
        private PlannerRefreshCoordinator refresh;
        private PlannerRefreshScheduler scheduler;
        private Task initialLoad;
        private PlannerRaidPlanWindow window;
        private ConfigEntry<KeyboardShortcut> toggleWindowKey;
        private readonly VisibleRefreshGate visibleRefreshGate = new VisibleRefreshGate(VisibleRefreshIntervalSeconds);

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
            Presentation = new PlannerRaidPlanPresentationController(RaidPlans);
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
                "Open or close Quest planner MOD SPT raid-plan window.");
            window = new PlannerRaidPlanWindow(Presentation, () => Cache == null ? 0L : Cache.Revision);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            StartInitialLoad();
            Logger.LogInfo("Quest planner MOD SPT v0.9.0 loaded (raid-plan window + recommendations + bounded lifecycle refresh).");
        }

        private void Update()
        {
            PlannerRaidPlanWindow value = window;
            if (value == null) return;

            ConfigEntry<KeyboardShortcut> key = toggleWindowKey;
            if (key != null && key.Value.IsDown())
            {
                value.Toggle();
                if (value.Visible)
                {
                    RequestStateRefresh("ui-open");
                    visibleRefreshGate.Open(Time.unscaledTime);
                }
                else
                {
                    visibleRefreshGate.Close();
                }
            }

            if (!value.Visible) return;
            if (visibleRefreshGate.ShouldRefresh(Time.unscaledTime))
                RequestStateRefresh("ui-visible-cadence");
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
                // A malformed custom quest must not turn a presentation exception into
                // repeated OnGUI failures every event/frame. Close the window and log once.
                value.Hide();
                visibleRefreshGate.Close();
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

        internal static PlannerRecommendationSnapshot GetRecommendations(int topN = 5, PlannerCandidatePolicy policy = null)
        {
            PlannerRecommendationProvider provider = Recommendations;
            return provider == null
                ? new PlannerRecommendationSnapshot(0L, 0L, Math.Max(1, topN), Array.Empty<PlannerRecommendationViewModel>())
                : provider.Get(topN, policy);
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
                    LogRecommendationSummary("initial-load");
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
                    if (!token.IsCancellationRequested)
                    {
                        Logger.LogInfo("Quest Planner state refreshed (" + NormalizeReason(reason) + "); revision=" + cache.Revision + ".");
                        LogRecommendationSummary(reason);
                    }
                }
                else if (!token.IsCancellationRequested && !string.Equals(error, "Refresh already in progress.", StringComparison.Ordinal))
                    Logger.LogWarning("Quest Planner state refresh failed (" + NormalizeReason(reason) + "): " + error);
            }, token);
        }

        private void LogRecommendationSummary(string reason)
        {
            try
            {
                PlannerRecommendationProvider provider = Recommendations;
                if (provider == null) return;
                PlannerRecommendationSnapshot snapshot = provider.Get(3);
                if (snapshot.Recommendations.Count == 0)
                {
                    Logger.LogInfo("Quest Planner recommendations (" + NormalizeReason(reason) + "): no current candidates.");
                    return;
                }

                for (int i = 0; i < snapshot.Recommendations.Count; i++)
                {
                    PlannerRecommendationViewModel value = snapshot.Recommendations[i];
                    Logger.LogInfo(
                        "Quest Planner recommendation #" + value.Rank + ": " + value.QuestName +
                        " | blockers=" + value.ImmediateBlockerCount +
                        " | path=" + value.PathQuestCount +
                        " | missing=" + Math.Round(value.TotalOutstanding, 2) +
                        " | FIR=" + Math.Round(value.FirOutstanding, 2) +
                        " | unlocks=" + value.ImmediateUnlockCount +
                        " | owned=" + value.FullyOwned + ".");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Quest Planner recommendation summary unavailable: " + ex.GetBaseException().Message);
            }
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
            if (scheduler != null) scheduler.Dispose();
            if (cancellation != null) cancellation.Cancel();
            if (window != null) window.Hide();
            visibleRefreshGate.Close();
            initialLoad = null;
            window = null;
            toggleWindowKey = null;
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

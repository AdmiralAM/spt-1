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
        private CancellationTokenSource cancellation;
        private PlannerRefreshCoordinator refresh;
        private PlannerRefreshScheduler scheduler;
        private Task initialLoad;
        private PlannerRaidPlanWindow window;
        private ConfigEntry<KeyboardShortcut> toggleWindowKey;

        internal static PlannerClientCache Cache { get; private set; }
        internal static PlannerRaidPlanProvider RaidPlans { get; private set; }
        internal static PlannerRaidPlanPresentationController Presentation { get; private set; }
        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Cache = new PlannerClientCache();
            RaidPlans = new PlannerRaidPlanProvider(Cache);
            Presentation = new PlannerRaidPlanPresentationController(RaidPlans);
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
            Logger.LogInfo("Quest planner MOD SPT v0.9.0 loaded (raid-plan window + bounded lifecycle refresh).");
        }

        private void Update()
        {
            ConfigEntry<KeyboardShortcut> key = toggleWindowKey;
            PlannerRaidPlanWindow value = window;
            if (key != null && value != null && key.Value.IsDown()) value.Toggle();
        }

        private void OnGUI()
        {
            PlannerRaidPlanWindow value = window;
            if (value != null) value.Draw();
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

        private void StartInitialLoad()
        {
            CancellationToken token = cancellation.Token;
            initialLoad = Task.Run(() =>
            {
                string error;
                if (refresh.TryRefreshState(token, out error))
                    Logger.LogInfo("Quest Planner topology/state cache initialized; revision=" + Cache.Revision + ".");
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
            if (coordinator == null || source == null || source.IsCancellationRequested) return;

            CancellationToken token = source.Token;
            Task.Run(() =>
            {
                string error;
                if (coordinator.TryRefreshState(token, out error))
                {
                    PlannerRaidPlanPresentationController presentation = Presentation;
                    if (presentation != null) presentation.Invalidate();
                    Logger.LogInfo("Quest Planner state refreshed (" + NormalizeReason(reason) + "); revision=" + Cache.Revision + ".");
                }
                else if (!token.IsCancellationRequested && !string.Equals(error, "Refresh already in progress.", StringComparison.Ordinal))
                    Logger.LogWarning("Quest Planner state refresh failed (" + NormalizeReason(reason) + "): " + error);
            }, token);
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
            initialLoad = null;
            window = null;
            toggleWindowKey = null;
            scheduler = null;
            refresh = null;
            cancellation = null;
            Presentation = null;
            RaidPlans = null;
            Cache = null;
            Instance = null;
        }
    }
}

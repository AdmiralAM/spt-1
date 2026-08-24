using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine.SceneManagement;

namespace SPTQuestPlanner.Client
{
    [BepInPlugin("com.admiralam.spt.questplanner", "SPT Quest Planner", "0.9.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private CancellationTokenSource cancellation;
        private PlannerRefreshCoordinator refresh;
        private PlannerRefreshScheduler scheduler;
        private Task initialLoad;

        internal static PlannerClientCache Cache { get; private set; }
        internal static PlannerRaidPlanProvider RaidPlans { get; private set; }
        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Cache = new PlannerClientCache();
            RaidPlans = new PlannerRaidPlanProvider(Cache);
            refresh = new PlannerRefreshCoordinator(
                new ReflectionSptPlannerTransport(),
                new ReflectionNewtonsoftPlannerDecoder(),
                Cache);
            scheduler = new PlannerRefreshScheduler(RequestStateRefreshImmediate, TimeSpan.FromMilliseconds(500));
            cancellation = new CancellationTokenSource();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            StartInitialLoad();
            Logger.LogInfo("SPT Quest Planner v0.9.0 loaded (raid-plan provider + bounded lifecycle refresh; no UI)");
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
                    Logger.LogInfo("Quest Planner state refreshed (" + NormalizeReason(reason) + "); revision=" + Cache.Revision + ".");
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
            initialLoad = null;
            scheduler = null;
            refresh = null;
            cancellation = null;
            RaidPlans = null;
            Cache = null;
            Instance = null;
        }
    }
}

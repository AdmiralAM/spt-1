using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;

namespace SPTQuestPlanner.Client
{
    [BepInPlugin("com.admiralam.spt.questplanner", "SPT Quest Planner", "0.8.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private CancellationTokenSource cancellation;
        private PlannerRefreshCoordinator refresh;
        private Task initialLoad;

        internal static PlannerClientCache Cache { get; private set; }
        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Cache = new PlannerClientCache();
            refresh = new PlannerRefreshCoordinator(
                new ReflectionSptPlannerTransport(),
                new ReflectionNewtonsoftPlannerDecoder(),
                Cache);
            cancellation = new CancellationTokenSource();
            StartInitialLoad();
            Logger.LogInfo("SPT Quest Planner v0.8.0 loaded (client cache foundation; no UI)");
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

        internal void RequestStateRefresh(string reason)
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

        private void OnDestroy()
        {
            if (cancellation != null) cancellation.Cancel();
            initialLoad = null;
            refresh = null;
            cancellation = null;
            Cache = null;
            Instance = null;
        }
    }
}

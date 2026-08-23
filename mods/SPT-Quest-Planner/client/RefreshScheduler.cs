using System;
using System.Threading;
using System.Threading.Tasks;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRefreshScheduler : IDisposable
    {
        private readonly Action<string> refreshAction;
        private readonly TimeSpan debounce;
        private readonly object gate = new object();
        private CancellationTokenSource pending;
        private string pendingReason;
        private bool disposed;

        public PlannerRefreshScheduler(Action<string> refreshAction, TimeSpan debounce)
        {
            this.refreshAction = refreshAction ?? throw new ArgumentNullException("refreshAction");
            if (debounce < TimeSpan.Zero) throw new ArgumentOutOfRangeException("debounce");
            this.debounce = debounce;
        }

        public void Request(string reason)
        {
            CancellationTokenSource source;
            lock (gate)
            {
                if (disposed) return;
                if (!string.IsNullOrWhiteSpace(reason)) pendingReason = reason.Trim();
                if (pending != null) pending.Cancel();
                pending = new CancellationTokenSource();
                source = pending;
            }

            Task.Run(async () =>
            {
                try
                {
                    if (debounce > TimeSpan.Zero)
                        await Task.Delay(debounce, source.Token).ConfigureAwait(false);

                    string reasonSnapshot;
                    lock (gate)
                    {
                        if (disposed || source != pending || source.IsCancellationRequested) return;
                        reasonSnapshot = string.IsNullOrWhiteSpace(pendingReason) ? "lifecycle" : pendingReason;
                        pending = null;
                        pendingReason = null;
                    }

                    refreshAction(reasonSnapshot);
                }
                catch (OperationCanceledException) { }
                finally
                {
                    source.Dispose();
                }
            });
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                if (pending != null) pending.Cancel();
                pending = null;
                pendingReason = null;
            }
        }
    }
}

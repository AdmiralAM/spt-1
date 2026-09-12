using System;
using System.Threading;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadScopeEpochRegression
    {
        internal static void Run()
        {
            ReloadScopeEpochGuard.ResetStateForRegression();

            using ManualResetEventSlim entered = new ManualResetEventSlim(false);
            using ManualResetEventSlim invalidated = new ManualResetEventSlim(false);
            Exception workerFailure = null;

            Thread staleOwner = new Thread(() =>
            {
                try
                {
                    ReloadScopeEpochGuard.EnterForRegression();
                    if (!ReloadScopeEpochGuard.IsCurrentForRegression())
                        throw new InvalidOperationException("fresh owner scope was not current before invalidation");

                    entered.Set();
                    if (!invalidated.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("epoch regression did not receive invalidation signal");

                    if (ReloadScopeEpochGuard.IsCurrentForRegression())
                        throw new InvalidOperationException("scope from superseded installation remained current on its owning thread");

                    ReloadScopeEpochGuard.ExitForRegression();
                    if (ReloadScopeEpochGuard.IsCurrentForRegression())
                        throw new InvalidOperationException("stale scope exit reactivated a superseded generation");
                }
                catch (Exception exception)
                {
                    workerFailure = exception;
                }
            });

            staleOwner.IsBackground = true;
            staleOwner.Start();
            if (!entered.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("epoch regression worker did not enter reload scope");

            ReloadScopeEpochGuard.InvalidateForRegression();

            ReloadScopeEpochGuard.EnterForRegression();
            if (!ReloadScopeEpochGuard.IsCurrentForRegression())
                throw new InvalidOperationException("new-generation reload scope did not become current");
            ReloadScopeEpochGuard.ExitForRegression();
            if (ReloadScopeEpochGuard.IsCurrentForRegression())
                throw new InvalidOperationException("new-generation reload scope leaked after exit");

            invalidated.Set();
            if (!staleOwner.Join(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("epoch regression worker did not terminate");
            if (workerFailure != null)
                throw new InvalidOperationException("cross-thread reload epoch regression failed", workerFailure);

            ReloadScopeEpochGuard.EnterForRegression();
            ReloadScopeEpochGuard.EnterForRegression();
            ReloadScopeEpochGuard.InvalidateForRegression();
            if (ReloadScopeEpochGuard.IsCurrentForRegression())
                throw new InvalidOperationException("same-thread nested stale scope survived generation invalidation");
            ReloadScopeEpochGuard.EnterForRegression();
            if (!ReloadScopeEpochGuard.IsCurrentForRegression())
                throw new InvalidOperationException("same-thread scope did not recover onto the current generation");
            ReloadScopeEpochGuard.ExitForRegression();
            if (ReloadScopeEpochGuard.IsCurrentForRegression())
                throw new InvalidOperationException("recovered scope leaked after exact exit");

            RunFastAccessInstallTransitionRegression();

            Console.WriteLine("ReloadScopeEpochRegression: PASS");

            // The publication-fence regression mutates the same global/thread epoch state,
            // so execute it deterministically after the base epoch regression rather than
            // through ModuleInitializer ordering.
            ReloadEpochPublicationFenceRegression.Run();
        }

        static void RunFastAccessInstallTransitionRegression()
        {
            object oldOriginalFast = ReloadCandidateBridgeRuntime.OriginalFastAccessSlots;
            object oldInstalledFast = ReloadCandidateBridgeRuntime.InstalledFastAccessSlots;
            object oldOriginalBind = ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots;
            object oldInstalledBind = ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots;

            var originalFast = new object[] { "fast-original" };
            var installedFast = new object[] { "fast-installed" };
            var originalBind = new object[] { "bind-original" };
            var installedBind = new object[] { "bind-installed" };

            try
            {
                ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
                ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
                ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
                ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;

                ReloadScopeEpochGuard.ResetStateForRegression();
                ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
                ReloadScopeEpochGuard.EnterForRegression();
                if (!ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("pre-install scope did not start current");

                ReloadScopeEpochGuard.FastAccessInstallCompletedForRegression(false);
                if (ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("failed FastAccess install did not invalidate an already-entered scope");
                if (ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast))
                    throw new InvalidOperationException("failed FastAccess install retained stale slot-array snapshot authority");

                ReloadScopeEpochGuard.FastAccessInstallCompletedForRegression(true);
                if (ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("failed-to-successful FastAccess install transition revived stale scope under a reused generation");
                if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast)
                    || !ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedFast)
                    || !ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalBind)
                    || !ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedBind))
                    throw new InvalidOperationException("successful FastAccess install did not publish all four exact slot-array snapshots");

                ReloadScopeEpochGuard.EnterForRegression();
                if (!ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("fresh scope did not recover after successful FastAccess install transition");
                ReloadScopeEpochGuard.ExitForRegression();
                if (ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("fresh post-install scope leaked after exact exit");

                ReloadScopeEpochGuard.ResetStateForRegression();
                ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
                ReloadScopeEpochGuard.EnterForRegression();
                ReloadScopeEpochGuard.FastAccessInstallCompletedForRegression(true);
                if (ReloadScopeEpochGuard.IsCurrentForRegression())
                    throw new InvalidOperationException("successful FastAccess authority refresh failed to invalidate a pre-refresh scope");
                if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedBind))
                    throw new InvalidOperationException("successful FastAccess authority refresh failed to republish exact snapshots");
            }
            finally
            {
                ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = oldOriginalFast;
                ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = oldInstalledFast;
                ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = oldOriginalBind;
                ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = oldInstalledBind;
                ReloadScopeEpochGuard.ResetStateForRegression();
            }
        }
    }
}
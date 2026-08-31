using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadScopeEpochRegression
    {
        [ModuleInitializer]
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

            // The invalidating thread must be able to enter the new generation immediately while
            // the old owner still carries stale ThreadStatic state.
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

            // Same-thread stale nesting must also be discarded when the generation changes.
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

            Console.WriteLine("ReloadScopeEpochRegression: PASS");
        }
    }
}

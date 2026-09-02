using System;
using System.Runtime.CompilerServices;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadEpochPublicationFenceRegression
    {
        [ModuleInitializer]
        internal static void Run()
        {
            ReloadScopeEpochGuard.ResetStateForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(ReloadEpochPublicationFence.CaptureForRegression()),
                "append outside a current reload scope is never publishable");

            ReloadScopeEpochGuard.EnterForRegression();
            ReloadEpochPublicationFence.Snapshot snapshot = ReloadEpochPublicationFence.CaptureForRegression();
            Require(snapshot.EntryCurrent, "current reload scope is captured at append entry");
            Require(ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "unchanged current epoch remains publishable");

            ReloadScopeEpochGuard.InvalidateForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "reset/invalidation during append permanently fences the entered transaction");

            ReloadScopeEpochGuard.EnterForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "later scope entry/reinstall cannot revive a stale pre-reset append snapshot");

            ReloadEpochPublicationFence.Snapshot fresh = ReloadEpochPublicationFence.CaptureForRegression();
            Require(fresh.EntryCurrent, "fresh post-reset scope can capture the new epoch");
            Require(ReloadEpochPublicationFence.ShouldPublishForRegression(fresh),
                "fresh post-reset append remains publishable while its epoch stays current");

            ReloadScopeEpochGuard.ExitForRegression();
            ReloadScopeEpochGuard.ExitForRegression();
            ReloadScopeEpochGuard.ResetStateForRegression();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Reload epoch publication fence regression failed: " + message);
        }
    }
}

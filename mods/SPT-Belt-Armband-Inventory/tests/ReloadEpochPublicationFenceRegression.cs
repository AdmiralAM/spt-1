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
            ReloadEpochPublicationFence.ResetForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(ReloadEpochPublicationFence.CaptureForRegression()),
                "append outside a current reload scope is never publishable");

            ReloadScopeEpochGuard.EnterForRegression();
            ReloadEpochPublicationFence.Snapshot snapshot = ReloadEpochPublicationFence.CaptureForRegression();
            Require(snapshot.EntryCurrent, "current reload scope is captured at append entry");
            Require(ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "unchanged current epoch remains publishable");

            ReloadScopeEpochGuard.InvalidateForRegression();
            ReloadEpochPublicationFence.InvalidateForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "reset/invalidation during append permanently fences the entered transaction");

            ReloadScopeEpochGuard.EnterForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(snapshot),
                "later scope entry/reinstall cannot revive a stale pre-reset append snapshot even after scope state becomes current again");

            ReloadEpochPublicationFence.Snapshot fresh = ReloadEpochPublicationFence.CaptureForRegression();
            Require(fresh.EntryCurrent, "fresh post-reset scope can capture the new epoch");
            Require(ReloadEpochPublicationFence.ShouldPublishForRegression(fresh),
                "fresh post-reset append remains publishable while its own epoch stays current");

            ReloadEpochPublicationFence.InvalidateForRegression();
            Require(!ReloadEpochPublicationFence.ShouldPublishForRegression(fresh),
                "a second reset independently fences the fresh transaction");

            ReloadScopeEpochGuard.ExitForRegression();
            ReloadScopeEpochGuard.ExitForRegression();
            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Reload epoch publication fence regression failed: " + message);
        }
    }
}

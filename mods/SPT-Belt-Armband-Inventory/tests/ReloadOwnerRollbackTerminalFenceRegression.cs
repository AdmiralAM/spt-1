using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadOwnerRollbackTerminalFenceRegression
    {
        [ModuleInitializer]
        internal static void Run()
        {
            ReloadOwnerRollbackTerminalFence.ResetForRegression();
            Assert(ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "fresh process authority allows reload owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "proven Harmony rollback does not poison future owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(false);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "unproven Harmony rollback terminally blocks later owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "later successful rollback cannot clear process-terminal stale-owner ambiguity");

            ReloadOwnerRollbackTerminalFence.ResetForRegression();
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Reload owner rollback terminal fence regression failed: " + message);
        }
    }
}

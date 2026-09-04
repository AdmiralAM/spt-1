using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadOwnerRollbackTerminalFenceRegression
    {
        [ModuleInitializer]
        internal static void Run()
        {
            FieldInfo poison = typeof(ReloadOwnerRollbackTerminalFence).GetField("terminalFailure", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(poison != null && HasRequiredModifier(poison, typeof(IsVolatile)),
                "process-terminal poison must be volatile so rollback failure is immediately visible to later owner-install prefixes on other threads");

            ReloadOwnerRollbackTerminalFence.ResetForRegression();
            Assert(ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "fresh process authority allows reload owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "proven normal Harmony rollback does not poison future owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(false);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "unproven normal Harmony rollback terminally blocks later owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "later successful rollback cannot clear process-terminal stale-owner ambiguity");

            Assert(!ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(false, false),
                "fence installation failure before owner creation must not invent rollback ambiguity");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(false, true),
                "once a fence owner was created, failed installation is terminal even if best-effort UnpatchSelf returned without throwing");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(true, true),
                "an already-poisoned process remains poisoned after a created-owner install failure");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(true, false),
                "an already-poisoned process remains poisoned when a later failing install creates no owner");

            Assert(!ReloadOwnerRollbackTerminalFence.ShouldRetainAssemblyLoadSubscriptionForRegression(true, false),
                "post-subscription retry success must immediately remove the AssemblyLoad handler; this closes the missed-load window without leaving a redundant observer");
            Assert(!ReloadOwnerRollbackTerminalFence.ShouldRetainAssemblyLoadSubscriptionForRegression(false, true),
                "post-subscription terminal failure must remove the AssemblyLoad handler because no later load may restore install authority");
            Assert(!ReloadOwnerRollbackTerminalFence.ShouldRetainAssemblyLoadSubscriptionForRegression(true, true),
                "terminal state dominates even if the second attempt also reports installed");
            Assert(ReloadOwnerRollbackTerminalFence.ShouldRetainAssemblyLoadSubscriptionForRegression(false, false),
                "only a still-unavailable, non-terminal post-subscription retry may retain the AssemblyLoad handler for a future 0Harmony load");

            ReloadOwnerRollbackTerminalFence.ResetForRegression();
        }

        static bool HasRequiredModifier(FieldInfo field, Type modifier)
        {
            Type[] modifiers = field.GetRequiredCustomModifiers();
            for (int i = 0; i < modifiers.Length; i++)
                if (modifiers[i] == modifier) return true;
            return false;
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Reload owner rollback terminal fence regression failed: " + message);
        }
    }
}
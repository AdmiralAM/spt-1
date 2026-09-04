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
                "proven Harmony rollback does not poison future owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(false);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "unproven Harmony rollback terminally blocks later owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "later successful rollback cannot clear process-terminal stale-owner ambiguity");

            Assert(!ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(false, false, true),
                "failed installation before owner creation must not invent rollback ambiguity");
            Assert(!ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(false, true, true),
                "proven cleanup of a newly-created owner may leave a previously-clean process unpoisoned");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(false, true, false),
                "unproven cleanup of a created owner must poison the process");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(true, true, true),
                "successful local cleanup must never clear terminal ambiguity already published by another rollback observer");
            Assert(ReloadOwnerRollbackTerminalFence.MergeTerminalFailureForRegression(true, false, true),
                "an already-poisoned process remains poisoned even when the failing install created no owner");

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
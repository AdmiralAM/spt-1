using System;
using System.IO;
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
            Assert(ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed,
                "fresh process authority allows reload owner installation");
            Assert(ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "regression projection must match production owner-install authority");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed,
                "proven normal Harmony rollback does not poison future owner installation");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(false);
            Assert(!ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed,
                "unproven normal Harmony rollback terminally blocks later owner installation");
            Assert(!ReloadOwnerRollbackTerminalFence.CanInstallForRegression(),
                "regression projection must observe process-terminal production denial");

            ReloadOwnerRollbackTerminalFence.ObserveRollbackForRegression(true);
            Assert(!ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed,
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

            AssertDirectProductionGateWiring();
            ReloadOwnerRollbackTerminalFence.ResetForRegression();
        }

        static void AssertDirectProductionGateWiring()
        {
            string root = FindModuleRoot();
            Assert(root != null, "module root must resolve for direct owner-install wiring proof");
            string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));

            AssertMethodGate(source, "bool TryInstallReloadReachability()", "if (!ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed)", "if (reachabilityRollbackUnsafe)");
            AssertMethodGate(source, "bool TryInstallReloadCandidateBridge(Type inventoryType, Type slotEnumType, object dedicatedBelt)", "if (!ReloadOwnerRollbackTerminalFence.OwnerInstallsAllowed)", "if (candidateBridgeRollbackUnsafe)");
        }

        static void AssertMethodGate(string source, string methodSignature, string directGate, string firstLegacyGate)
        {
            int method = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert(method >= 0, "production installer missing: " + methodSignature);
            int nextMethod = source.IndexOf("\n        static ", method + methodSignature.Length, StringComparison.Ordinal);
            if (nextMethod < 0) nextMethod = source.Length;
            string body = source.Substring(method, nextMethod - method);
            int gate = body.IndexOf(directGate, StringComparison.Ordinal);
            int legacy = body.IndexOf(firstLegacyGate, StringComparison.Ordinal);
            Assert(gate >= 0, "production installer lacks direct process-terminal authority gate: " + methodSignature);
            Assert(legacy >= 0 && gate < legacy,
                "process-terminal authority must be checked before per-instance rollback state/reflection/publication: " + methodSignature);
        }

        static string FindModuleRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
                if (File.Exists(candidate)) return current.FullName;
                current = current.Parent;
            }

            current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                string direct = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
                if (File.Exists(direct)) return current.FullName;
                string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
                if (File.Exists(Path.Combine(nested, "src", "FastAccessSlotPatches.cs"))) return nested;
                current = current.Parent;
            }
            return null;
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

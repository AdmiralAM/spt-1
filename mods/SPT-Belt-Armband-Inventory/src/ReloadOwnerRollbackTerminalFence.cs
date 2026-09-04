using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Process-wide fail-closed fence for Harmony owner rollback ambiguity.
    /// FastAccessSlotPatches keeps per-instance rollback authority, but a failed UnpatchSelf()
    /// can leave a stale owner alive after that instance has reset the shared runtime fields.
    /// A later FastAccessSlotPatches instance must never republish those fields and thereby
    /// reactivate an owner whose removal was not proven. This fence observes either owner
    /// rollback failure and permanently blocks both reload-owner install paths for the process.
    /// It does not add discovery, queries, retries, redirects, slot sources, or result rewriting.
    /// </summary>
    internal static class ReloadOwnerRollbackTerminalFence
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-owner-rollback-terminal-fence";
        static readonly object Gate = new object();
        static bool fenceInstalled;
        // Harmony rollback observers and later owner-install prefixes can execute on different
        // threads. The process-terminal poison bit must therefore publish immediately across
        // threads; a stale false read would otherwise be capable of re-authorizing a stale owner.
        static volatile bool terminalFailure;
        static object harmonyOwner;

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            if (TryInstallFence() || terminalFailure)
                return;

            // Subscribe before the second attempt. 0Harmony can become available after the
            // first Type.GetType probe but before AssemblyLoad registration; without this
            // immediate post-subscription retry that load event is lost forever and the
            // process-terminal owner fence may never publish. The retry is installation-only:
            // it does not widen reload discovery or touch candidate enumeration.
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            bool installedAfterSubscribe = TryInstallFence();
            if (!ShouldRetainAssemblyLoadSubscriptionForRegression(installedAfterSubscribe, terminalFailure))
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (terminalFailure)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }

            if (!TryInstallFence())
            {
                if (terminalFailure) AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static bool TryInstallFence()
        {
            lock (Gate)
            {
                if (fenceInstalled) return true;
                if (terminalFailure) return false;

                object owner = null;
                MethodInfo rollback = null;
                try
                {
                    Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                    Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                    if (harmonyType == null || harmonyMethodType == null) return false;

                    ConstructorInfo harmonyCtor = harmonyType.GetConstructor(new[] { typeof(string) });
                    ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                    MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
                    rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");

                    Type target = typeof(FastAccessSlotPatches);
                    MethodInfo installReachability = target.GetMethod("TryInstallReloadReachability", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo installCandidates = target.GetMethod("TryInstallReloadCandidateBridge", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo unpatchReachability = target.GetMethod("UnpatchReachability", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo unpatchCandidates = target.GetMethod("UnpatchCandidateBridge", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo beforeInstall = typeof(ReloadOwnerRollbackTerminalFence).GetMethod(nameof(BeforeOwnerInstall), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo afterRollback = typeof(ReloadOwnerRollbackTerminalFence).GetMethod(nameof(AfterOwnerRollback), BindingFlags.Static | BindingFlags.NonPublic);

                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || rollback == null
                        || installReachability == null || installReachability.ReturnType != typeof(bool)
                        || installCandidates == null || installCandidates.ReturnType != typeof(bool)
                        || unpatchReachability == null || unpatchReachability.ReturnType != typeof(bool)
                        || unpatchCandidates == null || unpatchCandidates.ReturnType != typeof(bool)
                        || beforeInstall == null || afterRollback == null)
                    {
                        terminalFailure = true;
                        return false;
                    }

                    owner = harmonyCtor.Invoke(new object[] { HarmonyId });
                    if (owner == null)
                    {
                        terminalFailure = true;
                        return false;
                    }

                    object installPrefix = harmonyMethodCtor.Invoke(new object[] { beforeInstall });
                    object rollbackPostfix = harmonyMethodCtor.Invoke(new object[] { afterRollback });

                    // Blockers are installed first. If later patch publication fails and this
                    // owner cannot be rolled back, terminalFailure=true leaves any surviving
                    // blocker fail-closed rather than allowing a partially protected reinstall.
                    PatchNamed(owner, patch, harmonyMethodType, installReachability, "prefix", installPrefix);
                    PatchNamed(owner, patch, harmonyMethodType, installCandidates, "prefix", installPrefix);
                    PatchNamed(owner, patch, harmonyMethodType, unpatchReachability, "postfix", rollbackPostfix);
                    PatchNamed(owner, patch, harmonyMethodType, unpatchCandidates, "postfix", rollbackPostfix);

                    harmonyOwner = owner;
                    fenceInstalled = true;
                    return true;
                }
                catch
                {
                    // UnpatchSelf is best-effort cleanup only here. A no-throw invocation does
                    // not prove that every partially published blocker/observer owned by this
                    // fence is absent. Once an owner was created and installation subsequently
                    // failed, authority is therefore process-terminal regardless of cleanup's
                    // apparent success. This is intentionally stricter than the normal owner
                    // rollback path: the fence itself is the last fail-closed boundary and must
                    // never certify its own absence from an unverified side effect.
                    TryRollback(owner, rollback);
                    harmonyOwner = null;
                    fenceInstalled = false;
                    terminalFailure = MergeTerminalFailureForRegression(terminalFailure, owner != null);
                    return false;
                }
            }
        }

        static bool BeforeOwnerInstall(ref bool __result)
        {
            if (!terminalFailure) return true;
            __result = false;
            return false;
        }

        static void AfterOwnerRollback(bool __result)
        {
            if (!__result) terminalFailure = true;
        }

        internal static bool CanInstallForRegression()
        {
            return !terminalFailure;
        }

        internal static void ObserveRollbackForRegression(bool rollbackProven)
        {
            if (!rollbackProven) terminalFailure = true;
        }

        internal static bool MergeTerminalFailureForRegression(bool currentTerminalFailure, bool ownerCreated)
        {
            return currentTerminalFailure || ownerCreated;
        }

        internal static bool ShouldRetainAssemblyLoadSubscriptionForRegression(bool installedAfterSubscribe, bool terminalAfterSubscribe)
        {
            return !installedAfterSubscribe && !terminalAfterSubscribe;
        }

        internal static void ResetForRegression()
        {
            terminalFailure = false;
        }

        static bool TryRollback(object owner, MethodInfo rollback)
        {
            if (owner == null) return true;
            if (rollback == null) return false;
            try
            {
                rollback.Invoke(owner, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 4 || parameters[0].ParameterType != typeof(MethodBase)) continue;
                bool prefix = false, postfix = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) prefix = true;
                    else if (string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) postfix = true;
                }
                if (!prefix || !postfix) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.GetParameters().Length != 0) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static void PatchNamed(object owner, MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, string patchKind, object patch)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            bool originalAssigned = false;
            bool patchAssigned = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (i == 0 && parameter.ParameterType == typeof(MethodBase))
                {
                    args[i] = original;
                    originalAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType && string.Equals(parameter.Name, patchKind, StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = patch;
                    patchAssigned = true;
                }
            }
            if (!originalAssigned || !patchAssigned) throw new MissingMethodException("Harmony.Patch parameter not found: " + patchKind);
            patchMethod.Invoke(owner, args);
        }
    }
}
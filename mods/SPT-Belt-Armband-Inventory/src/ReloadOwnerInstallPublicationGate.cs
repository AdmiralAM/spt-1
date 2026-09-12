using System;
using System.Reflection;
using System.Threading;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Global fail-closed publication gate for the FastAccessSlotPatches install transaction.
    /// Harmony owners become callable as soon as each Patch() succeeds, while the complete
    /// reachability/candidate bridge is published only after several Patch() calls. This gate
    /// keeps those partially installed hooks inert until FastAccessSlotPatches.TryInstall exits.
    /// A stale owner that survives a failed rollback remains inert permanently because runtime
    /// publication requires both the complete live subsystem contract and this gate's own exact
    /// successful-owner authority on every invocation. It never discovers candidates, changes
    /// slot arrays, retries a query, or changes vanilla ordering. A blocked candidate hook returns
    /// the exact incoming vanilla result object.
    /// </summary>
    internal static class ReloadOwnerInstallPublicationGate
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-owner-install-publication";
        static readonly object InstallGate = new object();
        static int installDepth;
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            if (TryInstall() || terminalFailure)
                return;

            // Subscribe before retrying. 0Harmony can become available after the first
            // Type.GetType probe but before AssemblyLoad registration; without the immediate
            // post-subscription retry that load event is lost and the publication gate may
            // never install, allowing partially published reload hooks to become callable.
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            bool installedAfterSubscribe = TryInstall();
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

            if (!TryInstall())
            {
                if (terminalFailure) AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static bool TryInstall()
        {
            lock (InstallGate)
            {
                if (installed) return true;
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

                    MethodInfo fastAccessInstall = typeof(FastAccessSlotPatches).GetMethod(
                        "TryInstall", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                        null, Type.EmptyTypes, null);
                    MethodInfo promoteReachability = typeof(FastAccessReloadRuntime).GetMethod(
                        nameof(FastAccessReloadRuntime.PromoteReachability),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null, new[] { typeof(object), typeof(bool).MakeByRefType() }, null);
                    MethodInfo enterReload = typeof(ReloadCandidateBridgeRuntime).GetMethod(
                        nameof(ReloadCandidateBridgeRuntime.EnterReloadScope),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null, Type.EmptyTypes, null);
                    MethodInfo appendCandidates = typeof(ReloadCandidateBridgeRuntime).GetMethod(
                        nameof(ReloadCandidateBridgeRuntime.AppendCandidates),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null, new[] { typeof(object), typeof(object), typeof(object) }, null);

                    MethodInfo beforeInstall = typeof(ReloadOwnerInstallPublicationGate).GetMethod(nameof(BeforeInstall), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo afterInstall = typeof(ReloadOwnerInstallPublicationGate).GetMethod(nameof(AfterInstall), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo beforePromote = typeof(ReloadOwnerInstallPublicationGate).GetMethod(nameof(BeforePromote), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo beforeEnter = typeof(ReloadOwnerInstallPublicationGate).GetMethod(nameof(BeforeEnterReload), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo beforeAppend = typeof(ReloadOwnerInstallPublicationGate).GetMethod(nameof(BeforeAppendCandidates), BindingFlags.Static | BindingFlags.NonPublic);

                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || rollback == null
                        || fastAccessInstall == null || fastAccessInstall.ReturnType != typeof(bool)
                        || promoteReachability == null || promoteReachability.ReturnType != typeof(void)
                        || enterReload == null || enterReload.ReturnType != typeof(void)
                        || appendCandidates == null || appendCandidates.ReturnType != typeof(object)
                        || beforeInstall == null || afterInstall == null || beforePromote == null || beforeEnter == null || beforeAppend == null)
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
                    object installFinalizer = harmonyMethodCtor.Invoke(new object[] { afterInstall });
                    object promotePrefix = harmonyMethodCtor.Invoke(new object[] { beforePromote });
                    object enterPrefix = harmonyMethodCtor.Invoke(new object[] { beforeEnter });
                    object appendPrefix = harmonyMethodCtor.Invoke(new object[] { beforeAppend });

                    Patch(owner, patch, harmonyMethodType, fastAccessInstall, installPrefix, null, installFinalizer);
                    Patch(owner, patch, harmonyMethodType, promoteReachability, promotePrefix, null, null);
                    Patch(owner, patch, harmonyMethodType, enterReload, enterPrefix, null, null);
                    Patch(owner, patch, harmonyMethodType, appendCandidates, appendPrefix, null, null);

                    harmonyOwner = owner;
                    installed = true;
                    return true;
                }
                catch
                {
                    bool rolledBack = TryRollback(owner, rollback);
                    harmonyOwner = null;
                    installed = false;
                    terminalFailure = owner != null && !rolledBack;
                    Interlocked.Exchange(ref installDepth, 0);
                    return false;
                }
            }
        }

        static void BeforeInstall()
        {
            BeginForRegression();
        }

        static Exception AfterInstall(Exception __exception)
        {
            EndForRegression();
            return __exception;
        }

        static bool BeforePromote()
        {
            return HasLiveReachabilityPublicationContract();
        }

        static bool BeforeEnterReload()
        {
            return HasLiveCandidatePublicationContract();
        }

        static bool BeforeAppendCandidates(object __2, ref object __result)
        {
            if (HasLiveCandidatePublicationContract()) return true;
            __result = __2;
            return false;
        }

        static bool HasLiveReachabilityPublicationContract()
        {
            return HasPublicationAuthority()
                && FastAccessReloadRuntime.ItemType != null
                && FastAccessReloadRuntime.MagazineType != null
                && FastAccessReloadRuntime.GetAllParentItems != null
                && FastAccessReloadRuntime.ReadTemplateId != null;
        }

        static bool HasLiveCandidatePublicationContract()
        {
            return HasPublicationAuthority()
                && ReloadCandidateBridgeRuntime.GetItemsInSlots != null
                && ReloadCandidateBridgeRuntime.BeltSlotsArgument != null
                && ReloadCandidateBridgeRuntime.ItemType != null
                && ReloadCandidateBridgeRuntime.MagazineType != null
                && ReloadCandidateBridgeRuntime.ReturnType != null
                && ReloadCandidateBridgeRuntime.GetAllParentItems != null
                && ReloadCandidateBridgeRuntime.ReadTemplateId != null;
        }

        static bool HasPublicationAuthority()
        {
            return installed
                && !terminalFailure
                && Volatile.Read(ref installDepth) == 0;
        }

        internal static bool HasPublicationAuthorityForRegression(bool installedState, bool terminalFailureState, int depth)
        {
            return installedState
                && !terminalFailureState
                && depth == 0;
        }

        internal static bool ShouldRetainAssemblyLoadSubscriptionForRegression(bool installedAfterSubscribe, bool terminalAfterSubscribe)
        {
            return !installedAfterSubscribe && !terminalAfterSubscribe;
        }

        internal static void BeginForRegression()
        {
            Interlocked.Increment(ref installDepth);
        }

        internal static void EndForRegression()
        {
            while (true)
            {
                int current = Volatile.Read(ref installDepth);
                if (current <= 0) return;
                if (Interlocked.CompareExchange(ref installDepth, current - 1, current) == current) return;
            }
        }

        internal static bool CanPublishForRegression()
        {
            return Volatile.Read(ref installDepth) == 0;
        }

        internal static object SelectCandidateForRegression(object vanillaResult, object candidateResult)
        {
            return CanPublishForRegression() ? candidateResult : vanillaResult;
        }

        internal static void ResetForRegression()
        {
            Interlocked.Exchange(ref installDepth, 0);
        }

        static void Patch(object owner, MethodInfo patch, Type harmonyMethodType, MethodInfo original, object prefix, object postfix, object finalizer)
        {
            ParameterInfo[] parameters = patch.GetParameters();
            object[] args = new object[parameters.Length];
            bool originalAssigned = false;
            bool prefixAssigned = prefix == null;
            bool postfixAssigned = postfix == null;
            bool finalizerAssigned = finalizer == null;

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (i == 0 && parameter.ParameterType == typeof(MethodBase))
                {
                    args[i] = original;
                    originalAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType && string.Equals(parameter.Name, "prefix", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = prefix;
                    if (prefix != null) prefixAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType && string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = postfix;
                    if (postfix != null) postfixAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType && string.Equals(parameter.Name, "finalizer", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = finalizer;
                    if (finalizer != null) finalizerAssigned = true;
                }
                else
                {
                    args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
                }
            }

            if (!originalAssigned || !prefixAssigned || !postfixAssigned || !finalizerAssigned)
                throw new MissingMethodException("Harmony.Patch exact publication-gate parameter contract changed.");
            patch.Invoke(owner, args);
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 4 || parameters[0].ParameterType != typeof(MethodBase)) continue;
                bool prefix = false, postfix = false, finalizer = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) prefix = true;
                    else if (string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) postfix = true;
                    else if (string.Equals(parameters[p].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) finalizer = true;
                }
                if (!prefix || !postfix || !finalizer) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            if (type == null) return null;
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
    }
}
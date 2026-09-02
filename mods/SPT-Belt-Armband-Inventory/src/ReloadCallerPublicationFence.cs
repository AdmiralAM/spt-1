using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Caller-side final publication fence for the exact GetItemsInSlots method patched by
    /// FastAccessSlotPatches. The bridge itself already fails closed through its lazy windows;
    /// this guard preserves the exact vanilla result across the remaining postfix-return window.
    /// A Reset/FastAccess generation transition or loss of pinned caller-array authority after
    /// AppendCandidates returned therefore cannot publish its derived Belt candidate result.
    /// No retry, second slot15 query, redirect, or alternate candidate source is introduced.
    /// </summary>
    internal static class ReloadCallerPublicationFence
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-caller-publication";
        const string CandidateFenceHarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-caller-publication.candidate";
        const string CandidateBridgeHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate";
        static readonly object InstallGate = new object();
        static bool lifecycleInstalled;
        static bool candidateInstalled;
        static bool terminalFailure;
        static object harmonyOwner;
        static object candidateHarmonyOwner;

        [ThreadStatic] static Stack<PublicationState> publicationStates;

        sealed class PublicationState
        {
            internal readonly object VanillaResult;
            internal readonly object Slots;
            internal readonly ReloadEpochPublicationFence.Snapshot Epoch;

            internal PublicationState(object vanillaResult, object slots, ReloadEpochPublicationFence.Snapshot epoch)
            {
                VanillaResult = vanillaResult;
                Slots = slots;
                Epoch = epoch;
            }
        }

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            if (!TryInstallLifecycleHook() && !terminalFailure)
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (terminalFailure)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }
            if (!TryInstallLifecycleHook())
            {
                if (terminalFailure) AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                return;
            }
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static bool TryInstallLifecycleHook()
        {
            lock (InstallGate)
            {
                if (lifecycleInstalled) return true;
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
                    MethodInfo install = typeof(FastAccessSlotPatches).GetMethod("TryInstall", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo afterInstall = typeof(ReloadCallerPublicationFence).GetMethod(nameof(AfterFastAccessInstall), BindingFlags.Static | BindingFlags.NonPublic);
                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || rollback == null
                        || install == null || install.ReturnType != typeof(bool) || install.GetParameters().Length != 0 || afterInstall == null)
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

                    object postfix = harmonyMethodCtor.Invoke(new object[] { afterInstall });
                    object[] args = BuildPostfixArguments(patch, harmonyMethodType, install, postfix);
                    if (args == null)
                    {
                        terminalFailure = true;
                        TryRollback(owner, rollback);
                        return false;
                    }

                    patch.Invoke(owner, args);
                    harmonyOwner = owner;
                    lifecycleInstalled = true;
                    return true;
                }
                catch
                {
                    bool rolledBack = TryRollback(owner, rollback);
                    harmonyOwner = null;
                    lifecycleInstalled = false;
                    candidateInstalled = false;
                    terminalFailure = owner != null && !rolledBack;
                    return false;
                }
            }
        }

        static void AfterFastAccessInstall(bool __result)
        {
            if (!__result) return;
            TryInstallCandidateFence();
        }

        static bool TryInstallCandidateFence()
        {
            lock (InstallGate)
            {
                if (candidateInstalled) return true;
                if (!lifecycleInstalled || terminalFailure || harmonyOwner == null) return false;

                object candidateOwner = null;
                MethodInfo candidateRollback = null;
                try
                {
                    Type harmonyType = harmonyOwner.GetType();
                    Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                    ConstructorInfo harmonyCtor = harmonyType.GetConstructor(new[] { typeof(string) });
                    ConstructorInfo harmonyMethodCtor = harmonyMethodType?.GetConstructor(new[] { typeof(MethodInfo) });
                    MethodInfo patch = harmonyMethodType == null ? null : FindPatchMethod(harmonyType, harmonyMethodType);
                    candidateRollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                    MethodInfo getItems = ReloadCandidateBridgeRuntime.GetItemsInSlots;
                    MethodInfo capture = typeof(ReloadCallerPublicationFence).GetMethod(nameof(CaptureVanilla), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo finalize = typeof(ReloadCallerPublicationFence).GetMethod(nameof(FinalizePublication), BindingFlags.Static | BindingFlags.NonPublic);
                    if (harmonyMethodType == null || harmonyCtor == null || harmonyMethodCtor == null || patch == null || candidateRollback == null
                        || getItems == null || capture == null || finalize == null)
                        return false;

                    object beforePostfix = harmonyMethodCtor.Invoke(new object[] { capture });
                    object afterPostfix = harmonyMethodCtor.Invoke(new object[] { finalize });
                    if (!SetOrdering(beforePostfix, "before", CandidateBridgeHarmonyId)
                        || !SetOrdering(afterPostfix, "after", CandidateBridgeHarmonyId))
                        return false;

                    object[] beforeArgs = BuildPostfixArguments(patch, harmonyMethodType, getItems, beforePostfix);
                    object[] afterArgs = BuildPostfixArguments(patch, harmonyMethodType, getItems, afterPostfix);
                    if (beforeArgs == null || afterArgs == null) return false;

                    candidateOwner = harmonyCtor.Invoke(new object[] { CandidateFenceHarmonyId });
                    if (candidateOwner == null) return false;

                    patch.Invoke(candidateOwner, beforeArgs);
                    patch.Invoke(candidateOwner, afterArgs);
                    candidateHarmonyOwner = candidateOwner;
                    candidateInstalled = true;
                    return true;
                }
                catch
                {
                    // The ordered pair is one publication contract. A partial pair is unsafe:
                    // a lone capture can leak ThreadStatic state and a lone finalizer has no
                    // matching vanilla snapshot. Roll back the dedicated candidate owner
                    // atomically rather than weakening Harmony ordering or leaving residue.
                    bool rolledBack = TryRollback(candidateOwner, candidateRollback);
                    candidateHarmonyOwner = null;
                    candidateInstalled = false;
                    if (candidateOwner != null && !rolledBack)
                        terminalFailure = true;
                    return false;
                }
            }
        }

        static void CaptureVanilla(object __0, object __result)
        {
            Stack<PublicationState> states = publicationStates ?? (publicationStates = new Stack<PublicationState>());
            states.Push(new PublicationState(__result, __0, ReloadEpochPublicationFence.CaptureForRegression()));
        }

        static void FinalizePublication(object __0, ref object __result)
        {
            Stack<PublicationState> states = publicationStates;
            if (states == null || states.Count == 0) return;

            PublicationState state = states.Pop();
            if (!ReferenceEquals(__0, state.Slots)
                || !state.Epoch.MayPublish()
                || !ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(state.Slots))
                __result = state.VanillaResult;
        }

        static bool SetOrdering(object harmonyMethod, string memberName, string harmonyId)
        {
            if (harmonyMethod == null || string.IsNullOrEmpty(memberName) || string.IsNullOrEmpty(harmonyId)) return false;
            Type type = harmonyMethod.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && property != null) return false;
            if (field != null)
            {
                if (field.FieldType != typeof(string[])) return false;
                field.SetValue(harmonyMethod, new[] { harmonyId });
                return true;
            }
            if (property != null)
            {
                if (property.PropertyType != typeof(string[]) || !property.CanWrite) return false;
                property.SetValue(harmonyMethod, new[] { harmonyId }, null);
                return true;
            }
            return false;
        }

        static object[] BuildPostfixArguments(MethodInfo patch, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patch.GetParameters();
            object[] args = new object[parameters.Length];
            bool originalAssigned = false;
            bool postfixAssigned = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (i == 0 && parameter.ParameterType == typeof(MethodBase))
                {
                    args[i] = original;
                    originalAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType && string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = postfix;
                    postfixAssigned = true;
                }
                else
                {
                    args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
                }
            }
            return originalAssigned && postfixAssigned ? args : null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            if (harmonyType == null || harmonyMethodType == null) return null;
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 3 || parameters[0].ParameterType != typeof(MethodBase)) continue;
                bool postfix = false;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        postfix = true;
                if (!postfix) continue;
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

        internal static object SelectForRegression(object vanillaResult, object candidateResult, object slots, ReloadEpochPublicationFence.Snapshot snapshot)
        {
            return snapshot.MayPublish() && ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)
                ? candidateResult
                : vanillaResult;
        }
    }
}

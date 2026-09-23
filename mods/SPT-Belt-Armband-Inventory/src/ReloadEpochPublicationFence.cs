using System;
using System.Reflection;
using System.Threading;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Final lifecycle fence for ReloadCandidateBridgeRuntime.AppendCandidates.
    /// A scope that was current at entry may not publish a derived candidate result if
    /// ReloadCandidateBridgeRuntime.Reset invalidated lifecycle state while the bridge
    /// was inside either lazy enumeration window. Exact execution-reference restoration
    /// or a later reinstall cannot revive the stale transaction: publication falls back
    /// to the exact incoming vanilla result object, with no retry or redirect.
    /// </summary>
    internal static class ReloadEpochPublicationFence
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-epoch-publication";
        static readonly object InstallGate = new object();
        static int generation;
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;

        internal readonly struct Snapshot
        {
            internal readonly bool EntryCurrent;
            internal readonly int Generation;

            internal Snapshot(bool entryCurrent, int generation)
            {
                EntryCurrent = entryCurrent;
                Generation = generation;
            }

            internal bool MayPublish()
            {
                return EntryCurrent
                    && ReloadScopeEpochGuard.IsCurrentForRegression()
                    && Generation == Volatile.Read(ref generation);
            }
        }

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            if (!TryInstall() && !terminalFailure)
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
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
                MethodInfo unpatchSelf = null;
                try
                {
                    Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                    Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                    if (harmonyType == null || harmonyMethodType == null) return false;

                    ConstructorInfo harmonyCtor = harmonyType.GetConstructor(new[] { typeof(string) });
                    ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                    MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
                    unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                    MethodInfo append = typeof(ReloadCandidateBridgeRuntime).GetMethod(
                        nameof(ReloadCandidateBridgeRuntime.AppendCandidates),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        new[] { typeof(object), typeof(object), typeof(object) },
                        null);
                    MethodInfo reset = typeof(ReloadCandidateBridgeRuntime).GetMethod(
                        nameof(ReloadCandidateBridgeRuntime.Reset),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null);
                    MethodInfo prefix = typeof(ReloadEpochPublicationFence).GetMethod(nameof(BeforeAppend), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo postfix = typeof(ReloadEpochPublicationFence).GetMethod(nameof(AfterAppend), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo resetPostfix = typeof(ReloadEpochPublicationFence).GetMethod(nameof(AfterReset), BindingFlags.Static | BindingFlags.NonPublic);
                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null
                        || append == null || append.ReturnType != typeof(object) || reset == null || reset.ReturnType != typeof(void)
                        || prefix == null || postfix == null || resetPostfix == null)
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

                    object prefixHarmony = harmonyMethodCtor.Invoke(new object[] { prefix });
                    object postfixHarmony = harmonyMethodCtor.Invoke(new object[] { postfix });
                    object resetHarmony = harmonyMethodCtor.Invoke(new object[] { resetPostfix });
                    object[] appendArguments = BuildPatchArguments(patch, harmonyMethodType, append, prefixHarmony, postfixHarmony);
                    object[] resetArguments = BuildPostfixArguments(patch, harmonyMethodType, reset, resetHarmony);
                    if (appendArguments == null || resetArguments == null)
                    {
                        terminalFailure = true;
                        TryRollbackOwner(owner, unpatchSelf);
                        return false;
                    }

                    patch.Invoke(owner, appendArguments);
                    patch.Invoke(owner, resetArguments);
                    harmonyOwner = owner;
                    installed = true;
                    return true;
                }
                catch
                {
                    bool rolledBack = TryRollbackOwner(owner, unpatchSelf);
                    harmonyOwner = null;
                    installed = false;
                    terminalFailure = owner != null && !rolledBack;
                    return false;
                }
            }
        }

        static void BeforeAppend(out Snapshot __state)
        {
            __state = CaptureForRegression();
        }

        static void AfterAppend(object __2, Snapshot __state, ref object __result)
        {
            if (!__state.MayPublish())
                __result = __2;
        }

        static void AfterReset()
        {
            InvalidateForRegression();
        }

        internal static Snapshot CaptureForRegression()
        {
            return new Snapshot(ReloadScopeEpochGuard.IsCurrentForRegression(), Volatile.Read(ref generation));
        }

        internal static bool ShouldPublishForRegression(Snapshot snapshot)
        {
            return snapshot.MayPublish();
        }

        internal static void ResetForRegression()
        {
            Interlocked.Exchange(ref generation, 0);
        }

        internal static void InvalidateForRegression()
        {
            Interlocked.Increment(ref generation);
        }

        static object[] BuildPatchArguments(MethodInfo patch, Type harmonyMethodType, MethodInfo original, object prefix, object postfix)
        {
            ParameterInfo[] parameters = patch.GetParameters();
            object[] args = new object[parameters.Length];
            bool originalAssigned = false;
            bool prefixAssigned = false;
            bool postfixAssigned = false;
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
                    prefixAssigned = true;
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
            return originalAssigned && prefixAssigned && postfixAssigned ? args : null;
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
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 3 || parameters[0].ParameterType != typeof(MethodBase)) continue;
                bool hasPrefix = false;
                bool hasPostfix = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) hasPrefix = true;
                    if (string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) hasPostfix = true;
                }
                if (!hasPrefix || !hasPostfix) continue;
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

        static bool TryRollbackOwner(object owner, MethodInfo unpatchSelf)
        {
            if (owner == null) return true;
            if (unpatchSelf == null) return false;
            try
            {
                unpatchSelf.Invoke(owner, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

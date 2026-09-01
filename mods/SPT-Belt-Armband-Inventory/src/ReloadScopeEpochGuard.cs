using System;
using System.Reflection;
using System.Threading;
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Cross-thread lifecycle guard for ReloadCandidateBridgeRuntime.
    /// ReloadCandidateBridgeRuntime intentionally keeps its hot scope state ThreadStatic, which means
    /// Reset() cannot clear a scope owned by another thread. This guard adds a process-wide generation:
    /// any Reset invalidates every previously-entered scope before a later reinstall can repopulate the
    /// bridge's static slot references. It never broadens candidate discovery or inventory traversal.
    /// </summary>
    internal static class ReloadScopeEpochGuard
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-scope-epoch";
        static readonly object installGate = new object();
        static int generation;
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;

        [ThreadStatic] static int threadGeneration;
        [ThreadStatic] static int threadDepth;

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
            lock (installGate)
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
                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null) return false;

                    Type runtime = typeof(ReloadCandidateBridgeRuntime);
                    MethodInfo enter = runtime.GetMethod("EnterReloadScope", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo exit = runtime.GetMethod("ExitReloadScope", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo append = runtime.GetMethod("AppendCandidates", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo reset = runtime.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (enter == null || exit == null || append == null || reset == null) return false;

                    owner = harmonyCtor.Invoke(new object[] { HarmonyId });
                    if (owner == null) return false;
                    PatchNamed(owner, patch, harmonyMethodType, enter, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeEnter)) }));
                    PatchNamed(owner, patch, harmonyMethodType, exit, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeExit)) }));
                    PatchNamed(owner, patch, harmonyMethodType, append, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeAppend)) }));
                    PatchNamed(owner, patch, harmonyMethodType, reset, "postfix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(AfterReset)) }));

                    harmonyOwner = owner;
                    installed = true;
                    return true;
                }
                catch
                {
                    // Harmony.Patch mutates process-wide state one patch at a time. If any later
                    // patch in this four-method transaction fails, roll back this owner before an
                    // AssemblyLoad retry can attempt installation again. If rollback itself cannot
                    // be proven, enter terminal fail-closed state rather than risking duplicate or
                    // mixed-generation patches on a later assembly-load retry.
                    bool rolledBack = TryRollbackOwner(owner, unpatchSelf);
                    harmonyOwner = null;
                    installed = false;
                    terminalFailure = owner != null && !rolledBack;
                    return false;
                }
            }
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

        static MethodInfo Method(string name)
        {
            return typeof(ReloadScopeEpochGuard).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        static void BeforeEnter()
        {
            EnterScope();
        }

        static void BeforeExit()
        {
            ExitScope();
        }

        static bool BeforeAppend(object __2, ref object __result)
        {
            if (IsCurrentScope()) return true;
            __result = __2;
            return false;
        }

        static void AfterReset()
        {
            Invalidate();
        }

        static void EnterScope()
        {
            int current = Volatile.Read(ref generation);
            if (threadDepth == 0 || threadGeneration != current)
            {
                threadDepth = 0;
                threadGeneration = current;
            }
            threadDepth++;
        }

        static void ExitScope()
        {
            int current = Volatile.Read(ref generation);
            if (threadGeneration != current)
            {
                threadDepth = 0;
                threadGeneration = current;
                return;
            }
            if (threadDepth > 0) threadDepth--;
        }

        static bool IsCurrentScope()
        {
            int current = Volatile.Read(ref generation);
            return threadDepth > 0 && threadGeneration == current;
        }

        static void Invalidate()
        {
            Interlocked.Increment(ref generation);
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!IsCompatiblePatchMethod(method, harmonyMethodType)) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static bool IsCompatiblePatchMethod(MethodInfo method, Type harmonyMethodType)
        {
            if (method == null || harmonyMethodType == null || !string.Equals(method.Name, "Patch", StringComparison.Ordinal)) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 3 || parameters[0].ParameterType != typeof(MethodBase)) return false;

            bool prefix = false;
            bool postfix = false;
            for (int i = 1; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (parameter.ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameter.Name, "prefix", StringComparison.OrdinalIgnoreCase))
                {
                    if (prefix) return false;
                    prefix = true;
                }
                else if (string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase))
                {
                    if (postfix) return false;
                    postfix = true;
                }
            }
            return prefix && postfix;
        }

        static void PatchNamed(object owner, MethodInfo patch, Type harmonyMethodType, MethodInfo original, string kind, object harmonyMethod)
        {
            ParameterInfo[] parameters = patch.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            bool assigned = false;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, kind, StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = harmonyMethod;
                    assigned = true;
                }
            }
            if (!assigned) throw new MissingMethodException("Harmony.Patch parameter not found: " + kind);
            patch.Invoke(owner, args);
        }

        // Deterministic regression surface. These methods exercise the same state machine as the Harmony callbacks.
        internal static void ResetStateForRegression()
        {
            Interlocked.Exchange(ref generation, 0);
            threadGeneration = 0;
            threadDepth = 0;
        }

        internal static void EnterForRegression() => EnterScope();
        internal static void ExitForRegression() => ExitScope();
        internal static void InvalidateForRegression() => Invalidate();
        internal static bool IsCurrentForRegression() => IsCurrentScope();
    }
}
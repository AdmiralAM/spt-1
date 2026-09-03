using System;
using System.Reflection;
using System.Threading;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Publication fence for FastAccessReloadRuntime.PromoteReachability.
    /// Reachability is vanilla-first: B&A&HB may only turn false into true for a magazine
    /// under a registered fast-access wearable. A Reset or any FastAccess TryInstall
    /// completion invalidates an in-flight promotion transaction; persistent execution
    /// reference drift does the same. Failure restores the exact incoming vanilla bool.
    /// </summary>
    internal static class ReloadReachabilityPublicationFence
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-reachability-publication";
        static readonly object InstallGate = new object();
        static int generation;
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;

        internal readonly struct Snapshot
        {
            internal readonly bool VanillaResult;
            internal readonly Type ItemType;
            internal readonly Type MagazineType;
            internal readonly object GetAllParentItems;
            internal readonly object ReadTemplateId;
            internal readonly int Generation;

            internal Snapshot(
                bool vanillaResult,
                Type itemType,
                Type magazineType,
                object getAllParentItems,
                object readTemplateId,
                int generation)
            {
                VanillaResult = vanillaResult;
                ItemType = itemType;
                MagazineType = magazineType;
                GetAllParentItems = getAllParentItems;
                ReadTemplateId = readTemplateId;
                Generation = generation;
            }

            internal bool MayPublish()
            {
                return ItemType != null
                    && MagazineType != null
                    && GetAllParentItems != null
                    && ReadTemplateId != null
                    && Generation == Volatile.Read(ref generation)
                    && ReferenceEquals(ItemType, FastAccessReloadRuntime.ItemType)
                    && ReferenceEquals(MagazineType, FastAccessReloadRuntime.MagazineType)
                    && ReferenceEquals(GetAllParentItems, FastAccessReloadRuntime.GetAllParentItems)
                    && ReferenceEquals(ReadTemplateId, FastAccessReloadRuntime.ReadTemplateId);
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
                    MethodInfo promote = typeof(FastAccessReloadRuntime).GetMethod(
                        nameof(FastAccessReloadRuntime.PromoteReachability),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        new[] { typeof(object), typeof(bool).MakeByRefType() },
                        null);
                    MethodInfo reset = typeof(FastAccessReloadRuntime).GetMethod(
                        nameof(FastAccessReloadRuntime.Reset),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null);
                    MethodInfo install = typeof(FastAccessSlotPatches).GetMethod(
                        "TryInstall",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null);
                    MethodInfo before = typeof(ReloadReachabilityPublicationFence).GetMethod(nameof(BeforePromote), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo after = typeof(ReloadReachabilityPublicationFence).GetMethod(nameof(AfterPromote), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo invalidate = typeof(ReloadReachabilityPublicationFence).GetMethod(nameof(AfterAuthorityTransition), BindingFlags.Static | BindingFlags.NonPublic);

                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || rollback == null
                        || promote == null || promote.ReturnType != typeof(void)
                        || reset == null || reset.ReturnType != typeof(void)
                        || install == null || install.ReturnType != typeof(bool)
                        || before == null || after == null || invalidate == null)
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

                    object prefix = harmonyMethodCtor.Invoke(new object[] { before });
                    object postfix = harmonyMethodCtor.Invoke(new object[] { after });
                    object transitionPostfix = harmonyMethodCtor.Invoke(new object[] { invalidate });
                    object[] promoteArgs = BuildPatchArguments(patch, harmonyMethodType, promote, prefix, postfix);
                    object[] resetArgs = BuildPostfixArguments(patch, harmonyMethodType, reset, transitionPostfix);
                    object[] installArgs = BuildPostfixArguments(patch, harmonyMethodType, install, transitionPostfix);
                    if (promoteArgs == null || resetArgs == null || installArgs == null)
                    {
                        terminalFailure = true;
                        TryRollback(owner, rollback);
                        return false;
                    }

                    patch.Invoke(owner, promoteArgs);
                    patch.Invoke(owner, resetArgs);
                    patch.Invoke(owner, installArgs);
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
                    return false;
                }
            }
        }

        static void BeforePromote(ref bool result, out Snapshot __state)
        {
            __state = CaptureForRegression(result);
        }

        static void AfterPromote(ref bool result, Snapshot __state)
        {
            if (!__state.MayPublish())
                result = __state.VanillaResult;
        }

        static void AfterAuthorityTransition()
        {
            InvalidateForRegression();
        }

        internal static Snapshot CaptureForRegression(bool vanillaResult)
        {
            return new Snapshot(
                vanillaResult,
                FastAccessReloadRuntime.ItemType,
                FastAccessReloadRuntime.MagazineType,
                FastAccessReloadRuntime.GetAllParentItems,
                FastAccessReloadRuntime.ReadTemplateId,
                Volatile.Read(ref generation));
        }

        internal static bool ShouldPublishForRegression(Snapshot snapshot)
        {
            return snapshot.MayPublish();
        }

        internal static bool SelectForRegression(bool candidateResult, Snapshot snapshot)
        {
            return snapshot.MayPublish() ? candidateResult : snapshot.VanillaResult;
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
                bool prefix = false;
                bool postfix = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) prefix = true;
                    if (string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) postfix = true;
                }
                if (!prefix || !postfix) continue;
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

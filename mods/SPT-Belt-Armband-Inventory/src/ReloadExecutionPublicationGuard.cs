using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Outer transaction fence for ReloadCandidateBridgeRuntime.AppendCandidates.
    ///
    /// The bridge already pins the exact GetItemsInSlots MethodInfo, the one-slot15
    /// argument and accepted caller arrays across its lazy-enumeration boundaries.
    /// This guard covers the remaining mutable execution contract by capturing exact
    /// static identities at AppendCandidates entry and refusing publication if any of
    /// them drift before return. It never discovers candidates, performs inventory
    /// traversal, retries a query, or changes vanilla ordering: failure returns the
    /// exact incoming vanilla result object.
    /// </summary>
    internal static class ReloadExecutionPublicationGuard
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-execution-publication";
        static readonly object InstallGate = new object();
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;

        internal sealed class Snapshot
        {
            internal readonly MethodInfo GetItemsInSlots;
            internal readonly object BeltSlotsArgument;
            internal readonly Type ItemType;
            internal readonly Type MagazineType;
            internal readonly Type ReturnType;
            internal readonly object GetAllParentItems;
            internal readonly object ReadTemplateId;
            internal readonly bool IsComplete;

            internal Snapshot(
                MethodInfo getItemsInSlots,
                object beltSlotsArgument,
                Type itemType,
                Type magazineType,
                Type returnType,
                object getAllParentItems,
                object readTemplateId)
            {
                GetItemsInSlots = getItemsInSlots;
                BeltSlotsArgument = beltSlotsArgument;
                ItemType = itemType;
                MagazineType = magazineType;
                ReturnType = returnType;
                GetAllParentItems = getAllParentItems;
                ReadTemplateId = readTemplateId;
                IsComplete = getItemsInSlots != null
                    && beltSlotsArgument != null
                    && itemType != null
                    && magazineType != null
                    && returnType != null
                    && getAllParentItems != null
                    && readTemplateId != null;
            }

            internal bool MatchesCurrent()
            {
                return IsComplete
                    && ReferenceEquals(GetItemsInSlots, ReloadCandidateBridgeRuntime.GetItemsInSlots)
                    && ReferenceEquals(BeltSlotsArgument, ReloadCandidateBridgeRuntime.BeltSlotsArgument)
                    && ReferenceEquals(ItemType, ReloadCandidateBridgeRuntime.ItemType)
                    && ReferenceEquals(MagazineType, ReloadCandidateBridgeRuntime.MagazineType)
                    && ReferenceEquals(ReturnType, ReloadCandidateBridgeRuntime.ReturnType)
                    && ReferenceEquals(GetAllParentItems, ReloadCandidateBridgeRuntime.GetAllParentItems)
                    && ReferenceEquals(ReadTemplateId, ReloadCandidateBridgeRuntime.ReadTemplateId);
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
                    MethodInfo prefix = typeof(ReloadExecutionPublicationGuard).GetMethod(nameof(BeforeAppend), BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo postfix = typeof(ReloadExecutionPublicationGuard).GetMethod(nameof(AfterAppend), BindingFlags.Static | BindingFlags.NonPublic);

                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null
                        || append == null || append.ReturnType != typeof(object) || prefix == null || postfix == null)
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
                    object[] arguments = BuildPatchArguments(patch, harmonyMethodType, append, prefixHarmony, postfixHarmony);
                    if (arguments == null)
                    {
                        terminalFailure = true;
                        TryRollbackOwner(owner, unpatchSelf);
                        return false;
                    }

                    patch.Invoke(owner, arguments);
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
                else if (parameter.ParameterType == harmonyMethodType
                    && string.Equals(parameter.Name, "prefix", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = prefix;
                    prefixAssigned = true;
                }
                else if (parameter.ParameterType == harmonyMethodType
                    && string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase))
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

        static void BeforeAppend(out Snapshot __state)
        {
            __state = CaptureForRegression();
        }

        static void AfterAppend(object __2, Snapshot __state, ref object __result)
        {
            if (__state == null || !__state.MatchesCurrent())
                __result = __2;
        }

        internal static Snapshot CaptureForRegression()
        {
            return new Snapshot(
                ReloadCandidateBridgeRuntime.GetItemsInSlots,
                ReloadCandidateBridgeRuntime.BeltSlotsArgument,
                ReloadCandidateBridgeRuntime.ItemType,
                ReloadCandidateBridgeRuntime.MagazineType,
                ReloadCandidateBridgeRuntime.ReturnType,
                ReloadCandidateBridgeRuntime.GetAllParentItems,
                ReloadCandidateBridgeRuntime.ReadTemplateId);
        }

        internal static bool ShouldPublishForRegression(Snapshot snapshot)
        {
            return snapshot != null && snapshot.MatchesCurrent();
        }
    }
}

using System;
using System.Collections;
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
    /// bridge's static slot references. It also pins point-in-time contents for the four accepted mutable
    /// slot-array references after FastAccessSlotPatches.TryInstall, so same-reference in-place edits fail
    /// closed before the scoped pseudo-slot15 query. It never broadens candidate discovery or inventory traversal.
    /// </summary>
    internal static class ReloadScopeEpochGuard
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.reload-scope-epoch";
        static readonly object installGate = new object();
        static int generation;
        static bool installed;
        static bool terminalFailure;
        static object harmonyOwner;
        static SlotArraySnapshotSet slotArraySnapshots;

        [ThreadStatic] static int threadGeneration;
        [ThreadStatic] static int threadDepth;

        sealed class SlotArraySnapshot
        {
            internal readonly object Reference;
            internal readonly Type RuntimeType;
            internal readonly object[] Values;

            internal SlotArraySnapshot(object reference, Type runtimeType, object[] values)
            {
                Reference = reference;
                RuntimeType = runtimeType;
                Values = values;
            }

            internal bool Matches(object candidate)
            {
                if (!ReferenceEquals(Reference, candidate) || !(candidate is Array array)
                    || array.GetType() != RuntimeType || array.Length != Values.Length)
                    return false;

                for (int i = 0; i < Values.Length; i++)
                    if (!object.Equals(array.GetValue(i), Values[i]))
                        return false;
                return true;
            }
        }

        sealed class SlotArraySnapshotSet
        {
            internal readonly SlotArraySnapshot OriginalFastAccess;
            internal readonly SlotArraySnapshot InstalledFastAccess;
            internal readonly SlotArraySnapshot OriginalBindAvailable;
            internal readonly SlotArraySnapshot InstalledBindAvailable;

            internal SlotArraySnapshotSet(
                SlotArraySnapshot originalFastAccess,
                SlotArraySnapshot installedFastAccess,
                SlotArraySnapshot originalBindAvailable,
                SlotArraySnapshot installedBindAvailable)
            {
                OriginalFastAccess = originalFastAccess;
                InstalledFastAccess = installedFastAccess;
                OriginalBindAvailable = originalBindAvailable;
                InstalledBindAvailable = installedBindAvailable;
            }

            internal bool Matches(object candidate)
            {
                return OriginalFastAccess.Matches(candidate)
                    || InstalledFastAccess.Matches(candidate)
                    || OriginalBindAvailable.Matches(candidate)
                    || InstalledBindAvailable.Matches(candidate);
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
                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null)
                        terminalFailure = true;
                    if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null) return false;

                    Type runtime = typeof(ReloadCandidateBridgeRuntime);
                    MethodInfo enter = runtime.GetMethod("EnterReloadScope", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo exit = runtime.GetMethod("ExitReloadScope", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo append = runtime.GetMethod("AppendCandidates", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    MethodInfo reset = runtime.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (enter == null || exit == null || append == null || reset == null)
                    {
                        terminalFailure = true;
                        return false;
                    }

                    MethodInfo fastAccessInstall = typeof(FastAccessSlotPatches).GetMethod("TryInstall", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fastAccessInstall == null || fastAccessInstall.ReturnType != typeof(bool) || fastAccessInstall.GetParameters().Length != 0)
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
                    PatchNamed(owner, patch, harmonyMethodType, enter, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeEnter)) }));
                    PatchNamed(owner, patch, harmonyMethodType, exit, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeExit)) }));
                    PatchNamed(owner, patch, harmonyMethodType, append, "prefix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(BeforeAppend)) }));
                    PatchNamed(owner, patch, harmonyMethodType, reset, "postfix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(AfterReset)) }));
                    PatchNamed(owner, patch, harmonyMethodType, fastAccessInstall, "postfix", harmonyMethodCtor.Invoke(new object[] { Method(nameof(AfterFastAccessInstall)) }));

                    harmonyOwner = owner;
                    installed = true;
                    return true;
                }
                catch
                {
                    // Harmony.Patch mutates process-wide state one patch at a time. If any later
                    // patch in this five-method transaction fails, roll back this owner before an
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

        static bool BeforeAppend(object __1, object __2, ref object __result)
        {
            // The pinned SPT 4.1 GetItemsInSlots contract is exactly Item[], the fallback query
            // itself must still be the one-element pseudo-slot15 argument created at install time,
            // and the exact accepted slot-array reference must retain its install-time contents.
            // Any return/query/array-state drift is refused before AppendCandidates can invoke
            // Inventory.GetItemsInSlots. Re-prove the generation after content inspection so a
            // concurrent Reset/reinstall cannot bridge through a stale scope.
            if (IsCurrentScope() && HasExactRuntimeReturnContract() && HasPinnedFastAccessArrayContent(__1) && IsCurrentScope())
                return true;
            __result = __2;
            return false;
        }

        static bool HasExactRuntimeReturnContract()
        {
            try
            {
                Type itemType = ReloadCandidateBridgeRuntime.ItemType;
                Type declaredReturn = ReloadCandidateBridgeRuntime.ReturnType;
                MethodInfo getItems = ReloadCandidateBridgeRuntime.GetItemsInSlots;
                object beltArgument = ReloadCandidateBridgeRuntime.BeltSlotsArgument;
                if (itemType == null || declaredReturn == null || getItems == null || beltArgument == null) return false;

                Type exactArray = itemType.MakeArrayType();
                if (declaredReturn != exactArray || getItems.ReturnType != exactArray) return false;

                ParameterInfo[] parameters = getItems.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(beltArgument)) return false;

                // The install path creates either EquipmentSlot[1] or List<EquipmentSlot>
                // for the decompiled IEnumerable<EquipmentSlot> boundary. Re-prove the
                // observable query value here without accepting structurally-similar or
                // multi-slot collections that could broaden candidate discovery.
                if (!(beltArgument is IEnumerable values)) return false;
                int count = 0;
                foreach (object value in values)
                {
                    if (value == null || Convert.ToInt32(value) != RuntimeIdentity.DedicatedBeltEquipmentSlotValue) return false;
                    count++;
                    if (count > 1) return false;
                }
                return count == 1;
            }
            catch
            {
                return false;
            }
        }

        static void AfterFastAccessInstall(bool __result)
        {
            if (!__result)
            {
                Volatile.Write(ref slotArraySnapshots, null);
                return;
            }

            SlotArraySnapshotSet captured = CaptureCurrentSlotArrays();
            Volatile.Write(ref slotArraySnapshots, captured);
        }

        static SlotArraySnapshotSet CaptureCurrentSlotArrays()
        {
            try
            {
                SlotArraySnapshot originalFastAccess = CaptureArray(ReloadCandidateBridgeRuntime.OriginalFastAccessSlots);
                SlotArraySnapshot installedFastAccess = CaptureArray(ReloadCandidateBridgeRuntime.InstalledFastAccessSlots);
                SlotArraySnapshot originalBindAvailable = CaptureArray(ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots);
                SlotArraySnapshot installedBindAvailable = CaptureArray(ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots);
                if (originalFastAccess == null || installedFastAccess == null || originalBindAvailable == null || installedBindAvailable == null)
                    return null;
                return new SlotArraySnapshotSet(originalFastAccess, installedFastAccess, originalBindAvailable, installedBindAvailable);
            }
            catch
            {
                return null;
            }
        }

        static SlotArraySnapshot CaptureArray(object value)
        {
            if (!(value is Array array)) return null;
            object[] values = new object[array.Length];
            for (int i = 0; i < array.Length; i++) values[i] = array.GetValue(i);
            return new SlotArraySnapshot(value, array.GetType(), values);
        }

        static bool HasPinnedFastAccessArrayContent(object candidate)
        {
            try
            {
                SlotArraySnapshotSet snapshots = Volatile.Read(ref slotArraySnapshots);
                return snapshots != null && snapshots.Matches(candidate);
            }
            catch
            {
                return false;
            }
        }

        static void AfterReset()
        {
            Volatile.Write(ref slotArraySnapshots, null);
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

        // Deterministic regression surface. These methods exercise the same state machines as the Harmony callbacks.
        internal static void ResetStateForRegression()
        {
            Interlocked.Exchange(ref generation, 0);
            threadGeneration = 0;
            threadDepth = 0;
            Volatile.Write(ref slotArraySnapshots, null);
        }

        internal static void EnterForRegression() => EnterScope();
        internal static void ExitForRegression() => ExitScope();
        internal static void InvalidateForRegression() => Invalidate();
        internal static bool IsCurrentForRegression() => IsCurrentScope();
        internal static bool HasExactRuntimeReturnContractForRegression() => HasExactRuntimeReturnContract();
        internal static void CaptureSlotArraysForRegression() => Volatile.Write(ref slotArraySnapshots, CaptureCurrentSlotArrays());
        internal static bool HasPinnedFastAccessArrayContentForRegression(object candidate) => HasPinnedFastAccessArrayContent(candidate);
        internal static void ClearSlotArraySnapshotsForRegression() => Volatile.Write(ref slotArraySnapshots, null);
    }
}

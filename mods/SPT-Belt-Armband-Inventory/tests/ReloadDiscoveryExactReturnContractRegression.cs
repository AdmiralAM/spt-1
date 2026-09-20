using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadDiscoveryExactReturnContractRegression
{
    enum FakeSlot { ArmBand = 10, Belt = 15 }
    sealed class FakeItem { }

    sealed class ExactAndBroadInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<FakeSlot> slots) => Array.Empty<FakeItem>();
        public FakeItem[] GetItemsInSlots(FakeSlot[] slots) => Array.Empty<FakeItem>();
        public IEnumerable GetItemsInSlots(List<FakeSlot> slots) => Array.Empty<FakeItem>();
    }

    sealed class BroadOnlyInventory
    {
        public FakeItem[] GetItemsInSlots(IEnumerable<FakeSlot> slots) => Array.Empty<FakeItem>();
        public IEnumerable<FakeItem> GetItemsInSlots(FakeSlot[] slots) => Array.Empty<FakeItem>();
    }

    sealed class AmbiguousExactInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<FakeSlot> slots) => Array.Empty<FakeItem>();
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<FakeSlot> slots, bool unused) => Array.Empty<FakeItem>();
    }

    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo finder = typeof(FastAccessSlotPatches).GetMethod("FindGetItemsInSlots", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate discovery helper is missing.");

        MethodInfo exact = finder.Invoke(null, new object[] { typeof(ExactAndBroadInventory), typeof(FakeSlot), typeof(FakeItem) }) as MethodInfo;
        if (exact == null || exact.ReturnType != typeof(IEnumerable<FakeItem>)
            || exact.GetParameters().Length != 1
            || exact.GetParameters()[0].ParameterType != typeof(IEnumerable<FakeSlot>))
            throw new InvalidOperationException("Reload candidate discovery must select only exact IEnumerable<Item> GetItemsInSlots(IEnumerable<EquipmentSlot>) and ignore array/non-generic lookalikes.");

        object broadOnly = finder.Invoke(null, new object[] { typeof(BroadOnlyInventory), typeof(FakeSlot), typeof(FakeItem) });
        if (broadOnly != null)
            throw new InvalidOperationException("Reload candidate discovery must reject Item[] return or array-parameter lookalikes even though they are assignable to the pinned interfaces.");

        // The two-argument method is not a candidate; the one exact signature remains unique.
        MethodInfo unique = finder.Invoke(null, new object[] { typeof(AmbiguousExactInventory), typeof(FakeSlot), typeof(FakeItem) }) as MethodInfo;
        if (unique == null || unique.GetParameters().Length != 1)
            throw new InvalidOperationException("Reload candidate discovery must ignore non-exact arity while retaining the unique pinned signature.");
    }
}

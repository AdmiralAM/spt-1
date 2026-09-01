using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadDiscoveryExactReturnContractRegression
{
    enum FakeSlot
    {
        ArmBand = 10,
        Belt = 15
    }

    sealed class FakeItem { }

    sealed class ExactAndBroadInventory
    {
        public FakeItem[] GetItemsInSlots(FakeSlot[] slots) => Array.Empty<FakeItem>();
        public IEnumerable GetItemsInSlots(List<FakeSlot> slots) => Array.Empty<FakeItem>();
    }

    sealed class BroadOnlyInventory
    {
        public IEnumerable GetItemsInSlots(FakeSlot[] slots) => Array.Empty<FakeItem>();
    }

    sealed class AmbiguousExactInventory
    {
        public FakeItem[] GetItemsInSlots(FakeSlot[] slots) => Array.Empty<FakeItem>();
        public FakeItem[] GetItemsInSlots(List<FakeSlot> slots) => Array.Empty<FakeItem>();
    }

    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo finder = typeof(FastAccessSlotPatches).GetMethod(
            "FindGetItemsInSlots",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate discovery helper is missing.");

        MethodInfo exact = finder.Invoke(null, new object[]
        {
            typeof(ExactAndBroadInventory), typeof(FakeSlot), typeof(FakeItem)
        }) as MethodInfo;
        if (exact == null || exact.ReturnType != typeof(FakeItem[])
            || exact.GetParameters().Length != 1
            || exact.GetParameters()[0].ParameterType != typeof(FakeSlot[]))
            throw new InvalidOperationException("Reload candidate discovery must select only the exact Item[] GetItemsInSlots contract and ignore broader enumerable overloads.");

        object broadOnly = finder.Invoke(null, new object[]
        {
            typeof(BroadOnlyInventory), typeof(FakeSlot), typeof(FakeItem)
        });
        if (broadOnly != null)
            throw new InvalidOperationException("Reload candidate discovery must fail closed when only a broader enumerable return contract exists.");

        object ambiguous = finder.Invoke(null, new object[]
        {
            typeof(AmbiguousExactInventory), typeof(FakeSlot), typeof(FakeItem)
        });
        if (ambiguous != null)
            throw new InvalidOperationException("Reload candidate discovery must fail closed when multiple exact Item[] slot-carrier overloads exist.");
    }
}

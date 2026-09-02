using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadSlotParameterContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();

        MethodInfo exact = typeof(ExactInventory).GetMethod(nameof(ExactInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload slot parameter regression failed: exact method missing");
        MethodInfo drift = typeof(DriftInventory).GetMethod(nameof(DriftInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload slot parameter regression failed: drift method missing");

        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.GetItemsInSlots = exact;

        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact IEnumerable<Item>(IEnumerable<slot>) contract must pass");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = drift;
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "assignability-compatible return with a different slot element contract must fail closed");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = exact;
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact slot parameter contract must recover after rejected drift");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    sealed class ExactInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots) => Array.Empty<FakeItem>();
    }

    sealed class DriftInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<long> slots) => Array.Empty<FakeItem>();
    }

    sealed class FakeItem { }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload slot parameter regression failed: " + message);
    }
}

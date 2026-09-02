using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateReturnContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", new FakeItem("foreign-root"));
        var beltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        IEnumerable<FakeItem> vanilla = new List<FakeItem> { vanillaMagazine };
        object slots = new object[] { "original-fast" };
        var inventory = new FakeInventory(new FakeItem[] { beltMagazine });

        Configure(inventory, slots);
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact IEnumerable<Item> GetItemsInSlots + one pseudo-slot15 query must pass the pre-bridge epoch gate");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object healthy = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(!ReferenceEquals(healthy, vanilla),
            "healthy pinned interface contract must append the exact Belt descendant");
        Assert(((IEnumerable<FakeItem>)healthy).SequenceEqual(new FakeItem[] { vanillaMagazine, beltMagazine }),
            "successful replacement must retain vanilla order as a strict prefix and append only the Belt candidate");
        Assert(inventory.Calls == 1, "healthy bridge must execute exactly one pseudo-slot15 query");

        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "declared Item[] drift must be rejected even though Item[] implements IEnumerable<Item>");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object incompatibleReturn = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(incompatibleReturn, vanilla) && inventory.Calls == 1,
            "declared array drift must preserve exact vanilla object before a second query");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeArrayReturnInventory).GetMethod(nameof(FakeArrayReturnInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: array-return drift method missing");
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "method-declared Item[] drift must fail closed despite interface assignability");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object driftMethodResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(driftMethodResult, vanilla) && inventory.Calls == 1,
            "method return drift must fail before inventory invocation");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: exact GetItemsInSlots missing");
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact interface contract must recover after rejected return drift");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[]
        {
            RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
            RuntimeIdentity.DedicatedBeltEquipmentSlotValue
        };
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "multi-slot fallback argument must fail closed before inventory enumeration");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object multiSlotResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(multiSlotResult, vanilla) && inventory.Calls == 1,
            "multi-slot state must preserve exact vanilla identity before querying");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue - 1 };
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "wrong pseudo-slot fallback argument must fail closed before inventory enumeration");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object wrongSlotResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(wrongSlotResult, vanilla) && inventory.Calls == 1,
            "wrong pseudo-slot state must preserve exact vanilla identity before querying");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact one-slot query must recover after rejected query-state drift");

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: reloadDepth field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: reentrant field missing");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "return/query-contract fail-closed paths must not leak reload scope or reentrancy state");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    static void Configure(FakeInventory inventory, object slots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object[] { "original-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object[] { "original-bind" };
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object[] { "original-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload candidate return-contract regression failed closed unexpectedly: " + message);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;
        internal int Calls { get; private set; }
        internal FakeInventory(FakeItem[] items) { this.items = items; }
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
        {
            Calls++;
            return items.Concat(Array.Empty<FakeItem>());
        }
    }

    sealed class FakeArrayReturnInventory
    {
        public FakeItem[] GetItemsInSlots(IEnumerable<int> slots) => Array.Empty<FakeItem>();
    }

    class FakeItem
    {
        internal string TemplateId { get; }
        internal IEnumerable Parents { get; }
        internal FakeItem(string templateId, params FakeItem[] parents)
        {
            TemplateId = templateId;
            Parents = parents ?? Array.Empty<FakeItem>();
        }
    }

    sealed class FakeMagazine : FakeItem
    {
        internal FakeMagazine(string templateId, params FakeItem[] parents) : base(templateId, parents) { }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload candidate return-contract regression failed: " + message);
    }
}

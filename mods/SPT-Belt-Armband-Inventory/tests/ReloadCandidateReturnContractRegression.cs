using System;
using System.Collections;
using System.Collections.Generic;
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
        var vanilla = new FakeItem[] { vanillaMagazine };
        var slots = new object();
        var inventory = new FakeInventory(new FakeItem[] { beltMagazine });

        Configure(inventory, slots);
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact Item[] GetItemsInSlots + one pseudo-slot15 query must pass the pre-bridge epoch gate");

        // The bridge is defined around the exact SPT 4.1 Item[] contract. If the
        // live vanilla result is not that array shape, fail closed before querying
        // or allocating any Belt fallback result.
        var driftedVanilla = new List<FakeItem> { vanillaMagazine };
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object driftedResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, driftedVanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(driftedResult, driftedVanilla),
            "non-Item[] vanilla result shape must preserve exact vanilla object identity");
        Assert(inventory.Calls == 0,
            "non-Item[] vanilla result shape must fail closed before the pseudo-slot15 fallback query");

        // The primary bridge now duplicates the exact return-contract proof instead
        // of depending solely on the separate epoch-owner Harmony prefix.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(List<FakeItem>);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "declared List return drift must be rejected by the epoch gate");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object incompatibleReturn = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(incompatibleReturn, vanilla),
            "primary bridge must preserve exact vanilla result on declared return drift");
        Assert(inventory.Calls == 0,
            "primary bridge must reject declared return drift before any pseudo-slot15 query even without relying on the epoch prefix");

        // Pinned runtime contract is stronger than covariance/assignability. A
        // GetItemsInSlots method that returns IEnumerable<Item> can carry Item[]
        // values, but it is not the SPT 4.1 boundary and must be refused directly.
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeDriftInventory).GetMethod(nameof(FakeDriftInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: drift GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "IEnumerable<Item> GetItemsInSlots drift must fail closed despite Item[] assignability");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object driftMethodResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(driftMethodResult, vanilla),
            "primary bridge must preserve exact vanilla result on GetItemsInSlots return-shape drift");
        Assert(inventory.Calls == 0,
            "drifted GetItemsInSlots contract must not reach the exact inventory query");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: exact GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact contract must recover after a rejected drifted method without a permanent circuit breaker");

        // Query-state corruption must also fail inside AppendCandidates itself.
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
        Assert(ReferenceEquals(multiSlotResult, vanilla) && inventory.Calls == 0,
            "primary bridge must reject multi-slot state before querying and preserve exact vanilla identity");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue - 1 };
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "wrong pseudo-slot fallback argument must fail closed before inventory enumeration");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object wrongSlotResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(wrongSlotResult, vanilla) && inventory.Calls == 0,
            "primary bridge must reject wrong pseudo-slot state before querying and preserve exact vanilla identity");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact one-slot query must recover after rejected query-state drift");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object recovered = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(!ReferenceEquals(recovered, vanilla),
            "healthy exact contract must still append the exact Belt descendant after fail-closed drift cases");
        Assert(inventory.Calls == 1,
            "healthy recovery must perform exactly one bounded pseudo-slot15 query");

        // The method can still be declared FakeItem[] while returning a covariant
        // FakeMagazine[] runtime object. That is CLR-valid, but it is not the exact
        // pinned SPT Item[] runtime boundary. Query once, then fail closed before
        // enumerating/merging the slot15 result and preserve the vanilla object.
        var covariantInventory = new FakeInventory(new FakeMagazine[] { beltMagazine });
        Configure(covariantInventory, slots);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object covariantResult = ReloadCandidateBridgeRuntime.AppendCandidates(covariantInventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(covariantResult, vanilla),
            "covariant Magazine[] fallback runtime shape must preserve exact vanilla result identity");
        Assert(covariantInventory.Calls == 1,
            "covariant runtime-shape rejection must occur immediately after the single bounded pseudo-slot15 query");

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: reloadDepth field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: reentrant field missing");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "return/query-contract fail-closed paths must not leak reload scope or reentrancy state");

        ReloadCandidateBridgeRuntime.Reset();
    }

    static void Configure(FakeInventory inventory, object slots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object();
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload candidate return-contract regression failed closed unexpectedly: " + message);
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;
        internal int Calls { get; private set; }

        internal FakeInventory(FakeItem[] items)
        {
            this.items = items;
        }

        public FakeItem[] GetItemsInSlots(object slots)
        {
            Calls++;
            return items;
        }
    }

    sealed class FakeDriftInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(object slots)
        {
            return Array.Empty<FakeItem>();
        }
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
        internal FakeMagazine(string templateId, params FakeItem[] parents)
            : base(templateId, parents)
        {
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload candidate return-contract regression failed: " + message);
    }
}

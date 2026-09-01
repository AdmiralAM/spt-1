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

        // Even after a valid exact-Belt candidate is discovered, a return-type
        // contract mismatch must discard the proposed merge rather than widening
        // Harmony's result type or leaking a partially-built replacement.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(List<FakeItem>);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "declared List return drift must be rejected before AppendCandidates");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object incompatibleReturn = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(incompatibleReturn, vanilla),
            "incompatible declared return type must discard Belt fallback and return exact vanilla result");
        Assert(inventory.Calls == 1,
            "direct bridge regression still proves legacy inner guard after one bounded exact-Belt slot query");

        // Pinned runtime contract is stronger than covariance/assignability. A
        // GetItemsInSlots method that returns IEnumerable<Item> can carry Item[]
        // values, but it is not the SPT 4.1 boundary and must be refused by the
        // epoch prefix before the fallback method can issue any query.
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeDriftInventory).GetMethod(nameof(FakeDriftInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: drift GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "IEnumerable<Item> GetItemsInSlots drift must fail closed despite Item[] assignability");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate return-contract regression failed: exact GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact contract must recover after a rejected drifted method without a permanent circuit breaker");

        // Query-state corruption must also fail before Inventory.GetItemsInSlots.
        // This keeps the fallback bounded to the one dedicated pseudo-slot even if
        // another participant mutates the bridge's process-wide static state.
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[]
        {
            RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
            RuntimeIdentity.DedicatedBeltEquipmentSlotValue
        };
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "multi-slot fallback argument must fail closed before inventory enumeration");

        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue - 1 };
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "wrong pseudo-slot fallback argument must fail closed before inventory enumeration");

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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        MethodInfo exact = typeof(FakeInventory).GetMethod(
            nameof(FakeInventory.GetItemsInSlots),
            new[] { typeof(IEnumerable<int>) })
            ?? throw new InvalidOperationException("Reload slot parameter regression failed: exact method missing");
        MethodInfo drift = typeof(FakeInventory).GetMethod(
            nameof(FakeInventory.GetItemsInSlots),
            new[] { typeof(IEnumerable<long>) })
            ?? throw new InvalidOperationException("Reload slot parameter regression failed: drift method missing");

        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var beltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", new FakeItem("foreign-root"));
        IEnumerable<FakeItem> vanilla = new FakeItem[] { vanillaMagazine };
        var inventory = new FakeInventory(beltMagazine);

        var originalFastAccessSlots = new object[] { "original-fast" };
        var installedFastAccessSlots = new object[] { "original-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var originalBindAvailableSlots = new object[] { "original-bind" };
        var installedBindAvailableSlots = new object[] { "original-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };

        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFastAccessSlots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFastAccessSlots;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBindAvailableSlots;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBindAvailableSlots;
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload slot parameter regression failed closed unexpectedly: " + message);
        ReloadCandidateBridgeRuntime.GetItemsInSlots = exact;
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact IEnumerable<Item>(IEnumerable<slot>) contract must pass the epoch guard");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object healthy = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, originalFastAccessSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        var healthyItems = ((IEnumerable<FakeItem>)healthy).ToArray();
        Assert(inventory.QueryCount == 1,
            "exact primary bridge contract executes exactly one pseudo-slot15 fallback query");
        Assert(healthyItems.Length == 2 && ReferenceEquals(healthyItems[0], vanillaMagazine)
            && ReferenceEquals(healthyItems[1], beltMagazine),
            "exact primary bridge contract preserves vanilla prefix and appends the exact Magazine Belt descendant");

        inventory.QueryCount = 0;
        ReloadCandidateBridgeRuntime.GetItemsInSlots = drift;
        Assert(!ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "assignability-compatible return with a different slot element contract must fail closed in the epoch guard");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object rejected = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, originalFastAccessSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(rejected, vanilla),
            "primary AppendCandidates must preserve exact vanilla result identity on slot-parameter drift");
        Assert(inventory.QueryCount == 0,
            "primary AppendCandidates must reject slot-parameter drift before any fallback query");

        ReloadCandidateBridgeRuntime.GetItemsInSlots = exact;
        Assert(ReloadScopeEpochGuard.HasExactRuntimeReturnContractForRegression(),
            "exact slot parameter contract must recover after rejected drift");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object recovered = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, originalFastAccessSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(inventory.QueryCount == 1 && !ReferenceEquals(recovered, vanilla),
            "restoring the exact method recovers one-query Belt fallback without stale failure state");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    sealed class FakeInventory
    {
        readonly FakeItem beltMagazine;
        internal int QueryCount;

        internal FakeInventory(FakeItem beltMagazine)
        {
            this.beltMagazine = beltMagazine;
        }

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
        {
            QueryCount++;
            return new[] { beltMagazine };
        }

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<long> slots)
        {
            QueryCount++;
            return new[] { beltMagazine };
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
        internal FakeMagazine(string templateId, params FakeItem[] parents) : base(templateId, parents) { }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload slot parameter regression failed: " + message);
    }
}

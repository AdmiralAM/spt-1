using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCapturedExecutionStateRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var belt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var magazine = new FakeMagazine("belt-magazine", belt);
        int[] slots = { 1, 2, 3 };

        RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ItemType = typeof(object), "ItemType");
        RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeItem), "MagazineType");
        RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<object>), "ReturnType");
        RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.GetAllParentItems = _ => Array.Empty<object>(), "GetAllParentItems");
        RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ReadTemplateId = _ => "replacement", "ReadTemplateId");

        Configure(slots);
        IEnumerable<FakeItem> vanilla = new FakeItem[] { new FakeMagazine("vanilla") };
        var postQueryInventory = new FakeInventory(magazine, () => ReloadCandidateBridgeRuntime.ReadTemplateId = _ => "replacement");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object postQuery = ReloadCandidateBridgeRuntime.AppendCandidates(postQueryInventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(postQuery, vanilla),
            "execution-state drift during lazy Belt enumeration must preserve exact vanilla identity");
        Require(postQueryInventory.QueryCount == 1,
            "post-query execution-state drift must not retry or redirect the single slot15 query");

        Configure(slots);
        var healthyInventory = new FakeInventory(magazine, null);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object healthy = ReloadCandidateBridgeRuntime.AppendCandidates(healthyInventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        FakeItem[] merged = ((IEnumerable<FakeItem>)healthy).ToArray();
        Require(healthyInventory.QueryCount == 1 && merged.Length == 2 && ReferenceEquals(merged[1], magazine),
            "restored exact execution state must retain one-query vanilla-prefix Belt append behavior");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    private static void RunPreQueryDrift(int[] slots, FakeMagazine magazine, Action drift, string label)
    {
        Configure(slots);
        var inventory = new FakeInventory(magazine, null);
        IEnumerable<FakeItem> vanilla = EnumerateWithDrift(new FakeMagazine("vanilla-" + label), drift);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object result = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(result, vanilla), label + " drift during lazy vanilla enumeration must preserve exact vanilla identity");
        Require(inventory.QueryCount == 0, label + " drift must fail closed before the fallback query");
    }

    private static IEnumerable<FakeItem> EnumerateWithDrift(FakeItem item, Action drift)
    {
        drift();
        yield return item;
    }

    private static void Configure(int[] slots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload captured execution-state regression failed: fake query method missing.");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new[] { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new[] { 4, 5, 6 };
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new[] { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload captured execution-state regression failed closed unexpectedly: " + message);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
    }

    private sealed class FakeInventory
    {
        private readonly FakeItem item;
        private readonly Action? duringEnumeration;

        internal FakeInventory(FakeItem item, Action? duringEnumeration)
        {
            this.item = item;
            this.duringEnumeration = duringEnumeration;
        }

        internal int QueryCount { get; private set; }

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
        {
            QueryCount++;
            return Enumerate();
        }

        private IEnumerable<FakeItem> Enumerate()
        {
            duringEnumeration?.Invoke();
            yield return item;
        }
    }

    private class FakeItem
    {
        internal string TemplateId { get; }
        internal IEnumerable Parents { get; }

        internal FakeItem(string templateId, params FakeItem[] parents)
        {
            TemplateId = templateId;
            Parents = parents ?? Array.Empty<FakeItem>();
        }
    }

    private sealed class FakeMagazine : FakeItem
    {
        internal FakeMagazine(string templateId, params FakeItem[] parents) : base(templateId, parents) { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload captured execution-state regression failed: " + message + ".");
    }
}

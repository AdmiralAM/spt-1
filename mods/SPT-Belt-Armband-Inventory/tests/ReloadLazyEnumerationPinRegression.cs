using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadLazyEnumerationPinRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var belt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var magazine = new FakeMagazine("belt-magazine", belt);
        IEnumerable<FakeItem> vanilla = new FakeItem[] { new FakeMagazine("vanilla-magazine") };

        int[] originalFast = { 1, 2, 3 };
        int[] installedFast = { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        int[] originalBind = { 4, 5, 6 };
        int[] installedBind = { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };

        Configure(originalFast, installedFast, originalBind, installedBind);

        var driftingInventory = new FakeInventory(magazine, () => originalFast[1] = 99);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object drifted = ReloadCandidateBridgeRuntime.AppendCandidates(driftingInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(drifted, vanilla),
            "same-reference slot-array drift during lazy Belt enumeration must preserve the exact vanilla enumerable object");

        originalFast[1] = 2;
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "restoring the exact captured content must restore the recognized retained-array pin");

        var healthyInventory = new FakeInventory(magazine, null);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object healthy = ReloadCandidateBridgeRuntime.AppendCandidates(healthyInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        FakeItem[] merged = ((IEnumerable<FakeItem>)healthy).ToArray();
        Require(!ReferenceEquals(healthy, vanilla),
            "healthy lazy Belt enumeration with an unchanged pinned array must publish a replacement sequence");
        Require(merged.Length == 2 && ReferenceEquals(merged[1], magazine),
            "healthy lazy Belt enumeration must preserve vanilla prefix and append the exact Belt descendant");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    private static void Configure(object originalFast, object installedFast, object originalBind, object installedBind)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload lazy enumeration pin regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload lazy enumeration pin regression failed closed unexpectedly: " + message);
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

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
        {
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
            throw new InvalidOperationException("Reload lazy enumeration pin regression failed: " + message + ".");
    }
}

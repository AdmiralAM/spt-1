using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateRecognizedArrayMatrixRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var belt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var magazine = new FakeMagazine("belt-mag", belt);
        var inventory = new FakeInventory(new FakeItem[] { magazine });
        var vanilla = Array.Empty<FakeItem>();

        object originalFast = new object[] { "original-fast" };
        object installedFast = new object[] { "original-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        object originalBind = new object[] { "original-bind" };
        object installedBind = new object[] { "original-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        Configure(originalFast, installedFast, originalBind, installedBind);

        object[] recognized = { originalFast, installedFast, originalBind, installedBind };
        for (int i = 0; i < recognized.Length; i++)
        {
            int before = inventory.Calls;
            ReloadCandidateBridgeRuntime.EnterReloadScope();
            object result = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognized[i], vanilla);
            ReloadCandidateBridgeRuntime.ExitReloadScope(null);
            Assert(result is FakeItem[] items && items.Length == 1 && ReferenceEquals(items[0], magazine),
                "each exact original/installed FastAccess/BindAvailable array reference with a captured content pin must retain the bounded Belt fallback");
            Assert(inventory.Calls == before + 1,
                "each recognized array path must issue exactly one pseudo-slot15 fallback query");
        }

        // Structural/copy equivalence is intentionally insufficient. This protects
        // vanilla-first behavior from unrelated callers that happen to pass an
        // equivalent slot collection through GetItemsInSlots.
        object copiedButForeign = new object[] { "original-fast" };
        int foreignBefore = inventory.Calls;
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object foreignResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, copiedButForeign, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(foreignResult, vanilla),
            "foreign/copy slot-array identity must return the exact vanilla result object");
        Assert(inventory.Calls == foreignBefore,
            "foreign/copy slot-array identity must fail before any pseudo-slot15 query");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    static void Configure(object originalFast, object installedFast, object originalBind, object installedBind)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("recognized-array regression: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException("recognized-array regression failed closed unexpectedly: " + message);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;
        internal int Calls { get; private set; }
        internal FakeInventory(FakeItem[] items) => this.items = items;
        public FakeItem[] GetItemsInSlots(object slots)
        {
            Calls++;
            return items;
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
        if (!condition) throw new InvalidOperationException("Reload recognized-array regression failed: " + message);
    }
}

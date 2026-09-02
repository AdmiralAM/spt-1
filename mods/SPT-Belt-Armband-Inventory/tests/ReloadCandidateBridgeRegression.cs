using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateBridgeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, false, true),
            "exact fast-access candidate enumeration is bridged only inside reload scope");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(false, false, true),
            "non-reload callers keep vanilla candidate enumeration");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, true, true),
            "bridge invocation is reentrancy-safe");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, false, false),
            "unrelated slot enumerations are not widened");
        Assert(FastAccessSlotPolicy.ShouldReuseVanillaReloadCandidates(false),
            "no exact Belt fallback keeps the original vanilla result object");
        Assert(!FastAccessSlotPolicy.ShouldReuseVanillaReloadCandidates(true),
            "a real exact Belt fallback is the only reason to allocate a merged result");

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate bridge regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate bridge regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "reset clears per-thread reload scope/reentrancy state");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        Assert((int)depth.GetValue(null)! == 2,
            "nested Reload/QuickReload entry increments scoped depth instead of collapsing to a boolean");

        var original = new InvalidOperationException("sentinel");
        Exception returned = ReloadCandidateBridgeRuntime.ExitReloadScope(original);
        Assert(ReferenceEquals(original, returned) && (int)depth.GetValue(null)! == 1,
            "finalizer preserves the original exception while unwinding exactly one nested scope");

        returned = ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(returned == null && (int)depth.GetValue(null)! == 0,
            "outer finalizer returns to the vanilla non-reload boundary");

        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0,
            "defensive extra unwind cannot make reload scope negative or leak future bridging");

        ExerciseCandidateMerge();
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    static void ExerciseCandidateMerge()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var foreignRoot = new FakeItem("foreign-container");
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", foreignRoot);
        var duplicateMagazine = new FakeMagazine("already-vanilla", exactBelt);
        var exactBeltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        var foreignMagazine = new FakeMagazine("foreign-magazine", foreignRoot);
        var nonMagazine = new FakeItem("not-a-magazine", exactBelt);
        var vanilla = new FakeItem[] { vanillaMagazine, duplicateMagazine };
        var originalFastAccessSlots = new object[] { "original-fast" };
        var installedFastAccessSlots = new object[] { "original-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var originalBindAvailableSlots = new object[] { "original-bind" };
        var installedBindAvailableSlots = new object[] { "original-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var inventory = new FakeInventory(new FakeItem[]
        {
            duplicateMagazine,
            foreignMagazine,
            nonMagazine,
            exactBeltMagazine
        });

        ConfigureFakeRuntime(
            originalFastAccessSlots,
            installedFastAccessSlots,
            originalBindAvailableSlots,
            installedBindAvailableSlots);

        object[] recognizedSlotReferences =
        {
            originalFastAccessSlots,
            installedFastAccessSlots,
            originalBindAvailableSlots,
            installedBindAvailableSlots
        };
        foreach (object recognizedSlots in recognizedSlotReferences)
        {
            ReloadCandidateBridgeRuntime.EnterReloadScope();
            object recognizedObject = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanilla);
            ReloadCandidateBridgeRuntime.ExitReloadScope(null);
            Assert(recognizedObject is FakeItem[],
                "all four exact FastAccess/BindAvailable retained-or-installed references preserve Item[] shape");
            var recognized = (FakeItem[])recognizedObject;
            Assert(recognized.Length == 3 && ReferenceEquals(recognized[2], exactBeltMagazine),
                "all four exact FastAccess/BindAvailable references with captured content pins activate the same exact Belt fallback");
        }

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object mergedObject = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, originalFastAccessSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(mergedObject is FakeItem[], "bridge preserves the exact Item[]-compatible return shape");
        var merged = (FakeItem[])mergedObject;
        Assert(!ReferenceEquals(merged, vanilla), "one real exact Belt fallback allocates a replacement result");
        Assert(merged.Length == 3, "only one unique exact Magazine Belt descendant is appended");
        Assert(ReferenceEquals(merged[0], vanillaMagazine) && ReferenceEquals(merged[1], duplicateMagazine),
            "complete vanilla candidate prefix and order are preserved by reference");
        Assert(ReferenceEquals(merged[2], exactBeltMagazine),
            "exact Magazine Belt descendant is appended after the vanilla prefix");

        inventory.Items = new FakeItem[] { duplicateMagazine, foreignMagazine, nonMagazine };
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object noOpObject = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, originalFastAccessSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(noOpObject, vanilla),
            "duplicate, foreign and non-magazine Belt candidates keep the exact vanilla result object");

        inventory.Items = new FakeItem[] { exactBeltMagazine };
        object unrelatedSlots = new object();
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object unrelatedObject = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, unrelatedSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(unrelatedObject, vanilla),
            "structurally unrelated slot enumeration cannot activate the Belt fallback even during reload scope");
    }

    static void ConfigureFakeRuntime(
        object originalFastAccessSlots,
        object installedFastAccessSlots,
        object originalBindAvailableSlots,
        object installedBindAvailableSlots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate bridge regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFastAccessSlots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFastAccessSlots;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBindAvailableSlots;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBindAvailableSlots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException("Reload candidate bridge regression failed closed unexpectedly: " + message);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
    }

    sealed class FakeInventory
    {
        internal FakeItem[] Items;

        internal FakeInventory(FakeItem[] items)
        {
            Items = items;
        }

        public FakeItem[] GetItemsInSlots(object slots)
        {
            return Items;
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
        if (!condition) throw new InvalidOperationException("Reload candidate bridge regression failed: " + message);
    }
}

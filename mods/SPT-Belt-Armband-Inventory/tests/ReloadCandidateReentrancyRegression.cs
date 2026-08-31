using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateReentrancyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", new FakeItem("vanilla-container"));
        var exactBeltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        var vanilla = new FakeItem[] { vanillaMagazine };
        var recognizedSlots = new object();
        var inventory = new FakeInventory(recognizedSlots, vanilla, new FakeItem[] { exactBeltMagazine });

        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate reentrancy regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new object();
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = recognizedSlots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message =>
            throw new InvalidOperationException("Reload candidate reentrancy regression failed closed unexpectedly: " + message);

        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate reentrancy regression failed: reentrant state field missing");
        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate reentrancy regression failed: reloadDepth state field missing");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object result = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(inventory.GetItemsCalls == 1,
            "the scoped Belt fallback may invoke GetItemsInSlots exactly once; its Harmony postfix must not recurse into another Belt enumeration");
        Assert(ReferenceEquals(inventory.NestedBridgeResult, vanilla),
            "a nested candidate-postfix attempt while the owned Belt enumeration is active must return the exact vanilla result object");
        Assert(result is FakeItem[], "outer bridge must preserve the exact Item[] return shape");
        var merged = (FakeItem[])result;
        Assert(merged.Length == 2
            && ReferenceEquals(merged[0], vanillaMagazine)
            && ReferenceEquals(merged[1], exactBeltMagazine),
            "outer bridge must remain vanilla-first and append only the exact Magazine Belt candidate after suppressing nested recursion");
        Assert(!(bool)reentrant.GetValue(null)! && (int)depth.GetValue(null)! == 0,
            "successful recursive-boundary execution must leave no reentrancy or reload-scope residue");

        ReloadCandidateBridgeRuntime.Reset();
    }

    sealed class FakeInventory
    {
        readonly object recognizedSlots;
        readonly FakeItem[] vanilla;
        readonly FakeItem[] items;

        internal int GetItemsCalls { get; private set; }
        internal object NestedBridgeResult { get; private set; }

        internal FakeInventory(object recognizedSlots, FakeItem[] vanilla, FakeItem[] items)
        {
            this.recognizedSlots = recognizedSlots;
            this.vanilla = vanilla;
            this.items = items;
        }

        public FakeItem[] GetItemsInSlots(object slots)
        {
            GetItemsCalls++;
            NestedBridgeResult = ReloadCandidateBridgeRuntime.AppendCandidates(this, recognizedSlots, vanilla);
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
        internal FakeMagazine(string templateId, params FakeItem[] parents)
            : base(templateId, parents)
        {
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload candidate reentrancy regression failed: " + message);
    }
}

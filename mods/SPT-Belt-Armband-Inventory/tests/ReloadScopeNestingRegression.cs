using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadScopeNestingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var slots = new object();
        var beltSlotArgument = new object();
        var beltParent = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var beltMagazine = new FakeMagazine("belt-mag");
        var vanillaMagazine = new FakeMagazine("vanilla-mag");
        var vanilla = new FakeItem[] { vanillaMagazine };
        var inventory = new FakeInventory(new FakeItem[] { beltMagazine });

        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload scope nesting regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = beltSlotArgument;
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item =>
            ReferenceEquals(item, beltMagazine) ? new FakeItem[] { beltParent } : Array.Empty<FakeItem>();
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload scope nesting regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload scope nesting regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        Assert((int)depth.GetValue(null)! == 2, "nested Reload/QuickReload prefixes must increment scope depth independently");

        object nested = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(nested is FakeItem[] nestedItems && nestedItems.Length == 2,
            "exact Magazine Belt fallback must remain active inside a nested reload scope");
        Assert(ReferenceEquals(((FakeItem[])nested)[0], vanillaMagazine) && ReferenceEquals(((FakeItem[])nested)[1], beltMagazine),
            "nested scope merge must preserve vanilla prefix and append only the exact Belt magazine");
        Assert((int)depth.GetValue(null)! == 2 && !(bool)reentrant.GetValue(null)!,
            "candidate enumeration must not consume nested reload depth or leak reentrancy");

        var syntheticInnerException = new InvalidOperationException("synthetic inner reload failure");
        Exception returnedInner = ReloadCandidateBridgeRuntime.ExitReloadScope(syntheticInnerException);
        Assert(ReferenceEquals(returnedInner, syntheticInnerException),
            "inner Harmony finalizer must preserve the original reload exception object");
        Assert((int)depth.GetValue(null)! == 1,
            "inner Harmony finalizer must unwind exactly one nested reload scope");

        object outerStillActive = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(outerStillActive is FakeItem[] outerItems && outerItems.Length == 2 && ReferenceEquals(outerItems[1], beltMagazine),
            "outer reload scope must remain active after the nested finalizer unwinds");

        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "outer Harmony finalizer must return reload scope state to a clean baseline");

        object outside = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(ReferenceEquals(outside, vanilla),
            "outside Reload/QuickReload scope the bridge must return the exact vanilla result object");

        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0,
            "defensive extra finalizer calls must clamp at zero instead of underflowing reload scope state");

        ReloadCandidateBridgeRuntime.Reset();
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;

        internal FakeInventory(FakeItem[] items)
        {
            this.items = items;
        }

        public FakeItem[] GetItemsInSlots(object slots)
        {
            return items;
        }
    }

    class FakeItem
    {
        internal FakeItem(string templateId)
        {
            TemplateId = templateId;
        }

        internal string TemplateId { get; }
    }

    sealed class FakeMagazine : FakeItem
    {
        internal FakeMagazine(string templateId) : base(templateId)
        {
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload scope nesting regression failed: " + message);
    }
}

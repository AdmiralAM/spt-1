using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using SPTBeltArmbandInventory;

internal static class ReloadScopeThreadIsolationRegression
{
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
            ?? throw new InvalidOperationException("Reload thread isolation regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = beltSlotArgument;
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item =>
            ReferenceEquals(item, beltMagazine) ? new FakeItem[] { beltParent } : Array.Empty<FakeItem>();
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload thread isolation regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload thread isolation regression failed: reentrant state field missing");

        Exception foreignFailure = null;
        object foreignResult = null;
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        try
        {
            Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
                "calling thread must own one clean active reload scope before foreign-thread execution");

            Thread foreign = new Thread(() =>
            {
                try
                {
                    int foreignDepth = (int)depth.GetValue(null)!;
                    bool foreignReentrant = (bool)reentrant.GetValue(null)!;
                    object result = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
                    if (foreignDepth != 0 || foreignReentrant)
                        throw new InvalidOperationException("ThreadStatic reload state leaked from the owner thread");
                    if (!ReferenceEquals(result, vanilla))
                        throw new InvalidOperationException("foreign thread activated Magazine Belt fallback outside its own reload scope");
                    if ((int)depth.GetValue(null)! != 0 || (bool)reentrant.GetValue(null)!)
                        throw new InvalidOperationException("foreign-thread no-op acquired reload scope or reentrancy state");
                    foreignResult = result;
                }
                catch (Exception exception)
                {
                    foreignFailure = exception;
                }
            });

            foreign.Start();
            if (!foreign.Join(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("foreign thread did not terminate within bounded time");
            if (foreignFailure != null)
                throw new InvalidOperationException("foreign-thread assertion failed", foreignFailure);

            Assert(ReferenceEquals(foreignResult, vanilla),
                "foreign thread must preserve the exact vanilla result identity while owner reload scope is active");
            Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
                "foreign-thread execution must not consume or mutate the owner thread reload scope");

            object ownerResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
            Assert(ownerResult is FakeItem[] ownerItems && ownerItems.Length == 2,
                "owner thread must retain exact Magazine Belt fallback after foreign-thread no-op");
            Assert(ReferenceEquals(((FakeItem[])ownerResult)[0], vanillaMagazine)
                && ReferenceEquals(((FakeItem[])ownerResult)[1], beltMagazine),
                "owner-thread merge must remain vanilla-first and exact-Belt-only");
            Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
                "owner candidate enumeration must not consume its reload scope or leak reentrancy");
        }
        finally
        {
            ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        }

        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "owner finalizer boundary must restore the calling thread to a clean state");

        // Reload and QuickReload can nest through game-side dispatch or another cooperative patch.
        // The bridge therefore treats the ThreadStatic value as an owned depth, not a boolean latch:
        // an inner finalizer must not consume the outer scope, and Harmony's exception value must
        // be returned unchanged through every unwind.
        Exception sentinel = new InvalidOperationException("sentinel");
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        Assert((int)depth.GetValue(null)! == 2 && !(bool)reentrant.GetValue(null)!,
            "nested reload scopes must increment depth without entering candidate reentrancy");

        Exception inner = ReloadCandidateBridgeRuntime.ExitReloadScope(sentinel);
        Assert(ReferenceEquals(inner, sentinel),
            "inner reload finalizer must preserve the exact Harmony exception identity");
        Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
            "inner reload finalizer must leave the outer reload scope active");

        object nestedOwnerResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(nestedOwnerResult is FakeItem[] nestedItems && nestedItems.Length == 2,
            "outer reload scope must retain Magazine Belt fallback after nested inner unwind");
        Assert(ReferenceEquals(((FakeItem[])nestedOwnerResult)[0], vanillaMagazine)
            && ReferenceEquals(((FakeItem[])nestedOwnerResult)[1], beltMagazine),
            "nested-scope fallback must remain vanilla-first and exact-Belt-only");
        Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
            "nested candidate enumeration must not consume the surviving outer scope");

        Exception outer = ReloadCandidateBridgeRuntime.ExitReloadScope(sentinel);
        Assert(ReferenceEquals(outer, sentinel),
            "outer reload finalizer must preserve the exact Harmony exception identity");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "outer reload finalizer must restore a clean ThreadStatic state");

        // Defensive unmatched finalizer calls must remain fail-closed rather than underflowing
        // into a permanently-active reload scope.
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "unmatched reload finalizer must saturate at zero and keep the bridge inactive");
        Assert(ReferenceEquals(ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla), vanilla),
            "zero-depth state after unmatched finalizer must preserve exact vanilla result identity");

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
        if (!condition) throw new InvalidOperationException("Reload thread isolation regression failed: " + message);
    }
}

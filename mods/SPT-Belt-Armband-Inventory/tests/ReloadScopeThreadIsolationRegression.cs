using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using SPTBeltArmbandInventory;

internal static class ReloadScopeThreadIsolationRegression
{
    internal static void Run()
    {
        int[] slots = { 1, 2, 3 };
        var installedFastAccess = new[] { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var originalBindAvailable = new[] { 4, 5, 6 };
        var installedBindAvailable = new[] { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var beltSlotArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        var beltParent = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var beltMagazine = new FakeMagazine("belt-mag");
        var vanillaMagazine = new FakeMagazine("vanilla-mag");
        IEnumerable<FakeItem> vanilla = new FakeItem[] { vanillaMagazine };
        var inventory = new FakeInventory(new FakeItem[] { beltMagazine });

        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload thread isolation regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = beltSlotArgument;
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFastAccess;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBindAvailable;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBindAvailable;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item =>
            ReferenceEquals(item, beltMagazine) ? new FakeItem[] { beltParent } : Array.Empty<FakeItem>();
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

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
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;

        internal FakeInventory(FakeItem[] items)
        {
            this.items = items;
        }

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
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

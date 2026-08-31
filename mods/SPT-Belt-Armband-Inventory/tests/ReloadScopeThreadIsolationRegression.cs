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

        using var scopeEntered = new ManualResetEventSlim(false);
        using var foreignChecked = new ManualResetEventSlim(false);
        Exception ownerFailure = null;
        Exception foreignFailure = null;
        object foreignResult = null;

        Thread owner = new Thread(() =>
        {
            try
            {
                ReloadCandidateBridgeRuntime.EnterReloadScope();
                Assert((int)depth.GetValue(null)! == 1, "owner thread must observe its active reload scope");
                scopeEntered.Set();
                if (!foreignChecked.Wait(TimeSpan.FromSeconds(5)))
                    throw new InvalidOperationException("foreign thread did not complete bounded isolation check");

                object ownerResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
                Assert(ownerResult is FakeItem[] ownerItems && ownerItems.Length == 2,
                    "owner thread must retain exact Magazine Belt fallback while its scope is active");
                Assert(ReferenceEquals(((FakeItem[])ownerResult)[0], vanillaMagazine)
                    && ReferenceEquals(((FakeItem[])ownerResult)[1], beltMagazine),
                    "owner-thread merge must remain vanilla-first and exact-Belt-only");
            }
            catch (Exception exception)
            {
                ownerFailure = exception;
            }
            finally
            {
                ReloadCandidateBridgeRuntime.ExitReloadScope(null);
            }
        });

        Thread foreign = new Thread(() =>
        {
            try
            {
                if (!scopeEntered.Wait(TimeSpan.FromSeconds(5)))
                    throw new InvalidOperationException("owner thread did not enter reload scope in time");

                Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
                    "ThreadStatic reload state must start clean on a foreign thread");
                foreignResult = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
                Assert(ReferenceEquals(foreignResult, vanilla),
                    "another thread must return the exact vanilla result even while the owner thread is inside Reload/QuickReload");
                Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
                    "foreign-thread no-op must not acquire reload scope or reentrancy state");
            }
            catch (Exception exception)
            {
                foreignFailure = exception;
            }
            finally
            {
                foreignChecked.Set();
            }
        });

        owner.Start();
        foreign.Start();
        if (!owner.Join(TimeSpan.FromSeconds(10)) || !foreign.Join(TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException("Reload thread isolation regression failed: worker thread did not terminate within bounded time");
        if (ownerFailure != null)
            throw new InvalidOperationException("Reload thread isolation regression failed: owner-thread assertion failed", ownerFailure);
        if (foreignFailure != null)
            throw new InvalidOperationException("Reload thread isolation regression failed: foreign-thread assertion failed", foreignFailure);

        Assert(ReferenceEquals(foreignResult, vanilla),
            "foreign thread result must remain exact vanilla identity after both threads finish");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "calling test thread must retain a clean independent ThreadStatic baseline");

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
        if (!condition) throw new InvalidOperationException(message);
    }
}

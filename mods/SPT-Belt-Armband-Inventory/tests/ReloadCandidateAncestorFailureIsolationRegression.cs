using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateAncestorFailureIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", new FakeItem("foreign-root"));
        var beltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        var vanilla = new FakeItem[] { vanillaMagazine };
        var slots = new object();
        var inventory = new FakeInventory(new FakeItem[] { beltMagazine });
        int warnings = 0;

        Configure(slots, message => warnings++);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item =>
        {
            if (ReferenceEquals(item, beltMagazine))
                throw new InvalidOperationException("synthetic ancestry failure");
            return ((FakeItem)item).Parents;
        };

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object failed = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(ReferenceEquals(failed, vanilla),
            "an ancestry-reader exception must fail closed to the exact vanilla result object");
        Assert(inventory.Calls == 1,
            "ancestor failure may occur only after the one bounded pseudo-slot15 query");
        Assert(warnings == 1,
            "first ancestor failure should emit one bounded diagnostic");
        AssertScopeClean("ancestor failure");

        // A single candidate-level compatibility failure must not permanently poison
        // the bridge. Once the exact startup-bound reader is healthy again, the same
        // thread and same recognized slot reference may append the Belt fallback.
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object recovered = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(recovered is FakeItem[] recoveredItems && recoveredItems.Length == 2,
            "a later healthy call must recover and append the exact Belt candidate");
        Assert(ReferenceEquals(((FakeItem[])recovered)[0], vanillaMagazine)
            && ReferenceEquals(((FakeItem[])recovered)[1], beltMagazine),
            "recovered merge must preserve vanilla prefix and append the Belt candidate");
        Assert(inventory.Calls == 2,
            "recovery must perform exactly one additional bounded pseudo-slot15 query");
        Assert(warnings == 1,
            "failure diagnostic must remain one-shot after recovery");
        AssertScopeClean("recovery");

        ReloadCandidateBridgeRuntime.Reset();
    }

    static void Configure(object slots, Action<string> warning)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload ancestor-failure regression failed: fake GetItemsInSlots missing");
        // Match the pinned primary bridge boundary exactly so this regression reaches
        // the intended ancestry-reader fault after one bounded fallback query rather
        // than being correctly rejected earlier by the query-contract guard.
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object();
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = warning;
    }

    static void AssertScopeClean(string phase)
    {
        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload ancestor-failure regression failed: reloadDepth field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload ancestor-failure regression failed: reentrant field missing");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            phase + " must not leak reload scope or reentrancy state");
    }

    sealed class FakeInventory
    {
        readonly FakeItem[] items;
        internal int Calls { get; private set; }

        internal FakeInventory(FakeItem[] items)
        {
            this.items = items;
        }

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
        internal FakeMagazine(string templateId, params FakeItem[] parents)
            : base(templateId, parents)
        {
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload ancestor-failure regression failed: " + message);
    }
}

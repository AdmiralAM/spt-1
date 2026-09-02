using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateAtomicFailureRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var vanillaMagazine = new FakeMagazine("vanilla-magazine", new FakeItem("vanilla-container"));
        var exactBeltMagazine = new FakeMagazine("belt-magazine", exactBelt);
        var poisonMagazine = new FakeMagazine("poison-magazine", exactBelt);
        var vanilla = new FakeItem[] { vanillaMagazine };
        var recognizedSlots = new object[] { "fast-access" };
        var inventory = new FakeInventory(new FakeItem[] { exactBeltMagazine, poisonMagazine });

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Atomic reload failure regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = recognizedSlots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object[] { "installed-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object[] { "original-bind" };
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object[] { "installed-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.GetAllParentItems = item =>
        {
            var typed = (FakeItem)item;
            if (string.Equals(typed.TemplateId, "poison-magazine", StringComparison.Ordinal))
                throw new InvalidOperationException("late candidate inspection failure");
            return typed.Parents;
        };
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

        // Diagnostics are deliberately hostile too. A valid Belt candidate is seen
        // before the poison candidate, so this proves a late failure cannot leak a
        // partially allocated/merged result after vanilla candidates were copied.
        ReloadCandidateBridgeRuntime.LogWarning = _ => throw new InvalidOperationException("diagnostic sink failure");

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Atomic reload failure regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Atomic reload failure regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object failed = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanilla);
        Assert(ReferenceEquals(failed, vanilla),
            "late Belt candidate inspection failure must discard every partial append and return the exact vanilla result object");
        Assert((int)depth.GetValue(null)! == 1,
            "candidate failure must not consume the Reload/QuickReload scope owned by the Harmony finalizer");
        Assert(!(bool)reentrant.GetValue(null)!,
            "candidate failure must clear reentrancy state even when diagnostic logging also throws");
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        // failureLogged suppresses repeated diagnostics only; it must never become a
        // circuit breaker that disables valid later reloads for the rest of a raid.
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        inventory.Items = new FakeItem[] { exactBeltMagazine };
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object recovered = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(recovered is FakeItem[], "a later valid reload must remain bridgeable after an earlier scoped failure");
        var merged = (FakeItem[])recovered;
        Assert(merged.Length == 2
            && ReferenceEquals(merged[0], vanillaMagazine)
            && ReferenceEquals(merged[1], exactBeltMagazine),
            "post-failure recovery must preserve the vanilla prefix and append only the exact Magazine Belt candidate");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "successful recovery must leave no per-thread scope or reentrancy residue");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
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
        if (!condition) throw new InvalidOperationException("Atomic reload failure regression failed: " + message);
    }
}

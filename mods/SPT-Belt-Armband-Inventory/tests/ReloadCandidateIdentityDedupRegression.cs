using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateIdentityDedupRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var exactBelt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var vanillaRoot = new FakeItem("vanilla-root");
        var vanillaMagazine = new FakeMagazine("same-template", vanillaRoot);
        var beltMagazineA = new FakeMagazine("same-template", exactBelt);
        var beltMagazineB = new FakeMagazine("same-template", exactBelt);
        var recognizedSlots = new object();
        var vanilla = new FakeItem[] { vanillaMagazine };

        // The slot enumerator is allowed to surface the same runtime object more than
        // once. The bridge must de-duplicate that exact object by reference, while
        // preserving two physically distinct magazines even when their template IDs
        // are identical. Template-level de-duplication would silently discard a real
        // reload source and violate vanilla item-identity semantics.
        var inventory = new FakeInventory(new FakeItem[]
        {
            beltMagazineA,
            beltMagazineA,
            beltMagazineB
        });

        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload candidate identity regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new object();
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = recognizedSlots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload candidate identity regression failed closed unexpectedly: " + message);

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate identity regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate identity regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object resultObject = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(resultObject is FakeItem[], "bridge must preserve Item[] return shape");
        var result = (FakeItem[])resultObject;
        Assert(result.Length == 3,
            "one repeated Belt object must collapse to one candidate while a distinct same-template magazine remains distinct");
        Assert(ReferenceEquals(result[0], vanillaMagazine),
            "vanilla candidate remains the complete authoritative prefix");
        Assert(ReferenceEquals(result[1], beltMagazineA) && ReferenceEquals(result[2], beltMagazineB),
            "Belt fallback must preserve first-seen reference order and never template-dedupe distinct magazines");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "identity de-duplication path must leave no reload-scope/reentrancy residue");

        // If the exact same Belt object is already in the vanilla result, it is not
        // appended again and the bridge must retain exact vanilla object identity.
        var vanillaWithBelt = new FakeItem[] { vanillaMagazine, beltMagazineA };
        inventory.Items = new FakeItem[] { beltMagazineA, beltMagazineA };
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object noOp = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, recognizedSlots, vanillaWithBelt);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert(ReferenceEquals(noOp, vanillaWithBelt),
            "a Belt candidate already present by reference in vanilla must be an exact no-op, including result-object identity");
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "no-op identity de-duplication path must also unwind cleanly");

        ReloadCandidateBridgeRuntime.Reset();
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
        if (!condition) throw new InvalidOperationException("Reload candidate identity regression failed: " + message);
    }
}

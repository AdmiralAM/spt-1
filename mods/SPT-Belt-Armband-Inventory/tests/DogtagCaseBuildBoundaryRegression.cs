using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class DogtagCaseBuildBoundaryRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string[] vanilla = (string[])EquipmentBuildContainerPolicy.VanillaContainerSlots.Clone();
        string[] extended = EquipmentBuildContainerPolicy.Build(vanilla);

        Assert(extended.Length == 7,
            "wearable equipment-build validation must remain bounded to the four vanilla container locations plus Belt, ArmBand and HeadBand");
        Assert(!extended.Contains("Dogtag", StringComparer.Ordinal),
            "the vanilla Dogtag equipment slot must never be promoted into equipment-build container validation");
        Assert(!extended.Contains(RuntimeIdentity.DogtagCaseItemId, StringComparer.Ordinal),
            "Dogtag Case template identity must never be treated as an equipment-build container slot");
        Assert(extended.Contains(RuntimeIdentity.DedicatedBeltWireSlotId, StringComparer.Ordinal)
            && extended.Contains(BeltSlotPlan.ArmBand, StringComparer.Ordinal)
            && extended.Contains(RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparer.Ordinal),
            "negative Dogtag build isolation must not remove the three intended wearable build-container locations");

        // Non-canonical runtime arrays are compatibility-owned by EFT/other mods.
        // Build() must preserve their exact object rather than opportunistically
        // inserting Dogtag or any B&A&HB location into an unknown contract.
        string[] foreign = { BeltSlotPlan.TacticalVest, BeltSlotPlan.Pockets, BeltSlotPlan.Backpack, "foreign-container" };
        string[] untouched = EquipmentBuildContainerPolicy.Build(foreign);
        Assert(ReferenceEquals(foreign, untouched),
            "unknown equipment-build container arrays must remain exact vanilla/foreign-owned objects");

        // The case is intentionally a vanilla-slot container and therefore must
        // remain absent from the capability registry that drives build/death/
        // insurance/fast-access extensions for B&A&HB wearables.
        Assert(!WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.DogtagCaseItemId, out _),
            "Dogtag Case must remain outside the wearable capability registry");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Dogtag Case build boundary regression failed: " + message);
    }
}

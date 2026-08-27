using System;
using System.Linq;
using SPTBeltArmbandInventory;

internal static class Program
{
    static int assertions;
    static readonly string[] Vanilla =
    {
        BeltSlotPlan.TacticalVest,
        BeltSlotPlan.Pockets,
        BeltSlotPlan.Backpack,
        BeltSlotPlan.SecuredContainer,
        BeltSlotPlan.Dogtag
    };

    static void Main()
    {
        SPTBeltArmbandInventory.Tests.ProfileCleanupRegression.Run();
        SPTBeltArmbandInventory.Tests.DedicatedWearableSlotContractRegression.Run();
        Assert(BeltSlotPlan.IsExpectedContainerPanelOrder(Vanilla), "recognizes SPT 4.1 container order");
        Assert(!BeltSlotPlan.IsExpectedContainerPanelOrder(new[] { BeltSlotPlan.Pockets }), "rejects unrelated enum arrays");

        string[] above = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.AbovePockets, true);
        Assert(Array.IndexOf(above, BeltSlotPlan.ArmBand) + 1 == Array.IndexOf(above, BeltSlotPlan.Pockets), "legacy layout helper places ArmBand above pockets");
        Assert(above.Length == Vanilla.Length + 1, "legacy layout helper adds exactly one ArmBand row");

        string[] below = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.BelowPockets, true);
        Assert(Array.IndexOf(below, BeltSlotPlan.ArmBand) == Array.IndexOf(below, BeltSlotPlan.Pockets) + 1, "legacy layout helper places ArmBand below pockets");
        Assert(below.Length == Vanilla.Length + 1, "legacy below layout adds exactly one row");

        string[] duplicateInput = Vanilla.Concat(new[] { BeltSlotPlan.ArmBand, BeltSlotPlan.ArmBand }).ToArray();
        string[] normalized = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.BelowPockets, true);
        Assert(normalized.Count(x => x == BeltSlotPlan.ArmBand) == 1, "legacy layout helper is idempotent");

        string[] hidden = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.AbovePockets, false);
        Assert(!hidden.Contains(BeltSlotPlan.ArmBand), "legacy hidden layout removes duplicate ArmBand row");
        Assert(hidden.SequenceEqual(Vanilla), "legacy hidden layout preserves vanilla order");

        string[] dedicatedEquipmentOrder = { "TacticalVest", "Pockets", "Backpack", "Headwear" };
        string[] withBelt = InsertDedicated(dedicatedEquipmentOrder, DedicatedWearableSlotContract.Belt);
        Assert(Array.IndexOf(withBelt, DedicatedWearableSlotContract.BeltSlotId) == Array.IndexOf(withBelt, "Pockets") + 1,
            "dedicated Belt is anchored immediately after Pockets");
        Assert(Array.IndexOf(withBelt, DedicatedWearableSlotContract.BeltSlotId) + 1 == Array.IndexOf(withBelt, "Backpack"),
            "dedicated Belt remains between Pockets and Backpack");
        string[] withHeadBand = InsertDedicated(withBelt, DedicatedWearableSlotContract.HeadBand);
        Assert(Array.IndexOf(withHeadBand, DedicatedWearableSlotContract.HeadBandSlotId) + 1 == Array.IndexOf(withHeadBand, "Headwear"),
            "dedicated HeadBand is anchored immediately before Headwear");

        Assert(!BeltSlotPlan.ShouldExposeBelt(false, false), "empty slot never exposes legacy panel projection");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, false), "plain armband never exposes legacy panel projection");
        Assert(!BeltSlotPlan.ShouldExposeBelt(false, true), "container flag alone is insufficient");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, true), "Phase 1 native GridWindow path keeps legacy panel projection disabled");

        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.ArmBand), "ArmBand category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.Belt), "Belt category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.HeadBand), "HeadBand category is supported");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.HeadBand) == AccessoryCapacityBand.Micro, "HeadBand uses micro capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) == AccessoryCapacityBand.Compact, "ArmBand uses compact capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.Belt) == AccessoryCapacityBand.Expanded, "Belt uses expanded capacity band");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.ArmBand) == AccessoryHostState.Validated, "ArmBand is the only validated runtime host");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.Belt) == AccessoryHostState.ConceptOnly, "Belt remains concept-only until its dedicated runtime injection boundary is proven");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.HeadBand) == AccessoryHostState.ConceptOnly, "HeadBand remains concept-only until its dedicated runtime injection boundary is proven");
        Assert(!AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.Belt, false, true), "category alone cannot expose an empty host");
        Assert(AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.HeadBand, true, true), "container-capable HeadBand may expose a row conceptually");
        Assert(AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.ArmBand, true, true), "validated ArmBand container can activate its runtime route");
        Assert(!AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.Belt, true, true), "concept-only Belt cannot activate before dedicated slot injection is proven");
        Assert(!AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.HeadBand, true, true), "concept-only HeadBand cannot activate before dedicated slot injection is proven");
        Assert(!AccessoryCategoryPolicy.CanExposeContainer((AccessoryCategory)99, true, true), "unknown category fails closed");
        Assert(!AccessoryCategoryPolicy.CanActivateRuntime((AccessoryCategory)99, true, true), "unknown category cannot activate runtime behavior");

        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.PanelProjection), "native GridWindow Phase 1 does not own legacy panel projection");
        Assert(AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess), "magazine belt retains reachable-container fast access");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.PaymentSource), "magazine-only RC does not install payment-source behavior");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.GrenadeAccess), "magazine-only RC does not install grenade behavior");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.Belt, AccessoryCapability.PanelProjection), "concept-only Belt has no runtime capabilities");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.HeadBand, AccessoryCapability.FastAccess), "concept-only HeadBand has no runtime capabilities");
        Assert(!AccessoryCapabilityPolicy.Has((AccessoryCategory)99, AccessoryCapability.PanelProjection), "unknown category has no capabilities");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.None), "empty capability request fails closed");
        Assert(!AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.GrenadeAccess, true, true), "disabled grenade capability cannot activate on the magazine RC");
        Assert(AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess, true, true), "assigned fast-access capability activates only for a real container");
        Assert(!AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess, true, false), "fast-access capability still requires a container item");

        Console.WriteLine("SPT Belt/Armband Inventory profile safety and dedicated slot contract regressions passed.");
    }

    static string[] InsertDedicated(string[] source, DedicatedWearableSlotDescriptor descriptor)
    {
        int anchor = Array.IndexOf(source, descriptor.UiAnchor);
        if (anchor < 0) throw new InvalidOperationException("Missing UI anchor " + descriptor.UiAnchor);
        int insertAt = descriptor.InsertAfterAnchor ? anchor + 1 : anchor;
        var result = new string[source.Length + 1];
        Array.Copy(source, 0, result, 0, insertAt);
        result[insertAt] = descriptor.SlotId;
        Array.Copy(source, insertAt, result, insertAt + 1, source.Length - insertAt);
        return result;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        assertions++;
    }
}

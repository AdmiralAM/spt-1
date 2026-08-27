using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class WearableDescriptorRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        WearableDescriptor armBand = WearableDescriptorRegistry.Get(AccessoryCategory.ArmBand);
        if (armBand.HostSlot != BeltSlotPlan.ArmBand)
            throw new InvalidOperationException("Validated ArmBand descriptor must retain the proven EFT ArmBand host slot.");
        if (armBand.HostState != AccessoryHostState.Validated)
            throw new InvalidOperationException("ArmBand descriptor must remain validated.");
        if (armBand.Capacity != AccessoryCapacityBand.Compact)
            throw new InvalidOperationException("ArmBand descriptor capacity drifted.");
        if (!armBand.Capabilities.HasFlag(AccessoryCapability.FastAccess)
            || !armBand.Capabilities.HasFlag(AccessoryCapability.PickupFallback)
            || !armBand.Capabilities.HasFlag(AccessoryCapability.DeathRetention))
            throw new InvalidOperationException("ArmBand descriptor lost proven lifecycle capabilities.");
        if (armBand.Capabilities.HasFlag(AccessoryCapability.PaymentSource)
            || armBand.Capabilities.HasFlag(AccessoryCapability.GrenadeAccess)
            || armBand.Capabilities.HasFlag(AccessoryCapability.PanelProjection))
            throw new InvalidOperationException("Magazine ArmBand descriptor activated unsupported capabilities.");

        WearableDescriptor belt = WearableDescriptorRegistry.Get(AccessoryCategory.Belt);
        WearableDescriptor headBand = WearableDescriptorRegistry.Get(AccessoryCategory.HeadBand);
        if (!string.Equals(belt.HostSlot, DedicatedWearableSlotContract.BeltSlotId, StringComparison.Ordinal)
            || !string.Equals(headBand.HostSlot, DedicatedWearableSlotContract.HeadBandSlotId, StringComparison.Ordinal))
            throw new InvalidOperationException("Belt/HeadBand descriptors must retain their fixed dedicated product slot identities.");
        if (belt.HostState != AccessoryHostState.ConceptOnly || headBand.HostState != AccessoryHostState.ConceptOnly)
            throw new InvalidOperationException("Dedicated identities may exist before physical runtime proof; host state must remain fail-closed until the full client slot boundary is activated.");
        if (belt.Capabilities != AccessoryCapability.None || headBand.Capabilities != AccessoryCapability.None)
            throw new InvalidOperationException("Unproven dedicated host descriptors must expose no policy capabilities yet.");

        if (AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) != armBand.Capacity
            || AccessoryCapabilityPolicy.Capabilities(AccessoryCategory.ArmBand) != armBand.Capabilities)
            throw new InvalidOperationException("Category/capability policy must remain a pure facade over the descriptor registry.");
    }
}

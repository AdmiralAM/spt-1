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
            throw new InvalidOperationException("ArmBand descriptor must remain the only validated wearable host after descriptor extraction.");
        if (armBand.Capacity != AccessoryCapacityBand.Compact)
            throw new InvalidOperationException("ArmBand descriptor capacity changed during no-behavior-change extraction.");
        if (!armBand.Capabilities.HasFlag(AccessoryCapability.FastAccess)
            || !armBand.Capabilities.HasFlag(AccessoryCapability.PickupFallback)
            || !armBand.Capabilities.HasFlag(AccessoryCapability.DeathRetention))
            throw new InvalidOperationException("ArmBand descriptor lost proven Phase 1 lifecycle capabilities.");
        if (armBand.Capabilities.HasFlag(AccessoryCapability.PaymentSource)
            || armBand.Capabilities.HasFlag(AccessoryCapability.GrenadeAccess)
            || armBand.Capabilities.HasFlag(AccessoryCapability.PanelProjection))
            throw new InvalidOperationException("Descriptor extraction must not activate dormant Phase 2 capabilities on the magazine RC.");

        WearableDescriptor belt = WearableDescriptorRegistry.Get(AccessoryCategory.Belt);
        WearableDescriptor headBand = WearableDescriptorRegistry.Get(AccessoryCategory.HeadBand);
        if (belt.HostSlot != null || headBand.HostSlot != null)
            throw new InvalidOperationException("Concept-only Belt/HeadBand descriptors must not invent EFT equipment slots.");
        if (belt.HostState != AccessoryHostState.ConceptOnly || headBand.HostState != AccessoryHostState.ConceptOnly)
            throw new InvalidOperationException("Belt/HeadBand must remain concept-only until their host boundaries are physically proven.");
        if (belt.Capabilities != AccessoryCapability.None || headBand.Capabilities != AccessoryCapability.None)
            throw new InvalidOperationException("Concept-only descriptors must expose no runtime capability surface.");

        if (AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) != armBand.Capacity
            || AccessoryCapabilityPolicy.Capabilities(AccessoryCategory.ArmBand) != armBand.Capabilities)
            throw new InvalidOperationException("Legacy category/capability policy must be a pure facade over the descriptor registry.");
    }
}

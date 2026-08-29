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
        if (belt.HostState != AccessoryHostState.Validated)
            throw new InvalidOperationException("Dedicated Belt host must reflect the physically proven slot/filter runtime boundary.");
        if (headBand.HostState != AccessoryHostState.RuntimeCandidate)
            throw new InvalidOperationException("HeadBand host must remain a runtime candidate until the single physical Phase 1 gate passes.");
        if (belt.Capabilities != AccessoryCapability.None || headBand.Capabilities != AccessoryCapability.None)
            throw new InvalidOperationException("Dedicated host placement must not implicitly grant unrelated capability policies.");
        if (!AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.Belt, true, true))
            throw new InvalidOperationException("Physically validated Belt host must be eligible for its dedicated runtime path.");
        if (AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.HeadBand, true, true))
            throw new InvalidOperationException("HeadBand candidate must not be promoted to validated before physical acceptance.");

        if (AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) != armBand.Capacity
            || AccessoryCapabilityPolicy.Capabilities(AccessoryCategory.ArmBand) != armBand.Capabilities)
            throw new InvalidOperationException("Category/capability policy must remain a pure facade over the descriptor registry.");
    }
}

using System;

namespace SPTBeltArmbandInventory
{
    [Flags]
    internal enum AccessoryCapability
    {
        None = 0,
        PanelProjection = 1 << 0,
        LootPriority = 1 << 1,
        UnloadPriority = 1 << 2,
        GrenadeAccess = 1 << 3,
        FastAccess = 1 << 4,
        PickupFallback = 1 << 5,
        PaymentSource = 1 << 6,
        BuildValidation = 1 << 7,
        DeathRetention = 1 << 8,
        ScavHostRestoration = 1 << 9
    }

    internal static class AccessoryCapabilityPolicy
    {
        internal static AccessoryCapability Capabilities(AccessoryCategory category)
        {
            return WearableDescriptorRegistry.TryGet(category, out WearableDescriptor descriptor)
                ? descriptor.Capabilities
                : AccessoryCapability.None;
        }

        internal static bool Has(AccessoryCategory category, AccessoryCapability capability)
        {
            if (capability == AccessoryCapability.None) return false;
            AccessoryCapability available = Capabilities(category);
            return (available & capability) == capability;
        }

        internal static bool CanUse(
            AccessoryCategory category,
            AccessoryCapability capability,
            bool hasItem,
            bool isContainer)
        {
            return Has(category, capability)
                && AccessoryCategoryPolicy.CanActivateRuntime(category, hasItem, isContainer);
        }
    }
}

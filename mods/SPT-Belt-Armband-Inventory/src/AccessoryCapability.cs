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

    // Phase 1 is deliberately a magazine-only ArmBand container. Keep only the
    // integrations that are useful for that exact item. Grenade/payment/panel
    // projection capabilities stay defined for later variants but are not active
    // until a concrete wearable actually needs them.
    internal static class AccessoryCapabilityPolicy
    {
        const AccessoryCapability ValidatedArmBandCapabilities =
            AccessoryCapability.LootPriority |
            AccessoryCapability.UnloadPriority |
            AccessoryCapability.FastAccess |
            AccessoryCapability.PickupFallback |
            AccessoryCapability.BuildValidation |
            AccessoryCapability.DeathRetention |
            AccessoryCapability.ScavHostRestoration;

        internal static AccessoryCapability Capabilities(AccessoryCategory category)
        {
            if (!AccessoryCategoryPolicy.IsSupported(category)) return AccessoryCapability.None;
            return category == AccessoryCategory.ArmBand
                ? ValidatedArmBandCapabilities
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

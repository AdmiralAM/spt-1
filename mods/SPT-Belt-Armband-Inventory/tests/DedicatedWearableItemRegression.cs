using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class DedicatedWearableItemRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.DedicatedMagazineBeltItemId, out var belt))
            throw new InvalidOperationException("Dedicated Magazine Belt descriptor missing.");
        if (belt.Category != AccessoryCategory.Belt
            || belt.GridColumns != RuntimeIdentity.DedicatedMagazineBeltGridColumns
            || belt.GridRows != RuntimeIdentity.DedicatedMagazineBeltGridRows)
            throw new InvalidOperationException("Dedicated Magazine Belt geometry/category contract drifted.");
        if (!belt.Has(AccessoryCapability.FastAccess)
            || !belt.Has(AccessoryCapability.LootPriority)
            || !belt.Has(AccessoryCapability.UnloadPriority)
            || !belt.Has(AccessoryCapability.BuildValidation))
            throw new InvalidOperationException("Dedicated Magazine Belt lost required tactical/container capabilities.");
        if (belt.Has(AccessoryCapability.PaymentSource) || belt.Has(AccessoryCapability.GrenadeAccess))
            throw new InvalidOperationException("Dedicated Magazine Belt activated unrelated payment/grenade capabilities.");

        if (!WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.EmergencyHeadBandItemId, out var headBand))
            throw new InvalidOperationException("Emergency HeadBand descriptor missing.");
        if (headBand.Category != AccessoryCategory.HeadBand
            || headBand.GridColumns != RuntimeIdentity.EmergencyHeadBandGridColumns
            || headBand.GridRows != RuntimeIdentity.EmergencyHeadBandGridRows)
            throw new InvalidOperationException("Emergency HeadBand geometry/category contract drifted.");
        if (!headBand.Has(AccessoryCapability.BuildValidation))
            throw new InvalidOperationException("Emergency HeadBand lost build-validation capability.");
        if (headBand.Has(AccessoryCapability.FastAccess)
            || headBand.Has(AccessoryCapability.PaymentSource)
            || headBand.Has(AccessoryCapability.GrenadeAccess))
            throw new InvalidOperationException("Emergency HeadBand activated unrelated tactical/payment/grenade capabilities.");
    }
}

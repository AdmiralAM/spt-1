using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class WearableConceptCatalogRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        WearableConceptDescriptor magazine = WearableConceptCatalog.Get(WearableConceptId.BeltMagazine);
        if (magazine.Category != AccessoryCategory.Belt
            || magazine.Capacity != AccessoryCapacityBand.Expanded
            || !magazine.Allows(WearableFilterDomain.Magazine)
            || magazine.Allows(WearableFilterDomain.Grenade))
            throw new InvalidOperationException("Magazine Belt concept must stay expanded, magazine-only tactical storage.");

        WearableConceptDescriptor grenadier = WearableConceptCatalog.Get(WearableConceptId.BeltGrenadier);
        if (!grenadier.Allows(WearableFilterDomain.Grenade)
            || (grenadier.IntendedCapabilities & AccessoryCapability.GrenadeAccess) == 0)
            throw new InvalidOperationException("Grenadier Belt concept must retain grenade-only access semantics.");

        WearableConceptDescriptor emergency = WearableConceptCatalog.Get(WearableConceptId.HeadBandEmergency);
        if (emergency.Category != AccessoryCategory.HeadBand
            || emergency.Capacity != AccessoryCapacityBand.Micro
            || !emergency.Allows(WearableFilterDomain.Medical)
            || !emergency.Allows(WearableFilterDomain.Injector))
            throw new InvalidOperationException("Emergency HeadBand concept must remain micro medical/injector utility.");

        WearableConceptDescriptor smuggler = WearableConceptCatalog.Get(WearableConceptId.HeadBandSmuggler);
        if (!smuggler.Allows(WearableFilterDomain.Money)
            || !smuggler.Allows(WearableFilterDomain.Card)
            || (smuggler.IntendedCapabilities & AccessoryCapability.PaymentSource) == 0)
            throw new InvalidOperationException("Smuggler HeadBand concept must retain personal-value/payment semantics.");

        foreach (WearableConceptId id in Enum.GetValues<WearableConceptId>())
            if (WearableConceptCatalog.CanActivateRuntime(id))
                throw new InvalidOperationException(id + " must remain ConceptOnly until its exact EFT host boundary is proven.");
    }
}

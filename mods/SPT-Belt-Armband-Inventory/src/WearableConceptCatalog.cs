using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    [Flags]
    internal enum WearableFilterDomain
    {
        None = 0,
        Magazine = 1 << 0,
        Medical = 1 << 1,
        Injector = 1 << 2,
        LooseAmmo = 1 << 3,
        AmmoBox = 1 << 4,
        Grenade = 1 << 5,
        Money = 1 << 6,
        Card = 1 << 7,
        Document = 1 << 8,
        SmallUtility = 1 << 9,
        PersonalValue = 1 << 10
    }

    internal enum WearableConceptId
    {
        BeltMagazine,
        BeltMedic,
        BeltAmmo,
        BeltGrenadier,
        BeltUtility,
        HeadBandEmergency,
        HeadBandSmuggler,
        HeadBandCombat
    }

    internal sealed class WearableConceptDescriptor
    {
        internal WearableConceptId Id { get; }
        internal AccessoryCategory Category { get; }
        internal AccessoryCapacityBand Capacity { get; }
        internal WearableFilterDomain FilterDomains { get; }
        internal AccessoryCapability IntendedCapabilities { get; }
        internal bool RuntimeEnabled { get; }

        internal WearableConceptDescriptor(
            WearableConceptId id,
            AccessoryCategory category,
            AccessoryCapacityBand capacity,
            WearableFilterDomain filterDomains,
            AccessoryCapability intendedCapabilities,
            bool runtimeEnabled = false)
        {
            if (filterDomains == WearableFilterDomain.None) throw new ArgumentOutOfRangeException(nameof(filterDomains));
            Id = id;
            Category = category;
            Capacity = capacity;
            FilterDomains = filterDomains;
            IntendedCapabilities = intendedCapabilities;
            RuntimeEnabled = runtimeEnabled;
        }

        internal bool Allows(WearableFilterDomain domain)
        {
            return domain != WearableFilterDomain.None && (FilterDomains & domain) == domain;
        }
    }

    internal static class WearableConceptCatalog
    {
        static readonly IReadOnlyDictionary<WearableConceptId, WearableConceptDescriptor> Concepts =
            new Dictionary<WearableConceptId, WearableConceptDescriptor>
            {
                [WearableConceptId.BeltMagazine] = new WearableConceptDescriptor(
                    WearableConceptId.BeltMagazine,
                    AccessoryCategory.Belt,
                    AccessoryCapacityBand.Expanded,
                    WearableFilterDomain.Magazine,
                    AccessoryCapability.LootPriority |
                    AccessoryCapability.UnloadPriority |
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.BeltMedic] = new WearableConceptDescriptor(
                    WearableConceptId.BeltMedic,
                    AccessoryCategory.Belt,
                    AccessoryCapacityBand.Expanded,
                    WearableFilterDomain.Medical | WearableFilterDomain.Injector,
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.BeltAmmo] = new WearableConceptDescriptor(
                    WearableConceptId.BeltAmmo,
                    AccessoryCategory.Belt,
                    AccessoryCapacityBand.Expanded,
                    WearableFilterDomain.LooseAmmo | WearableFilterDomain.AmmoBox,
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.BeltGrenadier] = new WearableConceptDescriptor(
                    WearableConceptId.BeltGrenadier,
                    AccessoryCategory.Belt,
                    AccessoryCapacityBand.Expanded,
                    WearableFilterDomain.Grenade,
                    AccessoryCapability.GrenadeAccess |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.BeltUtility] = new WearableConceptDescriptor(
                    WearableConceptId.BeltUtility,
                    AccessoryCategory.Belt,
                    AccessoryCapacityBand.Expanded,
                    WearableFilterDomain.SmallUtility,
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.HeadBandEmergency] = new WearableConceptDescriptor(
                    WearableConceptId.HeadBandEmergency,
                    AccessoryCategory.HeadBand,
                    AccessoryCapacityBand.Micro,
                    WearableFilterDomain.Medical | WearableFilterDomain.Injector,
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.HeadBandSmuggler] = new WearableConceptDescriptor(
                    WearableConceptId.HeadBandSmuggler,
                    AccessoryCategory.HeadBand,
                    AccessoryCapacityBand.Micro,
                    WearableFilterDomain.Money | WearableFilterDomain.Card | WearableFilterDomain.Document,
                    AccessoryCapability.PaymentSource |
                    AccessoryCapability.BuildValidation),

                [WearableConceptId.HeadBandCombat] = new WearableConceptDescriptor(
                    WearableConceptId.HeadBandCombat,
                    AccessoryCategory.HeadBand,
                    AccessoryCapacityBand.Micro,
                    WearableFilterDomain.Medical | WearableFilterDomain.Injector | WearableFilterDomain.PersonalValue,
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation)
            };

        internal static WearableConceptDescriptor Get(WearableConceptId id)
        {
            if (!Concepts.TryGetValue(id, out WearableConceptDescriptor descriptor))
                throw new ArgumentOutOfRangeException(nameof(id), id, null);
            return descriptor;
        }

        internal static bool CanActivateRuntime(WearableConceptId id)
        {
            WearableConceptDescriptor descriptor = Get(id);
            return descriptor.RuntimeEnabled
                && AccessoryCategoryPolicy.HostState(descriptor.Category) == AccessoryHostState.Validated;
        }
    }
}

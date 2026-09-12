using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal sealed class WearableItemDescriptor
    {
        internal string TemplateId { get; }
        internal AccessoryCategory Category { get; }
        internal int GridColumns { get; }
        internal int GridRows { get; }
        internal AccessoryCapability Capabilities { get; }

        internal WearableItemDescriptor(
            string templateId,
            AccessoryCategory category,
            int gridColumns,
            int gridRows,
            AccessoryCapability capabilities)
        {
            if (string.IsNullOrEmpty(templateId)) throw new ArgumentNullException(nameof(templateId));
            if (!AccessoryGridPolicy.IsValid(gridColumns, gridRows)) throw new ArgumentOutOfRangeException(nameof(gridColumns));
            TemplateId = templateId;
            Category = category;
            GridColumns = gridColumns;
            GridRows = gridRows;
            Capabilities = capabilities;
        }

        internal bool Has(AccessoryCapability capability)
        {
            return capability != AccessoryCapability.None && (Capabilities & capability) == capability;
        }
    }

    internal static class WearableItemDescriptorRegistry
    {
        static readonly IReadOnlyDictionary<string, WearableItemDescriptor> ByTemplate = Build();

        static IReadOnlyDictionary<string, WearableItemDescriptor> Build()
        {
            var descriptors = new Dictionary<string, WearableItemDescriptor>(StringComparer.Ordinal)
            {
                [RuntimeIdentity.CandidateItemId] = new WearableItemDescriptor(
                    RuntimeIdentity.CandidateItemId,
                    AccessoryCategory.ArmBand,
                    RuntimeIdentity.CandidateGridColumns,
                    RuntimeIdentity.CandidateGridRows,
                    WearableDescriptorRegistry.Get(AccessoryCategory.ArmBand).Capabilities),

                [RuntimeIdentity.WristWalletItemId] = new WearableItemDescriptor(
                    RuntimeIdentity.WristWalletItemId,
                    AccessoryCategory.ArmBand,
                    RuntimeIdentity.WristWalletGridColumns,
                    RuntimeIdentity.WristWalletGridRows,
                    AccessoryCapability.PaymentSource |
                    AccessoryCapability.BuildValidation |
                    AccessoryCapability.DeathRetention),

                // Dedicated Belt is the tactical fast-access family. Protected-by-default
                // is a product policy; a later F12 user toggle will be allowed to switch
                // the category to Lost on death without changing its persistent identity.
                [RuntimeIdentity.DedicatedMagazineBeltItemId] = new WearableItemDescriptor(
                    RuntimeIdentity.DedicatedMagazineBeltItemId,
                    AccessoryCategory.Belt,
                    RuntimeIdentity.DedicatedMagazineBeltGridColumns,
                    RuntimeIdentity.DedicatedMagazineBeltGridRows,
                    AccessoryCapability.LootPriority |
                    AccessoryCapability.UnloadPriority |
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.BuildValidation |
                    AccessoryCapability.ScavHostRestoration |
                    AccessoryCapability.DeathRetention),

                // HeadBand is protected personal utility storage. It deliberately does not
                // inherit tactical fast-access, payment-source or grenade semantics.
                [RuntimeIdentity.EmergencyHeadBandItemId] = new WearableItemDescriptor(
                    RuntimeIdentity.EmergencyHeadBandItemId,
                    AccessoryCategory.HeadBand,
                    RuntimeIdentity.EmergencyHeadBandGridColumns,
                    RuntimeIdentity.EmergencyHeadBandGridRows,
                    AccessoryCapability.BuildValidation |
                    AccessoryCapability.ScavHostRestoration |
                    AccessoryCapability.DeathRetention)
            };

            foreach (ArmBandVariantDescriptor variant in ArmBandVariantCatalog.All)
            {
                AccessoryCapability capabilities = AccessoryCapability.BuildValidation;
                if (variant.Role == ArmBandRole.Medical || variant.Role == ArmBandRole.Ammo)
                    capabilities |= AccessoryCapability.LootPriority;
                else if (variant.Role == ArmBandRole.Magazine)
                    capabilities |= AccessoryCapability.LootPriority | AccessoryCapability.UnloadPriority | AccessoryCapability.FastAccess;
                else if (variant.Role == ArmBandRole.Currency)
                    capabilities |= AccessoryCapability.PaymentSource;

                descriptors.Add(variant.TemplateId, new WearableItemDescriptor(
                    variant.TemplateId,
                    AccessoryCategory.ArmBand,
                    1,
                    2,
                    capabilities));
            }

            return descriptors;
        }

        internal static bool TryGet(string templateId, out WearableItemDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                descriptor = null;
                return false;
            }
            return ByTemplate.TryGetValue(templateId, out descriptor);
        }

        internal static bool IsRegistered(string templateId)
        {
            return TryGet(templateId, out _);
        }

        internal static bool HasCapability(string templateId, AccessoryCapability capability)
        {
            return TryGet(templateId, out WearableItemDescriptor descriptor) && descriptor.Has(capability);
        }
    }
}

using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal sealed class WearableDescriptor
    {
        internal AccessoryCategory Category { get; }
        internal string HostSlot { get; }
        internal AccessoryCapacityBand Capacity { get; }
        internal AccessoryHostState HostState { get; }
        internal AccessoryCapability Capabilities { get; }

        internal WearableDescriptor(
            AccessoryCategory category,
            string hostSlot,
            AccessoryCapacityBand capacity,
            AccessoryHostState hostState,
            AccessoryCapability capabilities)
        {
            Category = category;
            HostSlot = hostSlot;
            Capacity = capacity;
            HostState = hostState;
            Capabilities = capabilities;
        }
    }

    internal static class WearableDescriptorRegistry
    {
        static readonly IReadOnlyDictionary<AccessoryCategory, WearableDescriptor> Descriptors =
            new Dictionary<AccessoryCategory, WearableDescriptor>
            {
                [AccessoryCategory.ArmBand] = new WearableDescriptor(
                    AccessoryCategory.ArmBand,
                    BeltSlotPlan.ArmBand,
                    AccessoryCapacityBand.Compact,
                    AccessoryHostState.Validated,
                    AccessoryCapability.LootPriority |
                    AccessoryCapability.UnloadPriority |
                    AccessoryCapability.FastAccess |
                    AccessoryCapability.PickupFallback |
                    AccessoryCapability.BuildValidation |
                    AccessoryCapability.DeathRetention |
                    AccessoryCapability.ScavHostRestoration),

                // Belt and HeadBand are intentionally descriptor-only until their EFT
                // host boundaries are proven. Keeping them in the registry lets policy
                // become data-driven without activating invented runtime slots.
                [AccessoryCategory.Belt] = new WearableDescriptor(
                    AccessoryCategory.Belt,
                    null,
                    AccessoryCapacityBand.Expanded,
                    AccessoryHostState.ConceptOnly,
                    AccessoryCapability.None),

                [AccessoryCategory.HeadBand] = new WearableDescriptor(
                    AccessoryCategory.HeadBand,
                    null,
                    AccessoryCapacityBand.Micro,
                    AccessoryHostState.ConceptOnly,
                    AccessoryCapability.None)
            };

        internal static bool TryGet(AccessoryCategory category, out WearableDescriptor descriptor)
        {
            return Descriptors.TryGetValue(category, out descriptor);
        }

        internal static WearableDescriptor Get(AccessoryCategory category)
        {
            if (!TryGet(category, out WearableDescriptor descriptor))
                throw new ArgumentOutOfRangeException(nameof(category), category, null);
            return descriptor;
        }
    }
}

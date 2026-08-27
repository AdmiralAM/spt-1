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

                // Physical runtime validation has proven the dedicated Belt location
                // and its exact-item filter. The HeadBand location has a complete
                // runtime candidate but remains unvalidated until the single package
                // runtime gate proves its visible/native binding and interaction.
                [AccessoryCategory.Belt] = new WearableDescriptor(
                    AccessoryCategory.Belt,
                    DedicatedWearableSlotContract.BeltSlotId,
                    AccessoryCapacityBand.Expanded,
                    AccessoryHostState.Validated,
                    AccessoryCapability.None),

                [AccessoryCategory.HeadBand] = new WearableDescriptor(
                    AccessoryCategory.HeadBand,
                    DedicatedWearableSlotContract.HeadBandSlotId,
                    AccessoryCapacityBand.Micro,
                    AccessoryHostState.RuntimeCandidate,
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

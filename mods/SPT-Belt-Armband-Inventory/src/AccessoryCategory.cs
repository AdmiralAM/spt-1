using System;

namespace SPTBeltArmbandInventory
{
    internal enum AccessoryCategory
    {
        ArmBand,
        Belt,
        HeadBand
    }

    internal enum AccessoryCapacityBand
    {
        Micro,
        Compact,
        Expanded
    }

    internal enum AccessoryHostState
    {
        ConceptOnly,
        RuntimeCandidate,
        Validated
    }

    internal static class AccessoryCategoryPolicy
    {
        internal static AccessoryCapacityBand Capacity(AccessoryCategory category)
        {
            return WearableDescriptorRegistry.Get(category).Capacity;
        }

        internal static bool CanExposeContainer(AccessoryCategory category, bool hasItem, bool isContainer)
        {
            return IsSupported(category) && hasItem && isContainer;
        }

        internal static AccessoryHostState HostState(AccessoryCategory category)
        {
            return WearableDescriptorRegistry.Get(category).HostState;
        }

        internal static bool CanActivateRuntime(AccessoryCategory category, bool hasItem, bool isContainer)
        {
            return IsSupported(category)
                && HostState(category) == AccessoryHostState.Validated
                && CanExposeContainer(category, hasItem, isContainer);
        }

        internal static bool IsSupported(AccessoryCategory category)
        {
            return WearableDescriptorRegistry.TryGet(category, out _);
        }

        internal static string DisplayName(AccessoryCategory category)
        {
            switch (category)
            {
                case AccessoryCategory.ArmBand:
                    return "ArmBand";
                case AccessoryCategory.Belt:
                    return "Belt";
                case AccessoryCategory.HeadBand:
                    return "HeadBand";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }
    }
}

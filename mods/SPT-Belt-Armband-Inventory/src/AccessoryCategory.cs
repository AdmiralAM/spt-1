using System;

namespace SPTBeltArmbandInventory
{
    // Logical categories are intentionally separate from EFT equipment-slot names.
    // A category may only acquire a host slot after that slot is proven on the
    // target client; this prevents HeadBand/Belt design from inventing enum values.
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

    internal static class AccessoryCategoryPolicy
    {
        internal static AccessoryCapacityBand Capacity(AccessoryCategory category)
        {
            switch (category)
            {
                case AccessoryCategory.HeadBand:
                    return AccessoryCapacityBand.Micro;
                case AccessoryCategory.Belt:
                    return AccessoryCapacityBand.Expanded;
                default:
                    return AccessoryCapacityBand.Compact;
            }
        }

        internal static bool CanExposeContainer(AccessoryCategory category, bool hasItem, bool isContainer)
        {
            // Category does not override the runtime item contract. A cosmetic or
            // empty host must never become a container row by category alone.
            return hasItem && isContainer;
        }

        internal static bool IsSupported(AccessoryCategory category)
        {
            return category == AccessoryCategory.ArmBand
                || category == AccessoryCategory.Belt
                || category == AccessoryCategory.HeadBand;
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

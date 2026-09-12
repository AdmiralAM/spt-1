using System;

namespace SPTBeltArmbandInventory
{
    internal static class EquipmentCacheCapacityPolicy
    {
        internal static int RequiredCapacity(int slotCount, int maximumPublishedSlot)
        {
            if (slotCount < 0 || maximumPublishedSlot < 0)
                throw new ArgumentOutOfRangeException();
            return Math.Max(slotCount, maximumPublishedSlot + 1);
        }
    }
}

using System;

namespace SPTStackableArmorPlates;

public static class PlateStackPolicy
{
    public const string ArmorPlateParentId = "644120aa86ffbe10ee032b6f";

    public static bool IsStandalonePlate(string parentId, int width, int height, double weight)
        => parentId == ArmorPlateParentId && width > 0 && height > 0 && weight > 0;

    public static bool CanDonateDurability(double current, double maximum)
        => maximum > 0 && current > 0 && current < maximum;

    public static double RepairedDurability(double survivorCurrent, double survivorMaximum, double donorCurrent)
        => Math.Min(survivorMaximum, survivorCurrent + donorCurrent);

    public static bool SharesArmorSlot(int firstColliderMask, int secondColliderMask)
        => (firstColliderMask & secondColliderMask) != 0;
}

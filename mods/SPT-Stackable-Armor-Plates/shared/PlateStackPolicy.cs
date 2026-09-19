using System;

namespace SPTStackableArmorPlates;

public static class PlateStackPolicy
{
    public const string ArmorPlateParentId = "644120aa86ffbe10ee032b6f";
    public const int StackSize = 4;

    public static bool IsStandalonePlate(string parentId, int width, int height, double weight)
        => parentId == ArmorPlateParentId && width > 0 && height > 0 && weight > 0;

    public static int DesiredStackSize(int current) => Math.Max(current, StackSize);

    public static bool DurabilityMatches(float currentA, float maximumA, float currentB, float maximumB)
        => Math.Abs(currentA - currentB) < 0.001f && Math.Abs(maximumA - maximumB) < 0.001f;
}

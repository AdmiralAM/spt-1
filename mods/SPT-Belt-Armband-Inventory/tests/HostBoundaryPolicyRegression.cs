using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class HostBoundaryPolicyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string[] vanillaLike = { "Headwear", "Earpiece", "Eyewear", "FaceCover", "ArmBand", "TacticalVest" };
        if (HostBoundaryPolicy.FindExactHost(AccessoryCategory.Belt, vanillaLike) != null)
            throw new InvalidOperationException("Belt must not hijack unrelated vanilla equipment slots.");
        if (HostBoundaryPolicy.FindExactHost(AccessoryCategory.HeadBand, vanillaLike) != null)
            throw new InvalidOperationException("HeadBand must not hijack Headwear/FaceCover/Earpiece slots.");

        if (!string.Equals(HostBoundaryPolicy.FindExactHost(AccessoryCategory.Belt, new[] { "Belt" }), "Belt", StringComparison.Ordinal))
            throw new InvalidOperationException("An explicit Belt enum member must be discoverable as evidence.");
        if (!string.Equals(HostBoundaryPolicy.FindExactHost(AccessoryCategory.HeadBand, new[] { "HeadBand" }), "HeadBand", StringComparison.Ordinal))
            throw new InvalidOperationException("An explicit HeadBand enum member must be discoverable as evidence.");

        if (HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "Headwear"))
            throw new InvalidOperationException("Headwear must never be treated as a safe HeadBand host without a separate proven design.");
    }
}

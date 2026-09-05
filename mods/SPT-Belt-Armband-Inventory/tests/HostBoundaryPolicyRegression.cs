using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class HostBoundaryPolicyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string[] vanillaLike = { "Headwear", "Earpiece", "Eyewear", "FaceCover", "ArmBand", "TacticalVest", "Pockets", "Backpack" };

        if (!string.Equals(
                HostBoundaryPolicy.FindExactHost(AccessoryCategory.Belt, vanillaLike),
                DedicatedWearableSlotContract.BeltSlotId,
                StringComparison.Ordinal))
            throw new InvalidOperationException("Belt authority must remain the fixed dedicated Belt slot identity regardless of vanilla enum contents.");

        if (!string.Equals(
                HostBoundaryPolicy.FindExactHost(AccessoryCategory.HeadBand, vanillaLike),
                DedicatedWearableSlotContract.HeadBandSlotId,
                StringComparison.Ordinal))
            throw new InvalidOperationException("HeadBand authority must remain the fixed dedicated HeadBand slot identity regardless of vanilla enum contents.");

        if (!string.Equals(
                HostBoundaryPolicy.FindExactHost(AccessoryCategory.ArmBand, vanillaLike),
                BeltSlotPlan.ArmBand,
                StringComparison.Ordinal))
            throw new InvalidOperationException("ArmBand must continue to resolve only to the vanilla ArmBand slot.");

        if (!HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.Belt, DedicatedWearableSlotContract.BeltSlotId))
            throw new InvalidOperationException("Dedicated Belt slot must be the only safe Belt host.");
        if (!HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, DedicatedWearableSlotContract.HeadBandSlotId))
            throw new InvalidOperationException("Dedicated HeadBand slot must be the only safe HeadBand host.");

        if (HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.Belt, "Belt")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.Belt, "ArmBand")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.Belt, "Pockets")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.Belt, "Backpack"))
            throw new InvalidOperationException("Belt must never alias a plausible or unrelated vanilla slot.");

        if (HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "HeadBand")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "Headwear")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "FaceCover")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "Earpiece")
            || HostBoundaryPolicy.IsSafeExactHost(AccessoryCategory.HeadBand, "ArmBand"))
            throw new InvalidOperationException("HeadBand must never alias a plausible or vanilla head/arm slot.");

        if (!HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.Belt, "ArmBand")
            || !HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.Belt, "Pockets")
            || !HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.Belt, "Backpack"))
            throw new InvalidOperationException("Belt forbidden-substitute guard drifted.");

        if (!HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.HeadBand, "Headwear")
            || !HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.HeadBand, "FaceCover")
            || !HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.HeadBand, "Earpiece")
            || !HostBoundaryPolicy.IsForbiddenVanillaSubstitute(AccessoryCategory.HeadBand, "ArmBand"))
            throw new InvalidOperationException("HeadBand forbidden-substitute guard drifted.");
    }
}

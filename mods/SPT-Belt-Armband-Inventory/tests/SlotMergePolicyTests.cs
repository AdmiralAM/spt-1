using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class SlotMergePolicyTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!SlotMergePolicy.ShouldForce(BeltSlotPlan.ArmBand, false, false))
            throw new InvalidOperationException("Empty ArmBand must retain container-belt destination merge compatibility.");
        if (!SlotMergePolicy.ShouldForce(BeltSlotPlan.ArmBand, true, true))
            throw new InvalidOperationException("Container ArmBand must inherit merge semantics from the equipped belt item.");
        if (SlotMergePolicy.ShouldForce(BeltSlotPlan.ArmBand, true, false))
            throw new InvalidOperationException("Plain occupied ArmBand must retain vanilla merge semantics.");
        if (SlotMergePolicy.ShouldForce(BeltSlotPlan.TacticalVest, true, true))
            throw new InvalidOperationException("Non-ArmBand slots must retain vanilla merge semantics.");
    }
}

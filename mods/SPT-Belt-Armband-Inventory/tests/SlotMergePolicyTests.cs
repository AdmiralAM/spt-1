using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class SlotMergePolicyTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!SlotMergePolicy.ShouldForce(BeltSlotPlan.ArmBand))
            throw new InvalidOperationException("ArmBand must inherit merge semantics from the equipped belt item.");
        if (SlotMergePolicy.ShouldForce(BeltSlotPlan.TacticalVest))
            throw new InvalidOperationException("Non-ArmBand slots must retain vanilla merge semantics.");
    }
}

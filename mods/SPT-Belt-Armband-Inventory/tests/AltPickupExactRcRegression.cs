using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class AltPickupExactRcRegression
{
    sealed class ResultProbe { }
    sealed class EquipmentProbe { }
    sealed class ItemProbe { }

    [ModuleInitializer]
    internal static void Run()
    {
        if (!PickupSlotPolicy.ShouldTry(true, true, true, false, true))
            throw new InvalidOperationException("Exact RC with an empty compatible ArmBand must be eligible for Alt-pickup fallback.");
        if (PickupSlotPolicy.ShouldTry(true, false, true, false, true))
            throw new InvalidOperationException("Non-RC items must never use the B&A&HB Alt-pickup fallback.");
        if (PickupSlotPolicy.ShouldTry(true, true, false, false, true))
            throw new InvalidOperationException("Occupied ArmBand must never be replaced by Alt-pickup fallback.");
        if (PickupSlotPolicy.ShouldTry(true, true, true, true, true))
            throw new InvalidOperationException("Deleted ArmBand must never be revived by Alt-pickup fallback.");
        if (PickupSlotPolicy.ShouldTry(false, true, true, false, true))
            throw new InvalidOperationException("A vanilla pickup destination must always win.");

        MethodInfo postfix = PickupSlotPatches.BuildPostfix(typeof(ResultProbe), typeof(EquipmentProbe), typeof(ItemProbe));
        ParameterInfo[] parameters = postfix.GetParameters();
        if (parameters.Length != 3
            || parameters[0].Name != "__result"
            || parameters[1].Name != "__0"
            || parameters[2].Name != "__1")
            throw new InvalidOperationException("Runtime Alt-pickup postfix must bind typed positional Harmony arguments without __args allocation.");
    }
}

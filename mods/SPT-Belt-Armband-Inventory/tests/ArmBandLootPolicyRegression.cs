using System;
using System.Collections.Generic;
using System.Linq;
using SPTBeltArmbandInventory;

namespace SPTBeltArmbandInventory.Tests;

internal static class ArmBandLootPolicyRegression
{
    internal static void Run()
    {
        Require(ArmBandLootPolicy.Includes(ArmBandVisualPool.Standard, ArmBandVisualPool.ExistingRaid), "Standard includes existing-raid visuals");
        Require(ArmBandLootPolicy.Includes(ArmBandVisualPool.Standard, ArmBandVisualPool.Standard), "Standard includes standard visuals");
        Require(!ArmBandLootPolicy.Includes(ArmBandVisualPool.Standard, ArmBandVisualPool.All), "Standard excludes All-only visuals");
        ArmBandVariantDescriptor[] oneVisual = ArmBandVariantCatalog.All.Take(5).ToArray();
        var weights = new Dictionary<ArmBandRole, int>
        {
            [ArmBandRole.Medical] = 25, [ArmBandRole.Ammo] = 25, [ArmBandRole.Magazine] = 20,
            [ArmBandRole.Technical] = 20, [ArmBandRole.Currency] = 10
        };
        KeyValuePair<string, double>[] split = ArmBandLootPolicy.Split(17.0, oneVisual, weights);
        Require(split.Length == 5, "all positive roles are emitted");
        Require(Math.Abs(split.Sum(item => item.Value) - 17.0) < 1e-12, "source weight is preserved");
        Require(Math.Abs(split[0].Value - 4.25) < 1e-12 && Math.Abs(split[4].Value - 1.7) < 1e-12, "frozen role ratio is applied");
        weights[ArmBandRole.Currency] = 0;
        split = ArmBandLootPolicy.Split(17.0, oneVisual, weights);
        Require(split.Length == 4 && split.All(item => item.Key != oneVisual[4].TemplateId), "zero-weight roles are omitted");
        Require(Math.Abs(split.Sum(item => item.Value) - 17.0) < 1e-12, "disabled role does not reduce probability");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ArmBand loot policy regression failed: " + message);
    }
}

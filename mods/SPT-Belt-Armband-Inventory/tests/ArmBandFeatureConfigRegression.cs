using System;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests;

internal static class ArmBandFeatureConfigRegression
{
    internal static void Run()
    {
        ArmBandFeatureConfig.Validate(new BeltFeatureConfig());

        BeltFeatureConfig duplicateRoles = new();
        duplicateRoles.ArmBandRoles.EnabledRoles.Add(duplicateRoles.ArmBandRoles.EnabledRoles[0]);
        RequireRejected(duplicateRoles, "duplicate roles");

        BeltFeatureConfig allZero = new();
        allZero.ArmBandRoles.Weights = new ArmBandRoleWeights
        {
            Medical = 0, Ammo = 0, Magazine = 0, Technical = 0, Currency = 0
        };
        RequireRejected(allZero, "all-zero effective weights");

        BeltFeatureConfig foreignProtection = new();
        foreignProtection.ArmBandRoles.ProtectionTemplateAllowlist.Add("5b3f16c486f7747c327f55f7");
        RequireRejected(foreignProtection, "foreign protection ID");

        BeltFeatureConfig exactProtection = new();
        exactProtection.ArmBandRoles.ProtectionTemplateAllowlist.Add(ArmBandVariantCatalog.All[0].TemplateId);
        ArmBandFeatureConfig.Validate(exactProtection);
    }

    private static void RequireRejected(BeltFeatureConfig config, string caseName)
    {
        try
        {
            ArmBandFeatureConfig.Validate(config);
            throw new InvalidOperationException("Config regression accepted " + caseName);
        }
        catch (InvalidOperationException exception) when (!exception.Message.StartsWith("Config regression accepted", StringComparison.Ordinal)) { }
    }
}

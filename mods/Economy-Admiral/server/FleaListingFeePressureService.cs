using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class FleaListingFeePressureService(
    GlobalTable globalTable,
    ISptLogger<FleaListingFeePressureService> logger)
{
    private bool applied;

    public FleaListingFeePressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableFleaListingFeePressure || config.Mode != EconomyMode.Enforce)
            return new FleaListingFeePressureResult(false, 1.0, 0, 0, 0, 0, null);
        if (applied)
            return new FleaListingFeePressureResult(true, FleaListingFeePressurePolicy.ResolveTaxMultiplier(config), 0, 0, 0, 0, "already-applied");

        var ragfair = globalTable.Configuration.RagFair;
        var beforeItemTax = (double)ragfair.CommunityItemTax;
        var beforeRequirementTax = ragfair.CommunityRequirementTax;
        if (!double.IsFinite(beforeItemTax) || beforeItemTax <= 0 || !double.IsFinite(beforeRequirementTax) || beforeRequirementTax <= 0)
            throw new InvalidOperationException($"Flea listing-fee pressure requires positive finite native taxes, got item={beforeItemTax}, requirement={beforeRequirementTax}.");

        var multiplier = FleaListingFeePressurePolicy.ResolveTaxMultiplier(config);
        var targetItemTax = (float)FleaListingFeePressurePolicy.Apply(beforeItemTax, config);
        var targetRequirementTax = FleaListingFeePressurePolicy.Apply(beforeRequirementTax, config);

        try
        {
            ragfair.CommunityItemTax = targetItemTax;
            ragfair.CommunityRequirementTax = targetRequirementTax;

            if (Math.Abs(ragfair.CommunityItemTax - targetItemTax) > 0.000001f
                || Math.Abs(ragfair.CommunityRequirementTax - targetRequirementTax) > 0.000001)
                throw new InvalidOperationException("Flea listing-fee pressure post-write verification failed.");

            applied = true;
            logger.Info($"[Economy Admiral] flea listing-fee pressure applied: preset={config.Preset}, multiplier={multiplier:0.###}x, communityItemTax={beforeItemTax:0.###}->{targetItemTax:0.###}, communityRequirementTax={beforeRequirementTax:0.###}->{targetRequirementTax:0.###}");
            return new FleaListingFeePressureResult(true, multiplier, beforeItemTax, targetItemTax, beforeRequirementTax, targetRequirementTax, null);
        }
        catch (Exception applyException)
        {
            ragfair.CommunityItemTax = (float)beforeItemTax;
            ragfair.CommunityRequirementTax = beforeRequirementTax;
            throw new InvalidOperationException($"Flea listing-fee pressure transaction rolled back: {applyException.Message}", applyException);
        }
    }
}

public sealed record FleaListingFeePressureResult(
    bool Applied,
    double Multiplier,
    double ItemTaxBefore,
    double ItemTaxAfter,
    double RequirementTaxBefore,
    double RequirementTaxAfter,
    string? Note);

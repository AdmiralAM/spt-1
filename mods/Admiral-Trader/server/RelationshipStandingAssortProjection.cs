using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Applies the approved post-0.1.0 Relationship standing replenishment policy to an
/// already profile-scoped trader assort clone. This method must never receive the
/// global TradersTable assort instance.
/// </summary>
internal static class RelationshipStandingAssortProjection
{
    internal readonly record struct ProjectionResult(
        bool Applied,
        int LoyaltyLevel,
        int StockPerReset,
        int BuyRestriction,
        string? RejectionReason = null
    );

    /// <summary>
    /// Project the field-marker root offer onto the requested standing tier without
    /// changing offer identity, template, price/barter scheme, quest gates, or the
    /// profile-derived BuyRestrictionCurrent value already applied by SPT.
    /// </summary>
    internal static ProjectionResult Apply(TraderAssort assort, double standing)
    {
        ArgumentNullException.ThrowIfNull(assort);

        var tier = RelationshipStandingStockPolicy.Resolve(standing);
        var marker = assort.Items.FirstOrDefault(item => item.Id.ToString() == RelationshipStandingStockPolicy.MarkerOfferId);

        if (marker is null)
        {
            return Rejected(tier, "marker offer missing from profile-scoped assort");
        }

        if (marker.Template.ToString() != RelationshipStandingStockPolicy.MarkerTpl)
        {
            return Rejected(tier, "marker offer template mismatch");
        }

        if (!string.Equals(marker.SlotId, "hideout", StringComparison.Ordinal) || marker.Upd is null)
        {
            return Rejected(tier, "marker root offer shape invalid");
        }

        // StackObjectsCount is the finite trader stock visible for this reset.
        // BuyRestrictionMax is the per-profile cap. Preserve BuyRestrictionCurrent:
        // TraderAssortHelper has already projected the profile's persisted purchases.
        marker.Upd.StackObjectsCount = tier.StockPerReset;
        marker.Upd.BuyRestrictionMax = tier.BuyRestriction;
        marker.Upd.UnlimitedCount = false;

        return new ProjectionResult(true, tier.LoyaltyLevel, tier.StockPerReset, tier.BuyRestriction);
    }

    private static ProjectionResult Rejected(RelationshipStandingStockPolicy.Tier tier, string reason) =>
        new(false, tier.LoyaltyLevel, tier.StockPerReset, tier.BuyRestriction, reason);
}

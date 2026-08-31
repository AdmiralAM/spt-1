namespace AdmiralTrader.Server;

/// <summary>
/// Pure, side-effect-free standing policy for the post-0.1.0 Relationship field-marker
/// replenishment slice. This deliberately does not publish or mutate a trader assort;
/// the eventual request/profile-scoped projection seam remains a separate runtime gate.
/// </summary>
internal static class RelationshipStandingStockPolicy
{
    internal const string MarkerOfferId = "ad2000000000000000000004";
    internal const string MarkerTpl = "5991b51486f77447b112d44f";
    internal const int MarkerPriceRub = 16_500;

    internal readonly record struct Tier(
        int LoyaltyLevel,
        double StandingThreshold,
        int StockPerReset,
        int BuyRestriction);

    internal static readonly Tier Ll1 = new(1, 0.00, 12, 4);
    internal static readonly Tier Ll2 = new(2, 0.10, 16, 6);
    internal static readonly Tier Ll3 = new(3, 0.30, 20, 8);
    internal static readonly Tier Ll4 = new(4, 0.55, 24, 10);

    /// <summary>
    /// Resolve exactly one finite replenishment tier from current Admiral standing.
    /// Invalid/unresolved values fail safe to the frozen LL1 baseline.
    /// </summary>
    internal static Tier Resolve(double standing)
    {
        if (double.IsNaN(standing) || double.IsInfinity(standing) || standing < Ll2.StandingThreshold)
            return Ll1;
        if (standing < Ll3.StandingThreshold)
            return Ll2;
        if (standing < Ll4.StandingThreshold)
            return Ll3;
        return Ll4;
    }
}

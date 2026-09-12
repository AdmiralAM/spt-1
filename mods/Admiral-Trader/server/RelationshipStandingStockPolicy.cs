namespace AdmiralTrader.Server;

/// <summary>
/// Pure, side-effect-free standing policy for the post-0.1.0 Relationship field-marker
/// replenishment slice. Tier promotion mirrors Admiral's real loyalty contract: both
/// standing and PMC level must satisfy the same LL threshold before stock is uplifted.
/// </summary>
internal static class RelationshipStandingStockPolicy
{
    internal const string MarkerOfferId = "ad2000000000000000000004";
    internal const string MarkerTpl = "5991b51486f77447b112d44f";
    internal const int MarkerPriceRub = 16_500;

    internal readonly record struct Tier(
        int LoyaltyLevel,
        double StandingThreshold,
        int MinimumPlayerLevel,
        int StockPerReset,
        int BuyRestriction);

    internal static readonly Tier Ll1 = new(1, 0.00, 1, 12, 4);
    internal static readonly Tier Ll2 = new(2, 0.10, 15, 16, 6);
    internal static readonly Tier Ll3 = new(3, 0.30, 25, 20, 8);
    internal static readonly Tier Ll4 = new(4, 0.55, 35, 24, 10);

    /// <summary>
    /// Resolve exactly one finite replenishment tier from the current Admiral standing
    /// and PMC level. Invalid/unresolved values, or either unmet dimension, fail safe to
    /// the highest tier whose complete loyalty contract is actually satisfied.
    /// </summary>
    internal static Tier Resolve(double standing, int playerLevel)
    {
        if (!double.IsFinite(standing) || playerLevel < Ll2.MinimumPlayerLevel || standing < Ll2.StandingThreshold)
            return Ll1;
        if (playerLevel < Ll3.MinimumPlayerLevel || standing < Ll3.StandingThreshold)
            return Ll2;
        if (playerLevel < Ll4.MinimumPlayerLevel || standing < Ll4.StandingThreshold)
            return Ll3;
        return Ll4;
    }
}

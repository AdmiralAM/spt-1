using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Resolves Admiral standing and PMC level from the profile selected by the current SPT
/// session. Both are required because Admiral loyalty tiers are standing+level gates.
/// This is intentionally read-only and never derives state from the global trader table.
/// </summary>
[Injectable]
public sealed class RelationshipStandingProfileResolver(ProfileHelper profileHelper)
{
    private static readonly MongoId AdmiralTraderId = new(RuntimeIdentity.TraderId);

    /// <summary>
    /// Resolve current profile Admiral standing and PMC level. Missing/invalid state
    /// fails closed so callers retain the LL1 finite-stock baseline.
    /// </summary>
    public bool TryResolve(MongoId sessionId, out double standing, out int playerLevel)
    {
        standing = double.NaN;
        playerLevel = 0;

        var pmcProfile = profileHelper.GetPmcProfile(sessionId);
        if (pmcProfile?.Info?.Level is not int resolvedLevel
            || resolvedLevel < 1
            || pmcProfile.TradersInfo is null
            || !pmcProfile.TradersInfo.TryGetValue(AdmiralTraderId, out TraderInfo? traderInfo)
            || traderInfo?.Standing is not double resolvedStanding
            || !double.IsFinite(resolvedStanding))
        {
            return false;
        }

        standing = resolvedStanding;
        playerLevel = resolvedLevel;
        return true;
    }
}

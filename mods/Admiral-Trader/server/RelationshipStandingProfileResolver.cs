using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Resolves Admiral standing from the profile selected by the current SPT session.
/// This is intentionally read-only: Relationship stock projection must never write
/// loyalty/standing back into the profile or derive it from the global trader table.
/// </summary>
[Injectable]
public sealed class RelationshipStandingProfileResolver(ProfileHelper profileHelper)
{
    private static readonly MongoId AdmiralTraderId = new(RuntimeIdentity.TraderId);

    /// <summary>
    /// Resolve the current profile's Admiral standing. Missing/invalid state fails
    /// closed so callers can retain the LL1 finite-stock baseline.
    /// </summary>
    public bool TryResolve(MongoId sessionId, out double standing)
    {
        standing = double.NaN;

        var pmcProfile = profileHelper.GetPmcProfile(sessionId);
        if (pmcProfile?.TradersInfo is null
            || !pmcProfile.TradersInfo.TryGetValue(AdmiralTraderId, out TraderInfo? traderInfo)
            || traderInfo?.Standing is not double resolvedStanding
            || !double.IsFinite(resolvedStanding))
        {
            return false;
        }

        standing = resolvedStanding;
        return true;
    }
}

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Composes the post-0.1.0 Relationship pipeline for an assort SPT has already cloned
/// and projected for the requesting profile. Tier selection requires both current
/// Admiral standing and PMC level so it cannot outrun Admiral's actual loyalty contract.
/// </summary>
[Injectable]
public sealed class RelationshipStandingAssortCoordinator(RelationshipStandingProfileResolver standingResolver)
{
    public readonly record struct Result(
        bool Applied,
        bool ProfileProgressionResolved,
        int LoyaltyLevel,
        int StockPerReset,
        int BuyRestriction,
        string? RejectionReason = null
    );

    public Result Project(MongoId sessionId, TraderAssort profileScopedAssort)
    {
        ArgumentNullException.ThrowIfNull(profileScopedAssort);

        var progressionResolved = standingResolver.TryResolve(sessionId, out var standing, out var playerLevel);
        if (!progressionResolved)
        {
            standing = double.NaN;
            playerLevel = 0;
        }

        var projection = RelationshipStandingAssortProjection.Apply(profileScopedAssort, standing, playerLevel);
        return new Result(
            projection.Applied,
            progressionResolved,
            projection.LoyaltyLevel,
            projection.StockPerReset,
            projection.BuyRestriction,
            projection.RejectionReason
        );
    }
}

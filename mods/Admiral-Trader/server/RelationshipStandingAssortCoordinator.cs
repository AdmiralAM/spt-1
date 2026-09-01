using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AdmiralTrader.Server;

/// <summary>
/// Composes the post-0.1.0 Relationship standing pipeline for an assort that SPT has
/// already cloned and projected for the requesting profile. This class deliberately
/// does not own or discover the publication hook: callers must provide the
/// request/profile-scoped assort returned by the supported SPT assort path.
/// </summary>
[Injectable]
public sealed class RelationshipStandingAssortCoordinator(RelationshipStandingProfileResolver standingResolver)
{
    public readonly record struct Result(
        bool Applied,
        bool StandingResolved,
        int LoyaltyLevel,
        int StockPerReset,
        int BuyRestriction,
        string? RejectionReason = null
    );

    /// <summary>
    /// Resolve standing from the requesting session and project exactly one finite
    /// field-marker tier onto the supplied profile-scoped assort. Missing profile or
    /// standing state fails safe to the frozen LL1 values rather than suppressing the
    /// projection or granting a higher tier.
    /// </summary>
    public Result Project(MongoId sessionId, TraderAssort profileScopedAssort)
    {
        ArgumentNullException.ThrowIfNull(profileScopedAssort);

        var standingResolved = standingResolver.TryResolve(sessionId, out var standing);
        if (!standingResolved)
        {
            standing = double.NaN;
        }

        var projection = RelationshipStandingAssortProjection.Apply(profileScopedAssort, standing);
        return new Result(
            projection.Applied,
            standingResolved,
            projection.LoyaltyLevel,
            projection.StockPerReset,
            projection.BuyRestriction,
            projection.RejectionReason
        );
    }
}

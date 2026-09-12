using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.HttpResponse;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Utils;

namespace AdmiralTrader.Server;

/// <summary>
/// Handles Admiral's exact SPT 4.1.5 trader-assort route, obtains the normal
/// profile-scoped response from TraderCallbacks, then applies the relationship tier.
/// M5 enables only the bounded, profile-scoped field-marker replenishment projection.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers - 1)]
public sealed class RelationshipStandingAssortDynamicRouter(
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    TraderCallbacks traderCallbacks,
    RelationshipStandingAssortCoordinator coordinator)
    : DynamicRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                $"/client/trading/api/getTraderAssort/{RuntimeIdentity.TraderId}",
                (url, request, sessionId, _, cancellationToken) =>
                    GetAndProjectAdmiralAssortAsync(url, request, sessionId, cancellationToken, jsonUtil, httpResponseUtil, traderCallbacks, coordinator)
            ),
        ]
    )
{
    private static async ValueTask<string> GetAndProjectAdmiralAssortAsync(
        string url,
        EmptyRequestData request,
        MongoId sessionId,
        CancellationToken cancellationToken,
        JsonUtil jsonUtil,
        HttpResponseUtil httpResponseUtil,
        TraderCallbacks traderCallbacks,
        RelationshipStandingAssortCoordinator coordinator)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var output = await traderCallbacks.GetAssort(url, request, sessionId);

        GetBodyResponseData<TraderAssort>? response;
        try
        {
            response = jsonUtil.Deserialize<GetBodyResponseData<TraderAssort>>(output);
        }
        catch
        {
            // Fail closed: malformed or incompatible vanilla output is returned unchanged.
            return output;
        }

        if (response?.Data is null || response.Err is not null and not BackendErrorCodes.None)
        {
            return output;
        }

        coordinator.Project(sessionId, response.Data);
        return httpResponseUtil.GetBody(response.Data, response.Err ?? BackendErrorCodes.None, response.ErrMsg);
    }
}

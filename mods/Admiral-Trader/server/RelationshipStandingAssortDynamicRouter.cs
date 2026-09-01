using SPTarkov.DI.Annotations;
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
/// Post-processes the vanilla SPT 4.1.3 trader-assort response for Admiral only.
/// HttpRouter executes matching dynamic routers in registration order and passes the
/// previous route output into the next route, so this router deliberately runs after
/// the vanilla TraderDynamicRouter and projects only its already profile-scoped clone.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class RelationshipStandingAssortDynamicRouter(
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    RelationshipStandingAssortCoordinator coordinator)
    : DynamicRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/client/trading/api/getTraderAssort/",
                (url, _, sessionId, output, cancellationToken) =>
                    ProjectAdmiralAssortAsync(url, sessionId, output, cancellationToken, jsonUtil, httpResponseUtil, coordinator)
            ),
        ]
    )
{
    private static ValueTask<string> ProjectAdmiralAssortAsync(
        string url,
        MongoId sessionId,
        string? output,
        CancellationToken cancellationToken,
        JsonUtil jsonUtil,
        HttpResponseUtil httpResponseUtil,
        RelationshipStandingAssortCoordinator coordinator)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!url.EndsWith(RuntimeIdentity.TraderId, StringComparison.Ordinal) || string.IsNullOrEmpty(output))
        {
            return ValueTask.FromResult(output ?? string.Empty);
        }

        GetBodyResponseData<TraderAssort>? response;
        try
        {
            response = jsonUtil.Deserialize<GetBodyResponseData<TraderAssort>>(output);
        }
        catch
        {
            // Fail closed: malformed or incompatible vanilla output is returned unchanged.
            return ValueTask.FromResult(output);
        }

        if (response?.Data is null || response.Err is not null and not BackendErrorCodes.None)
        {
            return ValueTask.FromResult(output);
        }

        coordinator.Project(sessionId, response.Data);
        return ValueTask.FromResult(httpResponseUtil.GetBody(response.Data, response.Err ?? BackendErrorCodes.None, response.ErrMsg));
    }
}

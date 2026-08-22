using SPTItemIntelligence;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SPTItemIntelligence.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt.itemintelligence.server";
    public string Name { get; init; } = "SPT Item Intelligence Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.3.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable]
public sealed class RequirementDataService(
    JsonUtil jsonUtil,
    ProfileHelper profileHelper,
    TemplateTable templateTable,
    HideoutTable hideoutTable)
{
    public ValueTask<string> BuildSnapshotAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? profile = profileHelper.GetPmcProfile(sessionId);
        RequirementDataEnvelope envelope = new(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            profile!,
            templateTable.Quests,
            hideoutTable);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class ItemIntelligenceRouter(JsonUtil jsonUtil, RequirementDataService dataService)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction(
                RequirementDataContract.SnapshotRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await dataService.BuildSnapshotAsync(sessionId, cancellationToken)
            )
        ])
{ }

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class ItemIntelligenceLoadNotice(ISptLogger<ItemIntelligenceLoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Success("SPT Item Intelligence Server v0.3.0 loaded; requirement snapshot route ready");
        return Task.CompletedTask;
    }
}

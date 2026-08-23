using SPTQuestPlanner;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SPTQuestPlanner.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt.questplanner.server";
    public string Name { get; init; } = "SPT Quest Planner Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.1.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable]
public sealed class PlannerSnapshotService(
    JsonUtil jsonUtil,
    ProfileHelper profileHelper,
    TemplateTable templateTable)
{
    public ValueTask<string> BuildSnapshotAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object? profile = profileHelper.GetPmcProfile(sessionId);
        PlannerSnapshotEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            profile!,
            templateTable.Quests);

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class QuestPlannerRouter(JsonUtil jsonUtil, PlannerSnapshotService snapshotService)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction(
                PlannerDataContract.SnapshotRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await snapshotService.BuildSnapshotAsync(sessionId, cancellationToken)
            )
        ])
{ }

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class QuestPlannerLoadNotice(ISptLogger<QuestPlannerLoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Success("SPT Quest Planner Server v0.1.0 loaded; foundation snapshot route ready");
        return Task.CompletedTask;
    }
}

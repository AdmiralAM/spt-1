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
    public SemanticVersioning.Version Version { get; init; } = new("0.8.0");
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
    TemplateTable templateTable,
    PlannerStaticDataCache staticDataCache)
{
    public ValueTask<string> BuildTopologyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlannerStaticData staticData = staticDataCache.Get();
        PlannerTopologyEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            staticData.Extraction.Nodes,
            staticData.Extraction.Prerequisites,
            staticData.Extraction.ItemRequirements,
            staticData.Validation,
            staticData.Extraction.Warnings);
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }

    public ValueTask<string> BuildStateAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlannerStaticData staticData = staticDataCache.Get();
        PlannerStateEnvelope envelope = BuildStateEnvelope(sessionId, staticData);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }

    public ValueTask<string> BuildSnapshotAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlannerStaticData staticData = staticDataCache.Get();
        PlannerStateEnvelope state = BuildStateEnvelope(sessionId, staticData);
        PlannerSnapshotEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            state.GeneratedAtUnixSeconds,
            staticData.Extraction.Nodes,
            staticData.Extraction.Prerequisites,
            staticData.Extraction.ItemRequirements,
            state.Player,
            state.Inventory,
            state.StateCounts,
            staticData.Validation,
            state.Evaluation,
            state.OutstandingItems,
            state.Warnings);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }

    public ValueTask<string> BuildDiagnosticsAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlannerStaticData staticData = staticDataCache.Get();
        object profile = profileHelper.GetPmcProfile(sessionId)!;
        PlayerProjection player = ProfileProjectionExtractor.Extract(profile);
        InventoryProjection inventory = InventoryProjectionExtractor.Extract(profile);
        PlannerEvaluationResult evaluation = PlannerEvaluator.Evaluate(staticData.Graph, staticData.Extraction.ItemRequirements, player);
        IReadOnlyList<string> warnings = BuildWarnings(staticData.Extraction, player, inventory, evaluation);
        PlannerDiagnosticsEnvelope envelope = new(
            PlannerDataContract.SchemaVersion,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            profile,
            templateTable.Quests,
            warnings);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }

    private PlannerStateEnvelope BuildStateEnvelope(MongoId sessionId, PlannerStaticData staticData)
    {
        object profile = profileHelper.GetPmcProfile(sessionId)!;
        QuestExtractionResult extraction = staticData.Extraction;
        PlayerProjection player = ProfileProjectionExtractor.Extract(profile);
        InventoryProjection inventory = InventoryProjectionExtractor.Extract(profile);
        IReadOnlyDictionary<QuestState, int> stateCounts = ProfileProjectionExtractor.CountStates(extraction.Nodes, player);
        PlannerEvaluationResult evaluation = PlannerEvaluator.Evaluate(staticData.Graph, extraction.ItemRequirements, player);
        IReadOnlyList<OutstandingItemRequirement> outstanding =
            InventoryProjectionExtractor.CalculateOutstanding(evaluation.ItemRequirements, inventory);
        IReadOnlyList<string> warnings = BuildWarnings(extraction, player, inventory, evaluation);
        return new PlannerStateEnvelope(
            PlannerDataContract.SchemaVersion,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            player,
            inventory,
            stateCounts,
            evaluation,
            outstanding,
            warnings);
    }

    private static IReadOnlyList<string> BuildWarnings(
        QuestExtractionResult extraction,
        PlayerProjection player,
        InventoryProjection inventory,
        PlannerEvaluationResult evaluation)
    {
        List<string> warnings = new(
            extraction.Warnings.Count + player.Warnings.Count + inventory.Warnings.Count + evaluation.Warnings.Count + 2);
        warnings.AddRange(extraction.Warnings);
        warnings.AddRange(player.Warnings);
        warnings.AddRange(inventory.Warnings);
        warnings.AddRange(evaluation.Warnings);
        if (extraction.Nodes.Count > 0 && player.QuestStates.Count == 0)
            warnings.Add("Quest database is populated but PMC quest-state projection is empty");
        if (evaluation.ItemRequirements.Count > 0 && inventory.ByTemplate.Count == 0)
            warnings.Add("Quest item requirements exist but PMC inventory projection is empty");
        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class QuestPlannerRouter(JsonUtil jsonUtil, PlannerSnapshotService snapshotService)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction(
                PlannerDataContract.TopologyRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await snapshotService.BuildTopologyAsync(cancellationToken)),
            new RouteAction(
                PlannerDataContract.StateRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await snapshotService.BuildStateAsync(sessionId, cancellationToken)),
            new RouteAction(
                PlannerDataContract.SnapshotRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await snapshotService.BuildSnapshotAsync(sessionId, cancellationToken)),
            new RouteAction(
                PlannerDataContract.DiagnosticsRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await snapshotService.BuildDiagnosticsAsync(sessionId, cancellationToken))
        ])
{ }

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class QuestPlannerLoadNotice(ISptLogger<QuestPlannerLoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Success("SPT Quest Planner Server v0.8.0 loaded; split topology/state routes ready");
        return Task.CompletedTask;
    }
}

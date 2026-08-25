using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class EnforcementPlanService(
    QuestAnalysisService questAnalysisService,
    ModHelper modHelper,
    ISptLogger<EnforcementPlanService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EnforcementPlanService).Assembly);
        var config = await LoadConfigAsync(modPath, cancellationToken);
        if (config.Mode == EconomyMode.Off)
        {
            return;
        }

        var analysis = questAnalysisService.GetSnapshot();
        var candidates = analysis.Quests
            .Where(row => row.ObservationalFlags.Count > 0)
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(BuildCandidate)
            .ToList();

        var plan = new EnforcementPlanReport
        {
            SchemaVersion = 1,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            SourceAnalysisSchemaVersion = analysis.SchemaVersion,
            EnforceRequested = config.Mode == EconomyMode.Enforce,
            ApplyMutations = false,
            MutationCount = 0,
            CandidateCount = candidates.Count,
            Note = "Fail-closed planning artifact only. Candidates are derived from the in-memory unified audit snapshot; no target reward values are invented and no final DB records are mutated.",
            Candidates = candidates,
        };

        var planPath = SafePath(modPath, "reports/economy-admiral-enforcement-plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan, JsonOptions), cancellationToken);

        if (plan.EnforceRequested)
        {
            logger.Warning($"[Economy Admiral] Enforce requested but remains fail-closed: {plan.CandidateCount} review candidates, 0 mutations; plan={planPath}");
        }
        else
        {
            logger.Info($"[Economy Admiral] enforcement plan audit complete: {plan.CandidateCount} review candidates, 0 mutations; plan={planPath}");
        }
    }

    private static EnforcementPlanCandidate BuildCandidate(QuestAnalysisRow row)
    {
        var actions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var flag in row.ObservationalFlags)
        {
            switch (flag)
            {
                case "HIGH_ITEM_VALUE_LOW_STRUCTURE":
                case "RESTARTABLE_HIGH_ITEM_VALUE":
                    actions.Add("ReviewItemRewardBudget");
                    break;
                case "HIGH_XP_LOW_DEPTH":
                case "RESTARTABLE_HIGH_XP":
                    actions.Add("ReviewXpRewardBudget");
                    break;
                case "HIGH_STANDING_LOW_DEPTH":
                    actions.Add("ReviewStandingRewardBudget");
                    break;
                case "PREREQUISITE_CYCLE":
                    actions.Add("ReviewPrerequisiteGraph");
                    break;
            }
        }

        return new EnforcementPlanCandidate
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            TraderId = row.TraderId,
            Restartable = row.Restartable,
            ReasonFlags = row.ObservationalFlags.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            ProposedReviewActions = actions.ToList(),
            AutomaticMutationAllowed = false,
            ProposedMutation = null,
        };
    }

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Economy Admiral report path must stay inside the mod directory.");
        }
        return path;
    }

    private static async Task<EconomyConfig> LoadConfigAsync(string modPath, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(modPath, "config", "config.json");
        if (!File.Exists(configPath))
        {
            return new EconomyConfig();
        }

        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken) ?? new EconomyConfig();
    }
}

public sealed record EnforcementPlanReport
{
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public required int SourceAnalysisSchemaVersion { get; init; }
    public required bool EnforceRequested { get; init; }
    public required bool ApplyMutations { get; init; }
    public required int MutationCount { get; init; }
    public required int CandidateCount { get; init; }
    public required string Note { get; init; }
    public required List<EnforcementPlanCandidate> Candidates { get; init; }
}

public sealed record EnforcementPlanCandidate
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool Restartable { get; init; }
    public required List<string> ReasonFlags { get; init; }
    public required List<string> ProposedReviewActions { get; init; }
    public required bool AutomaticMutationAllowed { get; init; }
    public required object? ProposedMutation { get; init; }
}

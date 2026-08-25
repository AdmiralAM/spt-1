using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class EnforcementPlanService(
    ModHelper modHelper,
    ISptLogger<EnforcementPlanService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task RunAsync(QuestAnalysisReport analysis, QuestProvenanceDeltaReport provenance, CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EnforcementPlanService).Assembly);
        var config = await LoadConfigAsync(modPath, cancellationToken);
        var provenanceByQuest = provenance.Quests.ToDictionary(row => row.QuestId, StringComparer.Ordinal);

        var candidates = analysis.Quests
            .Where(row => row.ObservationalFlags.Count > 0)
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => BuildCandidate(row, provenanceByQuest.GetValueOrDefault(row.QuestId)))
            .ToList();

        var countsByProvenance = candidates
            .GroupBy(candidate => candidate.ProvenanceClass, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var countsByEligibility = candidates
            .GroupBy(candidate => candidate.MutationEligibilityClass, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var plan = new EnforcementPlanReport
        {
            SchemaVersion = 4,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            SourceAnalysisSchemaVersion = analysis.SchemaVersion,
            SourceProvenanceSchemaVersion = provenance.SchemaVersion,
            ProvenanceAware = true,
            MutationEligibilityPolicyVersion = 2,
            EnforceRequested = config.Mode == EconomyMode.Enforce,
            ApplyMutations = false,
            MutationCount = 0,
            CandidateCount = candidates.Count,
            CandidateCountsByProvenance = countsByProvenance,
            CandidateCountsByEligibility = countsByEligibility,
            Note = "Fail-closed planning artifact only. PristineUnchanged quests are protected. ModAdded quests can only become policy-eligible for flagged reward dimensions. PristineModified quests can only become policy-eligible where the same reward dimension is proven changed versus the pristine snapshot. Structural-only changes never authorize reward mutation. Unknown provenance is blocked. AutomaticMutationAllowed remains false for every row.",
            Candidates = candidates,
        };

        var planPath = SafePath(modPath, "reports/economy-admiral-enforcement-plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan, JsonOptions), cancellationToken);

        if (plan.EnforceRequested)
            logger.Warning($"[Economy Admiral] Enforce requested but remains fail-closed: {plan.CandidateCount} provenance-aware review candidates, 0 mutations; plan={planPath}");
        else
            logger.Info($"[Economy Admiral] enforcement plan audit complete: {plan.CandidateCount} provenance-aware review candidates, 0 mutations; plan={planPath}");
    }

    private static EnforcementPlanCandidate BuildCandidate(QuestAnalysisRow row, QuestProvenanceDeltaRow? provenance)
    {
        var actions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var flag in row.ObservationalFlags)
        {
            switch (flag)
            {
                case "HIGH_ITEM_VALUE_LOW_STRUCTURE":
                case "RESTARTABLE_HIGH_ITEM_VALUE": actions.Add("ReviewItemRewardBudget"); break;
                case "HIGH_XP_LOW_DEPTH":
                case "RESTARTABLE_HIGH_XP": actions.Add("ReviewXpRewardBudget"); break;
                case "HIGH_STANDING_LOW_DEPTH": actions.Add("ReviewStandingRewardBudget"); break;
                case "PREREQUISITE_CYCLE": actions.Add("ReviewPrerequisiteGraph"); break;
            }
        }

        var provenanceClass = provenance?.Provenance ?? "Unknown";
        var changedDimensions = provenance?.ChangedDimensions ?? [];
        var potentialMutationDimensions = ResolvePotentialMutationDimensions(provenanceClass, changedDimensions, actions);
        var eligibility = ResolveMutationEligibility(provenanceClass, potentialMutationDimensions.Count);
        return new EnforcementPlanCandidate
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            TraderId = row.TraderId,
            Restartable = row.Restartable,
            ProvenanceClass = provenanceClass,
            PristineUntouched = string.Equals(provenanceClass, "PristineUnchanged", StringComparison.Ordinal),
            MutationEligibilityClass = eligibility.Class,
            PotentialAutomaticMutationEligible = eligibility.PotentiallyEligible,
            PotentialMutationDimensions = potentialMutationDimensions,
            MutationEligibilityReason = eligibility.Reason,
            ChangedDimensions = changedDimensions,
            ReasonFlags = row.ObservationalFlags.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            ProposedReviewActions = actions.ToList(),
            AutomaticMutationAllowed = false,
            ProposedMutation = null,
        };
    }

    private static List<string> ResolvePotentialMutationDimensions(
        string provenanceClass,
        IReadOnlyCollection<string> changedDimensions,
        IReadOnlyCollection<string> reviewActions
    )
    {
        if (string.Equals(provenanceClass, "PristineUnchanged", StringComparison.Ordinal)
            || string.Equals(provenanceClass, "Unknown", StringComparison.Ordinal))
        {
            return [];
        }

        var dimensions = new SortedSet<string>(StringComparer.Ordinal);
        var modAdded = string.Equals(provenanceClass, "ModAdded", StringComparison.Ordinal);

        if (reviewActions.Contains("ReviewItemRewardBudget", StringComparer.Ordinal)
            && (modAdded || changedDimensions.Contains("SuccessItemHandbookValue", StringComparer.Ordinal)))
        {
            dimensions.Add("ItemRewardBudget");
        }
        if (reviewActions.Contains("ReviewXpRewardBudget", StringComparer.Ordinal)
            && (modAdded || changedDimensions.Contains("Experience", StringComparer.Ordinal)))
        {
            dimensions.Add("Experience");
        }
        if (reviewActions.Contains("ReviewStandingRewardBudget", StringComparer.Ordinal)
            && (modAdded || changedDimensions.Contains("TraderStanding", StringComparer.Ordinal)))
        {
            dimensions.Add("TraderStanding");
        }

        return dimensions.ToList();
    }

    private static MutationEligibility ResolveMutationEligibility(string provenanceClass, int potentialDimensionCount) => provenanceClass switch
    {
        "ModAdded" when potentialDimensionCount > 0 => new MutationEligibility("PolicyEligibleModAdded", true, "Quest was added after the pristine startup snapshot and has flagged reward dimensions. Future automatic mutation may be considered only for the listed potential dimensions after an explicit approved policy."),
        "ModAdded" => new MutationEligibility("ReviewOnlyModAdded", false, "Quest was added after the pristine startup snapshot, but no currently flagged reward dimension maps to a future mutation dimension."),
        "PristineModified" when potentialDimensionCount > 0 => new MutationEligibility("PolicyEligibleModifiedPristine", true, "A pristine quest was modified by the final mod stack. Only listed reward dimensions that are both flagged and proven changed may ever be considered by an explicit approved policy."),
        "PristineModified" => new MutationEligibility("ProtectedUnchangedRewardDimensions", false, "The pristine quest was modified, but none of the currently flagged reward dimensions are proven changed versus the pristine snapshot."),
        "PristineUnchanged" => new MutationEligibility("ProtectedPristine", false, "Quest matches the pristine startup snapshot and is protected from automatic mutation by default."),
        _ => new MutationEligibility("BlockedUnknownProvenance", false, "Quest provenance is not proven; automatic mutation is blocked."),
    };

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Economy Admiral report path must stay inside the mod directory.");
        return path;
    }

    private static async Task<EconomyConfig> LoadConfigAsync(string modPath, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(modPath, "config", "config.json");
        if (!File.Exists(configPath)) return new EconomyConfig();
        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken) ?? new EconomyConfig();
    }

    private sealed record MutationEligibility(string Class, bool PotentiallyEligible, string Reason);
}

public sealed record EnforcementPlanReport
{
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public required int SourceAnalysisSchemaVersion { get; init; }
    public required int SourceProvenanceSchemaVersion { get; init; }
    public required bool ProvenanceAware { get; init; }
    public required int MutationEligibilityPolicyVersion { get; init; }
    public required bool EnforceRequested { get; init; }
    public required bool ApplyMutations { get; init; }
    public required int MutationCount { get; init; }
    public required int CandidateCount { get; init; }
    public required Dictionary<string, int> CandidateCountsByProvenance { get; init; }
    public required Dictionary<string, int> CandidateCountsByEligibility { get; init; }
    public required string Note { get; init; }
    public required List<EnforcementPlanCandidate> Candidates { get; init; }
}

public sealed record EnforcementPlanCandidate
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool Restartable { get; init; }
    public required string ProvenanceClass { get; init; }
    public required bool PristineUntouched { get; init; }
    public required string MutationEligibilityClass { get; init; }
    public required bool PotentialAutomaticMutationEligible { get; init; }
    public required List<string> PotentialMutationDimensions { get; init; }
    public required string MutationEligibilityReason { get; init; }
    public required List<string> ChangedDimensions { get; init; }
    public required List<string> ReasonFlags { get; init; }
    public required List<string> ProposedReviewActions { get; init; }
    public required bool AutomaticMutationAllowed { get; init; }
    public required object? ProposedMutation { get; init; }
}

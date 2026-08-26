using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class EnforcementPlanService(
    TemplateTable templates,
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<EnforcementPlanService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<EnforcementPlanReport> RunAsync(
        QuestAnalysisReport analysis,
        QuestProvenanceDeltaReport provenance,
        CancellationToken cancellationToken)
    {
        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EnforcementPlanService).Assembly);
        var provenanceByQuest = provenance.Quests.ToDictionary(row => row.QuestId, StringComparer.Ordinal);
        var handbookPrices = BuildHandbookPrices();

        var candidates = analysis.Quests
            .Where(row => row.ObservationalFlags.Count > 0 || config.QuestRewardOverrides.ContainsKey(row.QuestId))
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => BuildCandidate(row, provenanceByQuest.GetValueOrDefault(row.QuestId), analysis, config, handbookPrices))
            .ToList();

        var proposals = candidates
            .SelectMany(candidate => candidate.ProposedMutations)
            .OrderBy(mutation => mutation.QuestId, StringComparer.Ordinal)
            .ThenBy(mutation => mutation.Dimension, StringComparer.Ordinal)
            .ToList();

        NumericRewardTransactionOutcome transaction = new()
        {
            Committed = false,
            RolledBack = false,
            Results = Array.Empty<NumericRewardTransactionResult>(),
        };
        if (config.Mode == EconomyMode.Enforce && proposals.Count > 0)
        {
            try
            {
                var requests = BuildTransactionRequests(proposals);
                transaction = NumericRewardTransactionCore.Execute(requests);
            }
            catch (Exception exception)
            {
                // Preflight happens before NumericRewardTransactionCore gets any writable slots, so no DB field has been changed.
                transaction = new NumericRewardTransactionOutcome
                {
                    Committed = false,
                    RolledBack = true,
                    Error = $"Enforce preflight failed before writes: {exception.Message}",
                    Results = Array.Empty<NumericRewardTransactionResult>(),
                };
            }
        }

        var appliedByKey = transaction.Results.ToDictionary(
            entry => MutationKey(entry.QuestId, entry.Dimension),
            StringComparer.Ordinal);
        var finalizedCandidates = candidates
            .Select(candidate => candidate with
            {
                ProposedMutations = candidate.ProposedMutations
                    .Select(mutation => appliedByKey.TryGetValue(MutationKey(mutation.QuestId, mutation.Dimension), out var appliedMutation)
                        ? mutation with { Applied = true, After = appliedMutation.After }
                        : mutation with { Applied = false, After = mutation.Before })
                    .ToList(),
            })
            .Select(candidate => candidate with
            {
                ProposedMutation = candidate.ProposedMutations.Count == 0 ? null : candidate.ProposedMutations,
            })
            .ToList();

        var countsByProvenance = finalizedCandidates
            .GroupBy(candidate => candidate.ProvenanceClass, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var countsByEligibility = finalizedCandidates
            .GroupBy(candidate => candidate.MutationEligibilityClass, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var itemStackEnabled = config.EnableItemRewardStackNormalization;
        var report = new EnforcementPlanReport
        {
            SchemaVersion = itemStackEnabled ? 6 : 5,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            SelectedPolicy = itemStackEnabled
                ? $"PresetNumericQuestRewardCapV1+SingleStackItemBudgetCapV1/{config.Preset}"
                : $"PresetNumericQuestRewardCapV1/{config.Preset}",
            SourceAnalysisSchemaVersion = analysis.SchemaVersion,
            SourceProvenanceSchemaVersion = provenance.SchemaVersion,
            ProvenanceAware = true,
            MutationEligibilityPolicyVersion = itemStackEnabled ? 4 : 3,
            EnforceRequested = config.Mode == EconomyMode.Enforce,
            ApplyMutations = config.Mode == EconomyMode.Enforce,
            PlannedMutationCount = proposals.Count,
            MutationCount = transaction.Results.Count,
            TransactionCommitted = config.Mode == EconomyMode.Enforce && proposals.Count > 0 && transaction.Committed,
            TransactionRolledBack = transaction.RolledBack,
            TransactionError = transaction.Error,
            CandidateCount = finalizedCandidates.Count,
            CandidateCountsByProvenance = countsByProvenance,
            CandidateCountsByEligibility = countsByEligibility,
            Note = itemStackEnabled
                ? "Opt-in post-Alpha enforcement: Experience/TraderStanding plus only a single known-price Success reward-item stack may be reduced. Item templates, reward records and structural quest fields are never added, removed or replaced. Pristine/unknown provenance protection remains mandatory."
                : config.Mode == EconomyMode.Enforce
                    ? "Active Alpha enforcement: only numeric Success Experience and TraderStanding rewards may be changed. PristineUnchanged and unknown provenance remain protected; PristineModified requires the exact reward dimension to be proven changed. Item rewards and structural quest fields remain preview-only/non-mutating."
                    : "Audit preview: deterministic Experience/TraderStanding proposals are emitted but the final DB is not mutated.",
            Candidates = finalizedCandidates,
        };

        var planPath = SafePath(modPath, "reports/economy-admiral-enforcement-plan.json");
        Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);

        if (config.Mode == EconomyMode.Enforce)
        {
            if (transaction.RolledBack)
                logger.Error($"[Economy Admiral] Enforce transaction rolled back/aborted: planned={proposals.Count}, error={transaction.Error}; plan={planPath}");
            else
                logger.Warning($"[Economy Admiral] Enforce committed: planned={proposals.Count}, mutations={transaction.Results.Count}; plan={planPath}");
        }
        else
        {
            logger.Info($"[Economy Admiral] enforcement preview complete: candidates={finalizedCandidates.Count}, planned={proposals.Count}, mutations=0; plan={planPath}");
        }

        return report;
    }

    private IReadOnlyList<NumericRewardTransactionRequest> BuildTransactionRequests(IReadOnlyList<NumericRewardMutation> proposals)
    {
        var requests = new List<NumericRewardTransactionRequest>(proposals.Count);
        foreach (var proposal in proposals)
        {
            if (!templates.Quests.TryGetValue(proposal.QuestId, out var quest))
                throw new InvalidOperationException($"Enforce quest '{proposal.QuestId}' disappeared from final DB.");

            if (proposal.Dimension == "ItemRewardStackCount")
            {
                var items = GetSuccessRewardItems(quest).ToList();
                if (items.Count != 1)
                    throw new InvalidOperationException($"Enforce quest '{proposal.QuestId}' item-stack mutation requires exactly one Success reward item record; actual={items.Count}.");

                var item = items[0];
                if (item.Upd is null)
                    throw new InvalidOperationException($"Enforce quest '{proposal.QuestId}' item-stack mutation requires writable Upd.StackObjectsCount.");

                requests.Add(new NumericRewardTransactionRequest
                {
                    QuestId = proposal.QuestId,
                    Dimension = proposal.Dimension,
                    ExpectedBefore = proposal.Before,
                    Target = proposal.Target,
                    Slots = [new NumericRewardSlot(
                        () => item.Upd?.StackObjectsCount ?? 1d,
                        value =>
                        {
                            if (item.Upd is null) throw new InvalidOperationException("Reward item Upd disappeared during item-stack transaction.");
                            item.Upd.StackObjectsCount = value;
                        })],
                });
                continue;
            }

            var rewards = GetSuccessRewards(quest, proposal.Dimension).ToList();
            if (rewards.Count == 0)
                throw new InvalidOperationException($"Enforce quest '{proposal.QuestId}' has no Success {proposal.Dimension} reward records.");

            requests.Add(new NumericRewardTransactionRequest
            {
                QuestId = proposal.QuestId,
                Dimension = proposal.Dimension,
                ExpectedBefore = proposal.Before,
                Target = proposal.Target,
                Slots = rewards.Select(reward => new NumericRewardSlot(
                    () => reward.Value ?? 0d,
                    value => reward.Value = value)).ToList(),
            });
        }
        return requests;
    }

    private EnforcementPlanCandidate BuildCandidate(
        QuestAnalysisRow row,
        QuestProvenanceDeltaRow? provenance,
        QuestAnalysisReport analysis,
        EconomyConfig config,
        IReadOnlyDictionary<string, double> handbookPrices)
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

        config.QuestRewardOverrides.TryGetValue(row.QuestId, out var manualOverride);
        if (manualOverride?.ExperienceTarget is not null) actions.Add("ManualExperienceTarget");
        if (manualOverride?.TraderStandingTarget is not null) actions.Add("ManualStandingTarget");

        var provenanceClass = provenance?.Provenance ?? "Unknown";
        var changedDimensions = provenance?.ChangedDimensions ?? [];
        var potentialMutationDimensions = ResolvePotentialMutationDimensions(
            provenanceClass,
            changedDimensions,
            actions,
            config.EnableItemRewardStackNormalization);
        var eligibility = ResolveMutationEligibility(provenanceClass, potentialMutationDimensions.Count);
        var manualDenied = manualOverride?.AllowAutomaticMutation == false;

        var mutations = new List<NumericRewardMutation>();
        if (eligibility.PotentiallyEligible && !manualDenied)
        {
            if (potentialMutationDimensions.Contains("Experience", StringComparer.Ordinal))
            {
                var target = ResolveExperienceTarget(row, analysis, manualOverride);
                if (target is { } xpTarget && NumericRewardTransactionCore.NeedsMutation(row.Experience, xpTarget, manualOverride?.ExperienceTarget is not null))
                    mutations.Add(BuildMutation(row, "Experience", row.Experience, xpTarget, manualOverride?.ExperienceTarget is not null));
            }
            if (potentialMutationDimensions.Contains("TraderStanding", StringComparer.Ordinal))
            {
                var target = ResolveStandingTarget(row, analysis, manualOverride);
                if (target is { } standingTarget && NumericRewardTransactionCore.NeedsMutation(row.TraderStanding, standingTarget, manualOverride?.TraderStandingTarget is not null))
                    mutations.Add(BuildMutation(row, "TraderStanding", row.TraderStanding, standingTarget, manualOverride?.TraderStandingTarget is not null));
            }
            if (potentialMutationDimensions.Contains("ItemRewardStackCount", StringComparer.Ordinal))
            {
                var itemMutation = BuildItemStackMutation(row, analysis, handbookPrices);
                if (itemMutation is not null) mutations.Add(itemMutation);
            }
        }

        return new EnforcementPlanCandidate
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            TraderId = row.TraderId,
            Restartable = row.Restartable,
            ProvenanceClass = provenanceClass,
            PristineUntouched = string.Equals(provenanceClass, "PristineUnchanged", StringComparison.Ordinal),
            MutationEligibilityClass = manualDenied ? "ManualOverrideDenied" : eligibility.Class,
            PotentialAutomaticMutationEligible = eligibility.PotentiallyEligible && !manualDenied,
            PotentialMutationDimensions = potentialMutationDimensions,
            MutationEligibilityReason = manualDenied
                ? "Manual quest override explicitly denies automatic mutation."
                : eligibility.Reason,
            ChangedDimensions = changedDimensions,
            ReasonFlags = row.ObservationalFlags.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            ProposedReviewActions = actions.ToList(),
            AutomaticMutationAllowed = mutations.Count > 0,
            ProposedMutations = mutations,
            ProposedMutation = mutations.Count == 0 ? null : mutations,
        };
    }

    private NumericRewardMutation? BuildItemStackMutation(
        QuestAnalysisRow row,
        QuestAnalysisReport analysis,
        IReadOnlyDictionary<string, double> handbookPrices)
    {
        if (!templates.Quests.TryGetValue(row.QuestId, out var quest)) return null;
        var items = GetSuccessRewardItems(quest).ToList();
        if (items.Count != 1) return null;

        var item = items[0];
        var templateId = item.Template.ToString();
        if (string.IsNullOrWhiteSpace(templateId) || !handbookPrices.TryGetValue(templateId, out var unitPrice)) return null;

        var baseline = row.Restartable && analysis.VanillaRestartable.QuestSamples > 0
            ? analysis.VanillaRestartable
            : analysis.Vanilla;
        if (baseline.MedianSuccessHandbookValue <= 0) return null;

        var multiple = row.Restartable && row.ObservationalFlags.Contains("RESTARTABLE_HIGH_ITEM_VALUE", StringComparer.Ordinal)
            ? analysis.Policy.RestartableHighItemValueWarnMultiple
            : analysis.Policy.HighItemValueLowStructureWarnMultiple;
        var budgetCap = baseline.MedianSuccessHandbookValue * multiple;
        var currentCount = item.Upd?.StackObjectsCount ?? 1d;
        var plan = ItemRewardStackPlanner.Plan(currentCount, unitPrice, budgetCap);
        if (!plan.Eligible || plan.TargetCount is null) return null;

        return new NumericRewardMutation
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            Dimension = "ItemRewardStackCount",
            RewardType = "Item",
            PolicyId = "PresetSingleStackItemBudgetCapV1",
            Before = Math.Round(plan.CurrentCount, 0),
            Current = Math.Round(plan.CurrentCount, 0),
            Target = Math.Round(plan.TargetCount.Value, 0),
            After = Math.Round(plan.CurrentCount, 0),
            ManualOverride = false,
            Applied = false,
        };
    }

    private static NumericRewardMutation BuildMutation(
        QuestAnalysisRow row,
        string dimension,
        double before,
        double target,
        bool manual)
    {
        var decimals = dimension == "Experience" ? 0 : 4;
        return new NumericRewardMutation
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            Dimension = dimension,
            RewardType = dimension,
            PolicyId = manual ? "ManualExactQuestRewardTargetV1" : "PresetNumericQuestRewardCapV1",
            Before = Math.Round(before, decimals),
            Current = Math.Round(before, decimals),
            Target = Math.Round(target, decimals),
            After = Math.Round(before, decimals),
            ManualOverride = manual,
            Applied = false,
        };
    }

    private static double? ResolveExperienceTarget(QuestAnalysisRow row, QuestAnalysisReport analysis, ManualQuestRewardOverride? manual)
    {
        if (manual?.ExperienceTarget is { } exact) return Math.Round(exact, 0);
        var baseline = row.Restartable && analysis.VanillaRestartable.QuestSamples > 0 ? analysis.VanillaRestartable : analysis.Vanilla;
        if (baseline.MedianXp <= 0) return null;
        var multiple = row.Restartable && row.ObservationalFlags.Contains("RESTARTABLE_HIGH_XP", StringComparer.Ordinal)
            ? analysis.Policy.RestartableHighXpWarnMultiple
            : analysis.Policy.HighXpLowDepthWarnMultiple;
        return Math.Round(baseline.MedianXp * multiple, 0);
    }

    private static double? ResolveStandingTarget(QuestAnalysisRow row, QuestAnalysisReport analysis, ManualQuestRewardOverride? manual)
    {
        if (manual?.TraderStandingTarget is { } exact) return Math.Round(exact, 4);
        var baseline = row.Restartable && analysis.VanillaRestartable.QuestSamples > 0 ? analysis.VanillaRestartable : analysis.Vanilla;
        if (baseline.MedianAbsoluteStanding <= 0 || row.TraderStanding == 0) return null;
        var magnitude = baseline.MedianAbsoluteStanding * analysis.Policy.HighStandingLowDepthWarnMultiple;
        return Math.Round(Math.CopySign(magnitude, row.TraderStanding), 4);
    }

    private static IEnumerable<Reward> GetSuccessRewards(Quest quest, string dimension)
    {
        if (quest.Rewards is null) yield break;
        var type = dimension == "Experience" ? RewardType.Experience : RewardType.TraderStanding;
        foreach (var pair in quest.Rewards)
        {
            if (!string.Equals(pair.Key, "Success", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var reward in pair.Value)
                if (reward.Type == type) yield return reward;
        }
    }

    private static IEnumerable<Item> GetSuccessRewardItems(Quest quest)
    {
        if (quest.Rewards is null) yield break;
        foreach (var pair in quest.Rewards)
        {
            if (!string.Equals(pair.Key, "Success", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var reward in pair.Value)
            {
                if (reward.Items is null) continue;
                foreach (var item in reward.Items) yield return item;
            }
        }
    }

    private IReadOnlyDictionary<string, double> BuildHandbookPrices() => templates.Handbook.Items
        .Where(item => item.Price is > 0)
        .GroupBy(item => item.Id.ToString(), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Price!.Value, StringComparer.Ordinal);

    private static List<string> ResolvePotentialMutationDimensions(
        string provenanceClass,
        IReadOnlyCollection<string> changedDimensions,
        IReadOnlyCollection<string> reviewActions,
        bool allowItemStackMutation)
    {
        if (string.Equals(provenanceClass, "PristineUnchanged", StringComparison.Ordinal)
            || string.Equals(provenanceClass, "Unknown", StringComparison.Ordinal))
            return [];

        var dimensions = new SortedSet<string>(StringComparer.Ordinal);
        var modAdded = string.Equals(provenanceClass, "ModAdded", StringComparison.Ordinal);

        if ((reviewActions.Contains("ReviewXpRewardBudget", StringComparer.Ordinal) || reviewActions.Contains("ManualExperienceTarget", StringComparer.Ordinal))
            && (modAdded || changedDimensions.Contains("Experience", StringComparer.Ordinal)))
            dimensions.Add("Experience");

        if ((reviewActions.Contains("ReviewStandingRewardBudget", StringComparer.Ordinal) || reviewActions.Contains("ManualStandingTarget", StringComparer.Ordinal))
            && (modAdded || changedDimensions.Contains("TraderStanding", StringComparer.Ordinal)))
            dimensions.Add("TraderStanding");

        if (allowItemStackMutation
            && reviewActions.Contains("ReviewItemRewardBudget", StringComparer.Ordinal)
            && (modAdded || changedDimensions.Contains("SuccessItemHandbookValue", StringComparer.Ordinal)))
            dimensions.Add("ItemRewardStackCount");

        return dimensions.ToList();
    }

    private static MutationEligibility ResolveMutationEligibility(string provenanceClass, int potentialDimensionCount) => provenanceClass switch
    {
        "ModAdded" when potentialDimensionCount > 0 => new MutationEligibility("PolicyEligibleModAdded", true, "Mod-added quest has explicitly flagged/manual reward dimensions. Only the listed dimensions may be changed."),
        "ModAdded" => new MutationEligibility("ReviewOnlyModAdded", false, "Mod-added quest has no eligible enforcement dimension."),
        "PristineModified" when potentialDimensionCount > 0 => new MutationEligibility("PolicyEligibleModifiedPristine", true, "Only reward dimensions both requested by policy/manual override and proven changed versus pristine may be changed."),
        "PristineModified" => new MutationEligibility("ProtectedUnchangedRewardDimensions", false, "Pristine quest changed structurally or in other dimensions, but the requested reward dimension is not proven changed."),
        "PristineUnchanged" => new MutationEligibility("ProtectedPristine", false, "Quest matches pristine startup snapshot and is never automatically mutated."),
        _ => new MutationEligibility("BlockedUnknownProvenance", false, "Quest provenance is not proven; automatic mutation is blocked."),
    };

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Economy Admiral report path must stay inside the mod directory.");
        return path;
    }

    private static string MutationKey(string questId, string dimension) => $"{questId}\u001f{dimension}";
    private sealed record MutationEligibility(string Class, bool PotentiallyEligible, string Reason);
}

public sealed record EnforcementPlanReport
{
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public required string SelectedPolicy { get; init; }
    public required int SourceAnalysisSchemaVersion { get; init; }
    public required int SourceProvenanceSchemaVersion { get; init; }
    public required bool ProvenanceAware { get; init; }
    public required int MutationEligibilityPolicyVersion { get; init; }
    public required bool EnforceRequested { get; init; }
    public required bool ApplyMutations { get; init; }
    public required int PlannedMutationCount { get; init; }
    public required int MutationCount { get; init; }
    public required bool TransactionCommitted { get; init; }
    public required bool TransactionRolledBack { get; init; }
    public string? TransactionError { get; init; }
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
    public required List<NumericRewardMutation> ProposedMutations { get; init; }
    public required object? ProposedMutation { get; init; }
}

public sealed record NumericRewardMutation
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string Dimension { get; init; }
    public required string RewardType { get; init; }
    public required string PolicyId { get; init; }
    public required double Before { get; init; }
    public required double Current { get; init; }
    public required double Target { get; init; }
    public required double After { get; init; }
    public required bool ManualOverride { get; init; }
    public required bool Applied { get; init; }
}

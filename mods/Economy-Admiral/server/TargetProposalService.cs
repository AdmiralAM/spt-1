using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class TargetProposalService(
    ModHelper modHelper,
    ISptLogger<TargetProposalService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task RunAsync(QuestAnalysisReport analysis, CancellationToken cancellationToken)
    {
        var proposals = analysis.Quests
            .Where(row => row.ObservationalFlags.Count > 0)
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => BuildProposal(row, analysis))
            .Where(row => row.Envelopes.Count > 0)
            .ToList();

        var report = new TargetProposalReport
        {
            SchemaVersion = 1,
            ProposalsAreMutations = false,
            ApplyMutations = false,
            SelectedCompositePolicy = null,
            Note = "Deterministic review envelopes only. Ceilings are derived from the same vanilla medians and resolved audit thresholds already used to flag quests. Item ceilings are budget references, not instructions to alter specific reward templates.",
            QuestProposalCount = proposals.Count,
            Proposals = proposals,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(TargetProposalService).Assembly);
        var reportPath = SafePath(modPath, "reports/economy-admiral-target-proposals.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        logger.Info($"[Economy Admiral] target-envelope evaluation complete: {proposals.Count} flagged quests; 0 mutations; report={reportPath}");
    }

    private static QuestTargetProposal BuildProposal(QuestAnalysisRow row, QuestAnalysisReport analysis)
    {
        var baseline = row.Restartable && analysis.VanillaRestartable.QuestSamples > 0
            ? analysis.VanillaRestartable
            : analysis.Vanilla;
        var policy = analysis.Policy;
        var envelopes = new List<TargetEnvelope>();

        if (row.ObservationalFlags.Contains("HIGH_ITEM_VALUE_LOW_STRUCTURE", StringComparer.Ordinal)
            || row.ObservationalFlags.Contains("RESTARTABLE_HIGH_ITEM_VALUE", StringComparer.Ordinal))
        {
            var multiple = row.Restartable
                ? policy.RestartableHighItemValueWarnMultiple
                : policy.HighItemValueLowStructureWarnMultiple;
            AddEnvelope(envelopes, "ItemRewardBudget", row.SuccessKnownHandbookValue, baseline.MedianSuccessHandbookValue, multiple,
                "Known handbook-value budget ceiling; does not select replacement item templates.");
        }

        if (row.ObservationalFlags.Contains("HIGH_XP_LOW_DEPTH", StringComparer.Ordinal)
            || row.ObservationalFlags.Contains("RESTARTABLE_HIGH_XP", StringComparer.Ordinal))
        {
            var multiple = row.Restartable
                ? policy.RestartableHighXpWarnMultiple
                : policy.HighXpLowDepthWarnMultiple;
            AddEnvelope(envelopes, "Experience", row.Experience, baseline.MedianXp, multiple,
                "XP ceiling candidate derived from the applicable vanilla median and resolved warning multiple.");
        }

        if (row.ObservationalFlags.Contains("HIGH_STANDING_LOW_DEPTH", StringComparer.Ordinal))
        {
            AddEnvelope(envelopes, "TraderStandingAbsolute", Math.Abs(row.TraderStanding), baseline.MedianAbsoluteStanding,
                policy.HighStandingLowDepthWarnMultiple,
                "Absolute trader-standing ceiling candidate; sign/direction is intentionally not mutated.");
        }

        return new QuestTargetProposal
        {
            QuestId = row.QuestId,
            QuestName = row.QuestName,
            TraderId = row.TraderId,
            Restartable = row.Restartable,
            SourceFlags = row.ObservationalFlags.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            Envelopes = envelopes.OrderBy(value => value.Dimension, StringComparer.Ordinal).ToList(),
            AutomaticMutationAllowed = false,
            ProposedMutation = null,
        };
    }

    private static void AddEnvelope(List<TargetEnvelope> target, string dimension, double currentValue, double vanillaMedian,
        double thresholdMultiple, string interpretation)
    {
        if (currentValue <= 0 || vanillaMedian <= 0 || thresholdMultiple <= 0)
        {
            return;
        }

        target.Add(new TargetEnvelope
        {
            Dimension = dimension,
            CurrentValue = Math.Round(currentValue, 4),
            ReferenceVanillaMedian = Math.Round(vanillaMedian, 4),
            ThresholdMultiple = Math.Round(thresholdMultiple, 4),
            CandidateCeiling = Math.Round(vanillaMedian * thresholdMultiple, 4),
            Interpretation = interpretation,
        });
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
}

public sealed record TargetProposalReport
{
    public required int SchemaVersion { get; init; }
    public required bool ProposalsAreMutations { get; init; }
    public required bool ApplyMutations { get; init; }
    public required string? SelectedCompositePolicy { get; init; }
    public required string Note { get; init; }
    public required int QuestProposalCount { get; init; }
    public required List<QuestTargetProposal> Proposals { get; init; }
}

public sealed record QuestTargetProposal
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required bool Restartable { get; init; }
    public required List<string> SourceFlags { get; init; }
    public required List<TargetEnvelope> Envelopes { get; init; }
    public required bool AutomaticMutationAllowed { get; init; }
    public required object? ProposedMutation { get; init; }
}

public sealed record TargetEnvelope
{
    public required string Dimension { get; init; }
    public required double CurrentValue { get; init; }
    public required double ReferenceVanillaMedian { get; init; }
    public required double ThresholdMultiple { get; init; }
    public required double CandidateCeiling { get; init; }
    public required string Interpretation { get; init; }
}
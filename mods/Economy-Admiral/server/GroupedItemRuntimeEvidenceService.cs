using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class GroupedItemRuntimeEvidenceService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task WriteAsync(EnforcementPlanReport enforcement, CancellationToken cancellationToken)
    {
        var labels = GroupedItemRewardSlot.SnapshotPlannedGroupedLabels()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        var appliedItemStacks = enforcement.Candidates
            .SelectMany(candidate => candidate.ProposedMutations)
            .Count(mutation => mutation.Applied && mutation.Dimension == "ItemRewardStackCount");

        var report = new GroupedItemRuntimeEvidence
        {
            TransactionCommitted = enforcement.TransactionCommitted,
            TotalAppliedItemStacks = appliedItemStacks,
            GroupedPlannedCount = labels.Count,
            GroupedAppliedCount = enforcement.TransactionCommitted ? labels.Count : 0,
            GroupedLabels = labels,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(GroupedItemRuntimeEvidenceService).Assembly);
        var path = Path.Combine(modPath, "reports", "economy-admiral-grouped-item-evidence.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
    }
}

public sealed record GroupedItemRuntimeEvidence
{
    public required bool TransactionCommitted { get; init; }
    public required int TotalAppliedItemStacks { get; init; }
    public required int GroupedPlannedCount { get; init; }
    public required int GroupedAppliedCount { get; init; }
    public required List<string> GroupedLabels { get; init; }
}

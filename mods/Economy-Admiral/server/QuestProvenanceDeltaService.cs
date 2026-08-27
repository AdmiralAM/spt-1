using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class QuestProvenanceDeltaService(
    ModHelper modHelper,
    ISptLogger<QuestProvenanceDeltaService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<QuestProvenanceDeltaReport> RunAsync(
        VanillaBaselineSnapshot baseline,
        QuestAnalysisReport analysis,
        CancellationToken cancellationToken
    )
    {
        var pristine = baseline.Quests.ToDictionary(row => row.QuestId, StringComparer.Ordinal);
        var finalIds = analysis.Quests.Select(row => row.QuestId).ToHashSet(StringComparer.Ordinal);
        var rows = new List<QuestProvenanceDeltaRow>();

        foreach (var final in analysis.Quests.OrderBy(row => row.QuestId, StringComparer.Ordinal))
        {
            if (!pristine.TryGetValue(final.QuestId, out var original))
            {
                rows.Add(new QuestProvenanceDeltaRow
                {
                    QuestId = final.QuestId,
                    QuestName = final.QuestName,
                    TraderId = final.TraderId,
                    Provenance = "ModAdded",
                    ChangedDimensions = ["QuestAdded"],
                    ObservationalFlagCount = final.ObservationalFlags.Count,
                    ObservationalFlags = final.ObservationalFlags,
                });
                continue;
            }

            var changes = Compare(original, final);
            rows.Add(new QuestProvenanceDeltaRow
            {
                QuestId = final.QuestId,
                QuestName = final.QuestName,
                TraderId = final.TraderId,
                Provenance = changes.Count == 0 ? "PristineUnchanged" : "PristineModified",
                ChangedDimensions = changes,
                ObservationalFlagCount = final.ObservationalFlags.Count,
                ObservationalFlags = final.ObservationalFlags,
            });
        }

        var removed = baseline.Quests
            .Where(row => !finalIds.Contains(row.QuestId))
            .OrderBy(row => row.QuestId, StringComparer.Ordinal)
            .Select(row => row.QuestId)
            .ToList();

        var traderGroups = rows
            .GroupBy(row => row.TraderId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new QuestProvenanceTraderSummary
            {
                TraderId = group.Key,
                FinalQuestCount = group.Count(),
                ModAddedQuestCount = group.Count(row => row.Provenance == "ModAdded"),
                PristineModifiedQuestCount = group.Count(row => row.Provenance == "PristineModified"),
                PristineUnchangedQuestCount = group.Count(row => row.Provenance == "PristineUnchanged"),
                FlaggedQuestCount = group.Count(row => row.ObservationalFlagCount > 0),
                ObservationalFlagCount = group.Sum(row => row.ObservationalFlagCount),
            })
            .ToList();

        var report = new QuestProvenanceDeltaReport
        {
            SchemaVersion = 1,
            BaselineCapturePriority = baseline.CapturePriority,
            PristineQuestCount = baseline.QuestCount,
            FinalQuestCount = analysis.Quests.Count,
            ModAddedQuestCount = rows.Count(row => row.Provenance == "ModAdded"),
            PristineModifiedQuestCount = rows.Count(row => row.Provenance == "PristineModified"),
            PristineUnchangedQuestCount = rows.Count(row => row.Provenance == "PristineUnchanged"),
            RemovedPristineQuestCount = removed.Count,
            RemovedPristineQuestIds = removed,
            EnforcementAffected = false,
            Note = "Quest provenance is derived from the priority-1 pristine startup snapshot versus the final PostLoad database. This report is observational only and does not authorize mutations.",
            Traders = traderGroups,
            Quests = rows,
        };

        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(QuestProvenanceDeltaService).Assembly);
        var reportPath = SafePath(modPath, "reports/economy-admiral-provenance-delta.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);

        logger.Info(
            $"[Economy Admiral] provenance delta complete: pristine={report.PristineQuestCount}, final={report.FinalQuestCount}, " +
            $"added={report.ModAddedQuestCount}, modified={report.PristineModifiedQuestCount}, unchanged={report.PristineUnchangedQuestCount}, removed={report.RemovedPristineQuestCount}; report={reportPath}"
        );
        return report;
    }

    private static List<string> Compare(VanillaQuestBaselineRow original, QuestAnalysisRow final)
    {
        var changes = new List<string>();
        if (original.Restartable != final.Restartable) changes.Add("Restartable");
        if (Different(original.SuccessKnownHandbookValue, final.SuccessKnownHandbookValue, 0.01)) changes.Add("SuccessItemHandbookValue");
        if (Different(original.Experience, final.Experience, 0.01)) changes.Add("Experience");
        if (Different(original.TraderStanding, final.TraderStanding, 0.0001)) changes.Add("TraderStanding");
        if (original.TraderUnlocks != final.TraderUnlocks) changes.Add("TraderUnlocks");
        if (original.AssortmentUnlocks != final.AssortmentUnlocks) changes.Add("AssortmentUnlocks");
        if (original.ProductionSchemeUnlocks != final.ProductionSchemeUnlocks) changes.Add("ProductionSchemeUnlocks");
        if (original.ObjectiveConditionCount != final.ObjectiveConditionCount) changes.Add("ObjectiveConditionCount");
        if (original.DirectPrerequisiteCount != final.DirectPrerequisiteCount) changes.Add("DirectPrerequisiteCount");
        if (original.MaximumPrerequisiteDepth != final.MaximumPrerequisiteDepth) changes.Add("MaximumPrerequisiteDepth");
        if (original.IsPrerequisiteCycleMember != final.IsPrerequisiteCycleMember) changes.Add("PrerequisiteCycleMembership");
        if (original.StructuredConstraintCount != final.StructuredConstraintCount) changes.Add("StructuredConstraintCount");
        return changes;
    }

    private static bool Different(double left, double right, double tolerance) => Math.Abs(left - right) > tolerance;

    private static string SafePath(string modPath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Economy Admiral provenance delta path must stay inside the mod directory.");
        return fullPath;
    }
}

public sealed record QuestProvenanceDeltaReport
{
    public required int SchemaVersion { get; init; }
    public required int BaselineCapturePriority { get; init; }
    public required int PristineQuestCount { get; init; }
    public required int FinalQuestCount { get; init; }
    public required int ModAddedQuestCount { get; init; }
    public required int PristineModifiedQuestCount { get; init; }
    public required int PristineUnchangedQuestCount { get; init; }
    public required int RemovedPristineQuestCount { get; init; }
    public required List<string> RemovedPristineQuestIds { get; init; }
    public required bool EnforcementAffected { get; init; }
    public required string Note { get; init; }
    public required List<QuestProvenanceTraderSummary> Traders { get; init; }
    public required List<QuestProvenanceDeltaRow> Quests { get; init; }
}

public sealed record QuestProvenanceTraderSummary
{
    public required string TraderId { get; init; }
    public required int FinalQuestCount { get; init; }
    public required int ModAddedQuestCount { get; init; }
    public required int PristineModifiedQuestCount { get; init; }
    public required int PristineUnchangedQuestCount { get; init; }
    public required int FlaggedQuestCount { get; init; }
    public required int ObservationalFlagCount { get; init; }
}

public sealed record QuestProvenanceDeltaRow
{
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required string TraderId { get; init; }
    public required string Provenance { get; init; }
    public required List<string> ChangedDimensions { get; init; }
    public required int ObservationalFlagCount { get; init; }
    public required List<string> ObservationalFlags { get; init; }
}

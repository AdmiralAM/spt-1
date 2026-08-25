using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyAuditService(
    TemplateTable templates,
    TradersTable traders,
    ModHelper modHelper,
    ISptLogger<EconomyAuditService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(EconomyAuditService).Assembly);
        var config = await LoadConfigAsync(modPath, cancellationToken);

        if (config.Mode == EconomyMode.Off)
        {
            logger.Info("[SPT Economy] mode=Off; final DB audit skipped");
            return;
        }

        if (config.Mode == EconomyMode.Enforce)
        {
            logger.Warning("[SPT Economy] mode=Enforce requested, but enforcement is not implemented in this slice; running read-only audit only");
        }

        var acquisitions = new Dictionary<string, MutableAcquisition>(StringComparer.Ordinal);
        ScanTraderAcquisition(acquisitions);
        ScanQuestRewards(acquisitions);

        var items = acquisitions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => FinalizeItem(pair.Key, pair.Value, config))
            .Where(item => !item.Ignored)
            .ToList();

        var report = new EconomyAuditReport
        {
            SchemaVersion = 1,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            EnforcementApplied = false,
            RepeatedRaidLootDecay = config.RepeatedRaidLootDecay,
            Database = new DatabaseSummary
            {
                TemplateItems = templates.Items.Count,
                Quests = templates.Quests.Count,
                Traders = traders.Count,
                TraderAssortRecords = traders.Values.Sum(trader => trader.Assort.Items.Count),
            },
            Acquisition = new AcquisitionSummary
            {
                ItemsWithKnownAcquisition = items.Count,
                TraderSourceEdges = items.Sum(item => item.TraderSources.Count),
                QuestRewardSourceEdges = items.Sum(item => item.QuestRewardSources.Count),
            },
            Items = items,
        };

        var reportPath = Path.GetFullPath(Path.Combine(modPath, config.ReportRelativePath));
        var modRoot = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!reportPath.StartsWith(modRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SPT Economy report path must stay inside the mod directory.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);

        logger.Info($"[SPT Economy] final DB audit complete: {report.Database.TemplateItems} templates, {report.Database.Traders} traders, {report.Database.Quests} quests, {items.Count} items with trader/quest acquisition; report={reportPath}");
    }

    private void ScanTraderAcquisition(Dictionary<string, MutableAcquisition> acquisitions)
    {
        foreach (var traderPair in traders.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var traderId = traderPair.Key.ToString();
            var assort = traderPair.Value.Assort;

            foreach (var item in assort.Items.Where(item => string.Equals(item.ParentId, "hideout", StringComparison.OrdinalIgnoreCase)))
            {
                var templateId = item.Template.ToString();
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    continue;
                }

                GetOrCreate(acquisitions, templateId).TraderSources.Add(traderId);
            }
        }
    }

    private void ScanQuestRewards(Dictionary<string, MutableAcquisition> acquisitions)
    {
        foreach (var questPair in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            if (questPair.Value.Rewards is null)
            {
                continue;
            }

            var questId = questPair.Key.ToString();
            var rewards = JsonSerializer.SerializeToElement(questPair.Value.Rewards);
            foreach (var templateId in FindTemplateIds(rewards))
            {
                GetOrCreate(acquisitions, templateId).QuestRewardSources.Add(questId);
            }
        }
    }

    private static IEnumerable<string> FindTemplateIds(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("_tpl") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            yield return value;
                        }
                    }

                    foreach (var nested in FindTemplateIds(property.Value))
                    {
                        yield return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in FindTemplateIds(item))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }

    private static MutableAcquisition GetOrCreate(Dictionary<string, MutableAcquisition> acquisitions, string templateId)
    {
        if (!acquisitions.TryGetValue(templateId, out var acquisition))
        {
            acquisition = new MutableAcquisition();
            acquisitions.Add(templateId, acquisition);
        }

        return acquisition;
    }

    private static ItemAcquisitionReport FinalizeItem(string templateId, MutableAcquisition acquisition, EconomyConfig config)
    {
        config.ManualOverrides.TryGetValue(templateId, out var manualOverride);
        var sourceCount = acquisition.TraderSources.Count + acquisition.QuestRewardSources.Count;

        return new ItemAcquisitionReport
        {
            TemplateId = templateId,
            Rarity = manualOverride?.Rarity ?? ClassifyRarity(sourceCount, config.Rarity),
            Ignored = manualOverride?.Ignore ?? false,
            OverrideNote = manualOverride?.Note,
            TraderSources = acquisition.TraderSources.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            QuestRewardSources = acquisition.QuestRewardSources.OrderBy(value => value, StringComparer.Ordinal).ToList(),
        };
    }

    private static string ClassifyRarity(int sourceCount, RarityThresholds thresholds)
    {
        if (sourceCount >= thresholds.CommonMinSources)
        {
            return "Common";
        }

        if (sourceCount >= thresholds.UncommonMinSources)
        {
            return "Uncommon";
        }

        if (sourceCount >= thresholds.RareMinSources)
        {
            return "Rare";
        }

        return "Exceptional";
    }

    private static async Task<EconomyConfig> LoadConfigAsync(string modPath, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(modPath, "config", "config.json");
        if (!File.Exists(configPath))
        {
            return new EconomyConfig();
        }

        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<EconomyConfig>(stream, JsonOptions, cancellationToken)
            ?? new EconomyConfig();
    }

    private sealed class MutableAcquisition
    {
        public HashSet<string> TraderSources { get; } = new(StringComparer.Ordinal);
        public HashSet<string> QuestRewardSources { get; } = new(StringComparer.Ordinal);
    }
}

public sealed record EconomyAuditReport
{
    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public required bool EnforcementApplied { get; init; }
    public required bool RepeatedRaidLootDecay { get; init; }
    public required DatabaseSummary Database { get; init; }
    public required AcquisitionSummary Acquisition { get; init; }
    public required List<ItemAcquisitionReport> Items { get; init; }
}

public sealed record DatabaseSummary
{
    public required int TemplateItems { get; init; }
    public required int Quests { get; init; }
    public required int Traders { get; init; }
    public required int TraderAssortRecords { get; init; }
}

public sealed record AcquisitionSummary
{
    public required int ItemsWithKnownAcquisition { get; init; }
    public required int TraderSourceEdges { get; init; }
    public required int QuestRewardSourceEdges { get; init; }
}

public sealed record ItemAcquisitionReport
{
    public required string TemplateId { get; init; }
    public required string Rarity { get; init; }
    public required bool Ignored { get; init; }
    public string? OverrideNote { get; init; }
    public required List<string> TraderSources { get; init; }
    public required List<string> QuestRewardSources { get; init; }
}

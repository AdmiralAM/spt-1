using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

public sealed record AdmiralTraderRuntimeAdapterReport
{
    public int SchemaVersion { get; init; } = 1;
    public required bool Installed { get; init; }
    public required string ModGuid { get; init; }
    public required string AttributionConfidence { get; init; }
    public int OfferCount { get; init; }
    public int BoundedRenewableOfferCount { get; init; }
    public int? MinimumEffectiveProgressionLevel { get; init; }
    public int? MaximumEffectiveProgressionLevel { get; init; }
    public required IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Offers { get; init; }
}

[Injectable]
public sealed class AdmiralTraderRuntimeAdapterService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<AdmiralTraderRuntimeAdapterReport> RunAsync(
        EconomyConfig config,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        var economyModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var traderModPath = AdmiralTraderInstallationLocator.LocateFromEconomyAdmiralModPath(economyModPath);

        AdmiralTraderRuntimeAdapterReport report;
        if (traderModPath is null)
        {
            report = new AdmiralTraderRuntimeAdapterReport
            {
                Installed = false,
                ModGuid = AdmiralTraderInstallationLocator.ExpectedModGuid,
                AttributionConfidence = AdmiralTraderAdapterEvidence.AttributionConfidence,
                Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
            };
        }
        else
        {
            var gameplayPolicyPath = RequireFile(traderModPath, "manifests", "gameplay-policy.json");
            var assortPath = RequireFile(traderModPath, "db", "assort.json");
            var questAssortPath = RequireFile(traderModPath, "db", "questassort.json");
            var questsPath = Path.Combine(traderModPath, "db", "quests");
            if (!Directory.Exists(questsPath))
            {
                throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: db/quests directory is missing.");
            }

            var policy = AdmiralTraderAdapterEvidence.ParseGameplayPolicy(await File.ReadAllTextAsync(gameplayPolicyPath, cancellationToken));
            var assortJson = await File.ReadAllTextAsync(assortPath, cancellationToken);
            var questAssortJson = await File.ReadAllTextAsync(questAssortPath, cancellationToken);
            var questFiles = Directory.EnumerateFiles(questsPath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (questFiles.Length == 0)
            {
                throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: no authored quest JSON records were found.");
            }

            var authoredQuestJson = new List<string>(questFiles.Length);
            foreach (var path in questFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                authoredQuestJson.Add(await File.ReadAllTextAsync(path, cancellationToken));
            }

            var offers = AdmiralTraderItemAdapter.ParseAndApplyEffectiveQuestGates(
                assortJson,
                questAssortJson,
                policy,
                authoredQuestJson);

            if (offers.Any(offer => offer.EffectiveGate is null || offer.Source.EarliestProgressionLevel is null))
            {
                throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: enriched offer evidence is incomplete.");
            }

            report = new AdmiralTraderRuntimeAdapterReport
            {
                Installed = true,
                ModGuid = AdmiralTraderInstallationLocator.ExpectedModGuid,
                AttributionConfidence = AdmiralTraderAdapterEvidence.AttributionConfidence,
                OfferCount = offers.Count,
                BoundedRenewableOfferCount = offers.Count(offer => offer.Capacity.SupplyBound == RenewableSupplyBound.Bounded),
                MinimumEffectiveProgressionLevel = offers.Min(offer => offer.Source.EarliestProgressionLevel),
                MaximumEffectiveProgressionLevel = offers.Max(offer => offer.Source.EarliestProgressionLevel),
                Offers = offers,
            };
        }

        var reportDirectory = Path.GetDirectoryName(Path.Combine(economyModPath, config.ReportRelativePath))
            ?? Path.Combine(economyModPath, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "economy-admiral-admiral-trader-adapter.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        return report;
    }

    private static string RequireFile(string root, params string[] relativeParts)
    {
        var path = relativeParts.Aggregate(root, Path.Combine);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader runtime adapter: required file is missing: {string.Join('/', relativeParts)}");
        }
        return path;
    }
}

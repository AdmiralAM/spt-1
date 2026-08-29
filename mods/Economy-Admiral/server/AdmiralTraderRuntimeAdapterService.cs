using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

namespace SPTEconomy;

[Injectable]
public sealed class AdmiralTraderRuntimeAdapterService(ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<AdmiralTraderRuntimeAdapterReport> RunAsync(EconomyConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();
        var economyModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var traderModPath = AdmiralTraderInstallationLocator.LocateFromEconomyAdmiralModPath(economyModPath);
        AdmiralTraderRuntimeAdapterReport report;

        if (traderModPath is null)
        {
            report = Empty(false, "NotInstalled", null);
        }
        else
        {
            var gameplayPolicyPath = Path.Combine(traderModPath, "manifests", "gameplay-policy.json");
            if (!File.Exists(gameplayPolicyPath))
            {
                report = Empty(true, "ContractUnavailable", "Admiral Trader is installed but manifests/gameplay-policy.json is absent; explicit evidence is suppressed until a supported machine-readable contract is present.");
            }
            else
            {
                try
                {
                    report = await LoadSupportedContractAsync(traderModPath, gameplayPolicyPath, cancellationToken);
                }
                catch (Exception exception) when (exception is InvalidOperationException or JsonException)
                {
                    report = Empty(true, "ContractUnsupported", exception.Message);
                }
            }
        }

        var reportDirectory = Path.GetDirectoryName(Path.Combine(economyModPath, config.ReportRelativePath)) ?? Path.Combine(economyModPath, "reports");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "economy-admiral-admiral-trader-adapter.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        return report;
    }

    private static async Task<AdmiralTraderRuntimeAdapterReport> LoadSupportedContractAsync(
        string traderModPath,
        string gameplayPolicyPath,
        CancellationToken cancellationToken)
    {
        var policyJson = await File.ReadAllTextAsync(gameplayPolicyPath, cancellationToken);
        using var policyDoc = JsonDocument.Parse(policyJson);
        if (!policyDoc.RootElement.TryGetProperty("schemaVersion", out var schemaElement) || !schemaElement.TryGetInt32(out var schemaVersion))
            throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: gameplay-policy schemaVersion is missing or invalid.");

        var assortJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "db", "assort.json"), cancellationToken);
        var questAssortJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "db", "questassort.json"), cancellationToken);
        var authoredQuestJson = await ReadQuestRecordsAsync(traderModPath, cancellationToken);
        IReadOnlyList<AdmiralTraderOfferAdapterEvidence> offers;
        string contractState;
        AdmiralTraderGameplayAlphaContractSummary? gameplay = null;

        if (schemaVersion == 4)
        {
            var campaignJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "manifests", "campaign-manifest.json"), cancellationToken);
            var identityJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "manifests", "identity-assets.json"), cancellationToken);
            var baseJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "db", "base.json"), cancellationToken);
            var baselineJson = await File.ReadAllTextAsync(RequireFile(traderModPath, "manifests", "baseline-stock.json"), cancellationToken);
            gameplay = AdmiralTraderGameplayAlphaAdapter.Parse(
                campaignJson,
                identityJson,
                baseJson,
                policyJson,
                baselineJson,
                assortJson,
                questAssortJson,
                authoredQuestJson);
            offers = gameplay.Offers;
            contractState = "LoadedGameplayAlphaV4";
        }
        else if (schemaVersion == 3)
        {
            var policy = AdmiralTraderAdapterEvidence.ParseGameplayPolicy(policyJson);
            offers = AdmiralTraderItemAdapter.ParseAndApplyEffectiveQuestGates(assortJson, questAssortJson, policy, authoredQuestJson);
            contractState = "LoadedPrototypeV3";
        }
        else
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader runtime adapter: unsupported gameplay-policy schemaVersion {schemaVersion}.");
        }

        if (offers.Any(offer => offer.Source.EarliestProgressionLevel is null))
            throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: enriched offer progression evidence is incomplete.");
        if (offers.Any(offer => string.Equals(offer.GateKind, "Quest", StringComparison.Ordinal) && offer.EffectiveGate is null))
            throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: quest-gated offer is missing effective gate evidence.");
        if (offers.Any(offer => !string.Equals(offer.Source.ProvenanceClass, AdmiralTraderAdapterEvidence.AttributionConfidence, StringComparison.Ordinal)))
            throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: explicit adapter provenance drifted.");

        return new AdmiralTraderRuntimeAdapterReport
        {
            Installed = true,
            ContractAvailable = true,
            ContractState = contractState,
            ProductName = gameplay?.ProductName ?? "Admiral Trader (legacy prototype)",
            ModGuid = gameplay?.ModGuid ?? AdmiralTraderInstallationLocator.ExpectedModGuid,
            TraderId = gameplay?.TraderId ?? AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId,
            GameplayPolicySchemaVersion = schemaVersion,
            AttributionConfidence = AdmiralTraderAdapterEvidence.AttributionConfidence,
            OfferCount = offers.Count,
            BaselineOfferCount = gameplay?.BaselineOfferCount ?? 0,
            RelationshipOfferCount = gameplay?.RelationshipOfferCount ?? 0,
            MilestoneOfferCount = gameplay?.MilestoneOfferCount ?? offers.Count,
            BoundedRenewableOfferCount = offers.Count(o => o.Capacity.SupplyBound == RenewableSupplyBound.Bounded),
            RelationshipStockAllowed = gameplay?.RelationshipStockAllowed ?? false,
            SpecialWeaponsPermanentOfferAllowed = gameplay?.SpecialWeaponsPermanentOfferAllowed ?? false,
            SpecialWeaponsSampleOnly = gameplay?.SpecialWeaponsSampleOnly ?? false,
            MinimumEffectiveProgressionLevel = offers.Min(o => o.Source.EarliestProgressionLevel),
            MaximumEffectiveProgressionLevel = offers.Max(o => o.Source.EarliestProgressionLevel),
            Offers = offers,
        };
    }

    private static AdmiralTraderRuntimeAdapterReport Empty(bool installed, string state, string? diagnostic) => new()
    {
        Installed = installed,
        ContractAvailable = false,
        ContractState = state,
        ContractDiagnostic = diagnostic,
        ProductName = AdmiralTraderGameplayAlphaAdapter.ExpectedProductName,
        ModGuid = AdmiralTraderInstallationLocator.ExpectedModGuid,
        TraderId = AdmiralTraderGameplayAlphaAdapter.ExpectedTraderId,
        AttributionConfidence = AdmiralTraderAdapterEvidence.AttributionConfidence,
        Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
    };

    private static async Task<IReadOnlyList<string>> ReadQuestRecordsAsync(string traderModPath, CancellationToken cancellationToken)
    {
        var questsPath = Path.Combine(traderModPath, "db", "quests");
        if (!Directory.Exists(questsPath)) throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: db/quests directory is missing.");
        var questFiles = Directory.EnumerateFiles(questsPath, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (questFiles.Length == 0) throw new InvalidOperationException("Economy Admiral Admiral Trader runtime adapter: no authored quest JSON records were found.");
        var records = new List<string>(questFiles.Length);
        foreach (var path in questFiles) records.Add(await File.ReadAllTextAsync(path, cancellationToken));
        return records;
    }

    private static string RequireFile(string root, params string[] relativeParts)
    {
        var path = relativeParts.Aggregate(root, Path.Combine);
        if (!File.Exists(path)) throw new InvalidOperationException($"Economy Admiral Admiral Trader runtime adapter: required file is missing: {string.Join('/', relativeParts)}");
        return path;
    }
}

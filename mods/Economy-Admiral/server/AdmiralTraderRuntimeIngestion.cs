namespace SPTEconomy;

public enum AdmiralTraderRuntimeIngestionState
{
    NotInstalled,
    Loaded,
}

public sealed record AdmiralTraderRuntimeIngestionSnapshot
{
    public required AdmiralTraderRuntimeIngestionState State { get; init; }
    public string? InstallationPath { get; init; }
    public AdmiralTraderAdapterContract? Contract { get; init; }
    public required IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Offers { get; init; }
}

public static class AdmiralTraderRuntimeIngestion
{
    public static AdmiralTraderRuntimeIngestionSnapshot LoadFromEconomyAdmiralModPath(string economyAdmiralModPath)
    {
        var installationPath = AdmiralTraderInstallationLocator.LocateFromEconomyAdmiralModPath(economyAdmiralModPath);
        if (installationPath is null)
        {
            return new AdmiralTraderRuntimeIngestionSnapshot
            {
                State = AdmiralTraderRuntimeIngestionState.NotInstalled,
                Offers = Array.Empty<AdmiralTraderOfferAdapterEvidence>(),
            };
        }

        return LoadFromAdmiralTraderPath(installationPath);
    }

    public static AdmiralTraderRuntimeIngestionSnapshot LoadFromAdmiralTraderPath(string installationPath)
    {
        if (string.IsNullOrWhiteSpace(installationPath))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: installation path must not be empty.");
        }

        var root = Path.GetFullPath(installationPath);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: installation path does not exist.");
        }

        var gameplayPolicyPath = RequireFile(root, "manifests", "gameplay-policy.json");
        var assortPath = RequireFile(root, "db", "assort.json");
        var questAssortPath = RequireFile(root, "db", "questassort.json");
        var questsPath = Path.Combine(root, "db", "quests");
        if (!Directory.Exists(questsPath))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: required db/quests directory is missing.");
        }

        var questFiles = Directory
            .EnumerateFiles(questsPath, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(questsPath, path), StringComparer.Ordinal)
            .ToList();
        if (questFiles.Count == 0)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: no authored quest JSON records were found.");
        }

        var contract = AdmiralTraderAdapterEvidence.ParseGameplayPolicy(File.ReadAllText(gameplayPolicyPath));
        var offers = AdmiralTraderItemAdapter.ParseAndApplyEffectiveQuestGates(
            File.ReadAllText(assortPath),
            File.ReadAllText(questAssortPath),
            contract,
            questFiles.Select(File.ReadAllText)
        );

        if (offers.Count != contract.ExpectedPermanentOfferCount)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: parsed offer count does not match maintained contract.");
        }
        if (offers.Any(offer => offer.EffectiveGate is null || !offer.EffectiveGate.CompleteQuestGraphEvidence))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader ingestion: effective quest-gate evidence is incomplete.");
        }

        return new AdmiralTraderRuntimeIngestionSnapshot
        {
            State = AdmiralTraderRuntimeIngestionState.Loaded,
            InstallationPath = root,
            Contract = contract,
            Offers = offers,
        };
    }

    private static string RequireFile(string root, params string[] relativeParts)
    {
        var path = relativeParts.Aggregate(root, Path.Combine);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Economy Admiral Admiral Trader ingestion: required file '{Path.GetRelativePath(root, path)}' is missing."
            );
        }
        return path;
    }
}

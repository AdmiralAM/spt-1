using System.Runtime.CompilerServices;
using System.Text.Json;
using SPTEconomy;

internal static class AdmiralTraderRuntimeIngestionSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"economy-admiral-ingestion-{Guid.NewGuid():N}");
        var economyPath = Path.Combine(root, "user", "mods", "Economy-Admiral");
        var traderPath = Path.Combine(root, "user", "mods", "Admiral-Trader");

        try
        {
            Directory.CreateDirectory(economyPath);
            var absent = AdmiralTraderRuntimeIngestion.LoadFromEconomyAdmiralModPath(economyPath);
            Require(absent.State == AdmiralTraderRuntimeIngestionState.NotInstalled, "missing Admiral Trader must be an explicit NotInstalled state");
            Require(absent.Offers.Count == 0, "NotInstalled state must not fabricate offers");

            WriteFixture(traderPath);
            var loaded = AdmiralTraderRuntimeIngestion.LoadFromEconomyAdmiralModPath(economyPath);
            Require(loaded.State == AdmiralTraderRuntimeIngestionState.Loaded, "maintained fixture should load");
            Require(loaded.Contract is not null, "loaded snapshot requires validated contract");
            Require(loaded.Offers.Count == 7, "maintained fixture should expose seven offers");
            Require(loaded.Offers.All(offer => offer.EffectiveGate?.CompleteQuestGraphEvidence == true), "all loaded offers require complete effective quest gates");
            Require(loaded.Offers.Select(offer => offer.Source.EarliestProgressionLevel).SequenceEqual(Enumerable.Range(5, 7).Select(value => (int?)value)), "effective progression levels must come from authored quest evidence");

            File.Delete(Path.Combine(traderPath, "db", "questassort.json"));
            MustFail("missing required file", () => AdmiralTraderRuntimeIngestion.LoadFromEconomyAdmiralModPath(economyPath));

            WriteFixture(traderPath);
            var duplicatePath = Path.Combine(root, "user", "mods", "Admiral-Trader-Duplicate");
            Directory.CreateDirectory(Path.Combine(duplicatePath, "manifests"));
            File.WriteAllText(Path.Combine(duplicatePath, "manifests", "campaign-manifest.json"), CampaignManifestJson());
            MustFail("duplicate modGuid", () => AdmiralTraderRuntimeIngestion.LoadFromEconomyAdmiralModPath(economyPath));

            Console.WriteLine("Economy Admiral Admiral Trader runtime ingestion smoke PASS");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void WriteFixture(string traderPath)
    {
        if (Directory.Exists(traderPath)) Directory.Delete(traderPath, true);
        Directory.CreateDirectory(Path.Combine(traderPath, "manifests"));
        Directory.CreateDirectory(Path.Combine(traderPath, "db", "quests"));

        File.WriteAllText(Path.Combine(traderPath, "manifests", "campaign-manifest.json"), CampaignManifestJson());
        File.WriteAllText(Path.Combine(traderPath, "manifests", "gameplay-policy.json"), GameplayPolicyJson());

        var offerIds = Enumerable.Range(1, 7).Select(i => $"offer{i}").ToArray();
        var questIds = Enumerable.Range(1, 7).Select(i => $"quest{i}").ToArray();

        var assort = new
        {
            items = offerIds.Select((id, index) => new
            {
                _id = id,
                _tpl = $"tpl{index + 1}",
                upd = new { UnlimitedCount = false, StackObjectsCount = 10, BuyRestrictionMax = 10 },
            }),
            loyal_level_items = offerIds.ToDictionary(id => id, _ => 1),
        };
        File.WriteAllText(Path.Combine(traderPath, "db", "assort.json"), JsonSerializer.Serialize(assort));

        var questAssort = new
        {
            Started = new Dictionary<string, string>(),
            Success = offerIds.Select((offer, index) => (offer, quest: questIds[index])).ToDictionary(pair => pair.offer, pair => pair.quest),
            Fail = new Dictionary<string, string>(),
        };
        File.WriteAllText(Path.Combine(traderPath, "db", "questassort.json"), JsonSerializer.Serialize(questAssort));

        for (var i = 0; i < questIds.Length; i++)
        {
            var quest = new
            {
                _id = questIds[i],
                conditions = new
                {
                    AvailableForStart = new object[]
                    {
                        new { conditionType = "Level", value = i + 5 },
                    },
                },
            };
            File.WriteAllText(Path.Combine(traderPath, "db", "quests", $"{questIds[i]}.json"), JsonSerializer.Serialize(quest));
        }
    }

    private static string CampaignManifestJson() => """
    {"product":{"modGuid":"com.admiralam.spt.admiraltrader"}}
    """;

    private static string GameplayPolicyJson() => """
    {
      "schemaVersion": 3,
      "productRole": "capability-broker",
      "logistics": {
        "expectedPermanentOfferCount": 7,
        "expectedAmmoPermanentOfferCount": 6,
        "maximumPermanentOfferStockPerReset": 80,
        "maximumAmmoUnitsAcrossPermanentOffersPerReset": 400,
        "maximumAmmoFullResetSpendRub": 300000,
        "maximumReferencePriceMultiplier": 1.3,
        "offersMustBeQuestGated": true,
        "offersMustBeFinite": true,
        "questUnlockLoyaltyLevel": 1,
        "specialWeaponsPermanentOfferAllowed": false,
        "specialWeaponsSampleOnly": true
      },
      "loyalty": {
        "role": "relationship-status-only",
        "capabilityAuthority": false,
        "standingMayBypassQuestGates": false,
        "salesSumMayGateProgression": false
      }
    }
    """;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral Admiral Trader runtime ingestion smoke: {message}");
    }

    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Economy Admiral Admiral Trader runtime ingestion smoke expected '{name}' to fail.");
    }
}

using System.Text.Json;

namespace SPTEconomy;

public sealed record AdmiralTraderGameplayAlphaContractSummary
{
    public required string ProductName { get; init; }
    public required string ModGuid { get; init; }
    public required string TraderId { get; init; }
    public required int GameplayPolicySchemaVersion { get; init; }
    public required bool RelationshipStockAllowed { get; init; }
    public required bool SpecialWeaponsPermanentOfferAllowed { get; init; }
    public required bool SpecialWeaponsSampleOnly { get; init; }
    public required int BaselineOfferCount { get; init; }
    public required int RelationshipOfferCount { get; init; }
    public required int CoreOfferCount { get; init; }
    public required int MilestoneOfferCount { get; init; }
    public required IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Offers { get; init; }
}

public static class AdmiralTraderGameplayAlphaAdapter
{
    public const int FrozenQuestCount = 31;
    public const int FrozenBaselineOfferCount = 4;
    public const int FrozenMilestoneOfferCount = 7;
    public const int FrozenTotalOfferCount = 11;
    public const int ActiveQuestCount = 43;
    public const int ActiveBaselineOfferCount = 4;
    public const int ActiveRelationshipOfferCount = 3;
    public const int ActiveMilestoneOfferCount = 8;
    public const int ActiveCoreOfferCount = 22;
    public const int ActiveTotalOfferCount = 37;
    public const string ExpectedProductName = "Admiral Trader";
    public const string ExpectedTraderId = "d5c27bb3169f8dfbc13f6b69";
    public const string ExpectedModGuid = "com.admiralam.spt.admiraltrader";

    public static void ValidateFrozenReleaseShape(
        AdmiralTraderGameplayAlphaContractSummary summary,
        int authoredQuestCount)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Require(authoredQuestCount == FrozenQuestCount,
            $"frozen 0.1.0 requires {FrozenQuestCount} authored quests but found {authoredQuestCount}.");
        Require(summary.BaselineOfferCount == FrozenBaselineOfferCount,
            $"frozen 0.1.0 requires {FrozenBaselineOfferCount} Baseline offers but found {summary.BaselineOfferCount}.");
        Require(summary.RelationshipOfferCount == 0,
            $"frozen 0.1.0 must not materialize Relationship offers but found {summary.RelationshipOfferCount}.");
        Require(summary.MilestoneOfferCount == FrozenMilestoneOfferCount,
            $"frozen 0.1.0 requires {FrozenMilestoneOfferCount} Milestone offers but found {summary.MilestoneOfferCount}.");
        Require(summary.Offers.Count == FrozenTotalOfferCount,
            $"frozen 0.1.0 requires {FrozenTotalOfferCount} total offers but found {summary.Offers.Count}.");
    }

    public static void ValidateActiveCampaignShape(
        AdmiralTraderGameplayAlphaContractSummary summary,
        int authoredQuestCount)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Require(summary.GameplayPolicySchemaVersion == 5, "active campaign requires gameplay-policy schemaVersion 5.");
        Require(authoredQuestCount == ActiveQuestCount,
            $"active campaign requires {ActiveQuestCount} authored quests but found {authoredQuestCount}.");
        Require(summary.BaselineOfferCount == ActiveBaselineOfferCount,
            $"active campaign requires {ActiveBaselineOfferCount} Baseline offers but found {summary.BaselineOfferCount}.");
        Require(summary.RelationshipOfferCount == ActiveRelationshipOfferCount,
            $"M5 requires {ActiveRelationshipOfferCount} Relationship offers but found {summary.RelationshipOfferCount}.");
        Require(summary.MilestoneOfferCount == ActiveMilestoneOfferCount,
            $"active campaign requires {ActiveMilestoneOfferCount} Milestone offers but found {summary.MilestoneOfferCount}.");
        Require(summary.CoreOfferCount == ActiveCoreOfferCount,
            $"active campaign requires {ActiveCoreOfferCount} Core offers but found {summary.CoreOfferCount}.");
        Require(summary.Offers.Count == ActiveTotalOfferCount,
            $"active campaign requires {ActiveTotalOfferCount} total offers but found {summary.Offers.Count}.");
    }

    public static AdmiralTraderGameplayAlphaContractSummary Parse(
        string campaignManifestJson,
        string identityAssetsJson,
        string traderBaseJson,
        string gameplayPolicyJson,
        string baselineStockJson,
        string assortJson,
        string questAssortJson,
        IEnumerable<string> authoredQuestJsonRecords,
        string? relationshipStockJson = null,
        string? storefrontCoreJson = null)
    {
        ValidateIdentity(campaignManifestJson, identityAssetsJson, traderBaseJson);

        using var policyDoc = JsonDocument.Parse(gameplayPolicyJson);
        var policy = policyDoc.RootElement;
        var policySchemaVersion = policy.GetProperty("schemaVersion").GetInt32();
        Require(policySchemaVersion is 4 or 5, "Gameplay Alpha requires gameplay-policy schemaVersion 4 or 5.");
        Require(policy.GetProperty("productRole").GetString() == "specialist-trader-and-capability-broker", "unsupported Gameplay Alpha productRole.");
        var traderStock = policy.GetProperty("traderStock");
        Require(traderStock.GetProperty("baselineStockRequired").GetBoolean(), "baseline stock must be required.");
        Require(!traderStock.GetProperty("baselineOffersMustBeQuestGated").GetBoolean(), "baseline offers must not be quest-gated.");
        Require(traderStock.GetProperty("baselineOffersMustBeFinite").GetBoolean(), "baseline offers must be finite.");
        var relationshipAllowed = traderStock.GetProperty("relationshipStockAllowed").GetBoolean();
        var logistics = policy.GetProperty("logistics");
        var expectedMilestone = logistics.GetProperty("expectedMilestonePermanentOfferCount").GetInt32();
        var expectedBaseline = policySchemaVersion == 5
            ? logistics.GetProperty("expectedBaselineOfferCount").GetInt32()
            : (int?)null;
        var expectedRelationship = policySchemaVersion == 5
            ? logistics.GetProperty("expectedRelationshipOfferCount").GetInt32()
            : (int?)null;
        var maxStock = logistics.GetProperty("maximumPermanentOfferStockPerReset").GetInt32();
        Require(expectedMilestone > 0 && maxStock > 0, "invalid Gameplay Alpha logistics bounds.");
        Require(logistics.GetProperty("milestoneOffersMustBeQuestGated").GetBoolean(), "milestone offers must be quest-gated.");
        Require(logistics.GetProperty("offersMustBeFinite").GetBoolean(), "permanent offers must remain finite.");
        var specialPermanentAllowed = logistics.GetProperty("specialWeaponsPermanentOfferAllowed").GetBoolean();
        var specialSampleOnly = logistics.GetProperty("specialWeaponsSampleOnly").GetBoolean();
        if (policySchemaVersion == 4)
            Require(!specialPermanentAllowed && specialSampleOnly, "frozen special-weapons permanent/sample-only contract drift.");
        else
            Require(specialPermanentAllowed && !specialSampleOnly, "active Special Weapons M576 capability contract drift.");

        using var baselineDoc = JsonDocument.Parse(baselineStockJson);
        var baselineRoot = baselineDoc.RootElement;
        Require(baselineRoot.GetProperty("schemaVersion").GetInt32() is 1 or 2, "unsupported baseline-stock schema.");
        Require(baselineRoot.GetProperty("stockClass").GetString() == "Baseline", "baseline-stock stockClass must be Baseline.");
        var baselineById = baselineRoot.GetProperty("offers").EnumerateArray().ToDictionary(x => ReqString(x, "offerId"), StringComparer.Ordinal);
        Require(baselineById.Count > 0, "Gameplay Alpha baseline-stock must contain offers.");

        using var assortDoc = JsonDocument.Parse(assortJson);
        using var questDoc = JsonDocument.Parse(questAssortJson);
        var assortRoot = assortDoc.RootElement;
        var loyalty = assortRoot.GetProperty("loyal_level_items");
        var success = questDoc.RootElement.GetProperty("success");
        Require(success.ValueKind == JsonValueKind.Object, "questassort.success must be an object.");
        var successIds = success.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Require(successIds.Count == expectedMilestone, $"expected {expectedMilestone} milestone success mappings but found {successIds.Count}.");
        Require(!baselineById.Keys.Any(successIds.Contains), "baseline offers must not appear in questassort.success.");

        var relationshipOffers = AdmiralTraderRelationshipManifest.Parse(relationshipStockJson, relationshipAllowed, traderBaseJson);
        var relationshipById = relationshipOffers.ToDictionary(x => x.OfferId, StringComparer.Ordinal);
        Require(!relationshipById.Keys.Any(baselineById.ContainsKey), "Relationship offers must not overlap Baseline offers.");
        Require(!relationshipById.Keys.Any(successIds.Contains), "Relationship offers must not overlap quest-gated Milestone offers.");
        using var coreDoc = string.IsNullOrWhiteSpace(storefrontCoreJson) ? null : JsonDocument.Parse(storefrontCoreJson);
        var coreById = coreDoc is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : coreDoc.RootElement.GetProperty("offers").EnumerateArray().ToDictionary(x => ReqString(x, "offerId"), StringComparer.Ordinal);
        Require(!coreById.Keys.Any(baselineById.ContainsKey), "Core offers must not overlap Baseline offers.");
        Require(!coreById.Keys.Any(relationshipById.ContainsKey), "Core offers must not overlap Relationship offers.");
        Require(!coreById.Keys.Any(successIds.Contains), "Core offers must not overlap quest-gated Milestone offers.");

        var results = new List<AdmiralTraderOfferAdapterEvidence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in assortRoot.GetProperty("items").EnumerateArray())
        {
            if (item.TryGetProperty("parentId", out var parent) && parent.GetString() != "hideout")
                continue;
            var offerId = ReqString(item, "_id");
            var tpl = ReqString(item, "_tpl");
            Require(seen.Add(offerId), $"duplicate offer id '{offerId}'.");
            var upd = item.GetProperty("upd");
            Require(!upd.GetProperty("UnlimitedCount").GetBoolean(), $"offer '{offerId}' became unlimited.");
            var stock = upd.GetProperty("StackObjectsCount").GetInt32();
            var buy = upd.GetProperty("BuyRestrictionMax").GetInt32();
            Require(stock > 0 && buy > 0 && stock <= maxStock, $"invalid bounded supply for '{offerId}'.");
            if (!loyalty.TryGetProperty(offerId, out var ll) || !ll.TryGetInt32(out var loyaltyLevel))
                throw new InvalidOperationException($"Economy Admiral Admiral Trader Gameplay Alpha adapter: missing loyalty mapping for '{offerId}'.");

            if (baselineById.TryGetValue(offerId, out var baseline))
            {
                Require(baseline.GetProperty("questGate").ValueKind == JsonValueKind.Null, $"baseline offer '{offerId}' must declare questGate null.");
                Require(ReqString(baseline, "tpl") == tpl, $"baseline tpl drift for '{offerId}'.");
                Require(baseline.GetProperty("stockPerReset").GetInt32() == stock && baseline.GetProperty("buyRestriction").GetInt32() == buy, $"baseline capacity drift for '{offerId}'.");
                Require(baseline.GetProperty("loyaltyLevel").GetInt32() == loyaltyLevel, $"baseline loyalty drift for '{offerId}'.");
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Baseline", "None", null, loyaltyLevel, stock, buy, 1));
                continue;
            }

            if (relationshipById.TryGetValue(offerId, out var relationship))
            {
                Require(relationship.ItemTemplateId == tpl, $"Relationship tpl drift for '{offerId}'.");
                Require(relationship.LoyaltyLevel == loyaltyLevel, $"Relationship loyalty drift for '{offerId}'.");
                Require(relationship.StockPerReset == stock && relationship.BuyRestrictionPerReset == buy, $"Relationship capacity drift for '{offerId}'.");
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Relationship", "Loyalty", null, loyaltyLevel, stock, buy, relationship.MinimumPlayerLevel));
                continue;
            }

            if (success.TryGetProperty(offerId, out var questValue) && questValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(questValue.GetString()))
            {
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Milestone", "Quest", questValue.GetString(), loyaltyLevel, stock, buy, null));
                continue;
            }

            if (coreById.TryGetValue(offerId, out var core))
            {
                Require(ReqString(core, "itemTpl") == tpl, $"Core tpl drift for '{offerId}'.");
                Require(core.GetProperty("loyaltyLevel").GetInt32() == loyaltyLevel, $"Core loyalty drift for '{offerId}'.");
                Require(core.GetProperty("stockPerReset").GetInt32() == stock && core.GetProperty("buyRestriction").GetInt32() == buy, $"Core capacity drift for '{offerId}'.");
                var minimumLevel = loyaltyLevel switch { 1 => 1, 2 => 15, 3 => 25, 4 => 35, _ => throw new InvalidOperationException($"Unsupported Admiral loyalty level {loyaltyLevel}.") };
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Core", "Loyalty", null, loyaltyLevel, stock, buy, minimumLevel));
                continue;
            }

            throw new InvalidOperationException($"Economy Admiral Admiral Trader Gameplay Alpha adapter: offer '{offerId}' has no explicit Baseline/Relationship/Core/Milestone classification.");
        }

        Require(results.Count(x => x.StockClass == "Baseline") == baselineById.Count, "baseline-stock contains offers absent from assort.");
        Require(results.Count(x => x.StockClass == "Relationship") == relationshipById.Count, "relationship-stock contains offers absent from assort.");
        Require(results.Count(x => x.StockClass == "Milestone") == expectedMilestone, "milestone offer count drift.");
        Require(results.Count(x => x.StockClass == "Core") == coreById.Count, "storefront core contains offers absent from assort.");
        if (policySchemaVersion == 5)
        {
            Require(baselineById.Count == expectedBaseline, "gameplay-policy baseline offer count drift.");
            Require(relationshipById.Count == expectedRelationship, "gameplay-policy Relationship offer count drift.");
        }
        var graph = QuestGateJsonParser.ParseMany(authoredQuestJsonRecords);
        var enriched = AdmiralTraderItemAdapter.ApplyEffectiveQuestGates(results, graph);

        return new AdmiralTraderGameplayAlphaContractSummary
        {
            ProductName = ExpectedProductName,
            ModGuid = ExpectedModGuid,
            TraderId = ExpectedTraderId,
            GameplayPolicySchemaVersion = policySchemaVersion,
            RelationshipStockAllowed = relationshipAllowed,
            SpecialWeaponsPermanentOfferAllowed = specialPermanentAllowed,
            SpecialWeaponsSampleOnly = specialSampleOnly,
            BaselineOfferCount = enriched.Count(x => x.StockClass == "Baseline"),
            RelationshipOfferCount = enriched.Count(x => x.StockClass == "Relationship"),
            CoreOfferCount = enriched.Count(x => x.StockClass == "Core"),
            MilestoneOfferCount = enriched.Count(x => x.StockClass == "Milestone"),
            Offers = enriched,
        };
    }

    private static void ValidateIdentity(string campaignManifestJson, string identityAssetsJson, string traderBaseJson)
    {
        using var campaignDoc = JsonDocument.Parse(campaignManifestJson);
        var campaign = campaignDoc.RootElement;
        Require(campaign.GetProperty("schemaVersion").GetInt32() == 1, "unsupported campaign-manifest schema.");
        var product = campaign.GetProperty("product");
        Require(ReqString(product, "modName") == ExpectedProductName, "campaign product name drift.");
        Require(ReqString(product, "modGuid") == ExpectedModGuid, "campaign modGuid/owner drift.");
        Require(ReqString(product, "traderId") == ExpectedTraderId, "campaign traderId drift.");

        using var identityDoc = JsonDocument.Parse(identityAssetsJson);
        var identity = identityDoc.RootElement;
        Require(identity.GetProperty("schemaVersion").GetInt32() == 3, "unsupported identity-assets schema.");
        Require(ReqString(identity, "product") == ExpectedProductName, "identity-assets product drift.");
        Require(ReqString(identity, "traderId") == ExpectedTraderId, "identity-assets traderId drift.");

        using var baseDoc = JsonDocument.Parse(traderBaseJson);
        var traderBase = baseDoc.RootElement;
        Require(ReqString(traderBase, "_id") == ExpectedTraderId, "runtime trader base id drift.");
        Require(ReqString(traderBase, "name") == "Admiral", "runtime trader base name drift.");
        Require(ReqString(traderBase, "nickname") == "Admiral", "runtime trader nickname drift.");
        Require(ReqString(traderBase, "avatar") == $"/files/trader/avatar/{ExpectedTraderId}.jpg", "runtime trader avatar route drift.");
    }

    private static string ReqString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidOperationException($"Economy Admiral Admiral Trader Gameplay Alpha adapter: '{name}' must be a non-empty string.");
        return value.GetString()!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral Admiral Trader Gameplay Alpha adapter: {message}");
    }
}

using System.Text.Json;

namespace SPTEconomy;

public static class AdmiralTraderGameplayAlphaAdapter
{
    public static IReadOnlyList<AdmiralTraderOfferAdapterEvidence> Parse(
        string gameplayPolicyJson,
        string baselineStockJson,
        string assortJson,
        string questAssortJson,
        IEnumerable<string> authoredQuestJsonRecords)
    {
        using var policyDoc = JsonDocument.Parse(gameplayPolicyJson);
        var policy = policyDoc.RootElement;
        Require(policy.GetProperty("schemaVersion").GetInt32() == 4, "Gameplay Alpha requires gameplay-policy schemaVersion 4.");
        Require(policy.GetProperty("productRole").GetString() == "specialist-trader-and-capability-broker", "unsupported Gameplay Alpha productRole.");
        var traderStock = policy.GetProperty("traderStock");
        Require(traderStock.GetProperty("baselineStockRequired").GetBoolean(), "baseline stock must be required.");
        Require(!traderStock.GetProperty("baselineOffersMustBeQuestGated").GetBoolean(), "baseline offers must not be quest-gated.");
        Require(traderStock.GetProperty("baselineOffersMustBeFinite").GetBoolean(), "baseline offers must be finite.");
        var logistics = policy.GetProperty("logistics");
        var expectedMilestone = logistics.GetProperty("expectedMilestonePermanentOfferCount").GetInt32();
        var maxStock = logistics.GetProperty("maximumPermanentOfferStockPerReset").GetInt32();
        Require(expectedMilestone > 0 && maxStock > 0, "invalid Gameplay Alpha logistics bounds.");
        Require(logistics.GetProperty("milestoneOffersMustBeQuestGated").GetBoolean(), "milestone offers must be quest-gated.");
        Require(logistics.GetProperty("offersMustBeFinite").GetBoolean(), "permanent offers must remain finite.");

        using var baselineDoc = JsonDocument.Parse(baselineStockJson);
        var baselineRoot = baselineDoc.RootElement;
        Require(baselineRoot.GetProperty("schemaVersion").GetInt32() == 1, "unsupported baseline-stock schema.");
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

        var results = new List<AdmiralTraderOfferAdapterEvidence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in assortRoot.GetProperty("items").EnumerateArray())
        {
            var offerId = ReqString(item, "_id");
            var tpl = ReqString(item, "_tpl");
            Require(seen.Add(offerId), $"duplicate offer id '{offerId}'.");
            var upd = item.GetProperty("upd");
            Require(!upd.GetProperty("UnlimitedCount").GetBoolean(), $"offer '{offerId}' became unlimited.");
            var stock = upd.GetProperty("StackObjectsCount").GetInt32();
            var buy = upd.GetProperty("BuyRestrictionMax").GetInt32();
            Require(stock > 0 && buy > 0 && stock <= maxStock, $"invalid bounded supply for '{offerId}'.");
            Require(loyalty.TryGetProperty(offerId, out var ll) && ll.TryGetInt32(out var loyaltyLevel), $"missing loyalty mapping for '{offerId}'.");

            if (baselineById.TryGetValue(offerId, out var baseline))
            {
                Require(baseline.GetProperty("questGate").ValueKind == JsonValueKind.Null, $"baseline offer '{offerId}' must declare questGate null.");
                Require(ReqString(baseline, "tpl") == tpl, $"baseline tpl drift for '{offerId}'.");
                Require(baseline.GetProperty("stockPerReset").GetInt32() == stock && baseline.GetProperty("buyRestriction").GetInt32() == buy, $"baseline capacity drift for '{offerId}'.");
                Require(baseline.GetProperty("loyaltyLevel").GetInt32() == loyaltyLevel, $"baseline loyalty drift for '{offerId}'.");
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Baseline", "None", null, loyaltyLevel, stock, buy, 1));
                continue;
            }

            if (success.TryGetProperty(offerId, out var questValue) && questValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(questValue.GetString()))
            {
                results.Add(AdmiralTraderItemAdapter.BuildEvidence(offerId, tpl, "Milestone", "Quest", questValue.GetString(), loyaltyLevel, stock, buy, null));
                continue;
            }

            throw new InvalidOperationException($"Economy Admiral Admiral Trader Gameplay Alpha adapter: offer '{offerId}' has no unambiguous Baseline/Relationship/Milestone classification.");
        }

        Require(results.Count(x => x.StockClass == "Baseline") == baselineById.Count, "baseline-stock contains offers absent from assort.");
        Require(results.Count(x => x.StockClass == "Milestone") == expectedMilestone, "milestone offer count drift.");
        var graph = QuestGateJsonParser.ParseMany(authoredQuestJsonRecords);
        return AdmiralTraderItemAdapter.ApplyEffectiveQuestGates(results, graph);
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

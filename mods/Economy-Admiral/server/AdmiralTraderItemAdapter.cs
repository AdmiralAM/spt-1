using System.Text.Json;

namespace SPTEconomy;

public sealed record AdmiralTraderOfferAdapterEvidence
{
    public required string OfferId { get; init; }
    public required string ItemTemplateId { get; init; }
    public string StockClass { get; init; } = "Milestone";
    public string GateKind { get; init; } = "Quest";
    public string? QuestGateId { get; init; }
    public required int LoyaltyLevel { get; init; }
    public required int StockPerReset { get; init; }
    public required int BuyRestrictionPerReset { get; init; }
    public required AcquisitionSourceEvidence Source { get; init; }
    public required RenewableSupplyCapacityEvidence Capacity { get; init; }
    public EffectiveQuestGateEvidence? EffectiveGate { get; init; }
}

public static class AdmiralTraderItemAdapter
{
    public static IReadOnlyList<AdmiralTraderOfferAdapterEvidence> ParseOffers(string assortJson, string questAssortJson, AdmiralTraderAdapterContract policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        AdmiralTraderAdapterEvidence.ValidateMaintainedContract(policy);
        using var assortDocument = JsonDocument.Parse(RequireJson(assortJson, "assort"));
        using var questDocument = JsonDocument.Parse(RequireJson(questAssortJson, "questassort"));
        var assortRoot = assortDocument.RootElement;
        var questRoot = questDocument.RootElement;
        var items = RequireProperty(assortRoot, "items");
        var loyalty = RequireProperty(assortRoot, "loyal_level_items");
        var success = RequireProperty(questRoot, "success");
        if (items.ValueKind != JsonValueKind.Array || loyalty.ValueKind != JsonValueKind.Object || success.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: malformed legacy assort/questassort collections.");

        var results = new List<AdmiralTraderOfferAdapterEvidence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var offerId = RequireString(item, "_id");
            var templateId = RequireString(item, "_tpl");
            if (!seen.Add(offerId)) throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: duplicate offer id '{offerId}'.");
            var upd = RequireProperty(item, "upd");
            var unlimited = RequireBool(upd, "UnlimitedCount");
            var stock = RequireInt(upd, "StackObjectsCount");
            var buyRestriction = RequireInt(upd, "BuyRestrictionMax");
            if (unlimited || stock < 1 || buyRestriction < 1 || stock > policy.MaximumPermanentOfferStockPerReset)
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: invalid bounded supply for offer '{offerId}'.");
            if (!loyalty.TryGetProperty(offerId, out var loyaltyValue) || !loyaltyValue.TryGetInt32(out var loyaltyLevel) || loyaltyLevel != policy.QuestUnlockLoyaltyLevel)
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: loyalty drift for offer '{offerId}'.");
            if (!success.TryGetProperty(offerId, out var questValue) || questValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(questValue.GetString()))
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' is not explicitly quest-gated.");
            results.Add(BuildEvidence(offerId, templateId, "Milestone", "Quest", questValue.GetString(), loyaltyLevel, stock, buyRestriction, null));
        }

        if (results.Count != policy.ExpectedPermanentOfferCount)
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: expected {policy.ExpectedPermanentOfferCount} maintained offers but found {results.Count}.");
        var successKeys = success.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var offerKeys = results.Select(r => r.OfferId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!successKeys.SequenceEqual(offerKeys, StringComparer.Ordinal))
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: questassort.success must map exactly the maintained legacy offers.");
        return results.OrderBy(r => r.OfferId, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<AdmiralTraderOfferAdapterEvidence> ParseAndApplyEffectiveQuestGates(string assortJson, string questAssortJson, AdmiralTraderAdapterContract policy, IEnumerable<string> authoredQuestJsonRecords)
        => ApplyEffectiveQuestGates(ParseOffers(assortJson, questAssortJson, policy), QuestGateJsonParser.ParseMany(authoredQuestJsonRecords));

    public static IReadOnlyList<AdmiralTraderOfferAdapterEvidence> ApplyEffectiveQuestGates(IEnumerable<AdmiralTraderOfferAdapterEvidence> offers, IEnumerable<QuestGateNode> questGraph)
    {
        var graph = questGraph.ToList();
        return offers.Select(offer =>
        {
            if (!string.Equals(offer.GateKind, "Quest", StringComparison.Ordinal)) return offer;
            if (string.IsNullOrWhiteSpace(offer.QuestGateId)) throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: quest gate missing for '{offer.OfferId}'.");
            var gate = EffectiveQuestGateEvidenceResolver.Resolve(offer.QuestGateId, graph);
            if (!gate.CompleteQuestGraphEvidence || !gate.EffectiveMinimumLevel.HasValue)
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: effective quest gate for offer '{offer.OfferId}' is incomplete.");
            return offer with { EffectiveGate = gate, Source = offer.Source with { EarliestProgressionLevel = gate.EffectiveMinimumLevel } };
        }).OrderBy(o => o.OfferId, StringComparer.Ordinal).ToArray();
    }

    internal static AdmiralTraderOfferAdapterEvidence BuildEvidence(string offerId, string templateId, string stockClass, string gateKind, string? questGateId, int loyaltyLevel, int stock, int buyRestriction, int? earliestLevel)
        => new()
        {
            OfferId = offerId,
            ItemTemplateId = templateId,
            StockClass = stockClass,
            GateKind = gateKind,
            QuestGateId = questGateId,
            LoyaltyLevel = loyaltyLevel,
            StockPerReset = stock,
            BuyRestrictionPerReset = buyRestriction,
            Source = new AcquisitionSourceEvidence { ItemTemplateId = templateId, SourceId = $"admiral-trader:{offerId}", Channel = AcquisitionChannel.TraderPurchase, Renewable = true, EarliestProgressionLevel = earliestLevel, ProvenanceClass = AdmiralTraderAdapterEvidence.AttributionConfidence },
            Capacity = new RenewableSupplyCapacityEvidence { ItemTemplateId = templateId, SourceId = $"admiral-trader:{offerId}", Channel = AcquisitionChannel.TraderPurchase, SupplyBound = RenewableSupplyBound.Bounded, MaxUnitsPerReset = stock, MaxAcquisitionsPerReset = buyRestriction },
        };

    private static string RequireJson(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: {name} JSON must not be empty.") : value;
    private static JsonElement RequireProperty(JsonElement parent, string name) => parent.TryGetProperty(name, out var value) ? value : throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: required property '{name}' is missing.");
    private static string RequireString(JsonElement parent, string name) { var v = RequireProperty(parent, name); return v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be a non-empty string."); }
    private static int RequireInt(JsonElement parent, string name) { var v = RequireProperty(parent, name); return v.TryGetInt32(out var n) ? n : throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be an integer."); }
    private static bool RequireBool(JsonElement parent, string name) { var v = RequireProperty(parent, name); return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be boolean."); }
}

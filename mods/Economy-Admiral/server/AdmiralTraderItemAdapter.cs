using System.Text.Json;

namespace SPTEconomy;

public sealed record AdmiralTraderOfferAdapterEvidence
{
    public required string OfferId { get; init; }
    public required string ItemTemplateId { get; init; }
    public required string QuestGateId { get; init; }
    public required int LoyaltyLevel { get; init; }
    public required int StockPerReset { get; init; }
    public required int BuyRestrictionPerReset { get; init; }
    public required AcquisitionSourceEvidence Source { get; init; }
    public required RenewableSupplyCapacityEvidence Capacity { get; init; }
}

public static class AdmiralTraderItemAdapter
{
    public static IReadOnlyList<AdmiralTraderOfferAdapterEvidence> ParseOffers(
        string assortJson,
        string questAssortJson,
        AdmiralTraderAdapterContract policy
    )
    {
        ArgumentNullException.ThrowIfNull(policy);
        AdmiralTraderAdapterEvidence.ValidateMaintainedContract(policy);

        using var assortDocument = JsonDocument.Parse(RequireJson(assortJson, "assort"));
        using var questDocument = JsonDocument.Parse(RequireJson(questAssortJson, "questassort"));

        var assortRoot = assortDocument.RootElement;
        var questRoot = questDocument.RootElement;
        RequireObject(assortRoot, "assort root");
        RequireObject(questRoot, "questassort root");

        var items = RequireProperty(assortRoot, "items");
        if (items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: assort.items must be an array.");
        }

        var loyalty = RequireProperty(assortRoot, "loyal_level_items");
        RequireObject(loyalty, "loyal_level_items");

        var success = RequireProperty(questRoot, "Success");
        RequireObject(success, "questassort.Success");

        var results = new List<AdmiralTraderOfferAdapterEvidence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items.EnumerateArray())
        {
            RequireObject(item, "assort item");
            var offerId = RequireString(item, "_id");
            var templateId = RequireString(item, "_tpl");
            if (!seen.Add(offerId))
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: duplicate offer id '{offerId}'.");
            }

            var upd = RequireProperty(item, "upd");
            RequireObject(upd, $"upd for offer '{offerId}'");
            var unlimited = RequireBool(upd, "UnlimitedCount");
            var stock = RequireInt(upd, "StackObjectsCount");
            var buyRestriction = RequireInt(upd, "BuyRestrictionMax");

            if (unlimited)
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: maintained offer '{offerId}' unexpectedly became unlimited.");
            }
            if (stock < 1 || buyRestriction < 1)
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' requires positive stock and buy restriction.");
            }
            if (stock > policy.MaximumPermanentOfferStockPerReset)
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' exceeds maintained stock cap {policy.MaximumPermanentOfferStockPerReset}.");
            }

            if (!loyalty.TryGetProperty(offerId, out var loyaltyValue) || !loyaltyValue.TryGetInt32(out var loyaltyLevel))
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' is missing loyalty mapping.");
            }
            if (loyaltyLevel != policy.QuestUnlockLoyaltyLevel)
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' loyalty level drifted from maintained quest-unlock level {policy.QuestUnlockLoyaltyLevel}.");
            }

            if (!success.TryGetProperty(offerId, out var questValue) || questValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(questValue.GetString()))
            {
                throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: offer '{offerId}' is not explicitly quest-gated by Success mapping.");
            }

            var questGateId = questValue.GetString()!;
            results.Add(new AdmiralTraderOfferAdapterEvidence
            {
                OfferId = offerId,
                ItemTemplateId = templateId,
                QuestGateId = questGateId,
                LoyaltyLevel = loyaltyLevel,
                StockPerReset = stock,
                BuyRestrictionPerReset = buyRestriction,
                Source = new AcquisitionSourceEvidence
                {
                    ItemTemplateId = templateId,
                    SourceId = $"admiral-trader:{offerId}",
                    Channel = AcquisitionChannel.TraderPurchase,
                    Renewable = true,
                    EarliestProgressionLevel = null,
                    ProvenanceClass = AdmiralTraderAdapterEvidence.AttributionConfidence,
                },
                Capacity = new RenewableSupplyCapacityEvidence
                {
                    ItemTemplateId = templateId,
                    SourceId = $"admiral-trader:{offerId}",
                    Channel = AcquisitionChannel.TraderPurchase,
                    SupplyBound = RenewableSupplyBound.Bounded,
                    MaxUnitsPerReset = stock,
                    MaxAcquisitionsPerReset = buyRestriction,
                },
            });
        }

        if (results.Count != policy.ExpectedPermanentOfferCount)
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: expected {policy.ExpectedPermanentOfferCount} maintained offers but found {results.Count}.");
        }

        var successKeys = success.EnumerateObject().Select(property => property.Name).OrderBy(value => value, StringComparer.Ordinal).ToList();
        var offerKeys = results.Select(result => result.OfferId).OrderBy(value => value, StringComparer.Ordinal).ToList();
        if (!successKeys.SequenceEqual(offerKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: questassort.Success must map exactly the maintained permanent offers.");
        }

        return results.OrderBy(result => result.OfferId, StringComparer.Ordinal).ToList();
    }

    private static string RequireJson(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: {name} JSON must not be empty.");
        }
        return value;
    }

    private static JsonElement RequireProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: required property '{name}' is missing.");
        }
        return value;
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: {name} must be an object.");
        }
    }

    private static string RequireString(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be a non-empty string.");
        }
        return value.GetString()!;
    }

    private static int RequireInt(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (!value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be an integer.");
        }
        return result;
    }

    private static bool RequireBool(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be boolean.");
        }
        return value.GetBoolean();
    }
}

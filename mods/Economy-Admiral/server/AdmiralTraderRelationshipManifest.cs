using System.Text.Json;

namespace SPTEconomy;

public sealed record AdmiralTraderRelationshipOfferContract
{
    public required string OfferId { get; init; }
    public required string ItemTemplateId { get; init; }
    public required int LoyaltyLevel { get; init; }
    public required double RequiredStanding { get; init; }
    public required int MinimumPlayerLevel { get; init; }
    public required int StockPerReset { get; init; }
    public required int BuyRestrictionPerReset { get; init; }
}

public static class AdmiralTraderRelationshipManifest
{
    public static IReadOnlyList<AdmiralTraderRelationshipOfferContract> Parse(string? manifestJson, bool relationshipStockAllowed, string traderBaseJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson)) return Array.Empty<AdmiralTraderRelationshipOfferContract>();

        using var document = JsonDocument.Parse(manifestJson);
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetInt32() == 1, "unsupported relationship-stock schema.");
        Require(root.GetProperty("stockClass").GetString() == "Relationship", "stockClass must be Relationship.");

        var authority = root.GetProperty("authority");
        Require(!authority.GetProperty("salesSumGateAllowed").GetBoolean(), "sales-sum gating is forbidden.");
        Require(!authority.GetProperty("questGateAllowed").GetBoolean(), "quest gating is forbidden for Relationship stock.");
        Require(!authority.GetProperty("capabilityAuthority").GetBoolean(), "Relationship stock cannot own capability authority.");
        Require(authority.GetProperty("finiteStockRequired").GetBoolean(), "Relationship stock must remain finite.");

        var enabled = root.GetProperty("materialization").GetProperty("enabled").GetBoolean();
        var offersElement = root.TryGetProperty("offers", out var offers) ? offers : default;
        if (!enabled)
        {
            Require(offersElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Array, "disabled Relationship offers must be absent or an array.");
            if (offersElement.ValueKind == JsonValueKind.Array) Require(offersElement.GetArrayLength() == 0, "disabled Relationship materialization cannot declare runtime offers.");
            return Array.Empty<AdmiralTraderRelationshipOfferContract>();
        }

        Require(relationshipStockAllowed, "Relationship materialization enabled while gameplay policy disables it.");
        Require(offersElement.ValueKind == JsonValueKind.Array && offersElement.GetArrayLength() > 0, "enabled Relationship materialization requires explicit offers.");

        using var baseDocument = JsonDocument.Parse(traderBaseJson);
        var loyaltyLevels = baseDocument.RootElement.GetProperty("loyaltyLevels").EnumerateArray().ToArray();
        Require(loyaltyLevels.Length == 4, "Admiral Relationship contract requires exactly four loyalty levels.");

        var result = new List<AdmiralTraderRelationshipOfferContract>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var offer in offersElement.EnumerateArray())
        {
            var offerId = ReqString(offer, "offerId");
            var tpl = ReqString(offer, "tpl");
            Require(seen.Add(offerId), $"duplicate Relationship offer '{offerId}'.");
            var loyaltyLevel = offer.GetProperty("loyaltyLevel").GetInt32();
            Require(loyaltyLevel is >= 2 and <= 4, $"Relationship offer '{offerId}' must use LL2-LL4.");
            var stock = offer.GetProperty("stockPerReset").GetInt32();
            var buy = offer.GetProperty("buyRestriction").GetInt32();
            Require(stock > 0 && buy > 0 && buy <= stock, $"invalid bounded supply for Relationship offer '{offerId}'.");
            Require(!offer.TryGetProperty("questGate", out var questGate) || questGate.ValueKind == JsonValueKind.Null, $"Relationship offer '{offerId}' cannot declare a quest gate.");

            var tier = loyaltyLevels[loyaltyLevel - 1];
            Require(tier.GetProperty("minSalesSum").GetInt32() == 0, $"LL{loyaltyLevel} sales-sum gate drifted.");
            var standing = tier.GetProperty("minStanding").GetDouble();
            var minimumLevel = tier.GetProperty("minLevel").GetInt32();
            Require(standing > 0 && minimumLevel > 0, $"LL{loyaltyLevel} Relationship progression gate is invalid.");

            result.Add(new AdmiralTraderRelationshipOfferContract
            {
                OfferId = offerId,
                ItemTemplateId = tpl,
                LoyaltyLevel = loyaltyLevel,
                RequiredStanding = standing,
                MinimumPlayerLevel = minimumLevel,
                StockPerReset = stock,
                BuyRestrictionPerReset = buy,
            });
        }

        return result.OrderBy(x => x.OfferId, StringComparer.Ordinal).ToArray();
    }

    private static string ReqString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidOperationException($"Economy Admiral Admiral Trader Relationship adapter: '{name}' must be a non-empty string.");
        return value.GetString()!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral Admiral Trader Relationship adapter: {message}");
    }
}

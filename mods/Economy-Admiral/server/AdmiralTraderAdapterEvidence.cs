using System.Text.Json;

namespace SPTEconomy;

public sealed record AdmiralTraderAdapterContract
{
    public required int SchemaVersion { get; init; }
    public required string ProductRole { get; init; }
    public required int ExpectedPermanentOfferCount { get; init; }
    public required int ExpectedAmmoPermanentOfferCount { get; init; }
    public required int MaximumPermanentOfferStockPerReset { get; init; }
    public required int MaximumAmmoUnitsAcrossPermanentOffersPerReset { get; init; }
    public required int MaximumAmmoFullResetSpendRub { get; init; }
    public required double MaximumReferencePriceMultiplier { get; init; }
    public required bool OffersMustBeQuestGated { get; init; }
    public required bool OffersMustBeFinite { get; init; }
    public required int QuestUnlockLoyaltyLevel { get; init; }
    public required bool SpecialWeaponsPermanentOfferAllowed { get; init; }
    public required bool SpecialWeaponsSampleOnly { get; init; }
    public required string LoyaltyRole { get; init; }
    public required bool CapabilityAuthority { get; init; }
    public required bool StandingMayBypassQuestGates { get; init; }
    public required bool SalesSumMayGateProgression { get; init; }
}

public static class AdmiralTraderAdapterEvidence
{
    public const string Owner = "Admiral Trader";
    public const string AttributionConfidence = "ExplicitAdapter";

    public static AdmiralTraderAdapterContract ParseGameplayPolicy(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: gameplay policy JSON must not be empty.");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        RequireObject(root, "root");

        var logistics = RequireProperty(root, "logistics");
        var loyalty = RequireProperty(root, "loyalty");

        var contract = new AdmiralTraderAdapterContract
        {
            SchemaVersion = RequireInt(root, "schemaVersion"),
            ProductRole = RequireString(root, "productRole"),
            ExpectedPermanentOfferCount = RequireInt(logistics, "expectedPermanentOfferCount"),
            ExpectedAmmoPermanentOfferCount = RequireInt(logistics, "expectedAmmoPermanentOfferCount"),
            MaximumPermanentOfferStockPerReset = RequireInt(logistics, "maximumPermanentOfferStockPerReset"),
            MaximumAmmoUnitsAcrossPermanentOffersPerReset = RequireInt(logistics, "maximumAmmoUnitsAcrossPermanentOffersPerReset"),
            MaximumAmmoFullResetSpendRub = RequireInt(logistics, "maximumAmmoFullResetSpendRub"),
            MaximumReferencePriceMultiplier = RequireDouble(logistics, "maximumReferencePriceMultiplier"),
            OffersMustBeQuestGated = RequireBool(logistics, "offersMustBeQuestGated"),
            OffersMustBeFinite = RequireBool(logistics, "offersMustBeFinite"),
            QuestUnlockLoyaltyLevel = RequireInt(logistics, "questUnlockLoyaltyLevel"),
            SpecialWeaponsPermanentOfferAllowed = RequireBool(logistics, "specialWeaponsPermanentOfferAllowed"),
            SpecialWeaponsSampleOnly = RequireBool(logistics, "specialWeaponsSampleOnly"),
            LoyaltyRole = RequireString(loyalty, "role"),
            CapabilityAuthority = RequireBool(loyalty, "capabilityAuthority"),
            StandingMayBypassQuestGates = RequireBool(loyalty, "standingMayBypassQuestGates"),
            SalesSumMayGateProgression = RequireBool(loyalty, "salesSumMayGateProgression"),
        };

        ValidateMaintainedContract(contract);
        return contract;
    }

    public static void ValidateMaintainedContract(AdmiralTraderAdapterContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.SchemaVersion < 3)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: gameplay-policy schema is older than supported schema 3.");
        }
        if (!string.Equals(contract.ProductRole, "capability-broker", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: unsupported Admiral Trader product role.");
        }
        if (!contract.OffersMustBeQuestGated || !contract.OffersMustBeFinite)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: maintained capability offers must remain quest-gated and finite.");
        }
        if (contract.QuestUnlockLoyaltyLevel != 1)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: quest-gated offers are expected at LL1; loyalty must not replace quest gating.");
        }
        if (contract.CapabilityAuthority || contract.StandingMayBypassQuestGates || contract.SalesSumMayGateProgression)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: loyalty/sales-sum may not become capability authority.");
        }
        if (!string.Equals(contract.LoyaltyRole, "relationship-status-only", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: unsupported loyalty role.");
        }
        if (contract.SpecialWeaponsPermanentOfferAllowed || !contract.SpecialWeaponsSampleOnly)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: Special Weapons must remain sample-only without a permanent offer.");
        }
        if (contract.ExpectedPermanentOfferCount < 1
            || contract.ExpectedAmmoPermanentOfferCount < 0
            || contract.ExpectedAmmoPermanentOfferCount > contract.ExpectedPermanentOfferCount
            || contract.MaximumPermanentOfferStockPerReset < 1
            || contract.MaximumAmmoUnitsAcrossPermanentOffersPerReset < 1
            || contract.MaximumAmmoFullResetSpendRub < 1
            || !double.IsFinite(contract.MaximumReferencePriceMultiplier)
            || contract.MaximumReferencePriceMultiplier <= 0)
        {
            throw new InvalidOperationException("Economy Admiral Admiral Trader adapter: maintained logistics bounds are invalid.");
        }
    }

    private static JsonElement RequireProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: required gameplay-policy property '{name}' is missing.");
        }
        return value;
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be an object.");
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

    private static double RequireDouble(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (!value.TryGetDouble(out var result) || !double.IsFinite(result))
        {
            throw new InvalidOperationException($"Economy Admiral Admiral Trader adapter: '{name}' must be finite numeric evidence.");
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

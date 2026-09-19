using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AdmiralTrader.Server;

public sealed record RewardBundlePolicy
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("catalog")]
    public required List<RewardBundleCatalogItem> Catalog { get; init; }

    [JsonPropertyName("bundles")]
    public required List<RewardBundleSpec> Bundles { get; init; }
}

public sealed record RewardBundleCatalogItem
{
    [JsonPropertyName("templateId")]
    public required string TemplateId { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }

    [JsonPropertyName("tier")]
    public required string Tier { get; init; }

    [JsonPropertyName("valueRub")]
    public int ValueRub { get; init; }

    [JsonPropertyName("compatibilityFamily")]
    public string? CompatibilityFamily { get; init; }

    [JsonPropertyName("parentRole")]
    public string? ParentRole { get; init; }

    [JsonPropertyName("slotId")]
    public string? SlotId { get; init; }

    [JsonPropertyName("requiredTemplates")]
    public List<string> RequiredTemplates { get; init; } = [];
}

public sealed record RewardBundleSpec
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("theme")]
    public required string Theme { get; init; }

    [JsonPropertyName("tier")]
    public required string Tier { get; init; }

    [JsonPropertyName("seed")]
    public required string Seed { get; init; }

    [JsonPropertyName("minValueRub")]
    public int MinValueRub { get; init; }

    [JsonPropertyName("maxValueRub")]
    public int MaxValueRub { get; init; }

    [JsonPropertyName("slots")]
    public required List<RewardBundleSlot> Slots { get; init; }
}

public sealed record RewardBundleSlot
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("chancePercent")]
    public int ChancePercent { get; init; } = 100;
}

public sealed record GeneratedRewardBundle(
    string SpecId,
    string Theme,
    string Tier,
    int TotalValueRub,
    IReadOnlyList<GeneratedRewardBundleItem> Items);

public sealed record GeneratedRewardBundleItem(
    string TemplateId,
    string Role,
    string Source,
    int ValueRub,
    string? CompatibilityFamily,
    string? ParentRole,
    string? SlotId);

public sealed class RewardBundleException(string stage, string specId, string detail)
    : InvalidOperationException($"Admiral reward bundle {stage} failure for '{specId}': {detail}")
{
    public string Stage { get; } = stage;
    public string SpecId { get; } = specId;
}

public static class RewardBundleEngine
{
    private static readonly HashSet<string> SupportedTiers = ["common", "rare", "epic"];

    public static void ValidatePolicy(RewardBundlePolicy policy)
    {
        if (policy.SchemaVersion != 1)
            throw new RewardBundleException("catalog", "policy", $"unsupported schema {policy.SchemaVersion}");

        HashSet<string> templateIds = new(StringComparer.Ordinal);
        foreach (RewardBundleCatalogItem item in policy.Catalog)
        {
            if (item.TemplateId.Length != 24 || !item.TemplateId.All(Uri.IsHexDigit))
                throw new RewardBundleException("catalog", "policy", $"invalid template id {item.TemplateId}");
            if (!templateIds.Add(item.TemplateId))
                throw new RewardBundleException("catalog", "policy", $"duplicate template id {item.TemplateId}");
            if (item.Roles.Count == 0 || item.Roles.Any(string.IsNullOrWhiteSpace))
                throw new RewardBundleException("catalog", "policy", $"template {item.TemplateId} has no valid roles");
            if (!SupportedTiers.Contains(item.Tier) || item.ValueRub <= 0)
                throw new RewardBundleException("catalog", "policy", $"template {item.TemplateId} has invalid tier/value");
        }

        HashSet<string> specIds = new(StringComparer.Ordinal);
        foreach (RewardBundleSpec spec in policy.Bundles)
        {
            if (!specIds.Add(spec.Id) || string.IsNullOrWhiteSpace(spec.Theme) || string.IsNullOrWhiteSpace(spec.Seed))
                throw new RewardBundleException("generation", spec.Id, "id, theme and seed must be unique and non-empty");
            if (!SupportedTiers.Contains(spec.Tier) || spec.MinValueRub < 0 || spec.MaxValueRub <= 0 || spec.MinValueRub > spec.MaxValueRub)
                throw new RewardBundleException("generation", spec.Id, "invalid tier or value envelope");
            if (spec.Slots.Count == 0 || spec.Slots.GroupBy(x => x.Role, StringComparer.Ordinal).Any(x => x.Count() > 1))
                throw new RewardBundleException("generation", spec.Id, "roles must be non-empty and unique");
            if (spec.Slots.Any(x => string.IsNullOrWhiteSpace(x.Role) || x.ChancePercent is < 0 or > 100))
                throw new RewardBundleException("generation", spec.Id, "slot role/chance is invalid");
        }
    }

    public static GeneratedRewardBundle Generate(
        RewardBundlePolicy policy,
        RewardBundleSpec spec,
        IReadOnlySet<string> availableTemplates)
    {
        ValidatePolicy(policy);
        if (!policy.Bundles.Any(row => ReferenceEquals(row, spec) || row.Id == spec.Id))
            throw new RewardBundleException("generation", spec.Id, "specification is not part of the loaded policy");

        List<GeneratedRewardBundleItem> selected = [];
        string? compatibilityFamily = null;

        for (int index = 0; index < spec.Slots.Count; index++)
        {
            RewardBundleSlot slot = spec.Slots[index];
            if (!slot.Required && DeterministicPercent(spec.Seed, spec.Id, slot.Role, index) >= slot.ChancePercent)
                continue;

            IEnumerable<RewardBundleCatalogItem> candidates = policy.Catalog.Where(item =>
                availableTemplates.Contains(item.TemplateId)
                && item.RequiredTemplates.All(availableTemplates.Contains)
                && item.Tier == spec.Tier
                && item.Roles.Contains(slot.Role, StringComparer.Ordinal)
                && selected.All(chosen => chosen.TemplateId != item.TemplateId));

            if (compatibilityFamily is not null && IsWeaponSupportRole(slot.Role))
                candidates = candidates.Where(item => item.CompatibilityFamily == compatibilityFamily);

            List<RewardBundleCatalogItem> affordable = candidates
                .Where(item => selected.Sum(x => x.ValueRub) + item.ValueRub <= spec.MaxValueRub)
                .OrderBy(item => item.TemplateId, StringComparer.Ordinal)
                .ToList();

            if (affordable.Count == 0)
            {
                if (slot.Required)
                    throw new RewardBundleException("generation", spec.Id, $"required role '{slot.Role}' has no available compatible candidate");
                continue;
            }

            RewardBundleCatalogItem picked = affordable[DeterministicIndex(affordable.Count, spec.Seed, spec.Id, slot.Role, index)];
            if (slot.Role == "weapon")
            {
                if (string.IsNullOrWhiteSpace(picked.CompatibilityFamily))
                    throw new RewardBundleException("validation", spec.Id, $"weapon {picked.TemplateId} has no compatibility family");
                compatibilityFamily = picked.CompatibilityFamily;
            }
            selected.Add(new GeneratedRewardBundleItem(
                picked.TemplateId, slot.Role, picked.Source, picked.ValueRub,
                picked.CompatibilityFamily, picked.ParentRole, picked.SlotId));
        }

        GeneratedRewardBundle bundle = new(spec.Id, spec.Theme, spec.Tier, selected.Sum(x => x.ValueRub), selected);
        ValidateBundle(spec, bundle, availableTemplates);
        return bundle;
    }

    public static void ValidateBundle(RewardBundleSpec spec, GeneratedRewardBundle bundle, IReadOnlySet<string> availableTemplates)
    {
        if (bundle.TotalValueRub < spec.MinValueRub || bundle.TotalValueRub > spec.MaxValueRub)
            throw new RewardBundleException("validation", spec.Id, $"bundle value {bundle.TotalValueRub} is outside {spec.MinValueRub}..{spec.MaxValueRub}");
        if (bundle.Items.Select(x => x.TemplateId).Distinct(StringComparer.Ordinal).Count() != bundle.Items.Count)
            throw new RewardBundleException("validation", spec.Id, "duplicate template selected");
        if (bundle.Items.Any(x => !availableTemplates.Contains(x.TemplateId)))
            throw new RewardBundleException("validation", spec.Id, "unavailable template selected");
        foreach (RewardBundleSlot slot in spec.Slots.Where(x => x.Required))
            if (!bundle.Items.Any(x => x.Role == slot.Role))
                throw new RewardBundleException("validation", spec.Id, $"required role '{slot.Role}' is missing");

        GeneratedRewardBundleItem? weapon = bundle.Items.FirstOrDefault(x => x.Role == "weapon");
        if (weapon is not null)
        {
            foreach (GeneratedRewardBundleItem support in bundle.Items.Where(x => IsWeaponSupportRole(x.Role)))
                if (support.CompatibilityFamily != weapon.CompatibilityFamily)
                    throw new RewardBundleException("validation", spec.Id, $"{support.Role} {support.TemplateId} is incompatible with weapon family {weapon.CompatibilityFamily}");
        }

        foreach (GeneratedRewardBundleItem child in bundle.Items.Where(x => x.ParentRole is not null))
            if (string.IsNullOrWhiteSpace(child.SlotId) || !bundle.Items.Any(x => x.Role == child.ParentRole))
                throw new RewardBundleException("validation", spec.Id, $"child {child.TemplateId} has no valid parent role/slot");
    }

    private static bool IsWeaponSupportRole(string role) => role is "weapon-part" or "magazine" or "ammunition";

    private static int DeterministicPercent(params object[] parts) => DeterministicIndex(100, parts);

    private static int DeterministicIndex(int count, params object[] parts)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)));
        return (int)(BitConverter.ToUInt32(digest, 0) % (uint)count);
    }
}

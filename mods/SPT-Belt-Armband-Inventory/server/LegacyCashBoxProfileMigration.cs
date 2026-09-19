using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Migration;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Preserves profiles created while the private Pack 'n' Strap import exposed
/// Small Cash Box without leaving B&amp;A&amp;HB as the owner of that foreign item.
/// WZ Wallet is the box's original 2x1/4x2 clone source, so the replacement
/// keeps both the stash footprint and every nested money location valid.
/// </summary>
[Injectable]
public sealed class LegacyCashBoxProfileMigration : AbstractProfileMigration
{
    public const string LegacyCashBoxTemplateId = "669c10fa06c00c483c58537a";
    public const string CompatibleWalletTemplateId = "60b0f6c058e0b0481a09ad11";

    public override string MigrationName => "BAndHBLegacyCashBoxToWzWalletV1";

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
        => ContainsLegacyCashBox(profile, "pmc") || ContainsLegacyCashBox(profile, "scav");

    public override JsonObject? Migrate(JsonObject profile)
    {
        Replace(profile, "pmc");
        Replace(profile, "scav");
        return base.Migrate(profile);
    }

    private static bool ContainsLegacyCashBox(JsonObject profile, string character)
        => Items(profile, character).Any(item => Read(item, "_tpl") == LegacyCashBoxTemplateId);

    private static void Replace(JsonObject profile, string character)
    {
        foreach (JsonObject item in Items(profile, character))
            if (Read(item, "_tpl") == LegacyCashBoxTemplateId)
                item["_tpl"] = CompatibleWalletTemplateId;
    }

    private static IEnumerable<JsonObject> Items(JsonObject profile, string character)
        => (profile["characters"]?[character]?["Inventory"]?["items"] as JsonArray)?.OfType<JsonObject>()
            ?? Enumerable.Empty<JsonObject>();

    private static string? Read(JsonObject item, string key)
    {
        try { return item[key]?.GetValue<string>(); }
        catch { return null; }
    }
}

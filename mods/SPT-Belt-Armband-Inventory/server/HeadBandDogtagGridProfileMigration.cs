using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Migration;

namespace SPTBeltArmbandInventory.Server;

/// <summary>Moves legacy cigarette-pocket contents out without deleting them.</summary>
[Injectable]
public sealed class HeadBandDogtagGridProfileMigration : AbstractProfileMigration
{
    public override string MigrationName => "BAndHBHeadBandDogtagGridV2";

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
        => Needs(profile, "pmc") || Needs(profile, "scav");

    public override JsonObject? Migrate(JsonObject profile)
    {
        Fix(profile, "pmc");
        Fix(profile, "scav");
        return base.Migrate(profile);
    }

    private static bool Needs(JsonObject profile, string character)
    {
        var inventory = profile["characters"]?[character]?["Inventory"] as JsonObject;
        var items = inventory?["items"] as JsonArray;
        if (items == null) return false;
        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand))
        {
            string? id = Read(headBand, "_id");
            if (id == null) continue;
            foreach (var child in Children(items, id))
            {
                string? tpl = Read(child, "_tpl");
                if (HeadBandUtilityPolicy.IsCigarette(tpl ?? "")) return true;
                if (HeadBandUtilityPolicy.IsDogtagCase(tpl ?? "")
                    && !string.Equals(Read(child, "slotId"), DedicatedWearableItems.HeadBandDogtagCaseGridName, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private static void Fix(JsonObject profile, string character)
    {
        var inventory = profile["characters"]?[character]?["Inventory"] as JsonObject;
        var items = inventory?["items"] as JsonArray;
        if (inventory == null || items == null) return;
        string? sorting = Read(inventory, "sortingTable");
        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand).ToArray())
        {
            string? id = Read(headBand, "_id");
            if (id == null) continue;
            bool casePlaced = false;
            foreach (var child in Children(items, id).ToArray())
            {
                string? tpl = Read(child, "_tpl");
                if (HeadBandUtilityPolicy.IsDogtagCase(tpl ?? "") && !casePlaced)
                {
                    child["slotId"] = DedicatedWearableItems.HeadBandDogtagCaseGridName;
                    child["location"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["r"] = "Horizontal" };
                    casePlaced = true;
                }
                else if (HeadBandUtilityPolicy.IsCigarette(tpl ?? "") && sorting != null)
                {
                    child["parentId"] = sorting;
                    child["slotId"] = "hideout";
                    child.Remove("location");
                }
            }
        }
    }

    private static bool IsHeadBand(JsonObject x) => Read(x, "_tpl") == RuntimeIdentity.EmergencyHeadBandItemId;
    private static IEnumerable<JsonObject> Children(JsonArray items, string parent) =>
        items.OfType<JsonObject>().Where(x => Read(x, "parentId") == parent);
    private static string? Read(JsonObject x, string key) { try { return x[key]?.GetValue<string>(); } catch { return null; } }
}

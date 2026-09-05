using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Migration;

namespace SPTBeltArmbandInventory.Server;

[Injectable]
public sealed class HeadBandSplitGridProfileMigration : AbstractProfileMigration
{
    public override string MigrationName => "BAndHBHeadBandSplitGridV1";

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return NeedsMigration(profile, "pmc") || NeedsMigration(profile, "scav");
    }

    public override JsonObject? Migrate(JsonObject profile)
    {
        MigrateCharacter(profile, "pmc");
        MigrateCharacter(profile, "scav");
        return base.Migrate(profile);
    }

    private static bool NeedsMigration(JsonObject profile, string character)
    {
        var inventory = GetInventory(profile, character);
        var items = inventory?["items"] as JsonArray;
        if (items == null) return false;

        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand))
        {
            string? headBandId = ReadString(headBand, "_id");
            if (string.IsNullOrEmpty(headBandId)) continue;

            int currencyCount = 0;
            int cigaretteCount = 0;
            foreach (var child in ImmediateChildren(items, headBandId))
            {
                string? tpl = ReadString(child, "_tpl");
                if (tpl != null && HeadBandUtilityPolicy.IsCurrencyOrWallet(tpl))
                {
                    currencyCount++;
                    if (currencyCount > 1
                        || !string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCurrencyGridName, StringComparison.Ordinal)
                        || !IsOriginLocation(child["location"])) return true;
                }
                else if (tpl != null && HeadBandUtilityPolicy.IsCigarette(tpl))
                {
                    cigaretteCount++;
                    if (cigaretteCount > 1
                        || !string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCigarettesGridName, StringComparison.Ordinal)
                        || !IsOriginLocation(child["location"])) return true;
                }
                else
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void MigrateCharacter(JsonObject profile, string character)
    {
        var inventory = GetInventory(profile, character);
        var items = inventory?["items"] as JsonArray;
        if (inventory == null || items == null) return;

        string? sortingTableId = ReadString(inventory, "sortingTable");
        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand).ToArray())
        {
            string? headBandId = ReadString(headBand, "_id");
            if (string.IsNullOrEmpty(headBandId)) continue;

            bool currencyOccupied = false;
            bool cigaretteOccupied = false;
            foreach (var child in ImmediateChildren(items, headBandId).ToArray())
            {
                string? tpl = ReadString(child, "_tpl");
                if (tpl != null && HeadBandUtilityPolicy.IsCurrencyOrWallet(tpl) && !currencyOccupied)
                {
                    PlaceInGrid(child, DedicatedWearableItems.HeadBandCurrencyGridName);
                    currencyOccupied = true;
                    continue;
                }

                if (tpl != null && HeadBandUtilityPolicy.IsCigarette(tpl) && !cigaretteOccupied)
                {
                    PlaceInGrid(child, DedicatedWearableItems.HeadBandCigarettesGridName);
                    cigaretteOccupied = true;
                    continue;
                }

                // Never delete overflow or an unknown legacy child. PMC profiles have
                // a sorting table; move the whole child subtree root there and leave
                // descendants attached to it. Scav normally has no sorting table, so
                // preserve an unclassifiable child untouched rather than corrupting it.
                if (!string.IsNullOrEmpty(sortingTableId))
                    MoveToSortingTable(child, sortingTableId);
            }
        }
    }

    private static JsonObject? GetInventory(JsonObject profile, string character)
    {
        return profile["characters"]?[character]?["Inventory"] as JsonObject;
    }

    private static IEnumerable<JsonObject> ImmediateChildren(JsonArray items, string parentId)
    {
        foreach (var node in items)
        {
            if (node is JsonObject item && string.Equals(ReadString(item, "parentId"), parentId, StringComparison.Ordinal))
                yield return item;
        }
    }

    private static bool IsHeadBand(JsonObject item)
    {
        return string.Equals(ReadString(item, "_tpl"), RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal);
    }

    private static void PlaceInGrid(JsonObject item, string slotId)
    {
        item["slotId"] = slotId;
        item["location"] = new JsonObject
        {
            ["x"] = 0,
            ["y"] = 0,
            ["r"] = "Horizontal"
        };
    }

    private static void MoveToSortingTable(JsonObject item, string sortingTableId)
    {
        item["parentId"] = sortingTableId;
        item["slotId"] = "hideout";
        item.Remove("location");
    }

    private static bool IsOriginLocation(JsonNode? location)
    {
        if (location is not JsonObject obj) return false;
        int? x = ReadInt(obj, "x");
        int? y = ReadInt(obj, "y");
        return x == 0 && y == 0;
    }

    private static string? ReadString(JsonObject obj, string key)
    {
        try { return obj[key]?.GetValue<string>(); }
        catch { return null; }
    }

    private static int? ReadInt(JsonObject obj, string key)
    {
        try { return obj[key]?.GetValue<int>(); }
        catch { return null; }
    }
}

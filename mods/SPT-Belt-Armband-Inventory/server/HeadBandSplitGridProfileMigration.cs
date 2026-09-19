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
        if (inventory == null || items == null) return false;

        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand))
        {
            string? headBandId = ReadString(headBand, "_id");
            if (string.IsNullOrEmpty(headBandId)) continue;

            int currencyCount = 0;
            int cigaretteCount = 0;
            foreach (var child in ImmediateChildren(items, headBandId))
            {
                string? currentSlot = ReadString(child, "slotId");
                if (string.Equals(currentSlot, DedicatedWearableItems.HeadBandCurrencyGridName, StringComparison.Ordinal)
                    && IsOriginLocation(child["location"])) { currencyCount = 1; continue; }
                if (string.Equals(currentSlot, DedicatedWearableItems.HeadBandCigarettesGridName, StringComparison.Ordinal)
                    && IsOriginLocation(child["location"])) { cigaretteCount = 1; continue; }

                string? tpl = ReadString(child, "_tpl");
                if (tpl != null && HeadBandUtilityPolicy.IsCurrencyOrWallet(tpl))
                {
                    if (currencyCount == 0) return true;
                }
                else if (tpl != null && HeadBandUtilityPolicy.IsCigarette(tpl))
                {
                    if (cigaretteCount == 0) return true;
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

        foreach (var headBand in items.OfType<JsonObject>().Where(IsHeadBand).ToArray())
        {
            string? headBandId = ReadString(headBand, "_id");
            if (string.IsNullOrEmpty(headBandId)) continue;

            JsonObject[] children = ImmediateChildren(items, headBandId).ToArray();
            bool currencyOccupied = children.Any(child =>
                string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCurrencyGridName, StringComparison.Ordinal)
                && IsOriginLocation(child["location"]));
            bool cigaretteOccupied = children.Any(child =>
                string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCigarettesGridName, StringComparison.Ordinal)
                && IsOriginLocation(child["location"]));
            foreach (var child in children)
            {
                if ((string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCurrencyGridName, StringComparison.Ordinal)
                    || string.Equals(ReadString(child, "slotId"), DedicatedWearableItems.HeadBandCigarettesGridName, StringComparison.Ordinal))
                    && IsOriginLocation(child["location"])) continue;
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

                // Unknown and overflow children stay attached. Startup migration must
                // never surprise the player by ejecting HeadBand contents to sorting.
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

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory.Server;

internal static class HeadBandSplitGridMigrationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var profile = JsonNode.Parse("""
        {
          "characters": {
            "pmc": {
              "Inventory": {
                "sortingTable": "sorting",
                "items": [
                  { "_id": "hb", "_tpl": "68ac0000000000000000000f", "parentId": "equipment", "slotId": "16" },
                  { "_id": "roubles", "_tpl": "5449016a4bdc2d6f028b456f", "parentId": "hb", "slotId": "main", "location": { "x": 0, "y": 0, "r": "Horizontal" } },
                  { "_id": "cigs1", "_tpl": "573476d324597737da2adc13", "parentId": "hb", "slotId": "main", "location": { "x": 0, "y": 1, "r": "Horizontal" } },
                  { "_id": "cigs2", "_tpl": "573476f124597737e04bf328", "parentId": "hb", "slotId": "main", "location": { "x": 0, "y": 0, "r": "Horizontal" } },
                  { "_id": "nested", "_tpl": "5449016a4bdc2d6f028b456f", "parentId": "cigs2", "slotId": "nested" }
                ]
              }
            },
            "scav": { "Inventory": { "items": [] } }
          }
        }
        """)!.AsObject();

        var migration = new HeadBandSplitGridProfileMigration();
        if (!migration.CanMigrate(profile, Array.Empty<SPTarkov.Server.Core.Migration.IProfileMigration>()))
            throw new InvalidOperationException("Split-grid migration failed to detect Stable Baseline 1 HeadBand contents.");

        migration.Migrate(profile);

        var items = profile["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();
        JsonObject Find(string id) => items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == id);

        var roubles = Find("roubles");
        if (roubles["parentId"]!.GetValue<string>() != "hb" || roubles["slotId"]!.GetValue<string>() != "main")
            throw new InvalidOperationException("Currency did not remain in the preserved HeadBand main grid.");
        AssertOrigin(roubles);

        var cigs1 = Find("cigs1");
        if (cigs1["parentId"]!.GetValue<string>() != "hb" || cigs1["slotId"]!.GetValue<string>() != "cigarettes")
            throw new InvalidOperationException("Cigarettes did not migrate to the dedicated HeadBand cigarettes grid.");
        AssertOrigin(cigs1);

        var overflow = Find("cigs2");
        if (overflow["parentId"]!.GetValue<string>() != "sorting" || overflow["slotId"]!.GetValue<string>() != "hideout" || overflow.ContainsKey("location"))
            throw new InvalidOperationException("Same-category overflow was not preserved in the sorting table.");

        var nested = Find("nested");
        if (nested["parentId"]!.GetValue<string>() != "cigs2")
            throw new InvalidOperationException("Moving an overflow root must preserve its descendant subtree.");

        if (migration.CanMigrate(profile, Array.Empty<SPTarkov.Server.Core.Migration.IProfileMigration>()))
            throw new InvalidOperationException("Split-grid migration is not idempotent after successful normalization.");
    }

    private static void AssertOrigin(JsonObject item)
    {
        var location = item["location"] as JsonObject;
        if (location == null
            || location["x"]?.GetValue<int>() != 0
            || location["y"]?.GetValue<int>() != 0
            || location["r"]?.GetValue<string>() != "Horizontal")
            throw new InvalidOperationException("Migrated 1x1 HeadBand child was not normalized to grid origin.");
    }
}

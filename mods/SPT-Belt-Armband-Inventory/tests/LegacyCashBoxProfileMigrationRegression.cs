using System;
using System.Linq;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests;

internal static class LegacyCashBoxProfileMigrationRegression
{
    internal static void Run()
    {
        JsonObject profile = JsonNode.Parse("""
        {
          "characters": {
            "pmc": { "Inventory": { "items": [
              { "_id": "cash-box", "_tpl": "669c10fa06c00c483c58537a", "parentId": "stash", "slotId": "hideout", "location": { "x": 0, "y": 8, "r": "Horizontal" } },
              { "_id": "rub", "_tpl": "5449016a4bdc2d6f028b456f", "parentId": "cash-box", "slotId": "main", "location": { "x": 2, "y": 1, "r": "Horizontal" }, "upd": { "StackObjectsCount": 23804 } }
            ] } },
            "scav": { "Inventory": { "items": [] } }
          }
        }
        """)!.AsObject();

        var migration = new LegacyCashBoxProfileMigration();
        if (!migration.CanMigrate(profile, Array.Empty<SPTarkov.Server.Core.Migration.IProfileMigration>()))
            throw new InvalidOperationException("Legacy Small Cash Box must request migration.");

        migration.Migrate(profile);
        JsonArray items = profile["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();
        JsonObject box = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "cash-box");
        JsonObject money = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "rub");

        if (box["_tpl"]!.GetValue<string>() != LegacyCashBoxProfileMigration.CompatibleWalletTemplateId)
            throw new InvalidOperationException("Legacy Small Cash Box was not converted to the compatible WZ Wallet.");
        if (box["location"]!["x"]!.GetValue<int>() != 0 || box["location"]!["y"]!.GetValue<int>() != 8)
            throw new InvalidOperationException("Cash-box stash placement changed during migration.");
        if (money["parentId"]!.GetValue<string>() != "cash-box" || money["upd"]!["StackObjectsCount"]!.GetValue<int>() != 23804)
            throw new InvalidOperationException("Nested money changed during cash-box migration.");
    }
}

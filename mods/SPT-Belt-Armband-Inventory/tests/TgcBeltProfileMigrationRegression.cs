using System;
using System.Linq;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests;

internal static class TgcBeltProfileMigrationRegression
{
    internal static void Run()
    {
        var profile = JsonNode.Parse("""
        {
          "characters": {
            "pmc": { "Inventory": { "sortingTable": "sort", "items": [
              { "_id": "belt", "_tpl": "672e2e75a16c1d2034c384cf", "parentId": "equipment", "slotId": "ArmBand" },
              { "_id": "child", "_tpl": "ammo", "parentId": "belt", "slotId": "main" }
            ] } },
            "scav": { "Inventory": { "items": [] } }
          }
        }
        """)!.AsObject();

        var migration = new TgcBeltProfileMigration();
        Require(migration.CanMigrate(profile, []), "legacy TGC ArmBand placement must be actionable");
        migration.Migrate(profile);
        JsonArray items = profile["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();
        JsonObject belt = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "belt");
        JsonObject child = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "child");
        Require(belt["parentId"]!.GetValue<string>() == "equipment" && belt["slotId"]!.GetValue<string>() == "15",
            "legacy TGC belt must move to dedicated slot15");
        Require(child["parentId"]!.GetValue<string>() == "belt", "TGC belt descendants must remain attached and unchanged");
        Require(!migration.CanMigrate(profile, []), "migration must be idempotent");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TGC profile migration regression failed: " + message);
    }
}

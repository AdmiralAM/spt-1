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
              { "_id": "child", "_tpl": "ammo", "parentId": "belt", "slotId": "main" },
              { "_id": "duplicate", "_tpl": "672e2e750ea81b3b93b943ac", "parentId": "equipment", "slotId": "ArmBand" },
              { "_id": "duplicateChild", "_tpl": "med", "parentId": "duplicate", "slotId": "main" }
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
        JsonObject duplicate = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "duplicate");
        JsonObject duplicateChild = items.OfType<JsonObject>().Single(x => x["_id"]!.GetValue<string>() == "duplicateChild");
        Require(belt["parentId"]!.GetValue<string>() == "equipment" && belt["slotId"]!.GetValue<string>() == "15",
            "legacy TGC belt must move to dedicated slot15");
        Require(child["parentId"]!.GetValue<string>() == "belt", "TGC belt descendants must remain attached and unchanged");
        Require(duplicate["parentId"]!.GetValue<string>() == "sort" && duplicate["slotId"]!.GetValue<string>() == "hideout",
            "duplicate legacy TGC belt must be preserved through the PMC sorting table");
        Require(duplicateChild["parentId"]!.GetValue<string>() == "duplicate",
            "displaced TGC belt descendants must remain attached and unchanged");
        Require(!migration.CanMigrate(profile, []), "migration must be idempotent");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TGC profile migration regression failed: " + message);
    }
}

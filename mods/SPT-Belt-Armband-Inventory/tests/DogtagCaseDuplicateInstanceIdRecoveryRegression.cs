using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseDuplicateInstanceIdRecoveryRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "duplicate-id", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "duplicate-id", "_tpl": "foreign-template", "slotId": "Pockets" },
              { "_id": "foreign-child", "_tpl": "foreign-template", "parentId": "duplicate-id", "slotId": "main" },
              { "_id": "unique-case", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "unique-child", "_tpl": "vanilla-dogtag", "parentId": "unique-case", "slotId": "main" }
            ]
          },
          "Services": [
            { "_id": "foreign-reference", "itemId": "duplicate-id", "kind": "foreign-service" },
            { "_id": "owned-reference", "itemId": "unique-child", "kind": "build-service" }
          ]
        }
        """.Replace("DOGTAG_TPL", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 2)
            throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: both directly owned case templates must be removed.");
        if (cleanup.RemovedReferences != 2)
            throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: only the unique owned descendant/reference chain may cascade.");

        string remaining = profile.ToJsonString();
        if (remaining.Contains("\"_tpl\":\"" + RuntimeIdentity.DogtagCaseItemId + "\"", StringComparison.Ordinal)
            || remaining.Contains("\"_id\":\"unique-child\"", StringComparison.Ordinal)
            || remaining.Contains("\"_id\":\"owned-reference\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: unique owned cleanup chain survived.");

        string[] preserved =
        {
            "foreign-template",
            "foreign-child",
            "foreign-reference"
        };
        foreach (string token in preserved)
            if (!remaining.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: ambiguous owned instance ID crossed into foreign profile state: " + token + ".");

        if (!remaining.Contains("\"parentId\":\"duplicate-id\"", StringComparison.Ordinal)
            || !remaining.Contains("\"itemId\":\"duplicate-id\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: ambiguous exact references must fail closed rather than cascade-delete.");

        ProfileCleanupPolicy.CleanupResult second = ProfileCleanupPolicy.Clean(profile);
        if (second.RemovedItems != 0 || second.RemovedReferences != 0)
            throw new InvalidOperationException("Dogtag duplicate-ID recovery regression failed: second cleanup must remain a strict no-op.");
    }
}
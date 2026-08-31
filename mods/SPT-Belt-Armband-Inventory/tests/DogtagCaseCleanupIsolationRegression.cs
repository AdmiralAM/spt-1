using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseCleanupIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "owned-case", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "owned-child", "_tpl": "vanilla-dogtag", "parentId": "owned-case", "slotId": "main" },
              { "_id": "unrelated-item", "_tpl": "vanilla-item", "slotId": "Pockets", "note": "owned-case" }
            ]
          },
          "Services": [
            { "_id": "exact-ref", "itemId": "owned-child", "kind": "build-service" },
            { "_id": "string-only-ref", "target": "owned-child", "kind": "foreign-schema" },
            { "_id": "substring-ref", "itemId": "owned-child-suffix", "kind": "foreign-item" },
            { "_id": "unrelated-parent", "parentId": "owned-child-suffix", "kind": "foreign-parent" }
          ]
        }
        """.Replace("DOGTAG_TPL", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 1 || cleanup.RemovedReferences != 2)
            throw new InvalidOperationException("Dogtag cleanup isolation regression failed: owned root, child and exact itemId reference must be the only removals.");

        string remaining = profile.ToJsonString();
        if (remaining.Contains("owned-case\"", StringComparison.Ordinal)
            || remaining.Contains("\"_id\":\"owned-child\"", StringComparison.Ordinal)
            || remaining.Contains("\"_id\":\"exact-ref\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag cleanup isolation regression failed: an owned root/descendant or exact service reference survived cleanup.");

        string[] preserved =
        {
            "unrelated-item",
            "string-only-ref",
            "substring-ref",
            "unrelated-parent"
        };
        foreach (string id in preserved)
            if (!remaining.Contains(id, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag cleanup isolation regression failed: cleanup crossed an exact ownership/reference boundary into " + id + ".");

        if (!remaining.Contains("\"target\":\"owned-child\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag cleanup isolation regression failed: unknown foreign-schema string fields must not be interpreted as B&A&HB ownership references.");
        if (!remaining.Contains("\"itemId\":\"owned-child-suffix\"", StringComparison.Ordinal)
            || !remaining.Contains("\"parentId\":\"owned-child-suffix\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag cleanup isolation regression failed: reference matching must remain exact and must not broaden to prefixes/substrings.");

        ProfileCleanupPolicy.CleanupResult second = ProfileCleanupPolicy.Clean(profile);
        if (second.RemovedItems != 0 || second.RemovedReferences != 0)
            throw new InvalidOperationException("Dogtag cleanup isolation regression failed: isolated cleanup must remain idempotent.");
    }
}

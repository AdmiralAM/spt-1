using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagOrdinaryTagRecoveryRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        // Recovery ownership must remain template/tree based across every serialized
        // persistence surface. Canonical BEAR/USEC personal dogtags are deliberately
        // present beside owned case roots in inventory, insurance, mail and direct
        // service/build references. Only references into an owned Case tree may be
        // removed; ordinary personal-tag references remain foreign/vanilla authority.
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "bear-personal", "_tpl": "59f32bb586f774757e1e8442", "slotId": "Dogtag" },
              { "_id": "usec-personal", "_tpl": "59f32c3b86f77472a31742f0", "slotId": "Dogtag" },
              { "_id": "owned-case", "_tpl": "DOGTAG_CASE", "slotId": "Dogtag" },
              { "_id": "owned-case-child", "_tpl": "59f32bb586f774757e1e8442", "parentId": "owned-case", "slotId": "main" }
            ]
          },
          "Insurance": [
            { "_id": "insured-bear-personal", "_tpl": "59f32bb586f774757e1e8442" },
            { "_id": "insured-owned-case", "_tpl": "DOGTAG_CASE" },
            { "_id": "insured-owned-child", "_tpl": "59f32c3b86f77472a31742f0", "parentId": "insured-owned-case", "slotId": "main" }
          ],
          "Mail": {
            "rewards": [
              { "_id": "mail-usec-personal", "_tpl": "59f32c3b86f77472a31742f0" },
              { "_id": "mail-owned-case", "_tpl": "DOGTAG_CASE" },
              { "_id": "mail-owned-child", "_tpl": "59f32bb586f774757e1e8442", "parentId": "mail-owned-case", "slotId": "main" }
            ]
          },
          "Services": [
            { "_id": "bear-personal-ref", "itemId": "bear-personal", "kind": "foreign-build-reference" },
            { "_id": "usec-personal-ref", "itemId": "usec-personal", "kind": "foreign-insurance-reference" },
            { "_id": "owned-case-ref", "itemId": "owned-case", "kind": "owned-tree-reference" },
            { "_id": "owned-child-ref", "itemId": "owned-case-child", "kind": "owned-tree-reference" }
          ]
        }
        """.Replace("DOGTAG_CASE", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 3 || cleanup.RemovedReferences != 5)
            throw new InvalidOperationException("Dogtag ordinary-tag recovery regression failed: owned case roots/children/references were not removed at the exact expected boundary.");

        string remaining = profile.ToJsonString();
        string[] preserved =
        {
            "bear-personal",
            "usec-personal",
            "insured-bear-personal",
            "mail-usec-personal",
            "bear-personal-ref",
            "usec-personal-ref"
        };
        if (preserved.Any(id => !remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag ordinary-tag recovery regression failed: a canonical personal dogtag or its foreign/vanilla direct reference was removed.");

        string[] removed =
        {
            "owned-case",
            "owned-case-child",
            "insured-owned-case",
            "insured-owned-child",
            "mail-owned-case",
            "mail-owned-child",
            "owned-case-ref",
            "owned-child-ref"
        };
        if (removed.Any(id => remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag ordinary-tag recovery regression failed: owned case tree data or its exact direct reference survived cleanup.");

        if (!remaining.Contains("\"itemId\":\"bear-personal\"", StringComparison.Ordinal)
            || !remaining.Contains("\"itemId\":\"usec-personal\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag ordinary-tag recovery regression failed: personal-dogtag service/build references must remain outside B&A&HB cleanup authority.");

        if (!remaining.Contains(DogtagCaseHostContract.BearDogtagTemplateId, StringComparison.Ordinal)
            || !remaining.Contains(DogtagCaseHostContract.UsecDogtagTemplateId, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag ordinary-tag recovery regression failed: canonical BEAR/USEC personal-tag templates did not survive cleanup.");
    }
}

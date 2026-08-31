using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseMultiRootRecoveryRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "ordinary-bear", "_tpl": "59f32bb586f774757e1e8442", "slotId": "Dogtag" },
              { "_id": "case-a", "_tpl": "DOGTAG_CASE", "slotId": "Dogtag" },
              { "_id": "a-child", "_tpl": "59f32c3b86f77472a31742f0", "parentId": "case-a", "slotId": "main" },
              { "_id": "a-grandchild", "_tpl": "vanilla-marker", "parentId": "a-child", "slotId": "marker" },
              { "_id": "case-b", "_tpl": "DOGTAG_CASE", "slotId": "Dogtag" },
              { "_id": "b-child", "_tpl": "59f32bb586f774757e1e8442", "parentId": "case-b", "slotId": "main" },
              { "_id": "unrelated", "_tpl": "vanilla-unrelated", "slotId": "Pockets" }
            ]
          },
          "Builds": [
            { "_id": "ref-a-root", "itemId": "case-a" },
            { "_id": "ref-a-child", "itemId": "a-child" },
            { "_id": "ref-a-grandchild", "itemId": "a-grandchild" },
            { "_id": "ref-b-root", "itemId": "case-b" },
            { "_id": "ref-b-child", "itemId": "b-child" },
            { "_id": "foreign-lookalike", "itemId": "case-a-suffix" },
            { "_id": "foreign-parent-lookalike", "parentId": "prefix-case-b" }
          ]
        }
        """.Replace("DOGTAG_CASE", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 2 || cleanup.RemovedReferences != 8)
            throw new InvalidOperationException("Dogtag multi-root recovery regression failed: cleanup did not compute the exact transitive closure for two independent owned case roots.");

        string remaining = profile.ToJsonString();
        string[] removed =
        {
            "case-a", "a-child", "a-grandchild",
            "case-b", "b-child",
            "ref-a-root", "ref-a-child", "ref-a-grandchild", "ref-b-root", "ref-b-child"
        };
        if (removed.Any(id => remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag multi-root recovery regression failed: an owned root, descendant or exact reference survived cleanup.");

        string[] preserved = { "ordinary-bear", "unrelated", "foreign-lookalike", "case-a-suffix", "foreign-parent-lookalike", "prefix-case-b" };
        if (preserved.Any(id => !remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag multi-root recovery regression failed: cleanup crossed an exact-reference boundary into unrelated/lookalike data.");

        if (!remaining.Contains(DogtagCaseHostContract.BearDogtagTemplateId, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag multi-root recovery regression failed: ordinary personal dogtag in the shared Dogtag host was removed.");

        string beforeSecond = remaining;
        ProfileCleanupPolicy.CleanupResult second = ProfileCleanupPolicy.Clean(profile);
        if (second.RemovedItems != 0 || second.RemovedReferences != 0 || !string.Equals(beforeSecond, profile.ToJsonString(), StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag multi-root recovery regression failed: second cleanup pass was not a strict no-op.");
    }
}

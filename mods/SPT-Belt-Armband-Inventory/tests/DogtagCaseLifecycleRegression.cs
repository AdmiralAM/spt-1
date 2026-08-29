using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseLifecycleRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!PersistentIdentityManifest.IsOwnedTemplate(RuntimeIdentity.DogtagCaseItemId))
            throw new InvalidOperationException("Dogtag Case template must remain in the persistent identity manifest for profile recovery.");
        if (!PersistentIdentityManifest.IsOwnedPersistentId(RuntimeIdentity.DogtagCaseGridId)
            || !PersistentIdentityManifest.IsOwnedPersistentId(RuntimeIdentity.DogtagCaseAssortId))
            throw new InvalidOperationException("Dogtag Case grid/assort identities must remain persistent and recoverable.");

        // The Dogtag Case is a vanilla-slot container, not a custom wearable.
        // Keeping it out of the descriptor registry prevents accidental opt-in to
        // DeathRetention, insurance suppression, build-validation or fast-access
        // capabilities that are intentionally reserved for B&A&HB wearables.
        if (WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.DogtagCaseItemId, out _))
            throw new InvalidOperationException("Dogtag Case must not be registered as a protected/capability-bearing wearable.");

        // Recovery/uninstall semantics must remove both the serialized owned root
        // and every descendant/reference even though this item lives in a vanilla
        // equipment slot rather than a custom slot.
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "dogtag-case-instance", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "dogtag-case-instance", "slotId": "main" },
              { "_id": "unrelated", "_tpl": "vanilla-unrelated", "slotId": "Pockets" }
            ]
          },
          "Builds": [
            { "_id": "build-ref", "itemId": "dogtag-case-instance" }
          ]
        }
        """.Replace("DOGTAG_TPL", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 1 || cleanup.RemovedReferences != 2)
            throw new InvalidOperationException("Dogtag Case profile cleanup must remove its owned root plus child/build references exactly.");

        string remaining = profile.ToJsonString();
        if (remaining.Contains("dogtag-case-instance", StringComparison.Ordinal)
            || remaining.Contains("dogtag-child", StringComparison.Ordinal)
            || remaining.Contains("build-ref", StringComparison.Ordinal)
            || !remaining.Contains("unrelated", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case profile cleanup crossed ownership boundaries or left dangling references.");
    }
}

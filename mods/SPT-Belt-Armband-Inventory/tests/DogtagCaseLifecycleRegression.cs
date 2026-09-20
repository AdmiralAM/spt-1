using System;
using System.Linq;
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

        if (WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.DogtagCaseItemId, out _))
            throw new InvalidOperationException("Dogtag Case must not be registered as a protected/capability-bearing wearable.");

        var vanillaDogtagCaseTree = new[]
        {
            new BeltInventoryNode("equipment", null, null, null),
            new BeltInventoryNode("dogtag-case-instance", "equipment", "Dogtag", RuntimeIdentity.DogtagCaseItemId),
            new BeltInventoryNode("dogtag-child", "dogtag-case-instance", "main", "vanilla-dogtag")
        };
        var wearableRoots = new[]
        {
            new ProtectedWearableRoot(BeltDeathPolicy.ArmBand, RuntimeIdentity.CandidateItemId),
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedBeltWireSlotId, RuntimeIdentity.DedicatedMagazineBeltItemId),
            new ProtectedWearableRoot(RuntimeIdentity.DedicatedHeadBandWireSlotId, RuntimeIdentity.EmergencyHeadBandItemId)
        };
        if (BeltDeathPolicy.GetKeptTreeIds(vanillaDogtagCaseTree, wearableRoots).Count != 0)
            throw new InvalidOperationException("Dogtag Case must not inherit B&A&HB death-retention semantics.");
        string[] lostDogtagTree = { "dogtag-case-instance", "dogtag-child" };
        if (!BeltDeathPolicy.FilterLostInsuredIds(lostDogtagTree, vanillaDogtagCaseTree, wearableRoots).SequenceEqual(lostDogtagTree))
            throw new InvalidOperationException("Dogtag Case must remain untouched by B&A&HB insurance-loss suppression.");

        // Recovery owns exact persistent templates/instance-reference edges only. The
        // shared vanilla Dogtag slot may contain ordinary BEAR/USEC personal tags and
        // unrelated schemas may contain IDs that merely prefix/suffix an owned ID.
        // None of those may become cleanup roots or descendants by slot name or text.
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "ordinary-personal-dogtag", "_tpl": "59f32bb586f774757e1e8442", "slotId": "Dogtag" },
              { "_id": "dogtag-case-instance", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "dogtag-case-instance", "slotId": "main" },
              { "_id": "dogtag-grandchild", "_tpl": "vanilla-marker", "parentId": "dogtag-child", "slotId": "marker" },
              { "_id": "unrelated", "_tpl": "vanilla-unrelated", "slotId": "Pockets" },
              { "_id": "parent-lookalike", "_tpl": "vanilla-unrelated", "parentId": "dogtag-case-instance-suffix" }
            ]
          },
          "Insurance": [
            { "_id": "insured-dogtag-case", "_tpl": "DOGTAG_TPL" },
            { "_id": "insured-dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "insured-dogtag-case", "slotId": "main" },
            { "_id": "insured-dogtag-grandchild", "_tpl": "vanilla-marker", "parentId": "insured-dogtag-child", "slotId": "marker" },
            { "_id": "ordinary-usec-insured", "_tpl": "59f32c3b86f77472a31742f0", "parentId": "insured-dogtag-case-suffix" },
            { "_id": "insured-unrelated", "_tpl": "vanilla-unrelated" }
          ],
          "Mail": {
            "rewards": [
              { "_id": "mail-dogtag-case", "_tpl": "DOGTAG_TPL" },
              { "_id": "mail-dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "mail-dogtag-case", "slotId": "main" },
              { "_id": "mail-dogtag-grandchild", "_tpl": "vanilla-marker", "parentId": "mail-dogtag-child", "slotId": "marker" },
              { "_id": "ordinary-bear-mail", "_tpl": "59f32bb586f774757e1e8442", "itemId": "prefix-mail-dogtag-case" },
              { "_id": "mail-unrelated", "_tpl": "vanilla-unrelated" }
            ]
          },
          "Builds": [
            { "_id": "build-root-ref", "itemId": "dogtag-case-instance" },
            { "_id": "build-child-ref", "itemId": "dogtag-child" },
            { "_id": "build-grandchild-ref", "itemId": "dogtag-grandchild" },
            { "_id": "reference-lookalike", "itemId": "prefix-dogtag-child-suffix" },
            { "_id": "unrelated-build", "itemId": "unrelated" }
          ]
        }
        """.Replace("DOGTAG_TPL", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 3 || cleanup.RemovedReferences != 9)
            throw new InvalidOperationException("Dogtag Case profile cleanup must remove equipment/mail/insurance owned roots plus transitive descendants and references to every removed tree node exactly.");

        string remaining = profile.ToJsonString();
        string[] removedIds =
        {
            "dogtag-case-instance", "dogtag-child", "dogtag-grandchild",
            "build-root-ref", "build-child-ref", "build-grandchild-ref",
            "insured-dogtag-case", "insured-dogtag-child", "insured-dogtag-grandchild",
            "mail-dogtag-case", "mail-dogtag-child", "mail-dogtag-grandchild"
        };
        if (removedIds.Any(id => remaining.Contains("\"_id\":\"" + id + "\"", StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag Case profile cleanup left an owned root, dangling transitive descendant or reference to a removed descendant.");

        string[] preservedIds =
        {
            "ordinary-personal-dogtag", "ordinary-usec-insured", "ordinary-bear-mail",
            "parent-lookalike", "reference-lookalike", "unrelated", "insured-unrelated", "mail-unrelated", "unrelated-build"
        };
        if (preservedIds.Any(id => !remaining.Contains("\"_id\":\"" + id + "\"", StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag Case profile cleanup crossed exact ownership boundaries into ordinary Dogtag-slot, lookalike-reference or unrelated profile data.");
        if (!remaining.Contains("59f32bb586f774757e1e8442", StringComparison.Ordinal)
            || !remaining.Contains("59f32c3b86f77472a31742f0", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case cleanup must preserve ordinary BEAR and USEC personal dogtag templates across shared profile surfaces.");
        if (!remaining.Contains("dogtag-case-instance-suffix", StringComparison.Ordinal)
            || !remaining.Contains("prefix-mail-dogtag-case", StringComparison.Ordinal)
            || !remaining.Contains("prefix-dogtag-child-suffix", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case cleanup must match parentId/itemId references by exact instance identity, never substring/prefix/suffix resemblance.");

        ProfileCleanupPolicy.CleanupResult secondCleanup = ProfileCleanupPolicy.Clean(profile);
        string secondRemaining = profile.ToJsonString();
        if (secondCleanup.RemovedItems != 0 || secondCleanup.RemovedReferences != 0)
            throw new InvalidOperationException("Dogtag Case profile cleanup must be idempotent after the owned tree has been removed.");
        if (!string.Equals(remaining, secondRemaining, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case profile cleanup must not mutate an already-clean profile on a second pass.");
    }
}
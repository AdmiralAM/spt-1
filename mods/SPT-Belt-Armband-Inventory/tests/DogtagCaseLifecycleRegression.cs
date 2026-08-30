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

        // The Dogtag Case is a vanilla-slot container, not a custom wearable.
        // Keeping it out of the descriptor registry prevents accidental opt-in to
        // DeathRetention, insurance suppression, build-validation or fast-access
        // capabilities that are intentionally reserved for B&A&HB wearables.
        if (WearableItemDescriptorRegistry.TryGet(RuntimeIdentity.DogtagCaseItemId, out _))
            throw new InvalidOperationException("Dogtag Case must not be registered as a protected/capability-bearing wearable.");

        // Prove the negative lifecycle boundary explicitly instead of relying only
        // on descriptor absence: a Dogtag-slot case and its child remain entirely
        // vanilla for death and lost-insurance processing.
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

        // Recovery/uninstall semantics must remove serialized owned roots wherever
        // SPT can persist them (equipment/stash, insurance or mail), together with
        // descendants and direct build/service references. This stays schema-
        // agnostic and therefore remains usable when the item template is absent.
        JsonNode profile = JsonNode.Parse("""
        {
          "Inventory": {
            "items": [
              { "_id": "dogtag-case-instance", "_tpl": "DOGTAG_TPL", "slotId": "Dogtag" },
              { "_id": "dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "dogtag-case-instance", "slotId": "main" },
              { "_id": "unrelated", "_tpl": "vanilla-unrelated", "slotId": "Pockets" }
            ]
          },
          "Insurance": [
            { "_id": "insured-dogtag-case", "_tpl": "DOGTAG_TPL" },
            { "_id": "insured-dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "insured-dogtag-case", "slotId": "main" },
            { "_id": "insured-unrelated", "_tpl": "vanilla-unrelated" }
          ],
          "Mail": {
            "rewards": [
              { "_id": "mail-dogtag-case", "_tpl": "DOGTAG_TPL" },
              { "_id": "mail-dogtag-child", "_tpl": "vanilla-dogtag", "parentId": "mail-dogtag-case", "slotId": "main" },
              { "_id": "mail-unrelated", "_tpl": "vanilla-unrelated" }
            ]
          },
          "Builds": [
            { "_id": "build-ref", "itemId": "dogtag-case-instance" },
            { "_id": "unrelated-build", "itemId": "unrelated" }
          ]
        }
        """.Replace("DOGTAG_TPL", RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))!;

        ProfileCleanupPolicy.CleanupResult cleanup = ProfileCleanupPolicy.Clean(profile);
        if (cleanup.RemovedItems != 3 || cleanup.RemovedReferences != 4)
            throw new InvalidOperationException("Dogtag Case profile cleanup must remove equipment/mail/insurance owned roots plus descendants/build references exactly.");

        string remaining = profile.ToJsonString();
        string[] removedIds =
        {
            "dogtag-case-instance", "dogtag-child", "build-ref",
            "insured-dogtag-case", "insured-dogtag-child",
            "mail-dogtag-case", "mail-dogtag-child"
        };
        if (removedIds.Any(id => remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag Case profile cleanup left an owned root or dangling descendant/reference.");

        string[] preservedIds = { "unrelated", "insured-unrelated", "mail-unrelated", "unrelated-build" };
        if (preservedIds.Any(id => !remaining.Contains(id, StringComparison.Ordinal)))
            throw new InvalidOperationException("Dogtag Case profile cleanup crossed ownership boundaries into unrelated profile data.");
    }
}

using System;
using System.Linq;
using SPTBeltArmbandInventory;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Runtime mirror of the B&A&HB #2 MOD SPT persistent identity contract.
/// Any identifier that can be serialized into an SPT profile, inventory tree,
/// trader assort reference, build or service record belongs here. Existing IDs
/// are immutable and must never be repurposed.
/// </summary>
public static class PersistentIdentityManifest
{
    private static readonly string[] LegacyTemplateIds =
    [
        RuntimeIdentity.CandidateItemId,
        RuntimeIdentity.WristWalletItemId,
        RuntimeIdentity.DedicatedMagazineBeltItemId,
        RuntimeIdentity.EmergencyHeadBandItemId,
        RuntimeIdentity.DogtagCaseItemId
    ];

    public static readonly string[] ParentIds =
    [
        RuntimeIdentity.SearchableTemplateParentId,
        RuntimeIdentity.BeltItemParentId,
        RuntimeIdentity.HeadBandItemParentId
    ];

    private static readonly string[] LegacyGridIds =
    [
        RuntimeIdentity.CandidateGridId,
        RuntimeIdentity.WristWalletGridId,
        RuntimeIdentity.DedicatedMagazineBeltGridId,
        RuntimeIdentity.EmergencyHeadBandGridId,
        RuntimeIdentity.EmergencyHeadBandCigarettesGridId,
        RuntimeIdentity.DogtagCaseGridId
    ];

    public static readonly string[] AssortIds =
    [
        RuntimeIdentity.CandidateAssortId,
        RuntimeIdentity.WristWalletAssortId,
        RuntimeIdentity.DedicatedMagazineBeltAssortId,
        RuntimeIdentity.EmergencyHeadBandAssortId,
        RuntimeIdentity.DogtagCaseAssortId
    ];

    public static readonly string[] SlotIds =
    [
        RuntimeIdentity.DedicatedBeltWireSlotId,
        RuntimeIdentity.DedicatedHeadBandWireSlotId
    ];

    public static readonly string[] SlotMongoIds =
    [
        RuntimeIdentity.DedicatedBeltSlotMongoId,
        RuntimeIdentity.DedicatedHeadBandSlotMongoId
    ];

    public static readonly string[] TemplateIds =
        LegacyTemplateIds.Concat(ArmBandVariantCatalog.All.Select(variant => variant.TemplateId)).ToArray();

    public static readonly string[] GridIds =
        LegacyGridIds.Concat(ArmBandVariantCatalog.All.Select(variant => variant.GridId)).ToArray();

    public static bool IsOwnedTemplate(string? templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return false;
        return Array.IndexOf(TemplateIds, templateId) >= 0;
    }

    public static bool IsOwnedPersistentId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return Array.IndexOf(TemplateIds, id) >= 0
            || Array.IndexOf(ParentIds, id) >= 0
            || Array.IndexOf(GridIds, id) >= 0
            || Array.IndexOf(AssortIds, id) >= 0
            || Array.IndexOf(SlotMongoIds, id) >= 0;
    }
}

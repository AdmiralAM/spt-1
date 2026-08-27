namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Authoritative persistent identity manifest for B&A&HB #2 MOD SPT.
/// Any identifier that can be serialized into an SPT profile, inventory tree,
/// trader assort reference, build or service record belongs here. Existing IDs
/// are immutable and must never be repurposed.
/// </summary>
public static class PersistentIdentityManifest
{
    public static readonly string[] TemplateIds =
    [
        RuntimeIdentity.CandidateItemId,
        RuntimeIdentity.WristWalletItemId,
        RuntimeIdentity.DedicatedMagazineBeltItemId,
        RuntimeIdentity.EmergencyHeadBandItemId
    ];

    public static readonly string[] ParentIds =
    [
        RuntimeIdentity.SearchableTemplateParentId,
        RuntimeIdentity.BeltItemParentId,
        RuntimeIdentity.HeadBandItemParentId
    ];

    public static readonly string[] GridIds =
    [
        RuntimeIdentity.CandidateGridId,
        RuntimeIdentity.WristWalletGridId,
        RuntimeIdentity.DedicatedMagazineBeltGridId,
        RuntimeIdentity.EmergencyHeadBandGridId
    ];

    public static readonly string[] AssortIds =
    [
        RuntimeIdentity.CandidateAssortId,
        RuntimeIdentity.WristWalletAssortId,
        RuntimeIdentity.DedicatedMagazineBeltAssortId,
        RuntimeIdentity.EmergencyHeadBandAssortId
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

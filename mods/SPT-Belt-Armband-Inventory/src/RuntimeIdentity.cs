namespace SPTBeltArmbandInventory
{
    // Shared, stable wire contract between the SPT server item definition and
    // the client JsonTypes registration. Existing identifiers must never be
    // repurposed for another accessory category.
    internal static class RuntimeIdentity
    {
        internal const string CandidateItemId = "68ac00000000000000000001";
        internal const string CandidateGridId = "68ac00000000000000000002";
        internal const string CandidateAssortId = "68ac00000000000000000003";
        internal const string SearchableTemplateParentId = "68ac00000000000000000004";
        internal const string BeltItemParentId = "68ac00000000000000000005";

        internal const string WristWalletItemId = "68ac00000000000000000006";
        internal const string WristWalletGridId = "68ac00000000000000000007";
        internal const string WristWalletAssortId = "68ac00000000000000000008";

        // Product identities are human-readable and remain stable inside B&A&HB.
        // EFT 4.1.x InventoryEquipment, however, parses every slot ID through the
        // closed EquipmentSlot enum and indexes a slot cache by its numeric value.
        // The vanilla enum currently ends at ArmBand=14, so the two dedicated
        // locations use collision-checked pseudo-enum values 15 and 16 on the wire.
        internal const string DedicatedBeltSlotName = "BAndHBBelt";
        internal const string DedicatedHeadBandSlotName = "BAndHBHeadBand";
        internal const int DedicatedBeltEquipmentSlotValue = 15;
        internal const int DedicatedHeadBandEquipmentSlotValue = 16;
        internal const string DedicatedBeltWireSlotId = "15";
        internal const string DedicatedHeadBandWireSlotId = "16";
        internal const string DedicatedBeltSlotMongoId = "68ac00000000000000000009";
        internal const string DedicatedHeadBandSlotMongoId = "68ac0000000000000000000a";
        internal const string HeadBandItemParentId = "68ac0000000000000000000b";

        // First concrete items for the two new equipment locations.
        internal const string DedicatedMagazineBeltItemId = "68ac0000000000000000000c";
        internal const string DedicatedMagazineBeltGridId = "68ac0000000000000000000d";
        internal const string DedicatedMagazineBeltAssortId = "68ac0000000000000000000e";
        internal const string EmergencyHeadBandItemId = "68ac0000000000000000000f";
        internal const string EmergencyHeadBandGridId = "68ac00000000000000000010";
        internal const string EmergencyHeadBandAssortId = "68ac00000000000000000011";

        internal const int CandidateGridColumns = 1;
        internal const int CandidateGridRows = 2;
        internal const int WristWalletGridColumns = 1;
        internal const int WristWalletGridRows = 1;
        internal const int DedicatedMagazineBeltGridColumns = 2;
        internal const int DedicatedMagazineBeltGridRows = 2;
        internal const int EmergencyHeadBandGridColumns = 1;
        internal const int EmergencyHeadBandGridRows = 2;
    }
}

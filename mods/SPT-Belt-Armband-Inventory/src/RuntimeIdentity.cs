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

        internal const string DedicatedBeltSlotName = "BAndHBBelt";
        internal const string DedicatedHeadBandSlotName = "BAndHBHeadBand";
        internal const int DedicatedBeltEquipmentSlotValue = 15;
        internal const int DedicatedHeadBandEquipmentSlotValue = 16;
        internal const string DedicatedBeltWireSlotId = "15";
        internal const string DedicatedHeadBandWireSlotId = "16";
        internal const string DedicatedBeltSlotMongoId = "68ac00000000000000000009";
        internal const string DedicatedHeadBandSlotMongoId = "68ac0000000000000000000a";
        internal const string HeadBandItemParentId = "68ac0000000000000000000b";

        internal const string DedicatedMagazineBeltItemId = "68ac0000000000000000000c";
        internal const string DedicatedMagazineBeltGridId = "68ac0000000000000000000d";
        internal const string DedicatedMagazineBeltAssortId = "68ac0000000000000000000e";
        internal const string EmergencyHeadBandItemId = "68ac0000000000000000000f";
        // Preserved from Stable Baseline 1. This remains the currency/wallet grid.
        internal const string EmergencyHeadBandGridId = "68ac00000000000000000010";
        internal const string EmergencyHeadBandAssortId = "68ac00000000000000000011";
        // New post-stable persistent identity: cigarettes-only HeadBand grid.
        internal const string EmergencyHeadBandCigarettesGridId = "68ac00000000000000000012";

        internal const int CandidateGridColumns = 1;
        internal const int CandidateGridRows = 2;
        internal const int WristWalletGridColumns = 1;
        internal const int WristWalletGridRows = 1;
        internal const int DedicatedMagazineBeltGridColumns = 2;
        internal const int DedicatedMagazineBeltGridRows = 2;
        internal const int EmergencyHeadBandGridColumns = 1;
        internal const int EmergencyHeadBandGridRows = 2;
        internal const int EmergencyHeadBandSplitGridColumns = 1;
        internal const int EmergencyHeadBandSplitGridRows = 1;
    }
}

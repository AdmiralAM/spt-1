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

        // Phase 2 proof item. It reuses the already proven searchable ArmBand
        // host/runtime type but has a distinct template/grid/assort identity and
        // independent item capabilities.
        internal const string WristWalletItemId = "68ac00000000000000000006";
        internal const string WristWalletGridId = "68ac00000000000000000007";
        internal const string WristWalletAssortId = "68ac00000000000000000008";

        internal const int CandidateGridColumns = 1;
        internal const int CandidateGridRows = 2;
        internal const int WristWalletGridColumns = 1;
        internal const int WristWalletGridRows = 1;
    }
}

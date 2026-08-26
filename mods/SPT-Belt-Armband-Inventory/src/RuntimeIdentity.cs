namespace SPTBeltArmbandInventory
{
    // Shared, stable wire contract between the SPT server item definition and
    // the client JsonTypes registration. Existing identifiers must never be
    // repurposed for another accessory category.
    internal static class RuntimeIdentity
    {
        internal const string CandidateItemId = "68ac00000000000000000001";
        internal const string CandidateGridId = "68ac00000000000000000002";
        internal const string SearchableTemplateParentId = "68ac00000000000000000004";
        internal const string BeltItemParentId = "68ac00000000000000000005";

        internal const int CandidateGridColumns = 1;
        internal const int CandidateGridRows = 2;
    }
}

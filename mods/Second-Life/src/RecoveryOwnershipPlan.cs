namespace Admiral.SecondLife;

public readonly record struct RecoveryOwnershipPlan(
    string CorpseEquipmentRootId,
    string RecoveryEquipmentRootId,
    EmergencyArmamentPlan? Armament)
{
    public static bool TryCreate(
        string corpseEquipmentRootId,
        string recoveryEquipmentRootId,
        EmergencyArmamentPlan? armament,
        out RecoveryOwnershipPlan plan)
    {
        plan = default;
        if (string.IsNullOrWhiteSpace(corpseEquipmentRootId) ||
            string.IsNullOrWhiteSpace(recoveryEquipmentRootId) ||
            !string.Equals(corpseEquipmentRootId, recoveryEquipmentRootId, StringComparison.Ordinal))
        {
            return false;
        }

        if (armament is EmergencyArmamentPlan selected)
        {
            string[] ids =
            {
                corpseEquipmentRootId,
                selected.PistolItemId,
                selected.InstalledMagazineItemId,
                selected.SpareMagazineItemId
            };
            if (ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            {
                return false;
            }
        }

        plan = new RecoveryOwnershipPlan(corpseEquipmentRootId, recoveryEquipmentRootId, armament);
        return true;
    }
}

namespace Admiral.SecondLife;

public readonly record struct StashMagazine(string ItemId, string TemplateId);

public sealed record StashPistol(
    string ItemId,
    string TemplateId,
    StashMagazine? InstalledMagazine,
    IReadOnlyList<StashMagazine> CompatibleSpareMagazines);

public readonly record struct EmergencyArmamentPlan(
    string PistolItemId,
    string PistolTemplateId,
    string InstalledMagazineItemId,
    string InstalledMagazineTemplateId,
    string SpareMagazineItemId,
    string SpareMagazineTemplateId);

public static class EmergencyArmamentSelector
{
    public static bool TrySelectFromStash(
        IEnumerable<StashPistol> stashPistols,
        int seed,
        out EmergencyArmamentPlan plan)
    {
        if (stashPistols is null) throw new ArgumentNullException(nameof(stashPistols));

        StashPistol[] eligible = stashPistols
            .Where(IsCompleteOwnedSet)
            .Select(pistol => new StashPistol(
                pistol.ItemId,
                pistol.TemplateId,
                pistol.InstalledMagazine,
                pistol.CompatibleSpareMagazines
                    .Where(magazine => IsIdentity(magazine.ItemId) && IsIdentity(magazine.TemplateId))
                    .Where(magazine => magazine.ItemId != pistol.InstalledMagazine!.Value.ItemId)
                    .GroupBy(magazine => magazine.ItemId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(magazine => magazine.ItemId, StringComparer.Ordinal)
                    .ToArray()))
            .Where(pistol => pistol.CompatibleSpareMagazines.Count > 0)
            .GroupBy(pistol => pistol.ItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(pistol => pistol.ItemId, StringComparer.Ordinal)
            .ToArray();

        if (eligible.Length == 0)
        {
            plan = default;
            return false;
        }

        string[] pistolKeys = eligible.Select(pistol => pistol.ItemId).ToArray();
        StashPistol selectedPistol = eligible[DeterministicSelection.Index(seed, pistolKeys)];
        StashMagazine[] spareMagazines = selectedPistol.CompatibleSpareMagazines.ToArray();
        string[] spareKeys = spareMagazines.Select(magazine => magazine.ItemId).ToArray();
        StashMagazine spare = spareMagazines[DeterministicSelection.Index(unchecked(seed * 397) ^ 0x51ed270b, spareKeys)];
        StashMagazine installed = selectedPistol.InstalledMagazine!.Value;

        plan = new EmergencyArmamentPlan(
            selectedPistol.ItemId,
            selectedPistol.TemplateId,
            installed.ItemId,
            installed.TemplateId,
            spare.ItemId,
            spare.TemplateId);
        return true;
    }

    private static bool IsCompleteOwnedSet(StashPistol pistol)
    {
        if (pistol is null || !IsIdentity(pistol.ItemId) || !IsIdentity(pistol.TemplateId)) return false;
        if (pistol.InstalledMagazine is not StashMagazine installed) return false;
        if (!IsIdentity(installed.ItemId) || !IsIdentity(installed.TemplateId)) return false;
        return pistol.CompatibleSpareMagazines is not null;
    }

    private static bool IsIdentity(string value) => !string.IsNullOrWhiteSpace(value);
}

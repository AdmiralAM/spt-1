using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Migration;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Moves TGC 3.0.0 belts out of the vanilla ArmBand location that TGC used
/// before B&amp;A&amp;HB took ownership of their equipment integration. Template IDs,
/// descendants and assets remain TGC-owned and unchanged.
/// </summary>
[Injectable]
public sealed class TgcBeltProfileMigration : AbstractProfileMigration
{
    public override string MigrationName => "BAndHBTgc300DedicatedBeltV1";

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
        => CanFix(profile, "pmc") || CanFix(profile, "scav");

    public override JsonObject? Migrate(JsonObject profile)
    {
        Fix(profile, "pmc");
        Fix(profile, "scav");
        return base.Migrate(profile);
    }

    private static bool CanFix(JsonObject profile, string character)
    {
        JsonObject? inventory = profile["characters"]?[character]?["Inventory"] as JsonObject;
        JsonObject[] items = (inventory?["items"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        if (!items.Any(IsLegacyArmBandBelt)) return false;
        return FindEquipmentParent(items) is string equipment
            && (!HasDedicatedBelt(items, equipment) || Read(inventory!, "sortingTable") != null);
    }

    private static void Fix(JsonObject profile, string character)
    {
        JsonObject? inventory = profile["characters"]?[character]?["Inventory"] as JsonObject;
        JsonObject[] items = (inventory?["items"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        string? equipment = FindEquipmentParent(items);
        if (inventory == null || equipment == null) return;

        bool occupied = HasDedicatedBelt(items, equipment);
        string? sortingTable = Read(inventory, "sortingTable");
        foreach (JsonObject belt in items.Where(IsLegacyArmBandBelt))
        {
            if (!occupied)
            {
                belt["parentId"] = equipment;
                belt["slotId"] = RuntimeIdentity.DedicatedBeltWireSlotId;
                belt.Remove("location");
                occupied = true;
            }
            else if (sortingTable != null)
            {
                belt["parentId"] = sortingTable;
                belt["slotId"] = "hideout";
                belt.Remove("location");
            }
        }
    }

    private static bool IsLegacyArmBandBelt(JsonObject item)
        => TgcCompatibilityPolicy.IsBelt(Read(item, "_tpl") ?? "")
            && string.Equals(Read(item, "slotId"), "ArmBand", StringComparison.Ordinal);

    private static bool HasDedicatedBelt(IEnumerable<JsonObject> items, string equipment)
        => items.Any(item => string.Equals(Read(item, "parentId"), equipment, StringComparison.Ordinal)
            && string.Equals(Read(item, "slotId"), RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal));

    private static string? FindEquipmentParent(IEnumerable<JsonObject> items)
        => items.FirstOrDefault(item => string.Equals(Read(item, "slotId"), "ArmBand", StringComparison.Ordinal)) is JsonObject armBand
            ? Read(armBand, "parentId")
            : null;

    private static string? Read(JsonObject item, string key)
    {
        try { return item[key]?.GetValue<string>(); }
        catch { return null; }
    }
}

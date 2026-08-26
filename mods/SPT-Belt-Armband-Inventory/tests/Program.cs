using System;
using System.Linq;
using SPTBeltArmbandInventory;

internal static class Program
{
    static int assertions;
    static readonly string[] Vanilla =
    {
        BeltSlotPlan.TacticalVest,
        BeltSlotPlan.Pockets,
        BeltSlotPlan.Backpack,
        BeltSlotPlan.SecuredContainer,
        BeltSlotPlan.Dogtag
    };

    static void Main()
    {
        Assert(BeltSlotPlan.IsExpectedContainerPanelOrder(Vanilla), "recognizes SPT 4.1 container order");
        Assert(!BeltSlotPlan.IsExpectedContainerPanelOrder(new[] { BeltSlotPlan.Pockets }), "rejects unrelated enum arrays");

        string[] above = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.AbovePockets, true);
        Assert(Array.IndexOf(above, BeltSlotPlan.ArmBand) + 1 == Array.IndexOf(above, BeltSlotPlan.Pockets), "places belt above pockets");
        Assert(above.Length == Vanilla.Length + 1, "adds exactly one belt row");

        string[] below = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.BelowPockets, true);
        Assert(Array.IndexOf(below, BeltSlotPlan.ArmBand) == Array.IndexOf(below, BeltSlotPlan.Pockets) + 1, "places belt below pockets");
        Assert(below.Length == Vanilla.Length + 1, "below layout adds exactly one row");

        string[] duplicateInput = Vanilla.Concat(new[] { BeltSlotPlan.ArmBand, BeltSlotPlan.ArmBand }).ToArray();
        string[] normalized = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.BelowPockets, true);
        Assert(normalized.Count(x => x == BeltSlotPlan.ArmBand) == 1, "layout is idempotent");

        string[] hidden = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.AbovePockets, false);
        Assert(!hidden.Contains(BeltSlotPlan.ArmBand), "plain armband removes duplicate belt row");
        Assert(hidden.SequenceEqual(Vanilla), "hidden layout preserves vanilla order");

        Assert(!BeltSlotPlan.ShouldExposeBelt(false, false), "empty slot stays hidden");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, false), "plain armband stays hidden");
        Assert(!BeltSlotPlan.ShouldExposeBelt(false, true), "container flag alone is insufficient");
        Assert(BeltSlotPlan.ShouldExposeBelt(true, true), "equipped container exposes belt row");

        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.ArmBand), "ArmBand category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.Belt), "Belt category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.HeadBand), "HeadBand category is supported");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.HeadBand) == AccessoryCapacityBand.Micro, "HeadBand uses micro capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) == AccessoryCapacityBand.Compact, "ArmBand uses compact capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.Belt) == AccessoryCapacityBand.Expanded, "Belt uses expanded capacity band");
        Assert(!AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.Belt, false, true), "category alone cannot expose an empty host");
        Assert(AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.HeadBand, true, true), "container-capable HeadBand may expose a row");

        Assert(AccessoryGridPolicy.IsValid(1, 2), "1x2 grid is valid");
        Assert(AccessoryGridPolicy.IsExactShape(1, 2, 1, 2), "1x2 grid keeps one-column geometry");
        Assert(!AccessoryGridPolicy.IsExactShape(1, 2, 2, 2), "1x2 grid never becomes 2x2");
        Assert(AccessoryGridPolicy.CellCount(1, 2) == 2, "1x2 grid declares exactly two cells");
        Assert(AccessoryGridPolicy.FitsDeclaredCapacity(1, 2, 2), "1x2 grid accepts two items");
        Assert(!AccessoryGridPolicy.FitsDeclaredCapacity(1, 2, 3), "1x2 grid rejects a third item");
        Assert(RuntimeIdentity.CandidateGridColumns == 1 && RuntimeIdentity.CandidateGridRows == 2, "shared runtime contract preserves the proven one-column two-row grid");
        Assert(RuntimeCustomBeltTypePatches.CustomTemplateParentId == RuntimeIdentity.SearchableTemplateParentId, "client template registration uses the shared runtime identity");
        Assert(RuntimeCustomBeltTypePatches.CustomBeltParentId == RuntimeIdentity.BeltItemParentId, "client item registration uses the shared runtime identity");

        Assert(ReflectionTools.HasContainers(new RuntimeContainer { IsContainer = true }), "runtime IsContainer flag is recognized");
        Assert(ReflectionTools.HasContainers(new RuntimeGrids { Grids = new object[] { new object() } }), "runtime Grids are recognized");
        Assert(ReflectionTools.HasContainers(new RuntimeTemplate { Template = new TemplateGrids { Grids = new object[] { new object() } } }), "template Grids are recognized for PackNStrap-style belts");
        Assert(!ReflectionTools.HasContainers(new RuntimeTemplate { Template = new TemplateGrids { Grids = Array.Empty<object>() } }), "empty template grids stay non-container");

        var propertyProbe = new MutablePropertyProbe { Value = "first" };
        Assert((string)ReflectionTools.ReadMember(propertyProbe, "Value") == "first", "reflection cache reads properties");
        propertyProbe.Value = "second";
        Assert((string)ReflectionTools.ReadMember(propertyProbe, "Value") == "second", "cached property accessor reads current values");
        var fieldProbe = new MutableFieldProbe { Value = 7 };
        Assert((int)ReflectionTools.ReadMember(fieldProbe, "Value") == 7, "reflection cache reads fields");
        fieldProbe.Value = 11;
        Assert((int)ReflectionTools.ReadMember(fieldProbe, "Value") == 11, "cached field accessor reads current values");
        Assert(ReflectionTools.ReadMember(fieldProbe, "Missing") == null, "missing members remain safe after lookup caching");

        string[] unusual = { BeltSlotPlan.TacticalVest, BeltSlotPlan.Backpack, BeltSlotPlan.SecuredContainer, BeltSlotPlan.Dogtag };
        string[] fallback = BeltSlotPlan.Build(unusual, BeltSlotPosition.AbovePockets, true);
        Assert(fallback[fallback.Length - 1] == BeltSlotPlan.ArmBand, "missing pockets falls back safely");
        Assert(unusual.SequenceEqual(BeltSlotPlan.Build(unusual, BeltSlotPosition.BelowPockets, false)), "disabled fallback does not invent slots");

        Assert(LootPriorityPlan.Build(LootItemKind.Magazine, true).SequenceEqual(new[] { "Vest", "Belt", "Pockets", "Backpack", "Secure" }), "magazines prioritize belt after vest");
        Assert(LootPriorityPlan.Build(LootItemKind.Ammo, true).SequenceEqual(new[] { "Belt", "Vest", "Pockets", "Backpack", "Secure" }), "ammo prioritizes belt first");
        Assert(LootPriorityPlan.Build(LootItemKind.Money, true).SequenceEqual(new[] { "Secure", "Backpack", "Vest", "Belt", "Pockets" }), "money keeps secure/backpack first and includes belt");
        Assert(LootPriorityPlan.Build(LootItemKind.Throwable, true).SequenceEqual(new[] { "Pockets", "Belt", "Vest", "Backpack", "Secure" }), "throwables prioritize belt after pockets");
        Assert(LootPriorityPlan.Build(LootItemKind.Other, true).SequenceEqual(new[] { "Backpack", "Vest", "Belt", "Pockets", "Secure" }), "general loot includes belt after vest");
        Assert(LootPriorityPlan.Build(LootItemKind.Other, false).SequenceEqual(new[] { "Backpack", "Vest", "Pockets", "Secure" }), "no belt preserves vanilla general priority");

        Assert(ScavBeltPolicy.ShouldRestore(true, true, true), "deleted Scav ArmBand is restored for an equipped container belt");
        Assert(!ScavBeltPolicy.ShouldRestore(true, true, false), "plain Scav armband remains vanilla-deleted");
        Assert(!ScavBeltPolicy.ShouldRestore(true, false, false), "empty Scav ArmBand remains vanilla-deleted");
        Assert(!ScavBeltPolicy.ShouldRestore(false, true, true), "already-visible ArmBand is not modified");

        Assert(GrenadeSlotPolicy.ShouldIncludeBelt(true, true), "container belt participates in grenade fast-access slots");
        Assert(!GrenadeSlotPolicy.ShouldIncludeBelt(true, false), "plain armband is never a grenade slot");
        Assert(!GrenadeSlotPolicy.ShouldIncludeBelt(false, true), "container flag without an equipped item is ignored");
        Assert(GrenadeSlotPolicy.ShouldAppendGrenade(true, true, false), "examined belt grenade is appended to vanilla grenade enumeration");
        Assert(!GrenadeSlotPolicy.ShouldAppendGrenade(false, true, false), "non-grenade belt item is ignored by grenade enumeration");
        Assert(!GrenadeSlotPolicy.ShouldAppendGrenade(true, false, false), "unexamined belt grenade preserves vanilla examination filtering");
        Assert(!GrenadeSlotPolicy.ShouldAppendGrenade(true, true, true), "existing grenade reference is not duplicated");

        Assert(HarmonyInstallPolicy.CanBegin(true, true, true, true), "Harmony mutations begin only when patch and rollback APIs are ready");
        Assert(!HarmonyInstallPolicy.CanBegin(true, true, true, false), "Harmony mutations fail closed when rollback is unavailable");

        object installedMutation = new object();
        Assert(RuntimeMutationPolicy.ShouldRestore(installedMutation, installedMutation), "temporary mutation restores while Belt still owns the installed value");
        Assert(!RuntimeMutationPolicy.ShouldRestore(new object(), installedMutation), "temporary mutation does not overwrite a later replacement from another mod");
        Assert(!RuntimeMutationPolicy.ShouldRestore(null, installedMutation), "temporary mutation does not restore after external clearing");

        Action<string> ownedInfoLogger = _ => { };
        Action<string> ownedWarningLogger = _ => { };
        Action<string> replacementLogger = _ => { };
        RuntimeCustomBeltTypes.LogInfo = ownedInfoLogger;
        RuntimeCustomBeltTypes.LogWarning = ownedWarningLogger;
        RuntimeCustomBeltTypes.ReleaseLogging(replacementLogger, ownedWarningLogger);
        Assert(ReferenceEquals(RuntimeCustomBeltTypes.LogInfo, ownedInfoLogger), "runtime type cleanup preserves a later or foreign info logger");
        Assert(RuntimeCustomBeltTypes.LogWarning == null, "runtime type cleanup releases its owned warning logger");
        RuntimeCustomBeltTypes.ReleaseLogging(ownedInfoLogger, null);
        Assert(RuntimeCustomBeltTypes.LogInfo == null, "runtime type cleanup releases its owned info logger");

        Assert(PickupSlotPolicy.ShouldTry(true, true, false, true), "compatible container can fall back to ArmBand when vanilla pickup has no slot");
        Assert(!PickupSlotPolicy.ShouldTry(false, true, false, true), "vanilla pickup result always wins");
        Assert(!PickupSlotPolicy.ShouldTry(true, false, false, true), "non-container item never uses belt pickup fallback");
        Assert(!PickupSlotPolicy.ShouldTry(true, true, true, true), "deleted ArmBand slot is never revived by pickup fallback");
        Assert(!PickupSlotPolicy.ShouldTry(true, true, false, false), "incompatible container is never forced into ArmBand");

        Assert(PaymentSlotPolicy.ShouldIncludeBelt(true, true), "container belt participates in in-raid trader-service payment slots");
        Assert(!PaymentSlotPolicy.ShouldIncludeBelt(true, false), "plain armband never becomes a payment slot");
        Assert(!PaymentSlotPolicy.ShouldIncludeBelt(false, true), "empty ArmBand never becomes a payment slot");

        string[] vanillaBuildContainers = EquipmentBuildContainerPolicy.VanillaContainerSlots.ToArray();
        string[] buildContainers = EquipmentBuildContainerPolicy.Build(vanillaBuildContainers);
        Assert(buildContainers.SequenceEqual(new[] { BeltSlotPlan.TacticalVest, BeltSlotPlan.Pockets, BeltSlotPlan.Backpack, BeltSlotPlan.SecuredContainer, BeltSlotPlan.ArmBand }), "equipment-build container validation includes ArmBand after vanilla containers");
        Assert(vanillaBuildContainers.SequenceEqual(EquipmentBuildContainerPolicy.VanillaContainerSlots), "equipment-build policy does not mutate the vanilla source array");
        string[] changedBuildContainers = { BeltSlotPlan.TacticalVest, BeltSlotPlan.Pockets };
        Assert(ReferenceEquals(changedBuildContainers, EquipmentBuildContainerPolicy.Build(changedBuildContainers)), "unexpected build-container shapes fail closed");
        Assert(ReferenceEquals(buildContainers, EquipmentBuildContainerPolicy.Build(buildContainers)), "already-extended build-container list stays unchanged");

        string[] vanillaFastAccess = { BeltSlotPlan.Pockets, BeltSlotPlan.TacticalVest };
        string[] extendedFastAccess = FastAccessSlotPolicy.Extend(vanillaFastAccess);
        Assert(extendedFastAccess.SequenceEqual(new[] { BeltSlotPlan.Pockets, BeltSlotPlan.TacticalVest, BeltSlotPlan.ArmBand }), "fast-access slot policy adds ArmBand after vanilla reachable containers");
        Assert(vanillaFastAccess.SequenceEqual(new[] { BeltSlotPlan.Pockets, BeltSlotPlan.TacticalVest }), "fast-access slot policy does not mutate vanilla source array");
        Assert(FastAccessSlotPolicy.Extend(extendedFastAccess).Count(x => x == BeltSlotPlan.ArmBand) == 1, "fast-access slot extension is idempotent");
        Assert(FastAccessSlotPolicy.Extend(null) == null, "missing fast-access slot source fails closed");

        var deathTree = new[]
        {
            new BeltInventoryNode("equipment", null, null),
            new BeltInventoryNode("belt", "equipment", BeltDeathPolicy.ArmBand),
            new BeltInventoryNode("grid-item", "belt", "main"),
            new BeltInventoryNode("nested-container", "belt", "main"),
            new BeltInventoryNode("nested-item", "nested-container", "main"),
            new BeltInventoryNode("vest", "equipment", "TacticalVest"),
            new BeltInventoryNode("vest-item", "vest", "main")
        };
        var keptTree = BeltDeathPolicy.GetKeptTreeIds(deathTree);
        Assert(keptTree.SetEquals(new[] { "belt", "grid-item", "nested-container", "nested-item" }), "death retention keeps exactly the ArmBand tree");
        Assert(BeltDeathPolicy.ShouldKeep("nested-item", deathTree), "nested belt contents survive death");
        Assert(!BeltDeathPolicy.ShouldKeep("vest-item", deathTree), "unrelated equipment contents keep vanilla death rules");
        Assert(BeltDeathPolicy.FilterLostInsuredIds(new[] { "belt", "grid-item", "nested-item", "vest-item" }, deathTree).SequenceEqual(new[] { "vest-item" }), "insurance loss excludes belt root and descendants");
        Assert(BeltDeathPolicy.GetKeptTreeIds(new[] { new BeltInventoryNode("vest", "equipment", "TacticalVest") }).Count == 0, "profiles without ArmBand are untouched");

        Console.WriteLine("SPT Belt/Armband Inventory Phase 1: " + assertions + " assertions passed.");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        assertions++;
    }

    sealed class RuntimeContainer { public bool IsContainer { get; set; } }
    sealed class RuntimeGrids { public object[] Grids { get; set; } }
    sealed class RuntimeTemplate { public TemplateGrids Template { get; set; } }
    sealed class TemplateGrids { public object[] Grids { get; set; } }
    sealed class MutablePropertyProbe { public string Value { get; set; } }
    sealed class MutableFieldProbe { public int Value; }
}

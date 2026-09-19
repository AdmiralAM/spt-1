using BepInEx;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SPTStackableArmorPlates.Client;

[BepInPlugin("com.admiralam.stackable-armor-plates.client", "Admiral Stackable Armor Plates", "0.1.0")]
[BepInDependency("com.lacyway.mc", "1.6.2")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        new Harmony("com.admiralam.stackable-armor-plates.client").PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Armor plate stack durability guards enabled.");
    }
}

internal static class PlateMergeGuard
{
    private const string ArmorPlateParentId = "644120aa86ffbe10ee032b6f";

    public static bool CanCombine(Item source, Item target)
    {
        if (!IsStandalonePlate(source) || !IsStandalonePlate(target))
        {
            return true;
        }

        RepairableComponent sourceRepair = source.GetItemComponent<RepairableComponent>();
        RepairableComponent targetRepair = target.GetItemComponent<RepairableComponent>();
        return sourceRepair != null
            && targetRepair != null
            && PlateStackPolicy.DurabilityMatches(
                sourceRepair.Durability,
                sourceRepair.MaxDurability,
                targetRepair.Durability,
                targetRepair.MaxDurability);
    }

    private static bool IsStandalonePlate(Item item)
        => item is ArmorPlate && item.Template.ParentId?.ToString() == ArmorPlateParentId;
}

[HarmonyPatch(typeof(ItemManipulator), nameof(ItemManipulator.Merge),
    new[] { typeof(Item), typeof(Item), typeof(ItemController), typeof(bool) })]
internal static class MergeDurabilityPatch
{
    private static bool Prefix(Item item, Item targetItem, ref OperationResult<MergeResult> __result)
    {
        if (PlateMergeGuard.CanCombine(item, targetItem))
        {
            return true;
        }

        __result = new StringError("Armor plates must have matching durability to stack");
        return false;
    }
}

[HarmonyPatch(typeof(ItemManipulator), nameof(ItemManipulator.TransferMax),
    new[] { typeof(Item), typeof(Item), typeof(int), typeof(ItemController), typeof(bool) })]
internal static class TransferDurabilityPatch
{
    private static bool Prefix(Item item, Item targetItem, ref OperationResult<TransferResult> __result)
    {
        if (PlateMergeGuard.CanCombine(item, targetItem))
        {
            return true;
        }

        __result = new StringError("Armor plates must have matching durability to stack");
        return false;
    }
}

// ArmorPlate is not a StackableItem, so the normal drag/drop action resolver can
// choose swap/move even after the server raises StackMaxSize. Route an exact
// plate-on-plate action through the native merge operation; the durability guard
// above remains the authority for whether the two instances may share a stack.
[HarmonyPatch(typeof(ItemController), nameof(ItemController.ExecutePossibleAction))]
internal static class PlateCombineActionPatch
{
    private static bool Prefix(
        ItemController __instance,
        ItemContext itemContext,
        Item targetItem,
        bool simulate,
        ref OperationResult __result)
    {
        Item sourceItem = itemContext?.Item;
        if (sourceItem is not ArmorPlate || targetItem is not ArmorPlate)
        {
            return true;
        }

        __result = ItemManipulator.Merge(sourceItem, targetItem, __instance, simulate);
        return false;
    }
}

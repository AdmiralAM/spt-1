using BepInEx;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using System;
using System.Linq;

namespace SPTStackableArmorPlates.Client;

[BepInPlugin("com.admiralam.stackable-armor-plates.client", "Admiral Armor Plate Field Repair", "0.2.0")]
[BepInDependency("com.lacyway.mc", "1.6.2")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        const string harmonyId = "com.admiralam.stackable-armor-plates.client";
        var harmony = new Harmony(harmonyId);
        harmony.PatchAll(typeof(Plugin).Assembly);

        var target = AccessTools.Method(
            typeof(ItemController),
            nameof(ItemController.ExecutePossibleAction),
            new[] { typeof(ItemContext), typeof(Item), typeof(bool), typeof(bool) });
        if (target == null || Harmony.GetPatchInfo(target)?.Owners.Contains(harmonyId) != true)
            throw new InvalidOperationException("Armor plate field-repair action patch was not installed.");
    }
}

[HarmonyPatch(typeof(ItemController), nameof(ItemController.ExecutePossibleAction),
    new[] { typeof(ItemContext), typeof(Item), typeof(bool), typeof(bool) })]
internal static class PlateFieldRepairPatch
{
    private const float Epsilon = 0.001f;

    private static bool Prefix(
        ItemController __instance,
        ItemContext itemContext,
        Item targetItem,
        bool simulate,
        ref OperationResult __result)
    {
        if (itemContext?.Item is not ArmorPlate source || targetItem is not ArmorPlate target || source == target)
            return true;

        RepairableComponent sourceRepair = source.GetItemComponent<RepairableComponent>();
        RepairableComponent targetRepair = target.GetItemComponent<RepairableComponent>();
        if (sourceRepair == null || targetRepair == null
            || IsFull(sourceRepair) || IsFull(targetRepair)
            || !SharePlateSlot(source, target))
        {
            return true;
        }

        Item consumed;
        RepairableComponent survivorRepair;
        if (sourceRepair.Durability <= targetRepair.Durability)
        {
            consumed = source;
            survivorRepair = targetRepair;
        }
        else
        {
            consumed = target;
            survivorRepair = sourceRepair;
        }

        RepairableComponent consumedRepair = consumed.GetItemComponent<RepairableComponent>();
        OperationResult<RemoveResult> removal = ItemManipulator.Remove(consumed, __instance, simulate);
        if (removal.Failed)
        {
            __result = removal.Error;
            return false;
        }

        if (!simulate)
            survivorRepair.Durability = Math.Min(survivorRepair.MaxDurability,
                survivorRepair.Durability + consumedRepair.Durability);

        __result = removal;
        return false;
    }

    private static bool IsFull(RepairableComponent repairable)
        => repairable.Durability >= repairable.MaxDurability - Epsilon;

    private static bool SharePlateSlot(ArmorPlate first, ArmorPlate second)
    {
        if (first.Template is not ArmoredEquipmentTemplate firstTemplate
            || second.Template is not ArmoredEquipmentTemplate secondTemplate)
        {
            return false;
        }

        EArmorPlateCollider firstSlots = firstTemplate.ArmorPlateColliders;
        EArmorPlateCollider secondSlots = secondTemplate.ArmorPlateColliders;
        return firstSlots != 0 && secondSlots != 0 && (firstSlots & secondSlots) != 0;
    }
}

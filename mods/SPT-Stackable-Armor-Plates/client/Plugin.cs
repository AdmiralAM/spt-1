using BepInEx;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using System;
using System.Linq;

namespace SPTStackableArmorPlates.Client;

[BepInPlugin("com.admiralam.stackable-armor-plates.client", "Admiral Stackable Armor Plates", "0.1.0")]
[BepInDependency("com.lacyway.mc", "1.6.2")]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        const string harmonyId = "com.admiralam.stackable-armor-plates.client";
        var harmony = new Harmony(harmonyId);
        harmony.PatchAll(typeof(Plugin).Assembly);

        var plateDropTarget = AccessTools.Method(
            typeof(ItemController),
            nameof(ItemController.ExecutePossibleAction),
            new[] { typeof(ItemContext), typeof(Item), typeof(bool), typeof(bool) });
        if (plateDropTarget == null || Harmony.GetPatchInfo(plateDropTarget)?.Owners.Contains(harmonyId) != true)
        {
            throw new InvalidOperationException("Armor plate item-on-item action patch was not installed.");
        }

        Logger.LogInfo("Armor plate exact item-on-item merge action enabled; native template matching remains authoritative.");
    }
}

// ArmorPlate is not a StackableItem, so the normal drag/drop action resolver can
// choose swap/move even after the server raises StackMaxSize. Route an exact
// plate-on-plate action through the native merge operation. Native merge keeps
// the required same-template rule and rejects different plate templates.
[HarmonyPatch(typeof(ItemController), nameof(ItemController.ExecutePossibleAction),
    new[] { typeof(ItemContext), typeof(Item), typeof(bool), typeof(bool) })]
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

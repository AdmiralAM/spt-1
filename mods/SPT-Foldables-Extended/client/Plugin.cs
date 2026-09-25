using System;
using System.Linq;
using BepInEx;
using EFT.InventoryLogic;
using Foldables.Models.Items;
using HarmonyLib;

namespace SPTFoldablesExtended.Client;

[BepInPlugin("com.admiralam.foldables-extended", "Admiral Foldables Extended", "0.1.0")]
[BepInDependency("com.ozen.foldables", "1.1.1")]
public sealed class Plugin : BaseUnityPlugin
{
    private const string ArmorId = "5448e54d4bdc2dcc718b4568";
    private const string HeadwearId = "5a341c4086f77401f2541505";
    private const string FaceCoverId = "5a341c4686f77469e155819e";
    private const string PosterId = "6759673c76e93d8eb20b2080";
    private static readonly string[] ArenaPosterPackIds =
    [
        "664a5775f3d3570fba06be64",
        "664b69c5a082271bc46c4e11",
        "664b69e8e1238e506d3630af",
        "664b69f3a082271bc46c4e13"
    ];

    private void Awake()
    {
        Register<FoldableArmor, FoldableArmorTemplate>(ArmorId, static (id, template) => new FoldableArmor(id, template));
        Register<FoldableHeadwear, FoldableHeadwearTemplate>(HeadwearId, static (id, template) => new FoldableHeadwear(id, template));
        Register<FoldableFaceCover, FoldableFaceCoverTemplate>(FaceCoverId, static (id, template) => new FoldableFaceCover(id, template));
        Register<FoldablePoster, FoldablePosterTemplate>(PosterId, static (id, template) => new FoldablePoster(id, template));
        foreach (string posterPackId in ArenaPosterPackIds)
        {
            Register<FoldablePoster, FoldablePosterTemplate>(posterPackId, static (id, template) => new FoldablePoster(id, template));
        }

        var harmony = new Harmony("com.admiralam.foldables-extended");
        harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Foldables Extended mappings and removable-content integration enabled.");
    }

    private static void Register<TItem, TTemplate>(string taxonomyId, Func<string, TTemplate, TItem> constructor)
        where TItem : Item
        where TTemplate : ItemTemplate
    {
        JsonTypes.TypeTable[taxonomyId] = typeof(TItem);
        JsonTypes.TemplateTypeTable[taxonomyId] = typeof(TTemplate);
        JsonTypes.ItemConstructors[taxonomyId] = (id, template) => constructor(id, (TTemplate)template);

        Type sorterBase = typeof(CompoundItem).IsAssignableFrom(typeof(TItem))
            ? typeof(CompoundItem)
            : typeof(Item);
        int itemIndex = ItemSorter.IndexOf(sorterBase);
        if (itemIndex < 0)
        {
            itemIndex = ItemSorter.IndexOf(typeof(Item));
        }
        if (!ItemSorter._itemSuccessors.Contains(typeof(TItem)))
        {
            ItemSorter._itemSuccessors.Insert(itemIndex, typeof(TItem));
        }
    }
}

[HarmonyPatch(typeof(ItemManipulator), nameof(ItemManipulator.CanFold))]
internal static class ExtendedItemCanFoldPatch
{
    private static void Postfix(Item item, ref FoldableComponent foldable, ref bool __result)
    {
        if (__result || item is not IFoldable)
        {
            return;
        }

        foldable = item.GetItemComponent<FoldableComponent>();
        __result = foldable?.CanBeFolded == true;
    }
}

[HarmonyPatch]
internal static class RemovableContentRequiredPatch
{
    private static System.Reflection.MethodBase TargetMethod()
        => AccessTools.Method("Foldables.Utils.ItemHelper:RequiresEmptyingBeforeFold");

    private static void Postfix(Item item, ref bool __result)
    {
        if (__result || item is not IFoldable { Folded: false } || item is not CompoundItem compound)
        {
            return;
        }

        __result = compound.Slots.Any(slot => !slot.Locked && slot.ContainedItem != null);
    }
}

[HarmonyPatch]
internal static class RemovableContentMovePatch
{
    private static System.Reflection.MethodBase TargetMethod()
        => AccessTools.Method("Foldables.Utils.ItemHelper:TryMoveContainedItemsToParent");

    private static bool Prefix(Item rootItem, InventoryController inventoryController, bool simulate, ref bool __result)
    {
        if (rootItem is not IFoldable || rootItem is not CompoundItem compound || !compound.Slots.Any(slot => !slot.Locked && slot.ContainedItem != null))
        {
            return true;
        }

        __result = RemovableContentMover.TryMove(rootItem, compound, inventoryController, simulate);
        return false;
    }
}

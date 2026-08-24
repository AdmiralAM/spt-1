using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SPTBeltArmbandInventory.Server.Patches;

public sealed class IsItemKeptAfterDeathPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(InRaidHelper).GetMethod(
            "IsItemKeptAfterDeath",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [PatchPostfix]
    public static void Postfix(PmcData pmcData, Item itemToCheck, ref bool __result)
    {
        if (__result) return;
        var inventoryItems = pmcData.Inventory?.Items;
        if (inventoryItems is null) return;

        var nodes = inventoryItems.Select(item => new BeltInventoryNode(
            item.Id.ToString(),
            item.ParentId?.ToString(),
            item.SlotId));

        if (BeltDeathPolicy.ShouldKeep(itemToCheck.Id.ToString(), nodes))
            __result = true;
    }
}

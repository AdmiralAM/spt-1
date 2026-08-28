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
        var methods = typeof(InRaidHelper).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        MethodInfo? selected = null;
        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, "IsItemKeptAfterDeath", StringComparison.Ordinal) || method.ReturnType != typeof(bool))
                continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(PmcData) || parameters[1].ParameterType != typeof(Item))
                continue;
            if (selected is not null)
                throw new AmbiguousMatchException("Multiple exact InRaidHelper.IsItemKeptAfterDeath(PmcData, Item) methods found.");
            selected = method;
        }
        return selected;
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
            item.SlotId,
            item.Template.ToString()));

        if (BeltDeathPolicy.ShouldKeep(itemToCheck.Id.ToString(), nodes, WearableProtectionRuntime.ActiveRoots))
            __result = true;
    }
}

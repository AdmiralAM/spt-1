using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Services.InRaid;

namespace SPTBeltArmbandInventory.Server.Patches;

[Injectable]
public sealed class HandleInsuredItemLostEventPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        MethodInfo? selected = null;
        foreach (var method in typeof(LocationLifecycleService).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!string.Equals(method.Name, "HandleInsuredItemLostEvent", StringComparison.Ordinal)
                || method.ReturnType != typeof(void))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length != 4
                || parameters[0].ParameterType != typeof(MongoId)
                || parameters[1].ParameterType != typeof(PmcData)
                || parameters[2].ParameterType != typeof(EndLocalRaidRequestData)
                || parameters[3].ParameterType != typeof(string))
                continue;

            if (selected is not null)
                throw new AmbiguousMatchException("Multiple exact LocationLifecycleService.HandleInsuredItemLostEvent(MongoId, PmcData, EndLocalRaidRequestData, string) methods found; insurance retention refused.");
            selected = method;
        }
        return selected;
    }

    [PatchPrefix]
    public static void Prefix(PmcData preRaidPmcProfile, EndLocalRaidRequestData request)
    {
        var lostInsuredItems = request.LostInsuredItems;
        var inventoryItems = preRaidPmcProfile.Inventory?.Items;
        if (lostInsuredItems is null || !lostInsuredItems.Any() || inventoryItems is null)
            return;

        var nodes = inventoryItems.Select(item => new BeltInventoryNode(
            item.Id.ToString(),
            item.ParentId?.ToString(),
            item.SlotId,
            item.Template.ToString()));
        ProtectedWearableRoot[] roots = WearableProtectionRuntime.ActiveRoots;
        var kept = BeltDeathPolicy.GetKeptTreeIds(nodes, roots);
        if (kept.Count == 0) return;

        request.LostInsuredItems = lostInsuredItems
            .Where(item => !kept.Contains(item.Id.ToString()))
            .ToList();
    }
}

using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Services.InRaid;

namespace SPTBeltArmbandInventory.Server.Patches;

public sealed class HandleInsuredItemLostEventPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(LocationLifecycleService).GetMethod(
            "HandleInsuredItemLostEvent",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [PatchPrefix]
    public static void Prefix(PmcData preRaidPmcProfile, EndLocalRaidRequestData request)
    {
        if (request.LostInsuredItems is null || request.LostInsuredItems.Count == 0 || preRaidPmcProfile.Inventory?.Items is null)
            return;

        var nodes = preRaidPmcProfile.Inventory.Items.Select(item => new BeltInventoryNode(
            item.Id.ToString(),
            item.ParentId?.ToString(),
            item.SlotId));
        var kept = BeltDeathPolicy.GetKeptTreeIds(nodes);
        if (kept.Count == 0) return;

        request.LostInsuredItems = request.LostInsuredItems
            .Where(item => !kept.Contains(item.Id.ToString()))
            .ToList();
    }
}

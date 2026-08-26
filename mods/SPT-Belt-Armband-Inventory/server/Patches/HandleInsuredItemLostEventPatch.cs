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
        var lostInsuredItems = request.LostInsuredItems;
        var inventoryItems = preRaidPmcProfile.Inventory?.Items;
        if (lostInsuredItems is null || !lostInsuredItems.Any() || inventoryItems is null)
            return;

        var nodes = inventoryItems.Select(item => new BeltInventoryNode(
            item.Id.ToString(),
            item.ParentId?.ToString(),
            item.SlotId,
            item.Template.ToString()));
        var kept = BeltDeathPolicy.GetKeptTreeIds(nodes, RuntimeCandidateBeltItem.RuntimeCandidateTpl);
        if (kept.Count == 0) return;

        request.LostInsuredItems = lostInsuredItems
            .Where(item => !kept.Contains(item.Id.ToString()))
            .ToList();
    }
}

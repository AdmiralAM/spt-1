using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AdmiralTrader.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1), UsedImplicitly]
public sealed class BeltProgressionRuntime(TemplateTable templates, TradersTable traders, ISptLogger<BeltProgressionRuntime> logger) : IOnLoad
{
    internal const string BeltParent = "68ac00000000000000000005";
    internal const string FenceId = "579dc571d53a0658a154fbec";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var belts = templates.Items.Values.Where(x => x.Parent.ToString() == BeltParent)
            .Select(x => (Item:x, Cells:x.Properties?.Grids?.Sum(g => (g.Properties?.CellsH ?? 0) * (g.Properties?.CellsV ?? 0)) ?? 0))
            .Where(x => x.Cells >= 4).ToArray();
        foreach (var belt in belts)
        {
            var floor = BeltPriceFloor(belt.Cells);
            var handbook = templates.Handbook.Items.FirstOrDefault(x => x.Id == belt.Item.Id);
            if (handbook is not null && handbook.Price < floor) handbook.Price = floor;
        }
        var ids = belts.Select(x => x.Item.Id).ToHashSet();
        var removed = 0;
        if (ids.Count > 0 && traders.TryGetValue(new MongoId(FenceId), out var fence))
        {
            var roots = fence.Assort.Items.Where(x => x.ParentId?.ToString() == "hideout" && ids.Contains(x.Template)).Select(x => x.Id).ToHashSet();
            var all = new HashSet<MongoId>(roots);
            bool changed;
            do { changed = false; foreach (var x in fence.Assort.Items) if (x.ParentId is { } p && all.Contains(p) && all.Add(x.Id)) changed = true; } while (changed);
            removed = roots.Count;
            fence.Assort.Items.RemoveAll(x => all.Contains(x.Id));
            foreach (var id in roots) { fence.Assort.BarterScheme.Remove(id); fence.Assort.LoyalLevelItems.Remove(id); }
        }
        logger.Success($"Admiral Belt progression enforced: detected={belts.Length}, Fence roots removed={removed}");
        return Task.CompletedTask;
    }

    public static double BeltPriceFloor(int cells) => cells switch
    {
        >= 20 => 450000, >= 15 => 325000, >= 12 => 240000, >= 8 => 170000, >= 4 => 100000,
        _ => 0
    };
}

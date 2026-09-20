using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
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
        var normalized = 0;
        foreach (var traderPair in traders)
        {
            var trader = traderPair.Value;
            if (traderPair.Key.ToString() == FenceId) continue;
            foreach (var offer in trader.Assort.Items.Where(x => string.Equals(x.ParentId?.ToString(), "hideout", StringComparison.Ordinal) && ids.Contains(x.Template)))
            {
                var cells = belts.First(x => x.Item.Id == offer.Template).Cells;
                var price = BeltPriceFloor(cells);
                trader.Assort.LoyalLevelItems[offer.Id] = BeltLoyaltyLevel(cells);
                if (trader.Assort.BarterScheme.TryGetValue(offer.Id, out var schemes)
                    && schemes.Count == 1 && schemes[0].Count == 1 && schemes[0][0].Template == Money.ROUBLES)
                {
                    double currentPrice = schemes[0][0].Count ?? 0;
                    if (currentPrice < price) schemes[0][0].Count = price;
                }
                offer.Upd ??= new Upd();
                offer.Upd.UnlimitedCount = false;
                offer.Upd.StackObjectsCount = BeltStock(cells);
                normalized++;
            }
        }
        if (ids.Count > 0 && traders.TryGetValue(new MongoId(FenceId), out var fence))
        {
            var roots = fence.Assort.Items.Where(x => string.Equals(x.ParentId?.ToString(), "hideout", StringComparison.Ordinal) && ids.Contains(x.Template)).Select(x => x.Id).ToHashSet();
            var all = roots.Select(x => x.ToString()).ToHashSet(StringComparer.Ordinal);
            foreach (var root in roots) CollectDescendants(fence.Assort.Items, all, root.ToString());
            removed = roots.Count;
            fence.Assort.Items.RemoveAll(x => all.Contains(x.Id.ToString()));
            foreach (var id in roots) { fence.Assort.BarterScheme.Remove(id); fence.Assort.LoyalLevelItems.Remove(id); }
        }
        logger.Success($"Admiral Belt progression enforced: detected={belts.Length}, offers normalized={normalized}, Fence roots removed={removed}");
        return Task.CompletedTask;
    }

    private static void CollectDescendants(List<Item> items, HashSet<string> collected, string parentId)
    {
        foreach (var child in items.Where(x => string.Equals(x.ParentId?.ToString(), parentId, StringComparison.Ordinal)))
            if (collected.Add(child.Id.ToString())) CollectDescendants(items, collected, child.Id.ToString());
    }

    public static double BeltPriceFloor(int cells) => cells switch
    {
        >= 20 => 450000, >= 15 => 325000, >= 12 => 240000, >= 8 => 170000, >= 4 => 100000,
        _ => 0
    };

    public static int BeltLoyaltyLevel(int cells) => cells switch
    {
        >= 15 => 4,
        >= 12 => 3,
        >= 8 => 2,
        >= 4 => 1,
        _ => 1
    };

    public static int BeltStock(int cells) => cells switch
    {
        >= 15 => 1,
        >= 12 => 2,
        >= 8 => 2,
        _ => 3
    };
}

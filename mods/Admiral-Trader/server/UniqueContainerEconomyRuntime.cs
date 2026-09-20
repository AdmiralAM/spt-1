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

/// <summary>
/// Owns progression and valuation for the portable specialty containers bundled with the
/// Admiral installation.  This is deliberately an explicit identity catalogue: classifying
/// every item with a grid would also catch rigs, backpacks and world-loot containers.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 2), UsedImplicitly]
public sealed class UniqueContainerEconomyRuntime(
    TemplateTable templates,
    TradersTable traders,
    ISptLogger<UniqueContainerEconomyRuntime> logger) : IOnLoad
{
    internal sealed record Policy(double PriceFloor, int LoyaltyLevel, int Stock);

    internal static readonly IReadOnlyDictionary<string, Policy> Catalogue =
        new Dictionary<string, Policy>(StringComparer.Ordinal)
        {
            ["9543bbe8083934dc3b1b1330"] = new(350_000, 2, 2), // Small Tool Bag
            ["c29f11b2e63a089916739c96"] = new(400_000, 3, 2), // Small Leather Docket
            ["12403f74773f49be6a2d84b7"] = new(350_000, 2, 2), // Small Medical Pouch
            ["440de5d056825485a0cf3a19"] = new(600_000, 3, 2), // Small Magazine Pouch
            ["6925918065a41e6b1e02a7d7"] = new(350_000, 2, 2), // Vintage Lunch Box
            ["2eabd4da4ab194eb168e72d3"] = new(500_000, 3, 1), // Small Key Ring
            ["669c10fa06c00c483c58537a"] = new(400_000, 3, 1), // Small Cash Box
            ["669c1a420c8342338269dd86"] = new(900_000, 3, 1), // Vintage Thermaster Cooler
            ["681c17948a6e3fb72e221390"] = new(750_000, 3, 1), // Vintage Fuel Container
            ["681d117c2b345ac614614bf8"] = new(1_200_000, 4, 1), // Mobile Food Cart
            ["681d124d70b2418646a80fa3"] = new(850_000, 3, 1), // Violin Case
            ["681d1395a5c2308fdcbcda92"] = new(950_000, 3, 1), // Attachment Case
            ["681d2b9736545ba7dfd0847a"] = new(650_000, 3, 1), // Mil-Spec Tool Case
            ["6761b213607f9a6f79017b65"] = new(1_500_000, 4, 1), // Mobile Infirmary secure container
            ["6761b213607f9a6f79017b67"] = new(750_000, 3, 1), // HEX-Case pistol carrier
            ["6a3c0e9643138b61c8739586"] = new(800_000, 3, 1), // Small ballistic plate case
            ["66326bfd46817c660d015122"] = new(2_000_000, 4, 1), // Abandoned Medical Box (1944)
            ["66326bfd46817c660d015150"] = new(900_000, 3, 1), // Snacky-Z
            ["6937ecc3dbdccab44605fcf0"] = new(1_500_000, 4, 1), // Twitch rare dogtag case
        };

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var active = Catalogue
            .Where(x => templates.Items.ContainsKey(new MongoId(x.Key)))
            .ToDictionary(x => new MongoId(x.Key), x => x.Value);

        foreach (var entry in active)
        {
            var handbook = templates.Handbook.Items.FirstOrDefault(x => x.Id == entry.Key);
            if (handbook is not null && handbook.Price < entry.Value.PriceFloor)
                handbook.Price = entry.Value.PriceFloor;
        }

        var normalized = 0;
        var removedFromFence = 0;
        foreach (var traderPair in traders)
        {
            if (traderPair.Key.ToString() == BeltProgressionRuntime.FenceId)
            {
                removedFromFence += RemoveOfferTrees(traderPair.Value, active.Keys.ToHashSet());
                continue;
            }

            foreach (var offer in traderPair.Value.Assort.Items
                         .Where(x => x.ParentId?.ToString() == "hideout" && active.ContainsKey(x.Template)))
            {
                var policy = active[offer.Template];
                traderPair.Value.Assort.LoyalLevelItems[offer.Id] = policy.LoyaltyLevel;
                if (traderPair.Value.Assort.BarterScheme.TryGetValue(offer.Id, out var schemes)
                    && schemes.Count == 1 && schemes[0].Count == 1 && schemes[0][0].Template == Money.ROUBLES
                    && (schemes[0][0].Count ?? 0) < policy.PriceFloor)
                    schemes[0][0].Count = policy.PriceFloor;

                offer.Upd ??= new Upd();
                offer.Upd.UnlimitedCount = false;
                offer.Upd.StackObjectsCount = policy.Stock;
                normalized++;
            }
        }

        logger.Success($"Admiral unique-container economy enforced: catalogue={Catalogue.Count}, present={active.Count}, offers={normalized}, Fence roots removed={removedFromFence}");
        return Task.CompletedTask;
    }

    private static int RemoveOfferTrees(Trader trader, HashSet<MongoId> templateIds)
    {
        var roots = trader.Assort.Items
            .Where(x => x.ParentId?.ToString() == "hideout" && templateIds.Contains(x.Template))
            .Select(x => x.Id).ToHashSet();
        var all = new HashSet<MongoId>(roots);
        bool changed;
        do
        {
            changed = false;
            foreach (var item in trader.Assort.Items)
                if (item.ParentId is { } parent && all.Contains(parent) && all.Add(item.Id)) changed = true;
        } while (changed);

        trader.Assort.Items.RemoveAll(x => all.Contains(x.Id));
        foreach (var root in roots)
        {
            trader.Assort.BarterScheme.Remove(root);
            trader.Assort.LoyalLevelItems.Remove(root);
        }
        return roots.Count;
    }
}

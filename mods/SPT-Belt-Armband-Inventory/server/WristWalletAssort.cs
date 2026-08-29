using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 2)]
public sealed class WristWalletAssort(TradersTable tradersTable, ISptLogger<WristWalletAssort> logger) : IOnLoad
{
    private const int PriceRoubles = 12500;
    private const int LoyaltyLevel = 1;
    private const int UnlimitedStock = 999999;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var trader = tradersTable.GetValueOrDefault(RuntimeCandidateOfferContract.RagmanTraderId)
            ?? throw new InvalidOperationException("B&A&HB Wrist Wallet could not find Ragman.");
        var id = new MongoId(RuntimeIdentity.WristWalletAssortId);
        var matches = trader.Assort.Items.Where(x => x.Id == id).Take(2).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: duplicate item entries own the persistent assort ID.");

        var existing = matches.SingleOrDefault();
        if (existing != null)
        {
            ValidateExisting(trader, id, existing);
            logger.Success($"B&A&HB Wrist Wallet retained validated Ragman LL{LoyaltyLevel} offer for {PriceRoubles:N0} RUB.");
            return Task.CompletedTask;
        }

        if (trader.Assort.BarterScheme.ContainsKey(id) || trader.Assort.LoyalLevelItems.ContainsKey(id))
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: item is absent but barter/loyalty metadata already owns the persistent assort ID.");

        trader.Assort.Items.Add(new Item
        {
            Id = id,
            Template = new MongoId(RuntimeIdentity.WristWalletItemId),
            ParentId = RuntimeCandidateOfferContract.RootId,
            SlotId = RuntimeCandidateOfferContract.RootId,
            Upd = new Upd { UnlimitedCount = true, StackObjectsCount = UnlimitedStock }
        });
        trader.Assort.BarterScheme.Add(id, [[new BarterScheme { Count = PriceRoubles, Template = Money.ROUBLES }]]);
        trader.Assort.LoyalLevelItems.Add(id, LoyaltyLevel);
        logger.Success($"B&A&HB Wrist Wallet added to Ragman LL{LoyaltyLevel} for {PriceRoubles:N0} RUB.");
        return Task.CompletedTask;
    }

    private static void ValidateExisting(Trader trader, MongoId id, Item existing)
    {
        if (!Equals(existing.Template, new MongoId(RuntimeIdentity.WristWalletItemId))
            || !string.Equals(existing.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || !string.Equals(existing.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal))
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: hierarchy differs.");

        if (existing.Upd == null || existing.Upd.UnlimitedCount != true || existing.Upd.StackObjectsCount != UnlimitedStock)
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: stock policy differs.");

        if (!trader.Assort.BarterScheme.TryGetValue(id, out var schemes)
            || schemes.Count != 1
            || schemes[0].Count != 1
            || !Equals(schemes[0][0].Template, Money.ROUBLES)
            || schemes[0][0].Count != PriceRoubles)
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: price differs.");

        if (!trader.Assort.LoyalLevelItems.TryGetValue(id, out var loyalty) || loyalty != LoyaltyLevel)
            throw new InvalidOperationException("B&A&HB Wrist Wallet assort ID collision: loyalty level differs.");
    }
}

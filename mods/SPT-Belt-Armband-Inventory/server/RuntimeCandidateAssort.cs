using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public sealed class RuntimeCandidateAssort(TradersTable tradersTable, ISptLogger<RuntimeCandidateAssort> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var trader = tradersTable.GetValueOrDefault(RuntimeCandidateOfferContract.RagmanTraderId)
            ?? throw new InvalidOperationException("B&A&HB Magazine Armband could not find Ragman.");
        var id = new MongoId(RuntimeIdentity.CandidateAssortId);
        var existing = trader.Assort.Items.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            ValidateExistingAssort();
            logger.Success($"B&A&HB Magazine Armband retained validated Ragman LL{RuntimeCandidateOfferContract.LoyaltyLevel} offer for {RuntimeCandidateOfferContract.PriceRoubles:N0} RUB.");
            return Task.CompletedTask;
        }

        EnsureNoPartialAssortCollision();
        trader.Assort.Items.Add(new Item
        {
            Id = id,
            Template = new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl),
            ParentId = RuntimeCandidateOfferContract.RootId,
            SlotId = RuntimeCandidateOfferContract.RootId,
            Upd = new Upd { UnlimitedCount = true, StackObjectsCount = RuntimeCandidateOfferContract.UnlimitedStock }
        });
        trader.Assort.BarterScheme.Add(id, [[new BarterScheme { Count = RuntimeCandidateOfferContract.PriceRoubles, Template = Money.ROUBLES }]]);
        trader.Assort.LoyalLevelItems.Add(id, RuntimeCandidateOfferContract.LoyaltyLevel);
        logger.Success($"B&A&HB Magazine Armband added to Ragman LL{RuntimeCandidateOfferContract.LoyaltyLevel} for {RuntimeCandidateOfferContract.PriceRoubles:N0} RUB.");
        return Task.CompletedTask;

        void EnsureNoPartialAssortCollision()
        {
            if (trader.Assort.BarterScheme.ContainsKey(id) || trader.Assort.LoyalLevelItems.ContainsKey(id))
                throw new InvalidOperationException("B&A&HB Magazine Armband assort ID collision: item is absent but barter/loyalty metadata already owns the persistent assort ID.");
        }

        void ValidateExistingAssort()
        {
            if (existing == null) throw new InvalidOperationException("B&A&HB Magazine Armband assort validation received no existing offer.");
            if (!Equals(existing.Template, new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl))
                || !string.Equals(existing.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
                || !string.Equals(existing.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal))
                throw new InvalidOperationException("B&A&HB Magazine Armband assort ID collision: existing offer points to a different item or hierarchy.");

            if (existing.Upd == null
                || existing.Upd.UnlimitedCount != true
                || existing.Upd.StackObjectsCount != RuntimeCandidateOfferContract.UnlimitedStock)
                throw new InvalidOperationException("B&A&HB Magazine Armband assort ID collision: existing offer stock policy differs from the product contract.");

            if (!trader.Assort.BarterScheme.TryGetValue(id, out var schemes)
                || schemes.Count != 1
                || schemes[0].Count != 1
                || !Equals(schemes[0][0].Template, Money.ROUBLES)
                || schemes[0][0].Count != RuntimeCandidateOfferContract.PriceRoubles)
                throw new InvalidOperationException($"B&A&HB Magazine Armband assort ID collision: existing offer does not cost exactly {RuntimeCandidateOfferContract.PriceRoubles:N0} RUB.");

            if (!trader.Assort.LoyalLevelItems.TryGetValue(id, out var loyaltyLevel)
                || loyaltyLevel != RuntimeCandidateOfferContract.LoyaltyLevel)
                throw new InvalidOperationException($"B&A&HB Magazine Armband assort ID collision: existing offer is not registered at Ragman LL{RuntimeCandidateOfferContract.LoyaltyLevel}.");
        }
    }
}

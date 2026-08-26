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
    private const string RagmanTraderId = "5ac3b934156ae10c4430e83c";
    private const string AssortItemId = "68ac00000000000000000003";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var trader = tradersTable.GetValueOrDefault(RagmanTraderId) ?? throw new InvalidOperationException("B&A&HB RC could not find Ragman.");
        var id = new MongoId(AssortItemId);
        var existing = trader.Assort.Items.FirstOrDefault(x => x.Id == id);
        if (existing != null)
        {
            ValidateExistingAssort();
            logger.Success("B&A&HB RC retained existing validated Ragman LL1 offer for 1,000 RUB.");
            return Task.CompletedTask;
        }
        trader.Assort.Items.Add(new Item { Id = id, Template = new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl), ParentId = "hideout", SlotId = "hideout", Upd = new Upd { UnlimitedCount = true, StackObjectsCount = 999999 } });
        trader.Assort.BarterScheme[id] = [[new BarterScheme { Count = 1000, Template = Money.ROUBLES }]];
        trader.Assort.LoyalLevelItems[id] = 1;
        logger.Success("B&A&HB RC added to Ragman LL1 for 1,000 RUB.");
        return Task.CompletedTask;

        void ValidateExistingAssort()
        {
            if (existing == null) throw new InvalidOperationException("B&A&HB RC assort validation received no existing offer.");
            if (!Equals(existing.Template, new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl))
                || !string.Equals(existing.ParentId, "hideout", StringComparison.Ordinal)
                || !string.Equals(existing.SlotId, "hideout", StringComparison.Ordinal))
                throw new InvalidOperationException("B&A&HB RC assort ID collision: existing offer points to a different item or hierarchy.");

            if (!trader.Assort.BarterScheme.TryGetValue(id, out var schemes)
                || schemes.Count != 1
                || schemes[0].Count != 1
                || !Equals(schemes[0][0].Template, Money.ROUBLES)
                || schemes[0][0].Count != 1000)
                throw new InvalidOperationException("B&A&HB RC assort ID collision: existing offer does not cost exactly 1,000 RUB.");

            if (!trader.Assort.LoyalLevelItems.TryGetValue(id, out var loyaltyLevel) || loyaltyLevel != 1)
                throw new InvalidOperationException("B&A&HB RC assort ID collision: existing offer is not registered at Ragman LL1.");
        }
    }
}

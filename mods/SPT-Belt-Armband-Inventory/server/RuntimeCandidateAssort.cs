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
        if (trader.Assort.Items.Exists(x => x.Id == id)) return Task.CompletedTask;
        trader.Assort.Items.Add(new Item { Id = id, Template = new MongoId(RuntimeCandidateBeltItem.RuntimeCandidateTpl), ParentId = "hideout", SlotId = "hideout", Upd = new Upd { UnlimitedCount = true, StackObjectsCount = 999999 } });
        trader.Assort.BarterScheme[id] = [[new BarterScheme { Count = 1000, Template = Money.ROUBLES }]];
        trader.Assort.LoyalLevelItems[id] = 1;
        logger.Success("B&A&HB RC added to Ragman LL1 for 1,000 RUB.");
        return Task.CompletedTask;
    }
}

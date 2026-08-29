using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 2)]
public sealed class DedicatedWearableAssort(
    TradersTable tradersTable,
    ISptLogger<DedicatedWearableAssort> logger) : IOnLoad
{
    private const int BeltLoyaltyLevel = 2;
    private const int HeadBandLoyaltyLevel = 1;
    private const int BeltPrice = 45000;
    private const int HeadBandPrice = 25000;
    private const int UnlimitedStock = 999999;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var trader = tradersTable.GetValueOrDefault(RuntimeCandidateOfferContract.RagmanTraderId)
            ?? throw new InvalidOperationException("B&A&HB dedicated wearable offers could not find Ragman.");

        EnsureOffer(
            new MongoId(RuntimeIdentity.DedicatedMagazineBeltAssortId),
            new MongoId(RuntimeIdentity.DedicatedMagazineBeltItemId),
            BeltPrice,
            BeltLoyaltyLevel,
            "Magazine Belt");
        EnsureOffer(
            new MongoId(RuntimeIdentity.EmergencyHeadBandAssortId),
            new MongoId(RuntimeIdentity.EmergencyHeadBandItemId),
            HeadBandPrice,
            HeadBandLoyaltyLevel,
            "Utility HeadBand");

        logger.Success($"B&A&HB product offers registered: Magazine Belt Ragman LL{BeltLoyaltyLevel}/{BeltPrice:N0} RUB; Utility HeadBand Ragman LL{HeadBandLoyaltyLevel}/{HeadBandPrice:N0} RUB.");
        return Task.CompletedTask;

        void EnsureOffer(MongoId assortId, MongoId templateId, int price, int loyaltyLevel, string label)
        {
            var existing = trader.Assort.Items.FirstOrDefault(x => x.Id == assortId);
            if (existing == null)
            {
                trader.Assort.Items.Add(new Item
                {
                    Id = assortId,
                    Template = templateId,
                    ParentId = RuntimeCandidateOfferContract.RootId,
                    SlotId = RuntimeCandidateOfferContract.RootId,
                    Upd = new Upd { UnlimitedCount = true, StackObjectsCount = UnlimitedStock }
                });
                trader.Assort.BarterScheme[assortId] = [[new BarterScheme { Count = price, Template = Money.ROUBLES }]];
                trader.Assort.LoyalLevelItems[assortId] = loyaltyLevel;
                return;
            }

            if (!Equals(existing.Template, templateId)
                || !string.Equals(existing.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
                || !string.Equals(existing.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
                || existing.Upd?.UnlimitedCount != true
                || existing.Upd.StackObjectsCount != UnlimitedStock)
                throw new InvalidOperationException($"B&A&HB dedicated {label} assort ID collision.");

            if (!trader.Assort.BarterScheme.TryGetValue(assortId, out var schemes)
                || schemes.Count != 1
                || schemes[0].Count != 1
                || !Equals(schemes[0][0].Template, Money.ROUBLES)
                || schemes[0][0].Count != price)
                throw new InvalidOperationException($"B&A&HB dedicated {label} barter contract collision.");

            if (!trader.Assort.LoyalLevelItems.TryGetValue(assortId, out var loyalty) || loyalty != loyaltyLevel)
                throw new InvalidOperationException($"B&A&HB dedicated {label} loyalty contract collision.");
        }
    }
}

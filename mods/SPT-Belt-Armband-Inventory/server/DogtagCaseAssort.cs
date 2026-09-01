using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Publishes the Dogtag Case only after both its exact template and the vanilla
/// Dogtag equipment host contract are live. This keeps the product obtainable
/// without weakening the ordinary personal-dogtag slot semantics.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 2)]
public sealed class DogtagCaseAssort(
    TradersTable tradersTable,
    TemplateTable templateTable,
    ISptLogger<DogtagCaseAssort> logger) : IOnLoad
{
    private const int PriceRoubles = 50000;
    private const int LoyaltyLevel = 2;
    private const int UnlimitedStock = 999999;
    private const string DogtagSlotName = "Dogtag";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templateId = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        RequirePublicationBoundary(templateTable, templateId);
        cancellationToken.ThrowIfCancellationRequested();

        var trader = tradersTable.GetValueOrDefault(RuntimeCandidateOfferContract.RagmanTraderId)
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case could not find Ragman.");
        var id = new MongoId(RuntimeIdentity.DogtagCaseAssortId);
        var matches = trader.Assort.Items.Where(x => x.Id == id).Take(2).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: duplicate item entries own the persistent assort ID.");

        var existing = matches.SingleOrDefault();
        if (existing != null)
        {
            ValidateExisting(trader, id, existing, templateId);
            RequirePublicationBoundary(templateTable, templateId);
            cancellationToken.ThrowIfCancellationRequested();
            logger.Success($"B&A&HB Dogtag Case retained validated Ragman LL{LoyaltyLevel} offer for {PriceRoubles:N0} RUB.");
            return Task.CompletedTask;
        }

        if (trader.Assort.BarterScheme.ContainsKey(id) || trader.Assort.LoyalLevelItems.ContainsKey(id))
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: item is absent but barter/loyalty metadata already owns the persistent assort ID.");

        var offer = new Item
        {
            Id = id,
            Template = templateId,
            ParentId = RuntimeCandidateOfferContract.RootId,
            SlotId = RuntimeCandidateOfferContract.RootId,
            Upd = new Upd { UnlimitedCount = true, StackObjectsCount = UnlimitedStock }
        };
        var barter = new List<List<BarterScheme>>
        {
            new() { new BarterScheme { Count = PriceRoubles, Template = Money.ROUBLES } }
        };

        bool itemAdded = false;
        bool barterAdded = false;
        bool loyaltyAdded = false;
        try
        {
            trader.Assort.Items.Add(offer);
            itemAdded = true;
            trader.Assort.BarterScheme.Add(id, barter);
            barterAdded = true;
            trader.Assort.LoyalLevelItems.Add(id, LoyaltyLevel);
            loyaltyAdded = true;

            cancellationToken.ThrowIfCancellationRequested();
            ValidateExisting(trader, id, offer, templateId);
            RequirePublicationBoundary(templateTable, templateId);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            RollbackOwnedAssortTuple(trader, id, offer, barter, itemAdded, barterAdded, loyaltyAdded);
            throw;
        }

        logger.Success($"B&A&HB Dogtag Case added to Ragman LL{LoyaltyLevel} for {PriceRoubles:N0} RUB after exact vanilla Dogtag host verification.");
        return Task.CompletedTask;
    }

    private static void RollbackOwnedAssortTuple(
        Trader trader,
        MongoId id,
        Item offer,
        List<List<BarterScheme>> barter,
        bool itemAdded,
        bool barterAdded,
        bool loyaltyAdded)
    {
        int ownedItemIndex = -1;
        if (itemAdded)
        {
            for (int i = trader.Assort.Items.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(trader.Assort.Items[i], offer)) continue;
                ownedItemIndex = i;
                break;
            }
        }

        bool ownsItem = ownedItemIndex >= 0;
        bool ownsBarter = barterAdded
            && trader.Assort.BarterScheme.TryGetValue(id, out var currentBarter)
            && ReferenceEquals(currentBarter, barter);

        if (loyaltyAdded && ownsItem && ownsBarter
            && trader.Assort.LoyalLevelItems.TryGetValue(id, out var currentLoyalty)
            && currentLoyalty == LoyaltyLevel)
            trader.Assort.LoyalLevelItems.Remove(id);

        if (ownsBarter)
            trader.Assort.BarterScheme.Remove(id);

        if (ownsItem)
            trader.Assort.Items.RemoveAt(ownedItemIndex);
    }

    private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)
    {
        DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);
        RequireExactDogtagHost(templateTable, templateId);
    }

    internal static void RequireExactDogtagHost(TemplateTable templateTable, MongoId templateId)
    {
        if (!Equals(templateId, new MongoId(RuntimeIdentity.DogtagCaseItemId)))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: requested template identity is not the exact Dogtag Case product.");

        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: default inventory template is missing.");

        var slots = inventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots == null || slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: vanilla Dogtag host boundary is missing or ambiguous.");

        var slot = slots[0];
        var groups = slot.Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter is missing or ambiguous.");

        var hostFilter = groups[0].Filter;
        if (hostFilter == null || hostFilter.Count < 2)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter does not preserve ordinary dogtags plus the exact container.");

        DogtagCaseHostContract.RequireCommitted(hostFilter);

        // The committed HashSet proof is meaningful only if the exact inventory
        // template that owned it is still the live DefaultInventory entry. A
        // replacement of TemplateTable.Items[DefaultInventory] must fail closed
        // before slot/filter identity checks can accidentally validate a detached
        // stale inventory object captured above.
        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var liveInventory)
            || !ReferenceEquals(liveInventory, inventory))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live DefaultInventory template changed during committed-host verification.");

        // The committed HashSet proof is meaningful only if that exact host is still
        // installed in the live DefaultInventory after verification. Re-resolve the
        // bounded Dogtag slot/group/filter shape and require reference identity for
        // every link; a replacement group that reuses the same HashSet is still a
        // structural host replacement and must fail closed before Ragman publication.
        var liveSlots = liveInventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (liveSlots == null || liveSlots.Length != 1 || !ReferenceEquals(liveSlots[0], slot))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag slot changed during committed-host verification.");

        var liveGroups = liveSlots[0].Properties?.Filters?.ToArray();
        if (liveGroups == null || liveGroups.Length != 1
            || !ReferenceEquals(liveGroups[0], groups[0])
            || !ReferenceEquals(liveGroups[0].Filter, hostFilter))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag filter group/filter changed during committed-host verification.");
    }

    private static void ValidateExisting(Trader trader, MongoId id, Item existing, MongoId templateId)
    {
        if (!Equals(existing.Template, templateId)
            || !string.Equals(existing.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || !string.Equals(existing.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal))
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: hierarchy differs.");

        if (existing.Upd == null || existing.Upd.UnlimitedCount != true || existing.Upd.StackObjectsCount != UnlimitedStock)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: stock policy differs.");

        if (!trader.Assort.BarterScheme.TryGetValue(id, out var schemes)
            || schemes.Count != 1
            || schemes[0].Count != 1
            || !Equals(schemes[0][0].Template, Money.ROUBLES)
            || schemes[0][0].Count != PriceRoubles)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: price differs.");

        if (!trader.Assort.LoyalLevelItems.TryGetValue(id, out var loyalty) || loyalty != LoyaltyLevel)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: loyalty level differs.");
    }
}
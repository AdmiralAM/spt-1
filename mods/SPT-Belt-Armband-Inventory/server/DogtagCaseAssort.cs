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
        var assort = trader.Assort
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case could not resolve Ragman assort.");
        var items = assort.Items
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case could not resolve Ragman assort Items collection.");
        var barterScheme = assort.BarterScheme
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case could not resolve Ragman assort BarterScheme collection.");
        var loyalLevelItems = assort.LoyalLevelItems
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case could not resolve Ragman assort LoyalLevelItems collection.");

        bool IsAssortWrapperIdentityCurrent()
            => ReferenceEquals(trader.Assort, assort)
                && ReferenceEquals(trader.Assort?.Items, items)
                && ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)
                && ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems);

        void RequireAssortWrapperIdentity()
        {
            if (!IsAssortWrapperIdentityCurrent())
                throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman assort wrapper chain changed during bounded publication.");
        }

        RequireAssortWrapperIdentity();

        var id = new MongoId(RuntimeIdentity.DogtagCaseAssortId);
        var matches = items.Where(x => x.Id == id).Take(2).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: duplicate item entries own the persistent assort ID.");

        var existing = matches.SingleOrDefault();
        if (existing != null)
        {
            if (!barterScheme.TryGetValue(id, out var existingBarter))
                throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: retained item has no barter tuple to capture before validation.");
            ValidateExisting(items, barterScheme, loyalLevelItems, id, existing, templateId);
            RequireAssortWrapperIdentity();
            if (!barterScheme.TryGetValue(id, out var liveExistingBarter)
                || !ReferenceEquals(liveExistingBarter, existingBarter))
                throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: retained barter tuple identity changed during validation.");
            RequirePublicationBoundary(templateTable, templateId);
            RequireAssortWrapperIdentity();
            RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, existing, existingBarter);
            RequireAssortWrapperIdentity();
            cancellationToken.ThrowIfCancellationRequested();
            logger.Success($"B&A&HB Dogtag Case retained validated Ragman LL{LoyaltyLevel} offer for {PriceRoubles:N0} RUB.");
            return Task.CompletedTask;
        }

        if (barterScheme.ContainsKey(id) || loyalLevelItems.ContainsKey(id))
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
            RequireAssortWrapperIdentity();
            items.Add(offer);
            itemAdded = true;
            barterScheme.Add(id, barter);
            barterAdded = true;
            loyalLevelItems.Add(id, LoyaltyLevel);
            loyaltyAdded = true;

            cancellationToken.ThrowIfCancellationRequested();
            RequireAssortWrapperIdentity();
            ValidateExisting(items, barterScheme, loyalLevelItems, id, offer, templateId);
            RequireAssortWrapperIdentity();
            RequirePublicationBoundary(templateTable, templateId);
            RequireAssortWrapperIdentity();
            RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, offer, barter);
            RequireAssortWrapperIdentity();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // Rollback is prefix-transactional. Before the first mutation, prove that every
            // component this transaction successfully published is still exact-owned and
            // that no later component was supplied by another participant. If any tuple
            // component drifted, preserve the complete live tuple untouched and fail closed
            // rather than removing a subset and leaving dangling Ragman metadata.
            if (!IsAssortWrapperIdentityCurrent())
                throw;

            int ownedItemIndex = -1;
            if (itemAdded)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(items[i], offer)) continue;
                    ownedItemIndex = i;
                    break;
                }
            }

            bool ownsItem = !itemAdded || ownedItemIndex >= 0;
            bool ownsBarter = barterAdded
                ? barterScheme.TryGetValue(id, out var currentBarter) && ReferenceEquals(currentBarter, barter)
                : !barterScheme.ContainsKey(id);
            bool ownsLoyalty = loyaltyAdded
                ? loyalLevelItems.TryGetValue(id, out var currentLoyalty) && currentLoyalty == LoyaltyLevel
                : !loyalLevelItems.ContainsKey(id);

            if (!ownsItem || !ownsBarter || !ownsLoyalty)
                throw;

            if (loyaltyAdded)
            {
                if (!IsAssortWrapperIdentityCurrent()) throw;
                if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;
                if (!barterScheme.TryGetValue(id, out var liveOwnedBarter) || !ReferenceEquals(liveOwnedBarter, barter)) throw;
                if (!loyalLevelItems.TryGetValue(id, out var liveOwnedLoyalty) || liveOwnedLoyalty != LoyaltyLevel) throw;
                loyalLevelItems.Remove(id);
            }

            if (barterAdded)
            {
                if (!IsAssortWrapperIdentityCurrent()) throw;
                if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;
                if (!barterScheme.TryGetValue(id, out var liveOwnedBarter) || !ReferenceEquals(liveOwnedBarter, barter)) throw;
                if (loyalLevelItems.ContainsKey(id)) throw;
                barterScheme.Remove(id);
            }

            if (itemAdded)
            {
                if (!IsAssortWrapperIdentityCurrent()) throw;
                if (ownedItemIndex < 0 || ownedItemIndex >= items.Count || !ReferenceEquals(items[ownedItemIndex], offer)) throw;
                if (barterScheme.ContainsKey(id) || loyalLevelItems.ContainsKey(id)) throw;
                items.RemoveAt(ownedItemIndex);
            }
            throw;
        }

        logger.Success($"B&A&HB Dogtag Case added to Ragman LL{LoyaltyLevel} for {PriceRoubles:N0} RUB after exact vanilla Dogtag host verification.");
        return Task.CompletedTask;
    }

    private static void RequirePublishedAssortTupleIdentity(
        List<Item> items,
        Dictionary<MongoId, List<List<BarterScheme>>> barterScheme,
        Dictionary<MongoId, int> loyalLevelItems,
        MongoId id,
        Item expectedItem,
        List<List<BarterScheme>> expectedBarter)
    {
        int exactItemMatches = 0;
        int idMatches = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Id != id) continue;
            idMatches++;
            if (ReferenceEquals(item, expectedItem)) exactItemMatches++;
            if (idMatches > 1)
                throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman item tuple changed or became ambiguous after validation.");
        }

        if (idMatches != 1 || exactItemMatches != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman item reference was replaced before publication.");

        if (!Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))
            || !string.Equals(expectedItem.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || !string.Equals(expectedItem.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || expectedItem.Upd == null
            || expectedItem.Upd.UnlimitedCount != true
            || expectedItem.Upd.StackObjectsCount != UnlimitedStock)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman item contents changed in place before publication.");

        if (!barterScheme.TryGetValue(id, out var liveBarter)
            || !ReferenceEquals(liveBarter, expectedBarter)
            || liveBarter.Count != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman barter reference/cardinality changed before publication.");

        var expectedInnerBarter = liveBarter[0];
        if (expectedInnerBarter == null || expectedInnerBarter.Count != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman inner barter cardinality changed before publication.");
        var expectedScheme = expectedInnerBarter[0];
        if (expectedScheme == null
            || !Equals(expectedScheme.Template, Money.ROUBLES)
            || expectedScheme.Count != PriceRoubles)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman barter contents changed in place before publication.");

        if (!ReferenceEquals(liveBarter[0], expectedInnerBarter)
            || !ReferenceEquals(liveBarter[0][0], expectedScheme))
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman inner barter identity changed during initial tuple proof.");

        if (!loyalLevelItems.TryGetValue(id, out var liveLoyalty)
            || liveLoyalty != LoyaltyLevel)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: validated Ragman loyalty metadata changed before publication.");

        RequirePublishedAssortTupleStillStable(items, barterScheme, loyalLevelItems, id, expectedItem, expectedBarter, expectedInnerBarter, expectedScheme);
    }

    private static void RequirePublishedAssortTupleStillStable(
        List<Item> items,
        Dictionary<MongoId, List<List<BarterScheme>>> barterScheme,
        Dictionary<MongoId, int> loyalLevelItems,
        MongoId id,
        Item expectedItem,
        List<List<BarterScheme>> expectedBarter,
        List<BarterScheme> expectedInnerBarter,
        BarterScheme expectedScheme)
    {
        int exactItemMatches = 0;
        int idMatches = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Id != id) continue;
            idMatches++;
            if (ReferenceEquals(item, expectedItem)) exactItemMatches++;
            if (idMatches > 1)
                throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman item tuple changed during final publication reproof.");
        }

        if (idMatches != 1 || exactItemMatches != 1
            || !Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))
            || !string.Equals(expectedItem.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || !string.Equals(expectedItem.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || expectedItem.Upd == null
            || expectedItem.Upd.UnlimitedCount != true
            || expectedItem.Upd.StackObjectsCount != UnlimitedStock)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman item identity/content changed during final publication reproof.");

        if (!barterScheme.TryGetValue(id, out var liveBarter)
            || !ReferenceEquals(liveBarter, expectedBarter)
            || liveBarter.Count != 1
            || !ReferenceEquals(liveBarter[0], expectedInnerBarter)
            || liveBarter[0].Count != 1
            || !ReferenceEquals(liveBarter[0][0], expectedScheme)
            || !Equals(liveBarter[0][0].Template, Money.ROUBLES)
            || liveBarter[0][0].Count != PriceRoubles)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman barter identity/content changed during final publication reproof.");

        if (!loyalLevelItems.TryGetValue(id, out var liveLoyalty)
            || liveLoyalty != LoyaltyLevel)
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: Ragman loyalty metadata changed during final publication reproof.");
    }

    private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)
    {
        DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);
        RequireExactDogtagHost(templateTable, templateId);
        DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);
    }

    internal static void RequireExactDogtagHost(TemplateTable templateTable, MongoId templateId)
    {
        if (!Equals(templateId, new MongoId(RuntimeIdentity.DogtagCaseItemId)))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: requested template identity is not the exact Dogtag Case product.");

        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: default inventory template is missing.");

        var inventoryProperties = inventory.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: default inventory properties are missing.");
        var slotsCollection = inventoryProperties.Slots
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: default inventory Slots collection is missing.");
        var slots = slotsCollection
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: vanilla Dogtag host boundary is missing or ambiguous.");

        var slot = slots[0];
        var slotProperties = slot.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag slot properties are missing.");
        var filtersCollection = slotProperties.Filters
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag Filters collection is missing.");
        var groups = filtersCollection.Take(2).ToArray();
        if (groups.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter is missing or ambiguous.");

        var hostFilter = groups[0].Filter;
        if (hostFilter == null || hostFilter.Count < 2)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter does not preserve ordinary dogtags plus the exact container.");

        DogtagCaseHostContract.RequireCommitted(hostFilter);

        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var liveInventory)
            || !ReferenceEquals(liveInventory, inventory))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live DefaultInventory template changed during committed-host verification.");
        if (!ReferenceEquals(liveInventory.Properties, inventoryProperties))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live DefaultInventory properties changed during committed-host verification.");
        if (!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live DefaultInventory Slots collection changed during committed-host verification.");

        var liveSlots = slotsCollection
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (liveSlots.Length != 1 || !ReferenceEquals(liveSlots[0], slot))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag slot changed during committed-host verification.");
        if (!ReferenceEquals(liveSlots[0].Properties, slotProperties))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag slot properties changed during committed-host verification.");
        if (!ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag Filters collection changed during committed-host verification.");

        var liveGroups = filtersCollection.Take(2).ToArray();
        if (liveGroups.Length != 1
            || !ReferenceEquals(liveGroups[0], groups[0])
            || !ReferenceEquals(liveGroups[0].Filter, hostFilter))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: live Dogtag filter group/filter changed during committed-host verification.");

        DogtagCaseHostContract.RequireCommitted(hostFilter);
    }

    private static void ValidateExisting(
        List<Item> items,
        Dictionary<MongoId, List<List<BarterScheme>>> barterScheme,
        Dictionary<MongoId, int> loyalLevelItems,
        MongoId id,
        Item existing,
        MongoId templateId)
    {
        int idMatches = 0;
        int exactMatches = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id != id) continue;
            idMatches++;
            if (ReferenceEquals(items[i], existing)) exactMatches++;
            if (idMatches > 1)
                throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: item tuple became ambiguous.");
        }

        if (idMatches != 1 || exactMatches != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: retained item reference changed.");

        if (!Equals(existing.Template, templateId)
            || !string.Equals(existing.ParentId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal)
            || !string.Equals(existing.SlotId, RuntimeCandidateOfferContract.RootId, StringComparison.Ordinal))
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: hierarchy differs.");

        if (existing.Upd == null || existing.Upd.UnlimitedCount != true || existing.Upd.StackObjectsCount != UnlimitedStock)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: stock policy differs.");

        if (!barterScheme.TryGetValue(id, out var schemes)
            || schemes.Count != 1
            || schemes[0].Count != 1
            || !Equals(schemes[0][0].Template, Money.ROUBLES)
            || schemes[0][0].Count != PriceRoubles)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: price differs.");

        if (!loyalLevelItems.TryGetValue(id, out var loyalty) || loyalty != LoyaltyLevel)
            throw new InvalidOperationException("B&A&HB Dogtag Case assort ID collision: loyalty level differs.");
    }
}
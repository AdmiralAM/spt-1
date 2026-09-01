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
            // Re-prove the live product + host after reading committed trader state.
            // Existing offers are not mutated here, so any concurrent startup drift
            // simply fails closed without ownership/rollback ambiguity.
            RequirePublicationBoundary(templateTable, templateId);
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

            // Publication is a committed-state boundary just like Dogtag host
            // exposure. Revalidate the exact live offer before declaring success.
            ValidateExisting(trader, id, offer, templateId);

            // Close the publication-time TOCTOU window as well: after the complete
            // owned assort tuple exists, re-prove both the live canonical product
            // and the committed vanilla Dogtag host. Failure remains inside this
            // invocation's rollback boundary and cannot publish a stale/corrupt case.
            RequirePublicationBoundary(templateTable, templateId);
        }
        catch
        {
            // Roll back only state created by this invocation. Existing/foreign
            // assort data was rejected before mutation and is never removed here.
            if (loyaltyAdded) trader.Assort.LoyalLevelItems.Remove(id);
            if (barterAdded) trader.Assort.BarterScheme.Remove(id);
            if (itemAdded) trader.Assort.Items.Remove(offer);
            throw;
        }

        logger.Success($"B&A&HB Dogtag Case added to Ragman LL{LoyaltyLevel} for {PriceRoubles:N0} RUB after exact vanilla Dogtag host verification.");
        return Task.CompletedTask;
    }

    private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)
    {
        // Keep template parity and equipment-host parity as one reusable publication
        // boundary so pre-publication and post-commit checks cannot silently diverge.
        DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);
        RequireExactDogtagHost(templateTable, templateId);
    }

    internal static void RequireExactDogtagHost(TemplateTable templateTable, MongoId templateId)
    {
        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: default inventory template is missing.");

        var slots = inventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots == null || slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: vanilla Dogtag host boundary is missing or ambiguous.");

        var groups = slots[0].Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter is missing or ambiguous.");

        var hostFilter = groups[0].Filter;
        if (hostFilter == null || hostFilter.Count < 2)
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: Dogtag host filter does not preserve ordinary dogtags plus the exact container.");

        // One committed-state proof is authoritative for publication. The host
        // contract snapshots this mutable set exactly once and proves on that same
        // point-in-time view that every captured vanilla/foreign entry survives,
        // the exact Dogtag Case is present, and no other B&A&HB-owned product has
        // contaminated the vanilla Dogtag host. Do not re-read the live HashSet
        // afterward: that would recreate a preservation/case-presence TOCTOU gap.
        DogtagCaseHostContract.RequireCommitted(hostFilter);

        // Keep the requested template argument an exact identity boundary as well;
        // callers may not reuse this verifier for another product/template.
        if (!Equals(templateId, new MongoId(RuntimeIdentity.DogtagCaseItemId)))
            throw new InvalidOperationException("B&A&HB Dogtag Case offer refused: requested template identity is not the exact Dogtag Case product.");
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

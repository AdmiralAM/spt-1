using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.Preload)]
public sealed class WearableTaxonomyRegistration(
    TemplateTable templateTable,
    ISptLogger<WearableTaxonomyRegistration> logger) : IOnLoad
{
    private static readonly MongoId SearchableItemBaseTpl = new("566162e44bdc2d3f298b4573");
    private static readonly MongoId SearchableParentTpl = new(RuntimeIdentity.SearchableTemplateParentId);
    private static readonly MongoId BeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private static readonly MongoId HeadBandParentTpl = new(RuntimeIdentity.HeadBandItemParentId);

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        // Validate every existing persistent identity before mutating TemplateTable.
        // A collision in the second/third node must not leave earlier nodes partially installed.
        TemplateItem? searchableAddition = PrepareNode(SearchableParentTpl, "BAndHBSearchableContainerTemplate", SearchableItemBaseTpl);
        TemplateItem? beltAddition = PrepareNode(BeltParentTpl, "BAndHBCustomBeltItem", SearchableParentTpl);
        TemplateItem? headBandAddition = PrepareNode(HeadBandParentTpl, "BAndHBCustomHeadBandItem", SearchableParentTpl);

        if (searchableAddition != null) templateTable.Items.Add(SearchableParentTpl, searchableAddition);
        if (beltAddition != null) templateTable.Items.Add(BeltParentTpl, beltAddition);
        if (headBandAddition != null) templateTable.Items.Add(HeadBandParentTpl, headBandAddition);

        logger.Success("B&A&HB #2 wearable taxonomy registered atomically for ArmBand/Belt/HeadBand runtime families.");
        return Task.CompletedTask;
    }

    private TemplateItem? PrepareNode(MongoId id, string name, MongoId parent)
    {
        if (!templateTable.Items.TryGetValue(id, out var existing))
        {
            return new TemplateItem
            {
                Id = id,
                Name = name,
                Parent = parent,
                Type = "Node",
                Properties = new TemplateItemProperties()
            };
        }

        if (!Equals(existing.Id, id)
            || !Equals(existing.Parent, parent)
            || !string.Equals(existing.Name, name, StringComparison.Ordinal)
            || !string.Equals(existing.Type, "Node", StringComparison.Ordinal))
            throw new InvalidOperationException($"B&A&HB taxonomy ID collision: {id}.");

        return null;
    }
}

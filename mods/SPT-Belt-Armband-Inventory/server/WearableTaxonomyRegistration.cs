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
        EnsureNode(SearchableParentTpl, "BAndHBSearchableContainerTemplate", SearchableItemBaseTpl);
        EnsureNode(BeltParentTpl, "BAndHBCustomBeltItem", SearchableParentTpl);
        EnsureNode(HeadBandParentTpl, "BAndHBCustomHeadBandItem", SearchableParentTpl);
        logger.Success("B&A&HB #2 wearable taxonomy registered for ArmBand/Belt/HeadBand runtime families.");
        return Task.CompletedTask;
    }

    private void EnsureNode(MongoId id, string name, MongoId parent)
    {
        if (!templateTable.Items.TryGetValue(id, out var existing))
        {
            templateTable.Items[id] = new TemplateItem
            {
                Id = id,
                Name = name,
                Parent = parent,
                Type = "Node",
                Properties = new TemplateItemProperties()
            };
            return;
        }

        if (!Equals(existing.Id, id)
            || !Equals(existing.Parent, parent)
            || !string.Equals(existing.Name, name, StringComparison.Ordinal)
            || !string.Equals(existing.Type, "Node", StringComparison.Ordinal))
            throw new InvalidOperationException($"B&A&HB taxonomy ID collision: {id}.");
    }
}

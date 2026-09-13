using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 3)]
public sealed class ArmBandVariantItems(
    TemplateTable templateTable,
    CustomItemService customItemService,
    ArmBandFeatureConfig featureConfig,
    ISptLogger<ArmBandVariantItems> logger) : IOnLoad
{
    private static readonly MongoId DefaultInventoryTpl = new("55d7217a4bdc2d86028b456d");
    private const string GridName = "main";
    private const string GridPrototype = "55d329c24bdc2d892f8b4567";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HashSet<MongoId> armBandFilter = RequireArmBandHost();
        ArmBandGridOrientation orientation = featureConfig.Value.ArmBandRoles.GridOrientation;

        foreach (IGrouping<string, ArmBandVariantDescriptor> visual in ArmBandVariantCatalog.All.GroupBy(x => x.SourceTemplateId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            MongoId sourceId = new(visual.Key);
            if (!templateTable.Items.ContainsKey(sourceId))
                throw new InvalidOperationException($"B&A&HB ArmBand visual source is missing: {visual.Key}.");
            foreach (ArmBandVariantDescriptor variant in visual)
                EnsureVariant(variant, sourceId, orientation);
        }

        foreach (ArmBandVariantDescriptor variant in ArmBandVariantCatalog.All)
        {
            MongoId id = new(variant.TemplateId);
            if (!templateTable.Items.ContainsKey(id))
                throw new InvalidOperationException($"B&A&HB ArmBand variant was not registered before host publication: {variant.TemplateId}.");
            armBandFilter.Add(id);
        }

        WearableProtectionRuntime.ConfigureVariantRoots(featureConfig.Value.ArmBandRoles.ProtectionTemplateAllowlist);
        logger.Success($"B&A&HB immutable ArmBand variants registered: visuals=26, roles=5, templates={ArmBandVariantCatalog.All.Length}, orientation={orientation}; exact ArmBand host exposure committed.");
        return Task.CompletedTask;
    }

    private void EnsureVariant(ArmBandVariantDescriptor variant, MongoId sourceId, ArmBandGridOrientation orientation)
    {
        MongoId variantId = new(variant.TemplateId);
        if (templateTable.Items.TryGetValue(variantId, out TemplateItem? existing))
        {
            ValidateExisting(existing, variant, orientation);
            return;
        }

        var sourceHandbook = templateTable.Handbook.Items.FirstOrDefault(entry => entry.Id == sourceId)
            ?? templateTable.Handbook.Items.Single(entry => entry.Id == new MongoId("5b3f16c486f7747c327f55f7"));
        string marker = Marker(variant.Role, russian: false);
        string ruMarker = Marker(variant.Role, russian: true);
        var details = new NewItemFromCloneDetails
        {
            NewItemName = $"B&A&HB {variant.VisualKey} {variant.Role} ArmBand",
            ItemTplToClone = sourceId,
            ParentId = RuntimeCandidateBeltItem.CustomBeltParentTpl,
            NewId = variant.TemplateId,
            FleaPriceRoubles = Price(variant.Role),
            HandbookPriceRoubles = Price(variant.Role),
            HandbookParentId = sourceHandbook.ParentId,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new()
                {
                    Name = $"{variant.VisualKey} ArmBand [{marker}]",
                    ShortName = $"{variant.VisualKey} [{marker}]",
                    Description = $"A 1x2 {RoleDescription(variant.Role, false)} ArmBand. Its functional role is permanent and independent of its visual design."
                },
                ["ru"] = new()
                {
                    Name = $"Повязка {variant.VisualKey} [{ruMarker}]",
                    ShortName = $"{variant.VisualKey} [{ruMarker}]",
                    Description = $"Наручная повязка 1x2: {RoleDescription(variant.Role, true)}. Назначение постоянно и не зависит от внешнего вида."
                }
            },
            OverrideProperties = new TemplateItemProperties
            {
                ExaminedByDefault = true,
                Grids =
                [
                    new Grid
                    {
                        Name = GridName,
                        Id = variant.GridId,
                        Parent = variant.TemplateId,
                        Prototype = GridPrototype,
                        Properties = new GridProperties
                        {
                            CellsH = orientation == ArmBandGridOrientation.Vertical ? 1 : 2,
                            CellsV = orientation == ArmBandGridOrientation.Vertical ? 2 : 1,
                            MinCount = 0,
                            MaxCount = 0,
                            MaxWeight = 0,
                            IsSortingTable = false,
                            Filters = [new GridFilter { Filter = Filter(variant.Role), ExcludedFilter = [] }]
                        }
                    }
                ]
            }
        };

        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
            throw new InvalidOperationException($"B&A&HB ArmBand variant creation failed ({variant.TemplateId}): {string.Join("; ", result.Errors)}");
    }

    private HashSet<MongoId> RequireArmBandHost()
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out TemplateItem? inventory))
            throw new InvalidOperationException("B&A&HB default inventory template is missing.");
        Slot[] slots = inventory.Properties?.Slots?.Where(slot => string.Equals(slot.Name, "ArmBand", StringComparison.Ordinal)).Take(2).ToArray() ?? [];
        if (slots.Length != 1)
            throw new InvalidOperationException("B&A&HB ArmBand slot boundary is missing or ambiguous.");
        SlotFilter[] filters = slots[0].Properties?.Filters?.Take(2).ToArray() ?? [];
        if (filters.Length != 1 || filters[0].Filter == null)
            throw new InvalidOperationException("B&A&HB ArmBand host must have exactly one mutable filter group.");
        if (filters[0].Filter.Contains(RuntimeCandidateBeltItem.CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB ArmBand host contains the broad Belt parent; exact variant publication refused.");
        return filters[0].Filter;
    }

    private static void ValidateExisting(TemplateItem item, ArmBandVariantDescriptor variant, ArmBandGridOrientation orientation)
    {
        Grid[] grids = item.Properties?.Grids?.ToArray() ?? [];
        if (item.Parent != RuntimeCandidateBeltItem.CustomBeltParentTpl || grids.Length != 1)
            throw new InvalidOperationException($"B&A&HB ArmBand variant identity collision: {variant.TemplateId}.");
        Grid grid = grids[0];
        GridProperties? properties = grid.Properties;
        MongoId[] expected = Filter(variant.Role).OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        MongoId[] actual = properties?.Filters?.SingleOrDefault()?.Filter?.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray() ?? [];
        if (grid.Id.ToString() != variant.GridId || grid.Parent?.ToString() != variant.TemplateId
            || grid.Name != GridName
            || properties?.CellsH != (orientation == ArmBandGridOrientation.Vertical ? 1 : 2)
            || properties.CellsV != (orientation == ArmBandGridOrientation.Vertical ? 2 : 1)
            || !actual.SequenceEqual(expected))
            throw new InvalidOperationException($"B&A&HB ArmBand variant contract collision: {variant.TemplateId}.");
    }

    private static HashSet<MongoId> Filter(ArmBandRole role) => role switch
    {
        ArmBandRole.Medical => [new("543be5664bdc2dd4348b4569"), new("57864c8c245977548867e7f1")],
        ArmBandRole.Ammo => [new("5485a8684bdc2da71d8b4567"), new("543be5cb4bdc2deb348b4568")],
        ArmBandRole.Magazine => [BaseClasses.MAGAZINE],
        ArmBandRole.Technical => [new("57864bb7245977548b3b66c2"), new("57864a66245977548f04a81f"), new("57864ee62459775490116fc1")],
        ArmBandRole.Currency => [new("543be5dd4bdc2deb348b4569"), new("5783c43d2459774bbe137486"), new("60b0f6c058e0b0481a09ad11")],
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static int Price(ArmBandRole role) => role switch
    {
        ArmBandRole.Currency => 18000,
        ArmBandRole.Magazine => 25000,
        _ => 20000
    };

    private static string Marker(ArmBandRole role, bool russian) => (role, russian) switch
    {
        (ArmBandRole.Medical, false) => "MED", (ArmBandRole.Medical, true) => "МЕД",
        (ArmBandRole.Ammo, false) => "AMMO", (ArmBandRole.Ammo, true) => "ПАТР",
        (ArmBandRole.Magazine, false) => "MAG", (ArmBandRole.Magazine, true) => "МАГ",
        (ArmBandRole.Technical, false) => "TECH", (ArmBandRole.Technical, true) => "ТЕХ",
        (ArmBandRole.Currency, false) => "CASH", (ArmBandRole.Currency, true) => "ДЕН",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static string RoleDescription(ArmBandRole role, bool russian) => (role, russian) switch
    {
        (ArmBandRole.Medical, false) => "medical storage", (ArmBandRole.Medical, true) => "медицинское хранение",
        (ArmBandRole.Ammo, false) => "loose-ammunition storage", (ArmBandRole.Ammo, true) => "хранение патронов",
        (ArmBandRole.Magazine, false) => "magazine carrier", (ArmBandRole.Magazine, true) => "подсумок для магазинов",
        (ArmBandRole.Technical, false) => "technical storage", (ArmBandRole.Technical, true) => "техническое хранение",
        (ArmBandRole.Currency, false) => "currency storage", (ArmBandRole.Currency, true) => "хранение денег",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}

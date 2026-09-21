using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace TGC;

[Injectable(TypePriority = OnLoadOrder.Preload + 10), UsedImplicitly]
public sealed class TgcContentIntegration(
    ISptLogger<TgcContentIntegration> logger,
    WTTServerCommonLib.WTTServerCommonLib commonLib,
    ModHelper modHelper,
    TemplateTable templates,
    LocaleTable locales,
    GlobalTable globals) : IOnLoad
{
    private Dictionary<MongoId, ModItem> _definitions = [];

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var root = modHelper.GetAbsolutePathToModFolder(assembly);
        _definitions = modHelper.GetJsonDataFromFile<Dictionary<MongoId, ModItem>>(
            root, "db/CustomItems/modTGC_items.json");
        var clothing = modHelper.GetJsonDataFromFile<Dictionary<MongoId, ModClothing>>(
            root, "db/modTGC_clothes.json");
        var presets = modHelper.GetJsonDataFromFile<ModPresets>(root, "db/globals.json");

        await commonLib.CustomItemServiceExtended.CreateCustomItems(assembly, null);

        foreach (var (itemId, definition) in _definitions)
        {
            CopyCloneCompatibility(templates.Items, definition.ItemTplToClone, itemId);
            ApplyExplicitCompatibility(templates.Items, itemId, definition);
            // Deliberately do not mutate armband or secure-container filters.
            // Those two upstream flags are consumed exclusively by B&A&HB.
        }

        foreach (var (itemId, definition) in clothing)
        {
            AddClothing(templates.Customization, itemId, definition);
            AddClothingLocales(locales, itemId, definition);
        }

        foreach (var (presetId, preset) in presets.Items)
        {
            globals.ItemPresets[presetId] = preset;
        }

        logger.Success(
            $"[TGC Admiral Integration] loaded {_definitions.Count} items, " +
            $"{clothing.Count} clothing records and {presets.Items.Count} presets; " +
            "Painter and Belt/secure-container mutation disabled.");
    }

    private void CopyCloneCompatibility(
        Dictionary<MongoId, TemplateItem> items,
        MongoId cloneId,
        MongoId customId)
    {
        foreach (var (ownerId, owner) in items)
        {
            if (_definitions.ContainsKey(ownerId) || owner.Properties is null)
            {
                continue;
            }

            foreach (var slot in EnumerateSlots(owner))
            {
                var filter = slot.Properties?.Filters?.FirstOrDefault()?.Filter;
                if (filter?.Contains(cloneId) == true)
                {
                    filter.Add(customId);
                }
            }

            if (owner.Properties.ConflictingItems?.Contains(cloneId) == true)
            {
                owner.Properties.ConflictingItems.Add(customId);
            }
        }
    }

    private static void ApplyExplicitCompatibility(
        Dictionary<MongoId, TemplateItem> items,
        MongoId customId,
        ModItem definition)
    {
        if (definition.AddToThisItemsFilters is not null)
        {
            var custom = items[customId];
            foreach (var (slotName, additions) in definition.AddToThisItemsFilters)
            {
                if (slotName == "conflicts")
                {
                    custom.Properties!.ConflictingItems!.UnionWith(additions);
                    continue;
                }

                // Count is intentional. Upstream 3.0.0 uses Capacity and may
                // index past the populated list.
                foreach (var slot in EnumerateSlots(custom).Where(slot => slot.Name == slotName))
                {
                    slot.Properties?.Filters?.FirstOrDefault()?.Filter?.UnionWith(additions);
                }
            }
        }

        if (definition.AddToExistingItemFilters is null)
        {
            return;
        }

        foreach (var (slotName, owners) in definition.AddToExistingItemFilters)
        {
            foreach (var ownerId in owners.Where(items.ContainsKey))
            {
                var owner = items[ownerId];
                if (slotName == "conflicts")
                {
                    owner.Properties!.ConflictingItems!.Add(customId);
                    continue;
                }

                foreach (var slot in EnumerateSlots(owner).Where(slot => slot.Name == slotName))
                {
                    slot.Properties?.Filters?.FirstOrDefault()?.Filter?.Add(customId);
                }
            }
        }
    }

    private static IEnumerable<Slot> EnumerateSlots(TemplateItem item) =>
        (item.Properties?.Slots ?? [])
        .Concat(item.Properties?.Chambers ?? [])
        .Concat(item.Properties?.Cartridges ?? []);

    private static void AddClothing(
        Dictionary<MongoId, CustomizationItem> table,
        MongoId itemId,
        ModClothing definition)
    {
        var source = table[definition.Clone];
        var custom = Clone(source);
        var authored = definition.Customization.Properties;
        custom.Id = itemId;
        custom.Name = itemId;
        custom.Properties = Clone(source.Properties);
        custom.Properties.Side = authored.Side;
        custom.Properties.Prefab = authored.Prefab ?? custom.Properties.Prefab;
        custom.Properties.Body = authored.Body ?? custom.Properties.Body;
        custom.Properties.Hands = authored.Hands ?? custom.Properties.Hands;
        custom.Properties.Feet = authored.Feet ?? custom.Properties.Feet;
        table[itemId] = custom;
    }

    private static void AddClothingLocales(LocaleTable locales, MongoId itemId, ModClothing definition)
    {
        foreach (var (localeCode, lazyLocale) in locales.Global)
        {
            lazyLocale.AddTransformer(data =>
            {
                var authored = definition.Locales.GetValueOrDefault(localeCode)
                    ?? definition.Locales.GetValueOrDefault("en");
                if (data is null || authored is null)
                {
                    return data;
                }

                data[itemId] = authored.Name ?? string.Empty;
                data[$"{itemId} name"] = authored.Name ?? string.Empty;
                data[$"{itemId} description"] = authored.Description ?? string.Empty;
                return data;
            });
        }
    }

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}

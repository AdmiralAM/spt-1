using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

namespace EFT.InventoryLogic
{
    public class Item
    {
        public Item(string id, object template)
        {
            Id = id;
            Template = template;
        }

        public string Id { get; }
        public object Template { get; }
    }

    public class SearchableItemTemplate
    {
    }

    public class SearchableItem : Item
    {
        public SearchableItem(string id, SearchableItemTemplate template) : base(id, template)
        {
        }
    }

    public static class JsonTypes
    {
        public static readonly Dictionary<string, Type> TypeTable = new Dictionary<string, Type>();
        public static readonly Dictionary<string, Type> TemplateTypeTable = new Dictionary<string, Type>();
        public static readonly Dictionary<string, Func<string, object, Item>> ItemConstructors =
            new Dictionary<string, Func<string, object, Item>>();
    }
}

internal static class RuntimeCustomTaxonomyRegistrationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (!RuntimeCustomBeltTypes.BuildAndRegister())
            throw new InvalidOperationException("Custom taxonomy JsonTypes registration failed.");

        AssertComplete(RuntimeCustomBeltTypePatches.CustomTemplateParentId);
        AssertComplete(RuntimeCustomBeltTypePatches.CustomBeltParentId);

        RuntimeCustomBeltTypes.RollbackJsonMappings();
        AssertAbsent(RuntimeCustomBeltTypePatches.CustomTemplateParentId);
        AssertAbsent(RuntimeCustomBeltTypePatches.CustomBeltParentId);
    }

    static void AssertComplete(string id)
    {
        if (!EFT.InventoryLogic.JsonTypes.TypeTable.TryGetValue(id, out Type itemType)
            || itemType != RuntimeCustomBeltTypes.CustomBeltItemType)
            throw new InvalidOperationException("Taxonomy item-type mapping missing for " + id + ".");
        if (!EFT.InventoryLogic.JsonTypes.TemplateTypeTable.TryGetValue(id, out Type templateType)
            || templateType != RuntimeCustomBeltTypes.CustomTemplateType)
            throw new InvalidOperationException("Taxonomy template-type mapping missing for " + id + ".");
        if (!EFT.InventoryLogic.JsonTypes.ItemConstructors.TryGetValue(id, out Func<string, object, EFT.InventoryLogic.Item> factory))
            throw new InvalidOperationException("Taxonomy item constructor missing for " + id + ".");

        object template = Activator.CreateInstance(templateType);
        EFT.InventoryLogic.Item item = factory("regression", template);
        if (item == null || item.GetType() != itemType || !ReferenceEquals(item.Template, template))
            throw new InvalidOperationException("Taxonomy constructor mapping is not executable for " + id + ".");
    }

    static void AssertAbsent(string id)
    {
        if (EFT.InventoryLogic.JsonTypes.TypeTable.ContainsKey(id)
            || EFT.InventoryLogic.JsonTypes.TemplateTypeTable.ContainsKey(id)
            || EFT.InventoryLogic.JsonTypes.ItemConstructors.ContainsKey(id))
            throw new InvalidOperationException("Taxonomy rollback left an owned JsonTypes mapping for " + id + ".");
    }
}

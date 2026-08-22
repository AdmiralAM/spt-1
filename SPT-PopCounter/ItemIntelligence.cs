using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SPTPopCounter.ItemIntelligence
{
    public enum ItemCategory
    {
        Unknown,
        Food,
        Meds,
        Ammo,
        Weapon,
        Armor,
        Backpack,
        Container,
        Key,
        Quest,
        Barter
    }

    public enum ItemTag
    {
        Unknown,
        Healing,
        Hydration,
        Energy,
        Bleed,
        Pain,
        Fracture,
        Antidote,
        Stimulant,
        Throwable,
        Armor,
        Storage,
        Quest
    }

    public sealed class ItemDefinition
    {
        readonly ItemTag[] tagValues;
        readonly ReadOnlyCollection<ItemTag> tags;

        public ItemDefinition(string templateId, string name, string shortName, ItemCategory category, IEnumerable<ItemTag> tags)
        {
            TemplateId = ItemText.NormalizeId(templateId);
            Name = ItemText.NormalizeName(name, shortName);
            ShortName = ItemText.NormalizeName(shortName, Name);
            Category = category;

            HashSet<ItemTag> unique = new HashSet<ItemTag>();
            if (tags != null)
                foreach (ItemTag tag in tags) unique.Add(tag);
            if (category == ItemCategory.Unknown) unique.Add(ItemTag.Unknown);
            else unique.Remove(ItemTag.Unknown);

            tagValues = new ItemTag[unique.Count];
            unique.CopyTo(tagValues);
            Array.Sort(tagValues);
            this.tags = Array.AsReadOnly(tagValues);
        }

        public string TemplateId { get; }
        public string Name { get; }
        public string ShortName { get; }
        public ItemCategory Category { get; }
        public IReadOnlyList<ItemTag> Tags => tags;
        public bool IsKnown => Category != ItemCategory.Unknown;

        public bool HasTag(ItemTag tag)
        {
            for (int i = 0; i < tagValues.Length; i++)
                if (tagValues[i] == tag) return true;
            return false;
        }
    }

    public static class ItemIntelligenceRegistry
    {
        public static ItemRegistry Shared { get; } = ItemRegistry.CreateDefault();

        public static ItemDefinition Resolve(object itemOrTemplate)
        {
            return Shared.Resolve(itemOrTemplate);
        }
    }

    public sealed class ItemDescriptor
    {
        readonly Dictionary<string, object> signals;

        public ItemDescriptor(
            string templateId,
            string parentId,
            string name,
            string shortName,
            string typeName,
            IDictionary<string, object> signals = null)
        {
            TemplateId = ItemText.NormalizeId(templateId);
            ParentId = ItemText.NormalizeId(parentId);
            Name = ItemText.NormalizeName(name, shortName);
            ShortName = ItemText.NormalizeName(shortName, Name);
            TypeName = ItemText.NormalizeWhitespace(typeName);
            this.signals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (signals != null)
                foreach (KeyValuePair<string, object> signal in signals)
                    if (!string.IsNullOrWhiteSpace(signal.Key)) this.signals[signal.Key.Trim()] = signal.Value;
        }

        public string TemplateId { get; }
        public string ParentId { get; }
        public string Name { get; }
        public string ShortName { get; }
        public string TypeName { get; }
        public IReadOnlyDictionary<string, object> Signals => signals;

        public bool TryGet(string name, out object value) => signals.TryGetValue(name, out value);

        public static ItemDescriptor FromObject(object itemOrTemplate)
        {
            return ItemObjectReader.Read(itemOrTemplate);
        }
    }

    public interface IItemMatcher
    {
        ItemCategory Match(ItemDescriptor item, ISet<ItemTag> tags);
    }

    public sealed class SemanticItemMatcher : IItemMatcher
    {
        public ItemCategory Match(ItemDescriptor item, ISet<ItemTag> tags)
        {
            if (item == null)
            {
                tags.Add(ItemTag.Unknown);
                return ItemCategory.Unknown;
            }

            string text = SearchText(item);
            AddSemanticTags(item, text, tags);

            ItemCategory category;
            object explicitCategory;
            if (item.TryGet("Category", out explicitCategory) && TryCategory(explicitCategory, out category))
                return Finish(category, tags);

            if (IsTrue(item, "QuestItem") || Has(text, "questitem", "quest item")) category = ItemCategory.Quest;
            else if (HasSignal(item, "Caliber", "AmmoType", "AmmoClass") || Has(text, "ammoitem", "ammunition", "cartridge")) category = ItemCategory.Ammo;
            else if (HasSignal(item, "WeaponClass", "WeapClass", "FireRate") || Has(text, "weaponitem", "firearm", "meleeweapon", "knifecomponent")) category = ItemCategory.Weapon;
            else if (HasSignal(item, "ArmorClass", "ArmorType") || Has(text, "armorcomponent", "armoredequipment", "bodyarmor")) category = ItemCategory.Armor;
            else if (Has(text, "backpack", "bagcomponent")) category = ItemCategory.Backpack;
            else if (Has(text, "keycomponent", "keyitem", "mechanicalkey") || HasSignal(item, "MaximumNumberOfUsage", "MaxNumberOfUsage")) category = ItemCategory.Key;
            else if (Has(text, "medicalitem", "meditem", "medscomponent", "drugitem", "stimulant") || HasSignal(item, "HpResource", "MaxHpResource", "MedUseTime")) category = ItemCategory.Meds;
            else if (Has(text, "fooditem", "drinkitem", "foodcomponent") || HasSignal(item, "Hydration", "Energy", "FoodUseTime")) category = ItemCategory.Food;
            else if (Has(text, "barteritem", "bartercomponent")) category = ItemCategory.Barter;
            else if (Has(text, "container", "tacticalrig", "vestcomponent") || HasSignal(item, "Grids")) category = ItemCategory.Container;
            else category = ItemCategory.Unknown;

            return Finish(category, tags);
        }

        static ItemCategory Finish(ItemCategory category, ISet<ItemTag> tags)
        {
            if (category == ItemCategory.Armor) tags.Add(ItemTag.Armor);
            if (category == ItemCategory.Backpack || category == ItemCategory.Container) tags.Add(ItemTag.Storage);
            if (category == ItemCategory.Quest) tags.Add(ItemTag.Quest);
            if (category == ItemCategory.Unknown) tags.Add(ItemTag.Unknown);
            else tags.Remove(ItemTag.Unknown);
            return category;
        }

        static void AddSemanticTags(ItemDescriptor item, string text, ISet<ItemTag> tags)
        {
            if (HasSignal(item, "HpResource", "MaxHpResource", "HealthEffects") || Has(text, "healing", "healthrestore")) tags.Add(ItemTag.Healing);
            if (HasNonZero(item, "Hydration") || Has(text, "hydration")) tags.Add(ItemTag.Hydration);
            if (HasNonZero(item, "Energy") || Has(text, "energy")) tags.Add(ItemTag.Energy);
            if (Has(text, "bleed", "hemorrhage")) tags.Add(ItemTag.Bleed);
            if (Has(text, "pain", "analgesic")) tags.Add(ItemTag.Pain);
            if (Has(text, "fracture", "brokenbone")) tags.Add(ItemTag.Fracture);
            if (Has(text, "antidote", "toxin", "poison")) tags.Add(ItemTag.Antidote);
            if (Has(text, "stimulant", "stimulator", "injector")) tags.Add(ItemTag.Stimulant);
            if (Has(text, "throwable", "grenade", "explosiveitem")) tags.Add(ItemTag.Throwable);
        }

        static string SearchText(ItemDescriptor item)
        {
            StringBuilder text = new StringBuilder(256);
            Append(text, item.TypeName);
            Append(text, item.ParentId);
            Append(text, item.Name);
            Append(text, item.ShortName);
            foreach (KeyValuePair<string, object> signal in item.Signals)
            {
                Append(text, signal.Key);
                AppendValue(text, signal.Value, 0);
            }
            return text.ToString().ToLowerInvariant();
        }

        static void AppendValue(StringBuilder target, object value, int depth)
        {
            if (value == null || depth > 2) return;
            string scalar = value as string;
            if (scalar != null)
            {
                Append(target, scalar);
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                int count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ >= 32) break;
                    Append(target, entry.Key == null ? null : entry.Key.ToString());
                    AppendValue(target, entry.Value, depth + 1);
                }
                return;
            }

            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                int count = 0;
                foreach (object entry in sequence)
                {
                    if (count++ >= 32) break;
                    AppendValue(target, entry, depth + 1);
                }
                return;
            }

            Append(target, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        static void Append(StringBuilder target, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (target.Length > 0) target.Append('|');
            target.Append(value);
        }

        static bool Has(string source, params string[] markers)
        {
            for (int i = 0; i < markers.Length; i++)
                if (source.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool HasSignal(ItemDescriptor item, params string[] names)
        {
            object value;
            for (int i = 0; i < names.Length; i++)
                if (item.TryGet(names[i], out value) && value != null) return true;
            return false;
        }

        static bool HasNonZero(ItemDescriptor item, string name)
        {
            object value;
            if (!item.TryGet(name, out value) || value == null) return false;
            try { return Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > double.Epsilon; }
            catch { return true; }
        }

        static bool IsTrue(ItemDescriptor item, string name)
        {
            object value;
            if (!item.TryGet(name, out value) || value == null) return false;
            if (value is bool) return (bool)value;
            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        static bool TryCategory(object value, out ItemCategory category)
        {
            return Enum.TryParse(value == null ? string.Empty : value.ToString(), true, out category);
        }
    }

    public sealed class ItemRegistry
    {
        readonly object sync = new object();
        readonly IItemMatcher matcher;
        readonly Dictionary<string, ItemDefinition> exact = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ItemCategory> parents = new Dictionary<string, ItemCategory>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ItemDefinition> cache = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        public ItemRegistry(IItemMatcher matcher = null)
        {
            this.matcher = matcher ?? new SemanticItemMatcher();
        }

        public static ItemRegistry CreateDefault() => new ItemRegistry(new SemanticItemMatcher());

        public void Register(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(definition.TemplateId)) throw new ArgumentException("A registered definition requires a template id.", nameof(definition));
            lock (sync)
            {
                exact[definition.TemplateId] = definition;
                cache.Remove(definition.TemplateId);
            }
        }

        public void RegisterParent(string parentId, ItemCategory category)
        {
            string normalized = ItemText.NormalizeId(parentId);
            if (string.IsNullOrEmpty(normalized)) throw new ArgumentException("A parent mapping requires an id.", nameof(parentId));
            lock (sync)
            {
                parents[normalized] = category;
                cache.Clear();
            }
        }

        public ItemDefinition Resolve(object itemOrTemplate)
        {
            return Resolve(ItemDescriptor.FromObject(itemOrTemplate));
        }

        public ItemDefinition Resolve(ItemDescriptor descriptor)
        {
            if (descriptor == null) return Unknown(null);

            lock (sync)
            {
                ItemDefinition known;
                if (!string.IsNullOrEmpty(descriptor.TemplateId) && exact.TryGetValue(descriptor.TemplateId, out known)) return known;
                if (!string.IsNullOrEmpty(descriptor.TemplateId) && cache.TryGetValue(descriptor.TemplateId, out known)) return known;

                HashSet<ItemTag> tags = new HashSet<ItemTag>();
                ItemCategory category = matcher.Match(descriptor, tags);
                ItemCategory parentCategory;
                if (!string.IsNullOrEmpty(descriptor.ParentId) && parents.TryGetValue(descriptor.ParentId, out parentCategory))
                {
                    category = parentCategory;
                    AddCategoryTags(category, tags);
                }

                ItemDefinition resolved = new ItemDefinition(descriptor.TemplateId, descriptor.Name, descriptor.ShortName, category, tags);
                if (!string.IsNullOrEmpty(descriptor.TemplateId)) cache[descriptor.TemplateId] = resolved;
                return resolved;
            }
        }

        public ItemDefinition Unknown(object itemOrTemplate)
        {
            ItemDescriptor descriptor = itemOrTemplate as ItemDescriptor ?? ItemDescriptor.FromObject(itemOrTemplate);
            return new ItemDefinition(
                descriptor == null ? string.Empty : descriptor.TemplateId,
                descriptor == null ? "Unknown item" : descriptor.Name,
                descriptor == null ? "Unknown item" : descriptor.ShortName,
                ItemCategory.Unknown,
                new[] { ItemTag.Unknown });
        }

        static void AddCategoryTags(ItemCategory category, ISet<ItemTag> tags)
        {
            tags.Remove(ItemTag.Unknown);
            if (category == ItemCategory.Unknown) tags.Add(ItemTag.Unknown);
            if (category == ItemCategory.Armor) tags.Add(ItemTag.Armor);
            if (category == ItemCategory.Backpack || category == ItemCategory.Container) tags.Add(ItemTag.Storage);
            if (category == ItemCategory.Quest) tags.Add(ItemTag.Quest);
        }
    }

    static class ItemObjectReader
    {
        static readonly string[] signalNames =
        {
            "Category", "QuestItem", "Caliber", "AmmoType", "AmmoClass", "WeaponClass", "WeapClass", "FireRate",
            "ArmorClass", "ArmorType", "Grids", "Slots", "MaximumNumberOfUsage", "MaxNumberOfUsage",
            "HpResource", "MaxHpResource", "MedUseTime", "Hydration", "Energy", "FoodUseTime",
            "HealthEffects", "Effects", "Buffs", "StimulatorBuffs", "Damage", "ExplosionStrength"
        };

        public static ItemDescriptor Read(object source)
        {
            if (source == null) return null;
            ItemDescriptor descriptor = source as ItemDescriptor;
            if (descriptor != null) return descriptor;

            object template = ReadMember(source, "Template") ?? source;
            object properties = ReadMember(template, "Props") ?? ReadMember(template, "_props") ?? ReadMember(template, "Properties");
            Dictionary<string, object> signals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            CollectDictionary(properties, signals);
            for (int i = 0; i < signalNames.Length; i++)
            {
                object value = ReadMember(properties, signalNames[i]) ?? ReadMember(template, signalNames[i]);
                if (value != null) signals[signalNames[i]] = value;
            }

            return new ItemDescriptor(
                FirstString(source, "TemplateId", "Tpl", "_tpl", "Id", "_id") ?? FirstString(template, "TemplateId", "Id", "_id"),
                FirstString(template, "ParentId", "Parent", "_parent"),
                FirstString(template, "Name", "LocalizedName", "_name") ?? FirstString(source, "Name", "LocalizedName", "_name"),
                FirstString(template, "ShortName", "LocalizedShortName") ?? FirstString(source, "ShortName", "LocalizedShortName"),
                template.GetType().FullName ?? template.GetType().Name,
                signals);
        }

        static void CollectDictionary(object source, IDictionary<string, object> target)
        {
            IDictionary dictionary = source as IDictionary;
            if (dictionary == null) return;
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key == null ? null : entry.Key.ToString();
                if (!string.IsNullOrWhiteSpace(key)) target[key] = entry.Value;
            }
        }

        static string FirstString(object source, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                object value = ReadMember(source, names[i]);
                string text = value == null ? null : value.ToString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
            return null;
        }

        static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                    if (string.Equals(entry.Key == null ? null : entry.Key.ToString(), name, StringComparison.OrdinalIgnoreCase)) return entry.Value;
                return null;
            }

            Type type = source.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            try
            {
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(source, null);
            }
            catch { }
            try
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(source);
            }
            catch { }
            return null;
        }
    }

    static class ItemText
    {
        public static string NormalizeId(string value)
        {
            return NormalizeWhitespace(value).ToLowerInvariant();
        }

        public static string NormalizeName(string primary, string fallback)
        {
            string value = NormalizeWhitespace(primary);
            if (value.Length == 0) value = NormalizeWhitespace(fallback);
            return value.Length == 0 ? "Unknown item" : value;
        }

        public static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder result = new StringBuilder(value.Length);
            bool whitespace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    whitespace = result.Length > 0;
                    continue;
                }
                if (whitespace) result.Append(' ');
                result.Append(c);
                whitespace = false;
            }
            return result.ToString();
        }
    }
}

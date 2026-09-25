using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class AmandsSenseIntegration : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.itemintelligence.amandssense";
        static AmandsSenseIntegration active;
        readonly ItemIntelligenceUiSettings settings;
        readonly ItemPresentationStore store;
        readonly RaidRequirementLedger ledger;
        readonly Action raidChanged;
        readonly Action raidStarted;
        readonly HashSet<string> pickedItemIds = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<object> trackedSenseItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
        readonly List<object> staleSenseItems = new List<object>();
        readonly Dictionary<object, SenseEvaluationCache> evaluationCache = new Dictionary<object, SenseEvaluationCache>(ReferenceEqualityComparer.Instance);
        readonly ConditionalWeakTable<object, SenseTextBaseline> textBaselines = new ConditionalWeakTable<object, SenseTextBaseline>();
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        int pickupObservedReported;

        public AmandsSenseIntegration(ItemIntelligenceUiSettings settings, ItemPresentationStore store,
            Action<string> logInfo, Action<string> logWarning,
            RaidRequirementLedger ledger = null, Action raidChanged = null, Action raidStarted = null)
        {
            this.settings = settings;
            this.store = store;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.ledger = ledger ?? new RaidRequirementLedger();
            this.raidChanged = raidChanged;
            this.raidStarted = raidStarted;
        }

        public bool IsInstalled { get; private set; }

        public bool TryInstall()
        {
            if (IsInstalled || !settings.SenseIntegration) return IsInstalled;
            try
            {
                Assembly sense = FindAssembly("AmandsSense");
                Type itemType = sense == null ? null : sense.GetType("AmandsSense.Components.AmandsSenseItem", false);
                Type containerType = sense == null ? null : sense.GetType("AmandsSense.Components.AmandsSenseContainer", false);
                Type senseClass = sense == null ? null : sense.GetType("AmandsSense.Components.AmandsSenseClass", false);
                MethodInfo setSense = FindMethod(itemType, "SetSense", 1);
                MethodInfo setContainerSense = FindMethod(containerType, "SetSense", 1);
                MethodInfo updateContainerSense = FindMethod(containerType, "UpdateSense", 0);
                MethodInfo remove = FindMethod(itemType, "RemoveLootItem", 1);
                MethodInfo clear = FindMethod(senseClass, "Clear", 0);
                if (setSense == null || setContainerSense == null || updateContainerSense == null || remove == null || clear == null)
                {
                    if (logWarning != null) logWarning("Item Intelligence Sense bridge unavailable: Item.SetSense=" + (setSense != null) +
                        ", Container.SetSense=" + (setContainerSense != null) + ", Container.UpdateSense=" + (updateContainerSense != null) +
                        ", RemoveLootItem=" + (remove != null) + ", Clear=" + (clear != null));
                    return false;
                }

                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                ConstructorInfo hmCtor = harmonyMethodType == null ? null : harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
                if (harmonyType == null || hmCtor == null || patch == null) return false;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(patch, hmCtor, setSense, null, typeof(AmandsSenseIntegration).GetMethod(nameof(SetSensePostfix), BindingFlags.Static | BindingFlags.NonPublic));
                Patch(patch, hmCtor, setContainerSense, null, typeof(AmandsSenseIntegration).GetMethod(nameof(SetSensePostfix), BindingFlags.Static | BindingFlags.NonPublic));
                Patch(patch, hmCtor, updateContainerSense, null, typeof(AmandsSenseIntegration).GetMethod(nameof(ContainerUpdatePostfix), BindingFlags.Static | BindingFlags.NonPublic));
                Patch(patch, hmCtor, remove, typeof(AmandsSenseIntegration).GetMethod(nameof(RemovePrefix), BindingFlags.Static | BindingFlags.NonPublic), null);
                Patch(patch, hmCtor, clear, null, typeof(AmandsSenseIntegration).GetMethod(nameof(ClearPostfix), BindingFlags.Static | BindingFlags.NonPublic));
                active = this;
                IsInstalled = true;
                if (logInfo != null) logInfo("Item Intelligence Amands Sense integration installed for Sense 3.1.x.");
                return true;
            }
            catch (Exception error)
            {
                if (logWarning != null) logWarning("Item Intelligence left Amands Sense unchanged: " + error.Message);
                Dispose();
                return false;
            }
        }

        static void SetSensePostfix(object __instance) { AmandsSenseIntegration value = active; if (value != null) value.Apply(__instance); }
        static void ContainerUpdatePostfix(object __instance)
        {
            AmandsSenseIntegration value = active;
            if (value == null || __instance == null) return;
            value.evaluationCache.Remove(__instance);
            value.Apply(__instance);
        }
        static void RemovePrefix(object __instance, object __0) { AmandsSenseIntegration value = active; if (value != null) value.RecordPickup(__instance, __0); }
        static void ClearPostfix() { AmandsSenseIntegration value = active; if (value != null) value.ResetRaid(); }

        void Apply(object senseItem)
        {
            if (!settings.SenseIntegration || senseItem == null) return;
            bool isContainer = IsContainer(senseItem);
            bool wantsContainerValue = isContainer && settings.SenseContainerValues;
            if (!settings.SenseRequiredItems && !settings.SenseCategories && !wantsContainerValue)
            {
                ApplySenseTextScale(senseItem, isContainer);
                if (isContainer) HideNativeLootLabel(senseItem);
                return;
            }
            trackedSenseItems.Add(senseItem);
            if (settings.SenseRequiredItems && ledger.BeginRaid())
            {
                if (raidStarted != null) raidStarted();
                if (raidChanged != null) raidChanged();
            }
            ItemPresentationIndex index = store.Current;
            object observed = Member(senseItem, "observedLootItem");
            object item = Member(observed, "Item");
            string itemId = settings.SenseRequiredItems ? Text(Member(item, "Id", "ID")) : string.Empty;
            if (itemId.Length > 0 && pickedItemIds.Remove(itemId) && ledger.Remove(itemId))
            {
                evaluationCache.Clear();
                if (raidChanged != null) raidChanged();
            }

            SenseVisualPolicy policy;
            long containerTotalValue;
            SenseEvaluationCache cached;
            if (evaluationCache.TryGetValue(senseItem, out cached) && ReferenceEquals(cached.Index, index) &&
                cached.LedgerRevision == ledger.Revision && cached.SettingsRevision == settings.Revision)
            {
                policy = cached.Policy;
                isContainer = cached.IsContainer;
                containerTotalValue = cached.ContainerTotalValue;
            }
            else
            {
                List<SenseVisualPolicy> candidates = new List<SenseVisualPolicy>();
                Dictionary<ItemNeedReason, int> categoryCounts = new Dictionary<ItemNeedReason, int>();
                string currencyCode = string.Empty;
                int currencyItems = 0;
                containerTotalValue = 0;
                bool observedContainer = isContainer;
                foreach (object contained in EnumerateSenseItemTree(senseItem, item))
                {
                    string templateId = Text(Member(contained, "TemplateId", "Tpl"));
                    if (templateId.Length == 0) continue;
                    int stackCount = Math.Max(1, Number(Member(contained, "StackObjectsCount"), 1));
                    ItemPresentationState state = index.Get(templateId);
                    if (settings.SenseContainerValues && observedContainer && !IsContainerRoot(senseItem, item, contained))
                    {
                        long unitValue = state.Price == null ? 0 : state.Price.FleaUnitValue;
                        if (unitValue > 0) containerTotalValue = SaturatingAdd(containerTotalValue, SaturatingMultiply(unitValue, stackCount));
                    }
                    ItemNeedReason itemCategory = settings.SenseCategories ? SenseCategory(contained) : ItemNeedReason.None;
                    if (itemCategory != ItemNeedReason.None)
                    {
                        int count;
                        categoryCounts.TryGetValue(itemCategory, out count);
                        categoryCounts[itemCategory] = count + stackCount;
                        if (itemCategory == ItemNeedReason.Currency)
                        {
                            string itemCurrencyCode = CurrencyCode(contained);
                            if (currencyItems == 0) currencyCode = itemCurrencyCode;
                            else if (currencyCode != itemCurrencyCode) currencyCode = "MIXED";
                            currencyItems++;
                        }
                    }
                    if (settings.SenseRequiredItems)
                    {
                        bool fir = Flag(Member(contained, "SpawnedInSession"));
                        ItemRequirementAllocation allocation = state.Requirement == null ? null : state.Requirement.Allocation;
                        ItemIntelligenceDecision decision = ledger.Evaluate(templateId, allocation, fir);
                        candidates.Add(SenseVisualPolicyEngine.Evaluate(decision.Allocation));
                    }
                }
                SenseVisualPolicy fallbackCategory = settings.SenseCategories ? CategoryPolicy(categoryCounts, currencyCode) : null;
                policy = SenseContainerPolicyEngine.Select(candidates, fallbackCategory,
                    isContainer && HasProtectedSenseVisual(senseItem), isContainer ? containerTotalValue : 0);
                if (evaluationCache.Count >= 512) evaluationCache.Clear();
                evaluationCache[senseItem] = new SenseEvaluationCache(index, ledger.Revision, settings.Revision, policy, isContainer, containerTotalValue);
            }
            ApplySenseTextScale(senseItem, isContainer);
            if (isContainer) HideNativeLootLabel(senseItem);
            bool hasContainerValue = isContainer && containerTotalValue > 0;
            bool hasRequirementPolicy = policy.Stock != SenseStockState.None;
            bool preserveNative = HasProtectedSenseVisual(senseItem) && !hasRequirementPolicy && !hasContainerValue;
            if (!policy.HasItemIntelligence && !hasContainerValue || preserveNative)
            {
                return;
            }

            Color primary = policy.HasItemIntelligence ? settings.GetSenseColor(policy.Category) : Color.white;
            Color stock = settings.GetSenseStockColor(policy.Stock);
            bool completedContainer = isContainer && policy.Stock == SenseStockState.Complete;
            bool preserveIcon = HasProtectedSenseVisual(senseItem) && !hasRequirementPolicy;
            Color valueBase = policy.HasItemIntelligence ? primary : settings.GetSenseContainerValueColor(containerTotalValue);
            Color valueColor = hasContainerValue ? ApplyContainerValueBrightness(valueBase, settings.GetSenseContainerValueBrightness(containerTotalValue)) : primary;
            Color renderColor = completedContainer ? stock : hasRequirementPolicy ? primary : hasContainerValue ? valueColor : primary;
            if (!preserveIcon && (policy.HasItemIntelligence || hasContainerValue)) SetField(senseItem, "color", renderColor);
            Color secondary = policy.HasItemIntelligence && policy.Stock == SenseStockState.None && hasContainerValue
                ? primary
                : !policy.HasItemIntelligence || policy.SecondaryCategory == ItemNeedReason.None || !settings.SenseSecondaryOutline
                    ? primary : settings.GetSenseColor(policy.SecondaryCategory);
            if (policy.HasItemIntelligence && !preserveIcon) SetField(senseItem, "outlineColor", secondary);

            ItemNeedIcon renderedIcon = completedContainer ? ItemNeedIcon.Complete : policy.Icon;
            object sprite = policy.HasItemIntelligence && !preserveIcon ? FindSenseSprite(senseItem.GetType().Assembly, IconFile(renderedIcon, senseItem)) : Member(senseItem, "sprite");
            if (policy.HasItemIntelligence && !preserveIcon && sprite != null) SetField(senseItem, "sprite", sprite);
            if (!preserveIcon && (policy.HasItemIntelligence || hasContainerValue)) ApplyRenderer(Member(senseItem, "spriteRenderer"), sprite, renderColor);
            if (!preserveIcon && (policy.HasItemIntelligence || hasContainerValue)) ApplyLight(Member(senseItem, "light"), renderColor);
            object typeText = Member(senseItem, "typeText");
            if (settings.SenseRemainingText)
            {
                SenseVisualPolicy textPolicy = preserveIcon ? new SenseVisualPolicy(ItemNeedIcon.None, ItemNeedReason.None, ItemNeedReason.None, SenseStockState.None, 0) : policy;
                ApplyText(typeText, CompactText(textPolicy, primary, stock, isContainer), Color.white, secondary);
            }
            if (isContainer)
            {
                object nativeCount = Member(senseItem, "descriptionText");
                if (nativeCount != null)
                {
                    Color countColor = settings.GetSenseCountColor(Number(Member(senseItem, "itemCount"), 0));
                    object existingCountColor = Member(nativeCount, "color");
                    if (existingCountColor is Color) countColor.a = ((Color)existingCountColor).a;
                    SetMember(nativeCount, "color", countColor);
                }
            }
        }

        internal void RefreshActive()
        {
            if (!IsInstalled || !settings.SenseIntegration ||
                (!settings.SenseRequiredItems && !settings.SenseCategories && !settings.SenseContainerValues)) return;
            staleSenseItems.Clear();
            object[] snapshot = new object[trackedSenseItems.Count];
            trackedSenseItems.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
            {
                object senseItem = snapshot[i];
                if (!IsAlive(senseItem))
                {
                    staleSenseItems.Add(senseItem);
                    continue;
                }
                try { Apply(senseItem); }
                catch { staleSenseItems.Add(senseItem); }
            }
            for (int i = 0; i < staleSenseItems.Count; i++) trackedSenseItems.Remove(staleSenseItems[i]);
            staleSenseItems.Clear();
        }

        static bool IsAlive(object value)
        {
            if (value == null) return false;
            Component component = value as Component;
            if (ReferenceEquals(component, null)) return true;
            try { return component != null && component.gameObject != null && component.gameObject.activeInHierarchy; }
            catch { return false; }
        }

        static IEnumerable<object> EnumerateSenseItemTree(object senseItem, object looseItem)
        {
            HashSet<object> yielded = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (object value in EnumerateItemTree(looseItem))
                if (yielded.Add(value)) yield return value;

            // AmandsSense uses a separate component for world containers. Its loot is owned by
            // LootableContainer.ItemOwner and is therefore not reachable from observedLootItem.
            object lootableContainer = Member(senseItem, "lootableContainer");
            object owner = Member(lootableContainer, "ItemOwner", "Owner");
            object root = Member(owner, "RootItem");
            foreach (object value in EnumerateItemTree(root))
                if (yielded.Add(value)) yield return value;

            IEnumerable ownerItems = Member(owner, "Items", "AllItems", "AllRealPlayerItems") as IEnumerable;
            if (ownerItems == null) yield break;
            foreach (object owned in ownerItems)
                foreach (object value in EnumerateItemTree(owned))
                    if (yielded.Add(value)) yield return value;
        }

        static IEnumerable<object> EnumerateItemTree(object root)
        {
            if (root == null) yield break;
            Queue<object> pending = new Queue<object>();
            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            pending.Enqueue(root);
            while (pending.Count > 0 && seen.Count < 512)
            {
                object item = pending.Dequeue();
                if (item == null || !seen.Add(item)) continue;
                yield return item;
                EnqueueItems(InvokeEnumerable(item, "GetAllItems"), pending);
                EnqueueItems(Member(item, "Children", "AllItems", "Items"), pending);
                EnqueueContained(Member(item, "Containers"), pending);
                EnqueueContained(Member(item, "Grids"), pending);
                EnqueueContained(Member(item, "Slots"), pending);
                EnqueueContained(Member(item, "Cartridges"), pending);
                EnqueueContained(Member(item, "Chambers"), pending);
            }
        }

        static void EnqueueContained(object containers, Queue<object> pending)
        {
            IEnumerable values = containers as IEnumerable;
            if (values == null || containers is string) return;
            foreach (object container in values)
            {
                if (container == null) continue;
                object contained = Member(container, "ContainedItem", "Item");
                if (contained != null) pending.Enqueue(contained);
                EnqueueItems(Member(container, "Items", "ContainedItems", "Children"), pending);
            }
        }

        static void EnqueueItems(object values, Queue<object> pending)
        {
            IEnumerable enumerable = values as IEnumerable;
            if (enumerable == null || values is string) return;
            foreach (object value in enumerable) if (value != null) pending.Enqueue(value);
        }

        static object InvokeEnumerable(object target, string name)
        {
            if (target == null) return null;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (method == null) continue;
                try { return method.Invoke(target, null); } catch { return null; }
            }
            return null;
        }

        void RecordPickup(object senseItem, object eventArgs)
        {
            if (!settings.SenseIntegration || !IsSuccess(eventArgs)) return;
            if (!settings.SenseRequiredItems)
            {
                evaluationCache.Clear();
                if (raidChanged != null) raidChanged();
                return;
            }
            object item = Member(Member(senseItem, "observedLootItem"), "Item");
            bool changed = false;
            int observed = 0;
            foreach (object picked in EnumerateItemTree(item))
            {
                string id = Text(Member(picked, "Id", "ID"));
                string template = Text(Member(picked, "TemplateId", "Tpl"));
                if (id.Length == 0 || template.Length == 0) continue;
                int stack = Math.Max(1, Number(Member(picked, "StackObjectsCount"), 1));
                bool fir = Flag(Member(picked, "SpawnedInSession"));
                changed |= ledger.Observe(id, template, stack, fir);
                pickedItemIds.Add(id);
                observed++;
            }
            if (changed)
            {
                evaluationCache.Clear();
                if (raidChanged != null) raidChanged();
            }
            if (observed > 0 && System.Threading.Interlocked.Exchange(ref pickupObservedReported, 1) == 0 && logInfo != null)
                logInfo("Item Intelligence raid inventory pickup tracking active; item tree records=" + observed + ".");
        }

        void ResetRaid()
        {
            int revision = ledger.Revision;
            ledger.Reset();
            pickedItemIds.Clear();
            trackedSenseItems.Clear();
            staleSenseItems.Clear();
            evaluationCache.Clear();
            if (ledger.Revision != revision && raidChanged != null) raidChanged();
        }

        static string Label(ItemNeedReason reason, string currencyCode = "")
        {
            if (reason == ItemNeedReason.ActiveQuest) return GameUiText.T("QUEST", "КВЕСТ");
            if (reason == ItemNeedReason.Hideout) return GameUiText.T("HIDEOUT", "УБЕЖИЩЕ");
            if (reason == ItemNeedReason.Food) return GameUiText.T("FOOD", "ЕДА");
            if (reason == ItemNeedReason.Water) return GameUiText.T("WATER", "ВОДА");
            if (reason == ItemNeedReason.Key) return GameUiText.T("KEY", "КЛЮЧ");
            if (reason == ItemNeedReason.Grenade) return GameUiText.T("GRENADE", "ГРАНАТА");
            if (reason == ItemNeedReason.Currency)
            {
                string symbol = currencyCode == "USD" ? "$" : currencyCode == "EUR" ? "€" : currencyCode == "RUB" ? "₽" : string.Empty;
                return GameUiText.T("CURRENCY", "ВАЛЮТА") + (symbol.Length == 0 ? string.Empty : " " + symbol);
            }
            return GameUiText.T("FUTURE", "ПОТОМ");
        }

        static string CompactText(SenseVisualPolicy policy, Color category, Color stock, bool isContainer)
        {
            if (isContainer && policy.Stock == SenseStockState.Complete) return string.Empty;
            if (!policy.HasItemIntelligence) return string.Empty;
            if (policy.Stock == SenseStockState.None)
                return "<color=#" + ColorUtility.ToHtmlStringRGB(category) + ">" + Label(policy.Category, policy.CurrencyCode) + "</color>";
            if (isContainer)
                return "<color=#" + ColorUtility.ToHtmlStringRGB(category) + ">" + Label(policy.Category, policy.CurrencyCode) + "</color>";
            if (policy.Stock == SenseStockState.Complete)
                return "<color=#" + ColorUtility.ToHtmlStringRGB(stock) + ">" + Label(policy.Category, policy.CurrencyCode) + "</color>";
            return "<color=#" + ColorUtility.ToHtmlStringRGB(category) + ">" + Label(policy.Category, policy.CurrencyCode) + "</color> " +
                   "<color=#" + ColorUtility.ToHtmlStringRGB(stock) + ">−" + policy.Remaining + "</color>";
        }

        static SenseVisualPolicy CategoryPolicy(Dictionary<ItemNeedReason, int> counts, string currencyCode)
        {
            if (counts == null || counts.Count == 0) return new SenseVisualPolicy(ItemNeedIcon.None, ItemNeedReason.None, ItemNeedReason.None, SenseStockState.None, 0);
            ItemNeedReason[] order = { ItemNeedReason.Food, ItemNeedReason.Water, ItemNeedReason.Grenade, ItemNeedReason.Key, ItemNeedReason.Currency };
            for (int i = 0; i < order.Length; i++)
            {
                int count;
                if (counts.TryGetValue(order[i], out count)) return SenseVisualPolicy.CategoryOnly(order[i], count, order[i] == ItemNeedReason.Currency ? currencyCode : string.Empty);
            }
            return new SenseVisualPolicy(ItemNeedIcon.None, ItemNeedReason.None, ItemNeedReason.None, SenseStockState.None, 0);
        }

        static ItemNeedReason SenseCategory(object item)
        {
            if (IsWater(item)) return ItemNeedReason.Water;
            ItemCategory category = ItemIntelligenceRegistry.Resolve(item).Category;
            if (category == ItemCategory.Key) return ItemNeedReason.Key;
            if (category == ItemCategory.Grenade) return ItemNeedReason.Grenade;
            if (category == ItemCategory.Currency) return ItemNeedReason.Currency;
            if (category == ItemCategory.Food) return ItemNeedReason.Food;
            return ItemNeedReason.None;
        }

        static bool IsWater(object item)
        {
            if (item == null) return false;
            ItemDescriptor descriptor = ItemDescriptor.FromObject(item);
            string type = descriptor.TypeName + " " + item.GetType().Name;
            object template = Member(item, "Template");
            if (template != null) type += " " + template.GetType().Name;
            object hydration;
            object energy;
            bool hydrates = descriptor.TryGet("Hydration", out hydration) && Number(hydration, 0) != 0;
            bool energizes = descriptor.TryGet("Energy", out energy) && Number(energy, 0) != 0;
            return SenseWaterClassifier.IsWater(type, hydrates ? 1 : 0, energizes ? 1 : 0);
        }

        static string CurrencyCode(object item)
        {
            object type;
            ItemDescriptor descriptor = ItemDescriptor.FromObject(item);
            if (descriptor.TryGet("type", out type))
            {
                string code = Text(type).Trim().ToUpperInvariant();
                if (code == "USD" || code == "EUR" || code == "RUB") return code;
            }
            string name = (descriptor.Name + " " + descriptor.ShortName + " " + descriptor.TypeName).ToLowerInvariant();
            if (name.Contains("usd") || name.Contains("dollar") || name.Contains("$")) return "USD";
            if (name.Contains("eur") || name.Contains("euro") || name.Contains("€")) return "EUR";
            if (name.Contains("rub") || name.Contains("rouble") || name.Contains("ruble") || name.Contains("₽")) return "RUB";
            return string.Empty;
        }

        static long SaturatingMultiply(long value, int count)
        {
            if (value <= 0 || count <= 0) return 0;
            return value > long.MaxValue / count ? long.MaxValue : value * count;
        }

        static long SaturatingAdd(long left, long right)
        {
            return right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        static Color ApplyContainerValueBrightness(Color categoryColor, float brightness)
        {
            float value = Mathf.Clamp(brightness, 0.10f, 1.00f);
            return new Color(categoryColor.r * value, categoryColor.g * value, categoryColor.b * value, categoryColor.a);
        }

        static bool HasProtectedSenseVisual(object senseItem)
        {
            if (IsContainer(senseItem)) return false;
            string type = Text(Member(senseItem, "senseItemType"));
            if (type == "Valuables" || type == "KappaItems" || type == "RareItems" || type == "WishList") return true;
            if (type == "QuestItems") return !IsContainer(senseItem);
            if (type == "QuestItems") return !IsContainer(senseItem);
            if (type == "ElectronicKeys" || type == "MechanicalKeys") return false;
            object raw = Member(senseItem, "color");
            if (!(raw is Color)) return false;
            Color color = (Color)raw;
            return (color.r > .85f && color.g < .25f) ||
                   (color.r > .85f && color.g > .70f && color.b < .30f) ||
                   (color.b > .45f && color.r > .25f && color.g < .45f);
        }

        static string IconFile(ItemNeedIcon icon, object senseItem)
        {
            if (icon == ItemNeedIcon.Quest) return "icon_quest.png";
            if (icon == ItemNeedIcon.Hideout) return "icon_barter_building.png";
            if (icon == ItemNeedIcon.Food) return "icon_provisions_food.png";
            if (icon == ItemNeedIcon.Water) return "icon_provisions_drinks.png";
            if (icon == ItemNeedIcon.Key)
                return Text(Member(senseItem, "senseItemType")) == "ElectronicKeys" ? "icon_keys_electronic.png" : "icon_keys_mechanic.png";
            if (icon == ItemNeedIcon.Grenade) return "icon_weapons_throw.png";
            if (icon == ItemNeedIcon.Currency) return "icon_money.png";
            if (icon == ItemNeedIcon.Complete) return "icon_fav_checked.png";
            return "icon_info.png";
        }

        static bool IsContainer(object senseItem)
        {
            return senseItem != null && (Member(senseItem, "lootableContainer") != null ||
                senseItem.GetType().Name.IndexOf("Container", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static bool IsContainerRoot(object senseItem, object looseItem, object candidate)
        {
            if (ReferenceEquals(candidate, looseItem)) return true;
            object lootableContainer = Member(senseItem, "lootableContainer");
            object owner = Member(lootableContainer, "ItemOwner", "Owner");
            return ReferenceEquals(candidate, Member(owner, "RootItem"));
        }

        static bool IsFood(object item)
        {
            if (item == null) return false;
            string itemType = item.GetType().Name;
            object template = Member(item, "Template");
            string templateType = template == null ? string.Empty : template.GetType().Name;
            return itemType.IndexOf("Food", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   itemType.IndexOf("Drink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   templateType.IndexOf("Food", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   templateType.IndexOf("Drink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   Member(item, "FoodDrinkComponent") != null || Member(template, "FoodDrinkComponent") != null;
        }

        static object FindSenseSprite(Assembly assembly, string key)
        {
            Type type = assembly.GetType("AmandsSense.Components.AmandsSenseClass", false);
            object value = Member(type, "LoadedSprites");
            IDictionary dictionary = value as IDictionary;
            return dictionary != null && dictionary.Contains(key) ? dictionary[key] : null;
        }

        static void ApplyRenderer(object renderer, object sprite, Color color)
        {
            if (renderer == null) return;
            if (sprite != null) SetMember(renderer, "sprite", sprite);
            object existing = Member(renderer, "color");
            if (existing is Color) color.a = ((Color)existing).a;
            SetMember(renderer, "color", color);
        }

        static void ApplyLight(object light, Color color)
        {
            if (light == null) return;
            color.a = 1f;
            SetMember(light, "color", color);
        }

        static void ApplyText(object text, string value, Color color, Color outline)
        {
            if (text == null) return;
            object existing = Member(text, "color");
            if (existing is Color) color.a = ((Color)existing).a;
            SetMember(text, "text", value);
            SetMember(text, "color", color);
            SetMember(text, "outlineColor", outline);
        }

        static bool IsSuccess(object args)
        {
            if (args == null) return true;
            object success = Member(args, "Succeed", "Succeeded", "Success", "IsSuccess");
            if (success != null) return Flag(success);
            object failed = Member(args, "Failed", "Failure", "IsFailed");
            if (failed != null && Flag(failed)) return false;
            object status = Member(args, "Status");
            if (status == null) return true;
            string text = status.ToString();
            return string.Equals(text, "Succeed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase) ||
                   Number(status, -1) == 1;
        }

        void ApplySenseTextScale(object senseItem, bool isContainer)
        {
            if (!settings.SenseIntegration) return;
            float scale = settings.SenseTextScale;
            ScaleSenseText(Member(senseItem, "nameText"), scale * (isContainer ? settings.SenseContainerNameScale : 1f));
            ScaleSenseText(Member(senseItem, "typeText"), scale);
            ScaleSenseText(Member(senseItem, "descriptionText"), scale);
        }

        void ScaleSenseText(object text, float scale)
        {
            if (text == null) return;
            SenseTextBaseline baseline = textBaselines.GetValue(text, key =>
            {
                float size;
                try { size = Convert.ToSingle(Member(key, "fontSize")); }
                catch { size = 0f; }
                return new SenseTextBaseline(size);
            });
            if (baseline.Size > 0f) SetMember(text, "fontSize", baseline.Size * scale);
        }

        static void HideNativeLootLabel(object senseItem)
        {
            object typeText = Member(senseItem, "typeText");
            string text = Text(Member(typeText, "text")).TrimEnd(':', '：');
            if (string.Equals(text, "LOOT", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("LOOT ", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "ДОБЫЧА", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("ДОБЫЧА ", StringComparison.OrdinalIgnoreCase))
                SetMember(typeText, "text", string.Empty);
        }

        sealed class SenseTextBaseline
        {
            internal SenseTextBaseline(float size) { Size = size; }
            internal float Size { get; }
        }

        static object Member(object source, params string[] names)
        {
            if (source == null) return null;
            Type originalType = source as Type ?? source.GetType();
            object target = source is Type ? null : source;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance);
            for (int i = 0; i < names.Length; i++)
            {
                for (Type type = originalType; type != null; type = type.BaseType)
                {
                    PropertyInfo property = type.GetProperty(names[i], flags | BindingFlags.DeclaredOnly);
                    if (property != null) { try { return property.GetValue(target, null); } catch { } }
                    FieldInfo field = type.GetField(names[i], flags | BindingFlags.DeclaredOnly);
                    if (field != null) { try { return field.GetValue(target); } catch { } }
                }
            }
            return null;
        }

        static void SetField(object target, string name, object value)
        {
            if (target == null) return;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) { field.SetValue(target, value); return; }
            }
        }

        static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite) { try { property.SetValue(target, value, null); } catch { } }
        }

        static string Text(object value) { return value == null ? string.Empty : value.ToString().Trim(); }
        static bool Flag(object value) { try { return value != null && Convert.ToBoolean(value); } catch { return false; } }
        static int Number(object value, int fallback) { try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; } }

        sealed class SenseEvaluationCache
        {
            internal SenseEvaluationCache(ItemPresentationIndex index, int ledgerRevision, int settingsRevision,
                SenseVisualPolicy policy, bool isContainer, long containerTotalValue)
            { Index = index; LedgerRevision = ledgerRevision; SettingsRevision = settingsRevision; Policy = policy; IsContainer = isContainer; ContainerTotalValue = containerTotalValue; }
            internal ItemPresentationIndex Index { get; }
            internal int LedgerRevision { get; }
            internal int SettingsRevision { get; }
            internal SenseVisualPolicy Policy { get; }
            internal bool IsContainer { get; }
            internal long ContainerTotalValue { get; }
        }
        static Assembly FindAssembly(string name) { foreach (Assembly value in AppDomain.CurrentDomain.GetAssemblies()) if (string.Equals(value.GetName().Name, name, StringComparison.OrdinalIgnoreCase)) return value; return null; }
        static MethodInfo FindMethod(Type type, string name, int parameters) { if (type == null) return null; foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) if (method.Name == name && method.GetParameters().Length == parameters) return method; return null; }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            if (harmonyType == null || harmonyMethodType == null) return null;
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                ParameterInfo[] p = method.GetParameters();
                if (method.Name == "Patch" && p.Length >= 3 && p[0].ParameterType == typeof(MethodBase) && p[1].ParameterType == harmonyMethodType) return method;
            }
            return null;
        }

        void Patch(MethodInfo patch, ConstructorInfo ctor, MethodInfo original, MethodInfo prefix, MethodInfo postfix)
        {
            ParameterInfo[] parameters = patch.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            if (parameters.Length > 1 && prefix != null) args[1] = ctor.Invoke(new object[] { prefix });
            if (parameters.Length > 2 && postfix != null) args[2] = ctor.Invoke(new object[] { postfix });
            patch.Invoke(harmony, args);
        }

        public void Dispose()
        {
            if (ReferenceEquals(active, this)) active = null;
            if (harmony != null)
            {
                try { MethodInfo unpatch = harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) }); if (unpatch != null) unpatch.Invoke(harmony, new object[] { HarmonyId }); } catch { }
            }
            harmony = null;
            IsInstalled = false;
            ResetRaid();
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object left, object right) { return ReferenceEquals(left, right); }
            public int GetHashCode(object value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
        }
    }
}

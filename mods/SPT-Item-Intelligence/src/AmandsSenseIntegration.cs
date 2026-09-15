using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        readonly HashSet<string> pickedItemIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;

        public AmandsSenseIntegration(ItemIntelligenceUiSettings settings, ItemPresentationStore store,
            Action<string> logInfo, Action<string> logWarning,
            RaidRequirementLedger ledger = null, Action raidChanged = null)
        {
            this.settings = settings;
            this.store = store;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.ledger = ledger ?? new RaidRequirementLedger();
            this.raidChanged = raidChanged;
        }

        public bool IsInstalled { get; private set; }

        public bool TryInstall()
        {
            if (IsInstalled || !settings.SenseIntegration) return IsInstalled;
            try
            {
                Assembly sense = FindAssembly("AmandsSense");
                Type itemType = sense == null ? null : sense.GetType("AmandsSense.Components.AmandsSenseItem", false);
                Type senseClass = sense == null ? null : sense.GetType("AmandsSense.Components.AmandsSenseClass", false);
                MethodInfo setSense = FindMethod(itemType, "SetSense", 1);
                MethodInfo remove = FindMethod(itemType, "RemoveLootItem", 1);
                MethodInfo clear = FindMethod(senseClass, "Clear", 0);
                if (setSense == null || remove == null || clear == null)
                {
                    if (logWarning != null) logWarning("Item Intelligence Sense bridge unavailable: SetSense=" + (setSense != null) + ", RemoveLootItem=" + (remove != null) + ", Clear=" + (clear != null));
                    return false;
                }

                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                ConstructorInfo hmCtor = harmonyMethodType == null ? null : harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
                if (harmonyType == null || hmCtor == null || patch == null) return false;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(patch, hmCtor, setSense, null, typeof(AmandsSenseIntegration).GetMethod(nameof(SetSensePostfix), BindingFlags.Static | BindingFlags.NonPublic));
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
        static void RemovePrefix(object __instance, object __0) { AmandsSenseIntegration value = active; if (value != null) value.RecordPickup(__instance, __0); }
        static void ClearPostfix() { AmandsSenseIntegration value = active; if (value != null) value.ResetRaid(); }

        void Apply(object senseItem)
        {
            if (!settings.SenseIntegration || !settings.SenseRequiredItems || senseItem == null) return;
            ItemPresentationIndex index = store.Current;
            object observed = Member(senseItem, "observedLootItem");
            object item = Member(observed, "Item");
            string itemId = Text(Member(item, "Id", "ID"));
            if (itemId.Length > 0 && pickedItemIds.Remove(itemId) && ledger.Remove(itemId) && raidChanged != null) raidChanged();

            List<SenseVisualPolicy> candidates = new List<SenseVisualPolicy>();
            foreach (object contained in EnumerateItemTree(item))
            {
                string templateId = Text(Member(contained, "TemplateId", "Tpl"));
                if (templateId.Length == 0) continue;
                bool fir = Flag(Member(contained, "SpawnedInSession"));
                ItemPresentationState state = index.Get(templateId);
                ItemRequirementAllocation allocation = state.Requirement == null ? null : state.Requirement.Allocation;
                ItemIntelligenceDecision decision = ledger.Evaluate(templateId, allocation, fir);
                candidates.Add(SenseVisualPolicyEngine.Evaluate(decision.Allocation));
            }
            SenseVisualPolicy policy = SenseContainerPolicyEngine.Combine(candidates);
            if (!policy.HasItemIntelligence) return;

            Color primary = settings.GetSenseColor(policy.Category);
            Color stock = settings.GetSenseStockColor(policy.Stock);
            bool preserveIcon = HasProtectedSenseVisual(senseItem) && policy.Stock != SenseStockState.Complete;
            if (!preserveIcon) SetField(senseItem, "color", primary);
            Color secondary = policy.SecondaryCategory == ItemNeedReason.None || !settings.SenseSecondaryOutline
                ? primary : settings.GetSenseColor(policy.SecondaryCategory);
            SetField(senseItem, "outlineColor", secondary);

            object sprite = preserveIcon ? Member(senseItem, "sprite") : FindSenseSprite(senseItem.GetType().Assembly, IconFile(policy.Icon));
            if (!preserveIcon && sprite != null) SetField(senseItem, "sprite", sprite);
            object nativeColor = Member(senseItem, "color");
            Color renderColor = preserveIcon && nativeColor is Color ? (Color)nativeColor : primary;
            ApplyRenderer(Member(senseItem, "spriteRenderer"), sprite, renderColor);
            ApplyLight(Member(senseItem, "light"), preserveIcon ? stock : primary);
            if (settings.SenseRemainingText)
                ApplyText(Member(senseItem, "typeText"), TwoLineText(policy, primary, stock), Color.white, secondary);
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
                EnqueueItems(Member(container, "Items", "Children"), pending);
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
            object item = Member(Member(senseItem, "observedLootItem"), "Item");
            string id = Text(Member(item, "Id", "ID"));
            string template = Text(Member(item, "TemplateId", "Tpl"));
            if (id.Length == 0 || template.Length == 0) return;
            int stack = Math.Max(1, Number(Member(item, "StackObjectsCount"), 1));
            bool fir = Flag(Member(item, "SpawnedInSession"));
            if (ledger.Observe(id, template, stack, fir) && raidChanged != null) raidChanged();
            pickedItemIds.Add(id);
        }

        void ResetRaid()
        {
            int revision = ledger.Revision;
            ledger.Reset();
            pickedItemIds.Clear();
            if (ledger.Revision != revision && raidChanged != null) raidChanged();
        }

        static string Label(ItemNeedReason reason)
        {
            if (reason == ItemNeedReason.ActiveQuest) return GameUiText.T("QUEST", "КВЕСТ");
            if (reason == ItemNeedReason.Hideout) return GameUiText.T("HIDEOUT", "УБЕЖИЩЕ");
            return GameUiText.T("FUTURE", "ПОТОМ");
        }

        static string StockText(SenseVisualPolicy policy)
        {
            if (policy.Stock == SenseStockState.Complete) return GameUiText.T(" · ALL ✓", " · ВСЁ ✓");
            if (policy.Stock == SenseStockState.NextCovered) return GameUiText.T(" · NEXT ✓", " · ЭТАП ✓");
            return " · −" + policy.Remaining;
        }

        static string TwoLineText(SenseVisualPolicy policy, Color category, Color stock)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(category) + ">" + Label(policy.Category) + "</color>\n" +
                   "<color=#" + ColorUtility.ToHtmlStringRGB(stock) + ">" + StockText(policy).TrimStart(' ', '·') + "</color>";
        }

        static bool HasProtectedSenseVisual(object senseItem)
        {
            string type = Text(Member(senseItem, "senseItemType"));
            if (type == "Valuables" || type == "QuestItems") return true;
            object raw = Member(senseItem, "color");
            if (!(raw is Color)) return false;
            Color color = (Color)raw;
            return (color.r > .85f && color.g < .25f) ||
                   (color.r > .85f && color.g > .70f && color.b < .30f) ||
                   (color.b > .45f && color.r > .25f && color.g < .45f);
        }

        static string IconFile(ItemNeedIcon icon)
        {
            if (icon == ItemNeedIcon.Quest) return "icon_quest.png";
            if (icon == ItemNeedIcon.Hideout) return "icon_barter_building.png";
            return "icon_info.png";
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
            object status = Member(args, "Status");
            if (status == null) return false;
            string text = status.ToString();
            return string.Equals(text, "Succeed", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase) || Number(status, -1) == 1;
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

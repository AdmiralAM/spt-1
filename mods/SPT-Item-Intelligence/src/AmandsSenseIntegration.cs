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
        readonly RaidRequirementLedger ledger = new RaidRequirementLedger();
        readonly HashSet<string> pickedItemIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        ItemPresentationIndex observedIndex;

        public AmandsSenseIntegration(ItemIntelligenceUiSettings settings, ItemPresentationStore store,
            Action<string> logInfo, Action<string> logWarning)
        {
            this.settings = settings;
            this.store = store;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
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
                if (setSense == null || remove == null || clear == null) return false;

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
            if (!ReferenceEquals(index, observedIndex)) { observedIndex = index; ResetRaid(); }
            object observed = Member(senseItem, "observedLootItem");
            object item = Member(observed, "Item");
            string templateId = Text(Member(item, "TemplateId", "Tpl"));
            string itemId = Text(Member(item, "Id", "ID"));
            int stack = Math.Max(1, Number(Member(item, "StackObjectsCount"), 1));
            bool fir = Flag(Member(item, "SpawnedInSession"));
            if (itemId.Length > 0 && pickedItemIds.Remove(itemId)) ledger.Remove(itemId);

            ItemPresentationState state = index.Get(templateId);
            ItemRequirementAllocation allocation = state.Requirement == null ? null : state.Requirement.Allocation;
            SenseRequirementPresentation presentation = SenseRequirementMapper.Map(ledger.Evaluate(templateId, allocation, fir));
            if (!presentation.OverridesSense) return;

            Color primary = settings.GetSenseColor(presentation.PrimaryReason);
            SetField(senseItem, "color", primary);
            Color secondary = presentation.OutlineReason == ItemNeedReason.None || !settings.SenseSecondaryOutline
                ? primary : settings.GetSenseColor(presentation.OutlineReason);
            SetField(senseItem, "outlineColor", secondary);

            object sprite = FindSenseSprite(senseItem.GetType().Assembly, IconFile(presentation.Icon));
            if (sprite != null) SetField(senseItem, "sprite", sprite);
            ApplyRenderer(Member(senseItem, "spriteRenderer"), sprite, primary);
            ApplyLight(Member(senseItem, "light"), primary);
            if (settings.SenseRemainingText)
                ApplyText(Member(senseItem, "typeText"), Label(presentation) + " ×" + presentation.Remaining, primary, secondary);
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
            ledger.Observe(id, template, stack, fir);
            pickedItemIds.Add(id);
        }

        void ResetRaid() { ledger.Reset(); pickedItemIds.Clear(); }

        static string Label(SenseRequirementPresentation value)
        {
            if (value.PrimaryReason == ItemNeedReason.ActiveQuest) return GameUiText.T("QUEST", "КВЕСТ");
            if (value.PrimaryReason == ItemNeedReason.Hideout) return GameUiText.T("HIDEOUT", "УБЕЖИЩЕ");
            return GameUiText.T("FUTURE", "ПОТОМ");
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
        static MethodInfo FindMethod(Type type, string name, int parameters) { if (type == null) return null; foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) if (method.Name == name && method.GetParameters().Length == parameters) return method; return null; }

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
    }
}

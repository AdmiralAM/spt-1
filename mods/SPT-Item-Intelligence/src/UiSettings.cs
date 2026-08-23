using System;
using System.Threading;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemIntelligenceUiSettings
    {
        readonly ConfigEntry<ItemTooltipMode> tooltipMode;
        readonly ConfigEntry<ItemValueMode> valueMode;
        readonly ConfigEntry<float> tooltipScale;
        readonly ConfigEntry<float> tooltipOpacity;
        readonly ConfigEntry<int> tooltipFontSize;
        readonly ConfigEntry<float> markerSize;
        readonly ConfigEntry<float> markerOpacity;
        readonly ConfigEntry<float> markerOffsetX;
        readonly ConfigEntry<float> markerOffsetY;
        readonly ConfigEntry<Color> defaultColor;
        readonly ConfigEntry<Color> questNowColor;
        readonly ConfigEntry<Color> hideoutColor;
        readonly ConfigEntry<Color> questLaterColor;
        readonly ConfigEntry<Color> enoughColor;
        readonly ConfigEntry<Color> partialColor;
        readonly ConfigEntry<Color> missingColor;
        int revision;

        public event Action Changed;

        public ItemIntelligenceUiSettings(ConfigFile config)
        {
            tooltipMode = config.Bind("Tooltip", "Mode", ItemTooltipMode.Normal,
                "Minimal: Value + Keep. Normal: adds Quest Now, Hideout and Quest Later with owned/required progress. Detailed: adds owned and up to three concrete targets. Full: shows every concrete target and FIR detail. Internal ids and sell/surplus decisions are never shown.");
            valueMode = config.Bind("Tooltip", "Value Source", ItemValueMode.Vendor,
                "Vendor: show the highest NPC trader sell value. Flea: show the flea-market value.");
            tooltipScale = config.Bind("Tooltip", "Scale", 1.00f,
                new ConfigDescription("Overall tooltip scale.", new AcceptableValueRange<float>(0.75f, 1.50f)));
            tooltipOpacity = config.Bind("Tooltip", "Opacity", 0.96f,
                new ConfigDescription("Tooltip background opacity.", new AcceptableValueRange<float>(0.35f, 1.00f)));
            tooltipFontSize = config.Bind("Tooltip", "Font Size", 13,
                new ConfigDescription("Tooltip text size before Scale is applied.", new AcceptableValueRange<int>(11, 18)));

            markerSize = config.Bind("Marker", "Size", 14f,
                new ConfigDescription("Information marker size in pixels.", new AcceptableValueRange<float>(10f, 28f)));
            markerOpacity = config.Bind("Marker", "Opacity", 0.96f,
                new ConfigDescription("Information marker opacity.", new AcceptableValueRange<float>(0.20f, 1f)));
            markerOffsetX = config.Bind("Marker", "Offset X", 3f,
                new ConfigDescription("Horizontal inset from the item cell upper-left corner; negative values move outward.", new AcceptableValueRange<float>(-40f, 40f)));
            markerOffsetY = config.Bind("Marker", "Offset Y", 3f,
                new ConfigDescription("Vertical inset from the item cell upper-left corner; negative values move outward.", new AcceptableValueRange<float>(-40f, 40f)));

            defaultColor = ColorEntry(config, "Marker Colors", "Default Color", new Color(0.90f, 0.90f, 0.90f), "No unmet requirement.");
            questNowColor = ColorEntry(config, "Marker Colors", "Quest Now Color", new Color(1.00f, 0.35f, 0.21f), "Unmet active quest requirement.");
            hideoutColor = ColorEntry(config, "Marker Colors", "Hideout Color", new Color(0.20f, 0.78f, 1.00f), "Unmet hideout requirement.");
            questLaterColor = ColorEntry(config, "Marker Colors", "Quest Later Color", new Color(0.75f, 0.55f, 1.00f), "Unmet future quest requirement.");

            enoughColor = ColorEntry(config, "Tooltip Colors", "Complete Color", new Color(0.45f, 0.90f, 0.48f), "Requirement is fully satisfied.");
            partialColor = ColorEntry(config, "Tooltip Colors", "Partial Color", new Color(1.00f, 0.72f, 0.20f), "Requirement is partially satisfied.");
            missingColor = ColorEntry(config, "Tooltip Colors", "Missing Color", new Color(1.00f, 0.34f, 0.28f), "Requirement has no usable stock.");

            tooltipMode.SettingChanged += delegate { Touch(); };
            valueMode.SettingChanged += delegate { Touch(); };
            tooltipScale.SettingChanged += delegate { Touch(); };
            tooltipOpacity.SettingChanged += delegate { Touch(); };
            tooltipFontSize.SettingChanged += delegate { Touch(); };
            markerSize.SettingChanged += delegate { Touch(); };
            markerOpacity.SettingChanged += delegate { Touch(); };
            markerOffsetX.SettingChanged += delegate { Touch(); };
            markerOffsetY.SettingChanged += delegate { Touch(); };
            defaultColor.SettingChanged += delegate { Touch(); };
            questNowColor.SettingChanged += delegate { Touch(); };
            hideoutColor.SettingChanged += delegate { Touch(); };
            questLaterColor.SettingChanged += delegate { Touch(); };
            enoughColor.SettingChanged += delegate { Touch(); };
            partialColor.SettingChanged += delegate { Touch(); };
            missingColor.SettingChanged += delegate { Touch(); };
        }

        public ItemTooltipMode TooltipMode => tooltipMode.Value;
        public ItemValueMode ValueMode => valueMode.Value;
        public float TooltipScale => Mathf.Clamp(tooltipScale.Value, 0.75f, 1.50f);
        public float TooltipOpacity => Mathf.Clamp01(tooltipOpacity.Value);
        public int TooltipFontSize => Mathf.Clamp(tooltipFontSize.Value, 11, 18);
        public float MarkerSize => Mathf.Clamp(markerSize.Value, 10f, 28f);
        public float MarkerOpacity => Mathf.Clamp01(markerOpacity.Value);
        public float MarkerOffsetX => Mathf.Clamp(markerOffsetX.Value, -40f, 40f);
        public float MarkerOffsetY => Mathf.Clamp(markerOffsetY.Value, -40f, 40f);
        public Color CompleteColor => enoughColor.Value;
        public Color PartialColor => partialColor.Value;
        public Color MissingColor => missingColor.Value;
        public int Revision => Volatile.Read(ref revision);

        public Color GetColor(ItemMarkerKind kind)
        {
            switch (kind)
            {
                case ItemMarkerKind.QuestNow: return questNowColor.Value;
                case ItemMarkerKind.Hideout: return hideoutColor.Value;
                case ItemMarkerKind.QuestLater: return questLaterColor.Value;
                default: return defaultColor.Value;
            }
        }

        void Touch()
        {
            Interlocked.Increment(ref revision);
            Action changed = Changed;
            if (changed != null) changed();
        }

        static ConfigEntry<Color> ColorEntry(ConfigFile config, string section, string name, Color value, string description)
        {
            return config.Bind(section, name, value, description + " Uses the native color selector.");
        }
    }
}

using System;
using System.Threading;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTItemIntelligence
{
    public enum ItemValueMode
    {
        Vendor,
        Flea
    }

    public sealed class ItemIntelligenceUiSettings
    {
        readonly ConfigEntry<ItemTooltipMode> tooltipMode;
        readonly ConfigEntry<ItemValueMode> valueMode;
        readonly ConfigEntry<float> markerSize;
        readonly ConfigEntry<float> markerOpacity;
        readonly ConfigEntry<float> markerOffsetX;
        readonly ConfigEntry<float> markerOffsetY;
        readonly ConfigEntry<Color> defaultColor;
        readonly ConfigEntry<Color> questNowColor;
        readonly ConfigEntry<Color> hideoutColor;
        readonly ConfigEntry<Color> questLaterColor;
        int revision;

        public event Action Changed;

        public ItemIntelligenceUiSettings(ConfigFile config)
        {
            tooltipMode = config.Bind("Tooltip", "Mode", ItemTooltipMode.Normal,
                "Minimal: Value + Keep. Normal: adds Quest Now, Hideout and Quest Later with owned/required progress. Detailed: adds owned and up to three concrete targets. Full: shows every concrete target. Internal ids and sell/surplus decisions are never shown.");
            valueMode = config.Bind("Tooltip", "Value Source", ItemValueMode.Vendor,
                "Vendor: show the highest NPC trader sell value. Flea: show the flea-market value.");
            markerSize = config.Bind("Marker", "Size", 14f,
                new ConfigDescription("Information marker size in pixels.", new AcceptableValueRange<float>(10f, 28f)));
            markerOpacity = config.Bind("Marker", "Opacity", 0.96f,
                new ConfigDescription("Information marker opacity.", new AcceptableValueRange<float>(0.20f, 1f)));
            markerOffsetX = config.Bind("Marker", "Offset X", 3f,
                new ConfigDescription("Horizontal inset from the item cell upper-left corner; negative values move outward.", new AcceptableValueRange<float>(-40f, 40f)));
            markerOffsetY = config.Bind("Marker", "Offset Y", 3f,
                new ConfigDescription("Vertical inset from the item cell upper-left corner; negative values move outward.", new AcceptableValueRange<float>(-40f, 40f)));

            defaultColor = ColorEntry(config, "Default Color", new Color(0.90f, 0.90f, 0.90f), "No unmet requirement.");
            questNowColor = ColorEntry(config, "Quest Now Color", new Color(1.00f, 0.35f, 0.21f), "Unmet active quest requirement.");
            hideoutColor = ColorEntry(config, "Hideout Color", new Color(0.20f, 0.78f, 1.00f), "Unmet hideout requirement.");
            questLaterColor = ColorEntry(config, "Quest Later Color", new Color(0.75f, 0.55f, 1.00f), "Unmet future quest requirement.");

            tooltipMode.SettingChanged += delegate { Touch(); };
            valueMode.SettingChanged += delegate { Touch(); };
            markerSize.SettingChanged += delegate { Touch(); };
            markerOpacity.SettingChanged += delegate { Touch(); };
            markerOffsetX.SettingChanged += delegate { Touch(); };
            markerOffsetY.SettingChanged += delegate { Touch(); };
            defaultColor.SettingChanged += delegate { Touch(); };
            questNowColor.SettingChanged += delegate { Touch(); };
            hideoutColor.SettingChanged += delegate { Touch(); };
            questLaterColor.SettingChanged += delegate { Touch(); };
        }

        public ItemTooltipMode TooltipMode => tooltipMode.Value;
        public ItemValueMode ValueMode => valueMode.Value;
        public float MarkerSize => Mathf.Clamp(markerSize.Value, 10f, 28f);
        public float MarkerOpacity => Mathf.Clamp01(markerOpacity.Value);
        public float MarkerOffsetX => Mathf.Clamp(markerOffsetX.Value, -40f, 40f);
        public float MarkerOffsetY => Mathf.Clamp(markerOffsetY.Value, -40f, 40f);
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

        static ConfigEntry<Color> ColorEntry(ConfigFile config, string name, Color value, string description)
        {
            return config.Bind("Marker Colors", name, value, description + " Uses the native color selector.");
        }
    }
}

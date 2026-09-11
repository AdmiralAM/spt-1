using System;
using System.Threading;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTItemIntelligence
{
    public enum ItemMarkerSide
    {
        Left,
        Right
    }

    public sealed class ItemIntelligenceUiSettings
    {
        readonly ConfigEntry<bool> markers, tooltips, quests, futureQuests, hideout, value, relevance, backgrounds;
        readonly ConfigEntry<ItemTooltipMode> tooltipMode;
        readonly ConfigEntry<ItemValueMode> valueMode;
        readonly ConfigEntry<float> tooltipScale;
        readonly ConfigEntry<float> tooltipOpacity;
        readonly ConfigEntry<int> tooltipFontSize;
        readonly ConfigEntry<int> tooltipMaximumWidth;
        readonly ConfigEntry<float> tooltipPadding;
        readonly ConfigEntry<ItemMarkerSide> markerSide;
        readonly ConfigEntry<float> markerSize;
        readonly ConfigEntry<float> markerOpacity;
        readonly ConfigEntry<float> markerOffsetX;
        readonly ConfigEntry<float> markerOffsetY;
        readonly ConfigEntry<bool> markerHalo;
        readonly ConfigEntry<float> markerHaloStrength;
        readonly ConfigEntry<Color> markerBackgroundColor;
        readonly ConfigEntry<int> markerBackgroundOpacity;
        readonly ConfigEntry<Color> defaultColor;
        readonly ConfigEntry<Color> questNowColor;
        readonly ConfigEntry<Color> hideoutColor;
        readonly ConfigEntry<Color> questLaterColor;
        readonly ConfigEntry<Color> enoughColor;
        readonly ConfigEntry<Color> partialColor;
        readonly ConfigEntry<Color> missingColor;
        int revision;
        ModuleSelection modules;

        public event Action Changed;

        public ItemIntelligenceUiSettings(ConfigFile config)
        {
            markers = Module(config, "Markers", true, "Show one contextual item-cell intelligence badge.");
            tooltips = Module(config, "Tooltips", true, "Show the compact information card when hovering the badge, or the item caption when no badge is present.");
            quests = Module(config, "Quests", true, "Include active quest requirements.");
            futureQuests = Module(config, "Future Quests", true, "Include independent future quest requirements.");
            hideout = Module(config, "Hideout", true, "Include incomplete current and future hideout upgrades.");
            value = Module(config, "Value", true, "Show value, buyer, flea and per-slot information.");
            relevance = Module(config, "Craft and Barter", true, "Show craft and barter relevance.");
            backgrounds = Module(config, "Background Coloring (Valuation)", true, "Color neutral ordinary-item backgrounds only. Ammunition, keys, authored colors and compatibility highlights keep their dedicated owner.");
            tooltipMode = config.Bind("Tooltip", "Mode", ItemTooltipMode.Normal,
                "Minimal: summary and selected value. Normal: regular-play card with owned/FIR, the F12-selected value source, craft/barter relevance and active quest, hideout and future quest progress. Detailed: Normal plus one nearest concrete target. Full: both price sources, per-slot value, all concrete targets and craft/barter relevance. Internal ids and sell recommendations are never shown.");
            valueMode = config.Bind("Tooltip", "Value Source", ItemValueMode.Vendor,
                "Vendor: show the highest NPC trader sell value. Flea: show the flea-market value.");
            tooltipScale = config.Bind("Tooltip", "Scale", 1.00f,
                new ConfigDescription("Scales the complete card after its content is measured. 1.00 is native size.", new AcceptableValueRange<float>(0.10f, 5.00f)));
            tooltipOpacity = config.Bind("Tooltip", "Opacity", 0.96f,
                new ConfigDescription("Tooltip background opacity; 0 disables the background completely.", new AcceptableValueRange<float>(0f, 1.00f)));
            tooltipFontSize = config.Bind("Tooltip", "Font Size", 13,
                new ConfigDescription("Tooltip text size before Scale is applied.", new AcceptableValueRange<int>(1, 64)));
            tooltipMaximumWidth = config.Bind("Tooltip", "Maximum Width", 360,
                new ConfigDescription("Maximum card width in pixels. The card remains fitted to shorter text.", new AcceptableValueRange<int>(80, 1000)));
            tooltipPadding = config.Bind("Tooltip", "Inner Padding", 8f,
                new ConfigDescription("Space between text and the card edge in pixels.", new AcceptableValueRange<float>(0f, 40f)));

            markerSide = config.Bind("Marker", "Side", ItemMarkerSide.Left,
                "Select the upper-left or upper-right item-cell corner. This stays attached to the selected edge on multi-cell items.");
            markerSize = config.Bind("Marker", "Size", 14f,
                new ConfigDescription("Information marker size in pixels.", new AcceptableValueRange<float>(1f, 100f)));
            markerOpacity = config.Bind("Marker", "Opacity", 0.96f,
                new ConfigDescription("Check and ring opacity.", new AcceptableValueRange<float>(0f, 1f)));
            markerBackgroundColor = ColorEntry(config, "Marker", "Circle Background Color", new Color(0.067f, 0.082f, 0.094f), "Color inside the marker circle.");
            markerBackgroundOpacity = config.Bind("Marker", "Circle Background Opacity (%)", 92,
                new ConfigDescription("Opacity inside the marker circle from 0 to 100 percent.", new AcceptableValueRange<int>(0, 100)));
            markerOffsetX = config.Bind("Marker", "Offset X", 3f,
                new ConfigDescription("Horizontal offset from the selected edge. Positive values move inward; negative values move outward.", new AcceptableValueRange<float>(-80f, 80f)));
            markerOffsetY = config.Bind("Marker", "Offset Y", 3f,
                new ConfigDescription("Vertical inset from the item cell upper edge; negative values move outward.", new AcceptableValueRange<float>(-40f, 40f)));
            markerHalo = config.Bind("Marker", "Halo", false,
                "Adds a soft diffuse same-color halo behind the marker. Disabled by default until visually accepted in runtime.");
            markerHaloStrength = config.Bind("Marker", "Halo Strength", 0.22f,
                new ConfigDescription("Opacity of the soft marker halo.", new AcceptableValueRange<float>(0f, 0.50f)));

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
            tooltipMaximumWidth.SettingChanged += delegate { Touch(); };
            tooltipPadding.SettingChanged += delegate { Touch(); };
            markerSide.SettingChanged += delegate { Touch(); };
            markerSize.SettingChanged += delegate { Touch(); };
            markerOpacity.SettingChanged += delegate { Touch(); };
            markerBackgroundColor.SettingChanged += delegate { Touch(); };
            markerBackgroundOpacity.SettingChanged += delegate { Touch(); };
            markerOffsetX.SettingChanged += delegate { Touch(); };
            markerOffsetY.SettingChanged += delegate { Touch(); };
            markerHalo.SettingChanged += delegate { Touch(); };
            markerHaloStrength.SettingChanged += delegate { Touch(); };
            defaultColor.SettingChanged += delegate { Touch(); };
            questNowColor.SettingChanged += delegate { Touch(); };
            hideoutColor.SettingChanged += delegate { Touch(); };
            questLaterColor.SettingChanged += delegate { Touch(); };
            enoughColor.SettingChanged += delegate { Touch(); };
            partialColor.SettingChanged += delegate { Touch(); };
            missingColor.SettingChanged += delegate { Touch(); };
            modules = ReadModules();
        }

        public ItemTooltipMode TooltipMode => tooltipMode.Value;
        public ModuleSelection Modules => modules;
        ModuleSelection ReadModules() => new ModuleSelection(markers.Value, tooltips.Value, quests.Value, futureQuests.Value, hideout.Value, value.Value, relevance.Value, backgrounds.Value);
        public ItemValueMode ValueMode => valueMode.Value;
        public float TooltipScale => Mathf.Clamp(tooltipScale.Value, 0.10f, 5.00f);
        public float TooltipOpacity => Mathf.Clamp01(tooltipOpacity.Value);
        public int TooltipFontSize => Mathf.Clamp(tooltipFontSize.Value, 1, 64);
        public int TooltipMaximumWidth => Mathf.Clamp(tooltipMaximumWidth.Value, 80, 1000);
        public float TooltipPadding => Mathf.Clamp(tooltipPadding.Value, 0f, 40f);
        public ItemMarkerSide MarkerSide => markerSide.Value;
        public float MarkerSize => Mathf.Clamp(markerSize.Value, 1f, 100f);
        public float MarkerOpacity => Mathf.Clamp01(markerOpacity.Value);
        public Color MarkerBackgroundColor => markerBackgroundColor.Value;
        public float MarkerBackgroundOpacity => Mathf.Clamp(markerBackgroundOpacity.Value, 0, 100) / 100f;
        public float MarkerOffsetX => Mathf.Clamp(markerOffsetX.Value, -80f, 80f);
        public float MarkerOffsetY => Mathf.Clamp(markerOffsetY.Value, -40f, 40f);
        public bool MarkerHalo => markerHalo.Value;
        public float MarkerHaloStrength => Mathf.Clamp(markerHaloStrength.Value, 0f, 0.50f);
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
            modules = ReadModules();
            Interlocked.Increment(ref revision);
            Action changed = Changed;
            if (changed != null) changed();
        }

        ConfigEntry<bool> Module(ConfigFile config, string name, bool enabled, string description)
        {
            ConfigEntry<bool> entry = config.Bind("Modules", name, enabled, description);
            entry.SettingChanged += delegate { Touch(); };
            return entry;
        }

        static ConfigEntry<Color> ColorEntry(ConfigFile config, string section, string name, Color value, string description)
        {
            return config.Bind(section, name, value, description + " Uses the native color selector.");
        }
    }
}

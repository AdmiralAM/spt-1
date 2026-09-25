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

    public enum ItemMarkerSymbol
    {
        Check,
        Cross,
        Dot,
        Alert
    }

    public enum ItemMarkerFrame
    {
        Circle,
        Hex,
        Diamond,
        Square
    }

    public sealed class ItemIntelligenceUiSettings
    {
        readonly ConfigEntry<bool> markers, tooltips, quests, futureQuests, hideout, value, relevance, backgrounds, ammoPenetrationMarker;
        readonly ConfigEntry<ItemTooltipMode> tooltipMode;
        readonly ConfigEntry<ItemValueMode> valueMode;
        readonly ConfigEntry<float> tooltipScale;
        readonly ConfigEntry<float> tooltipOpacity;
        readonly ConfigEntry<int> tooltipFontSize;
        readonly ConfigEntry<int> tooltipMaximumWidth;
        readonly ConfigEntry<float> tooltipPadding;
        readonly ConfigEntry<ItemMarkerSide> markerSide;
        readonly ConfigEntry<ItemMarkerSymbol> markerSymbol;
        readonly ConfigEntry<ItemMarkerFrame> markerFrame;
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
        readonly ConfigEntry<bool> senseIntegration, senseRequiredItems, senseCategories, senseContainerValues, senseSecondaryOutline, senseRemainingText;
        readonly ConfigEntry<float> senseContainerNameScale;
        readonly ConfigEntry<Color> senseQuestColor, senseHideoutColor, senseFutureColor, senseFoodColor;
        readonly ConfigEntry<Color> senseWaterColor, senseKeyColor, senseGrenadeColor, senseCurrencyColor;
        readonly ConfigEntry<Color> senseContainerNeutralColor, senseContainerBlueColor, senseContainerPaleYellowColor, senseContainerBrightYellowColor;
        readonly ConfigEntry<float> senseContainerBrightness50k, senseContainerBrightness100k, senseContainerBrightness200k, senseContainerBrightness500k;
        readonly ConfigEntry<Color> senseMissingColor, sensePartialColor, senseNextColor, senseCompleteColor;
        readonly ConfigEntry<Color> senseCountOneColor, senseCountFewColor, senseCountManyColor;
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
            backgrounds = Module(config, "Background Coloring (Valuation)", true, "Restore the accepted Item Valuation palette for ordinary items and ammunition. Keys remain owned by BetterKeys and compatibility outlines remain owned by CompatibilityHighlighter/EFT.");
            ammoPenetrationMarker = Module(config, "Ammo Penetration Class", true, "Show a compact I–VI badge on ammunition cards using EFT's own six armor penetration ratings.");
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
            markerSymbol = config.Bind("Marker", "Symbol", ItemMarkerSymbol.Check,
                "Embedded high-resolution symbol: Check, Cross, Dot or Alert.");
            markerFrame = config.Bind("Marker", "Frame", ItemMarkerFrame.Circle,
                "Embedded badge silhouette: Circle, Hex, Diamond or Square.");
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

            senseIntegration = config.Bind("Amands Sense", "Integration", true,
                "Use Item Intelligence requirement decisions for loose-loot Sense markers when Amands Sense is installed.");
            senseRequiredItems = config.Bind("Amands Sense", "Required Items", true,
                "Apply Item Intelligence active quest, hideout, future quest and completed-item decisions to Sense.");
            senseCategories = config.Bind("Amands Sense", "Category Markers", true,
                "Show key, grenade, currency, food and water markers after requirement and protected native-value priority.");
            senseContainerValues = config.Bind("Amands Sense", "Container Value Colors", true,
                "Use a value-band tint when container contents have no stronger semantic category; otherwise keep the category hue and scale its brightness. The total uses flea prices, excludes the container itself, and never adds a price to Sense text. Requires the Value module.");
            senseSecondaryOutline = config.Bind("Amands Sense", "Secondary Reason Outline", true,
                "Use the outline for a second simultaneous requirement reason.");
            senseRemainingText = config.Bind("Amands Sense", "Remaining Count Text", true,
                "Show the Item Intelligence requirement label and remaining count in Sense text mode.");
            senseContainerNameScale = config.Bind("Amands Sense", "Container Name Scale", 0.86f,
                new ConfigDescription("Scale only the native Sense container or crate name line.", new AcceptableValueRange<float>(0.50f, 1.00f)));
            senseQuestColor = ColorEntry(config, "Amands Sense Colors", "Active Quest", new Color(1.00f, 0.35f, 0.21f), "Unmet active quest requirement.");
            senseHideoutColor = ColorEntry(config, "Amands Sense Colors", "Hideout", new Color(0.20f, 0.78f, 1.00f), "Unmet hideout requirement.");
            senseFutureColor = ColorEntry(config, "Amands Sense Colors", "Future Quest", new Color(0.75f, 0.55f, 1.00f), "Unmet future quest requirement.");
            senseFoodColor = ColorEntry(config, "Amands Sense Colors", "Food", new Color(0.84f, 0.93f, 0.70f), "Food and drink category when no stronger Item Intelligence requirement is present.");
            senseWaterColor = ColorEntry(config, "Amands Sense Colors", "Water", new Color(33f / 255f, 168f / 255f, 1.00f), "Water and drinks category when no stronger Item Intelligence requirement is present; matches the installed Sense Drinks color.");
            senseKeyColor = ColorEntry(config, "Amands Sense Colors", "Keys", new Color(0.18f, 0.90f, 0.70f), "Key category when no stronger Item Intelligence requirement is present.");
            senseGrenadeColor = ColorEntry(config, "Amands Sense Colors", "Grenades", new Color(1.00f, 0.52f, 0.16f), "Grenade category when no stronger Item Intelligence requirement is present.");
            senseCurrencyColor = ColorEntry(config, "Amands Sense Colors", "Currency", new Color(0.82f, 0.78f, 0.45f), "Money category when no stronger Item Intelligence requirement is present.");
            senseContainerNeutralColor = ColorEntry(config, "Amands Sense Container Value Colors", "Below 50k", Color.white, "Neutral marker tint for container contents below 50,000 total flea value.");
            senseContainerBlueColor = ColorEntry(config, "Amands Sense Container Value Colors", "50k to 100k", new Color(0.20f, 0.52f, 1.00f), "Blue marker tint for container contents from 50,000 to 99,999 total flea value.");
            senseContainerPaleYellowColor = ColorEntry(config, "Amands Sense Container Value Colors", "100k to 200k", new Color(1.00f, 0.91f, 0.45f), "Pale yellow marker tint for container contents from 100,000 to 199,999 total flea value.");
            senseContainerBrightYellowColor = ColorEntry(config, "Amands Sense Container Value Colors", "200k Plus", new Color(1.00f, 0.86f, 0.12f), "Bright yellow marker tint for container contents worth 200,000 or more on the flea market.");
            senseContainerBrightness50k = BrightnessEntry(config, "50k", 0.70f, "Marker brightness at a 50,000 total flea value.");
            senseContainerBrightness100k = BrightnessEntry(config, "100k", 0.85f, "Marker brightness at a 100,000 total flea value.");
            senseContainerBrightness200k = BrightnessEntry(config, "200k", 0.95f, "Marker brightness at a 200,000 total flea value.");
            senseContainerBrightness500k = BrightnessEntry(config, "500k Plus", 1.00f, "Maximum marker brightness at 500,000 or more total flea value.");
            senseMissingColor = ColorEntry(config, "Amands Sense Stock Colors", "Missing", new Color(1.00f, 0.16f, 0.12f), "Not enough for the nearest requirement.");
            sensePartialColor = ColorEntry(config, "Amands Sense Stock Colors", "Partial", new Color(1.00f, 0.58f, 0.12f), "Some useful stock, but the nearest requirement is not covered.");
            senseNextColor = ColorEntry(config, "Amands Sense Stock Colors", "Next Covered", new Color(0.62f, 1.00f, 0.38f), "Nearest requirement covered, later requirements remain.");
            senseCompleteColor = ColorEntry(config, "Amands Sense Stock Colors", "Complete", new Color(0.10f, 1.00f, 0.20f), "All tracked requirements are covered.");
            senseCountOneColor = ColorEntry(config, "Amands Sense Count Colors", "One Item", Color.white, "Count color for one useful item in a container.");
            senseCountFewColor = ColorEntry(config, "Amands Sense Count Colors", "Two to Three", new Color(1.00f, 0.91f, 0.45f), "Count color for two or three useful items in a container.");
            senseCountManyColor = ColorEntry(config, "Amands Sense Count Colors", "Four Plus", new Color(1.00f, 0.62f, 0.22f), "Count color for four or more useful items in a container.");

            tooltipMode.SettingChanged += delegate { Touch(); };
            valueMode.SettingChanged += delegate { Touch(); };
            tooltipScale.SettingChanged += delegate { Touch(); };
            tooltipOpacity.SettingChanged += delegate { Touch(); };
            tooltipFontSize.SettingChanged += delegate { Touch(); };
            tooltipMaximumWidth.SettingChanged += delegate { Touch(); };
            tooltipPadding.SettingChanged += delegate { Touch(); };
            markerSide.SettingChanged += delegate { Touch(); };
            markerSymbol.SettingChanged += delegate { Touch(); };
            markerFrame.SettingChanged += delegate { Touch(); };
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
            senseIntegration.SettingChanged += delegate { Touch(); };
            senseRequiredItems.SettingChanged += delegate { Touch(); };
            senseCategories.SettingChanged += delegate { Touch(); };
            senseContainerValues.SettingChanged += delegate { Touch(); };
            senseSecondaryOutline.SettingChanged += delegate { Touch(); };
            senseRemainingText.SettingChanged += delegate { Touch(); };
            senseContainerNameScale.SettingChanged += delegate { Touch(); };
            senseQuestColor.SettingChanged += delegate { Touch(); };
            senseHideoutColor.SettingChanged += delegate { Touch(); };
            senseFutureColor.SettingChanged += delegate { Touch(); };
            senseFoodColor.SettingChanged += delegate { Touch(); };
            senseWaterColor.SettingChanged += delegate { Touch(); };
            senseKeyColor.SettingChanged += delegate { Touch(); };
            senseGrenadeColor.SettingChanged += delegate { Touch(); };
            senseCurrencyColor.SettingChanged += delegate { Touch(); };
            senseContainerNeutralColor.SettingChanged += delegate { Touch(); };
            senseContainerBlueColor.SettingChanged += delegate { Touch(); };
            senseContainerPaleYellowColor.SettingChanged += delegate { Touch(); };
            senseContainerBrightYellowColor.SettingChanged += delegate { Touch(); };
            senseContainerBrightness50k.SettingChanged += delegate { Touch(); };
            senseContainerBrightness100k.SettingChanged += delegate { Touch(); };
            senseContainerBrightness200k.SettingChanged += delegate { Touch(); };
            senseContainerBrightness500k.SettingChanged += delegate { Touch(); };
            senseMissingColor.SettingChanged += delegate { Touch(); };
            sensePartialColor.SettingChanged += delegate { Touch(); };
            senseNextColor.SettingChanged += delegate { Touch(); };
            senseCompleteColor.SettingChanged += delegate { Touch(); };
            senseCountOneColor.SettingChanged += delegate { Touch(); };
            senseCountFewColor.SettingChanged += delegate { Touch(); };
            senseCountManyColor.SettingChanged += delegate { Touch(); };
            modules = ReadModules();
        }

        public ItemTooltipMode TooltipMode => tooltipMode.Value;
        public ModuleSelection Modules => modules;
        ModuleSelection ReadModules() => new ModuleSelection(markers.Value, tooltips.Value, quests.Value, futureQuests.Value, hideout.Value, value.Value, relevance.Value, backgrounds.Value, ammoPenetrationMarker.Value);
        public ItemValueMode ValueMode => valueMode.Value;
        public float TooltipScale => Mathf.Clamp(tooltipScale.Value, 0.10f, 5.00f);
        public float TooltipOpacity => Mathf.Clamp01(tooltipOpacity.Value);
        public int TooltipFontSize => Mathf.Clamp(tooltipFontSize.Value, 1, 64);
        public int TooltipMaximumWidth => Mathf.Clamp(tooltipMaximumWidth.Value, 80, 1000);
        public float TooltipPadding => Mathf.Clamp(tooltipPadding.Value, 0f, 40f);
        public ItemMarkerSide MarkerSide => markerSide.Value;
        public ItemMarkerSymbol MarkerSymbol => markerSymbol.Value;
        public ItemMarkerFrame MarkerFrame => markerFrame.Value;
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
        public bool SenseIntegration => senseIntegration.Value;
        public bool SenseRequiredItems => senseRequiredItems.Value;
        public bool SenseCategories => senseCategories.Value;
        public bool SenseContainerValues => senseContainerValues.Value && modules.Value;
        public bool SenseSecondaryOutline => senseSecondaryOutline.Value;
        public bool SenseRemainingText => senseRemainingText.Value;
        public float SenseContainerNameScale => Mathf.Clamp(senseContainerNameScale.Value, 0.50f, 1.00f);
        public Color GetSenseColor(ItemNeedReason reason)
        {
            if (reason == ItemNeedReason.ActiveQuest) return senseQuestColor.Value;
            if (reason == ItemNeedReason.Hideout) return senseHideoutColor.Value;
            if (reason == ItemNeedReason.FutureQuest) return senseFutureColor.Value;
            if (reason == ItemNeedReason.Food) return senseFoodColor.Value;
            if (reason == ItemNeedReason.Water) return senseWaterColor.Value;
            if (reason == ItemNeedReason.Key) return senseKeyColor.Value;
            if (reason == ItemNeedReason.Grenade) return senseGrenadeColor.Value;
            if (reason == ItemNeedReason.Currency) return senseCurrencyColor.Value;
            return defaultColor.Value;
        }
        public float GetSenseContainerValueBrightness(long totalValue) =>
            SenseContainerValuePolicy.ResolveBrightness(totalValue, senseContainerBrightness50k.Value,
                senseContainerBrightness100k.Value, senseContainerBrightness200k.Value, senseContainerBrightness500k.Value);
        public Color GetSenseContainerValueColor(long totalValue)
        {
            SenseContainerValueBand band = SenseContainerValuePolicy.ResolveBand(totalValue);
            if (band == SenseContainerValueBand.Blue) return senseContainerBlueColor.Value;
            if (band == SenseContainerValueBand.PaleYellow) return senseContainerPaleYellowColor.Value;
            if (band == SenseContainerValueBand.BrightYellow) return senseContainerBrightYellowColor.Value;
            return senseContainerNeutralColor.Value;
        }
        public Color GetSenseCountColor(int count)
        {
            if (count >= 4) return senseCountManyColor.Value;
            if (count >= 2) return senseCountFewColor.Value;
            return senseCountOneColor.Value;
        }
        public Color GetSenseStockColor(SenseStockState state)
        {
            if (state == SenseStockState.Complete) return senseCompleteColor.Value;
            if (state == SenseStockState.NextCovered) return senseNextColor.Value;
            if (state == SenseStockState.Partial) return sensePartialColor.Value;
            return senseMissingColor.Value;
        }
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

        static ConfigEntry<float> BrightnessEntry(ConfigFile config, string key, float value, string description)
        {
            return config.Bind("Amands Sense Container Value Brightness", key, value,
                new ConfigDescription(description, new AcceptableValueRange<float>(0.10f, 1.00f)));
        }
    }
}

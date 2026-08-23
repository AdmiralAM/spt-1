using BepInEx.Configuration;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemIntelligenceUiSettings
    {
        readonly ConfigEntry<ItemTooltipMode> tooltipMode;
        readonly ConfigEntry<float> markerSize;
        readonly ConfigEntry<float> markerOpacity;
        readonly ConfigEntry<float> markerOffsetX;
        readonly ConfigEntry<float> markerOffsetY;
        readonly ConfigEntry<string> neutralColor;
        readonly ConfigEntry<string> questNowColor;
        readonly ConfigEntry<string> questLaterColor;
        readonly ConfigEntry<string> hideoutColor;
        readonly ConfigEntry<string> keepColor;
        readonly ConfigEntry<string> loadingColor;
        readonly ConfigEntry<string> unavailableColor;
        Color neutral;
        Color questNow;
        Color questLater;
        Color hideout;
        Color keep;
        Color loading;
        Color unavailable;

        public ItemIntelligenceUiSettings(ConfigFile config)
        {
            tooltipMode = config.Bind("Tooltip", "Mode", ItemTooltipMode.Normal, "Minimal: Value + Keep. Normal: adds Quest Now/Later and Hideout. Detailed: adds per-slot and owned counts. Full: adds decision and template id.");
            markerSize = config.Bind("Marker", "Size", 18f, new ConfigDescription("Marker size in pixels.", new AcceptableValueRange<float>(12f, 32f)));
            markerOpacity = config.Bind("Marker", "Opacity", 0.94f, new ConfigDescription("Marker opacity.", new AcceptableValueRange<float>(0.20f, 1f)));
            markerOffsetX = config.Bind("Marker", "Offset X", -3f, new ConfigDescription("Horizontal offset from the item card top-right corner.", new AcceptableValueRange<float>(-40f, 40f)));
            markerOffsetY = config.Bind("Marker", "Offset Y", 3f, new ConfigDescription("Vertical offset from the item card top-right corner.", new AcceptableValueRange<float>(-40f, 40f)));

            neutralColor = ColorEntry(config, "Neutral Color", "#607D95", "No active requirement.");
            questNowColor = ColorEntry(config, "Quest Now Color", "#D85B35", "Needed by an active quest.");
            questLaterColor = ColorEntry(config, "Quest Later Color", "#9B7BC4", "Needed by a future quest.");
            hideoutColor = ColorEntry(config, "Hideout Color", "#3E9CC7", "Needed by a future hideout stage.");
            keepColor = ColorEntry(config, "Keep Color", "#C9A63B", "Required for another known reason.");
            loadingColor = ColorEntry(config, "Loading Color", "#68717A", "Live requirement data is loading.");
            unavailableColor = ColorEntry(config, "Unavailable Color", "#B83A3A", "Live requirement data is unavailable.");
            RefreshColors();
            neutralColor.SettingChanged += delegate { RefreshColors(); };
            questNowColor.SettingChanged += delegate { RefreshColors(); };
            questLaterColor.SettingChanged += delegate { RefreshColors(); };
            hideoutColor.SettingChanged += delegate { RefreshColors(); };
            keepColor.SettingChanged += delegate { RefreshColors(); };
            loadingColor.SettingChanged += delegate { RefreshColors(); };
            unavailableColor.SettingChanged += delegate { RefreshColors(); };
        }

        public ItemTooltipMode TooltipMode => tooltipMode.Value;
        public float MarkerSize => Mathf.Clamp(markerSize.Value, 12f, 32f);
        public float MarkerOpacity => Mathf.Clamp01(markerOpacity.Value);
        public float MarkerOffsetX => Mathf.Clamp(markerOffsetX.Value, -40f, 40f);
        public float MarkerOffsetY => Mathf.Clamp(markerOffsetY.Value, -40f, 40f);

        public Color GetColor(ItemMarkerKind kind)
        {
            switch (kind)
            {
                case ItemMarkerKind.QuestNow: return questNow;
                case ItemMarkerKind.QuestLater: return questLater;
                case ItemMarkerKind.Hideout: return hideout;
                case ItemMarkerKind.Keep: return keep;
                case ItemMarkerKind.Loading: return loading;
                case ItemMarkerKind.Unavailable: return unavailable;
                default: return neutral;
            }
        }

        void RefreshColors()
        {
            neutral = Parse(neutralColor.Value, new Color(0.38f, 0.49f, 0.58f));
            questNow = Parse(questNowColor.Value, new Color(0.85f, 0.36f, 0.21f));
            questLater = Parse(questLaterColor.Value, new Color(0.61f, 0.48f, 0.77f));
            hideout = Parse(hideoutColor.Value, new Color(0.24f, 0.61f, 0.78f));
            keep = Parse(keepColor.Value, new Color(0.79f, 0.65f, 0.23f));
            loading = Parse(loadingColor.Value, new Color(0.41f, 0.44f, 0.48f));
            unavailable = Parse(unavailableColor.Value, new Color(0.72f, 0.23f, 0.23f));
        }

        static ConfigEntry<string> ColorEntry(ConfigFile config, string name, string value, string description)
        {
            return config.Bind("Marker Colors", name, value, description + " Use #RRGGBB.");
        }

        static Color Parse(string value, Color fallback)
        {
            Color parsed;
            return !string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value.Trim(), out parsed) ? parsed : fallback;
        }
    }
}

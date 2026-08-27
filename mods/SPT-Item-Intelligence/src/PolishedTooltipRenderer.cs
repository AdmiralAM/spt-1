using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SPTItemIntelligence
{
    internal static class PolishedTooltipRenderer
    {
        const float Gap = 8f;
        const float ScreenMargin = 6f;
        const long PriceGreen = 50000;
        const long PriceRed = 100000;
        const long PriceGold = 250000;
        const int RenderCacheLimit = 1024;

        static GUISkin cachedSkin;
        static GUIStyle cachedLabel;
        static GUIStyle cachedSemanticLabel;
        static readonly GUIContent measureContent = new GUIContent();
        static readonly Dictionary<string, string> displayLineCache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> priceRenderCache = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<SemanticLineKey, string> semanticRenderCache = new Dictionary<SemanticLineKey, string>();
        static string[] lineBuffer = Array.Empty<string>();
        static float[] rowHeightBuffer = Array.Empty<float>();

        public static void Draw(Rect marker, ItemHoverText text, ItemIntelligenceUiSettings settings)
        {
            if (text == null || settings == null) return;

            ItemTooltipMode mode = settings.TooltipMode;
            int lineCount = text.GetLineCount(mode);
            if (lineCount <= 0) return;

            EnsureBuffers(lineCount);

            float scale = settings.TooltipScale;
            int fontSize = Mathf.RoundToInt(settings.TooltipFontSize * scale);
            float baseLineHeight = Mathf.Max(fontSize + 8f, 20f * scale);
            float horizontalPadding = 11f * scale;
            float verticalPadding = 7f * scale;
            float rowGap = 1f * scale;

            GUIStyle label = GetLabelStyle(fontSize);
            GUIStyle semanticLabel = GetSemanticLabelStyle(fontSize);

            float naturalWidth = 0f;
            for (int i = 0; i < lineCount; i++)
            {
                string line = DisplayLine(text.GetLine(mode, i), mode);
                lineBuffer[i] = line;
                if (line.Length == 0) continue;
                measureContent.text = line;
                naturalWidth = Mathf.Max(naturalWidth, label.CalcSize(measureContent).x);
            }

            float minimumWidth = 200f * scale;
            float preferredMaximumWidth = 430f * scale;
            float screenMaximumWidth = Mathf.Max(minimumWidth, Screen.width - ScreenMargin * 2f);
            float maximumWidth = Mathf.Min(preferredMaximumWidth, screenMaximumWidth);
            float width = Mathf.Clamp(naturalWidth + horizontalPadding * 2f, minimumWidth, maximumWidth);
            float textWidth = Mathf.Max(1f, width - horizontalPadding * 2f);

            float contentHeight = 0f;
            for (int i = 0; i < lineCount; i++)
            {
                if (lineBuffer[i].Length == 0)
                {
                    rowHeightBuffer[i] = baseLineHeight;
                }
                else
                {
                    measureContent.text = lineBuffer[i];
                    float wrappedHeight = label.CalcHeight(measureContent, textWidth);
                    rowHeightBuffer[i] = Mathf.Max(baseLineHeight, wrappedHeight + 2f * scale);
                }
                contentHeight += rowHeightBuffer[i];
                if (i + 1 < lineCount) contentHeight += rowGap;
            }

            float height = verticalPadding * 2f + contentHeight;
            float x = marker.xMax + Gap;
            if (x + width > Screen.width - ScreenMargin) x = marker.xMin - width - Gap;
            x = Mathf.Clamp(x, ScreenMargin, Mathf.Max(ScreenMargin, Screen.width - ScreenMargin - width));
            float y = Mathf.Clamp(marker.yMin, ScreenMargin, Mathf.Max(ScreenMargin, Screen.height - ScreenMargin - height));

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, settings.TooltipOpacity);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            float yCursor = y + verticalPadding;
            for (int i = 0; i < lineCount; i++)
            {
                string line = lineBuffer[i];
                if (line.Length > 0)
                {
                    Color semantic = ResolveColor(line, settings);
                    bool hasSemanticProgress = semantic != Color.white && HasProgressRatio(line);
                    bool hasPrice = TryReadRoubleAmount(line, out long price, out _, out _);
                    GUIStyle activeStyle = hasSemanticProgress || hasPrice ? semanticLabel : label;
                    activeStyle.normal.textColor = Color.white;
                    string rendered = hasSemanticProgress
                        ? GetCachedSemanticLine(line, semantic)
                        : hasPrice ? GetCachedPriceLine(line, price) : line;
                    GUI.Label(new Rect(x + horizontalPadding, yCursor, textWidth, rowHeightBuffer[i]), rendered, activeStyle);
                }
                yCursor += rowHeightBuffer[i] + rowGap;
            }

            GUI.color = previous;
        }

        static GUIStyle GetLabelStyle(int fontSize)
        {
            GUISkin skin = GUI.skin;
            if (cachedLabel == null || !object.ReferenceEquals(cachedSkin, skin))
            {
                cachedSkin = skin;
                cachedLabel = new GUIStyle(skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = true,
                    richText = false,
                    padding = new RectOffset(0, 0, 1, 2)
                };
                cachedSemanticLabel = new GUIStyle(cachedLabel) { richText = true };
            }
            cachedLabel.fontSize = fontSize;
            cachedSemanticLabel.fontSize = fontSize;
            return cachedLabel;
        }

        static GUIStyle GetSemanticLabelStyle(int fontSize)
        {
            GetLabelStyle(fontSize);
            return cachedSemanticLabel;
        }

        static void EnsureBuffers(int lineCount)
        {
            if (lineBuffer.Length >= lineCount) return;
            int capacity = Math.Max(8, lineBuffer.Length == 0 ? 8 : lineBuffer.Length * 2);
            while (capacity < lineCount) capacity *= 2;
            lineBuffer = new string[capacity];
            rowHeightBuffer = new float[capacity];
        }

        internal static string DisplayLine(string line, ItemTooltipMode mode)
        {
            if (string.IsNullOrEmpty(line) || mode == ItemTooltipMode.Full) return line ?? string.Empty;
            const string firSeparator = " · FIR";
            int fir = line.IndexOf(firSeparator, StringComparison.OrdinalIgnoreCase);
            if (fir < 0) return line;

            string cached;
            if (displayLineCache.TryGetValue(line, out cached)) return cached;
            cached = line.Substring(0, fir);
            AddBounded(displayLineCache, line, cached);
            return cached;
        }

        internal static Color ResolveColor(string line, ItemIntelligenceUiSettings settings)
        {
            if (string.IsNullOrEmpty(line) || settings == null) return Color.white;
            bool found = false;
            bool anyProgress = false;
            bool allComplete = true;
            int cursor = 0;
            while (TryReadNextRatio(line, ref cursor, out int owned, out int required))
            {
                if (required <= 0) continue;
                found = true;
                if (owned > 0) anyProgress = true;
                if (owned < required) allComplete = false;
            }
            if (!found) return Color.white;
            if (allComplete) return settings.CompleteColor;
            if (!anyProgress) return settings.MissingColor;
            return settings.PartialColor;
        }

        internal static Color ResolvePriceColor(long value)
        {
            if (value >= PriceGold) return new Color(1f, 0.72f, 0.18f, 1f);
            if (value >= PriceRed) return new Color(1f, 0.32f, 0.28f, 1f);
            if (value >= PriceGreen) return new Color(0.38f, 0.90f, 0.42f, 1f);
            return Color.white;
        }

        static string GetCachedPriceLine(string line, long value)
        {
            string cached;
            if (priceRenderCache.TryGetValue(line, out cached)) return cached;
            cached = ApplyPriceAmountColor(line, ResolvePriceColor(value));
            AddBounded(priceRenderCache, line, cached);
            return cached;
        }

        static string GetCachedSemanticLine(string line, Color color)
        {
            SemanticLineKey key = new SemanticLineKey(line, PackColor(color));
            string cached;
            if (semanticRenderCache.TryGetValue(key, out cached)) return cached;
            cached = ApplySemanticProgressColor(line, color);
            if (semanticRenderCache.Count >= RenderCacheLimit) semanticRenderCache.Clear();
            semanticRenderCache[key] = cached;
            return cached;
        }

        static void AddBounded(Dictionary<string, string> cache, string key, string value)
        {
            if (cache.Count >= RenderCacheLimit) cache.Clear();
            cache[key] = value;
        }

        static uint PackColor(Color color)
        {
            Color32 packed = color;
            return ((uint)packed.r << 24) | ((uint)packed.g << 16) | ((uint)packed.b << 8) | packed.a;
        }

        internal static string ApplyPriceAmountColor(string line, Color color)
        {
            if (!TryReadRoubleAmount(line, out _, out int start, out int end) || color == Color.white) return line ?? string.Empty;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return line.Substring(0, start) + "<color=#" + hex + ">" + line.Substring(start, end - start) + "</color>" + line.Substring(end);
        }

        internal static bool TryReadRoubleAmount(string line, out long value, out int start, out int end)
        {
            value = 0;
            start = -1;
            end = -1;
            if (string.IsNullOrEmpty(line)) return false;

            int rouble = line.IndexOf('₽');
            if (rouble <= 0) return false;
            int cursor = rouble - 1;
            while (cursor >= 0 && char.IsWhiteSpace(line[cursor])) cursor--;
            int numberEnd = cursor + 1;
            while (cursor >= 0 && (char.IsDigit(line[cursor]) || line[cursor] == ',' || line[cursor] == ' ')) cursor--;
            int numberStart = cursor + 1;
            if (numberEnd <= numberStart) return false;

            long parsed = 0;
            bool hasDigit = false;
            for (int i = numberStart; i < numberEnd; i++)
            {
                char c = line[i];
                if (c == ',' || c == ' ') continue;
                if (c < '0' || c > '9') return false;
                hasDigit = true;
                int digit = c - '0';
                if (parsed > (long.MaxValue - digit) / 10) { parsed = long.MaxValue; break; }
                parsed = parsed * 10 + digit;
            }
            if (!hasDigit) return false;
            value = parsed;
            start = numberStart;
            end = rouble + 1;
            return true;
        }

        internal static string ApplySemanticProgressColor(string line, Color color)
        {
            if (string.IsNullOrEmpty(line)) return line ?? string.Empty;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            StringBuilder result = new StringBuilder(line.Length + 48);
            int cursor = 0;
            int scan = 0;
            while (TryFindNextRatio(line, scan, out int start, out int end))
            {
                result.Append(line, cursor, start - cursor);
                result.Append("<color=#").Append(hex).Append('>');
                result.Append(line, start, end - start);
                result.Append("</color>");
                cursor = end;
                scan = end;
            }
            result.Append(line, cursor, line.Length - cursor);
            string colored = result.ToString();
            if (colored.EndsWith("✓", StringComparison.Ordinal))
                colored = colored.Substring(0, colored.Length - 1) + "<color=#" + hex + ">✓</color>";
            return colored;
        }

        static bool HasProgressRatio(string line)
        {
            int cursor = 0;
            return TryReadNextRatio(line, ref cursor, out _, out _);
        }

        static bool TryReadNextRatio(string line, ref int cursor, out int owned, out int required)
        {
            owned = 0;
            required = 0;
            if (!TryFindNextRatio(line, cursor, out int start, out int end))
            {
                cursor = line == null ? 0 : line.Length;
                return false;
            }
            int slash = line.IndexOf('/', start, end - start);
            if (slash < 0 || !TryParsePositiveInt(line, start, slash, out owned) || !TryParsePositiveInt(line, slash + 1, end, out required))
            {
                cursor = end;
                return false;
            }
            cursor = end;
            return true;
        }

        static bool TryParsePositiveInt(string text, int start, int end, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || start < 0 || end <= start || end > text.Length) return false;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9') return false;
                int digit = c - '0';
                if (value > (int.MaxValue - digit) / 10) return false;
                value = value * 10 + digit;
            }
            return true;
        }

        static bool TryFindNextRatio(string line, int startAt, out int start, out int end)
        {
            start = -1;
            end = -1;
            if (string.IsNullOrEmpty(line)) return false;
            for (int i = Math.Max(0, startAt); i < line.Length; i++)
            {
                if (!char.IsDigit(line[i])) continue;
                int firstEnd = i;
                while (firstEnd < line.Length && char.IsDigit(line[firstEnd])) firstEnd++;
                if (firstEnd >= line.Length || line[firstEnd] != '/') { i = firstEnd; continue; }
                int secondStart = firstEnd + 1;
                int secondEnd = secondStart;
                while (secondEnd < line.Length && char.IsDigit(line[secondEnd])) secondEnd++;
                if (secondEnd == secondStart) { i = firstEnd; continue; }
                start = i;
                end = secondEnd;
                return true;
            }
            return false;
        }

        readonly struct SemanticLineKey : IEquatable<SemanticLineKey>
        {
            readonly string line;
            readonly uint color;

            public SemanticLineKey(string line, uint color)
            {
                this.line = line ?? string.Empty;
                this.color = color;
            }

            public bool Equals(SemanticLineKey other)
            {
                return color == other.color && string.Equals(line, other.line, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is SemanticLineKey && Equals((SemanticLineKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((line == null ? 0 : StringComparer.Ordinal.GetHashCode(line)) * 397) ^ (int)color;
                }
            }
        }
    }
}

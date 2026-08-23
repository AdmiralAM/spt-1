using System;
using UnityEngine;

namespace SPTItemIntelligence
{
    internal static class PolishedTooltipRenderer
    {
        const float Gap = 8f;

        public static void Draw(Rect marker, ItemHoverText text, ItemIntelligenceUiSettings settings)
        {
            if (text == null || settings == null) return;

            ItemTooltipMode mode = settings.TooltipMode;
            int lineCount = text.GetLineCount(mode);
            if (lineCount <= 0) return;

            float scale = settings.TooltipScale;
            int fontSize = Mathf.RoundToInt(settings.TooltipFontSize * scale);
            float lineHeight = Mathf.Max(fontSize + 7f, 19f * scale);
            float horizontalPadding = 12f * scale;
            float verticalPadding = 8f * scale;

            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(0, 0, 0, 1)
            };

            float contentWidth = 0f;
            for (int i = 0; i < lineCount; i++)
            {
                string line = DisplayLine(text.GetLine(mode, i), mode);
                if (line.Length == 0) continue;
                contentWidth = Mathf.Max(contentWidth, label.CalcSize(new GUIContent(line)).x);
            }

            float width = Mathf.Clamp(contentWidth + horizontalPadding * 2f, 220f * scale, 430f * scale);
            float height = verticalPadding * 2f + lineCount * lineHeight;
            float x = marker.xMax + Gap;
            if (x + width > Screen.width) x = marker.xMin - width - Gap;
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width));
            float y = Mathf.Clamp(marker.yMin, 0f, Mathf.Max(0f, Screen.height - height));

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, settings.TooltipOpacity);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            for (int i = 0; i < lineCount; i++)
            {
                string raw = text.GetLine(mode, i);
                string line = DisplayLine(raw, mode);
                if (line.Length == 0) continue;
                label.normal.textColor = ResolveColor(line, settings);
                GUI.Label(
                    new Rect(x + horizontalPadding, y + verticalPadding + i * lineHeight, width - horizontalPadding * 2f, lineHeight + 2f),
                    line,
                    label);
            }

            GUI.color = previous;
        }

        internal static string DisplayLine(string line, ItemTooltipMode mode)
        {
            if (string.IsNullOrEmpty(line) || mode == ItemTooltipMode.Full) return line ?? string.Empty;

            const string firSeparator = " · FIR";
            int fir = line.IndexOf(firSeparator, StringComparison.OrdinalIgnoreCase);
            if (fir >= 0) return line.Substring(0, fir);
            return line;
        }

        internal static Color ResolveColor(string line, ItemIntelligenceUiSettings settings)
        {
            if (string.IsNullOrEmpty(line) || settings == null) return Color.white;
            if (line.EndsWith("✓", StringComparison.Ordinal)) return settings.CompleteColor;

            int colon = line.IndexOf(':');
            int slash = line.IndexOf('/');
            if (colon >= 0 && slash > colon)
            {
                int owned;
                int required;
                if (TryReadNumberBeforeSlash(line, colon + 1, slash, out owned) &&
                    TryReadNumberAfterSlash(line, slash + 1, out required) && required > 0)
                {
                    if (owned <= 0) return settings.MissingColor;
                    if (owned < required) return settings.PartialColor;
                    return settings.CompleteColor;
                }
            }

            return Color.white;
        }

        static bool TryReadNumberBeforeSlash(string line, int start, int slash, out int value)
        {
            value = 0;
            int first = -1;
            for (int i = start; i < slash; i++)
            {
                if (!char.IsDigit(line[i])) continue;
                first = i;
                break;
            }
            if (first < 0) return false;
            return int.TryParse(line.Substring(first, slash - first).Trim(), out value);
        }

        static bool TryReadNumberAfterSlash(string line, int start, out int value)
        {
            value = 0;
            int end = start;
            while (end < line.Length && char.IsDigit(line[end])) end++;
            if (end == start) return false;
            return int.TryParse(line.Substring(start, end - start), out value);
        }
    }
}

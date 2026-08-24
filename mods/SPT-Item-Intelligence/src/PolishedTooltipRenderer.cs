using System;
using UnityEngine;

namespace SPTItemIntelligence
{
    internal static class PolishedTooltipRenderer
    {
        const float Gap = 8f;
        const float ScreenMargin = 6f;

        public static void Draw(Rect marker, ItemHoverText text, ItemIntelligenceUiSettings settings)
        {
            if (text == null || settings == null) return;

            ItemTooltipMode mode = settings.TooltipMode;
            int lineCount = text.GetLineCount(mode);
            if (lineCount <= 0) return;

            float scale = settings.TooltipScale;
            int fontSize = Mathf.RoundToInt(settings.TooltipFontSize * scale);
            float baseLineHeight = Mathf.Max(fontSize + 8f, 20f * scale);
            float horizontalPadding = 11f * scale;
            float verticalPadding = 7f * scale;
            float rowGap = 1f * scale;

            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(0, 0, 1, 2)
            };

            string[] lines = new string[lineCount];
            float naturalWidth = 0f;
            for (int i = 0; i < lineCount; i++)
            {
                string line = DisplayLine(text.GetLine(mode, i), mode);
                lines[i] = line;
                if (line.Length == 0) continue;
                naturalWidth = Mathf.Max(naturalWidth, label.CalcSize(new GUIContent(line)).x);
            }

            float minimumWidth = 200f * scale;
            float preferredMaximumWidth = 430f * scale;
            float screenMaximumWidth = Mathf.Max(minimumWidth, Screen.width - ScreenMargin * 2f);
            float maximumWidth = Mathf.Min(preferredMaximumWidth, screenMaximumWidth);
            float width = Mathf.Clamp(naturalWidth + horizontalPadding * 2f, minimumWidth, maximumWidth);
            float textWidth = Mathf.Max(1f, width - horizontalPadding * 2f);

            float[] rowHeights = new float[lineCount];
            float contentHeight = 0f;
            for (int i = 0; i < lineCount; i++)
            {
                if (lines[i].Length == 0)
                {
                    rowHeights[i] = baseLineHeight;
                }
                else
                {
                    float wrappedHeight = label.CalcHeight(new GUIContent(lines[i]), textWidth);
                    rowHeights[i] = Mathf.Max(baseLineHeight, wrappedHeight + 2f * scale);
                }
                contentHeight += rowHeights[i];
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
                string line = lines[i];
                if (line.Length > 0)
                {
                    label.normal.textColor = ResolveColor(line, settings);
                    GUI.Label(
                        new Rect(x + horizontalPadding, yCursor, textWidth, rowHeights[i]),
                        line,
                        label);
                }
                yCursor += rowHeights[i] + rowGap;
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

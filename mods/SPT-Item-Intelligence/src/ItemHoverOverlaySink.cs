using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverOverlaySink : IItemHoverViewSink, IItemHoverAnchorSink
    {
        readonly Vector3[] worldCorners = new Vector3[4];
        readonly ItemIntelligenceUiSettings settings;
        ItemHoverText current = ItemHoverText.Empty;
        RectTransform anchor;
        GUIStyle markerStyle;
        bool drawingDisabled;

        public ItemHoverOverlaySink(ItemIntelligenceUiSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public ItemHoverText Current => Volatile.Read(ref current);

        public void Show(ItemHoverText text)
        {
            Interlocked.Exchange(ref current, text ?? ItemHoverText.Empty);
        }

        public void Clear()
        {
            Interlocked.Exchange(ref current, ItemHoverText.Empty);
        }

        public void SetAnchor(object itemView)
        {
            Interlocked.Exchange(ref anchor, ResolveRectTransform(itemView));
        }

        public void ClearAnchor()
        {
            Interlocked.Exchange(ref anchor, null);
        }

        public void Draw()
        {
            if (drawingDisabled) return;
            ItemHoverText text = Current;
            RectTransform target = Volatile.Read(ref anchor);
            if (text == null || !text.HasData || target == null) return;

            try
            {
                Rect markerRect;
                if (!TryGetMarkerRect(target, out markerRect)) return;

                ItemMarkerPresentation marker = ItemMarkerPresentation.From(text);
                if (!marker.IsVisible) return;

                int previousDepth = GUI.depth;
                Color previousColor = GUI.color;
                try
                {
                    GUI.depth = -1000;
                    Color markerColor = settings.GetColor(marker.Kind);
                    markerColor.a *= settings.MarkerOpacity;
                    GUI.color = markerColor;
                    GUI.DrawTexture(markerRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Box(markerRect, GUIContent.none);
                    GUI.Label(markerRect, marker.Glyph, MarkerStyle);

                    Vector2 mouse = Event.current == null
                        ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)
                        : Event.current.mousePosition;
                    if (markerRect.Contains(mouse)) DrawDetails(markerRect, text, settings.TooltipMode);
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.depth = previousDepth;
                }
            }
            catch
            {
                drawingDisabled = true;
                Clear();
                ClearAnchor();
            }
        }

        GUIStyle MarkerStyle
        {
            get
            {
                if (markerStyle != null) return markerStyle;
                markerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
                markerStyle.normal.textColor = Color.white;
                markerStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(settings.MarkerSize * 0.66f), 10, 22);
                return markerStyle;
            }
        }

        bool TryGetMarkerRect(RectTransform target, out Rect marker)
        {
            target.GetWorldCorners(worldCorners);
            Camera camera = ResolveCamera(target);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[2]);

            float left = Mathf.Min(bottomLeft.x, topRight.x);
            float right = Mathf.Max(bottomLeft.x, topRight.x);
            float top = Screen.height - Mathf.Max(bottomLeft.y, topRight.y);
            float bottom = Screen.height - Mathf.Min(bottomLeft.y, topRight.y);
            float itemWidth = right - left;
            float itemHeight = bottom - top;
            if (itemWidth < 4f || itemHeight < 4f)
            {
                marker = default(Rect);
                return false;
            }

            float maximumSize = Mathf.Max(12f, Mathf.Min(itemWidth, itemHeight) - 6f);
            float size = Mathf.Min(settings.MarkerSize, maximumSize);
            marker = new Rect(right - size + settings.MarkerOffsetX, top + settings.MarkerOffsetY, size, size);
            return marker.xMax > 0f && marker.yMax > 0f && marker.xMin < Screen.width && marker.yMin < Screen.height;
        }

        static void DrawDetails(Rect marker, ItemHoverText text, ItemTooltipMode mode)
        {
            const float width = 286f;
            const float gap = 8f;
            const float lineHeight = 18f;
            int lineCount = text.GetLineCount(mode);
            float height = 12f + Math.Max(1, lineCount) * lineHeight;
            float x = marker.xMax + gap;
            if (x + width > Screen.width) x = marker.xMin - width - gap;
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width));
            float y = Mathf.Clamp(marker.yMin, 0f, Mathf.Max(0f, Screen.height - height));

            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            for (int i = 0; i < lineCount; i++)
                GUI.Label(new Rect(x + 10f, y + 6f + i * lineHeight, width - 20f, lineHeight), text.GetLine(mode, i));
        }

        static RectTransform ResolveRectTransform(object itemView)
        {
            if (itemView == null) return null;
            try
            {
                Component component = itemView as Component;
                if (component != null) return component.transform as RectTransform;
                GameObject gameObject = itemView as GameObject;
                if (gameObject != null) return gameObject.transform as RectTransform;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
                PropertyInfo property = itemView.GetType().GetProperty("transform", flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(itemView, null) as RectTransform;
                FieldInfo field = itemView.GetType().GetField("transform", flags);
                return field == null ? null : field.GetValue(itemView) as RectTransform;
            }
            catch { return null; }
        }

        static Camera ResolveCamera(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

    }
}

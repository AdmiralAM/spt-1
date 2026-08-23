using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverOverlaySink : IItemHoverViewSink, IItemHoverAnchorSink
    {
        readonly Vector3[] worldCorners = new Vector3[4];
        ItemHoverText current = ItemHoverText.Empty;
        RectTransform anchor;
        GUIStyle markerStyle;
        bool drawingDisabled;

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
                    GUI.color = MarkerColor(marker.Kind);
                    GUI.DrawTexture(markerRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Box(markerRect, GUIContent.none);
                    GUI.Label(markerRect, marker.Glyph, MarkerStyle);

                    Vector2 mouse = Event.current == null
                        ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)
                        : Event.current.mousePosition;
                    if (markerRect.Contains(mouse)) DrawDetails(markerRect, text);
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

            float size = Mathf.Clamp(Mathf.Min(itemWidth, itemHeight) * 0.32f, 16f, 22f);
            marker = new Rect(right - size - 3f, top + 3f, size, size);
            return marker.xMax > 0f && marker.yMax > 0f && marker.xMin < Screen.width && marker.yMin < Screen.height;
        }

        static void DrawDetails(Rect marker, ItemHoverText text)
        {
            const float width = 300f;
            const float height = 66f;
            const float gap = 8f;
            float x = marker.xMax + gap;
            if (x + width > Screen.width) x = marker.xMin - width - gap;
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - width));
            float y = Mathf.Clamp(marker.yMin, 0f, Mathf.Max(0f, Screen.height - height));

            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 18f), text.Primary);
            GUI.Label(new Rect(x + 10f, y + 24f, width - 20f, 18f), text.Secondary);
            GUI.Label(new Rect(x + 10f, y + 42f, width - 20f, 18f), text.Status);
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

        static Color MarkerColor(ItemMarkerKind kind)
        {
            switch (kind)
            {
                case ItemMarkerKind.SafeToSell: return new Color(0.18f, 0.62f, 0.28f, 0.96f);
                case ItemMarkerKind.Keep: return new Color(0.86f, 0.48f, 0.10f, 0.96f);
                case ItemMarkerKind.Unavailable: return new Color(0.72f, 0.18f, 0.18f, 0.96f);
                case ItemMarkerKind.Loading: return new Color(0.38f, 0.43f, 0.48f, 0.96f);
                default: return new Color(0.25f, 0.45f, 0.62f, 0.96f);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverOverlaySink : IItemHoverViewSink, IItemHoverAnchorSink, IItemViewRegistrySink
    {
        readonly Vector3[] worldCorners = new Vector3[4];
        readonly Dictionary<object, TrackedItemView> trackedViews = new Dictionary<object, TrackedItemView>(ReferenceComparer.Instance);
        readonly List<object> staleViews = new List<object>();
        readonly ItemIntelligenceUiSettings settings;
        readonly ItemPresentationStore store;
        readonly ItemHoverTextCache textCache;
        readonly Func<string, ItemHoverText> fallbackFactory;
        ItemHoverText current = ItemHoverText.Empty;
        object hoveredView;
        ItemPresentationIndex renderedIndex;
        GUIStyle markerStyle;
        GUIStyle markerShadowStyle;
        int invalidationVersion;
        int renderedInvalidation = -1;
        bool drawingDisabled;

        public ItemHoverOverlaySink(
            ItemIntelligenceUiSettings settings,
            ItemPresentationStore store,
            ItemHoverTextCache textCache,
            Func<string, ItemHoverText> fallbackFactory)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.textCache = textCache ?? throw new ArgumentNullException(nameof(textCache));
            this.fallbackFactory = fallbackFactory;
        }

        public ItemHoverText Current => Volatile.Read(ref current);
        internal int TrackedViewCount => trackedViews.Count;

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
            Interlocked.Exchange(ref hoveredView, itemView);
        }

        public void ClearAnchor()
        {
            Interlocked.Exchange(ref hoveredView, null);
        }

        public void RegisterView(object itemView, string templateId)
        {
            string normalized = RequirementContribution.NormalizeId(templateId);
            int stackCount = EftItemTemplateIdResolver.ResolveStackCount(itemView);
            RectTransform target = ResolveRectTransform(itemView);
            if (itemView == null || normalized.Length == 0 || target == null) return;

            TrackedItemView existing;
            if (trackedViews.TryGetValue(itemView, out existing))
            {
                if (existing.TemplateId == normalized && existing.StackCount == stackCount && object.ReferenceEquals(existing.Anchor, target)) return;
                existing.TemplateId = normalized;
                existing.Anchor = target;
                existing.StackCount = stackCount;
                existing.Text = ResolveText(normalized, stackCount, store.Current);
                return;
            }

            trackedViews[itemView] = new TrackedItemView(target, normalized, stackCount, ResolveText(normalized, stackCount, store.Current));
        }

        public void UnregisterView(object itemView)
        {
            if (itemView == null) return;
            trackedViews.Remove(itemView);
            if (object.ReferenceEquals(Volatile.Read(ref hoveredView), itemView))
            {
                ClearAnchor();
                Clear();
            }
        }

        public void ClearViews()
        {
            trackedViews.Clear();
            staleViews.Clear();
            renderedIndex = null;
            ClearAnchor();
            Clear();
        }

        public void Invalidate()
        {
            Interlocked.Increment(ref invalidationVersion);
        }

        public void Draw()
        {
            if (drawingDisabled) return;
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            RefreshTrackedTextIfNeeded();
            if (trackedViews.Count == 0) return;

            try
            {
                int previousDepth = GUI.depth;
                Color previousColor = GUI.color;
                try
                {
                    GUI.depth = -1000;
                    Vector2 mouse = Event.current == null
                        ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)
                        : Event.current.mousePosition;
                    object activeView = Volatile.Read(ref hoveredView);

                    foreach (KeyValuePair<object, TrackedItemView> pair in trackedViews)
                    {
                        TrackedItemView tracked = pair.Value;
                        try
                        {
                            if (!IsAlive(tracked.Anchor))
                            {
                                staleViews.Add(pair.Key);
                                continue;
                            }
                            if (!tracked.Anchor.gameObject.activeInHierarchy) continue;

                            Rect markerRect;
                            if (!TryGetMarkerRect(tracked.Anchor, out markerRect)) continue;
                            ItemMarkerPresentation marker = ItemMarkerPresentation.From(tracked.Text);
                            if (!marker.IsVisible) continue;

                            DrawMarker(markerRect, marker);
                            if (object.ReferenceEquals(pair.Key, activeView) && markerRect.Contains(mouse))
                                DrawDetails(markerRect, tracked.Text, settings.TooltipMode);
                        }
                        catch
                        {
                            staleViews.Add(pair.Key);
                        }
                    }
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.depth = previousDepth;
                }

                RemoveStaleViews();
            }
            catch
            {
                drawingDisabled = true;
                ClearViews();
            }
        }

        void RefreshTrackedTextIfNeeded()
        {
            ItemPresentationIndex index = store.Current;
            int version = Volatile.Read(ref invalidationVersion);
            if (object.ReferenceEquals(index, renderedIndex) && version == renderedInvalidation) return;

            foreach (TrackedItemView tracked in trackedViews.Values)
                tracked.Text = ResolveText(tracked.TemplateId, tracked.StackCount, index);

            renderedIndex = index;
            renderedInvalidation = version;
        }

        ItemHoverText ResolveText(string templateId, int stackCount, ItemPresentationIndex index)
        {
            ItemPresentationIndex safeIndex = index ?? ItemPresentationIndex.Empty;
            ItemPresentationState presentation = safeIndex.Get(templateId);
            if (presentation != ItemPresentationState.Empty)
            {
                if (presentation.Price != null && stackCount > 1)
                    presentation = new ItemPresentationState(
                        presentation.TemplateId,
                        presentation.Requirement,
                        ItemPriceEvaluator.WithStackCount(presentation.Price, stackCount));
                return textCache.Get(new ItemHoverState(presentation), safeIndex) ?? ItemHoverText.Empty;
            }

            if (fallbackFactory == null) return ItemHoverText.Empty;
            try { return fallbackFactory(templateId) ?? ItemHoverText.Empty; }
            catch { return ItemHoverText.Empty; }
        }

        void DrawMarker(Rect markerRect, ItemMarkerPresentation marker)
        {
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(settings.MarkerSize * 0.78f), 10, 25);
            MarkerStyle.fontSize = fontSize;
            MarkerShadowStyle.fontSize = fontSize;

            Color markerColor = settings.GetColor(marker.Kind);
            markerColor.a = settings.MarkerOpacity;
            MarkerStyle.normal.textColor = markerColor;
            MarkerShadowStyle.normal.textColor = new Color(0f, 0f, 0f, markerColor.a * 0.82f);
            GUI.color = Color.white;
            GUI.Label(new Rect(markerRect.x + 1f, markerRect.y + 1f, markerRect.width, markerRect.height), marker.Glyph, MarkerShadowStyle);
            GUI.Label(markerRect, marker.Glyph, MarkerStyle);
        }

        GUIStyle MarkerStyle
        {
            get
            {
                if (markerStyle != null) return markerStyle;
                markerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                return markerStyle;
            }
        }

        GUIStyle MarkerShadowStyle
        {
            get
            {
                if (markerShadowStyle != null) return markerShadowStyle;
                markerShadowStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                return markerShadowStyle;
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
            marker = new Rect(left + settings.MarkerOffsetX, top + settings.MarkerOffsetY, size, size);
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

        static bool IsAlive(RectTransform target)
        {
            try { return target != null && target.gameObject != null; }
            catch { return false; }
        }

        static Camera ResolveCamera(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        void RemoveStaleViews()
        {
            if (staleViews.Count == 0) return;
            for (int i = 0; i < staleViews.Count; i++) UnregisterView(staleViews[i]);
            staleViews.Clear();
        }

        sealed class TrackedItemView
        {
            public TrackedItemView(RectTransform anchor, string templateId, int stackCount, ItemHoverText text)
            {
                Anchor = anchor;
                TemplateId = templateId;
                StackCount = Math.Max(1, stackCount);
                Text = text ?? ItemHoverText.Empty;
            }

            public RectTransform Anchor { get; set; }
            public string TemplateId { get; set; }
            public int StackCount { get; set; }
            public ItemHoverText Text { get; set; }
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => object.ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}

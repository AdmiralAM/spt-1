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
        readonly Dictionary<object, TrackedItemView> trackedViews = new Dictionary<object, TrackedItemView>(ReferenceComparer.Instance);
        readonly List<object> staleViews = new List<object>();
        readonly ItemIntelligenceUiSettings settings;
        readonly ItemPresentationStore store;
        readonly ItemHoverTextCache textCache;
        readonly Func<string, ItemHoverText> fallbackFactory;
        ItemHoverText current = ItemHoverText.Empty;
        object hoveredView;
        ItemPresentationIndex renderedIndex;
        int invalidationVersion;
        int renderedInvalidation = -1;
        bool tooltipDrawingDisabled;

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

            TrackedItemView tracked;
            if (!trackedViews.TryGetValue(itemView, out tracked))
            {
                tracked = new TrackedItemView(target, normalized, stackCount, AttachedMarkerView.TryCreate(target));
                trackedViews[itemView] = tracked;
            }
            else
            {
                tracked.TemplateId = normalized;
                tracked.StackCount = Math.Max(1, stackCount);
                if (!object.ReferenceEquals(tracked.Anchor, target) || tracked.Marker == null) tracked.ReplaceAnchor(target);
            }

            tracked.Text = ResolveText(normalized, tracked.StackCount, store.Current);
            tracked.Apply(settings);
        }

        public void UnregisterView(object itemView)
        {
            if (itemView == null) return;
            TrackedItemView tracked;
            if (trackedViews.TryGetValue(itemView, out tracked)) tracked.Dispose();
            trackedViews.Remove(itemView);
            if (object.ReferenceEquals(Volatile.Read(ref hoveredView), itemView))
            {
                ClearAnchor();
                Clear();
            }
        }

        public void ClearViews()
        {
            foreach (TrackedItemView tracked in trackedViews.Values) tracked.Dispose();
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
            if (tooltipDrawingDisabled) return;
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            RefreshTrackedViewsIfNeeded();

            object activeView = Volatile.Read(ref hoveredView);
            if (activeView == null) return;
            TrackedItemView tracked;
            if (!trackedViews.TryGetValue(activeView, out tracked) || tracked.Marker == null) return;

            try
            {
                Rect markerRect;
                if (!tracked.Marker.TryGetScreenRect(out markerRect)) return;
                Vector2 mouse = Event.current == null
                    ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)
                    : Event.current.mousePosition;
                if (!markerRect.Contains(mouse)) return;

                int previousDepth = GUI.depth;
                Color previousColor = GUI.color;
                try
                {
                    GUI.depth = -1000;
                    GUI.color = Color.white;
                    PolishedTooltipRenderer.Draw(markerRect, tracked.Text, settings);
                }
                finally
                {
                    GUI.color = previousColor;
                    GUI.depth = previousDepth;
                }
            }
            catch
            {
                tooltipDrawingDisabled = true;
            }
        }

        void RefreshTrackedViewsIfNeeded()
        {
            ItemPresentationIndex index = store.Current;
            int version = Volatile.Read(ref invalidationVersion);
            if (object.ReferenceEquals(index, renderedIndex) && version == renderedInvalidation) return;

            foreach (KeyValuePair<object, TrackedItemView> pair in trackedViews)
            {
                TrackedItemView tracked = pair.Value;
                if (!IsAlive(tracked.Anchor))
                {
                    staleViews.Add(pair.Key);
                    continue;
                }
                tracked.Text = ResolveText(tracked.TemplateId, tracked.StackCount, index);
                tracked.Apply(settings);
            }
            RemoveStaleViews();
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

        void RemoveStaleViews()
        {
            if (staleViews.Count == 0) return;
            for (int i = 0; i < staleViews.Count; i++) UnregisterView(staleViews[i]);
            staleViews.Clear();
        }

        sealed class TrackedItemView : IDisposable
        {
            public TrackedItemView(RectTransform anchor, string templateId, int stackCount, AttachedMarkerView marker)
            {
                Anchor = anchor;
                TemplateId = templateId;
                StackCount = Math.Max(1, stackCount);
                Marker = marker;
                Text = ItemHoverText.Empty;
            }

            public RectTransform Anchor { get; private set; }
            public string TemplateId { get; set; }
            public int StackCount { get; set; }
            public ItemHoverText Text { get; set; }
            public AttachedMarkerView Marker { get; private set; }

            public void ReplaceAnchor(RectTransform anchor)
            {
                if (Marker != null) Marker.Dispose();
                Anchor = anchor;
                Marker = AttachedMarkerView.TryCreate(anchor);
            }

            public void Apply(ItemIntelligenceUiSettings settings)
            {
                if (Marker != null) Marker.Apply(ItemMarkerPresentation.From(Text), settings);
            }

            public void Dispose()
            {
                if (Marker != null) Marker.Dispose();
                Marker = null;
            }
        }

        sealed class AttachedMarkerView : IDisposable
        {
            static readonly Type textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", false);
            static readonly Type outlineType = Type.GetType("UnityEngine.UI.Outline, UnityEngine.UI", false);
            readonly Vector3[] worldCorners = new Vector3[4];
            readonly GameObject markerObject;
            readonly RectTransform rect;
            readonly Component text;
            readonly Component outline;

            AttachedMarkerView(GameObject markerObject, RectTransform rect, Component text, Component outline)
            {
                this.markerObject = markerObject;
                this.rect = rect;
                this.text = text;
                this.outline = outline;
            }

            public static AttachedMarkerView TryCreate(RectTransform anchor)
            {
                if (anchor == null || textType == null) return null;
                try
                {
                    GameObject markerObject = new GameObject("SPTItemIntelligenceMarker", typeof(RectTransform));
                    markerObject.layer = anchor.gameObject.layer;
                    RectTransform rect = markerObject.transform as RectTransform;
                    rect.SetParent(anchor, false);
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;

                    Component text = markerObject.AddComponent(textType) as Component;
                    Component outline = outlineType == null ? null : markerObject.AddComponent(outlineType) as Component;
                    Set(text, "text", "ⓘ");
                    Set(text, "fontStyle", FontStyle.Bold);
                    Set(text, "alignment", Enum.Parse(PropertyType(text, "alignment"), "MiddleCenter"));
                    Set(text, "raycastTarget", false);
                    Set(text, "supportRichText", false);
                    Set(text, "horizontalOverflow", Enum.Parse(PropertyType(text, "horizontalOverflow"), "Overflow"));
                    Set(text, "verticalOverflow", Enum.Parse(PropertyType(text, "verticalOverflow"), "Overflow"));
                    Set(text, "font", BuiltinFont());
                    if (outline != null)
                    {
                        Set(outline, "effectColor", new Color(0f, 0f, 0f, 0.95f));
                        Set(outline, "useGraphicAlpha", true);
                    }
                    rect.SetAsLastSibling();
                    return new AttachedMarkerView(markerObject, rect, text, outline);
                }
                catch { return null; }
            }

            public void Apply(ItemMarkerPresentation presentation, ItemIntelligenceUiSettings settings)
            {
                if (markerObject == null || presentation == null || settings == null) return;
                bool visible = presentation.IsVisible;
                if (markerObject.activeSelf != visible) markerObject.SetActive(visible);
                if (!visible) return;

                float size = settings.MarkerSize;
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(settings.MarkerOffsetX, -settings.MarkerOffsetY);
                Color color = settings.GetColor(presentation.Kind);
                color.a = settings.MarkerOpacity;
                Set(text, "text", presentation.Glyph);
                Set(text, "fontSize", Mathf.Clamp(Mathf.RoundToInt(size * 0.88f), 9, 25));
                Set(text, "color", color);
                if (outline != null)
                {
                    float thickness = Mathf.Clamp(size * 0.085f, 1f, 2f);
                    Set(outline, "effectDistance", new Vector2(thickness, -thickness));
                }
                rect.SetAsLastSibling();
            }

            public bool TryGetScreenRect(out Rect result)
            {
                result = default(Rect);
                if (rect == null || markerObject == null || !markerObject.activeInHierarchy) return false;
                rect.GetWorldCorners(worldCorners);
                Canvas canvas = rect.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[2]);
                float left = Mathf.Min(bottomLeft.x, topRight.x);
                float right = Mathf.Max(bottomLeft.x, topRight.x);
                float top = Screen.height - Mathf.Max(bottomLeft.y, topRight.y);
                float bottom = Screen.height - Mathf.Min(bottomLeft.y, topRight.y);
                result = new Rect(left, top, right - left, bottom - top);
                return result.width > 0f && result.height > 0f && result.xMax > 0f && result.yMax > 0f && result.xMin < Screen.width && result.yMin < Screen.height;
            }

            public void Dispose()
            {
                if (markerObject != null) UnityEngine.Object.Destroy(markerObject);
            }

            static Font BuiltinFont()
            {
                try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch
                {
                    try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { return null; }
                }
            }

            static Type PropertyType(object target, string name)
            {
                PropertyInfo property = target == null ? null : target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                return property == null ? typeof(int) : property.PropertyType;
            }

            static void Set(object target, string name, object value)
            {
                if (target == null || value == null) return;
                try
                {
                    PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (property != null && property.CanWrite) property.SetValue(target, value, null);
                }
                catch { }
            }
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => object.ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}

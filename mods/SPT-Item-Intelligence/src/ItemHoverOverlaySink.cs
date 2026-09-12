using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverOverlaySink : IItemHoverViewSink, IItemHoverAnchorSink, IItemViewRegistrySink, IInventorySessionLifecycle
    {
        readonly Dictionary<object, TrackedItemView> trackedViews = new Dictionary<object, TrackedItemView>(ReferenceComparer.Instance);
        readonly List<object> staleViews = new List<object>();
        readonly ItemIntelligenceUiSettings settings;
        readonly ItemPresentationStore store;
        readonly ItemHoverTextCache textCache;
        readonly Func<string, ItemHoverText> fallbackFactory;
        ItemHoverText current = ItemHoverText.Empty;
        object hoveredView;
        object pinnedView;
        Rect pinnedCardRect;
        ItemPresentationIndex renderedIndex;
        int invalidationVersion;
        int renderedInvalidation = -1;
        bool tooltipDrawingDisabled;
        public event Action InventoryOpened;
        public void OnViewInitialized()
        {
            if (settings.Modules.TrackViews && trackedViews.Count == 0) InventoryOpened?.Invoke();
        }

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
            if (!settings.Modules.TrackViews) return;
            string normalized = RequirementContribution.NormalizeId(templateId);
            int stackCount = EftItemTemplateIdResolver.ResolveStackCount(itemView);
            RectTransform target = ResolveRectTransform(itemView);
            if (itemView == null || normalized.Length == 0 || target == null) return;
            GameLanguageDetector.ObserveNativeUi(target);

            TrackedItemView tracked;
            if (!trackedViews.TryGetValue(itemView, out tracked))
            {
                tracked = new TrackedItemView(target, normalized, stackCount, null);
                trackedViews[itemView] = tracked;
            }
            else
            {
                tracked.TemplateId = normalized;
                tracked.StackCount = Math.Max(1, stackCount);
                if (!object.ReferenceEquals(tracked.Anchor, target)) tracked.ReplaceAnchor(target);
            }

            tracked.Text = ResolveText(normalized, tracked.StackCount, store.Current);
            tracked.BackgroundColor = store.Current.Get(normalized).Price?.BackgroundColor;
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
            if (object.ReferenceEquals(pinnedView, itemView)) ClearPinned();
        }

        public void ClearViews()
        {
            foreach (TrackedItemView tracked in trackedViews.Values) tracked.Dispose();
            trackedViews.Clear();
            staleViews.Clear();
            renderedIndex = null;
            ClearAnchor();
            ClearPinned();
            Clear();
        }

        public void Invalidate()
        {
            Interlocked.Increment(ref invalidationVersion);
        }

        public void Draw()
        {
            if (!settings.Modules.TrackViews) return;
            Event guiEvent = Event.current;
            bool repaint = guiEvent == null || guiEvent.type == EventType.Repaint;
            bool click = guiEvent != null && guiEvent.type == EventType.MouseDown && guiEvent.button == 0;
            if (!repaint && !click) return;
            if (repaint) RefreshTrackedViewsIfNeeded();
            if (tooltipDrawingDisabled || !settings.Modules.Tooltips) return;

            object activeView = pinnedView ?? Volatile.Read(ref hoveredView);
            if (activeView == null) return;
            TrackedItemView tracked;
            if (!trackedViews.TryGetValue(activeView, out tracked)) return;

            try
            {
                Rect markerRect;
                if (!tracked.TryGetTooltipHotspot(out markerRect)) return;
                Vector2 mouse = guiEvent == null
                    ? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)
                    : guiEvent.mousePosition;

                if (click)
                {
                    object hovered = Volatile.Read(ref hoveredView);
                    if (hovered != null && trackedViews.TryGetValue(hovered, out TrackedItemView hoveredTracked) &&
                        hoveredTracked.TryGetTooltipHotspot(out Rect hoveredMarker) && hoveredMarker.Contains(mouse))
                    {
                        if (object.ReferenceEquals(pinnedView, hovered)) ClearPinned();
                        else { pinnedView = hovered; pinnedCardRect = default(Rect); }
                        guiEvent.Use();
                    }
                    else if (pinnedView != null && !pinnedCardRect.Contains(mouse))
                    {
                        ClearPinned();
                        guiEvent.Use();
                    }
                    return;
                }

                bool pinned = object.ReferenceEquals(pinnedView, activeView);
                if (!pinned && !markerRect.Contains(mouse)) return;

                int previousDepth = GUI.depth;
                Color previousColor = GUI.color;
                try
                {
                    GUI.depth = -1000;
                    GUI.color = Color.white;
                    Rect card = PolishedTooltipRenderer.Draw(markerRect, tracked.Text, settings,
                        pinned ? ItemTooltipMode.Full : (ItemTooltipMode?)null);
                    if (pinned) pinnedCardRect = card;
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

        void ClearPinned()
        {
            pinnedView = null;
            pinnedCardRect = default(Rect);
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
                tracked.BackgroundColor = index.Get(tracked.TemplateId).Price?.BackgroundColor;
                tracked.Apply(settings);
            }
            RemoveStaleViews();
            renderedIndex = index;
            renderedInvalidation = version;
        }

        ItemHoverText ResolveText(string templateId, int stackCount, ItemPresentationIndex index)
        {
            if (!settings.Modules.Markers && !settings.Modules.Tooltips) return ItemHoverText.Empty;
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
            readonly Vector3[] corners = new Vector3[4];
            BackgroundView background;
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
            public string BackgroundColor { get; set; }
            public AttachedMarkerView Marker { get; private set; }

            public void ReplaceAnchor(RectTransform anchor)
            {
                if (Marker != null) Marker.Dispose();
                if (background != null) background.Dispose();
                background = null;
                Anchor = anchor;
                Marker = null;
            }

            public void Apply(ItemIntelligenceUiSettings settings)
            {
                if (settings.Modules.Backgrounds && !string.IsNullOrEmpty(BackgroundColor))
                {
                    if (background == null) background = BackgroundView.Create(Anchor);
                    if (background != null) background.Apply(BackgroundColor);
                }
                else { if (background != null) background.Dispose(); background = null; }
                ItemMarkerPresentation state = ItemMarkerPresentation.From(Text, contextual: true);
                if (!settings.Modules.Markers || !state.IsVisible)
                {
                    if (Marker != null) Marker.Dispose();
                    Marker = null;
                    return;
                }
                if (Marker == null) Marker = AttachedMarkerView.TryCreate(Anchor);
                if (Marker != null) Marker.Apply(state, Text, settings);
            }

            public bool TryGetScreenRect(out Rect result)
            {
                result = default(Rect);
                if (Anchor == null || !Anchor.gameObject.activeInHierarchy) return false;
                Anchor.GetWorldCorners(corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                result = new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
                return result.width > 0 && result.height > 0;
            }

            public bool TryGetTooltipHotspot(out Rect result)
            {
                if (Marker != null && Marker.TryGetScreenRect(out result)) return true;
                if (!TryGetScreenRect(out Rect cell))
                {
                    result = default(Rect);
                    return false;
                }

                // Items without our marker still have one predictable, unobtrusive target:
                // the caption strip at the top of the native item cell.
                float height = Mathf.Clamp(cell.height * 0.24f, 16f, 24f);
                result = new Rect(cell.xMin, cell.yMin, cell.width, height);
                return true;
            }

            public void Dispose()
            {
                if (background != null) background.Dispose();
                background = null;
                if (Marker != null) Marker.Dispose();
                Marker = null;
            }
        }

        sealed class BackgroundView : IDisposable
        {
            static readonly Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
            static readonly PropertyInfo colorProperty = imageType?.GetProperty("color");
            readonly Component image;
            readonly Color originalColor;
            BackgroundView(Component image, Color originalColor) { this.image = image; this.originalColor = originalColor; }
            public static BackgroundView Create(RectTransform anchor)
            {
                if (imageType == null || colorProperty == null || anchor == null) return null;
                Component best = null;
                int bestScore = int.MinValue;
                foreach (Component candidate in anchor.GetComponentsInChildren(imageType, true))
                {
                    if (candidate == null || candidate.gameObject == null) continue;
                    string name = candidate.gameObject.name ?? string.Empty;
                    if (name.StartsWith("SPTItemIntelligence", StringComparison.Ordinal)) continue;
                    int score = 0;
                    if (name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0) score += 120;
                    if (name.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0) score += 60;
                    if (name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) score -= 100;
                    RectTransform rect = candidate.transform as RectTransform;
                    if (rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one) score += 30;
                    if (rect != null && object.ReferenceEquals(rect.parent, anchor)) score += 20;
                    if (score > bestScore) { bestScore = score; best = candidate; }
                }
                if (best == null || bestScore < 50) return null;
                object original = colorProperty.GetValue(best, null);
                return original is Color ? new BackgroundView(best, (Color)original) : null;
            }
            public void Apply(string hex)
            {
                Color color;
                if (!ColorUtility.TryParseHtmlString(hex, out color)) return;
                // Match the accepted Item Valuation HEX palette exactly. Alpha blending here
                // changes every perceived tier and was the source of the runtime mismatch.
                color.a = 1f;
                object current = colorProperty.GetValue(image, null);
                if (current is Color && SameColor((Color)current, color)) return;
                colorProperty.SetValue(image, color, null);
            }
            static bool SameColor(Color left, Color right) =>
                Mathf.Abs(left.r - right.r) < .002f &&
                Mathf.Abs(left.g - right.g) < .002f &&
                Mathf.Abs(left.b - right.b) < .002f &&
                Mathf.Abs(left.a - right.a) < .002f;
            public void Dispose()
            {
                try { if (image != null) colorProperty.SetValue(image, originalColor, null); }
                catch { }
            }
        }

        sealed class AttachedMarkerView : IDisposable
        {
            static Sprite checkmarkSprite;
            static Sprite circleSprite;
            static Sprite ringSprite;
            static readonly Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
            static readonly Type outlineType = Type.GetType("UnityEngine.UI.Outline, UnityEngine.UI", false);
            static readonly object haloSpriteSync = new object();
            static Sprite haloSprite;
            readonly Vector3[] worldCorners = new Vector3[4];
            readonly GameObject markerObject;
            readonly RectTransform rect;
            readonly Component backgroundImage;
            readonly Component ringImage;
            readonly Component glyphImage;
            readonly GameObject haloObject;
            readonly RectTransform haloRect;
            readonly Component haloImage;
            readonly Component outline;

            AttachedMarkerView(
                GameObject markerObject,
                RectTransform rect,
                Component backgroundImage,
                Component ringImage,
                Component glyphImage,
                GameObject haloObject,
                RectTransform haloRect,
                Component haloImage,
                Component outline)
            {
                this.markerObject = markerObject;
                this.rect = rect;
                this.backgroundImage = backgroundImage;
                this.ringImage = ringImage;
                this.glyphImage = glyphImage;
                this.haloObject = haloObject;
                this.haloRect = haloRect;
                this.haloImage = haloImage;
                this.outline = outline;
            }

            public static AttachedMarkerView TryCreate(RectTransform anchor)
            {
                if (anchor == null || imageType == null) return null;
                try
                {
                    GameObject haloObject = null;
                    RectTransform haloRect = null;
                    Component haloImage = null;
                    Sprite sprite = HaloSprite();
                    if (imageType != null && sprite != null)
                    {
                        haloObject = new GameObject("SPTItemIntelligenceHalo", typeof(RectTransform));
                        haloObject.layer = anchor.gameObject.layer;
                        haloRect = haloObject.transform as RectTransform;
                        haloRect.SetParent(anchor, false);
                        haloRect.anchorMin = new Vector2(0f, 1f);
                        haloRect.anchorMax = new Vector2(0f, 1f);
                        haloRect.pivot = new Vector2(0.5f, 0.5f);
                        haloRect.localScale = Vector3.one;
                        haloRect.localRotation = Quaternion.identity;
                        haloImage = haloObject.AddComponent(imageType) as Component;
                        Set(haloImage, "sprite", sprite);
                        Set(haloImage, "raycastTarget", false);
                        Set(haloImage, "preserveAspect", true);
                        haloObject.SetActive(false);
                    }

                    GameObject markerObject = new GameObject("SPTItemIntelligenceMarker", typeof(RectTransform));
                    markerObject.layer = anchor.gameObject.layer;
                    RectTransform rect = markerObject.transform as RectTransform;
                    rect.SetParent(anchor, false);
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;

                    Component backgroundImage = markerObject.AddComponent(imageType) as Component;
                    Component outline = null;
                    Set(backgroundImage, "sprite", CircleSprite());
                    Set(backgroundImage, "preserveAspect", true);
                    Set(backgroundImage, "raycastTarget", false);
                    Component ringImage = CreateMarkerLayer(markerObject, "SPTItemIntelligenceRing", RingSprite(), 1f);
                    Component glyphImage = CreateMarkerLayer(markerObject, "SPTItemIntelligenceCheck", CheckmarkSprite(), .76f);
                    if (haloRect != null) haloRect.SetAsLastSibling();
                    rect.SetAsLastSibling();
                    return new AttachedMarkerView(markerObject, rect, backgroundImage, ringImage, glyphImage, haloObject, haloRect, haloImage, outline);
                }
                catch { return null; }
            }

            public void Apply(ItemMarkerPresentation presentation, ItemHoverText hoverText, ItemIntelligenceUiSettings settings)
            {
                if (markerObject == null || presentation == null || settings == null) return;
                bool visible = presentation.IsVisible;
                if (markerObject.activeSelf != visible) markerObject.SetActive(visible);
                if (!visible)
                {
                    if (haloObject != null && haloObject.activeSelf) haloObject.SetActive(false);
                    return;
                }

                float size = settings.MarkerSize;
                bool right = settings.MarkerSide == ItemMarkerSide.Right;
                Vector2 anchor = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
                Vector2 position = new Vector2(right ? -settings.MarkerOffsetX : settings.MarkerOffsetX, -settings.MarkerOffsetY);
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = anchor;
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = position;

                Color sourceColor = settings.GetColor(presentation.Kind);
                sourceColor.a = settings.MarkerOpacity;
                Set(ringImage, "color", sourceColor);
                Color statusColor = ResolveStatusColor(hoverText, settings);
                statusColor.a = settings.MarkerOpacity;
                Set(glyphImage, "color", statusColor);
                Color fillColor = settings.MarkerBackgroundColor;
                fillColor.a = settings.MarkerBackgroundOpacity;
                Set(backgroundImage, "color", fillColor);

                bool haloEnabled = haloObject != null && haloRect != null && haloImage != null && settings.MarkerHalo && settings.MarkerHaloStrength > 0f;
                if (haloObject != null && haloObject.activeSelf != haloEnabled) haloObject.SetActive(haloEnabled);
                if (haloEnabled)
                {
                    haloRect.anchorMin = anchor;
                    haloRect.anchorMax = anchor;
                    haloRect.pivot = new Vector2(0.5f, 0.5f);
                    float haloSize = size * 1.70f;
                    haloRect.sizeDelta = new Vector2(haloSize, haloSize);
                    haloRect.anchoredPosition = position + new Vector2(right ? -size * 0.5f : size * 0.5f, -size * 0.5f);
                    Color haloColor = sourceColor;
                    haloColor.a = settings.MarkerHaloStrength * settings.MarkerOpacity;
                    Set(haloImage, "color", haloColor);
                    haloRect.SetAsLastSibling();
                }

                if (outline != null)
                {
                    float thickness = Mathf.Clamp(size * 0.075f, 0.9f, 1.8f);
                    Set(outline, "effectDistance", new Vector2(thickness, -thickness));
                }
                rect.SetAsLastSibling();
            }

            static Color ResolveStatusColor(ItemHoverText text, ItemIntelligenceUiSettings settings)
            {
                if (text == null || text.Allocation == null) return settings.GetColor(ItemMarkerKind.Default);
                if (text.Allocation.Coverage == RequirementCoverage.Enough) return settings.CompleteColor;
                if (text.Allocation.Coverage == RequirementCoverage.NeedMore)
                    return text.Allocation.KeepOwned > 0 ? settings.PartialColor : settings.MissingColor;
                return settings.GetColor(ItemMarkerKind.Default);
            }

            static Component CreateMarkerLayer(GameObject parent, string name, Sprite sprite, float scale)
            {
                GameObject layer = new GameObject(name, typeof(RectTransform));
                layer.layer = parent.layer;
                RectTransform layerRect = layer.transform as RectTransform;
                layerRect.SetParent(parent.transform, false);
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.pivot = new Vector2(.5f, .5f);
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                layerRect.sizeDelta = Vector2.zero;
                layerRect.localScale = new Vector3(scale, scale, 1f);
                Component image = layer.AddComponent(imageType) as Component;
                Set(image, "sprite", sprite);
                Set(image, "preserveAspect", true);
                Set(image, "raycastTarget", false);
                return image;
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
                if (haloObject != null) UnityEngine.Object.Destroy(haloObject);
                if (markerObject != null) UnityEngine.Object.Destroy(markerObject);
            }

            static Sprite HaloSprite()
            {
                if (haloSprite != null) return haloSprite;
                lock (haloSpriteSync)
                {
                    if (haloSprite != null) return haloSprite;
                    try
                    {
                        const int textureSize = 32;
                        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                        texture.name = "SPTItemIntelligenceHaloTexture";
                        texture.hideFlags = HideFlags.HideAndDontSave;
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        Color32[] pixels = new Color32[textureSize * textureSize];
                        for (int y = 0; y < textureSize; y++)
                        {
                            for (int x = 0; x < textureSize; x++)
                            {
                                float nx = ((x + 0.5f) / textureSize) * 2f - 1f;
                                float ny = ((y + 0.5f) / textureSize) * 2f - 1f;
                                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                                float alpha = Mathf.Clamp01(1f - distance);
                                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                                pixels[(y * textureSize) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                            }
                        }
                        texture.SetPixels32(pixels);
                        texture.Apply(false, true);
                        haloSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
                        haloSprite.name = "SPTItemIntelligenceHaloSprite";
                        haloSprite.hideFlags = HideFlags.HideAndDontSave;
                    }
                    catch
                    {
                        haloSprite = null;
                    }
                }
                return haloSprite;
            }

            static Sprite CircleSprite()
            {
                if (circleSprite != null) return circleSprite;
                circleSprite = RadialSprite("ItemIntelligenceCircle", .49f, 0f);
                return circleSprite;
            }

            static Sprite RingSprite()
            {
                if (ringSprite != null) return ringSprite;
                ringSprite = RadialSprite("ItemIntelligenceRing", .49f, .36f);
                return ringSprite;
            }

            static Sprite RadialSprite(string name, float outer, float inner)
            {
                const int size = 64;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = name;
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + .5f) / size - .5f;
                    float ny = (y + .5f) / size - .5f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float outerAlpha = Mathf.Clamp01((outer - distance) * size);
                    float innerAlpha = inner <= 0f ? 1f : Mathf.Clamp01((distance - inner) * size);
                    byte alpha = (byte)Mathf.RoundToInt(outerAlpha * innerAlpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                Sprite result = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                result.name = name + "Sprite";
                result.hideFlags = HideFlags.HideAndDontSave;
                return result;
            }

            // Original two-stroke geometry. No external sprite, font glyph, asset, or copied geometry.
            static Sprite CheckmarkSprite()
            {
                if (checkmarkSprite != null) return checkmarkSprite;
                const int size = 64;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "ItemIntelligenceOriginalCheckmark";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + .5f) / size, (y + .5f) / size);
                    float check = Mathf.Min(SegmentDistance(p, new Vector2(.22f, .50f), new Vector2(.43f, .30f)),
                        SegmentDistance(p, new Vector2(.43f, .30f), new Vector2(.79f, .69f)));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01((.075f - check) * size) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                checkmarkSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
                checkmarkSprite.hideFlags = HideFlags.HideAndDontSave;
                return checkmarkSprite;
            }
            static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude));
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

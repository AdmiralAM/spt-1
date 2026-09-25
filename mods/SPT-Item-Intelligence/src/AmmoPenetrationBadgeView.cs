using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class AmmoPenetrationBadgeView : IDisposable
    {
        static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
        static readonly object SpriteSync = new object();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly GameObject badgeObject;
        readonly RectTransform rect;
        readonly Component image;

        AmmoPenetrationBadgeView(GameObject badgeObject, RectTransform rect, Component image)
        { this.badgeObject = badgeObject; this.rect = rect; this.image = image; }

        public static AmmoPenetrationBadgeView TryCreate(RectTransform anchor)
        {
            if (anchor == null || ImageType == null) return null;
            try
            {
                GameObject badgeObject = new GameObject("SPTItemIntelligenceAmmoPenetration", typeof(RectTransform));
                badgeObject.layer = anchor.gameObject.layer;
                RectTransform rect = badgeObject.transform as RectTransform;
                rect.SetParent(anchor, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchoredPosition = new Vector2(3f, 3f);
                Component image = badgeObject.AddComponent(ImageType) as Component;
                Set(image, "raycastTarget", false);
                Set(image, "preserveAspect", true);
                badgeObject.SetActive(false);
                rect.SetAsLastSibling();
                return new AmmoPenetrationBadgeView(badgeObject, rect, image);
            }
            catch { return null; }
        }

        public void Apply(string romanClass, RectTransform anchor)
        {
            Sprite sprite = GetSprite(romanClass);
            bool visible = sprite != null && anchor != null && anchor.gameObject.activeInHierarchy;
            if (badgeObject == null) return;
            if (visible)
            {
                Set(image, "sprite", sprite);
                float side = Mathf.Clamp(Mathf.Min(anchor.rect.width, anchor.rect.height) * 0.34f, 16f, 23f);
                rect.sizeDelta = new Vector2(side * 1.25f, side);
                rect.anchoredPosition = new Vector2(3f, 3f);
                rect.SetAsLastSibling();
            }
            if (badgeObject.activeSelf != visible) badgeObject.SetActive(visible);
        }

        static Sprite GetSprite(string romanClass)
        {
            if (string.IsNullOrEmpty(romanClass)) return null;
            lock (SpriteSync)
            {
                Sprite cached;
                if (Sprites.TryGetValue(romanClass, out cached)) return cached;
                int value;
                switch (romanClass)
                {
                    case "I": value = 1; break;
                    case "II": value = 2; break;
                    case "III": value = 3; break;
                    case "IV": value = 4; break;
                    case "V": value = 5; break;
                    case "VI": value = 6; break;
                    default: return null;
                }
                try
                {
                    const int width = 96, height = 72;
                    Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    texture.name = "SPTItemIntelligenceAmmoPenetration_" + romanClass;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    Color32[] pixels = new Color32[width * height];
                    DrawRoundedRect(pixels, width, height, 3f, 3f, 93f, 69f, 13f, new Color(0.02f, 0.025f, 0.03f, 0.97f));
                    DrawRoundedRect(pixels, width, height, 5f, 5f, 91f, 67f, 11f, new Color(0.12f, 0.16f, 0.19f, 0.96f));
                    DrawNumeral(pixels, width, height, value);
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    cached = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 96f);
                    cached.name = texture.name + "Sprite";
                    cached.hideFlags = HideFlags.HideAndDontSave;
                    Sprites[romanClass] = cached;
                    return cached;
                }
                catch { return null; }
            }
        }

        static void DrawNumeral(Color32[] pixels, int width, int height, int value)
        {
            List<Segment> strokes = new List<Segment>();
            if (value <= 3)
            {
                float unit = 8f, gap = 5f;
                float total = (value * unit) + ((value - 1) * gap);
                float start = (width - total) * 0.5f;
                for (int i = 0; i < value; i++) AddI(strokes, start + i * (unit + gap) + unit * 0.5f);
            }
            else if (value == 4)
            {
                AddI(strokes, 33f);
                AddV(strokes, 61f);
            }
            else if (value == 5) AddV(strokes, 48f);
            else
            {
                AddV(strokes, 36f);
                AddI(strokes, 69f);
            }
            for (int i = 0; i < strokes.Count; i++) DrawSegment(pixels, width, height, strokes[i], 8f, new Color(0.005f, 0.008f, 0.01f, 1f));
            for (int i = 0; i < strokes.Count; i++) DrawSegment(pixels, width, height, strokes[i], 4.8f, new Color(0.94f, 0.96f, 0.97f, 1f));
        }

        static void AddI(List<Segment> strokes, float x)
        {
            strokes.Add(new Segment(x, 20f, x, 51f));
            strokes.Add(new Segment(x - 5f, 50f, x + 5f, 50f));
            strokes.Add(new Segment(x - 5f, 21f, x + 5f, 21f));
        }

        static void AddV(List<Segment> strokes, float center)
        {
            strokes.Add(new Segment(center - 12f, 50f, center, 20f));
            strokes.Add(new Segment(center + 12f, 50f, center, 20f));
        }

        static void DrawSegment(Color32[] pixels, int width, int height, Segment segment, float strokeWidth, Color color)
        {
            float radius = strokeWidth * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(segment.X1, segment.X2) - radius - 1f));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(segment.X1, segment.X2) + radius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(segment.Y1, segment.Y2) - radius - 1f));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(segment.Y1, segment.Y2) + radius + 1f));
            float dx = segment.X2 - segment.X1, dy = segment.Y2 - segment.Y1;
            float lengthSquared = (dx * dx) + (dy * dy);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float t = lengthSquared <= 0f ? 0f : Mathf.Clamp01((((x + 0.5f - segment.X1) * dx) + ((y + 0.5f - segment.Y1) * dy)) / lengthSquared);
                float px = segment.X1 + t * dx, py = segment.Y1 + t * dy;
                float distance = Mathf.Sqrt(((x + 0.5f - px) * (x + 0.5f - px)) + ((y + 0.5f - py) * (y + 0.5f - py)));
                float alpha = 1f - Mathf.SmoothStep(radius - 0.6f, radius + 0.6f, distance);
                if (alpha > 0f) Blend(pixels, y * width + x, color, alpha);
            }
        }

        static void DrawRoundedRect(Color32[] pixels, int width, int height, float xMin, float yMin, float xMax, float yMax, float radius, Color color)
        {
            for (int y = Mathf.Max(0, Mathf.FloorToInt(yMin - 1)); y <= Mathf.Min(height - 1, Mathf.CeilToInt(yMax + 1)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(xMin - 1)); x <= Mathf.Min(width - 1, Mathf.CeilToInt(xMax + 1)); x++)
            {
                float qx = Mathf.Abs(x + 0.5f - ((xMin + xMax) * 0.5f)) - (((xMax - xMin) * 0.5f) - radius);
                float qy = Mathf.Abs(y + 0.5f - ((yMin + yMax) * 0.5f)) - (((yMax - yMin) * 0.5f) - radius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float signed = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float alpha = 1f - Mathf.SmoothStep(-0.7f, 0.7f, signed);
                if (alpha > 0f) Blend(pixels, y * width + x, color, alpha);
            }
        }

        static void Blend(Color32[] pixels, int index, Color color, float coverage)
        {
            float sourceAlpha = Mathf.Clamp01(color.a * coverage);
            Color32 destination = pixels[index];
            float destinationAlpha = destination.a / 255f;
            float resultAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            if (resultAlpha <= 0f) return;
            float inverse = destinationAlpha * (1f - sourceAlpha);
            pixels[index] = new Color32(
                (byte)Mathf.RoundToInt(((color.r * sourceAlpha + destination.r / 255f * inverse) / resultAlpha) * 255f),
                (byte)Mathf.RoundToInt(((color.g * sourceAlpha + destination.g / 255f * inverse) / resultAlpha) * 255f),
                (byte)Mathf.RoundToInt(((color.b * sourceAlpha + destination.b / 255f * inverse) / resultAlpha) * 255f),
                (byte)Mathf.RoundToInt(resultAlpha * 255f));
        }

        static void Set(object target, string propertyName, object value)
        {
            if (target == null) return;
            try
            {
                PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite) property.SetValue(target, value, null);
            }
            catch { }
        }

        public void Dispose()
        {
            if (badgeObject != null) UnityEngine.Object.Destroy(badgeObject);
        }

        struct Segment
        {
            internal Segment(float x1, float y1, float x2, float y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }
            internal float X1, Y1, X2, Y2;
        }
    }
}

using System;
using System.Reflection;
using UnityEngine;

namespace SPTItemIntelligence
{
    // A small native UI label, sized like EFT's armor-class tab. No generated raster glyphs.
    public sealed class AmmoPenetrationBadgeView : IDisposable
    {
        static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
        static readonly Type TextType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", false);
        static readonly Type OutlineType = Type.GetType("UnityEngine.UI.Outline, UnityEngine.UI", false);
        static Sprite shieldSprite;
        readonly GameObject badgeObject;
        readonly RectTransform rect;
        readonly Component background;
        readonly Component label;

        AmmoPenetrationBadgeView(GameObject badgeObject, RectTransform rect, Component background, Component label)
        { this.badgeObject = badgeObject; this.rect = rect; this.background = background; this.label = label; }

        public static AmmoPenetrationBadgeView TryCreate(RectTransform anchor)
        {
            if (anchor == null || ImageType == null || TextType == null) return null;
            GameObject badge = null;
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                Sprite sprite = GetShieldSprite();
                if (font == null || sprite == null) return null;
                badge = new GameObject("SPTItemIntelligenceAmmoPenetration", typeof(RectTransform));
                badge.layer = anchor.gameObject.layer;
                RectTransform rect = badge.transform as RectTransform;
                rect.SetParent(anchor, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.sizeDelta = new Vector2(18f, 16f);
                rect.anchoredPosition = new Vector2(2f, 2f);

                Component background = badge.AddComponent(ImageType) as Component;
                Set(background, "sprite", sprite);
                Set(background, "preserveAspect", true);
                Set(background, "color", new Color(0.72f, 0.75f, 0.77f, 0.98f));
                Set(background, "raycastTarget", false);
                if (OutlineType != null)
                {
                    Component outline = badge.AddComponent(OutlineType) as Component;
                    Set(outline, "effectColor", new Color(0.02f, 0.03f, 0.04f, 0.75f));
                    Set(outline, "effectDistance", new Vector2(0.7f, -0.7f));
                }

                GameObject textObject = new GameObject("Class", typeof(RectTransform));
                textObject.layer = badge.layer;
                RectTransform textRect = textObject.transform as RectTransform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(0f, 2f);
                textRect.offsetMax = new Vector2(0f, 0f);
                Component label = textObject.AddComponent(TextType) as Component;
                Set(label, "font", font);
                Set(label, "fontSize", 11);
                Set(label, "fontStyle", FontStyle.Bold);
                Set(label, "alignment", TextAnchor.MiddleCenter);
                Set(label, "color", new Color(0.045f, 0.055f, 0.065f, 1f));
                Set(label, "raycastTarget", false);
                badge.SetActive(false);
                rect.SetAsLastSibling();
                return new AmmoPenetrationBadgeView(badge, rect, background, label);
            }
            catch
            {
                if (badge != null) UnityEngine.Object.Destroy(badge);
                return null;
            }
        }

        public void Apply(string romanClass, float chance, RectTransform anchor)
        {
            if (badgeObject == null) return;
            bool visible = IsValidClass(romanClass) && anchor != null && anchor.gameObject.activeInHierarchy;
            if (visible)
            {
                Set(label, "text", romanClass);
                Set(background, "color", ShieldColor(chance));
                rect.sizeDelta = new Vector2(romanClass.Length >= 3 ? 21f : 18f, 16f);
                rect.anchoredPosition = new Vector2(2f, 2f);
                rect.SetAsLastSibling();
            }
            if (badgeObject.activeSelf != visible) badgeObject.SetActive(visible);
        }

        static bool IsValidClass(string value) =>
            value == "I" || value == "II" || value == "III" || value == "IV" || value == "V" || value == "VI";

        public static Color ShieldColor(float chance)
        {
            if (chance < 20f) return new Color(0.55f, 0.36f, 0.35f, 0.92f); // very low
            if (chance < 40f) return new Color(0.66f, 0.40f, 0.37f, 0.94f); // low
            if (chance < 60f) return new Color(0.68f, 0.61f, 0.41f, 0.94f); // medium
            if (chance < 80f) return new Color(0.57f, 0.68f, 0.43f, 0.94f); // high
            return new Color(0.45f, 0.66f, 0.48f, 0.96f); // very high
        }

        static Sprite GetShieldSprite()
        {
            if (shieldSprite != null) return shieldSprite;
            const int width = 72, height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SPTItemIntelligenceAmmoShield";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float px = x + (sx + 0.5f) * 0.25f;
                    float py = y + (sy + 0.5f) * 0.25f;
                    float limit = py < 18f ? 5f : 5f + (py - 18f) * 0.67f;
                    if (py >= 5f && py <= 59f && px >= limit && px <= width - limit) covered++;
                }
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(covered * 255 / 16));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            shieldSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 72f);
            return shieldSprite;
        }

        static void Set(object target, string propertyName, object value)
        {
            if (target == null) return;
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite) property.SetValue(target, value, null);
        }

        public void Dispose()
        {
            if (badgeObject != null) UnityEngine.Object.Destroy(badgeObject);
        }
    }
}

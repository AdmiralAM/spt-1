using System;
using System.Reflection;
using UnityEngine;

namespace SPTItemIntelligence
{
    public sealed class AmmoPenetrationBadgeView : IDisposable
    {
        static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
        static readonly Sprite[] NativeClassSprites = new Sprite[7];
        static readonly bool[] SpriteLookedUp = new bool[7];
        static MethodInfo popSprite;
        static bool resourceLookupAttempted;
        readonly GameObject badgeObject;
        readonly RectTransform rect;
        readonly Component image;

        AmmoPenetrationBadgeView(GameObject badgeObject, RectTransform rect, Component image)
        { this.badgeObject = badgeObject; this.rect = rect; this.image = image; }

        public static AmmoPenetrationBadgeView TryCreate(RectTransform anchor)
        {
            if (anchor == null || ImageType == null) return null;
            GameObject badge = null;
            try
            {
                badge = new GameObject("SPTItemIntelligenceAmmoPenetration", typeof(RectTransform));
                badge.layer = anchor.gameObject.layer;
                RectTransform rect = badge.transform as RectTransform;
                rect.SetParent(anchor, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchoredPosition = new Vector2(3f, 3f);
                Component image = badge.AddComponent(ImageType) as Component;
                Set(image, "raycastTarget", false);
                Set(image, "preserveAspect", true);
                badge.SetActive(false);
                rect.SetAsLastSibling();
                return new AmmoPenetrationBadgeView(badge, rect, image);
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
            int tier = Tier(romanClass);
            Sprite sprite = tier == 0 ? null : GetNativeClassSprite(tier);
            bool visible = sprite != null && anchor != null && anchor.gameObject.activeInHierarchy;
            if (visible)
            {
                Set(image, "sprite", sprite);
                Set(image, "color", ShieldColor(chance));
                float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
                rect.sizeDelta = new Vector2(Mathf.Clamp(16f * aspect, 15f, 24f), 16f);
                rect.anchoredPosition = new Vector2(3f, 3f);
                rect.SetAsLastSibling();
            }
            if (badgeObject.activeSelf != visible) badgeObject.SetActive(visible);
        }

        static int Tier(string value)
        {
            switch (value)
            {
                case "I": return 1;
                case "II": return 2;
                case "III": return 3;
                case "IV": return 4;
                case "V": return 5;
                case "VI": return 6;
                default: return 0;
            }
        }

        static Sprite GetNativeClassSprite(int tier)
        {
            if (tier < 1 || tier > 6) return null;
            if (SpriteLookedUp[tier]) return NativeClassSprites[tier];
            if (!BindResourceLookup()) return null;
            try
            {
                Sprite sprite = popSprite.Invoke(null, new object[] { "Mod Types/icon_type_mod_armor_plate_" + tier }) as Sprite;
                if (sprite != null)
                {
                    NativeClassSprites[tier] = sprite;
                    SpriteLookedUp[tier] = true;
                }
                return sprite;
            }
            catch { return null; }
        }

        static bool BindResourceLookup()
        {
            if (popSprite != null) return true;
            if (resourceLookupAttempted) return false;
            resourceLookupAttempted = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type cache = assembly.GetType("EFT.Utilities.ResourcesCache", false);
                if (cache == null) continue;
                foreach (MethodInfo method in cache.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (method.Name != "Pop" || !method.IsGenericMethodDefinition) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string)) continue;
                    popSprite = method.MakeGenericMethod(typeof(Sprite));
                    return true;
                }
            }
            return false;
        }

        public static Color ShieldColor(float chance)
        {
            if (chance < 20f) return new Color(0.73f, 0.51f, 0.49f, 1f);
            if (chance < 40f) return new Color(0.79f, 0.57f, 0.52f, 1f);
            if (chance < 60f) return new Color(0.80f, 0.74f, 0.55f, 1f);
            if (chance < 80f) return new Color(0.72f, 0.79f, 0.50f, 1f);
            return new Color(0.43f, 0.78f, 0.62f, 1f);
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

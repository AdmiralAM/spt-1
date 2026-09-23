using System;
using System.Reflection;
using UnityEngine;

namespace SPTItemIntelligence
{
    internal static class GameLanguageDetector
    {
        static bool observedRussianUi;

        internal static bool DetectRussian()
        {
            return TryReadGameLanguage(out string language)
                ? IsRussian(language)
                : Application.systemLanguage == SystemLanguage.Russian;
        }

        internal static void ObserveNativeUi(Component root)
        {
            if (observedRussianUi || root == null) return;
            try
            {
                Component[] components = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null) continue;
                    PropertyInfo text = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                    if (text == null || text.PropertyType != typeof(string) || !text.CanRead) continue;
                    string value = text.GetValue(component, null) as string;
                    if (!ContainsCyrillic(value)) continue;
                    observedRussianUi = true;
                    GameUiText.SetRussian(true);
                    return;
                }
            }
            catch { }
        }

        static bool ContainsCyrillic(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
                if (value[i] >= '\u0400' && value[i] <= '\u04ff') return true;
            return false;
        }

        static bool TryReadGameLanguage(out string language)
        {
            language = null;
            string[] propertyNames = { "CurrentLanguage", "CurrentLanguageCode", "Language", "Locale" };
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { types = exception.Types; }
                catch { continue; }
                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || (type.FullName ?? type.Name).IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    for (int p = 0; p < propertyNames.Length; p++)
                    {
                        try
                        {
                            PropertyInfo property = type.GetProperty(propertyNames[p], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            if (property == null || property.GetIndexParameters().Length != 0) continue;
                            string candidate = property.GetValue(null, null)?.ToString();
                            if (string.IsNullOrWhiteSpace(candidate)) continue;
                            language = candidate;
                            return true;
                        }
                        catch { }
                    }
                }
            }
            return false;
        }

        static bool IsRussian(string language)
        {
            string value = (language ?? string.Empty).Trim();
            return value.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                || value.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("рус", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

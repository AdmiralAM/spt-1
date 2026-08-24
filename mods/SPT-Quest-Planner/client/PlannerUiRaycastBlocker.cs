using System;
using System.Reflection;
using UnityEngine;

namespace SPTQuestPlanner.Client
{
    internal sealed class PlannerUiRaycastBlocker
    {
        private GameObject root;

        public void Ensure()
        {
            if (root != null) return;

            Type graphicRaycasterType = FindType("UnityEngine.UI.GraphicRaycaster");
            Type imageType = FindType("UnityEngine.UI.Image");
            if (graphicRaycasterType == null || imageType == null)
                throw new InvalidOperationException("UnityEngine.UI raycast components are unavailable.");

            root = new GameObject("QuestPlannerModalBlocker", typeof(Canvas));
            UnityEngine.Object.DontDestroyOnLoad(root);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            root.AddComponent(graphicRaycasterType);

            GameObject shield = new GameObject("QuestPlannerRaycastShield", typeof(RectTransform));
            shield.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)shield.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Component image = shield.AddComponent(imageType);
            SetProperty(image, "color", new Color(0f, 0f, 0f, 0.001f));
            SetProperty(image, "raycastTarget", true);
        }

        public void Release()
        {
            GameObject value = root;
            root = null;
            if (value != null)
                UnityEngine.Object.Destroy(value);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
                throw new InvalidOperationException("Unity UI property unavailable: " + propertyName);
            property.SetValue(target, value, null);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(fullName, false, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }
    }
}

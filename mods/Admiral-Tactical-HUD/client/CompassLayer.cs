using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTPopCounter
{
    public sealed partial class Plugin
    {
        // The heading strip and its 80-degree projection follow the MIT-licensed
        // Vinarator Compass HUD 1.1.2. The lifecycle, item gate and renderer are
        // integrated with Admiral HUD so there is no second world scan or plugin.
        const string VanillaCompassTemplate = "5f4f9eb969cdc30ff33f09db";
        static readonly string[] DirectionsEn = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        static readonly string[] DirectionsRu = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
        static readonly string[] DegreeLabels = CreateDegreeLabels();

        ConfigEntry<bool> compassEnabled, compassRequireItem, compassShowDegrees, compassRussianDirections;
        ConfigEntry<float> compassScale, compassOpacity, compassTopOffset;
        Type compassEquipmentType;
        MethodInfo compassGetSlot;
        object[] compassSlotArguments;
        object[] compassSpecialSlots;
        bool compassHasItem;
        Camera compassCamera;
        float compassYaw;
        int compassRoundedHeading = -1;
        string compassHeadingLabel = "0°";
        GUIStyle compassTextStyle, compassCenterStyle;

        static string[] CreateDegreeLabels()
        {
            string[] labels = new string[24];
            for (int i = 0; i < labels.Length; i++) labels[i] = (i * 15).ToString();
            return labels;
        }

        void BindCompass()
        {
            compassEnabled = Config.Bind("Compass", "Enabled", true, "Показывать шкалу компаса в рейде");
            compassRequireItem = Config.Bind("Compass", "Require compass in special slot", true,
                "Показывать шкалу только с компасом EYE MK.2 в одном из трёх специальных слотов");
            compassShowDegrees = Config.Bind("Compass", "Show degrees", true, "Показывать градусы и азимут");
            compassRussianDirections = Config.Bind("Compass", "Russian directions", true, "Русские обозначения сторон света");
            compassScale = Config.Bind("Compass", "Scale", 1f,
                new ConfigDescription("Масштаб шкалы", new AcceptableValueRange<float>(.6f, 1.5f)));
            compassOpacity = Config.Bind("Compass", "Opacity", .85f,
                new ConfigDescription("Прозрачность шкалы", new AcceptableValueRange<float>(.2f, 1f)));
            compassTopOffset = Config.Bind("Compass", "Top offset", 16f,
                new ConfigDescription("Отступ от верхнего края экрана", new AcceptableValueRange<float>(0f, 300f)));
        }

        void RefreshCompass(object localPlayer)
        {
            if (!compassEnabled.Value || !IsUsableUnityObject(localPlayer))
            {
                compassHasItem = false;
                return;
            }
            compassHasItem = !compassRequireItem.Value || HasSpecialSlotCompass(localPlayer);
        }

        bool HasSpecialSlotCompass(object player)
        {
            try
            {
                object controller = ReadMember(player, "InventoryController");
                object inventory = ReadMember(controller, "Inventory");
                object equipment = ReadMember(inventory, "Equipment");
                if (equipment == null) return false;

                if (compassGetSlot == null || compassEquipmentType != equipment.GetType())
                {
                    compassGetSlot = null;
                    MethodInfo[] methods = equipment.GetType().GetMethods(InstanceFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (methods[i].Name != "GetSlot") continue;
                        ParameterInfo[] parameters = methods[i].GetParameters();
                        if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum) continue;
                        compassGetSlot = methods[i];
                        break;
                    }
                    if (compassGetSlot == null) return false;
                    compassSlotArguments = new object[1];
                    compassEquipmentType = equipment.GetType();
                    Type slotType = compassGetSlot.GetParameters()[0].ParameterType;
                    compassSpecialSlots = new object[3];
                    for (int i = 0; i < compassSpecialSlots.Length; i++)
                        compassSpecialSlots[i] = Enum.Parse(slotType, "SpecialSlot" + (i + 1));
                }

                for (int i = 0; i < compassSpecialSlots.Length; i++)
                {
                    compassSlotArguments[0] = compassSpecialSlots[i];
                    object slot = compassGetSlot.Invoke(equipment, compassSlotArguments);
                    object item = ReadMember(slot, "ContainedItem");
                    string templateId = (ReadMember(item, "TemplateId") ??
                        ReadMember(ReadMember(item, "Template"), "_id") ??
                        ReadMember(ReadMember(item, "Template"), "Id"))?.ToString();
                    if (string.Equals(templateId, VanillaCompassTemplate, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { /* Missing/changed EFT slot API hides only the compass. */ }
            return false;
        }

        void UpdateCompass()
        {
            if (!inRaid || !compassEnabled.Value || (compassRequireItem.Value && !compassHasItem)) return;
            if (compassCamera == null) compassCamera = Camera.main;
            if (compassCamera == null) return;

            // Compass HUD 1.1.2 uses a 180-degree shift for Tarkov's world axes.
            compassYaw = Mathf.Repeat(compassCamera.transform.eulerAngles.y + 180f, 360f);
            int rounded = Mathf.RoundToInt(compassYaw) % 360;
            if (rounded != compassRoundedHeading)
            {
                compassRoundedHeading = rounded;
                compassHeadingLabel = rounded.ToString() + "°";
            }
        }

        void ResetCompass()
        {
            compassEquipmentType = null;
            compassGetSlot = null;
            compassSlotArguments = null;
            compassSpecialSlots = null;
            compassHasItem = false;
            compassCamera = null;
            compassRoundedHeading = -1;
        }

        void DisposeCompass()
        {
            ResetCompass();
            compassTextStyle = null;
            compassCenterStyle = null;
        }

        void RenderCompass()
        {
            if (Event.current.type != EventType.Repaint || !inRaid || !compassEnabled.Value ||
                (compassRequireItem.Value && !compassHasItem) || compassCamera == null || Cursor.visible) return;

            if (compassTextStyle == null)
            {
                compassTextStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
                compassCenterStyle = new GUIStyle(compassTextStyle) { fontStyle = FontStyle.Bold, fontSize = 15 };
            }

            float scale = compassScale.Value;
            float width = Mathf.Min(Screen.width - 24f, 520f * scale);
            if (width <= 0f) return;
            float center = Screen.width * .5f;
            float top = Mathf.Clamp(compassTopOffset.Value, 0f, Mathf.Max(0f, Screen.height - 56f * scale));
            float half = width * .5f;
            float pixelsPerDegree = width / 80f;
            float opacity = compassOpacity.Value;
            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, opacity * .42f);
            GUI.DrawTexture(new Rect(center - half, top, width, 43f * scale), Texture2D.whiteTexture);

            for (int angle = 0; angle < 360; angle += 15)
            {
                float delta = Mathf.DeltaAngle(compassYaw, angle);
                if (Mathf.Abs(delta) > 40f) continue;
                float edge = 1f - Mathf.Pow(Mathf.Abs(delta) / 40f, 2f);
                float x = center + delta * pixelsPerDegree;
                bool cardinal = angle % 45 == 0;
                GUI.color = new Color(1f, 1f, 1f, opacity * edge);
                GUI.DrawTexture(new Rect(x, top + 4f * scale, 1f, (cardinal ? 10f : 6f) * scale), Texture2D.whiteTexture);
                if (cardinal)
                {
                    string label = (compassRussianDirections.Value ? DirectionsRu : DirectionsEn)[angle / 45];
                    GUI.Label(new Rect(x - 20f * scale, top + 13f * scale, 40f * scale, 19f * scale), label, compassTextStyle);
                }
                else if (compassShowDegrees.Value)
                {
                    GUI.Label(new Rect(x - 20f * scale, top + 13f * scale, 40f * scale, 19f * scale), DegreeLabels[angle / 15], compassTextStyle);
                }
            }

            GUI.color = new Color(1f, .72f, .36f, opacity);
            GUI.DrawTexture(new Rect(center - 1f, top, 2f, 16f * scale), Texture2D.whiteTexture);
            if (compassShowDegrees.Value)
                GUI.Label(new Rect(center - 35f * scale, top + 27f * scale, 70f * scale, 15f * scale), compassHeadingLabel, compassCenterStyle);
            GUI.color = previous;
        }
    }
}

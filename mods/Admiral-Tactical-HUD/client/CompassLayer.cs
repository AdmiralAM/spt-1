using System;
using System.Collections;
using System.Collections.Generic;
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
        ConfigEntry<bool> compassShowExtracts, compassShowTransits, compassShowQuests;
        readonly List<CompassMarker> compassMarkers = new List<CompassMarker>(32);
        object compassMarkerWorld;
        float compassNextMarkerCapture;
        Transform compassPlayerTransform;
        Type compassQuestUtilsType;
        MethodInfo compassQuestCapture, compassQuestMarkers, compassQuestDiscard;
        readonly object[] compassQuestArguments = new object[1];
        bool compassQuestBridgeAttempted, compassQuestDataCaptured;

        sealed class CompassMarker
        {
            public Component Source;
            public Vector3 Position;
            public bool IsTransit;
            public bool IsQuest;
        }
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
            compassShowExtracts = Config.Bind("Compass", "Show eligible extracts", true,
                "Показывать доступные персонажу выходы; закрытые выходы скрыты");
            compassShowTransits = Config.Bind("Compass", "Show transits", true,
                "Показывать переходы между локациями");
            compassShowQuests = Config.Bind("Compass", "Show active quest objectives (Dynamic Maps)", true,
                "Цели активных незавершённых заданий; требуется Dynamic Maps");
        }

        void RefreshCompassMarkers(object world, object localPlayer)
        {
            if (!compassEnabled.Value || (compassRequireItem.Value && !compassHasItem))
            {
                compassMarkers.Clear();
                compassNextMarkerCapture = 0f;
                return;
            }
            Component player = localPlayer as Component;
            compassPlayerTransform = player != null ? player.transform : null;
            if (!ReferenceEquals(compassMarkerWorld, world))
            {
                compassMarkerWorld = world;
                compassMarkers.Clear();
                compassNextMarkerCapture = 0f;
            }
            if (Time.unscaledTime < compassNextMarkerCapture) return;
            compassNextMarkerCapture = Time.unscaledTime + (compassMarkers.Count == 0 ? 2f : 10f);
            compassMarkers.Clear();
            try
            {
                if (compassShowExtracts.Value)
                {
                    object controller = ReadMember(world, "ExfiltrationController");
                    object profile = ReadMember(localPlayer, "Profile");
                    bool scav = string.Equals(ReadMember(profile, "Side")?.ToString(), "Savage", StringComparison.OrdinalIgnoreCase);
                    object points = ReadMember(controller, scav ? "ScavExfiltrationPoints" : "ExfiltrationPoints");
                    CaptureCompassPoints(points as IEnumerable, false, localPlayer);
                }
                if (compassShowTransits.Value)
                {
                    object controller = ReadMember(world, "TransitController");
                    object points = ReadMember(controller, "pointsById");
                    CaptureCompassPoints((ReadMember(points, "Values") ?? points) as IEnumerable, true, localPlayer);
                }
                if (compassShowQuests.Value && Time.timeSinceLevelLoad >= 8f)
                    CaptureCompassQuestMarkers(localPlayer);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Compass marker capture unavailable: " + ex.Message);
            }
        }

        void CaptureCompassQuestMarkers(object localPlayer)
        {
            object profile = ReadMember(localPlayer, "Profile");
            if (string.Equals(ReadMember(profile, "Side")?.ToString(), "Savage", StringComparison.OrdinalIgnoreCase))
                return; // Match Dynamic Maps: no player-quest markers in scav raids.
            // Dynamic Maps owns quest qualification. Resolve its internal, version-sensitive
            // interface only once and fail closed if the optional mod is unavailable.
            if (!compassQuestBridgeAttempted)
            {
                compassQuestBridgeAttempted = true;
                compassQuestUtilsType = Type.GetType("DynamicMaps.Utils.QuestUtils, DynamicMaps", false);
                if (compassQuestUtilsType != null)
                {
                    BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                    compassQuestCapture = compassQuestUtilsType.GetMethod("TryCaptureQuestData", flags);
                    compassQuestMarkers = compassQuestUtilsType.GetMethod("GetMarkerDefsForPlayer", flags);
                    compassQuestDiscard = compassQuestUtilsType.GetMethod("DiscardQuestData", flags);
                }
            }
            if (compassQuestCapture == null || compassQuestMarkers == null) return;
            ParameterInfo[] parameters = compassQuestMarkers.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(localPlayer)) return;
            try
            {
                if (!compassQuestDataCaptured)
                {
                    compassQuestDataCaptured = true;
                    // Donor performs one bounded capture of quest triggers/items per raid.
                    compassQuestCapture.Invoke(null, null);
                }
                compassQuestArguments[0] = localPlayer;
                IEnumerable definitions = compassQuestMarkers.Invoke(null, compassQuestArguments) as IEnumerable;
                if (definitions == null) return;
                foreach (object definition in definitions)
                {
                    if (compassMarkers.Count >= 32) break;
                    if (!(ReadMember(definition, "Position") is Vector3 mapPosition)) continue;
                    // Dynamic Maps converts Unity (x,y,z) to map (x,z,y).
                    Vector3 worldPosition = new Vector3(mapPosition.x, mapPosition.z, mapPosition.y);
                    bool duplicate = false;
                    for (int i = 0; i < compassMarkers.Count; i++)
                        if (compassMarkers[i].IsQuest &&
                            (compassMarkers[i].Position - worldPosition).sqrMagnitude < 1f)
                        { duplicate = true; break; }
                    if (!duplicate) compassMarkers.Add(new CompassMarker { Position = worldPosition, IsQuest = true });
                }
            }
            catch (Exception ex)
            {
                compassQuestMarkers = null;
                Logger.LogDebug("Optional Dynamic Maps quest bridge disabled: " + ex.Message);
            }
            finally { compassQuestArguments[0] = null; }
        }

        void CaptureCompassPoints(IEnumerable points, bool transit, object localPlayer)
        {
            if (points == null) return;
            foreach (object point in points)
            {
                if (compassMarkers.Count >= 20) break;
                Component component = point as Component;
                if (component == null || !component.gameObject.activeInHierarchy) continue;
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null && !behaviour.isActiveAndEnabled) continue;
                if (!transit)
                {
                    string status = ReadMember(point, "Status")?.ToString();
                    if (status == "NotPresent" || status == "Unknown") continue;
                    MethodInfo match = null;
                    foreach (MethodInfo candidate in point.GetType().GetMethods(InstanceFlags))
                        if (candidate.Name == "InfiltrationMatch" &&
                            candidate.GetParameters().Length == 1 &&
                            candidate.GetParameters()[0].ParameterType.IsInstanceOfType(localPlayer))
                        { match = candidate; break; }
                    if (match != null)
                    {
                        ParameterInfo[] parameters = match.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(localPlayer))
                        {
                            if (!IsTrue(match.Invoke(point, new[] { localPlayer }))) continue;
                        }
                        else continue; // Eligibility cannot be proven: never reveal the point.
                    }
                    else continue;
                }
                compassMarkers.Add(new CompassMarker
                {
                    Source = component,
                    Position = component.transform.position,
                    IsTransit = transit
                });
            }
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
            if (compassQuestDataCaptured && compassQuestDiscard != null)
                try { compassQuestDiscard.Invoke(null, null); } catch { }
            compassQuestDataCaptured = false;
            compassQuestBridgeAttempted = false;
            compassQuestUtilsType = null;
            compassQuestCapture = compassQuestMarkers = compassQuestDiscard = null;
            compassQuestArguments[0] = null;
            compassEquipmentType = null;
            compassGetSlot = null;
            compassSlotArguments = null;
            compassSpecialSlots = null;
            compassHasItem = false;
            compassCamera = null;
            compassRoundedHeading = -1;
            compassMarkers.Clear();
            compassMarkerWorld = null;
            compassNextMarkerCapture = 0f;
            compassPlayerTransform = null;
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
            if (compassPlayerTransform != null)
            {
                Vector3 origin = compassPlayerTransform.position;
                for (int i = 0; i < compassMarkers.Count; i++)
                {
                    CompassMarker marker = compassMarkers[i];
                    if (!marker.IsQuest && marker.Source == null) continue;
                    Vector3 direction = marker.Position - origin;
                    if (direction.sqrMagnitude < 1f) continue;
                    float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + 180f, 360f);
                    float delta = Mathf.DeltaAngle(compassYaw, bearing);
                    float x = center + Mathf.Clamp(delta, -39f, 39f) * pixelsPerDegree;
                    GUI.color = marker.IsQuest ? new Color(.60f, .84f, 1f, opacity) :
                        marker.IsTransit ? new Color(1f, .56f, .43f, opacity) :
                        new Color(.70f, .91f, .58f, opacity);
                    GUI.Label(new Rect(x - 9f * scale, top + 43f * scale, 18f * scale, 17f * scale),
                        compassRussianDirections.Value
                            ? marker.IsQuest ? "З" : marker.IsTransit ? "П" : "В"
                            : marker.IsQuest ? "Q" : marker.IsTransit ? "T" : "E", compassCenterStyle);
                }
            }
            if (compassShowDegrees.Value)
                GUI.Label(new Rect(center - 35f * scale, top + 27f * scale, 70f * scale, 15f * scale), compassHeadingLabel, compassCenterStyle);
            GUI.color = previous;
        }
    }
}

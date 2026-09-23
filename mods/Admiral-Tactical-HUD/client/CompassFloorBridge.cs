using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTPopCounter
{
    public sealed partial class Plugin
    {
        struct CompassFloorBound
        {
            public Vector3 Min;
            public Vector3 Max;
            public int Level;
            public float Volume;
        }

        readonly List<CompassFloorBound> compassFloorBounds = new List<CompassFloorBound>(64);
        object compassFloorMapDef;
        float compassNextFloorRefresh;
        Type compassDynamicMapsPluginType;
        FieldInfo compassDynamicMapsInstance;
        Texture2D compassUpChevron, compassDownChevron;

        void RefreshCompassFloorBounds()
        {
            if (Time.unscaledTime < compassNextFloorRefresh) return;
            compassNextFloorRefresh = Time.unscaledTime + 5f;
            try
            {
                if (compassDynamicMapsPluginType == null)
                {
                    compassDynamicMapsPluginType = Type.GetType("DynamicMaps.Plugin, DynamicMaps", false);
                    compassDynamicMapsInstance = compassDynamicMapsPluginType?.GetField("Instance",
                        BindingFlags.Static | BindingFlags.Public);
                }
                object plugin = compassDynamicMapsInstance?.GetValue(null);
                object map = ReadMember(plugin, "Map");
                object mapView = ReadMember(map, "MapView");
                object mapDef = ReadMember(mapView, "CurrentMapDef");
                if (ReferenceEquals(mapDef, compassFloorMapDef) && compassFloorBounds.Count > 0) return;
                compassFloorMapDef = mapDef;
                compassFloorBounds.Clear();
                object layers = ReadMember(mapDef, "Layers");
                IEnumerable values = ReadMember(layers, "Values") as IEnumerable;
                if (values == null) return;
                foreach (object layer in values)
                {
                    if (!(ReadMember(layer, "Level") is int level)) continue;
                    IEnumerable bounds = ReadMember(layer, "GameBounds") as IEnumerable;
                    if (bounds == null) continue;
                    foreach (object bound in bounds)
                    {
                        if (!(ReadMember(bound, "Min") is Vector3 min) ||
                            !(ReadMember(bound, "Max") is Vector3 max)) continue;
                        float volume = (max.x - min.x) * (max.y - min.y) * (max.z - min.z);
                        if (volume <= 0f) continue;
                        compassFloorBounds.Add(new CompassFloorBound
                        {
                            Min = min,
                            Max = max,
                            Level = level,
                            Volume = volume
                        });
                    }
                }
            }
            catch { compassFloorBounds.Clear(); /* Optional map data never breaks the base HUD. */ }
        }

        int? CompassFloorAt(Vector3 worldPosition)
        {
            // Dynamic Maps' bounds use (world X, world Z, world Y).
            Vector3 map = new Vector3(worldPosition.x, worldPosition.z, worldPosition.y);
            float bestVolume = float.MaxValue;
            int? bestLevel = null;
            for (int i = 0; i < compassFloorBounds.Count; i++)
            {
                CompassFloorBound bound = compassFloorBounds[i];
                if (map.x <= bound.Min.x || map.x >= bound.Max.x ||
                    map.y <= bound.Min.y || map.y >= bound.Max.y ||
                    map.z <= bound.Min.z || map.z >= bound.Max.z ||
                    bound.Volume >= bestVolume) continue;
                bestVolume = bound.Volume;
                bestLevel = bound.Level;
            }
            return bestLevel;
        }

        int CompassHeightRelation(Vector3 playerPosition, Vector3 targetPosition)
        {
            int? playerFloor = CompassFloorAt(playerPosition);
            int? targetFloor = CompassFloorAt(targetPosition);
            if (playerFloor.HasValue && targetFloor.HasValue)
                return targetFloor.Value.CompareTo(playerFloor.Value);

            // Unsupported/absent map data: only a clear vertical separation gets a mark.
            float difference = targetPosition.y - playerPosition.y;
            return Mathf.Abs(difference) >= 3f ? Math.Sign(difference) : 0;
        }

        void ResetCompassFloorBounds()
        {
            compassFloorBounds.Clear();
            compassFloorMapDef = null;
            compassNextFloorRefresh = 0f;
            compassDynamicMapsPluginType = null;
            compassDynamicMapsInstance = null;
        }

        Texture2D MakeCompassChevron(bool up)
        {
            Texture2D texture = new Texture2D(7, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 7; x++)
                {
                    int row = up ? y : 3 - y;
                    texture.SetPixel(x, 3 - y,
                        x == 3 - row || x == 3 + row ? Color.white : Color.clear);
                }
            texture.Apply(false, true);
            return texture;
        }

        void RenderCompassHeightMarker(float x, float skullTop, float skullSize,
            int relation, float scale, float opacity)
        {
            if (relation == 0) return;
            if (compassUpChevron == null) compassUpChevron = MakeCompassChevron(true);
            if (compassDownChevron == null) compassDownChevron = MakeCompassChevron(false);
            GUI.color = relation > 0
                ? new Color(.38f, .95f, .49f, opacity)
                : new Color(1f, .35f, .32f, opacity);
            GUI.DrawTexture(new Rect(x - 5f * scale,
                    relation > 0 ? skullTop - 7f * scale : skullTop + skullSize + 2f * scale,
                    10f * scale, 6f * scale),
                relation > 0 ? compassUpChevron : compassDownChevron);
        }

        void DisposeCompassHeightTextures()
        {
            if (compassUpChevron != null) Destroy(compassUpChevron);
            if (compassDownChevron != null) Destroy(compassDownChevron);
            compassUpChevron = compassDownChevron = null;
        }
    }
}

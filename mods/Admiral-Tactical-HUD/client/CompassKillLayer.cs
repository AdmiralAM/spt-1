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
        sealed class CompassKillSubscription
        {
            public EventInfo Event;
            public Delegate Handler;
        }

        sealed class CompassKillMarker
        {
            public Vector3 Position;
            public float ExpiresAt;
            public int HeightRelation;
        }

        sealed class CompassCorpseMarker
        {
            public Vector3 Position;
            public float DistanceSq;
            public int HeightRelation;
        }

        ConfigEntry<bool> compassKillEnabled;
        ConfigEntry<float> compassKillLifetime;
        ConfigEntry<bool> compassShowNearbyBodies;
        ConfigEntry<float> compassNearbyBodyRadius;
        readonly Dictionary<object, CompassKillSubscription> compassKillSubscriptions =
            new Dictionary<object, CompassKillSubscription>();
        readonly List<CompassKillMarker> compassKillMarkers = new List<CompassKillMarker>(5);
        readonly List<CompassCorpseMarker> compassCorpseMarkers = new List<CompassCorpseMarker>(8);
        object compassKillWorld;
        string compassLocalProfileId;
        Texture2D compassSkullTexture;
        float compassNextCorpseRefresh;

        void BindCompassKills()
        {
            compassKillEnabled = Config.Bind("Compass Events", "Player kill direction", true,
                "Краткий маркер только для убийства, совершённого вашим персонажем");
            compassKillLifetime = Config.Bind("Compass Events", "Kill marker lifetime", 15f,
                new ConfigDescription("Время показа маркера в секундах",
                    new AcceptableValueRange<float>(5f, 60f)));
            compassShowNearbyBodies = Config.Bind("Compass Events", "Nearby bodies", true,
                "Показывать тела рядом с персонажем");
            compassNearbyBodyRadius = Config.Bind("Compass Events", "Body detection radius", 25f,
                new ConfigDescription("Радиус показа тел в метрах",
                    new AcceptableValueRange<float>(5f, 50f)));
        }

        void RefreshCompassKillTracking(object world, object localPlayer, List<object> players)
        {
            if (!compassEnabled.Value || !compassLocalReady)
            {
                ResetCompassKills();
                return;
            }
            if (!ReferenceEquals(compassKillWorld, world))
            {
                ResetCompassKills();
                compassKillWorld = world;
            }
            compassLocalProfileId = (ReadMember(localPlayer, "ProfileId") ??
                ReadMember(ReadMember(localPlayer, "Profile"), "Id"))?.ToString();
            if (string.IsNullOrEmpty(compassLocalProfileId)) return;
            RefreshCompassFloorBounds();
            RefreshNearbyCompassBodies(world);
            if (compassPlayerTransform != null)
                for (int i = 0; i < compassKillMarkers.Count; i++)
                    compassKillMarkers[i].HeightRelation = CompassHeightRelation(
                        compassPlayerTransform.position, compassKillMarkers[i].Position);
            if (!compassKillEnabled.Value)
            {
                ClearCompassKillSubscriptions();
                compassKillMarkers.Clear();
                return;
            }

            for (int i = 0; i < players.Count && compassKillSubscriptions.Count < 256; i++)
            {
                object victim = players[i];
                if (ReferenceEquals(victim, localPlayer) || !IsAlive(victim) ||
                    compassKillSubscriptions.ContainsKey(victim)) continue;
                try
                {
                    EventInfo death = victim.GetType().GetEvent("OnPlayerDeadOrUnspawn", InstanceFlags);
                    if (death == null) continue;
                    MethodInfo callback = GetType().GetMethod(nameof(OnCompassPlayerDead), InstanceFlags);
                    Delegate handler = Delegate.CreateDelegate(death.EventHandlerType, this, callback);
                    death.AddEventHandler(victim, handler);
                    compassKillSubscriptions.Add(victim, new CompassKillSubscription
                    {
                        Event = death,
                        Handler = handler
                    });
                }
                catch { /* Changed EFT event shape disables only that source. */ }
            }
        }

        void OnCompassPlayerDead(object victim)
        {
            Component component = victim as Component;
            bool dead = !IsAlive(victim);
            bool playerKill = inRaid && !string.IsNullOrEmpty(compassLocalProfileId) &&
                dead &&
                string.Equals(ReadMember(victim, "KillerId")?.ToString(),
                    compassLocalProfileId, StringComparison.Ordinal);
            bool nearbyDeath = dead && component != null && compassPlayerTransform != null &&
                (component.transform.position - compassPlayerTransform.position).sqrMagnitude <=
                    compassNearbyBodyRadius.Value * compassNearbyBodyRadius.Value;
            if (inRaid && component != null && (playerKill || nearbyDeath))
            {
                if (compassKillMarkers.Count == 5) compassKillMarkers.RemoveAt(0);
                compassKillMarkers.Add(new CompassKillMarker
                {
                    Position = component.transform.position,
                    ExpiresAt = Time.unscaledTime + compassKillLifetime.Value,
                    HeightRelation = CompassHeightRelation(
                        compassPlayerTransform.position, component.transform.position)
                });
            }
            UntrackCompassKillSource(victim);
        }

        void RefreshNearbyCompassBodies(object world)
        {
            if (!compassShowNearbyBodies.Value || compassPlayerTransform == null)
            {
                compassCorpseMarkers.Clear();
                return;
            }
            if (Time.unscaledTime < compassNextCorpseRefresh) return;
            compassNextCorpseRefresh = Time.unscaledTime + 2f;
            compassCorpseMarkers.Clear();
            IEnumerable players = ReadMember(world, "AllPlayersEverExisted") as IEnumerable;
            if (players == null) return;
            Vector3 origin = compassPlayerTransform.position;
            float limit = compassNearbyBodyRadius.Value * compassNearbyBodyRadius.Value;
            foreach (object player in players)
            {
                if (IsAlive(player)) continue;
                Component corpse = ReadMember(player, "Corpse") as Component;
                if (corpse == null) continue;
                Vector3 position = corpse.transform.position;
                float distanceSq = (position - origin).sqrMagnitude;
                if (distanceSq > limit) continue;
                if (compassCorpseMarkers.Count < 8)
                {
                    compassCorpseMarkers.Add(new CompassCorpseMarker
                    {
                        Position = position,
                        DistanceSq = distanceSq,
                        HeightRelation = CompassHeightRelation(origin, position)
                    });
                    continue;
                }
                int farthest = 0;
                for (int i = 1; i < compassCorpseMarkers.Count; i++)
                    if (compassCorpseMarkers[i].DistanceSq > compassCorpseMarkers[farthest].DistanceSq)
                        farthest = i;
                if (distanceSq < compassCorpseMarkers[farthest].DistanceSq)
                {
                    compassCorpseMarkers[farthest].Position = position;
                    compassCorpseMarkers[farthest].DistanceSq = distanceSq;
                    compassCorpseMarkers[farthest].HeightRelation = CompassHeightRelation(origin, position);
                }
            }
        }

        void UntrackCompassKillSource(object victim)
        {
            if (!compassKillSubscriptions.TryGetValue(victim, out CompassKillSubscription subscription)) return;
            try { subscription.Event.RemoveEventHandler(victim, subscription.Handler); } catch { }
            compassKillSubscriptions.Remove(victim);
        }

        void UpdateCompassKills()
        {
            float now = Time.unscaledTime;
            for (int i = compassKillMarkers.Count - 1; i >= 0; i--)
                if (compassKillMarkers[i].ExpiresAt <= now) compassKillMarkers.RemoveAt(i);
        }

        void ResetCompassKills()
        {
            ClearCompassKillSubscriptions();
            compassKillMarkers.Clear();
            compassCorpseMarkers.Clear();
            compassKillWorld = null;
            compassLocalProfileId = null;
            compassNextCorpseRefresh = 0f;
            ResetCompassFloorBounds();
        }

        void ClearCompassKillSubscriptions()
        {
            foreach (KeyValuePair<object, CompassKillSubscription> pair in compassKillSubscriptions)
                try { pair.Value.Event.RemoveEventHandler(pair.Key, pair.Value.Handler); } catch { }
            compassKillSubscriptions.Clear();
        }

        void DisposeCompassKills()
        {
            ResetCompassKills();
            if (compassSkullTexture != null) Destroy(compassSkullTexture);
            compassSkullTexture = null;
            DisposeCompassHeightTextures();
        }

        void EnsureCompassSkullTexture()
        {
            if (compassSkullTexture != null) return;
            // Tiny code-native glyph: no font fallback, external asset or per-repaint allocation.
            string[] rows =
            {
                "001111100", "011111110", "110101011", "110101011", "111111111",
                "011111110", "001111100", "001010100", "001111100"
            };
            compassSkullTexture = new Texture2D(9, 9, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 9; x++)
                    compassSkullTexture.SetPixel(x, 8 - y,
                        rows[y][x] == '1' ? Color.white : Color.clear);
            compassSkullTexture.Apply(false, true);
        }

        void RenderCompassKills(float center, float top, float scale, float pixelsPerDegree, float opacity)
        {
            if (compassPlayerTransform == null) return;
            if (compassKillMarkers.Count > 0 || compassCorpseMarkers.Count > 0)
                EnsureCompassSkullTexture();
            Vector3 origin = compassPlayerTransform.position;
            for (int i = 0; i < compassCorpseMarkers.Count; i++)
            {
                Vector3 direction = compassCorpseMarkers[i].Position - origin;
                if (direction.sqrMagnitude < 1f) continue;
                float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + 180f, 360f);
                float delta = Mathf.DeltaAngle(compassYaw, bearing);
                float x = center + Mathf.Clamp(delta, -39f, 39f) * pixelsPerDegree;
                bool freshDeath = false;
                for (int k = 0; k < compassKillMarkers.Count; k++)
                    if ((compassKillMarkers[k].Position - compassCorpseMarkers[i].Position).sqrMagnitude < 1f)
                    { freshDeath = true; break; }
                if (freshDeath) continue;
                GUI.color = new Color(.74f, .77f, .79f, opacity * .8f);
                GUI.DrawTexture(new Rect(x - 7f * scale, top + 61f * scale, 14f * scale, 14f * scale),
                    compassSkullTexture);
                RenderCompassHeightMarker(x, top + 61f * scale, 14f * scale,
                    compassCorpseMarkers[i].HeightRelation, scale, opacity);
            }
            for (int i = 0; i < compassKillMarkers.Count; i++)
            {
                Vector3 direction = compassKillMarkers[i].Position - origin;
                if (direction.sqrMagnitude < 1f) continue;
                float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + 180f, 360f);
                float delta = Mathf.DeltaAngle(compassYaw, bearing);
                float x = center + Mathf.Clamp(delta, -39f, 39f) * pixelsPerDegree;
                GUI.color = new Color(1f, .30f, .30f, opacity);
                GUI.DrawTexture(new Rect(x - 9f * scale, top + 58f * scale, 18f * scale, 18f * scale),
                    compassSkullTexture);
                RenderCompassHeightMarker(x, top + 58f * scale, 18f * scale,
                    compassKillMarkers[i].HeightRelation, scale, opacity);
            }
        }
    }
}

using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTPopCounter
{
    public sealed partial class Plugin
    {
        HudVisualRenderer visualRenderer;

        void RenderVisualHud()
        {
            if (visualRenderer == null)
            {
                visualRenderer = new HudVisualRenderer(this);
                Logger.LogInfo("HUD renderer initialized (single-plugin architecture)");
            }
            visualRenderer.Render();
        }

        sealed class HudVisualRenderer
        {
            static readonly Color KillPmc = new Color(.56f, .76f, .51f, 1f);
            static readonly Color KillScav = new Color(.77f, .43f, .40f, 1f);
            static readonly Color KillBoss = new Color(.86f, .62f, .28f, 1f);
            static readonly Color KillRaider = new Color(.66f, .53f, .78f, 1f);
            static readonly Color KillSelf = new Color(.65f, .78f, .42f, 1f);
            static readonly Color Neutral = new Color(.80f, .82f, .81f, 1f);
            static readonly Color WeaponTextColor = new Color(.88f, .89f, .87f, 1f);
            static readonly Color Muted = new Color(.58f, .60f, .59f, 1f);
            static readonly Color Head = new Color(.84f, .33f, .30f, 1f);
            static readonly Color Water = new Color(.49f, .69f, .86f, 1f);
            static readonly Color Energy = new Color(.86f, .70f, .31f, 1f);
            static readonly Color WeightInk = new Color(.82f, .84f, .82f, 1f);
            static readonly string[] WeightIcons = { "weight1", "weight1", "weight2", "weight3" };

            readonly Plugin runtime;
            readonly HudIcons icons;
            readonly Font hudFont;
            readonly Texture2D medallionPlate;
            readonly Texture2D medallionRing;
            readonly GUIContent textContent = new GUIContent();
            GUIStyle text;
            int dragCluster;
            Vector2 dragOffset;
            Rect populationEditRect, statusEditRect, killEditRect;
            bool populationEditRectValid, statusEditRectValid, killEditRectValid;
            int cachedPmc = int.MinValue, cachedScav = int.MinValue, cachedBoss = int.MinValue, cachedRaider = int.MinValue;
            int cachedHydration = int.MinValue, cachedEnergy = int.MinValue, cachedWeight = int.MinValue;
            string pmcText, scavText, bossText, raiderText, hydrationText, energyText, weightText;

            public HudVisualRenderer(Plugin runtime)
            {
                this.runtime = runtime;
                icons = new HudIcons();
                try
                {
                    hudFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Bahnschrift SemiCondensed", "Bahnschrift", "Segoe UI Semibold", "Arial Narrow", "Arial" }, 14);
                }
                catch
                {
                    hudFont = null;
                }
                medallionPlate = CreateMedallion(false);
                medallionRing = CreateMedallion(true);
            }

            static Texture2D CreateMedallion(bool ring)
            {
                const int textureSize = 64;
                Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                texture.name = ring ? "HUD icon medallion ring" : "HUD icon medallion plate";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = new Color[textureSize * textureSize];
                for (int y = 0; y < textureSize; y++)
                {
                    for (int x = 0; x < textureSize; x++)
                    {
                        float nx = (x + .5f - textureSize * .5f) / textureSize;
                        float ny = (y + .5f - textureSize * .5f) / textureSize;
                        float distance = Mathf.Sqrt(nx * nx + ny * ny);
                        float alpha = ring
                            ? Mathf.Clamp01(1f - Mathf.Abs(distance - .455f) / .035f)
                            : Mathf.Clamp01((.50f - distance) / .045f);
                        pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return texture;
            }

            public void Dispose()
            {
                if (medallionPlate != null) UnityEngine.Object.Destroy(medallionPlate);
                if (medallionRing != null) UnityEngine.Object.Destroy(medallionRing);
            }

            public void Render()
            {
                bool debug = runtime.workAlways.Value;
                bool editing = runtime.editMode.Value;
                EventType eventType = Event.current.type;
                bool relevantEvent = eventType == EventType.Repaint || eventType == EventType.MouseDown ||
                                     eventType == EventType.MouseDrag || eventType == EventType.MouseUp;
                if (!relevantEvent) return;

                bool showPopulation = (runtime.inRaid || debug) && (runtime.mode >= 1 || editing) && runtime.popEnabled.Value;
                bool showStatus = (runtime.inRaid || debug || runtime.statusOutside.Value) &&
                                  (runtime.mode >= 2 || editing) && runtime.statusEnabled.Value;
                bool showKillFeed = (runtime.inRaid || debug) && runtime.killEnabled.Value;

                if (eventType != EventType.Repaint)
                {
                    if (!editing) return;
                    if (showPopulation && populationEditRectValid)
                        EditSurface(1, populationEditRect, runtime.popX, runtime.popY, true);
                    if (showStatus && statusEditRectValid)
                        EditSurface(2, statusEditRect, runtime.statusX, runtime.statusY, true);
                    if (showKillFeed && killEditRectValid)
                        EditSurface(3, killEditRect, runtime.killX, runtime.killY, false);
                    return;
                }

                if (showPopulation) DrawPopulation();
                if (showStatus) DrawStatus();
                if (showKillFeed) DrawKillFeed(editing);
            }

            void EnsureStyle(int size)
            {
                if (text == null)
                {
                    text = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Normal,
                        alignment = TextAnchor.UpperLeft,
                        clipping = TextClipping.Overflow,
                        padding = new RectOffset(),
                        margin = new RectOffset(),
                        richText = false
                    };
                    if (hudFont != null) text.font = hudFont;
                }
                text.fontSize = size;
            }

            float Text(Rect root, string value, float x, float y, int size, float opacity, Color color, float alphaScale = 1f)
            {
                if (string.IsNullOrEmpty(value)) return x;
                EnsureStyle(size);

                float effective = Mathf.Clamp01(opacity * alphaScale);
                Color main = color;
                main.a *= effective;
                textContent.text = value;
                float width = text.CalcSize(textContent).x;
                Rect r = new Rect(root.x + x, root.y + y, width + 4, size + 7);
                Color old = text.normal.textColor;

                if (Event.current.type == EventType.Repaint)
                {
                    Color outline = new Color(0f, 0f, 0f, Mathf.Clamp01(effective * .88f));
                    text.normal.textColor = outline;
                    GUI.Label(new Rect(r.x - 1, r.y, r.width, r.height), textContent, text);
                    GUI.Label(new Rect(r.x + 1, r.y, r.width, r.height), textContent, text);
                    GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), textContent, text);
                    GUI.Label(new Rect(r.x, r.y + 1, r.width, r.height), textContent, text);
                    GUI.Label(new Rect(r.x + 1, r.y + 2, r.width, r.height), textContent, text);
                    text.normal.textColor = main;
                    GUI.Label(r, textContent, text);
                    text.normal.textColor = old;
                }

                return x + width + 2;
            }

            float Icon(Rect root, string key, float x, float y, int size, float opacity, Color color, float scale = 1f)
            {
                if (string.Equals(key, "scav", StringComparison.OrdinalIgnoreCase)) scale *= 1.18f;
                float px = Mathf.Max(11f, (size + 6) * scale);
                if (Event.current.type != EventType.Repaint) return x + px + 2;

                Texture2D texture = icons.Get(key);
                if (texture == null) return x;

                Rect r = new Rect(root.x + x, root.y + y - 2, px, px);
                Rect plate = new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4);
                Color old = GUI.color;
                float iconOpacity = Mathf.Sqrt(Mathf.Clamp01(opacity));
                bool bareStatus = key == "water" || key == "energy" || key == "weight" ||
                                  key == "weight1" || key == "weight2" || key == "weight3";
                if (!bareStatus)
                {
                    GUI.color = new Color(.95f, .96f, .95f, Mathf.Clamp01(iconOpacity * .96f));
                    GUI.DrawTexture(plate, medallionPlate, ScaleMode.StretchToFill, true);
                    Color rim = color;
                    rim.a = Mathf.Clamp01(iconOpacity * .96f);
                    GUI.color = rim;
                    GUI.DrawTexture(plate, medallionRing, ScaleMode.StretchToFill, true);
                }

                GUI.color = new Color(0, 0, 0, Mathf.Clamp01(iconOpacity * .78f));
                GUI.DrawTexture(new Rect(r.x - 1, r.y, r.width, r.height), texture, ScaleMode.ScaleToFit, true);
                GUI.DrawTexture(new Rect(r.x + 1, r.y, r.width, r.height), texture, ScaleMode.ScaleToFit, true);
                GUI.DrawTexture(new Rect(r.x, r.y - 1, r.width, r.height), texture, ScaleMode.ScaleToFit, true);
                GUI.DrawTexture(new Rect(r.x, r.y + 1, r.width, r.height), texture, ScaleMode.ScaleToFit, true);

                Color c = color;
                c.a *= iconOpacity;
                GUI.color = c;
                GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit, true);
                GUI.color = old;
                return x + px + 2;
            }

            static float Gap(float x, float amount) => x + amount;
            static bool Vertical(ConfigEntry<string> layout) => string.Equals(layout.Value, "Vertical", StringComparison.OrdinalIgnoreCase);

            static string CachedInt(int value, ref int cachedValue, ref string cachedText)
            {
                if (value != cachedValue || cachedText == null)
                {
                    cachedValue = value;
                    cachedText = value.ToString();
                }
                return cachedText;
            }

            void EditSurface(int id, Rect r, ConfigEntry<float> xEntry, ConfigEntry<float> yEntry, bool fromBottom)
            {
                CacheEditRect(id, r);
                if (!runtime.editMode.Value) return;

                if (Event.current.type == EventType.Repaint)
                {
                    Color old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, .045f);
                    GUI.Box(r, string.Empty);
                    GUI.color = old;
                }

                Event e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    dragCluster = id;
                    dragOffset = e.mousePosition - new Vector2(r.x, r.y);
                    e.Use();
                }

                if (e.type == EventType.MouseDrag && dragCluster == id)
                {
                    Vector2 p = e.mousePosition - dragOffset;
                    p.x = Mathf.Clamp(p.x, -r.width + 8, Screen.width - 8);
                    p.y = Mathf.Clamp(p.y, -r.height + 6, Screen.height - 6);
                    if (xEntry != null) xEntry.Value = p.x;
                    if (yEntry != null) yEntry.Value = fromBottom ? Screen.height - p.y - r.height : p.y;
                    CacheEditRect(id, new Rect(p.x, p.y, r.width, r.height));
                    e.Use();
                }

                if (e.type == EventType.MouseUp && dragCluster == id)
                {
                    dragCluster = 0;
                    try { runtime.Config.Save(); } catch { }
                    e.Use();
                }
            }

            void CacheEditRect(int id, Rect r)
            {
                if (id == 1) { populationEditRect = r; populationEditRectValid = true; }
                else if (id == 2) { statusEditRect = r; statusEditRectValid = true; }
                else if (id == 3) { killEditRect = r; killEditRectValid = true; }
            }

            void DrawPopulation()
            {
                int size = runtime.popSize.Value;
                float opacity = runtime.popOpacity.Value;
                bool vertical = Vertical(runtime.popLayout);
                float rowHeight = size + 14;
                float height = vertical ? rowHeight * 4 : size + 9;
                Rect root = new Rect(runtime.popX.Value, Screen.height - runtime.popY.Value - height, vertical ? 90 : 300, height);
                float x = 0;
                string currentPmc = CachedInt(runtime.pmc, ref cachedPmc, ref pmcText);
                string currentScav = CachedInt(runtime.scav, ref cachedScav, ref scavText);
                string currentBoss = CachedInt(runtime.boss, ref cachedBoss, ref bossText);
                string currentRaider = CachedInt(runtime.reinforced, ref cachedRaider, ref raiderText);

                if (vertical)
                {
                    float maxX = 0;
                    x = Icon(root, "usec", 0, 0, size, opacity, runtime.pmcColor.Value, .90f);
                    x = Text(root, currentPmc, Gap(x, 3), 0, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    x = Icon(root, "scav", 0, rowHeight, size, opacity, runtime.scavColor.Value, .90f);
                    x = Text(root, currentScav, Gap(x, 3), rowHeight, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    x = Icon(root, "boss", 0, rowHeight * 2, size, opacity, runtime.bossColor.Value, .90f);
                    x = Text(root, currentBoss, Gap(x, 3), rowHeight * 2, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    x = Icon(root, "raider", 0, rowHeight * 3, size, opacity, runtime.reinforcedColor.Value, .90f);
                    x = Text(root, currentRaider, Gap(x, 3), rowHeight * 3, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    EditSurface(1, new Rect(root.x, root.y, Mathf.Max(28, maxX), height), runtime.popX, runtime.popY, true);
                    return;
                }

                x = Icon(root, "usec", x, 0, size, opacity, runtime.pmcColor.Value, .90f); x = Gap(x, 3);
                x = Text(root, currentPmc, x, 0, size, opacity, Neutral); x = Gap(x, 7);
                x = Icon(root, "scav", x, 0, size, opacity, runtime.scavColor.Value, .90f); x = Gap(x, 3);
                x = Text(root, currentScav, x, 0, size, opacity, Neutral); x = Gap(x, 7);
                x = Icon(root, "boss", x, 0, size, opacity, runtime.bossColor.Value, .90f); x = Gap(x, 3);
                x = Text(root, currentBoss, x, 0, size, opacity, Neutral); x = Gap(x, 7);
                x = Icon(root, "raider", x, 0, size, opacity, runtime.reinforcedColor.Value, .90f); x = Gap(x, 3);
                x = Text(root, currentRaider, x, 0, size, opacity, Neutral);
                EditSurface(1, new Rect(root.x, root.y, Mathf.Max(28, x), height), runtime.popX, runtime.popY, true);
            }

            void DrawStatus()
            {
                int size = runtime.statusSize.Value;
                float opacity = runtime.statusOpacity.Value;
                bool vertical = Vertical(runtime.statusLayout);
                float rowHeight = size + 14;
                float height = vertical ? rowHeight * 3 : size + 9;
                Rect root = new Rect(runtime.statusX.Value, Screen.height - runtime.statusY.Value - height, vertical ? 125 : 300, height);
                string currentHydration = CachedInt(Mathf.RoundToInt(runtime.hydration), ref cachedHydration, ref hydrationText);
                string currentEnergy = CachedInt(Mathf.RoundToInt(runtime.energy), ref cachedEnergy, ref energyText);
                string currentWeight = CachedInt(Mathf.RoundToInt(runtime.weight), ref cachedWeight, ref weightText);

                Color weightColor = runtime.weightOk.Value;
                int severity = 1;
                if (runtime.overweightLimit > 0 && runtime.weight >= runtime.overweightLimit) { weightColor = runtime.weightHeavy.Value; severity = 2; }
                if (runtime.walkDrainLimit > 0 && runtime.weight >= runtime.walkDrainLimit) { weightColor = runtime.weightCritical.Value; severity = 3; }

                float x = 0;
                if (vertical)
                {
                    float maxX = 0;
                    x = Icon(root, "water", 0, 0, size, opacity, Water, .90f);
                    x = Text(root, currentHydration, Gap(x, 3), 0, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    x = Icon(root, "energy", 0, rowHeight, size, opacity, Energy, .90f);
                    x = Text(root, currentEnergy, Gap(x, 3), rowHeight, size, opacity, Neutral); maxX = Mathf.Max(maxX, x);
                    x = Icon(root, "weight", 0, rowHeight * 2, size, opacity, WeightInk, 1.05f);
                    x = Text(root, currentWeight, Gap(x, 3), rowHeight * 2, size, opacity, Neutral);
                    x = Text(root, "kg", x, rowHeight * 2 + 1, Mathf.Max(8, size - 2), opacity, Muted, .82f);
                    x = Icon(root, WeightIcons[severity], Gap(x, 3), rowHeight * 2 + 2, size, opacity, weightColor, .70f);
                    maxX = Mathf.Max(maxX, x);
                    EditSurface(2, new Rect(root.x, root.y, Mathf.Max(28, maxX), height), runtime.statusX, runtime.statusY, true);
                    return;
                }

                x = Icon(root, "water", x, 0, size, opacity, Water, .90f);
                x = Text(root, currentHydration, Gap(x, 3), 0, size, opacity, Neutral); x = Gap(x, 8);
                x = Icon(root, "energy", x, 0, size, opacity, Energy, .90f);
                x = Text(root, currentEnergy, Gap(x, 3), 0, size, opacity, Neutral); x = Gap(x, 8);
                x = Icon(root, "weight", x, 0, size, opacity, WeightInk, 1.05f);
                x = Text(root, currentWeight, Gap(x, 3), 0, size, opacity, Neutral);
                x = Text(root, "kg", x, 1, Mathf.Max(8, size - 2), opacity, Muted, .82f);
                x = Icon(root, WeightIcons[severity], Gap(x, 3), 2, size, opacity, weightColor, .70f);
                EditSurface(2, new Rect(root.x, root.y, Mathf.Max(28, x), height), runtime.statusX, runtime.statusY, true);
            }

            void DrawKillFeed(bool editing)
            {
                int size = runtime.killSize.Value;
                int max = runtime.killMax.Value;
                float opacity = runtime.killOpacity.Value;
                float life = runtime.killLifetime.Value;
                string displayMode = runtime.killMode.Value;

                int count = Mathf.Min(max, runtime.kills.Count);
                int rows = editing ? Mathf.Max(1, count) : count;
                float width = displayMode == "Detailed" ? 235f : displayMode == "Minimal" ? 88f : 205f;
                float rowHeight = size + 14;
                Rect root = new Rect(runtime.killX.Value, runtime.killY.Value, width, rowHeight * Mathf.Max(1, rows));

                if (rows > 0) EditSurface(3, root, runtime.killX, runtime.killY, false);

                if (runtime.kills.Count == 0)
                {
                    if (editing)
                        DrawKillRow(root, "Self", "Scav", "AK-105", "head", "187m", true, 0, 1f, displayMode, size, opacity);
                    return;
                }

                int shown = 0;
                float fadeWindow = Mathf.Max(.25f, Mathf.Min(1.4f, life));
                for (int i = runtime.kills.Count - 1; i >= 0 && shown < max; i--, shown++)
                {
                    KillLine k = runtime.kills[i];
                    float age = Time.unscaledTime - k.Created;
                    float fade = Mathf.Clamp01((life - age) / fadeWindow);
                    DrawKillRow(root, k.Killer, k.Victim, k.WeaponText, k.HitIcon, k.DistanceText, k.HasDistance,
                        shown, fade, displayMode, size, opacity);
                }
            }

            void DrawKillRow(Rect r, string killer, string victim, string weaponText, string hitIcon, string distanceText,
                bool hasDistance, int row, float fade, string displayMode, int size, float opacity)
            {
                float y = row * (size + 14);
                float x = 0;
                float op = opacity * fade;
                Color killerColor = RoleColor(killer);
                Color victimColor = RoleColor(victim);

                x = Icon(r, RoleIcon(killer), x, y, size, op, killerColor, 1f);
                x = Gap(x, 5);

                if (displayMode != "Minimal")
                {
                    x = Text(r, weaponText, x, y, Mathf.Max(9, size), op, WeaponTextColor, 1f);
                    x = Gap(x, 5);
                }

                x = Icon(r, RoleIcon(victim), x, y, size, op, victimColor, 1f);

                if (displayMode != "Minimal")
                {
                    if (displayMode == "Detailed")
                    {
                        x = Gap(x, 6);
                        x = Icon(r, hitIcon, x, y, size, op, hitIcon == "head" ? Head : Muted, 1f);
                    }
                    if (hasDistance)
                    {
                        x = Gap(x, 1);
                        Text(r, distanceText, x, y + 1, Mathf.Max(8, size - 1), op, Muted, .90f);
                    }
                }
            }

            static Color RoleColor(string role)
            {
                if (role == "USEC" || role == "BEAR" || role == "PMC") return KillPmc;
                if (role == "Scav") return KillScav;
                if (role == "Boss") return KillBoss;
                if (role == "Raider") return KillRaider;
                if (role == "Self") return KillSelf;
                return Neutral;
            }

            static string RoleIcon(string role)
            {
                if (role == "BEAR") return "bear";
                if (role == "Scav") return "scav";
                if (role == "Boss") return "boss";
                if (role == "Raider") return "raider";
                if (role == "Self") return "self";
                return "usec";
            }

            internal static string HitKey(string hit)
            {
                if (string.IsNullOrEmpty(hit)) return "torso";
                if (Contains(hit, "head")) return "head";
                if (Contains(hit, "leftarm") || Contains(hit, "left arm")) return "left_arm";
                if (Contains(hit, "rightarm") || Contains(hit, "right arm")) return "right_arm";
                if (Contains(hit, "leftleg") || Contains(hit, "left leg")) return "left_leg";
                if (Contains(hit, "rightleg") || Contains(hit, "right leg")) return "right_leg";
                if (Contains(hit, "arm")) return "left_arm";
                if (Contains(hit, "leg")) return "left_leg";
                if (Contains(hit, "stomach")) return "stomach";
                return "torso";
            }

            internal static string CleanWeapon(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return "?";
                string s = raw.Trim();
                int bracket = s.IndexOf('[');
                if (bracket >= 0) s = s.Substring(0, bracket).Trim();
                s = s.Replace("ShortName", string.Empty).Replace("Template", string.Empty).Trim(' ', '[', ']', '(', ')', '{', '}');
                if (s.Length == 0) return "?";

                string compact = s.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
                bool hexLike = compact.Length >= 20;
                for (int i = 0; i < compact.Length && hexLike; i++)
                    if (!Uri.IsHexDigit(compact[i])) hexLike = false;
                if (hexLike) return "?";

                string lower = s.ToLowerInvariant();
                string[] noise = { "assault rifle", "assault carbine", "marksman rifle", "sniper rifle", "submachine gun", "machine gun", "shotgun", "pistol", "carbine", "rifle", "weapon" };
                for (int i = 0; i < noise.Length; i++)
                {
                    int index = lower.IndexOf(noise[i], StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        s = (s.Substring(0, index) + s.Substring(index + noise[i].Length)).Trim(' ', '-', ':');
                        lower = s.ToLowerInvariant();
                    }
                }

                int caliber = s.IndexOf(" 5.", StringComparison.Ordinal);
                if (caliber < 0) caliber = s.IndexOf(" 7.", StringComparison.Ordinal);
                if (caliber < 0) caliber = s.IndexOf(" 9x", StringComparison.OrdinalIgnoreCase);
                if (caliber > 0) s = s.Substring(0, caliber).Trim();

                while (s.Contains("  ")) s = s.Replace("  ", " ");
                if (s.Length > 14) s = s.Substring(0, 14).Trim();
                return string.IsNullOrEmpty(s) ? "?" : s;
            }

            static bool Contains(string value, string token) => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

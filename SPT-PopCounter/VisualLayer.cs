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
            static readonly Color Neutral = new Color(.80f, .82f, .81f, 1f);
            static readonly Color Muted = new Color(.58f, .60f, .59f, 1f);
            static readonly Color Head = new Color(.84f, .33f, .30f, 1f);
            static readonly Color Water = new Color(.49f, .69f, .86f, 1f);
            static readonly Color Energy = new Color(.86f, .70f, .31f, 1f);

            readonly Plugin runtime;
            readonly HudIcons icons;
            readonly Font hudFont;
            GUIStyle text;
            int dragCluster;
            Vector2 dragOffset;

            public HudVisualRenderer(Plugin runtime)
            {
                this.runtime = runtime;
                icons = new HudIcons();
                try
                {
                    hudFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Bahnschrift SemiCondensed", "Bahnschrift", "Arial Narrow", "Arial" }, 14);
                }
                catch
                {
                    hudFont = null;
                }
            }

            public void Render()
            {
                bool debug = runtime.workAlways.Value;
                bool editing = runtime.editMode.Value;

                if ((runtime.inRaid || debug) && (runtime.mode >= 1 || editing) && runtime.popEnabled.Value)
                    DrawPopulation();

                if ((runtime.inRaid || debug || runtime.statusOutside.Value) &&
                    (runtime.mode >= 2 || editing) && runtime.statusEnabled.Value)
                    DrawStatus();

                if ((runtime.inRaid || debug) && runtime.killEnabled.Value)
                    DrawKillFeed(editing);
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
                float width = text.CalcSize(new GUIContent(value)).x;
                Rect r = new Rect(root.x + x, root.y + y, width + 4, size + 7);
                Color old = text.normal.textColor;

                text.normal.textColor = new Color(0, 0, 0, Mathf.Clamp01(effective * .94f));
                int[,] offsets = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 }, { 1, 1 } };
                for (int i = 0; i < 8; i++)
                    GUI.Label(new Rect(r.x + offsets[i, 0], r.y + offsets[i, 1], r.width, r.height), value, text);

                text.normal.textColor = new Color(0, 0, 0, Mathf.Clamp01(effective * .38f));
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), value, text);

                text.normal.textColor = main;
                GUI.Label(r, value, text);
                text.normal.textColor = old;
                return x + width + 2;
            }

            float Icon(Rect root, string key, float x, float y, int size, float opacity, Color color, float scale = 1f)
            {
                Texture2D texture = icons.Get(key);
                if (texture == null) return x;

                float px = Mathf.Max(11f, (size + 6) * scale);
                Rect r = new Rect(root.x + x, root.y + y - 2, px, px);
                Color old = GUI.color;

                GUI.color = new Color(0, 0, 0, Mathf.Clamp01(opacity * .70f));
                GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width, r.height), texture, ScaleMode.ScaleToFit, true);

                Color c = color;
                c.a *= opacity;
                GUI.color = c;
                GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit, true);
                GUI.color = old;
                return x + px + 2;
            }

            static float Gap(float x, float amount) => x + amount;

            void EditSurface(int id, Rect r, ConfigEntry<float> xEntry, ConfigEntry<float> yEntry, bool fromBottom)
            {
                if (!runtime.editMode.Value) return;

                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, .045f);
                GUI.Box(r, string.Empty);
                GUI.color = old;

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
                    e.Use();
                }

                if (e.type == EventType.MouseUp && dragCluster == id)
                {
                    dragCluster = 0;
                    try { runtime.Config.Save(); } catch { }
                    e.Use();
                }
            }

            void DrawPopulation()
            {
                int size = runtime.popSize.Value;
                float opacity = runtime.popOpacity.Value;
                float height = size + 9;
                Rect root = new Rect(runtime.popX.Value, Screen.height - runtime.popY.Value - height, 260, height);
                float x = 0;

                x = Icon(root, "usec", x, 0, size, opacity, runtime.pmcColor.Value, .90f);
                x = Text(root, runtime.pmc.ToString(), x, 0, size, opacity, Neutral);
                x = Gap(x, 7);
                x = Icon(root, "scav", x, 0, size, opacity, runtime.scavColor.Value, .90f);
                x = Text(root, runtime.scav.ToString(), x, 0, size, opacity, Neutral);
                x = Gap(x, 7);
                x = Icon(root, "boss", x, 0, size, opacity, runtime.bossColor.Value, .90f);
                x = Text(root, runtime.boss.ToString(), x, 0, size, opacity, Neutral);
                x = Gap(x, 7);
                x = Icon(root, "raider", x, 0, size, opacity, runtime.reinforcedColor.Value, .90f);
                x = Text(root, runtime.reinforced.ToString(), x, 0, size, opacity, Neutral);

                EditSurface(1, new Rect(root.x, root.y, Mathf.Max(28, x), height), runtime.popX, runtime.popY, true);
            }

            void DrawStatus()
            {
                int size = runtime.statusSize.Value;
                float opacity = runtime.statusOpacity.Value;
                float height = size + 9;
                Rect root = new Rect(runtime.statusX.Value, Screen.height - runtime.statusY.Value - height, 260, height);
                float x = 0;

                x = Icon(root, "water", x, 0, size, opacity, Water, .90f);
                x = Text(root, Mathf.RoundToInt(runtime.hydration).ToString(), x, 0, size, opacity, Neutral);
                x = Gap(x, 8);
                x = Icon(root, "energy", x, 0, size, opacity, Energy, .90f);
                x = Text(root, Mathf.RoundToInt(runtime.energy).ToString(), x, 0, size, opacity, Neutral);
                x = Gap(x, 8);

                Color weightColor = runtime.weightOk.Value;
                int severity = 1;
                if (runtime.overweightLimit > 0 && runtime.weight >= runtime.overweightLimit)
                {
                    weightColor = runtime.weightHeavy.Value;
                    severity = 2;
                }
                if (runtime.walkDrainLimit > 0 && runtime.weight >= runtime.walkDrainLimit)
                {
                    weightColor = runtime.weightCritical.Value;
                    severity = 3;
                }

                x = Icon(root, "weight", x, 0, size, opacity, Muted, .86f);
                x = Text(root, Mathf.RoundToInt(runtime.weight).ToString(), x, 0, size, opacity, Neutral);
                x = Text(root, "kg", x, 1, Mathf.Max(8, size - 2), opacity, Muted, .82f);
                x = Gap(x, 3);
                x = Icon(root, "weight" + severity, x, 0, size, opacity, weightColor, .70f);

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
                float width = displayMode == "Detailed" ? 330f : displayMode == "Minimal" ? 145f : 245f;
                float rowHeight = size + 8;
                Rect root = new Rect(runtime.killX.Value, runtime.killY.Value, width, rowHeight * Mathf.Max(1, rows));

                if (rows > 0)
                    EditSurface(3, root, runtime.killX, runtime.killY, false);

                if (runtime.kills.Count == 0)
                {
                    if (editing)
                        DrawKillRow(root, "USEC", "Scav", "AK-74", "Head", 187f, true, 0, 1f, displayMode, size, opacity);
                    return;
                }

                int shown = 0;
                float fadeWindow = Mathf.Max(.25f, Mathf.Min(1.4f, life));
                for (int i = runtime.kills.Count - 1; i >= 0 && shown < max; i--, shown++)
                {
                    KillLine k = runtime.kills[i];
                    float age = Time.unscaledTime - k.Created;
                    float fade = Mathf.Clamp01((life - age) / fadeWindow);
                    DrawKillRow(root, k.Killer, k.Victim, CleanWeapon(k.Weapon), k.Hit, k.Distance, k.HasDistance,
                        shown, fade, displayMode, size, opacity);
                }
            }

            void DrawKillRow(Rect r, string killer, string victim, string weapon, string hit, float distance,
                bool hasDistance, int row, float fade, string displayMode, int size, float opacity)
            {
                float y = row * (size + 8);
                float x = 0;
                float op = opacity * fade;
                Color killerColor = RoleColor(killer);
                Color victimColor = RoleColor(victim);

                x = Icon(r, RoleIcon(killer), x, y, size, op, killerColor, .86f);
                x = Text(r, ShortRole(killer), x, y, size, op, killerColor, .98f);
                x = Gap(x, 7);

                if (displayMode != "Minimal")
                {
                    x = Icon(r, WeaponKey(weapon), x, y, size, op, Neutral, .96f);
                    if (displayMode == "Detailed" && weapon != "?")
                    {
                        x = Text(r, weapon, x, y + 1, Mathf.Max(8, size - 1), op, Muted, .94f);
                        x = Gap(x, 6);
                    }
                    else
                    {
                        x = Gap(x, 4);
                    }
                }

                x = Icon(r, RoleIcon(victim), x, y, size, op, victimColor, .86f);
                x = Text(r, ShortRole(victim), x, y, size, op, victimColor, .98f);

                if (displayMode != "Minimal")
                {
                    x = Gap(x, 6);
                    string hitKey = HitKey(hit);
                    x = Icon(r, hitKey, x, y, size, op, hitKey == "head" ? Head : Muted, .76f);
                    if (hasDistance)
                    {
                        x = Gap(x, 1);
                        Text(r, Mathf.RoundToInt(distance) + "m", x, y + 1, Mathf.Max(8, size - 1), op, Muted, .90f);
                    }
                }
            }

            static string ShortRole(string role)
            {
                if (role == "USEC") return "USEC";
                if (role == "BEAR") return "BEAR";
                if (role == "Scav") return "SCAV";
                if (role == "Boss") return "BOSS";
                if (role == "Raider") return "RAID";
                if (role == "PMC") return "PMC";
                return "?";
            }

            static Color RoleColor(string role)
            {
                if (role == "USEC" || role == "BEAR" || role == "PMC") return KillPmc;
                if (role == "Scav") return KillScav;
                if (role == "Boss") return KillBoss;
                if (role == "Raider") return KillRaider;
                return Neutral;
            }

            static string RoleIcon(string role)
            {
                if (role == "BEAR") return "bear";
                if (role == "Scav") return "scav";
                if (role == "Boss") return "boss";
                if (role == "Raider") return "raider";
                return "usec";
            }

            static string HitKey(string hit)
            {
                hit = (hit ?? string.Empty).ToLowerInvariant();
                if (hit.Contains("head")) return "head";
                if (hit.Contains("arm")) return "arm";
                if (hit.Contains("leg")) return "leg";
                if (hit.Contains("stomach")) return "stomach";
                return "torso";
            }

            static string CleanWeapon(string raw)
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
                if (s.Length > 22) s = s.Substring(0, 22).Trim();
                return s;
            }

            static string WeaponKey(string weapon)
            {
                string w = (weapon ?? string.Empty).ToLowerInvariant();
                if (w.Contains("ak") || w.Contains("rpk") || w.Contains("rd-704") || w.Contains("vpo-136") || w.Contains("vpo-209")) return "ak";
                if (w.Contains("m4") || w.Contains("hk416") || w.Contains("hk 416") || w.Contains("adar") || w.Contains("tx-15") || w.Contains("tx15") || w.Contains("m16") || w.Contains("mdr") || w.Contains("scar") || w.Contains("aug") || w.Contains("g36") || w.Contains("mcx")) return "ar";
                if (w.Contains("mp5") || w.Contains("mp7") || w.Contains("mp9") || w.Contains("pp-") || w.Contains("pp19") || w.Contains("vector") || w.Contains("ump") || w.Contains("p90") || w.Contains("kedr") || w.Contains("klin")) return "smg";
                if (w.Contains("saiga-12") || w.Contains("mp-133") || w.Contains("mp-153") || w.Contains("mp-155") || w.Contains("m870") || w.Contains("590a1") || w.Contains("ks-23") || w.Contains("benelli")) return "shotgun";
                if (w.Contains("svd") || w.Contains("m700") || w.Contains("dvl") || w.Contains("t-5000") || w.Contains("mosin") || w.Contains("axmc") || w.Contains("vpo-215") || w.Contains("sv-98")) return "sniper";
                if (w.Contains("glock") || w.Contains("p226") || w.Contains("m9") || w.Contains("tt") || w.Contains("usp") || w.Contains("five-seven") || w.Contains("1911") || w.Contains("aps") || w.Contains("pm pistol")) return "pistol";
                return "weapon";
            }
        }
    }
}

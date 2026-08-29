using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.tacticalhud.fullcensus", "Admiral Tactical HUD - Full Census", "1.13.3")]
    public sealed class FullCensusPlugin : BaseUnityPlugin
    {
        enum Bucket { Pmc, Scav, Raider, Rogue, Boss, Guard, Goon, Cultist, Infected, Btr, Other }
        enum RowVis { Always, WhenPresent, Hidden }

        struct CensusRow
        {
            public string Label;
            public int Value;
            public string Icon;
            public bool Accent;
            public bool Rule;

            public CensusRow(string label, int value, string icon, bool accent = false, bool rule = false)
            {
                Label = label;
                Value = value;
                Icon = icon;
                Accent = accent;
                Rule = rule;
            }
        }

        ConfigEntry<bool> enabled, onlyInRaid, showIcons, useTarkovFont, splitRogue, splitBoss;
        ConfigEntry<int> fontSize, offsetRight, offsetTop;
        ConfigEntry<float> backgroundOpacity, iconScale;
        ConfigEntry<string> interval;
        ConfigEntry<KeyboardShortcut> toggleKey;
        ConfigEntry<RowVis> showPmc, showScav, showRaider, showRogue, showBoss, showGuard, showGoon, showCultist,
            showInfected, showBtr, showOther, showTotal;

        readonly int[] counts = new int[11];
        readonly Dictionary<string, int> customFactions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly List<CensusRow> rows = new List<CensusRow>(20);
        readonly Dictionary<Type, Dictionary<string, MemberInfo>> memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();
        readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        readonly HudIcons icons = new HudIcons();

        Type worldType, singletonType;
        PropertyInfo singletonInstance;
        MemberInfo playersMember;
        object world;
        float nextWorldPoll, nextRecount, fade;
        bool inRaid;

        GUIStyle labelStyle, valueStyle, titleStyle;
        Texture2D pixel;
        Font font;
        bool fontSearched;

        static readonly Color Background = new Color(.055f,.055f,.050f,1f);
        static readonly Color Border = new Color(.62f,.57f,.44f,.35f);
        static readonly Color Accent = new Color(.78f,.65f,.42f,1f);
        static readonly Color LabelColor = new Color(.72f,.70f,.62f,1f);
        static readonly Color ValueColor = new Color(.88f,.86f,.80f,1f);
        static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        void Awake()
        {
            enabled = Config.Bind("1. General", "Enable Full Census", false,
                "Full BotCensus-style population panel. Disable the compact Population section in Admiral Tactical HUD when using this mode.");
            onlyInRaid = Config.Bind("1. General", "Only In Raid", true, "Hide the full census outside raid/hideout.");
            toggleKey = Config.Bind("1. General", "Toggle Key", new KeyboardShortcut(KeyCode.None), "Optional key to toggle Full Census.");

            fontSize = Config.Bind("2. Display", "Font Size", 14, new ConfigDescription("Overall Full Census scale.", new AcceptableValueRange<int>(10,32)));
            offsetRight = Config.Bind("2. Display", "Offset Right", 20, "Distance from right edge.");
            offsetTop = Config.Bind("2. Display", "Offset Top", 40, "Distance from top edge.");
            backgroundOpacity = Config.Bind("2. Display", "Background Opacity", .62f, new ConfigDescription("0 transparent / 1 opaque", new AcceptableValueRange<float>(0f,1f)));
            showIcons = Config.Bind("2. Display", "Show Icons", true, "Use BotCensus population glyphs where available, with Admiral reserve fallback.");
            iconScale = Config.Bind("2. Display", "Icon Scale", 1f, new ConfigDescription("Icon scale relative to text.", new AcceptableValueRange<float>(.6f,1.5f)));
            useTarkovFont = Config.Bind("2. Display", "Use Tarkov Font", true, "Prefer EFT Bender font, then Bahnschrift/DIN fallback.");

            splitRogue = Config.Bind("3. Rows", "Split Rogue And Raider", true, "Separate Raider and Rogue rows.");
            splitBoss = Config.Bind("3. Rows", "Split Boss And Guard", true, "Separate Boss and Guard rows.");
            showPmc = Vis("PMC", RowVis.Always);
            showScav = Vis("Scav", RowVis.Always);
            showRaider = Vis("Raider", RowVis.WhenPresent);
            showRogue = Vis("Rogue", RowVis.WhenPresent);
            showBoss = Vis("Boss", RowVis.WhenPresent);
            showGuard = Vis("Guard", RowVis.WhenPresent);
            showGoon = Vis("Goons", RowVis.WhenPresent);
            showCultist = Vis("Cultist", RowVis.WhenPresent);
            showInfected = Vis("Infected", RowVis.WhenPresent);
            showBtr = Vis("BTR", RowVis.WhenPresent);
            showOther = Vis("Other", RowVis.WhenPresent);
            showTotal = Vis("Total", RowVis.Always);

            interval = Config.Bind("4. Performance", "Update Interval", "2s",
                new ConfigDescription("How often the full population tally is rebuilt.", new AcceptableValueList<string>("1s","2s","5s","10s")));

            Logger.LogInfo("Admiral Tactical HUD - Full Census v1.13.3 loaded (disabled by default)");
        }

        ConfigEntry<RowVis> Vis(string name, RowVis value) => Config.Bind("3. Rows", name, value,
            "Always = show at zero; WhenPresent = show only when alive; Hidden = never.");

        void Update()
        {
            KeyboardShortcut key = toggleKey.Value;
            if (key.MainKey != KeyCode.None && key.IsDown()) enabled.Value = !enabled.Value;

            if (!enabled.Value)
            {
                fade = 0f;
                world = null;
                inRaid = false;
                return;
            }

            float now = Time.unscaledTime;
            if (now >= nextWorldPoll)
            {
                nextWorldPoll = now + .5f;
                object candidate = GetWorld();
                bool valid = IsRaidWorld(candidate);
                if (inRaid && !valid)
                {
                    rows.Clear();
                    customFactions.Clear();
                    Array.Clear(counts,0,counts.Length);
                    fade = 0f;
                }
                inRaid = valid;
                world = valid ? candidate : null;
            }

            if (!inRaid) return;
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / .35f);

            if (now >= nextRecount)
            {
                nextRecount = now + IntervalSeconds(interval.Value);
                Recount();
            }
        }

        static float IntervalSeconds(string value)
        {
            if (value == "1s") return 1f;
            if (value == "5s") return 5f;
            if (value == "10s") return 10f;
            return 2f;
        }

        void Recount()
        {
            Array.Clear(counts,0,counts.Length);
            customFactions.Clear();
            IEnumerable players = GetPlayers(world);
            if (players == null) { BuildRows(); return; }

            foreach (object player in players)
                Classify(player);

            BuildRows();
        }

        void Classify(object player)
        {
            if (player == null || IsTrue(ReadMember(player,"IsYourPlayer"))) return;
            object ai = ReadMember(player,"IsAI");
            if (ai is bool && !(bool)ai) return;

            object health = ReadMember(player,"HealthController");
            object alive = ReadMember(health,"IsAlive") ?? ReadMember(player,"IsAlive");
            if (alive is bool && !(bool)alive) return;

            object profile = ReadMember(player,"Profile");
            object info = ReadMember(profile,"Info");
            object settings = ReadMember(info,"Settings") ?? ReadMember(profile,"Settings");
            object roleObj = ReadMember(settings,"Role") ?? ReadMember(info,"Role");
            string roleName = roleObj?.ToString() ?? string.Empty;
            int role = RoleNumber(roleObj);

            if (role > 67)
            {
                string faction = RangeFallback(role) ?? "Custom";
                customFactions.TryGetValue(faction,out int current);
                customFactions[faction] = current + 1;
                return;
            }

            string side = (ReadMember(profile,"Side") ?? ReadMember(info,"Side"))?.ToString() ?? string.Empty;
            if (Contains(side,"USEC") || Contains(side,"BEAR"))
            {
                counts[(int)Bucket.Pmc]++;
                return;
            }

            counts[(int)VanillaBucket(role, roleName)]++;
        }

        static Bucket VanillaBucket(int role, string name)
        {
            switch (role)
            {
                case 0: case 1: case 10: case 19: case 37: return Bucket.Scav;
                case 9: return Bucket.Raider;
                case 24: case 34: case 35: return Bucket.Rogue;
                case 26: case 27: case 28: return Bucket.Goon;
                case 60: case 61: case 62: case 63: case 64: return Bucket.Infected;
                case 38: case 40: return Bucket.Boss;
                case 67: return Bucket.Guard;
                case 46: return Bucket.Btr;
                case 18: case 25: case 48: case 49: case 50: case 53: return Bucket.Other;
            }

            if (Contains(name,"boss")) return Bucket.Boss;
            if (Contains(name,"follower")) return Bucket.Guard;
            if (Contains(name,"sectant") || Contains(name,"sectact")) return Bucket.Cultist;
            return Bucket.Other;
        }

        static string RangeFallback(int role)
        {
            if (role >= 848400 && role <= 848405) return "RUAF";
            if (role == 848406) return "Remnant";
            if (role >= 848420 && role <= 848423) return "Black Division";
            if (role >= 848430 && role <= 848431) return "Wedge";
            if (role >= 868588 && role <= 868589) return "Blackout";
            if (role >= 1170 && role <= 1173) return "UNTAR";
            return null;
        }

        void BuildRows()
        {
            rows.Clear();
            Add("PMC", counts[(int)Bucket.Pmc], showPmc, "usec");
            Add("Scav", counts[(int)Bucket.Scav], showScav, "scav");

            if (splitRogue.Value)
            {
                Add("Raider", counts[(int)Bucket.Raider], showRaider, "raider");
                Add("Rogue", counts[(int)Bucket.Rogue], showRogue, "rogue");
            }
            else AddCombined("Raider / Rogue", counts[(int)Bucket.Raider] + counts[(int)Bucket.Rogue], showRaider, "raider");

            if (splitBoss.Value)
            {
                Add("Boss", counts[(int)Bucket.Boss], showBoss, "boss");
                Add("Guard", counts[(int)Bucket.Guard], showGuard, "raider");
            }
            else AddCombined("Boss / Guard", counts[(int)Bucket.Boss] + counts[(int)Bucket.Guard], showBoss, "boss");

            Add("Goons", counts[(int)Bucket.Goon], showGoon, "boss", true);
            Add("Cultist", counts[(int)Bucket.Cultist], showCultist, "boss");
            Add("Infected", counts[(int)Bucket.Infected], showInfected, "scav");
            Add("BTR", counts[(int)Bucket.Btr], showBtr, "rogue");

            foreach (KeyValuePair<string,int> faction in customFactions)
                if (faction.Value > 0) rows.Add(new CensusRow(faction.Key, faction.Value, "usec", true));

            Add("Other", counts[(int)Bucket.Other], showOther, "raider");

            int total = 0;
            for (int i=0;i<counts.Length;i++) total += counts[i];
            foreach (KeyValuePair<string,int> faction in customFactions) total += faction.Value;
            if (showTotal.Value != RowVis.Hidden && (showTotal.Value == RowVis.Always || total > 0))
                rows.Add(new CensusRow("Total Bots", total, "usec", false, true));
        }

        void Add(string label, int value, ConfigEntry<RowVis> vis, string icon, bool accent = false)
        {
            if (vis.Value == RowVis.Hidden) return;
            if (vis.Value == RowVis.WhenPresent && value <= 0) return;
            rows.Add(new CensusRow(label,value,icon,accent));
        }

        void AddCombined(string label, int value, ConfigEntry<RowVis> vis, string icon) => Add(label,value,vis,icon);

        void OnGUI()
        {
            if (!enabled.Value || fade <= 0f) return;
            if (onlyInRaid.Value && !inRaid) return;
            if (Event.current.type != EventType.Repaint) return;
            Draw();
        }

        void Draw()
        {
            if (rows.Count == 0) return;
            int size = fontSize.Value;
            int pad = Mathf.RoundToInt(size * .7f);
            int rowHeight = size + 8;
            int titleSize = Mathf.Max(10,size - 2);
            int titleHeight = titleSize + 10;
            int iconSize = showIcons.Value ? Mathf.Min(Mathf.RoundToInt(size * .95f * iconScale.Value),rowHeight) : 0;
            int iconColumn = showIcons.Value ? iconSize + Mathf.RoundToInt(size * .5f) : 0;
            int ruleGap = Mathf.RoundToInt(size * .4f);
            int rules = 0;
            for (int i=0;i<rows.Count;i++) if (rows[i].Rule) rules++;
            int width = Mathf.RoundToInt(size * 12.5f) + pad * 2 + iconColumn;
            int height = titleHeight + rows.Count * rowHeight + rules * ruleGap + pad;
            float x = Screen.width - width - offsetRight.Value;
            float y = offsetTop.Value;
            Rect panel = new Rect(x,y,width,height);
            float alpha = fade * (2f - fade);

            DrawRect(panel, Fade(Background,backgroundOpacity.Value * alpha));
            DrawBorder(panel, Fade(Border,Border.a * alpha));
            DrawRect(new Rect(panel.x,panel.y,2f,panel.height), Fade(Accent,Accent.a * alpha));

            Font drawFont = useTarkovFont.Value ? ResolveFont() : null;
            EnsureStyles(drawFont,size,titleSize,alpha);
            float left = panel.x + pad + 3f;
            float right = panel.x + width - pad;
            float innerWidth = right - left;
            GUI.Label(new Rect(left,y+4f,innerWidth,titleHeight),"ADMIRAL CENSUS",titleStyle);
            DrawRect(new Rect(left,y+titleHeight+2f,innerWidth,1f),Fade(Border,Border.a*alpha));

            float rowTop = y + titleHeight + 6f;
            for (int i=0;i<rows.Count;i++)
            {
                CensusRow row = rows[i];
                if (row.Rule)
                {
                    DrawRect(new Rect(left,rowTop+ruleGap*.5f,innerWidth,1f),Fade(Border,Border.a*alpha));
                    rowTop += ruleGap;
                }

                Rect rect = new Rect(left,rowTop,innerWidth,rowHeight);
                Color ink = row.Accent ? Fade(Accent,alpha) : Fade(LabelColor,alpha);
                labelStyle.normal.textColor = ink;
                valueStyle.normal.textColor = row.Accent ? Fade(Accent,alpha) : Fade(ValueColor,alpha);

                if (showIcons.Value)
                {
                    Texture2D icon = icons.Get(row.Icon);
                    if (icon != null) DrawIcon(new Rect(rect.x,rect.y+(rowHeight-iconSize)*.5f,iconSize,iconSize),icon,ink);
                }

                Rect textRect = new Rect(rect.x+iconColumn,rect.y,innerWidth-iconColumn,rowHeight);
                GUI.Label(textRect,row.Label.ToUpperInvariant(),labelStyle);
                GUI.Label(textRect,row.Value.ToString(),valueStyle);
                rowTop += rowHeight;
            }
        }

        void EnsureStyles(Font drawFont, int size, int titleSize, float alpha)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, richText = false };
                valueStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, richText = false };
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, richText = false };
            }
            labelStyle.font = valueStyle.font = titleStyle.font = drawFont;
            labelStyle.fontSize = valueStyle.fontSize = size;
            titleStyle.fontSize = titleSize;
            labelStyle.normal.textColor = Fade(LabelColor,alpha);
            valueStyle.normal.textColor = Fade(ValueColor,alpha);
            titleStyle.normal.textColor = Fade(Accent,alpha);
        }

        Font ResolveFont()
        {
            if (font != null || fontSearched) return font;
            fontSearched = true;
            try
            {
                foreach (Font candidate in Resources.FindObjectsOfTypeAll<Font>())
                {
                    if (candidate == null || string.IsNullOrEmpty(candidate.name)) continue;
                    if (candidate.name.IndexOf("Bender",StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        font = candidate;
                        break;
                    }
                }
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift SemiCondensed","Bahnschrift","DIN","Arial Narrow","Arial" },16);
            }
            catch { font = null; }
            return font;
        }

        Texture2D Pixel()
        {
            if (pixel != null) return pixel;
            pixel = new Texture2D(1,1,TextureFormat.RGBA32,false);
            pixel.SetPixel(0,0,Color.white);
            pixel.Apply(false,true);
            pixel.hideFlags = HideFlags.HideAndDontSave;
            return pixel;
        }

        void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect,Pixel(),ScaleMode.StretchToFill,true);
            GUI.color = previous;
        }

        static void DrawIcon(Rect rect, Texture2D texture, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
            GUI.color = previous;
        }

        void DrawBorder(Rect r, Color color)
        {
            DrawRect(new Rect(r.x,r.y,r.width,1f),color);
            DrawRect(new Rect(r.x,r.yMax-1f,r.width,1f),color);
            DrawRect(new Rect(r.x,r.y,1f,r.height),color);
            DrawRect(new Rect(r.xMax-1f,r.y,1f,r.height),color);
        }

        static Color Fade(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }

        object GetWorld()
        {
            worldType = worldType ?? FindType("EFT.GameWorld");
            singletonType = singletonType ?? FindType("Comfort.Common.Singleton`1");
            if (worldType == null || singletonType == null) return null;
            try
            {
                if (singletonInstance == null)
                    singletonInstance = singletonType.MakeGenericType(worldType).GetProperty("Instance",BindingFlags.Public|BindingFlags.Static);
                return singletonInstance?.GetValue(null,null);
            }
            catch { return null; }
        }

        bool IsRaidWorld(object candidate)
        {
            if (!Usable(candidate)) return false;
            string type = candidate.GetType().FullName ?? string.Empty;
            if (Contains(type,"Hideout")) return false;
            string scene = string.Empty;
            try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
            return !Contains(scene,"Hideout") && !Contains(scene,"убежищ");
        }

        IEnumerable GetPlayers(object candidate)
        {
            if (candidate == null) return null;
            if (playersMember == null)
            {
                Type type = candidate.GetType();
                playersMember = (MemberInfo)type.GetProperty("RegisteredPlayers",InstanceFlags) ??
                                type.GetProperty("AllPlayers",InstanceFlags) ??
                                (MemberInfo)type.GetField("RegisteredPlayers",InstanceFlags);
            }
            try
            {
                if (playersMember is PropertyInfo p) return p.GetValue(candidate,null) as IEnumerable;
                if (playersMember is FieldInfo f) return f.GetValue(candidate) as IEnumerable;
            }
            catch { }
            return null;
        }

        object ReadMember(object value, string name)
        {
            if (value == null || string.IsNullOrEmpty(name)) return null;
            Type type = value.GetType();
            if (!memberCache.TryGetValue(type,out Dictionary<string,MemberInfo> map))
            {
                map = new Dictionary<string,MemberInfo>(StringComparer.Ordinal);
                memberCache[type] = map;
            }
            if (!map.TryGetValue(name,out MemberInfo member))
            {
                try { member = type.GetProperty(name,InstanceFlags); } catch { member = null; }
                if (member == null) try { member = type.GetField(name,InstanceFlags); } catch { member = null; }
                map[name] = member;
            }
            try
            {
                if (member is PropertyInfo p) return p.GetValue(value,null);
                if (member is FieldInfo f) return f.GetValue(value);
            }
            catch { }
            return null;
        }

        Type FindType(string name)
        {
            if (typeCache.TryGetValue(name,out Type cached)) return cached;
            Type found = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { found = assembly.GetType(name,false); } catch { found = null; }
                if (found != null) break;
            }
            typeCache[name] = found;
            return found;
        }

        static int RoleNumber(object role)
        {
            if (role == null) return int.MinValue;
            try { return Convert.ToInt32(role); } catch { return int.MinValue; }
        }

        static bool IsTrue(object value) => value is bool && (bool)value;
        static bool Usable(object value)
        {
            if (value == null) return false;
            UnityEngine.Object unity = value as UnityEngine.Object;
            return ReferenceEquals(unity,null) || unity;
        }
        static bool Contains(string value, string token) => !string.IsNullOrEmpty(value) && value.IndexOf(token,StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

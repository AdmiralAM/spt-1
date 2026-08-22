using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.tacticalhud", "SPT Tactical HUD", "1.10.3")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        ConfigEntry<bool> workAlways, editMode, popEnabled, statusEnabled, statusOutside, killEnabled, showVersion, killDiagnostics;
        ConfigEntry<KeyboardShortcut> toggleKey;
        ConfigEntry<int> popSize, statusSize, killSize, killMax;
        ConfigEntry<string> killMode;
        ConfigEntry<float> popOpacity, statusOpacity, killOpacity, popX, popY, statusX, statusY, killX, killY, killLifetime;
        ConfigEntry<Color> pmcColor, scavColor, bossColor, reinforcedColor, weightOk, weightHeavy, weightCritical;

        float nextRefresh, nextVersionSearch, nextVersionApply, nextSubscribe;
        int mode, pmc, scav, boss, reinforced;
        bool inRaid, previousRaidState, versionSearchPending, lastShowVersion;
        float hydration, energy, weight, overweightLimit, walkDrainLimit;
        GUIStyle text, shadow;
        Type worldType, singletonType;
        PropertyInfo singletonInstance;
        MemberInfo playersMember;
        Vector2 dragOffset;
        int dragCluster;

        readonly Dictionary<string, Tracked> tracked = new Dictionary<string, Tracked>();
        readonly Dictionary<string, object> playersByProfileId = new Dictionary<string, object>();
        readonly Queue<string> subscribeQueue = new Queue<string>();
        readonly HashSet<string> queuedIds = new HashSet<string>();
        readonly List<KillLine> kills = new List<KillLine>();
        readonly HashSet<string> diagSeen = new HashSet<string>();
        readonly List<VersionTarget> versionTargets = new List<VersionTarget>();

        static readonly Color FixedPmc = new Color(.52f, .72f, .45f, 1f);
        static readonly Color FixedScav = new Color(.72f, .34f, .31f, 1f);
        static readonly Color FixedBoss = new Color(.78f, .50f, .20f, 1f);
        static readonly Color FixedRaider = new Color(.56f, .42f, .70f, 1f);
        static readonly Color FixedNeutral = new Color(.72f, .74f, .74f, 1f);
        static readonly Color FixedHeadshot = new Color(.72f, .25f, .23f, 1f);

        sealed class Tracked
        {
            public object Player, Health, LastDamage, LastAttacker;
            public bool Alive, DeathCaptured, Subscribed;
            public Vector3 Pos;
            public string Kind, LastHit, LastWeapon;
            public EventInfo DiedEvent, DamageEvent, PlayerDamageEvent;
            public Delegate DiedHandler, DamageHandler, PlayerDamageHandler;
        }
        sealed class KillLine { public string Killer, Victim, Weapon, Hit; public float Distance, Created; public bool HasDistance; }
        sealed class VersionTarget { public object Owner; public MemberInfo Member; public string Original; }

        void Awake()
        {
            workAlways = Config.Bind("General", "Work Always", false, "Debug override");
            toggleKey = Config.Bind("General", "Toggle Key", new KeyboardShortcut(KeyCode.F9), "Hidden -> population -> population + status -> hidden");
            editMode = Config.Bind("General", "HUD Edit Mode", false, "Enable compact drag hitboxes");
            showVersion = Config.Bind("General", "Show SPT Version Label", false, "Show/hide native SPT version label");
            lastShowVersion = showVersion.Value;
            versionSearchPending = true;
            nextVersionSearch = Time.unscaledTime + 4f;

            popEnabled = Config.Bind("Population", "Enabled", true, "Population");
            popSize = Size("Population"); popOpacity = Opacity("Population"); popX = X("Population", 8); popY = BottomY("Population", 8);
            pmcColor = C("Population Colors", "PMC", .55f, .72f, .58f); scavColor = C("Population Colors", "Scav", .72f, .48f, .46f); bossColor = C("Population Colors", "Boss", .78f, .60f, .38f); reinforcedColor = C("Population Colors", "Reinforced", .63f, .51f, .72f);

            statusEnabled = Config.Bind("Player Status", "Enabled", true, "Status");
            statusOutside = Config.Bind("Player Status", "Show Outside Raid", false, "Show outside raid if profile data is available");
            statusSize = Size("Player Status"); statusOpacity = Opacity("Player Status"); statusX = X("Player Status", 8); statusY = BottomY("Player Status", 24);
            weightOk = C("Player Status Colors", "Weight OK", .58f, .75f, .52f); weightHeavy = C("Player Status Colors", "Weight Heavy", .78f, .68f, .39f); weightCritical = C("Player Status Colors", "Weight Critical", .75f, .42f, .39f);

            killEnabled = Config.Bind("Kill Feed", "Enabled", true, "Runtime kill feed");
            killDiagnostics = Config.Bind("Kill Feed", "Diagnostics", false, "Log unresolved death attribution fields");
            killMode = Config.Bind("Kill Feed", "Display Mode", "Normal", new ConfigDescription("Minimal / Normal / Detailed", new AcceptableValueList<string>("Minimal", "Normal", "Detailed")));
            killSize = Size("Kill Feed"); killOpacity = Opacity("Kill Feed"); killX = X("Kill Feed", 1500);
            killY = Config.Bind("Kill Feed", "Position Y", 100f, new ConfigDescription("Top Y", new AcceptableValueRange<float>(-100, 2000)));
            killLifetime = Config.Bind("Kill Feed", "Lifetime", 6f, new ConfigDescription("Seconds", new AcceptableValueRange<float>(2, 15)));
            killMax = Config.Bind("Kill Feed", "Max Entries", 3, new ConfigDescription("Lines", new AcceptableValueRange<int>(1, 6)));
            Logger.LogInfo("SPT Tactical HUD v1.10.3 loaded");
        }

        ConfigEntry<int> Size(string s) => Config.Bind(s, "Size", 10, new ConfigDescription("Size", new AcceptableValueRange<int>(8, 20)));
        ConfigEntry<float> Opacity(string s) => Config.Bind(s, "Opacity", .55f, new ConfigDescription("0 invisible / 1 opaque", new AcceptableValueRange<float>(0, 1)));
        ConfigEntry<float> X(string s, float v) => Config.Bind(s, "Position X", v, new ConfigDescription("X", new AcceptableValueRange<float>(-400, 4000)));
        ConfigEntry<float> BottomY(string s, float v) => Config.Bind(s, "Position Y From Bottom", v, new ConfigDescription("Bottom", new AcceptableValueRange<float>(-100, 2000)));
        ConfigEntry<Color> C(string s, string k, float r, float g, float b) => Config.Bind(s, k, new Color(r, g, b, 1), "Muted color");

        void Update()
        {
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .25f; Refresh(); }
            if (inRaid && Time.unscaledTime >= nextSubscribe) { nextSubscribe = Time.unscaledTime + .35f; ProcessOneSubscription(); }
            if (versionSearchPending && Time.unscaledTime >= nextVersionSearch) { versionSearchPending = false; DiscoverVersionTargetsOnce(); }
            if (versionTargets.Count > 0 && Time.unscaledTime >= nextVersionApply) { nextVersionApply = Time.unscaledTime + 1f; ApplyKnownVersionTargets(); }
            if (showVersion.Value != lastShowVersion)
            {
                lastShowVersion = showVersion.Value;
                if (versionTargets.Count == 0) { versionSearchPending = true; nextVersionSearch = Time.unscaledTime + .5f; }
                else ApplyKnownVersionTargets();
            }
            kills.RemoveAll(k => Time.unscaledTime - k.Created > killLifetime.Value);
            if (!inRaid && !workAlways.Value && !statusOutside.Value) { mode = 0; return; }
            if (toggleKey.Value.IsDown()) mode = (mode + 1) % 3;
        }

        void OnDestroy() { foreach (Tracked t in tracked.Values) Unsubscribe(t); }

        void OnGUI()
        {
            RenderVisualHud();
        }

        Rect PopRect() => new Rect(popX.Value, Screen.height - popY.Value - (popSize.Value + 7), Mathf.Max(95, popSize.Value * 12), popSize.Value + 7);
        Rect StatusRect() => new Rect(statusX.Value, Screen.height - statusY.Value - (statusSize.Value + 7), Mathf.Max(125, statusSize.Value * 15), statusSize.Value + 7);
        Rect KillRect() => new Rect(killX.Value, killY.Value, killMode.Value == "Minimal" ? 180 : 290, (killSize.Value + 6) * Mathf.Max(1, killMax.Value));

        void Ensure()
        {
            if (text == null) text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, padding = new RectOffset(), margin = new RectOffset() };
            if (shadow == null) shadow = new GUIStyle(text);
        }

        float Label(Rect r, string s, float x, float y, int sz, float op, Color c)
        {
            text.fontSize = shadow.fontSize = sz; c.a *= op; text.normal.textColor = c; shadow.normal.textColor = new Color(0, 0, 0, op <= 0 ? 0 : Mathf.Min(1, op + .22f));
            float w = text.CalcSize(new GUIContent(s)).x;
            GUI.Label(new Rect(r.x + x + 1, r.y + y + 1, w + 3, sz + 5), s, shadow);
            GUI.Label(new Rect(r.x + x, r.y + y, w + 3, sz + 5), s, text);
            return x + w + 3;
        }

        void BoxDrag(int id, Rect r)
        {
            if (!editMode.Value) return;
            GUI.Box(r, ""); Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { dragCluster = id; dragOffset = e.mousePosition - new Vector2(r.x, r.y); e.Use(); }
            if (e.type == EventType.MouseDrag && dragCluster == id)
            {
                Vector2 p = e.mousePosition - dragOffset; p.x = Mathf.Clamp(p.x, -r.width + 8, Screen.width - 8); p.y = Mathf.Clamp(p.y, -r.height + 6, Screen.height - 6);
                if (id == 1) { popX.Value = p.x; popY.Value = Screen.height - p.y - r.height; }
                else if (id == 2) { statusX.Value = p.x; statusY.Value = Screen.height - p.y - r.height; }
                else { killX.Value = p.x; killY.Value = p.y; }
                e.Use();
            }
            if (e.type == EventType.MouseUp && dragCluster == id) { dragCluster = 0; Config.Save(); e.Use(); }
        }

        void DrawPop(Rect r)
        {
            BoxDrag(1, r); float x = 0;
            x = Label(r, "P " + pmc, x, 0, popSize.Value, popOpacity.Value, pmcColor.Value);
            x = Label(r, "S " + scav, x, 0, popSize.Value, popOpacity.Value, scavColor.Value);
            x = Label(r, "B " + boss, x, 0, popSize.Value, popOpacity.Value, bossColor.Value);
            Label(r, "R " + reinforced, x, 0, popSize.Value, popOpacity.Value, reinforcedColor.Value);
        }

        void DrawStatus(Rect r)
        {
            BoxDrag(2, r); float x = 0;
            x = Label(r, "H " + Mathf.RoundToInt(hydration), x, 0, statusSize.Value, statusOpacity.Value, new Color(.48f, .70f, .88f));
            x = Label(r, "E " + Mathf.RoundToInt(energy), x, 0, statusSize.Value, statusOpacity.Value, new Color(.90f, .72f, .28f));
            Color wc; string load;
            if (overweightLimit <= 0 || weight < overweightLimit) { wc = weightOk.Value; load = "▲"; }
            else if (walkDrainLimit <= 0 || weight < walkDrainLimit) { wc = weightHeavy.Value; load = "▲▲"; }
            else { wc = weightCritical.Value; load = "▲▲▲"; }
            Label(r, "W " + load, x, 0, statusSize.Value, statusOpacity.Value, wc);
        }

        void DrawKills(Rect r, bool editing)
        {
            BoxDrag(3, r);
            if (kills.Count == 0)
            {
                if (editing) DrawKillRow(r, new KillLine { Killer = "USEC", Victim = "Scav", Weapon = "AK-74", Hit = "Head", Distance = 187, HasDistance = true, Created = Time.unscaledTime }, 0, 1f);
                return;
            }
            int start = Math.Max(0, kills.Count - killMax.Value), row = 0;
            for (int i = kills.Count - 1; i >= start; i--)
            {
                KillLine k = kills[i]; float age = Time.unscaledTime - k.Created, fade = Mathf.Clamp01((killLifetime.Value - age) / Mathf.Min(2f, killLifetime.Value));
                DrawKillRow(r, k, row++, fade);
            }
        }

        void DrawKillRow(Rect r, KillLine k, int row, float fade)
        {
            float y = row * (killSize.Value + 6), x = 0, op = killOpacity.Value * fade;
            Color kc = RoleColor(k.Killer), vc = RoleColor(k.Victim);
            x = Label(r, k.Killer, x, y, killSize.Value, op, kc);
            x = Label(r, " [" + (string.IsNullOrEmpty(k.Weapon) ? "?" : k.Weapon) + "] ", x, y, killSize.Value, op, FixedNeutral);
            x = Label(r, k.Victim, x, y, killSize.Value, op, vc);
            if (killMode.Value != "Minimal")
            {
                string hit = string.IsNullOrEmpty(k.Hit) ? "?" : ShortHit(k.Hit);
                x = Label(r, " " + hit, x, y, killSize.Value, op, hit == "HEAD" ? FixedHeadshot : FixedNeutral);
                if (k.HasDistance) Label(r, " " + Mathf.RoundToInt(k.Distance) + "m", x, y, killSize.Value, op, FixedNeutral);
            }
        }

        Color RoleColor(string k)
        {
            if (k == "USEC" || k == "BEAR" || k == "PMC") return FixedPmc;
            if (k == "Scav") return FixedScav;
            if (k == "Boss") return FixedBoss;
            if (k == "Raider") return FixedRaider;
            return FixedNeutral;
        }

        string ShortHit(string h)
        {
            h = (h ?? "").ToLowerInvariant();
            if (h.Contains("head")) return "HEAD";
            if (h.Contains("arm")) return "ARM";
            if (h.Contains("leg")) return "LEG";
            if (h.Contains("stomach")) return "STOM";
            return "TORSO";
        }

        void Refresh()
        {
            try
            {
                object world = GetWorld();
                if (world == null) { SetRaidState(false); return; }
                IEnumerable ps = GetPlayers(world);
                if (ps == null) { SetRaidState(false); return; }
                SetRaidState(true);

                int p = 0, s = 0, b = 0, r = 0; object local = null;
                var seen = new HashSet<string>(); playersByProfileId.Clear();
                foreach (object pl in ps)
                {
                    if (pl == null) continue;
                    string id = PlayerId(pl); if (string.IsNullOrEmpty(id)) id = pl.GetHashCode().ToString();
                    seen.Add(id); playersByProfileId[id] = pl;
                    bool alive = IsAlive(pl); string kind = Kind(pl); Vector3 pos = Position(pl);
                    if (IsTrue(ReadMember(pl, "IsYourPlayer"))) local = pl;
                    else if (alive)
                    {
                        if (kind == "Boss") b++; else if (kind == "Raider") r++; else if (kind == "USEC" || kind == "BEAR" || kind == "PMC") p++; else s++;
                    }

                    Tracked t;
                    if (!tracked.TryGetValue(id, out t))
                    {
                        t = new Tracked { Player = pl, Alive = alive, Pos = pos, Kind = kind };
                        tracked[id] = t;
                        if (queuedIds.Add(id)) subscribeQueue.Enqueue(id);
                    }
                    else
                    {
                        t.Player = pl; t.Pos = pos; t.Kind = kind;
                        if (t.Alive && !alive && !t.DeathCaptured) CaptureDeath(t);
                        t.Alive = alive;
                    }
                }

                foreach (string id in tracked.Keys.Where(x => !seen.Contains(x)).ToList())
                {
                    Tracked t = tracked[id];
                    if (t.Alive && !t.DeathCaptured && (t.LastDamage != null || t.LastAttacker != null)) CaptureDeath(t);
                    Unsubscribe(t); tracked.Remove(id); queuedIds.Remove(id);
                }

                pmc = p; scav = s; boss = b; reinforced = r; RefreshStatus(local);
            }
            catch (Exception ex) { Logger.LogWarning("HUD refresh: " + ex.Message); }
        }

        void SetRaidState(bool value)
        {
            previousRaidState = inRaid; inRaid = value;
            if (inRaid == previousRaidState) return;

            versionTargets.Clear();
            versionSearchPending = true;
            nextVersionSearch = Time.unscaledTime + (inRaid ? 4f : 3f);

            if (!inRaid)
            {
                foreach (Tracked t in tracked.Values) Unsubscribe(t);
                tracked.Clear(); playersByProfileId.Clear(); subscribeQueue.Clear(); queuedIds.Clear(); kills.Clear(); diagSeen.Clear();
                if (!workAlways.Value && !statusOutside.Value) mode = 0;
            }
        }

        void ProcessOneSubscription()
        {
            while (subscribeQueue.Count > 0)
            {
                string id = subscribeQueue.Dequeue(); queuedIds.Remove(id);
                Tracked t;
                if (!tracked.TryGetValue(id, out t) || t.Subscribed || !t.Alive) continue;
                SubscribeEvents(id, t);
                break;
            }
        }

        void SubscribeEvents(string id, Tracked t)
        {
            try
            {
                object hc = ReadMember(t.Player, "HealthController"); if (hc == null) return; t.Health = hc;
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                t.DiedEvent = FindEvent(hc.GetType(), "DiedEvent", flags);
                if (t.DiedEvent != null)
                {
                    t.DiedHandler = BuildEventDelegate(t.DiedEvent.EventHandlerType, "OnTrackedDied", id, 0);
                    t.DiedEvent.AddEventHandler(hc, t.DiedHandler);
                }

                t.DamageEvent = FindEvent(hc.GetType(), "ApplyDamageEvent", flags);
                if (t.DamageEvent != null)
                {
                    t.DamageHandler = BuildEventDelegate(t.DamageEvent.EventHandlerType, "OnTrackedDamage", id, 1);
                    t.DamageEvent.AddEventHandler(hc, t.DamageHandler);
                }

                t.PlayerDamageEvent = FindEvent(hc.GetType(), "OnApplyDamageByPlayer", flags);
                if (t.PlayerDamageEvent != null)
                {
                    t.PlayerDamageHandler = BuildEventDelegate(t.PlayerDamageEvent.EventHandlerType, "OnTrackedPlayerDamage", id, 2);
                    t.PlayerDamageEvent.AddEventHandler(hc, t.PlayerDamageHandler);
                }
                t.Subscribed = true;
            }
            catch (Exception ex) { if (killDiagnostics.Value) Logger.LogWarning("KillFeed subscribe: " + ex.Message); }
        }

        EventInfo FindEvent(Type type, string name, BindingFlags flags)
        {
            EventInfo e = type.GetEvent(name, flags); if (e != null) return e;
            foreach (Type i in type.GetInterfaces()) { e = i.GetEvent(name, flags); if (e != null) return e; }
            for (Type b = type.BaseType; b != null; b = b.BaseType) { e = b.GetEvent(name, flags); if (e != null) return e; }
            return null;
        }

        Delegate BuildEventDelegate(Type handlerType, string method, string id, int modeId)
        {
            MethodInfo invoke = handlerType.GetMethod("Invoke");
            ParameterExpression[] pars = invoke.GetParameters().Select((p, i) => Expression.Parameter(p.ParameterType, "p" + i)).ToArray();
            MethodInfo target = GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Expression call;
            if (modeId == 0) call = Expression.Call(Expression.Constant(this), target, Expression.Constant(id));
            else if (modeId == 1)
            {
                Expression hit = pars.Length > 0 ? Expression.Convert(pars[0], typeof(object)) : Expression.Constant(null, typeof(object));
                Expression data = pars.Length > 2 ? Expression.Convert(pars[2], typeof(object)) : (pars.Length > 1 ? Expression.Convert(pars[1], typeof(object)) : Expression.Constant(null, typeof(object)));
                call = Expression.Call(Expression.Constant(this), target, Expression.Constant(id), hit, data);
            }
            else
            {
                Expression a = pars.Length > 0 ? Expression.Convert(pars[0], typeof(object)) : Expression.Constant(null, typeof(object));
                Expression b = pars.Length > 1 ? Expression.Convert(pars[1], typeof(object)) : Expression.Constant(null, typeof(object));
                call = Expression.Call(Expression.Constant(this), target, Expression.Constant(id), a, b);
            }
            return Expression.Lambda(handlerType, call, pars).Compile();
        }

        void OnTrackedDamage(string id, object hit, object data)
        {
            Tracked t; if (!tracked.TryGetValue(id, out t)) return;
            t.LastHit = hit?.ToString(); t.LastDamage = data;
            object a = ExtractAttacker(data) ?? ResolveAttackerById(data);
            if (a != null)
            {
                t.LastAttacker = a;
                string w = Weapon(a); if (!string.IsNullOrEmpty(w) && w != "?") t.LastWeapon = w;
            }
            if (string.IsNullOrEmpty(t.LastWeapon)) t.LastWeapon = ResolveWeaponFromDamage(data);
        }

        void OnTrackedPlayerDamage(string id, object a, object b)
        {
            Tracked t; if (!tracked.TryGetValue(id, out t)) return;
            object pa = NormalizePlayer(a), pb = NormalizePlayer(b), attacker = null;
            if (pa != null && !ReferenceEquals(pa, t.Player)) attacker = pa;
            else if (pb != null && !ReferenceEquals(pb, t.Player)) attacker = pb;
            if (attacker != null)
            {
                t.LastAttacker = attacker;
                string w = Weapon(attacker); if (!string.IsNullOrEmpty(w) && w != "?") t.LastWeapon = w;
            }
        }

        void OnTrackedDied(string id)
        {
            Tracked t; if (!tracked.TryGetValue(id, out t) || t.DeathCaptured) return;
            CaptureDeath(t); t.Alive = false;
        }

        void CaptureDeath(Tracked t)
        {
            t.DeathCaptured = true;
            object hc = t.Health ?? ReadMember(t.Player, "HealthController");
            object info = FirstNonNull(t.LastDamage, ReadMember(hc, "LastDamageInfo"), ReadMember(t.Player, "LastDamageInfo"), ReadMember(hc, "DamageInfo"), ReadMember(t.Player, "DamageInfo"));
            object attacker = t.LastAttacker ?? ExtractAttacker(info) ?? ResolveAttackerById(info);
            string hit = FirstNonEmpty(t.LastHit, ExtractHit(info));
            string killer = "Unknown", weapon = FirstNonEmpty(t.LastWeapon, ResolveWeaponFromDamage(info), "?"); float dist = 0; bool hasDist = false;
            if (attacker != null && !ReferenceEquals(attacker, t.Player))
            {
                killer = Kind(attacker);
                string liveWeapon = Weapon(attacker); if (!string.IsNullOrEmpty(liveWeapon) && liveWeapon != "?") weapon = liveWeapon;
                Vector3 ap = Position(attacker);
                if (ap != Vector3.zero && t.Pos != Vector3.zero) { dist = Vector3.Distance(ap, t.Pos); hasDist = true; }
            }
            else DiagnoseDeath(t.Player, hc, info);
            kills.Add(new KillLine { Killer = killer, Victim = t.Kind, Weapon = weapon, Hit = hit, Distance = dist, HasDistance = hasDist, Created = Time.unscaledTime });
        }

        string ResolveWeaponFromDamage(object info)
        {
            if (info == null) return null;
            foreach (string n in new[] { "Weapon", "WeaponItem", "SourceItem", "Item", "WeaponTemplate" })
            {
                object v = ReadMember(info, n); if (v == null) continue;
                string direct = v as string; if (!string.IsNullOrEmpty(direct)) return direct;
                object tpl = ReadMember(v, "Template");
                string name = (ReadMember(tpl, "ShortName") ?? ReadMember(tpl, "Name") ?? ReadMember(v, "ShortName") ?? ReadMember(v, "Name"))?.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
            foreach (string n in new[] { "WeaponName", "SourceName", "WeaponId" })
            {
                string s = ReadMember(info, n)?.ToString(); if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        object ExtractAttacker(object info)
        {
            if (info == null) return null;
            foreach (string n in new[] { "Player", "Attacker", "SourcePlayer", "Aggressor", "Killer", "Instigator", "Owner", "Source" })
            {
                object p = NormalizePlayer(ReadMember(info, n)); if (p != null) return p;
            }
            object nested = FirstNonNull(ReadMember(info, "DamageSource"), ReadMember(info, "Weapon"), ReadMember(info, "Bullet"));
            if (nested != null) foreach (string n in new[] { "Player", "Owner", "Attacker", "SourcePlayer" }) { object p = NormalizePlayer(ReadMember(nested, n)); if (p != null) return p; }
            return null;
        }

        object ResolveAttackerById(object info)
        {
            if (info == null) return null;
            foreach (string n in new[] { "SourceId", "AttackerId", "KillerId", "PlayerId", "ProfileId", "SourceProfileId" })
            {
                string id = ReadMember(info, n)?.ToString(); object p;
                if (!string.IsNullOrEmpty(id) && playersByProfileId.TryGetValue(id, out p)) return p;
            }
            object nested = FirstNonNull(ReadMember(info, "DamageSource"), ReadMember(info, "Weapon"), ReadMember(info, "Bullet"));
            if (nested != null)
                foreach (string n in new[] { "SourceId", "AttackerId", "OwnerId", "ProfileId" })
                {
                    string id = ReadMember(nested, n)?.ToString(); object p;
                    if (!string.IsNullOrEmpty(id) && playersByProfileId.TryGetValue(id, out p)) return p;
                }
            return null;
        }

        object NormalizePlayer(object v)
        {
            if (v == null) return null; if (ReadMember(v, "Profile") != null) return v;
            foreach (string n in new[] { "Player", "Owner", "Person", "Controller" }) { object p = ReadMember(v, n); if (p != null && ReadMember(p, "Profile") != null) return p; }
            return null;
        }

        string ExtractHit(object info)
        {
            if (info == null) return null;
            foreach (string n in new[] { "BodyPart", "HitBodyPart", "BodyPartType", "DamageBodyPart", "HitPart" }) { object v = ReadMember(info, n); if (v != null) return v.ToString(); }
            return null;
        }

        void DiagnoseDeath(object victim, object hc, object info)
        {
            if (!killDiagnostics.Value) return;
            string key = (info?.GetType().FullName ?? "null") + "|" + (hc?.GetType().FullName ?? "null"); if (!diagSeen.Add(key)) return;
            Logger.LogWarning("KillFeed unresolved victim=" + victim?.GetType().FullName + " health=" + (hc?.GetType().FullName ?? "null") + " damage=" + (info?.GetType().FullName ?? "null"));
        }

        void Unsubscribe(Tracked t)
        {
            try { if (t.Health != null && t.DiedEvent != null && t.DiedHandler != null) t.DiedEvent.RemoveEventHandler(t.Health, t.DiedHandler); } catch { }
            try { if (t.Health != null && t.DamageEvent != null && t.DamageHandler != null) t.DamageEvent.RemoveEventHandler(t.Health, t.DamageHandler); } catch { }
            try { if (t.Health != null && t.PlayerDamageEvent != null && t.PlayerDamageHandler != null) t.PlayerDamageEvent.RemoveEventHandler(t.Health, t.PlayerDamageHandler); } catch { }
            t.Subscribed = false;
        }

        void RefreshStatus(object pl)
        {
            if (pl == null) return;
            object hc = ReadMember(pl, "HealthController");
            hydration = ReadFloatDeep(hc, "Hydration", "Current") ?? ReadFloatDeep(hc, "Hydration", "Value") ?? hydration;
            energy = ReadFloatDeep(hc, "Energy", "Current") ?? ReadFloatDeep(hc, "Energy", "Value") ?? energy;

            object profile = ReadMember(pl, "Profile"), inv = ReadMember(profile, "Inventory"), skills = ReadMember(profile, "Skills");
            float? normal = ReadWrappedFloat(ReadMember(inv, "TotalWeight"));
            float? elite = ReadWrappedFloat(ReadMember(inv, "TotalWeightEliteSkill"));
            bool eliteBuff = ReadWrappedBool(ReadMember(skills, "StrengthBuffElite")) ?? false;
            weight = (eliteBuff ? elite : normal) ?? normal ?? elite ?? weight;

            object global = GetSingletonInstance(FindType("EFT.GlobalConfiguration") ?? FindTypeByName("GlobalConfiguration"));
            object stamina = ReadMember(global, "Stamina");
            Vector2? baseLimits = ReadVector2(ReadMember(stamina, "BaseOverweightLimits"));
            Vector2? walkLimits = ReadVector2(ReadMember(stamina, "WalkOverweightLimits"));
            float rel = ReadFloat(ReadMember(hc, "CarryingWeightRelativeModifier")) ?? 1f;
            float abs = ReadFloat(ReadMember(hc, "CarryingWeightAbsoluteModifier")) ?? 0f;
            if (baseLimits.HasValue) overweightLimit = baseLimits.Value.x * rel + abs;
            if (walkLimits.HasValue) walkDrainLimit = walkLimits.Value.x * rel + abs;
        }

        void DiscoverVersionTargetsOnce()
        {
            try
            {
                foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                {
                    if (mb == null) continue; Type t = mb.GetType(); string tn = t.Name;
                    if (tn.IndexOf("Text", StringComparison.OrdinalIgnoreCase) < 0 && tn.IndexOf("Label", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    foreach (MemberInfo m in StringMembers(t))
                    {
                        string value = ReadStringMember(mb, m);
                        if (string.IsNullOrEmpty(value) || value.IndexOf("SPT", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        versionTargets.Add(new VersionTarget { Owner = mb, Member = m, Original = value });
                    }
                }
                ApplyKnownVersionTargets();
            }
            catch (Exception ex) { Logger.LogDebug("Version discovery: " + ex.Message); }
        }

        void ApplyKnownVersionTargets()
        {
            foreach (VersionTarget t in versionTargets.ToArray())
            {
                if (t.Owner == null || t.Member == null) continue;
                WriteStringMember(t.Owner, t.Member, showVersion.Value ? t.Original : string.Empty);
            }
        }

        IEnumerable<MemberInfo> StringMembers(Type t)
        {
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) if (p.PropertyType == typeof(string) && p.CanRead && p.CanWrite) yield return p;
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) if (f.FieldType == typeof(string)) yield return f;
        }

        string ReadStringMember(object o, MemberInfo m)
        {
            try { PropertyInfo p = m as PropertyInfo; if (p != null) return p.GetValue(o, null) as string; FieldInfo f = m as FieldInfo; if (f != null) return f.GetValue(o) as string; } catch { }
            return null;
        }

        void WriteStringMember(object o, MemberInfo m, string value)
        {
            try { PropertyInfo p = m as PropertyInfo; if (p != null && p.CanWrite) { p.SetValue(o, value, null); return; } FieldInfo f = m as FieldInfo; if (f != null) f.SetValue(o, value); } catch { }
        }

        string Kind(object p)
        {
            string role = Role(p), side = Side(p);
            if (IsBoss(role)) return "Boss"; if (IsReinforced(role)) return "Raider";
            if (!string.IsNullOrEmpty(side) && side.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0) return "USEC";
            if (!string.IsNullOrEmpty(side) && side.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0) return "BEAR";
            if (IsPmc(side)) return "PMC"; return "Scav";
        }

        string Weapon(object p)
        {
            object hands = ReadMember(p, "HandsController"), item = ReadMember(hands, "Item"), tpl = ReadMember(item, "Template");
            string n = (ReadMember(tpl, "ShortName") ?? ReadMember(tpl, "Name") ?? ReadMember(item, "ShortName") ?? ReadMember(item, "Name"))?.ToString();
            return string.IsNullOrEmpty(n) ? "?" : n;
        }

        Vector3 Position(object p)
        {
            try { object tr = ReadMember(p, "Transform") ?? ReadMember(p, "transform"), v = ReadMember(tr, "position"); if (v is Vector3) return (Vector3)v; } catch { }
            return Vector3.zero;
        }

        string PlayerId(object p)
        {
            object pr = ReadMember(p, "Profile"); return (ReadMember(pr, "Id") ?? ReadMember(pr, "ProfileId") ?? ReadMember(p, "ProfileId"))?.ToString();
        }

        object GetWorld()
        {
            if (worldType == null) worldType = FindType("EFT.GameWorld") ?? FindTypeByName("GameWorld"); if (worldType == null) return null;
            if (singletonType == null) singletonType = FindType("Comfort.Common.Singleton`1"); if (singletonType == null) return null;
            if (singletonInstance == null) singletonInstance = singletonType.MakeGenericType(worldType).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return singletonInstance?.GetValue(null, null);
        }

        IEnumerable GetPlayers(object w)
        {
            if (playersMember == null)
            {
                Type t = w.GetType(); playersMember = (MemberInfo)t.GetProperty("RegisteredPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? t.GetProperty("AllPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? (MemberInfo)t.GetField("RegisteredPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            object v = playersMember is PropertyInfo pi ? pi.GetValue(w, null) : (playersMember as FieldInfo)?.GetValue(w); return v as IEnumerable;
        }

        object GetSingletonInstance(Type t)
        {
            if (t == null) return null; Type s = FindType("Comfort.Common.Singleton`1"); if (s == null) return null;
            try { return s.MakeGenericType(t).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null); } catch { return null; }
        }

        static bool IsAlive(object p) { object h = ReadMember(p, "HealthController"), a = h != null ? ReadMember(h, "IsAlive") : ReadMember(p, "IsAlive"); return !(a is bool) || (bool)a; }
        static string Side(object p) { object pr = ReadMember(p, "Profile"), i = ReadMember(pr, "Info"); return (ReadMember(i, "Side") ?? ReadMember(pr, "Side"))?.ToString(); }
        static string Role(object p) { object pr = ReadMember(p, "Profile"), i = ReadMember(pr, "Info"), s = ReadMember(i, "Settings") ?? ReadMember(pr, "Settings"); return (ReadMember(s, "Role") ?? ReadMember(i, "Role"))?.ToString(); }
        static bool IsPmc(string s) => s != null && (s.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0);
        static bool IsBoss(string r) => !string.IsNullOrEmpty(r) && r.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool IsReinforced(string role) { if (string.IsNullOrEmpty(role)) return false; string r = role.ToLowerInvariant(); return r.Contains("follower") || r.Contains("pmcbot") || r.Contains("exusec") || r.Contains("raider") || r.Contains("rogue") || r.Contains("sectant") || r.Contains("arena") || r.Contains("assaultgroup"); }
        static bool IsTrue(object v) => v is bool && (bool)v;
        static float? ReadFloat(object v) { if (v == null) return null; try { return Convert.ToSingle(v); } catch { return null; } }
        static float? ReadWrappedFloat(object v) { if (v == null) return null; return ReadFloat(ReadMember(v, "Value")) ?? ReadFloat(v); }
        static bool? ReadWrappedBool(object v) { if (v == null) return null; object x = ReadMember(v, "Value") ?? v; return x is bool ? (bool?)x : null; }
        static float? ReadFloatDeep(object o, string a, string b) { object x = ReadMember(o, a); return ReadFloat(ReadMember(x, b)) ?? ReadFloat(x); }
        static Vector2? ReadVector2(object v) { if (v is Vector2) return (Vector2)v; return null; }
        static string FirstNonEmpty(params string[] values) { foreach (string s in values) if (!string.IsNullOrEmpty(s) && s != "?") return s; return values.LastOrDefault(); }
        static object FirstNonNull(params object[] values) { foreach (object v in values) if (v != null) return v; return null; }

        static object ReadMember(object o, string n)
        {
            if (o == null) return null; Type t = o.GetType();
            try { PropertyInfo p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (p != null) return p.GetValue(o, null); } catch { }
            try { FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(o); } catch { }
            return null;
        }

        static Type FindType(string n) { foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) try { Type t = a.GetType(n, false); if (t != null) return t; } catch { } return null; }
        static Type FindTypeByName(string n)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { Type t = a.GetTypes().FirstOrDefault(x => x.Name == n); if (t != null) return t; }
                catch (ReflectionTypeLoadException e) { Type t = e.Types?.FirstOrDefault(x => x != null && x.Name == n); if (t != null) return t; }
                catch { }
            }
            return null;
        }
    }
}

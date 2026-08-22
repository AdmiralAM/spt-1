using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.tacticalhud", "SPT Tactical HUD", "1.13.2")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        ConfigEntry<bool> workAlways, editMode, popEnabled, statusEnabled, statusOutside, killEnabled, showVersion, killDiagnostics;
        ConfigEntry<KeyboardShortcut> toggleKey;
        ConfigEntry<int> savedMode, popSize, statusSize, killSize, killMax;
        ConfigEntry<string> popLayout, statusLayout, killMode;
        ConfigEntry<float> popOpacity, statusOpacity, killOpacity, popX, popY, statusX, statusY, killX, killY, killLifetime;
        ConfigEntry<Color> pmcColor, scavColor, bossColor, reinforcedColor, weightOk, weightHeavy, weightCritical;

        float nextRefresh, nextVersionSearch, nextSubscribe, nextOutsideProfileSearch;
        int mode, pmc, scav, boss, reinforced, versionSearchAttempts;
        bool inRaid, lastShowVersion, versionTextTypesResolved;
        float hydration, energy, weight, overweightLimit, walkDrainLimit;
        Type worldType, singletonType;
        PropertyInfo singletonInstance;
        MemberInfo playersMember;
        object outsideProfile;

        readonly Dictionary<string, Tracked> tracked = new Dictionary<string, Tracked>();
        readonly Dictionary<string, object> playersByProfileId = new Dictionary<string, object>();
        readonly List<object> refreshPlayers = new List<object>(64);
        readonly HashSet<string> refreshSeen = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> refreshRemoved = new List<string>(32);
        readonly Queue<string> subscribeQueue = new Queue<string>();
        readonly HashSet<string> queuedIds = new HashSet<string>();
        readonly List<KillLine> kills = new List<KillLine>();
        readonly HashSet<string> diagSeen = new HashSet<string>();
        readonly List<VersionTarget> versionTargets = new List<VersionTarget>();
        readonly List<Type> versionTextTypes = new List<Type>();
        readonly Dictionary<Type, MemberInfo[]> versionStringMembers = new Dictionary<Type, MemberInfo[]>();

        sealed class Tracked
        {
            public object Player, Health, LastDamage, LastAttacker;
            public bool Alive, DeathCaptured, Subscribed;
            public Vector3 Pos;
            public string Kind, LastHit, LastWeapon;
            public EventInfo DiedEvent, DamageEvent, PlayerDamageEvent;
            public Delegate DiedHandler, DamageHandler, PlayerDamageHandler;
        }
        sealed class KillLine
        {
            public string Killer, Victim, WeaponIcon, HitIcon, DistanceText;
            public float Created;
            public bool HasDistance;
        }
        sealed class VersionTarget { public object Owner; public MemberInfo Member; public string Original; public bool WasEnabled; }

        void Awake()
        {
            workAlways = Config.Bind("General", "Work Always", false, "Debug override");
            toggleKey = Config.Bind("General", "Toggle Key", new KeyboardShortcut(KeyCode.F9), "Hidden -> population -> population + status -> hidden");
            savedMode = Config.Bind("General", "HUD State", 0,
                new ConfigDescription("0 hidden / 1 population / 2 population + status", new AcceptableValueRange<int>(0, 2)));
            mode = Mathf.Clamp(savedMode.Value, 0, 2);
            editMode = Config.Bind("General", "HUD Edit Mode", false, "Enable compact drag hitboxes");
            showVersion = Config.Bind("General", "Show SPT Version Label", false, "Show/hide native SPT version label");
            lastShowVersion = showVersion.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ArmVersionSearch(.35f);

            popEnabled = Config.Bind("Population", "Enabled", true, "Population");
            popLayout = Layout("Population");
            popSize = Size("Population"); popOpacity = Opacity("Population"); popX = X("Population", 8); popY = BottomY("Population", 8);
            pmcColor = C("Population Colors", "PMC", .55f, .72f, .58f); scavColor = C("Population Colors", "Scav", .72f, .48f, .46f); bossColor = C("Population Colors", "Boss", .78f, .60f, .38f); reinforcedColor = C("Population Colors", "Reinforced", .63f, .51f, .72f);

            statusEnabled = Config.Bind("Player Status", "Enabled", true, "Status");
            statusLayout = Layout("Player Status");
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
            Logger.LogInfo("SPT Tactical HUD v1.13.2 loaded (HUD state " + mode + ")");
        }

        ConfigEntry<string> Layout(string s) => Config.Bind(s, "Layout", "Horizontal",
            new ConfigDescription("Horizontal / Vertical", new AcceptableValueList<string>("Horizontal", "Vertical")));
        ConfigEntry<int> Size(string s) => Config.Bind(s, "Size", 10, new ConfigDescription("Size", new AcceptableValueRange<int>(8, 20)));
        ConfigEntry<float> Opacity(string s) => Config.Bind(s, "Opacity", .55f, new ConfigDescription("0 invisible / 1 opaque", new AcceptableValueRange<float>(0, 1)));
        ConfigEntry<float> X(string s, float v) => Config.Bind(s, "Position X", v, new ConfigDescription("X", new AcceptableValueRange<float>(-400, 4000)));
        ConfigEntry<float> BottomY(string s, float v) => Config.Bind(s, "Position Y From Bottom", v, new ConfigDescription("Bottom", new AcceptableValueRange<float>(-100, 2000)));
        ConfigEntry<Color> C(string s, string k, float r, float g, float b) => Config.Bind(s, k, new Color(r, g, b, 1), "Muted color");

        void Update()
        {
            int configuredMode = Mathf.Clamp(savedMode.Value, 0, 2);
            if (configuredMode != mode) mode = configuredMode;
            if (toggleKey.Value.IsDown()) SetHudMode((mode + 1) % 3);
            if (Time.unscaledTime >= nextRefresh)
            {
                Refresh();
                nextRefresh = Time.unscaledTime + (inRaid ? .25f : statusOutside.Value ? .5f : .75f);
            }
            if (inRaid && Time.unscaledTime >= nextSubscribe) { nextSubscribe = Time.unscaledTime + .35f; ProcessOneSubscription(); }
            if (versionSearchAttempts > 0 && Time.unscaledTime >= nextVersionSearch)
            {
                versionSearchAttempts--;
                DiscoverVersionTargetsOnce();
                if (versionTargets.Count > 0) versionSearchAttempts = 0;
                else if (versionSearchAttempts > 0) nextVersionSearch = Time.unscaledTime + 1.5f;
            }
            if (showVersion.Value != lastShowVersion)
            {
                lastShowVersion = showVersion.Value;
                ApplyKnownVersionTargets();
                if (versionTargets.Count == 0) ArmVersionSearch(0f);
            }
            if (kills.Count > 0)
            {
                float now = Time.unscaledTime;
                float lifetime = killLifetime.Value;
                for (int i = kills.Count - 1; i >= 0; i--)
                    if (now - kills[i].Created > lifetime) kills.RemoveAt(i);
            }
        }

        void SetHudMode(int value)
        {
            mode = Mathf.Clamp(value, 0, 2);
            if (savedMode.Value == mode) return;
            savedMode.Value = mode;
            try { Config.Save(); } catch { }
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (Tracked t in tracked.Values) Unsubscribe(t);
            if (visualRenderer != null) visualRenderer.Dispose();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            versionTargets.RemoveAll(target => !IsUsableUnityObject(target.Owner));
            ArmVersionSearch(.25f);
        }

        void ArmVersionSearch(float delay)
        {
            versionSearchAttempts = 3;
            nextVersionSearch = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        void OnGUI()
        {
            RenderVisualHud();
        }

        void Refresh()
        {
            refreshPlayers.Clear();
            refreshSeen.Clear();
            refreshRemoved.Clear();
            try
            {
                object world = GetWorld();
                if (world == null)
                {
                    SetRaidState(false);
                    RefreshOutsideRaidStatus(null);
                    return;
                }
                IEnumerable ps = GetPlayers(world);
                if (ps == null)
                {
                    SetRaidState(false);
                    RefreshOutsideRaidStatus(null);
                    return;
                }

                object local = null;
                foreach (object player in ps)
                {
                    if (!IsUsableUnityObject(player)) continue;
                    refreshPlayers.Add(player);
                    if (IsTrue(ReadMember(player, "IsYourPlayer"))) local = player;
                }

                if (!IsRaidWorld(world, local))
                {
                    SetRaidState(false);
                    RefreshOutsideRaidStatus(local);
                    return;
                }

                SetRaidState(true);

                int p = 0, s = 0, b = 0, r = 0;
                playersByProfileId.Clear();
                foreach (object pl in refreshPlayers)
                {
                    string id = PlayerId(pl); if (string.IsNullOrEmpty(id)) id = pl.GetHashCode().ToString();
                    refreshSeen.Add(id); playersByProfileId[id] = pl;
                    bool alive = IsAlive(pl); string kind = Kind(pl); Vector3 pos = Position(pl);
                    if (!ReferenceEquals(pl, local) && alive)
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

                foreach (string id in tracked.Keys)
                    if (!refreshSeen.Contains(id)) refreshRemoved.Add(id);

                for (int i = 0; i < refreshRemoved.Count; i++)
                {
                    string id = refreshRemoved[i];
                    Tracked t = tracked[id];
                    if (t.Alive && !t.DeathCaptured && (t.LastDamage != null || t.LastAttacker != null)) CaptureDeath(t);
                    Unsubscribe(t); tracked.Remove(id); queuedIds.Remove(id);
                }

                pmc = p; scav = s; boss = b; reinforced = r; RefreshStatus(local);
            }
            catch (Exception ex) { Logger.LogWarning("HUD refresh: " + ex.Message); }
            finally
            {
                refreshPlayers.Clear();
                refreshSeen.Clear();
                refreshRemoved.Clear();
            }
        }

        void SetRaidState(bool value)
        {
            if (inRaid == value) return;
            inRaid = value;

            foreach (Tracked t in tracked.Values) Unsubscribe(t);
            tracked.Clear();
            playersByProfileId.Clear();
            subscribeQueue.Clear();
            queuedIds.Clear();
            kills.Clear();
            diagSeen.Clear();
            pmc = scav = boss = reinforced = 0;

            if (!inRaid && !statusOutside.Value)
            {
                hydration = energy = weight = overweightLimit = walkDrainLimit = 0f;
            }
        }

        bool IsRaidWorld(object world, object localPlayer)
        {
            if (!IsUsableUnityObject(world) || !IsUsableUnityObject(localPlayer)) return false;

            string scene = string.Empty;
            try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
            if (ContainsHideoutMarker(scene) || ContainsHideoutMarker(world.GetType().FullName)) return false;

            foreach (string member in new[] { "LocationId", "Location", "LocationName", "SceneName", "MapName" })
            {
                object value = ReadMember(world, member);
                if (ContainsHideoutMarker(value?.ToString())) return false;
            }

            object profile = ReadMember(localPlayer, "Profile");
            foreach (object value in new[]
            {
                ReadMember(localPlayer, "Location"),
                ReadMember(profile, "Location"),
                ReadMember(ReadMember(profile, "Info"), "Location")
            })
            {
                if (ContainsHideoutMarker(value?.ToString())) return false;
            }

            return true;
        }

        static bool ContainsHideoutMarker(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string normalized = value.ToLowerInvariant();
            return normalized.Contains("hideout") || normalized.Contains("убежищ");
        }

        static bool IsUsableUnityObject(object value)
        {
            if (value == null) return false;
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return ReferenceEquals(unityObject, null) || unityObject;
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
            string killer = "Unknown", victim = IsTrue(ReadMember(t.Player, "IsYourPlayer")) ? "Self" : t.Kind;
            string weapon = FirstNonEmpty(t.LastWeapon, ResolveWeaponFromDamage(info), "?"); float dist = 0; bool hasDist = false;
            if (attacker != null && !ReferenceEquals(attacker, t.Player))
            {
                killer = IsTrue(ReadMember(attacker, "IsYourPlayer")) ? "Self" : Kind(attacker);
                string liveWeapon = Weapon(attacker); if (!string.IsNullOrEmpty(liveWeapon) && liveWeapon != "?") weapon = liveWeapon;
                Vector3 ap = Position(attacker);
                if (ap != Vector3.zero && t.Pos != Vector3.zero) { dist = Vector3.Distance(ap, t.Pos); hasDist = true; }
            }
            else DiagnoseDeath(t.Player, hc, info);
            string cleanWeapon = HudVisualRenderer.CleanWeapon(weapon);
            kills.Add(new KillLine
            {
                Killer = killer,
                Victim = victim,
                WeaponIcon = HudVisualRenderer.WeaponKey(cleanWeapon),
                HitIcon = HudVisualRenderer.HitKey(hit),
                DistanceText = hasDist ? Mathf.RoundToInt(dist) + "m" : string.Empty,
                HasDistance = hasDist,
                Created = Time.unscaledTime
            });
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
            if (!IsUsableUnityObject(pl)) return;
            object hc = ReadMember(pl, "HealthController");
            object profile = ReadMember(pl, "Profile");
            if (LooksLikeProfile(profile)) outsideProfile = profile;
            RefreshStatusSources(hc, profile);
        }

        void RefreshOutsideRaidStatus(object localPlayer)
        {
            if (!statusOutside.Value)
            {
                hydration = energy = weight = overweightLimit = walkDrainLimit = 0f;
                return;
            }

            if (IsUsableUnityObject(localPlayer))
            {
                RefreshStatus(localPlayer);
                return;
            }

            object profile = FindOutsideProfile();
            if (profile != null) RefreshStatusSources(null, profile);
        }

        void RefreshStatusSources(object hc, object profile)
        {
            object health = FirstNonNull(ReadMember(profile, "Health"), ReadMember(profile, "HealthInfo"), ReadMember(profile, "HealthController"));

            float? currentHydration =
                ReadFloatDeep(hc, "Hydration", "Current") ??
                ReadFloatDeep(hc, "Hydration", "Value") ??
                ReadFloatDeep(health, "Hydration", "Current") ??
                ReadFloatDeep(health, "Hydration", "Value") ??
                ReadFloatDeep(profile, "Hydration", "Current") ??
                ReadFloatDeep(profile, "Hydration", "Value");
            float? currentEnergy =
                ReadFloatDeep(hc, "Energy", "Current") ??
                ReadFloatDeep(hc, "Energy", "Value") ??
                ReadFloatDeep(health, "Energy", "Current") ??
                ReadFloatDeep(health, "Energy", "Value") ??
                ReadFloatDeep(profile, "Energy", "Current") ??
                ReadFloatDeep(profile, "Energy", "Value");

            if (currentHydration.HasValue) hydration = currentHydration.Value;
            if (currentEnergy.HasValue) energy = currentEnergy.Value;

            object inv = ReadMember(profile, "Inventory"), skills = ReadMember(profile, "Skills");
            float? normal = ReadWrappedFloat(ReadMember(inv, "TotalWeight"));
            float? elite = ReadWrappedFloat(ReadMember(inv, "TotalWeightEliteSkill"));
            bool eliteBuff = ReadWrappedBool(ReadMember(skills, "StrengthBuffElite")) ?? false;
            float? currentWeight = (eliteBuff ? elite : normal) ?? normal ?? elite;
            if (currentWeight.HasValue) weight = currentWeight.Value;

            object global = GetSingletonInstance(FindType("EFT.GlobalConfiguration") ?? FindTypeByName("GlobalConfiguration"));
            object stamina = ReadMember(global, "Stamina");
            Vector2? baseLimits = ReadVector2(ReadMember(stamina, "BaseOverweightLimits"));
            Vector2? walkLimits = ReadVector2(ReadMember(stamina, "WalkOverweightLimits"));
            float rel = ReadFloat(ReadMember(hc, "CarryingWeightRelativeModifier")) ?? 1f;
            float abs = ReadFloat(ReadMember(hc, "CarryingWeightAbsoluteModifier")) ?? 0f;
            if (baseLimits.HasValue) overweightLimit = baseLimits.Value.x * rel + abs;
            if (walkLimits.HasValue) walkDrainLimit = walkLimits.Value.x * rel + abs;
        }

        object FindOutsideProfile()
        {
            if (LooksLikeProfile(outsideProfile)) return outsideProfile;
            if (Time.unscaledTime < nextOutsideProfileSearch) return null;
            nextOutsideProfileSearch = Time.unscaledTime + 2f;

            foreach (string typeName in new[] { "EFT.TarkovApplication", "TarkovApplication", "EFT.ClientApplication", "ClientApplication" })
            {
                Type type = FindType(typeName) ?? FindTypeByName(typeName.Substring(typeName.LastIndexOf('.') + 1));
                object source = GetSingletonInstance(type);
                object profile = ProfileFromSource(source);
                if (profile != null) return outsideProfile = profile;
            }

            return null;
        }

        object ProfileFromSource(object source)
        {
            if (source == null) return null;
            if (LooksLikeProfile(source)) return source;

            foreach (string name in new[] { "Profile", "MainProfile", "ActiveProfile", "SelectedProfile", "ProfileOfPet" })
            {
                object profile = ReadMember(source, name);
                if (LooksLikeProfile(profile)) return profile;
            }

            foreach (string name in new[] { "Session", "BackEndSession", "BackendSession", "ClientSession" })
            {
                object session = ReadMember(source, name);
                if (session == null) continue;
                foreach (string profileName in new[] { "Profile", "MainProfile", "ActiveProfile", "ProfileOfPet" })
                {
                    object profile = ReadMember(session, profileName);
                    if (LooksLikeProfile(profile)) return profile;
                }
            }

            return null;
        }

        static bool LooksLikeProfile(object value)
        {
            if (value == null) return false;
            return ReadMember(value, "Inventory") != null &&
                   (ReadMember(value, "Info") != null || ReadMember(value, "Id") != null || ReadMember(value, "ProfileId") != null);
        }

        void DiscoverVersionTargetsOnce()
        {
            try
            {
                versionTargets.RemoveAll(target => !IsUsableUnityObject(target.Owner));
                EnsureVersionTextTypes();
                foreach (Type componentType in versionTextTypes)
                {
                    foreach (UnityEngine.Object component in Resources.FindObjectsOfTypeAll(componentType))
                    {
                        if (!IsUsableUnityObject(component)) continue;
                        Type concreteType = component.GetType();
                        MemberInfo[] members;
                        if (!versionStringMembers.TryGetValue(concreteType, out members))
                        {
                            members = StringMembers(concreteType).ToArray();
                            versionStringMembers[concreteType] = members;
                        }

                        foreach (MemberInfo member in members)
                        {
                            string value = ReadStringMember(component, member);
                            if (!LooksLikeSptVersion(value)) continue;
                            if (versionTargets.Any(target => ReferenceEquals(target.Owner, component) && target.Member.Name == member.Name)) continue;
                            Behaviour behaviour = component as Behaviour;
                            versionTargets.Add(new VersionTarget
                            {
                                Owner = component,
                                Member = member,
                                Original = value,
                                WasEnabled = behaviour == null || behaviour.enabled
                            });
                        }
                    }
                }
                ApplyKnownVersionTargets();
            }
            catch (Exception ex) { Logger.LogDebug("Version discovery: " + ex.Message); }
        }

        void EnsureVersionTextTypes()
        {
            if (versionTextTypesResolved) return;
            versionTextTypesResolved = true;
            foreach (string name in new[]
            {
                "TMPro.TextMeshProUGUI",
                "TMPro.TMP_Text",
                "UnityEngine.UI.Text",
                "EFT.UI.VersionNumber"
            })
            {
                Type type = FindType(name);
                if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type)) continue;
                if (!versionTextTypes.Any(existing => existing.IsAssignableFrom(type))) versionTextTypes.Add(type);
            }
        }

        void ApplyKnownVersionTargets()
        {
            for (int i = versionTargets.Count - 1; i >= 0; i--)
            {
                VersionTarget target = versionTargets[i];
                if (!IsUsableUnityObject(target.Owner) || target.Member == null)
                {
                    versionTargets.RemoveAt(i);
                    continue;
                }

                string current = ReadStringMember(target.Owner, target.Member);
                Behaviour behaviour = target.Owner as Behaviour;
                if (showVersion.Value)
                {
                    if (behaviour != null) behaviour.enabled = target.WasEnabled;
                    WriteStringMember(target.Owner, target.Member, target.Original);
                }
                else
                {
                    if (LooksLikeSptVersion(current)) target.Original = current;
                    WriteStringMember(target.Owner, target.Member, string.Empty);
                    if (behaviour != null) behaviour.enabled = false;
                }
            }
        }

        IEnumerable<MemberInfo> StringMembers(Type t)
        {
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (p.PropertyType == typeof(string) && p.CanRead && p.CanWrite && IsTextMemberName(p.Name)) yield return p;
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (f.FieldType == typeof(string) && IsTextMemberName(f.Name)) yield return f;
        }

        static bool IsTextMemberName(string name)
        {
            string normalized = (name ?? string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            return normalized == "text" || normalized == "mtext";
        }

        static bool LooksLikeSptVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf("SPT", StringComparison.OrdinalIgnoreCase) < 0) return false;
            int digits = 0, dots = 0;
            foreach (char c in value)
            {
                if (char.IsDigit(c)) digits++;
                else if (c == '.') dots++;
            }
            return digits >= 3 && dots >= 2;
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

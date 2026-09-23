using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.tacticalhud", "Admiral Tactical HUD", "1.13.3")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        ConfigEntry<bool> workAlways, editMode, popEnabled, statusEnabled, statusOutside, showVersion;
        ConfigEntry<KeyboardShortcut> toggleKey;
        ConfigEntry<int> savedMode, popSize, statusSize;
        ConfigEntry<string> popLayout, statusLayout;
        ConfigEntry<float> popOpacity, statusOpacity, popX, popY, statusX, statusY;
        ConfigEntry<Color> pmcColor, scavColor, bossColor, reinforcedColor, weightOk, weightHeavy, weightCritical;

        float nextRefresh, nextVersionSearch, nextOutsideProfileSearch;
        int mode, pmc, scav, boss, reinforced, versionSearchAttempts;
        bool inRaid, lastShowVersion, versionTextTypesResolved;
        float hydration, energy, weight, overweightLimit, walkDrainLimit;
        Type worldType, singletonType, globalConfigurationType;
        PropertyInfo singletonInstance;
        MemberInfo playersMember;
        object outsideProfile, globalConfiguration;

        static readonly string[] WorldLocationMembers = { "LocationId", "Location", "LocationName", "SceneName", "MapName" };
        static readonly string[] OutsideApplicationTypes = { "EFT.TarkovApplication", "TarkovApplication", "EFT.ClientApplication", "ClientApplication" };
        static readonly string[] ProfileMembers = { "Profile", "MainProfile", "ActiveProfile", "SelectedProfile", "ProfileOfPet" };
        static readonly string[] SessionMembers = { "Session", "BackEndSession", "BackendSession", "ClientSession" };
        static readonly string[] SessionProfileMembers = { "Profile", "MainProfile", "ActiveProfile", "ProfileOfPet" };
        static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly Dictionary<Type, Dictionary<string, MemberInfo>> MemberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();
        static readonly Dictionary<string, Type> ExactTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        static readonly Dictionary<string, Type> SimpleTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        static readonly Dictionary<Type, PropertyInfo> SingletonPropertyCache = new Dictionary<Type, PropertyInfo>();
        readonly List<object> refreshPlayers = new List<object>(64);
        readonly List<VersionTarget> versionTargets = new List<VersionTarget>();
        readonly List<Type> versionTextTypes = new List<Type>();
        readonly Dictionary<Type, MemberInfo[]> versionStringMembers = new Dictionary<Type, MemberInfo[]>();

        sealed class VersionTarget
        {
            public object Owner;
            public MemberInfo Member;
            public string Original;
            public bool WasEnabled;
        }

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
            popSize = Size("Population");
            popOpacity = Opacity("Population");
            popX = X("Population", 8);
            popY = BottomY("Population", 8);
            pmcColor = C("Population Colors", "PMC", .55f, .72f, .58f);
            scavColor = C("Population Colors", "Scav", .72f, .48f, .46f);
            bossColor = C("Population Colors", "Boss", .78f, .60f, .38f);
            reinforcedColor = C("Population Colors", "Reinforced", .63f, .51f, .72f);

            statusEnabled = Config.Bind("Player Status", "Enabled", true, "Status");
            statusLayout = Layout("Player Status");
            statusOutside = Config.Bind("Player Status", "Show Outside Raid", false, "Show outside raid if profile data is available");
            statusSize = Size("Player Status");
            statusOpacity = Opacity("Player Status");
            statusX = X("Player Status", 8);
            statusY = BottomY("Player Status", 24);
            weightOk = C("Player Status Colors", "Weight OK", .58f, .75f, .52f);
            weightHeavy = C("Player Status Colors", "Weight Heavy", .78f, .68f, .39f);
            weightCritical = C("Player Status Colors", "Weight Critical", .75f, .42f, .39f);

            BindCompass();

            Logger.LogInfo("Admiral Tactical HUD v1.13.3 loaded (optimized runtime, HUD state " + mode + ")");
        }

        ConfigEntry<string> Layout(string s) => Config.Bind(s, "Layout", "Horizontal",
            new ConfigDescription("Horizontal / Vertical", new AcceptableValueList<string>("Horizontal", "Vertical")));
        ConfigEntry<int> Size(string s) => Config.Bind(s, "Size", 10,
            new ConfigDescription("Size", new AcceptableValueRange<int>(8, 20)));
        ConfigEntry<float> Opacity(string s) => Config.Bind(s, "Opacity", .55f,
            new ConfigDescription("0 invisible / 1 opaque", new AcceptableValueRange<float>(0, 1)));
        ConfigEntry<float> X(string s, float v) => Config.Bind(s, "Position X", v,
            new ConfigDescription("X", new AcceptableValueRange<float>(-400, 4000)));
        ConfigEntry<float> BottomY(string s, float v) => Config.Bind(s, "Position Y From Bottom", v,
            new ConfigDescription("Bottom", new AcceptableValueRange<float>(-100, 2000)));
        ConfigEntry<Color> C(string s, string k, float r, float g, float b) => Config.Bind(s, k, new Color(r, g, b, 1), "Muted color");

        bool PopulationActive => popEnabled.Value && (mode >= 1 || editMode.Value || workAlways.Value);
        bool StatusActive => statusEnabled.Value && (mode >= 2 || editMode.Value || workAlways.Value);

        void Update()
        {
            int configuredMode = Mathf.Clamp(savedMode.Value, 0, 2);
            if (configuredMode != mode) mode = configuredMode;
            if (toggleKey.Value.IsDown()) SetHudMode((mode + 1) % 3);

            float now = Time.unscaledTime;
            UpdateCompass();
            if (now >= nextRefresh)
            {
                Refresh();
                nextRefresh = now + RefreshInterval();
            }

            if (versionSearchAttempts > 0 && now >= nextVersionSearch)
            {
                versionSearchAttempts--;
                DiscoverVersionTargetsOnce();
                if (versionTargets.Count > 0) versionSearchAttempts = 0;
                else if (versionSearchAttempts > 0) nextVersionSearch = now + 1.5f;
            }

            if (showVersion.Value != lastShowVersion)
            {
                lastShowVersion = showVersion.Value;
                ApplyKnownVersionTargets();
                if (versionTargets.Count == 0) ArmVersionSearch(0f);
            }

        }

        float RefreshInterval()
        {
            if (!inRaid) return statusOutside.Value ? 1.0f : 2.0f;
            if (PopulationActive || StatusActive) return .75f;
            return 2.0f;
        }

        void SetHudMode(int value)
        {
            mode = Mathf.Clamp(value, 0, 2);
            if (savedMode.Value == mode) return;
            savedMode.Value = mode;
            try { Config.Save(); } catch { }
            nextRefresh = 0f;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (visualRenderer != null) visualRenderer.Dispose();
            DisposeCompass();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            versionTargets.RemoveAll(target => !IsUsableUnityObject(target.Owner));
            playersMember = null;
            outsideProfile = null;
            globalConfiguration = null;
            nextRefresh = 0f;
            ResetCompass();
            ArmVersionSearch(.25f);
        }

        void ArmVersionSearch(float delay)
        {
            versionSearchAttempts = 3;
            nextVersionSearch = Time.unscaledTime + Mathf.Max(0f, delay);
        }

        void OnGUI()
        {
            bool couldRender = editMode.Value || workAlways.Value || inRaid || (statusOutside.Value && StatusActive);
            if (!couldRender) return;
            RenderVisualHud();
            RenderCompass();
        }

        void Refresh()
        {
            refreshPlayers.Clear();

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
                    if (local == null && IsTrue(ReadMember(player, "IsYourPlayer"))) local = player;
                }

                if (!IsRaidWorld(world, local))
                {
                    SetRaidState(false);
                    RefreshOutsideRaidStatus(local);
                    return;
                }

                SetRaidState(true);
                RefreshCompass(local);

                bool needPopulation = PopulationActive;
                bool needStatus = StatusActive;

                if (!needPopulation)
                {
                    if (needStatus) RefreshStatus(local);
                    return;
                }

                int p = 0, s = 0, b = 0, r = 0;

                foreach (object pl in refreshPlayers)
                {
                    bool alive = IsAlive(pl);
                    string kind = Kind(pl);

                    if (needPopulation && !ReferenceEquals(pl, local) && alive)
                    {
                        if (kind == "Boss") b++;
                        else if (kind == "Raider") r++;
                        else if (kind == "USEC" || kind == "BEAR" || kind == "PMC") p++;
                        else s++;
                    }

                }

                if (needPopulation)
                {
                    pmc = p;
                    scav = s;
                    boss = b;
                    reinforced = r;
                }

                if (needStatus) RefreshStatus(local);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("HUD refresh: " + ex.Message);
            }
            finally
            {
                refreshPlayers.Clear();
            }
        }

        void SetRaidState(bool value)
        {
            if (inRaid == value) return;
            inRaid = value;
            if (!inRaid) ResetCompass();
            pmc = scav = boss = reinforced = 0;
            if (!inRaid && !statusOutside.Value)
                hydration = energy = weight = overweightLimit = walkDrainLimit = 0f;
        }

        bool IsRaidWorld(object world, object localPlayer)
        {
            if (!IsUsableUnityObject(world) || !IsUsableUnityObject(localPlayer)) return false;

            string scene = string.Empty;
            try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
            if (ContainsHideoutMarker(scene) || ContainsHideoutMarker(world.GetType().FullName)) return false;

            for (int i = 0; i < WorldLocationMembers.Length; i++)
            {
                object value = ReadMember(world, WorldLocationMembers[i]);
                if (ContainsHideoutMarker(value?.ToString())) return false;
            }

            object profile = ReadMember(localPlayer, "Profile");
            object value1 = ReadMember(localPlayer, "Location");
            object value2 = ReadMember(profile, "Location");
            object value3 = ReadMember(ReadMember(profile, "Info"), "Location");
            return !ContainsHideoutMarker(value1?.ToString()) &&
                   !ContainsHideoutMarker(value2?.ToString()) &&
                   !ContainsHideoutMarker(value3?.ToString());
        }

        static bool ContainsHideoutMarker(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf("hideout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("убежищ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsUsableUnityObject(object value)
        {
            if (value == null) return false;
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return ReferenceEquals(unityObject, null) || unityObject;
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
            if (!statusOutside.Value || !StatusActive)
            {
                if (!statusOutside.Value) hydration = energy = weight = overweightLimit = walkDrainLimit = 0f;
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
                ReadFloatDeep(hc, "Hydration", "Current") ?? ReadFloatDeep(hc, "Hydration", "Value") ??
                ReadFloatDeep(health, "Hydration", "Current") ?? ReadFloatDeep(health, "Hydration", "Value") ??
                ReadFloatDeep(profile, "Hydration", "Current") ?? ReadFloatDeep(profile, "Hydration", "Value");

            float? currentEnergy =
                ReadFloatDeep(hc, "Energy", "Current") ?? ReadFloatDeep(hc, "Energy", "Value") ??
                ReadFloatDeep(health, "Energy", "Current") ?? ReadFloatDeep(health, "Energy", "Value") ??
                ReadFloatDeep(profile, "Energy", "Current") ?? ReadFloatDeep(profile, "Energy", "Value");

            if (currentHydration.HasValue) hydration = currentHydration.Value;
            if (currentEnergy.HasValue) energy = currentEnergy.Value;

            object inv = ReadMember(profile, "Inventory");
            object skills = ReadMember(profile, "Skills");
            float? normal = ReadWrappedFloat(ReadMember(inv, "TotalWeight"));
            float? elite = ReadWrappedFloat(ReadMember(inv, "TotalWeightEliteSkill"));
            bool eliteBuff = ReadWrappedBool(ReadMember(skills, "StrengthBuffElite")) ?? false;
            float? currentWeight = (eliteBuff ? elite : normal) ?? normal ?? elite;
            if (currentWeight.HasValue) weight = currentWeight.Value;

            if (globalConfiguration == null)
            {
                globalConfigurationType ??= FindType("EFT.GlobalConfiguration") ?? FindTypeByName("GlobalConfiguration");
                globalConfiguration = GetSingletonInstance(globalConfigurationType);
            }

            object stamina = ReadMember(globalConfiguration, "Stamina");
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
            nextOutsideProfileSearch = Time.unscaledTime + 3f;

            for (int i = 0; i < OutsideApplicationTypes.Length; i++)
            {
                string typeName = OutsideApplicationTypes[i];
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

            for (int i = 0; i < ProfileMembers.Length; i++)
            {
                object profile = ReadMember(source, ProfileMembers[i]);
                if (LooksLikeProfile(profile)) return profile;
            }

            for (int i = 0; i < SessionMembers.Length; i++)
            {
                object session = ReadMember(source, SessionMembers[i]);
                if (session == null) continue;
                for (int j = 0; j < SessionProfileMembers.Length; j++)
                {
                    object profile = ReadMember(session, SessionProfileMembers[j]);
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
                        if (!versionStringMembers.TryGetValue(concreteType, out MemberInfo[] members))
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
            catch (Exception ex)
            {
                Logger.LogDebug("Version discovery: " + ex.Message);
            }
        }

        void EnsureVersionTextTypes()
        {
            if (versionTextTypesResolved) return;
            versionTextTypesResolved = true;
            foreach (string name in new[] { "TMPro.TextMeshProUGUI", "TMPro.TMP_Text", "UnityEngine.UI.Text", "EFT.UI.VersionNumber" })
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
            foreach (PropertyInfo p in t.GetProperties(InstanceFlags))
                if (p.PropertyType == typeof(string) && p.CanRead && p.CanWrite && IsTextMemberName(p.Name)) yield return p;
            foreach (FieldInfo f in t.GetFields(InstanceFlags))
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

        static string ReadStringMember(object o, MemberInfo m)
        {
            try
            {
                if (m is PropertyInfo p) return p.GetValue(o, null) as string;
                if (m is FieldInfo f) return f.GetValue(o) as string;
            }
            catch { }
            return null;
        }

        static void WriteStringMember(object o, MemberInfo m, string value)
        {
            try
            {
                if (m is PropertyInfo p && p.CanWrite) { p.SetValue(o, value, null); return; }
                if (m is FieldInfo f) f.SetValue(o, value);
            }
            catch { }
        }

        string Kind(object p)
        {
            string role = Role(p), side = Side(p);
            if (IsBoss(role)) return "Boss";
            if (IsReinforced(role)) return "Raider";
            if (!string.IsNullOrEmpty(side) && side.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0) return "USEC";
            if (!string.IsNullOrEmpty(side) && side.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0) return "BEAR";
            if (IsPmc(side)) return "PMC";
            return "Scav";
        }

        object GetWorld()
        {
            worldType ??= FindType("EFT.GameWorld") ?? FindTypeByName("GameWorld");
            if (worldType == null) return null;
            singletonType ??= FindType("Comfort.Common.Singleton`1");
            if (singletonType == null) return null;
            singletonInstance ??= singletonType.MakeGenericType(worldType).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return singletonInstance?.GetValue(null, null);
        }

        IEnumerable GetPlayers(object w)
        {
            if (playersMember == null)
            {
                Type t = w.GetType();
                playersMember = (MemberInfo)t.GetProperty("RegisteredPlayers", InstanceFlags) ??
                                t.GetProperty("AllPlayers", InstanceFlags) ??
                                (MemberInfo)t.GetField("RegisteredPlayers", InstanceFlags);
            }
            object v = playersMember is PropertyInfo pi ? pi.GetValue(w, null) : (playersMember as FieldInfo)?.GetValue(w);
            return v as IEnumerable;
        }

        static object GetSingletonInstance(Type t)
        {
            if (t == null) return null;
            Type singleton = FindType("Comfort.Common.Singleton`1");
            if (singleton == null) return null;
            try
            {
                if (!SingletonPropertyCache.TryGetValue(t, out PropertyInfo property))
                {
                    property = singleton.MakeGenericType(t).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    SingletonPropertyCache[t] = property;
                }
                return property?.GetValue(null, null);
            }
            catch { return null; }
        }

        static bool IsAlive(object p)
        {
            object h = ReadMember(p, "HealthController");
            object a = h != null ? ReadMember(h, "IsAlive") : ReadMember(p, "IsAlive");
            return !(a is bool) || (bool)a;
        }

        static string Side(object p)
        {
            object pr = ReadMember(p, "Profile");
            object i = ReadMember(pr, "Info");
            return (ReadMember(i, "Side") ?? ReadMember(pr, "Side"))?.ToString();
        }

        static string Role(object p)
        {
            object pr = ReadMember(p, "Profile");
            object i = ReadMember(pr, "Info");
            object s = ReadMember(i, "Settings") ?? ReadMember(pr, "Settings");
            return (ReadMember(s, "Role") ?? ReadMember(i, "Role"))?.ToString();
        }

        static bool IsPmc(string s) => s != null &&
            (s.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0);
        static bool IsBoss(string r) => !string.IsNullOrEmpty(r) && r.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsReinforced(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            return role.IndexOf("follower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("pmcbot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("exusec", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("raider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("sectant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   role.IndexOf("assaultgroup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsTrue(object v) => v is bool && (bool)v;
        static float? ReadFloat(object v) { if (v == null) return null; try { return Convert.ToSingle(v); } catch { return null; } }
        static float? ReadWrappedFloat(object v) { if (v == null) return null; return ReadFloat(ReadMember(v, "Value")) ?? ReadFloat(v); }
        static bool? ReadWrappedBool(object v) { if (v == null) return null; object x = ReadMember(v, "Value") ?? v; return x is bool ? (bool?)x : null; }
        static float? ReadFloatDeep(object o, string a, string b) { object x = ReadMember(o, a); return ReadFloat(ReadMember(x, b)) ?? ReadFloat(x); }
        static Vector2? ReadVector2(object v) { if (v is Vector2 vector) return vector; return null; }
        static object FirstNonNull(params object[] values) { foreach (object v in values) if (v != null) return v; return null; }

        static object ReadMember(object o, string n)
        {
            if (o == null || string.IsNullOrEmpty(n)) return null;
            Type t = o.GetType();
            if (!MemberCache.TryGetValue(t, out Dictionary<string, MemberInfo> perType))
            {
                perType = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
                MemberCache[t] = perType;
            }

            if (!perType.TryGetValue(n, out MemberInfo member))
            {
                try { member = t.GetProperty(n, InstanceFlags); } catch { member = null; }
                if (member == null)
                {
                    try { member = t.GetField(n, InstanceFlags); } catch { member = null; }
                }
                perType[n] = member;
            }

            if (member == null) return null;
            try
            {
                if (member is PropertyInfo p) return p.GetValue(o, null);
                if (member is FieldInfo f) return f.GetValue(o);
            }
            catch { }
            return null;
        }

        static Type FindType(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (ExactTypeCache.TryGetValue(n, out Type cached)) return cached;
            Type result = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    result = a.GetType(n, false);
                    if (result != null) break;
                }
                catch { }
            }
            ExactTypeCache[n] = result;
            return result;
        }

        static Type FindTypeByName(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (SimpleTypeCache.TryGetValue(n, out Type cached)) return cached;
            Type result = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    result = a.GetTypes().FirstOrDefault(x => x.Name == n);
                    if (result != null) break;
                }
                catch (ReflectionTypeLoadException e)
                {
                    result = e.Types?.FirstOrDefault(x => x != null && x.Name == n);
                    if (result != null) break;
                }
                catch { }
            }
            SimpleTypeCache[n] = result;
            return result;
        }
    }
}

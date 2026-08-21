using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.popcounter", "SPT Tactical HUD", "2.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> _workAlways, _editMode, _populationEnabled, _statusEnabled, _killFeedEnabled;
        private ConfigEntry<KeyboardShortcut> _toggleKey;
        private ConfigEntry<int> _fontSize;
        private ConfigEntry<float> _opacity, _populationX, _populationY, _statusX, _statusY, _killFeedX, _killFeedY;

        private float _nextRefresh;
        private int _displayMode; // 0 hidden, 1 population, 2 population+status
        private bool _inRaid;
        private int _pmcCount, _scavCount, _bossCount, _reinforcedCount;
        private float _hydration, _energy, _weight, _maxWeight;
        private GUIStyle _textStyle, _shadowStyle, _editStyle;
        private Type _gameWorldType, _singletonOpenType;
        private PropertyInfo _singletonInstanceProperty;
        private MemberInfo _registeredPlayersMember;
        private Vector2 _dragOffset;
        private int _dragCluster; // 1 pop 2 status 3 killfeed

        private void Awake()
        {
            _workAlways = Config.Bind("General", "Work Always", false, "Show/test HUD outside raids with zero counters.");
            _toggleKey = Config.Bind("General", "Toggle Key", new KeyboardShortcut(KeyCode.F9), "Cycle: hidden -> population -> population + status -> hidden.");
            _editMode = Config.Bind("General", "HUD Edit Mode", false, "Drag HUD clusters with the mouse. Positions are saved automatically.");
            _fontSize = Config.Bind("Appearance", "Font Size", 11, new ConfigDescription("HUD font size.", new AcceptableValueRange<int>(8, 24)));
            _opacity = Config.Bind("Appearance", "Opacity", 0.58f, new ConfigDescription("HUD opacity.", new AcceptableValueRange<float>(0.15f, 1f)));
            _populationEnabled = Config.Bind("Population", "Enabled", true, "Enable raid population cluster.");
            _populationX = Config.Bind("Population", "Position X", 8f, new ConfigDescription("Left position.", new AcceptableValueRange<float>(0f, 4000f)));
            _populationY = Config.Bind("Population", "Position Y From Bottom", 8f, new ConfigDescription("Bottom margin.", new AcceptableValueRange<float>(0f, 2000f)));
            _statusEnabled = Config.Bind("Player Status", "Enabled", true, "Enable hydration, energy and weight cluster.");
            _statusX = Config.Bind("Player Status", "Position X", 8f, new ConfigDescription("Left position.", new AcceptableValueRange<float>(0f, 4000f)));
            _statusY = Config.Bind("Player Status", "Position Y From Bottom", 24f, new ConfigDescription("Bottom margin.", new AcceptableValueRange<float>(0f, 2000f)));
            _killFeedEnabled = Config.Bind("Kill Feed", "Enabled", false, "Enable kill feed. Event capture is implemented in the next stage.");
            _killFeedX = Config.Bind("Kill Feed", "Position X", 1500f, new ConfigDescription("Left position.", new AcceptableValueRange<float>(0f, 4000f)));
            _killFeedY = Config.Bind("Kill Feed", "Position Y", 100f, new ConfigDescription("Top position.", new AcceptableValueRange<float>(0f, 2000f)));
            Logger.LogInfo("SPT Tactical HUD 2.0.0 loaded");
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 0.5f; Refresh(); }
            if (!_inRaid && !_workAlways.Value) { _displayMode = 0; return; }
            if (_toggleKey.Value.IsDown()) { _displayMode = (_displayMode + 1) % 3; }
        }

        private void OnGUI()
        {
            bool allowed = _inRaid || _workAlways.Value;
            if (!allowed) return;
            EnsureStyles();

            bool editing = _editMode.Value;
            if ((_displayMode >= 1 || editing) && _populationEnabled.Value)
                DrawCluster(1, new Rect(_populationX.Value, Screen.height - _populationY.Value - 18f, 240f, 18f), PopulationText());
            if ((_displayMode >= 2 || editing) && _statusEnabled.Value)
                DrawCluster(2, new Rect(_statusX.Value, Screen.height - _statusY.Value - 18f, 300f, 18f), StatusText());
            if (editing && _killFeedEnabled.Value)
                DrawCluster(3, new Rect(_killFeedX.Value, _killFeedY.Value, 330f, 20f), "Scav > PMC  HEAD  200m");
        }

        private void EnsureStyles()
        {
            if (_textStyle == null) _textStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, padding = new RectOffset(0,0,0,0), margin = new RectOffset(0,0,0,0) };
            if (_shadowStyle == null) _shadowStyle = new GUIStyle(_textStyle);
            if (_editStyle == null) _editStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _textStyle.fontSize = _fontSize.Value;
            _shadowStyle.fontSize = _fontSize.Value;
            _textStyle.normal.textColor = new Color(.78f,.80f,.80f,_opacity.Value);
            _shadowStyle.normal.textColor = new Color(0f,0f,0f,Mathf.Min(1f,_opacity.Value + .22f));
        }

        private void DrawCluster(int id, Rect rect, string text)
        {
            if (_editMode.Value)
            {
                GUI.Box(rect, "", _editStyle);
                HandleDrag(id, rect);
            }
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _shadowStyle);
            GUI.Label(rect, text, _textStyle);
        }

        private void HandleDrag(int id, Rect rect)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)) { _dragCluster = id; _dragOffset = e.mousePosition - new Vector2(rect.x, rect.y); e.Use(); }
            if (e.type == EventType.MouseDrag && _dragCluster == id)
            {
                Vector2 p = e.mousePosition - _dragOffset;
                p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
                p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
                if (id == 1) { _populationX.Value = p.x; _populationY.Value = Screen.height - p.y - rect.height; }
                else if (id == 2) { _statusX.Value = p.x; _statusY.Value = Screen.height - p.y - rect.height; }
                else { _killFeedX.Value = p.x; _killFeedY.Value = p.y; }
                e.Use();
            }
            if (e.type == EventType.MouseUp && _dragCluster == id) { _dragCluster = 0; Config.Save(); e.Use(); }
        }

        private string PopulationText() => $"P {_pmcCount} | S {_scavCount} | B {_bossCount} | R {_reinforcedCount}";
        private string StatusText() => $"H {Mathf.RoundToInt(_hydration)} | E {Mathf.RoundToInt(_energy)} | W {Mathf.RoundToInt(_weight)}/{Mathf.RoundToInt(_maxWeight)}";

        private void Refresh()
        {
            try
            {
                object world = GetGameWorldInstance();
                if (world == null) { SetOutsideRaid(); return; }
                IEnumerable players = GetPlayers(world);
                if (players == null) { SetOutsideRaid(); return; }
                _inRaid = true;
                int p=0,s=0,b=0,r=0;
                object localPlayer = null;
                foreach (object player in players)
                {
                    if (player == null) continue;
                    if (IsTrue(ReadMember(player,"IsYourPlayer"))) { localPlayer = player; continue; }
                    if (!IsAlive(player)) continue;
                    string role=GetRole(player), side=GetSide(player);
                    if (IsBossRole(role)) b++; else if (IsReinforcedRole(role)) r++; else if (IsPmcSide(side)) p++; else if (IsScavSide(side)) s++;
                }
                _pmcCount=p; _scavCount=s; _bossCount=b; _reinforcedCount=r;
                RefreshPlayerStatus(localPlayer);
            }
            catch (Exception ex) { Logger.LogWarning("Tactical HUD refresh: " + ex.Message); }
        }

        private void RefreshPlayerStatus(object player)
        {
            if (player == null) { _hydration=_energy=_weight=_maxWeight=0; return; }
            object hc=ReadMember(player,"HealthController");
            _hydration=ReadFloatDeep(hc,"Hydration","Current") ?? ReadFloatDeep(hc,"Hydration","Value") ?? 0f;
            _energy=ReadFloatDeep(hc,"Energy","Current") ?? ReadFloatDeep(hc,"Energy","Value") ?? 0f;
            object pc=ReadMember(player,"Physical");
            _weight=ReadFloat(ReadMember(pc,"CurrentWeight")) ?? ReadFloat(ReadMember(player,"Weight")) ?? 0f;
            _maxWeight=ReadFloat(ReadMember(pc,"MaxWeight")) ?? ReadFloat(ReadMember(pc,"BaseWeightLimit")) ?? 0f;
        }

        private void SetOutsideRaid() { _inRaid=false; _pmcCount=_scavCount=_bossCount=_reinforcedCount=0; _hydration=_energy=_weight=_maxWeight=0; if (!_workAlways.Value) _displayMode=0; }
        private object GetGameWorldInstance() { if(_gameWorldType==null)_gameWorldType=FindType("EFT.GameWorld")??FindTypeByName("GameWorld"); if(_gameWorldType==null)return null; if(_singletonOpenType==null)_singletonOpenType=FindType("Comfort.Common.Singleton`1"); if(_singletonOpenType==null)return null; if(_singletonInstanceProperty==null)_singletonInstanceProperty=_singletonOpenType.MakeGenericType(_gameWorldType).GetProperty("Instance",BindingFlags.Public|BindingFlags.Static); return _singletonInstanceProperty?.GetValue(null,null); }
        private IEnumerable GetPlayers(object world) { if(_registeredPlayersMember==null){Type t=world.GetType();_registeredPlayersMember=(MemberInfo)t.GetProperty("RegisteredPlayers",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)??t.GetProperty("AllPlayers",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)??(MemberInfo)t.GetField("RegisteredPlayers",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);} object v=_registeredPlayersMember is PropertyInfo pi?pi.GetValue(world,null):(_registeredPlayersMember as FieldInfo)?.GetValue(world);return v as IEnumerable; }
        private static bool IsAlive(object p){object h=ReadMember(p,"HealthController");object a=h!=null?ReadMember(h,"IsAlive"):ReadMember(p,"IsAlive");return !(a is bool)|| (bool)a;}
        private static string GetSide(object p){object pr=ReadMember(p,"Profile"),i=ReadMember(pr,"Info");return (ReadMember(i,"Side")??ReadMember(pr,"Side"))?.ToString();}
        private static string GetRole(object p){object pr=ReadMember(p,"Profile"),i=ReadMember(pr,"Info"),s=ReadMember(i,"Settings")??ReadMember(pr,"Settings");return (ReadMember(s,"Role")??ReadMember(i,"Role"))?.ToString();}
        private static bool IsPmcSide(string s)=>s!=null&&(s.IndexOf("USEC",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("BEAR",StringComparison.OrdinalIgnoreCase)>=0);
        private static bool IsScavSide(string s)=>s!=null&&(s.IndexOf("Savage",StringComparison.OrdinalIgnoreCase)>=0||s.IndexOf("Scav",StringComparison.OrdinalIgnoreCase)>=0);
        private static bool IsBossRole(string r)=>!string.IsNullOrEmpty(r)&&r.IndexOf("boss",StringComparison.OrdinalIgnoreCase)>=0;
        private static bool IsReinforcedRole(string role){if(string.IsNullOrEmpty(role))return false;string r=role.ToLowerInvariant();return r.Contains("follower")||r.Contains("pmcbot")||r.Contains("exusec")||r.Contains("raider")||r.Contains("rogue")||r.Contains("sectant")||r.Contains("arena")||r.Contains("assaultgroup");}
        private static bool IsTrue(object v)=>v is bool&&(bool)v;
        private static float? ReadFloat(object v){if(v==null)return null;try{return Convert.ToSingle(v);}catch{return null;}}
        private static float? ReadFloatDeep(object o,string first,string second){object a=ReadMember(o,first);return ReadFloat(ReadMember(a,second))??ReadFloat(a);}
        private static object ReadMember(object o,string n){if(o==null)return null;Type t=o.GetType();try{PropertyInfo p=t.GetProperty(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null)return p.GetValue(o,null);}catch{}try{FieldInfo f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f.GetValue(o);}catch{}return null;}
        private static Type FindType(string n){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())try{Type t=a.GetType(n,false);if(t!=null)return t;}catch{}return null;}
        private static Type FindTypeByName(string n){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){try{Type t=a.GetTypes().FirstOrDefault(x=>x.Name==n);if(t!=null)return t;}catch(ReflectionTypeLoadException e){Type t=e.Types?.FirstOrDefault(x=>x!=null&&x.Name==n);if(t!=null)return t;}catch{}}return null;}
    }
}

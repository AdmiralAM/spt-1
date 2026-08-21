using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.popcounter", "SPT PopCounter", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private float _nextRefresh;
        private bool _visible = true;
        private int _pmcCount;
        private int _scavCount;
        private string _status = "waiting for raid";
        private GUIStyle _style;
        private Type _gameWorldType;
        private Type _singletonOpenType;
        private PropertyInfo _singletonInstanceProperty;
        private MemberInfo _registeredPlayersMember;

        private void Awake() => Logger.LogInfo("SPT PopCounter loaded");

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) _visible = !_visible;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            RefreshCounts();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _style.normal.textColor = Color.white;
            }
            string text = _status == null ? $"PMC: {_pmcCount}   SCAV: {_scavCount}" : $"PMC: {_pmcCount}   SCAV: {_scavCount}   [{_status}]";
            GUI.Label(new Rect(20f, 20f, 520f, 32f), text, _style);
        }

        private void RefreshCounts()
        {
            try
            {
                object world = GetGameWorldInstance();
                if (world == null) { _pmcCount = _scavCount = 0; _status = "waiting for raid"; return; }
                IEnumerable players = GetPlayers(world);
                if (players == null) { _status = "players unavailable"; return; }
                int pmc = 0, scav = 0;
                foreach (object player in players)
                {
                    if (player == null || IsTrue(ReadMember(player, "IsYourPlayer")) || !IsAlive(player)) continue;
                    string side = GetSide(player);
                    if (side == null) continue;
                    if (side.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0 || side.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0) pmc++;
                    else if (side.IndexOf("Savage", StringComparison.OrdinalIgnoreCase) >= 0 || side.IndexOf("Scav", StringComparison.OrdinalIgnoreCase) >= 0) scav++;
                }
                _pmcCount = pmc; _scavCount = scav; _status = null;
            }
            catch (Exception ex)
            {
                _status = "reflection error";
                Logger.LogWarning("PopCounter: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private object GetGameWorldInstance()
        {
            if (_gameWorldType == null) _gameWorldType = FindType("EFT.GameWorld") ?? FindTypeByName("GameWorld");
            if (_gameWorldType == null) return null;
            if (_singletonOpenType == null) _singletonOpenType = FindType("Comfort.Common.Singleton`1");
            if (_singletonOpenType == null) return null;
            if (_singletonInstanceProperty == null)
                _singletonInstanceProperty = _singletonOpenType.MakeGenericType(_gameWorldType).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return _singletonInstanceProperty?.GetValue(null, null);
        }

        private IEnumerable GetPlayers(object world)
        {
            if (_registeredPlayersMember == null)
            {
                Type t = world.GetType();
                _registeredPlayersMember = (MemberInfo)t.GetProperty("RegisteredPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? t.GetProperty("AllPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? (MemberInfo)t.GetField("RegisteredPlayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            object value = _registeredPlayersMember is PropertyInfo p ? p.GetValue(world, null) : (_registeredPlayersMember as FieldInfo)?.GetValue(world);
            return value as IEnumerable;
        }

        private static bool IsAlive(object player)
        {
            object hc = ReadMember(player, "HealthController");
            object alive = hc != null ? ReadMember(hc, "IsAlive") : ReadMember(player, "IsAlive");
            return !(alive is bool) || (bool)alive;
        }

        private static string GetSide(object player)
        {
            object profile = ReadMember(player, "Profile");
            object info = ReadMember(profile, "Info");
            object side = ReadMember(info, "Side") ?? ReadMember(profile, "Side");
            return side?.ToString();
        }

        private static bool IsTrue(object value) => value is bool && (bool)value;

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            Type t = instance.GetType();
            try { var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (p != null) return p.GetValue(instance, null); } catch { }
            try { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(instance); } catch { }
            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { try { Type t = a.GetType(fullName, false); if (t != null) return t; } catch { } }
            return null;
        }

        private static Type FindTypeByName(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { Type t = a.GetTypes().FirstOrDefault(x => x.Name == name); if (t != null) return t; }
                catch (ReflectionTypeLoadException e) { Type t = e.Types?.FirstOrDefault(x => x != null && x.Name == name); if (t != null) return t; }
                catch { }
            }
            return null;
        }
    }
}

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace SPTPopCounter
{
    [BepInPlugin("com.admiralam.spt.popcounter", "SPT PopCounter", "1.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private float _nextRefresh;
        private int _displayMode; // 0=hidden, 1=compact, 2=expanded
        private bool _inRaid;

        private int _pmcCount;
        private int _scavCount;
        private int _bossCount;
        private int _reinforcedCount;

        private GUIStyle _style;
        private Type _gameWorldType;
        private Type _singletonOpenType;
        private PropertyInfo _singletonInstanceProperty;
        private MemberInfo _registeredPlayersMember;

        private void Awake() => Logger.LogInfo("SPT PopCounter 1.1.0 loaded");

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.5f;
                RefreshCounts();
            }

            if (!_inRaid)
            {
                _displayMode = 0;
                return;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                _displayMode++;
                if (_displayMode > 2) _displayMode = 0;
            }
        }

        private void OnGUI()
        {
            if (!_inRaid || _displayMode == 0) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperRight,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
                _style.normal.textColor = new Color(1f, 1f, 1f, 0.48f);
            }

            const float rightMargin = 8f;
            const float topMargin = 6f;
            const float width = 150f;
            const float lineHeight = 14f;

            float x = Screen.width - width - rightMargin;
            string line1 = $"P:{_pmcCount} S:{_scavCount}";
            GUI.Label(new Rect(x, topMargin, width, lineHeight), line1, _style);

            if (_displayMode == 2)
            {
                string line2 = $"B:{_bossCount} R:{_reinforcedCount}";
                GUI.Label(new Rect(x, topMargin + lineHeight, width, lineHeight), line2, _style);
            }
        }

        private void RefreshCounts()
        {
            try
            {
                object world = GetGameWorldInstance();
                if (world == null)
                {
                    ResetOutsideRaid();
                    return;
                }

                IEnumerable players = GetPlayers(world);
                if (players == null)
                {
                    ResetOutsideRaid();
                    return;
                }

                _inRaid = true;

                int pmc = 0;
                int scav = 0;
                int boss = 0;
                int reinforced = 0;

                foreach (object player in players)
                {
                    if (player == null || IsTrue(ReadMember(player, "IsYourPlayer")) || !IsAlive(player)) continue;

                    string role = GetRole(player);
                    string side = GetSide(player);

                    if (IsBossRole(role))
                    {
                        boss++;
                        continue;
                    }

                    if (IsReinforcedRole(role))
                    {
                        reinforced++;
                        continue;
                    }

                    if (IsPmcSide(side))
                    {
                        pmc++;
                    }
                    else if (IsScavSide(side))
                    {
                        scav++;
                    }
                }

                _pmcCount = pmc;
                _scavCount = scav;
                _bossCount = boss;
                _reinforcedCount = reinforced;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("PopCounter refresh failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void ResetOutsideRaid()
        {
            _inRaid = false;
            _displayMode = 0;
            _pmcCount = 0;
            _scavCount = 0;
            _bossCount = 0;
            _reinforcedCount = 0;
        }

        private object GetGameWorldInstance()
        {
            if (_gameWorldType == null)
                _gameWorldType = FindType("EFT.GameWorld") ?? FindTypeByName("GameWorld");

            if (_gameWorldType == null) return null;

            if (_singletonOpenType == null)
                _singletonOpenType = FindType("Comfort.Common.Singleton`1");

            if (_singletonOpenType == null) return null;

            if (_singletonInstanceProperty == null)
            {
                _singletonInstanceProperty = _singletonOpenType
                    .MakeGenericType(_gameWorldType)
                    .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            }

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

            object value = _registeredPlayersMember is PropertyInfo p
                ? p.GetValue(world, null)
                : (_registeredPlayersMember as FieldInfo)?.GetValue(world);

            return value as IEnumerable;
        }

        private static bool IsAlive(object player)
        {
            object healthController = ReadMember(player, "HealthController");
            object alive = healthController != null
                ? ReadMember(healthController, "IsAlive")
                : ReadMember(player, "IsAlive");

            return !(alive is bool) || (bool)alive;
        }

        private static string GetSide(object player)
        {
            object profile = ReadMember(player, "Profile");
            object info = ReadMember(profile, "Info");
            object side = ReadMember(info, "Side") ?? ReadMember(profile, "Side");
            return side?.ToString();
        }

        private static string GetRole(object player)
        {
            object profile = ReadMember(player, "Profile");
            object info = ReadMember(profile, "Info");
            object settings = ReadMember(info, "Settings") ?? ReadMember(profile, "Settings");
            object role = ReadMember(settings, "Role") ?? ReadMember(info, "Role");
            return role?.ToString();
        }

        private static bool IsPmcSide(string side)
        {
            if (side == null) return false;
            return side.IndexOf("USEC", StringComparison.OrdinalIgnoreCase) >= 0
                || side.IndexOf("BEAR", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsScavSide(string side)
        {
            if (side == null) return false;
            return side.IndexOf("Savage", StringComparison.OrdinalIgnoreCase) >= 0
                || side.IndexOf("Scav", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBossRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            return role.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReinforcedRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;

            string r = role.ToLowerInvariant();
            return r.Contains("follower")
                || r.Contains("pmcbot")
                || r.Contains("exusec")
                || r.Contains("raider")
                || r.Contains("rogue")
                || r.Contains("sectant")
                || r.Contains("arena")
                || r.Contains("assaultgroup");
        }

        private static bool IsTrue(object value) => value is bool && (bool)value;

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            Type t = instance.GetType();

            try
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null) return p.GetValue(instance, null);
            }
            catch { }

            try
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(instance);
            }
            catch { }

            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = a.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        private static Type FindTypeByName(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = a.GetTypes().FirstOrDefault(x => x.Name == name);
                    if (t != null) return t;
                }
                catch (ReflectionTypeLoadException e)
                {
                    Type t = e.Types?.FirstOrDefault(x => x != null && x.Name == name);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SPTPopCounter
{
    // Reflection-only optional bridges adapted from CameronsWorks/BotCensus (MIT).
    // Neither MoreBotsAPI nor Fika is a required runtime dependency.
    internal static class CensusIntegrations
    {
        const string MoreBotsPluginId = "com.morebotsapi.tacticaltoaster";
        const string FikaPluginId = "com.fika.core";

        static bool moreBotsBroken, managerResolved, managerReady, registryResolved, sectionBroken;
        static PropertyInfo managerInstance, isFollower, sainSettings;
        static FieldInfo section;
        static MethodInfo getFactionsByRole, getRegistry;
        static Type roleType;
        static IDictionary registry;
        static Dictionary<string, bool> bossFactions;
        static readonly Dictionary<int, string> labels = new Dictionary<int, string>();

        static bool fikaResolved, fikaBroken;
        static MethodInfo tryGetCoopHandler;
        static MemberInfo fikaPlayers;

        public static void InvalidateMoreBots()
        {
            bossFactions = null;
            labels.Clear();
        }

        public static string FactionLabel(int role)
        {
            if (labels.TryGetValue(role, out string cached)) return cached;
            return labels[role] = GetLiveFaction(role) ?? RangeFallback(role) ?? RegistryFaction(role) ?? "Custom";
        }

        public static bool IsEscort(int role)
        {
            IDictionary values = Registry();
            if (values == null) return false;
            try
            {
                object custom = values[role];
                return custom != null && isFollower.GetValue(custom, null) is bool follower && follower;
            }
            catch (Exception ex)
            {
                DisableMoreBots(ex);
                return false;
            }
        }

        public static bool FactionHasBoss(string faction)
        {
            if (bossFactions == null) BuildBossFactions();
            return bossFactions.TryGetValue(faction, out bool value) && value;
        }

        static void BuildBossFactions()
        {
            bossFactions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            IDictionary values = Registry();
            if (values == null) return;
            try
            {
                foreach (DictionaryEntry entry in values)
                {
                    int role;
                    try { role = Convert.ToInt32(entry.Key); } catch { continue; }
                    if (entry.Value == null || !(isFollower.GetValue(entry.Value, null) is bool follower)) continue;
                    string faction = FactionLabel(role);
                    bool leads = !follower;
                    bossFactions[faction] = bossFactions.TryGetValue(faction, out bool had) ? had || leads : leads;
                }
            }
            catch (Exception ex) { DisableMoreBots(ex); }
        }

        static string GetLiveFaction(int role)
        {
            object manager = Manager();
            if (manager == null || getFactionsByRole == null || roleType == null) return null;
            try
            {
                object roleValue = roleType.IsEnum ? Enum.ToObject(roleType, role) : Convert.ChangeType(role, roleType);
                IList names = getFactionsByRole.Invoke(manager, new[] { roleValue }) as IList;
                return names == null || names.Count == 0 ? null : Prettify(names[0] as string);
            }
            catch (Exception ex) { DisableMoreBots(ex); return null; }
        }

        static object Manager()
        {
            if (moreBotsBroken) return null;
            if (!managerResolved) ResolveManager();
            if (!managerReady) return null;
            try { return managerInstance.GetValue(null, null); }
            catch (Exception ex) { DisableMoreBots(ex); return null; }
        }

        static void ResolveManager()
        {
            managerResolved = true;
            if (!Chainloader.PluginInfos.ContainsKey(MoreBotsPluginId)) return;
            Type manager = FindType("MoreBotsAPI.Components.FactionManager");
            if (manager == null) return;
            managerInstance = FindStaticProperty(manager, "Instance");
            foreach (MethodInfo method in manager.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "GetFactionsByRole") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                getFactionsByRole = method;
                roleType = parameters[0].ParameterType;
                break;
            }
            managerReady = managerInstance != null && getFactionsByRole != null;
        }

        static IDictionary Registry()
        {
            if (registry != null) return registry;
            if (moreBotsBroken) return null;
            if (!registryResolved) ResolveRegistry();
            if (getRegistry == null || isFollower == null) return null;
            try { return registry = getRegistry.Invoke(null, null) as IDictionary; }
            catch (Exception ex) { DisableMoreBots(ex); return null; }
        }

        static void ResolveRegistry()
        {
            registryResolved = true;
            if (!Chainloader.PluginInfos.ContainsKey(MoreBotsPluginId)) return;
            Type manager = FindType("MoreBotsAPI.CustomWildSpawnTypeManager");
            Type custom = FindType("MoreBotsAPI.CustomWildSpawnType");
            if (manager == null || custom == null) return;
            getRegistry = manager.GetMethod("GetCustomWildSpawnTypeDict", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            isFollower = custom.GetProperty("IsFollower", BindingFlags.Public | BindingFlags.Instance);
            sainSettings = custom.GetProperty("SAINSettings", BindingFlags.Public | BindingFlags.Instance);
            Type sain = FindType("MoreBotsAPI.SAINSettings");
            if (sain != null) section = sain.GetField("Section", BindingFlags.Public | BindingFlags.Instance);
        }

        static string RegistryFaction(int role)
        {
            IDictionary values = Registry();
            if (values == null || sectionBroken || sainSettings == null || section == null) return null;
            try
            {
                object custom = values[role];
                object settings = custom == null ? null : sainSettings.GetValue(custom, null);
                return settings == null ? null : Prettify(section.GetValue(settings) as string);
            }
            catch (Exception ex)
            {
                sectionBroken = true;
                Debug.LogWarning("[Admiral Tactical HUD] MoreBotsAPI faction metadata unavailable; using known ranges: " + ex.Message);
                return null;
            }
        }

        static void DisableMoreBots(Exception ex)
        {
            if (moreBotsBroken) return;
            moreBotsBroken = true;
            Debug.LogWarning("[Admiral Tactical HUD] MoreBotsAPI census bridge disabled; using known ranges: " + ex.Message);
        }

        public static bool TryGetFikaPlayers(out IEnumerable players)
        {
            players = null;
            if (fikaBroken || !Chainloader.PluginInfos.ContainsKey(FikaPluginId)) return false;
            if (!fikaResolved) ResolveFika();
            if (tryGetCoopHandler == null) return false;
            try
            {
                object[] arguments = { null };
                if (!(tryGetCoopHandler.Invoke(null, arguments) is bool ok) || !ok || arguments[0] == null) return false;
                object collection = ReadMember(arguments[0], fikaPlayers);
                object values = ReadNamedMember(collection, "Values");
                players = values as IEnumerable ?? collection as IEnumerable;
                return players != null;
            }
            catch (Exception ex)
            {
                fikaBroken = true;
                Debug.LogWarning("[Admiral Tactical HUD] Fika census source unavailable; using local players: " + ex.Message);
                return false;
            }
        }

        static void ResolveFika()
        {
            fikaResolved = true;
            Type handler = FindType("Fika.Core.Main.Components.CoopHandler");
            if (handler == null) return;
            foreach (MethodInfo method in handler.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "TryGetCoopHandler") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsByRef) { tryGetCoopHandler = method; break; }
            }
            Type resultType = tryGetCoopHandler?.GetParameters()[0].ParameterType.GetElementType();
            if (resultType != null)
                fikaPlayers = (MemberInfo)resultType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance) ??
                              resultType.GetField("Players", BindingFlags.Public | BindingFlags.Instance);
        }

        static object ReadMember(object owner, MemberInfo member)
        {
            if (owner == null || member == null) return null;
            if (member is PropertyInfo property) return property.GetValue(owner, null);
            if (member is FieldInfo field) return field.GetValue(owner);
            return null;
        }

        static object ReadNamedMember(object owner, string name)
        {
            if (owner == null) return null;
            Type type = owner.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return property.GetValue(owner, null);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(owner);
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

        static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            switch (raw.ToLowerInvariant())
            {
                case "ruaf": return "RUAF";
                case "remnant": return "Remnant";
                case "untar": return "UNTAR";
                case "blackdiv": return "Black Division";
                case "blackdivision": return "Blackout";
                default: return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
            }
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { Type type = assembly.GetType(fullName, false); if (type != null) return type; }
                catch { }
            }
            return null;
        }

        static PropertyInfo FindStaticProperty(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, flags);
                if (property != null) return property;
            }
            return null;
        }
    }
}

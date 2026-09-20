using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTBeltArmbandInventory
{
    internal static class PackNStrapCompatibility
    {
        internal const string PluginGuid = "com.wtt.packnstrap";

        internal static bool IsClientPresent(IEnumerable<string> pluginGuids)
        {
            if (pluginGuids == null) return false;
            foreach (string guid in pluginGuids)
                if (string.Equals(guid, PluginGuid, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static bool IsServerPresent(IEnumerable<string> assemblyNames)
        {
            if (assemblyNames == null) return false;
            foreach (string name in assemblyNames)
            {
                if (string.Equals(name, "WTT-PackNStrapServer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "WTT-PackNStrap", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static bool IsServerPresentNow()
        {
            return IsServerPresent(AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name ?? string.Empty));
        }
    }
}

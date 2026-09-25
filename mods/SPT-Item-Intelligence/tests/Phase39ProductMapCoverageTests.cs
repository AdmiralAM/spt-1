using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class Phase39ProductMapCoverageTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string module = Path.Combine(root, "mods", "SPT-Item-Intelligence");
        string client = Path.Combine(module, "src");
        string server = Path.Combine(module, "server");
        string map = File.ReadAllText(Path.Combine(module, "docs", "stable-beta-product-map.md"));

        foreach (string file in Directory.GetFiles(client, "*.cs").OrderBy(Path.GetFileName))
            Expect(map.Contains("`" + Path.GetFileName(file) + "`", StringComparison.Ordinal),
                "client source unit is mapped: " + Path.GetFileName(file), ref assertions);
        foreach (string file in Directory.GetFiles(server, "*.cs").OrderBy(Path.GetFileName))
            Expect(map.Contains("`" + Path.GetFileName(file) + "`", StringComparison.Ordinal),
                "server source unit is mapped: " + Path.GetFileName(file), ref assertions);

        string plugin = File.ReadAllText(Path.Combine(client, "Plugin.cs"));
        string settings = File.ReadAllText(Path.Combine(client, "UiSettings.cs"));
        string allClient = string.Join("\n", Directory.GetFiles(client, "*.cs").Select(File.ReadAllText));
        string serverSource = File.ReadAllText(Path.Combine(server, "ServerMod.cs"));

        foreach (Match match in Regex.Matches(allClient, "const\\s+string\\s+HarmonyId\\s*=\\s*\"([^\"]+)\""))
            Expect(map.Contains("`" + match.Groups[1].Value + "`", StringComparison.Ordinal),
                "Harmony owner is mapped: " + match.Groups[1].Value, ref assertions);
        foreach (Match match in Regex.Matches(plugin, "BepInDependency\\(\"([^\"]+)\""))
            Expect(map.Contains("`" + match.Groups[1].Value + "`", StringComparison.Ordinal),
                "soft dependency is mapped: " + match.Groups[1].Value, ref assertions);

        HashSet<string> configEntries = new HashSet<string>(StringComparer.Ordinal);
        AddMatches(configEntries, settings, "Module\\(config,\\s*\"([^\"]+)\"", "Modules");
        AddMatches(configEntries, settings, "config\\.Bind\\(\"([^\"]+)\",\\s*\"([^\"]+)\"", null);
        AddMatches(configEntries, settings, "ColorEntry\\(config,\\s*\"([^\"]+)\",\\s*\"([^\"]+)\"", null);
        foreach (string entry in configEntries.OrderBy(value => value))
            Expect(map.Contains("`" + entry + "`", StringComparison.Ordinal), "F12 entry is mapped: " + entry, ref assertions);

        string[] requiredRuntimeTokens = {
            "Item Intelligence Admiral.dll", "Item Intelligence Admiral Server.dll",
            "/spt-item-intelligence/v2/snapshot", "ModMetadata", "RequirementDataService",
            "ItemIntelligenceRouter", "ItemIntelligenceLoadNotice", "Plugin.Update", "Plugin.OnGUI",
            "RefreshInventorySessionAfterBurst", "Task.Run", "RaidInventoryPollSeconds",
            "OnPointerEnter", "OnPointerExit", "AmandsSenseItem.SetSense",
            "AmandsSenseContainer.SetSense", "AmandsSenseContainer.UpdateSense",
            "AmandsSenseItem.RemoveLootItem", "AmandsSenseClass.Clear",
            "CompatibilityHighlighter.HoverHighlightDriver.GetAllItemViews"
        };
        foreach (string token in requiredRuntimeTokens)
            Expect(map.Contains("`" + token + "`", StringComparison.Ordinal), "runtime surface is mapped: " + token, ref assertions);
        Expect(serverSource.Contains("[Injectable") && serverSource.Contains("RequirementDataContract.SnapshotRoute"),
            "guard audits the live DI/router source", ref assertions);
        return assertions;
    }

    static void AddMatches(HashSet<string> result, string source, string pattern, string fixedSection)
    {
        foreach (Match match in Regex.Matches(source, pattern, RegexOptions.Multiline))
        {
            string section = fixedSection ?? match.Groups[1].Value;
            string name = fixedSection == null ? match.Groups[2].Value : match.Groups[1].Value;
            result.Add(section + " / " + name);
        }
    }

    static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "mods", "SPT-Item-Intelligence"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 39 assertion failed: " + message);
    }
}

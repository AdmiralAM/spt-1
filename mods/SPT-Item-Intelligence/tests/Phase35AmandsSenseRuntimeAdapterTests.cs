using System;
using System.IO;

static class Phase35AmandsSenseRuntimeAdapterTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string module = Path.Combine(root, "mods", "SPT-Item-Intelligence");
        string adapter = File.ReadAllText(Path.Combine(module, "src", "AmandsSenseIntegration.cs"));
        string plugin = File.ReadAllText(Path.Combine(module, "src", "Plugin.cs"));
        string settings = File.ReadAllText(Path.Combine(module, "src", "UiSettings.cs"));
        string project = File.ReadAllText(Path.Combine(module, "src", "SPT-Item-Intelligence.csproj"));

        Expect(plugin.Contains("BepInDependency(\"xyz.drakia.Sense\", BepInDependency.DependencyFlags.SoftDependency)"),
            "Sense load ordering is optional and never becomes a mandatory plugin dependency", ref assertions);
        Expect(adapter.Contains("FindAssembly(\"AmandsSense\")") &&
               adapter.Contains("AmandsSense.Components.AmandsSenseItem") &&
               adapter.Contains("AmandsSense.Components.AmandsSenseClass"),
            "the adapter discovers the installed Sense runtime without a compile-time type dependency", ref assertions);
        Expect(adapter.Contains("FindMethod(itemType, \"SetSense\", 1)") &&
               adapter.Contains("FindMethod(itemType, \"RemoveLootItem\", 1)") &&
               adapter.Contains("FindMethod(senseClass, \"Clear\", 0)"),
            "bounded lifecycle hooks cover presentation, pickup/drop and raid reset", ref assertions);
        Expect(adapter.Contains("!settings.SenseIntegration || !settings.SenseRequiredItems") &&
               plugin.Contains("senseIntegration.Dispose();"),
            "disabled integration exits immediately and removes its runtime patches", ref assertions);
        Expect(adapter.Contains("SenseRequirementMapper.Map(ledger.Evaluate(templateId, allocation, fir))") &&
               adapter.Contains("if (!presentation.OverridesSense) return;"),
            "the shared deterministic requirement decision is the only reason Sense presentation is changed", ref assertions);
        Expect(adapter.Contains("icon_quest.png") && adapter.Contains("icon_barter_building.png") && adapter.Contains("icon_info.png"),
            "the adapter reuses Sense-owned runtime sprites instead of copying or shipping its assets", ref assertions);
        Expect(adapter.Contains("ledger.Observe(id, template, stack, fir)") &&
               adapter.Contains("if (itemId.Length > 0 && pickedItemIds.Remove(itemId)) ledger.Remove(itemId);") &&
               adapter.Contains("void ResetRaid() { ledger.Reset(); pickedItemIds.Clear(); }"),
            "successive pickup, returned loot and raid reset update reservations without polling", ref assertions);
        Expect(settings.Contains("config.Bind(\"Amands Sense\", \"Integration\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Required Items\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Secondary Reason Outline\", true") &&
               settings.Contains("config.Bind(\"Amands Sense\", \"Remaining Count Text\", true"),
            "the required Sense layers are independently configurable in Item Intelligence F12", ref assertions);
        Expect(settings.Contains("\"Amands Sense Colors\", \"Active Quest\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Hideout\"") &&
               settings.Contains("\"Amands Sense Colors\", \"Future Quest\""),
            "active quest, hideout and future quest colors remain independently configurable", ref assertions);
        Expect(!project.Contains("AmandsSense", StringComparison.OrdinalIgnoreCase) &&
               !adapter.Contains("using AmandsSense", StringComparison.OrdinalIgnoreCase),
            "Amands Sense is not a build dependency and remains optional", ref assertions);
        Expect(!adapter.Contains("File.Write") && !adapter.Contains("Items.json") && !adapter.Contains("Sense.cfg"),
            "the integration never rewrites Sense files or user configuration", ref assertions);

        return assertions;
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
        if (!condition) throw new InvalidOperationException("Phase 35 assertion failed: " + message);
    }
}

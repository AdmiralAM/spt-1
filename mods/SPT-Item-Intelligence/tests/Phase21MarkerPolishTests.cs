using System;
using System.IO;

static class Phase21MarkerPolishTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string settings = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "UiSettings.cs"));
        string overlay = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));

        Expect(settings.Contains("enum ItemMarkerSide") && settings.Contains("Left") && settings.Contains("Right"),
            "marker side exposes left/right choices", ref assertions);
        Expect(settings.Contains("AcceptableValueRange<float>(-80f, 80f)"),
            "horizontal marker offset has symmetric extended travel", ref assertions);
        Expect(overlay.Contains("settings.MarkerSide == ItemMarkerSide.Right"),
            "runtime placement switches by selected marker side", ref assertions);
        Expect(overlay.Contains("right ? -settings.MarkerOffsetX : settings.MarkerOffsetX"),
            "positive X offset moves inward from either selected edge", ref assertions);
        Expect(overlay.Contains("size * 0.78f"),
            "marker glyph is slightly smaller inside the same hit box", ref assertions);
        Expect(settings.Contains("Glow Strength") && settings.Contains("Glow Radius") && overlay.Contains("settings.MarkerGlow"),
            "marker glow is configurable and applied at runtime", ref assertions);
        Expect(overlay.Contains("glowColor.a = settings.MarkerGlowStrength * settings.MarkerOpacity"),
            "glow alpha follows configured strength and marker opacity", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 21 assertion failed: " + message);
    }
}

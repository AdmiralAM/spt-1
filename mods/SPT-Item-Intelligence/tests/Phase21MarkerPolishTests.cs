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
        Expect(settings.Contains("Tooltip background opacity; 0 disables the background completely.") &&
               settings.Contains("AcceptableValueRange<float>(0f, 1.00f)"),
            "tooltip background can be fully disabled with zero opacity", ref assertions);
        Expect(overlay.Contains("settings.MarkerSide == ItemMarkerSide.Right"),
            "runtime placement switches by selected marker side", ref assertions);
        Expect(overlay.Contains("right ? -settings.MarkerOffsetX : settings.MarkerOffsetX"),
            "positive X offset moves inward from either selected edge", ref assertions);
        Expect(overlay.Contains("size * 0.78f"),
            "marker glyph is slightly smaller inside the same hit box", ref assertions);
        Expect(!settings.Contains("Glow Strength") && !settings.Contains("Glow Radius") && !overlay.Contains("settings.MarkerGlow"),
            "rejected Outline glow path stays removed", ref assertions);
        Expect(settings.Contains("\"Halo\"") && settings.Contains("\"Halo Strength\"") &&
               settings.Contains("AcceptableValueRange<float>(0f, 0.50f)"),
            "soft halo exposes only a toggle and bounded strength control", ref assertions);
        Expect(overlay.Contains("SPTItemIntelligenceHalo") && overlay.Contains("static Sprite haloSprite") &&
               overlay.Contains("Texture2D texture = new Texture2D") && overlay.Contains("FilterMode.Bilinear"),
            "halo is a reusable radial image layer rather than duplicated glyph outlines", ref assertions);
        Expect(overlay.Contains("float haloSize = size * 1.70f") &&
               overlay.Contains("haloColor.a = settings.MarkerHaloStrength * settings.MarkerOpacity"),
            "halo spread follows marker size and inherits marker opacity semantics", ref assertions);
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

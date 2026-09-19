using System;
using System.IO;

static class Phase24HotPathOptimizationTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "Plugin.cs"));
        string server = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "server", "ServerMod.cs"));
        string compatibility = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "CompatibilityHighlighterIntegration.cs"));
        string sink = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));

        Expect(renderer.Contains("static string[] lineBuffer") && renderer.Contains("static float[] rowHeightBuffer"),
            "tooltip renderer reuses line and row-height buffers across repaint calls", ref assertions);
        Expect(renderer.Contains("EnsureBuffers(lineCount)"),
            "tooltip buffers grow only when line capacity is insufficient", ref assertions);
        Expect(!renderer.Contains("string[] lines = new string[lineCount]") && !renderer.Contains("float[] rowHeights = new float[lineCount]"),
            "tooltip repaint path does not allocate per-draw line arrays", ref assertions);
        Expect(renderer.Contains("static GUIStyle cachedLabel") && renderer.Contains("static GUIStyle cachedSemanticLabel"),
            "tooltip renderer caches GUI styles instead of rebuilding them every repaint", ref assertions);
        Expect(renderer.Contains("object.ReferenceEquals(cachedSkin, skin)"),
            "cached styles are rebuilt only when the active GUI skin changes", ref assertions);
        Expect(renderer.Contains("clipping = TextClipping.Clip") && renderer.Contains("wordWrap = true") && renderer.Contains("label.CalcHeight"),
            "performance pass preserves the established tooltip geometry contract", ref assertions);
        Expect(plugin.Contains("if (raidLedger.IsRaidSessionActive)") && plugin.Contains("InventorySnapshotMinimumSeconds") &&
               plugin.Contains("InventorySnapshotSettleSeconds"),
            "full server snapshots are suppressed in raid and menu bursts are coalesced after hideout state settles", ref assertions);
        Expect(server.Contains("FreezeProfile(profileHelper.GetPmcProfile(sessionId))") && server.Contains("Deserialize<JsonElement>"),
            "server serializes an immutable profile generation instead of a mutating live object", ref assertions);
        Expect(compatibility.Contains("GetAllItemViews") && compatibility.Contains("MergeRegisteredViews") &&
               compatibility.Contains("activeInHierarchy") && !compatibility.Contains("FindObjectsOfType") &&
               !compatibility.Contains("Resources.FindObjectsOfTypeAll"),
            "CompatibilityHighlighter receives detached container views without a global Unity scan", ref assertions);
        Expect(compatibility.Contains("TypeBuilder") && compatibility.Contains("CreateTypeInfo()") &&
               !compatibility.Contains("DynamicMethod"),
            "CompatibilityHighlighter bridge finalizes a conventional runtime patch method through the Mono-compatible path", ref assertions);
        Expect(compatibility.Contains("if (postfix == null)") &&
               compatibility.Contains("runtime patch method was not finalized by Mono"),
            "CompatibilityHighlighter bridge cannot report installed when Mono returned no patch method", ref assertions);
        Expect(compatibility.Contains("BindingFlags.Static | BindingFlags.Public") &&
               compatibility.Contains("DefineParameter(1, ParameterAttributes.None, \"__result\")") &&
               compatibility.Contains("if (merge == null) return null"),
            "CompatibilityHighlighter bridge resolves its public merge callback and names Harmony's result parameter", ref assertions);
        Expect(sink.Contains("CompatibilityHighlighterIntegration.Track(itemView)") &&
               sink.Contains("CompatibilityHighlighterIntegration.Untrack(itemView)"),
            "the compatibility bridge follows the existing ItemView lifecycle", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 24 assertion failed: " + message);
    }
}

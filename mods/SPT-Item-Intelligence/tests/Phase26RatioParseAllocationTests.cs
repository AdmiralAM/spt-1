using System;
using System.IO;

static class Phase26RatioParseAllocationTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));

        Expect(renderer.Contains("TryParsePositiveInt"),
            "ratio parsing uses index-based integer parser", ref assertions);
        Expect(!renderer.Contains("int.TryParse(line.Substring(start, slash - start)") &&
               !renderer.Contains("int.TryParse(line.Substring(slash + 1, end - slash - 1)"),
            "ratio parsing avoids substring allocations", ref assertions);
        Expect(renderer.Contains("value > (int.MaxValue - digit) / 10"),
            "manual parser rejects integer overflow", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 26 assertion failed: " + message);
    }
}

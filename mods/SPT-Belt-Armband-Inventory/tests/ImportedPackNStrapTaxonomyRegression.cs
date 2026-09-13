using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ImportedPackNStrapTaxonomyRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string source = File.ReadAllText(Path.Combine(FindModuleRoot(), "src", "RuntimeCustomBeltTypes.cs"));
        Require(source.Contains("680fce2ec7b9b222270f074c", StringComparison.Ordinal), "custom template parent must be registered");
        Require(source.Contains("680fd1dae5044e670a092e16", StringComparison.Ordinal), "custom container parent must be registered");
        Require(source.Contains("68154651f849fb4e7d816738", StringComparison.Ordinal), "custom secure-container parent must be registered");
        Require(source.Contains("6815465859b8c6ff13f94026", StringComparison.Ordinal), "custom belt parent must be registered");
        Require(source.Contains("RegisterIfPresent", StringComparison.Ordinal), "private import registration must run before item data is requested");
        Require(source.Contains("ImportedPackNStrapTypeRegistry.Rollback", StringComparison.Ordinal), "private mappings must have owned rollback");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Imported Pack 'n' Strap taxonomy regression failed: " + message + ".");
    }

    static string FindModuleRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "Plugin.cs"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate B&A&HB module root.");
    }
}

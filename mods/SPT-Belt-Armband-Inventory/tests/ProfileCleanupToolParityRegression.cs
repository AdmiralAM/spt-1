using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ProfileCleanupToolParityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Profile cleanup tool parity regression failed: module root could not be resolved.");

        string policyPath = Path.Combine(root, "server", "ProfileCleanupPolicy.cs");
        string scriptPath = Path.Combine(root, "profile-safety", "Clean-BAndHBProfile.ps1");
        string policy = File.ReadAllText(policyPath);
        string script = File.ReadAllText(scriptPath);

        Require(policy, "CollectInstanceIdCounts(profile)", "compiled cleanup must pre-count serialized instance IDs");
        Require(policy, "IsUniqueInstanceId(id, instanceIdCounts)", "compiled cleanup must gate cascade IDs by exact cardinality");
        Require(policy, "count == 1", "compiled cleanup uniqueness must mean exactly one serialized instance ID");

        Require(script, "$idCounts = @{}", "offline cleanup must pre-count serialized instance IDs");
        Require(script, "function Test-UniqueInstanceId", "offline cleanup must expose the same uniqueness gate");
        Require(script, "([int]$idCounts[$Id] -eq 1)", "offline cleanup uniqueness must mean exactly one serialized instance ID");
        Require(script, "if (Test-UniqueInstanceId $id)", "direct owned IDs must enter cascade authority only when unique");
        Require(script, "if ((Test-UniqueInstanceId $id) -and -not $removedIds.ContainsKey($id))", "removed descendant IDs must continue cascade only when unique");

        if (script.Contains("$removedIds[[string]$idProperty.Value] = $true", StringComparison.Ordinal))
            throw new InvalidOperationException("Profile cleanup tool parity regression failed: offline recovery restored unconditional owned-ID cascade authority.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "profile-safety", "Clean-BAndHBProfile.ps1");
            string policy = Path.Combine(current.FullName, "server", "ProfileCleanupPolicy.cs");
            if (File.Exists(candidate) && File.Exists(policy)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "profile-safety", "Clean-BAndHBProfile.ps1");
            string directPolicy = Path.Combine(current.FullName, "server", "ProfileCleanupPolicy.cs");
            if (File.Exists(direct) && File.Exists(directPolicy)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "profile-safety", "Clean-BAndHBProfile.ps1"))
                && File.Exists(Path.Combine(nested, "server", "ProfileCleanupPolicy.cs")))
                return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string text, string token, string message)
    {
        if (!text.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Profile cleanup tool parity regression failed: " + message + ".");
    }
}
using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ValidateWorkflowAuthorityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindRepositoryRoot();
        if (root == null)
            throw new InvalidOperationException("Validate workflow authority regression failed: repository root could not be resolved.");

        string workflowPath = Path.Combine(root, ".github", "workflows", "belt-armband-validate.yml");
        string workflow = File.ReadAllText(workflowPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        string[] mandatoryStaticGuards =
        {
            "check_hotpaths.py",
            "check_reload_access.py",
            "check_version_contract.py",
            "check_slot_registration.py",
            "check_doc_authority.py",
            "check_product_contract.py",
            "check_identity_manifest.py",
            "check_legacy_conflict_gate.py",
            "check_offer_host_contract.py",
            "check_offer_template_boundary.py",
            "check_protection_sync.py",
            "check_server_patch_registration.py",
            "check_taxonomy_ownership.py",
            "check_compact_layout.py"
        };

        foreach (string guard in mandatoryStaticGuards)
        {
            string command = "python mods/SPT-Belt-Armband-Inventory/tools/" + guard;
            int first = workflow.IndexOf(command, StringComparison.Ordinal);
            if (first < 0)
                throw new InvalidOperationException("Validate workflow authority regression failed: mandatory static guard is not executed: " + guard + ".");
            if (workflow.IndexOf(command, first + command.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Validate workflow authority regression failed: mandatory static guard is executed more than once: " + guard + ".");
        }

        RequireExactlyOnce(workflow,
            "dotnet run --project mods/SPT-Belt-Armband-Inventory/tests/SPT-Belt-Armband-Inventory.Tests.csproj -c Release",
            "deterministic regression suite");
        RequireExactlyOnce(workflow,
            "& 'mods/SPT-Belt-Armband-Inventory/profile-safety/Clean-BAndHBProfile.ps1' -ProfilePath $fixture",
            "offline profile recovery exercise");
        RequireExactlyOnce(workflow,
            "dotnet build mods/SPT-Belt-Armband-Inventory/src/SPT-Belt-Armband-Inventory.csproj -c Release",
            "client build");
        RequireExactlyOnce(workflow,
            "dotnet build mods/SPT-Belt-Armband-Inventory/server/SPT-Belt-Armband-Inventory.Server.csproj -c Release",
            "server build");
        RequireExactlyOnce(workflow,
            "uses: actions/upload-artifact@v4",
            "RC artifact upload");

        int firstGuard = workflow.IndexOf("check_hotpaths.py", StringComparison.Ordinal);
        int regressions = workflow.IndexOf("dotnet run --project mods/SPT-Belt-Armband-Inventory/tests/SPT-Belt-Armband-Inventory.Tests.csproj -c Release", StringComparison.Ordinal);
        int recovery = workflow.IndexOf("Clean-BAndHBProfile.ps1' -ProfilePath $fixture", StringComparison.Ordinal);
        int clientBuild = workflow.IndexOf("dotnet build mods/SPT-Belt-Armband-Inventory/src/SPT-Belt-Armband-Inventory.csproj -c Release", StringComparison.Ordinal);
        int serverBuild = workflow.IndexOf("dotnet build mods/SPT-Belt-Armband-Inventory/server/SPT-Belt-Armband-Inventory.Server.csproj -c Release", StringComparison.Ordinal);
        int staging = workflow.IndexOf("Stage B&A&HB #2 MOD SPT RC1", StringComparison.Ordinal);
        int upload = workflow.IndexOf("uses: actions/upload-artifact@v4", StringComparison.Ordinal);

        if (firstGuard < 0 || regressions < 0 || recovery < 0 || clientBuild < 0 || serverBuild < 0 || staging < 0 || upload < 0
            || !(firstGuard < regressions && regressions < recovery && recovery < clientBuild && clientBuild < serverBuild && serverBuild < staging && staging < upload))
            throw new InvalidOperationException("Validate workflow authority regression failed: guard/regression/recovery/build/stage/upload ordering drifted.");
    }

    private static void RequireExactlyOnce(string text, string token, string label)
    {
        int first = text.IndexOf(token, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(token, first + token.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Validate workflow authority regression failed: " + label + " must appear exactly once.");
    }

    private static string? FindRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".github", "workflows", "belt-armband-validate.yml")))
                return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".github", "workflows", "belt-armband-validate.yml")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}

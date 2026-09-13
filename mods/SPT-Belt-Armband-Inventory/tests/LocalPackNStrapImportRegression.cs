using System;
using System.IO;

namespace SPTBeltArmbandInventory.Tests;

internal static class LocalPackNStrapImportRegression
{
    internal static void Run()
    {
        string root = FindModuleRoot();
        string script = File.ReadAllText(Path.Combine(root, "tools", "Import-PackNStrapLocal.ps1"));
        string clientProject = File.ReadAllText(Path.Combine(root, "src", "SPT-Belt-Armband-Inventory.csproj"));
        string serverProject = File.ReadAllText(Path.Combine(root, "server", "SPT-Belt-Armband-Inventory.Server.csproj"));
        string slotRegistration = File.ReadAllText(Path.Combine(root, "server", "DedicatedEquipmentSlotRegistration.cs"));
        string dedicatedAssort = File.ReadAllText(Path.Combine(root, "server", "DedicatedWearableAssort.cs"));
        string candidateAssort = File.ReadAllText(Path.Combine(root, "server", "RuntimeCandidateAssort.cs"));
        string walletAssort = File.ReadAllText(Path.Combine(root, "server", "WristWalletAssort.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Require(script.Contains("$item['addtoInventorySlots'] = @()", StringComparison.Ordinal), "import must remove upstream ArmBand publication");
        Require(script.Contains("'SPT.Server','SPT.Launcher','EscapeFromTarkov'", StringComparison.Ordinal), "deployment must stop while SPT/EFT is active");
        Require(script.Contains("Move-Item -LiteralPath $serverSource", StringComparison.Ordinal) && script.Contains("Move-Item -LiteralPath $clientSource", StringComparison.Ordinal), "original runtime must be preserved outside active paths");
        Require(script.Contains("packnstrap-disabled-*", StringComparison.Ordinal), "deployment must be repeatable from the preserved runtime");
        Require(clientProject.Contains("Condition=\"'$(PackNStrapSourceRoot)' != ''\"", StringComparison.Ordinal), "client import must remain opt-in");
        Require(serverProject.Contains("PACKNSTRAP_LOCAL_IMPORT", StringComparison.Ordinal), "server import must remain opt-in");
        Require(slotRegistration.Contains("LocalPackNStrapImportState.BeltParentId", StringComparison.Ordinal), "dedicated Belt must accept the imported parent only in private mode");
        Require(dedicatedAssort.Contains("|| LocalPackNStrapImportState.Enabled", StringComparison.Ordinal), "private import must suppress the duplicate Admiral belt offer");
        Require(candidateAssort.Contains("|| LocalPackNStrapImportState.Enabled", StringComparison.Ordinal), "private import must suppress the duplicate Admiral armband offer");
        Require(walletAssort.Contains("|| LocalPackNStrapImportState.Enabled", StringComparison.Ordinal), "private import must suppress the duplicate Admiral wallet offer");
        Require(plugin.Contains("B&A&HB companion mode initialized with exact Admiral wallet payment sources", StringComparison.Ordinal)
            && !plugin.Contains("without Belt/ArmBand loot, unload, Scav, fast-access, merge, pickup, payment", StringComparison.Ordinal),
            "companion mode must retain exact Admiral payment-source ownership");
        Require(plugin.Contains("new LootPriorityPatches(Logger.LogInfo, Logger.LogWarning, true)", StringComparison.Ordinal),
            "companion mode must add money-only Admiral wallet auto-deposit");
        string lootRuntime = File.ReadAllText(Path.Combine(root, "src", "LootPriorityRuntime.cs"));
        Require(lootRuntime.Contains("if (walletOnly)", StringComparison.Ordinal)
            && lootRuntime.Contains("AddUnique(augmented, existing)", StringComparison.Ordinal),
            "companion wallet mode must preserve Pack 'n' Strap's existing container order after exact wallets");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Local Pack 'n' Strap import regression failed: " + message + ".");
    }

    private static string FindModuleRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DedicatedEquipmentSlotRegistration.cs"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate SPT-Belt-Armband-Inventory module root.");
    }
}

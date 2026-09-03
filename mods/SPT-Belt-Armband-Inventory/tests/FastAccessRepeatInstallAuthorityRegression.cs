using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class FastAccessRepeatInstallAuthorityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("FastAccess repeat-install authority regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int tryInstall = source.IndexOf("internal bool TryInstall()", StringComparison.Ordinal);
        int repeatGate = source.IndexOf("if (installed)", tryInstall, StringComparison.Ordinal);
        int repeatCall = source.IndexOf("return ValidateExistingInstallAuthority();", repeatGate, StringComparison.Ordinal);
        int installTry = source.IndexOf("try\n            {", repeatCall, StringComparison.Ordinal);
        if (tryInstall < 0 || repeatGate <= tryInstall || repeatCall <= repeatGate || installTry <= repeatCall)
            throw new InvalidOperationException("FastAccess repeat-install authority regression failed: repeat-install authority gate must execute before the first-install rollback try/catch.");

        int validator = source.IndexOf("bool ValidateExistingInstallAuthority()", installTry, StringComparison.Ordinal);
        int nextMethod = source.IndexOf("bool TryInstallReloadReachability()", validator, StringComparison.Ordinal);
        if (validator < 0 || nextMethod <= validator)
            throw new InvalidOperationException("FastAccess repeat-install authority regression failed: authority validator boundary is missing.");

        string body = source.Substring(validator, nextMethod - validator);
        Require(body, "fastAccessSlotsField.GetValue(null)", "repeat install must read the live FastAccessSlots reference");
        Require(body, "bindAvailableSlotsField.GetValue(null)", "repeat install must read the live BindAvailableSlotsExtended reference");
        Require(body, "FastAccessSlotPolicy.HasExactInstalledArrayAuthority(", "repeat install must require exact installed references for both arrays");
        Require(body, "idempotent no-op", "exact authority must explicitly remain a no-mutation success");
        Require(body, "live array authority drifted", "drift must refuse repeat installation");
        Require(body, "live array authority could not be proven", "unreadable authority must fail closed");

        if (body.Contains("SetValue(", StringComparison.Ordinal)
            || body.Contains("RestoreOwnedWrites(", StringComparison.Ordinal)
            || body.Contains("Unpatch", StringComparison.Ordinal)
            || body.Contains("ClearState(", StringComparison.Ordinal))
            throw new InvalidOperationException("FastAccess repeat-install authority regression failed: repeat-install validation must not mutate arrays, rollback Harmony, or clear ownership state.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string source = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(source)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "FastAccessSlotPatches.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("FastAccess repeat-install authority regression failed: " + message + ".");
    }
}

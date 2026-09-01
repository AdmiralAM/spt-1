using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPrimaryFallbackContractRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int append = source.IndexOf("internal static object AppendCandidates(", StringComparison.Ordinal);
        int helperCall = append < 0 ? -1 : source.IndexOf("if (!HasExactFallbackQueryContract())", append, StringComparison.Ordinal);
        int exactVanilla = helperCall < 0 ? -1 : source.IndexOf("vanillaItems.GetType() != ItemType.MakeArrayType()", helperCall, StringComparison.Ordinal);
        int invoke = exactVanilla < 0 ? -1 : source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", exactVanilla, StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactFallbackQueryContract()", StringComparison.Ordinal);
        int reset = helper < 0 ? -1 : source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);

        if (append < 0 || helperCall < 0 || exactVanilla < 0 || invoke < 0
            || !(append < helperCall && helperCall < exactVanilla && exactVanilla < invoke))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary AppendCandidates must prove exact query contract and exact vanilla Item[] shape before fallback invocation.");

        if (helper < 0 || reset < 0)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: bounded contract helper region was not found.");

        string contract = source.Substring(helper, reset - helper);
        foreach (string token in new[]
        {
            "Type exactArray = itemType.MakeArrayType();",
            "declaredReturn != exactArray || getItems.ReturnType != exactArray",
            "parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(beltArgument)",
            "if (!(beltArgument is IEnumerable values))",
            "Convert.ToInt32(value) != RuntimeIdentity.DedicatedBeltEquipmentSlotValue",
            "if (count > 1)",
            "return count == 1;",
            "catch",
            "return false;"
        })
        {
            if (!contract.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary contract proof missing token: " + token);
        }

        if (contract.Contains("AppDomain.CurrentDomain.GetAssemblies", StringComparison.Ordinal)
            || contract.Contains("ReflectionTools.FindType", StringComparison.Ordinal)
            || contract.Contains("GetMethods(", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary hot-path contract proof must remain bounded and startup-bound, not perform runtime discovery.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs")))
                return current.FullName;
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
}
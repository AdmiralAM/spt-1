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
        int firstPin = append < 0 ? -1 : source.IndexOf("if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots))", append, StringComparison.Ordinal);
        int helperCall = firstPin < 0 ? -1 : source.IndexOf("if (!HasExactFallbackQueryContract())", firstPin, StringComparison.Ordinal);
        int exactArray = helperCall < 0 ? -1 : source.IndexOf("Type exactArrayType = ItemType.MakeArrayType();", helperCall, StringComparison.Ordinal);
        int exactVanilla = exactArray < 0 ? -1 : source.IndexOf("vanillaItems.GetType() != exactArrayType", exactArray, StringComparison.Ordinal);
        int secondPin = exactVanilla < 0 ? -1 : source.IndexOf("if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots))", exactVanilla, StringComparison.Ordinal);
        int invoke = secondPin < 0 ? -1 : source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", secondPin, StringComparison.Ordinal);
        int exactFallback = invoke < 0 ? -1 : source.IndexOf("beltItems.GetType() != exactArrayType", invoke, StringComparison.Ordinal);
        int merge = exactFallback < 0 ? -1 : source.IndexOf("List<object> merged = null;", exactFallback, StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactFallbackQueryContract()", StringComparison.Ordinal);
        int reset = helper < 0 ? -1 : source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);

        if (append < 0 || firstPin < 0 || helperCall < 0 || exactArray < 0 || exactVanilla < 0 || secondPin < 0 || invoke < 0 || exactFallback < 0 || merge < 0
            || !(append < firstPin && firstPin < helperCall && helperCall < exactArray && exactArray < exactVanilla
                && exactVanilla < secondPin && secondPin < invoke && invoke < exactFallback && exactFallback < merge))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary AppendCandidates must consume the shared slot-array content pin before contract inspection, re-prove it immediately before the one slot15 query, then prove exact vanilla/returned Item[] shapes before merge.");

        int thirdPin = source.IndexOf("if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots))", secondPin + 1, StringComparison.Ordinal);
        if (thirdPin >= 0 && thirdPin < merge)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary bridge must use exactly two bounded shared-pin proofs before the single slot15 query, not accumulate redundant hot-path checks.");

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

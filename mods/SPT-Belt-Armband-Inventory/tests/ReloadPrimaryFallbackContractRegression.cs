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
        int helperCall = firstPin < 0 ? -1 : source.IndexOf("if (!HasExactFallbackQueryContract()", firstPin, StringComparison.Ordinal);
        int exactVanillaType = helperCall < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(vanillaResult)", helperCall, StringComparison.Ordinal);
        int vanillaEnumerable = exactVanillaType < 0 ? -1 : source.IndexOf("!(vanillaResult is IEnumerable vanillaSequence)", exactVanillaType, StringComparison.Ordinal);
        int secondPin = vanillaEnumerable < 0 ? -1 : source.IndexOf("if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots))", vanillaEnumerable, StringComparison.Ordinal);
        int invoke = secondPin < 0 ? -1 : source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", secondPin, StringComparison.Ordinal);
        int exactFallbackType = invoke < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(beltResult)", invoke, StringComparison.Ordinal);
        int fallbackEnumerable = exactFallbackType < 0 ? -1 : source.IndexOf("!(beltResult is IEnumerable beltItems)", exactFallbackType, StringComparison.Ordinal);
        int thirdPin = fallbackEnumerable < 0 ? -1 : source.IndexOf("!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)", fallbackEnumerable, StringComparison.Ordinal);
        int merge = thirdPin < 0 ? -1 : source.IndexOf("List<object> merged = null;", thirdPin, StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactFallbackQueryContract()", StringComparison.Ordinal);
        int reset = helper < 0 ? -1 : source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);

        if (append < 0 || firstPin < 0 || helperCall < 0 || exactVanillaType < 0 || vanillaEnumerable < 0
            || secondPin < 0 || invoke < 0 || exactFallbackType < 0 || fallbackEnumerable < 0 || thirdPin < 0 || merge < 0
            || !(append < firstPin && firstPin < helperCall && helperCall < exactVanillaType && exactVanillaType < vanillaEnumerable
                && vanillaEnumerable < secondPin && secondPin < invoke && invoke < exactFallbackType
                && exactFallbackType < fallbackEnumerable && fallbackEnumerable < thirdPin && thirdPin < merge))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary AppendCandidates must consume the shared slot-array content pin before exact generic-interface contract inspection, re-prove it immediately before the one slot15 query and after that query before enumeration, then prove exact declared IEnumerable<Item> compatibility before merge.");

        int fourthPin = source.IndexOf("HasPinnedFastAccessArrayContentForRegression(slots)", thirdPin + 1, StringComparison.Ordinal);
        if (fourthPin >= 0 && fourthPin < merge)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary bridge must use exactly three bounded shared-pin proofs around the single slot15 query before merge.");

        if (helper < 0 || reset < 0)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: bounded contract helper region was not found.");

        string contract = source.Substring(helper, reset - helper);
        foreach (string token in new[]
        {
            "Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);",
            "declaredReturn != exactReturn || getItems.ReturnType != exactReturn",
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

        foreach (string forbidden in new[]
        {
            "MakeArrayType()",
            "GetType() !=",
            "AppDomain.CurrentDomain.GetAssemblies",
            "ReflectionTools.FindType",
            "GetMethods("
        })
        {
            if (contract.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary hot-path contract proof widened or regressed to an obsolete concrete-array/runtime-discovery contract: " + forbidden);
        }
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

using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPrimaryFallbackContractRegression
{
    private const string PinToken = "ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)";
    private const string ExecutionProof = "HasExactExecutionContract(getItemsInSlots, beltSlotsArgument, itemType, magazineType, returnType, getAllParentItems, readTemplateId)";

    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int append = source.IndexOf("internal static object AppendCandidates(", StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactExecutionContract(", append, StringComparison.Ordinal);
        int reset = source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);
        if (append < 0 || helper <= append || reset <= helper)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: bounded bridge/helper region was not found.");

        string body = source.Substring(append, helper - append);
        string[] captures =
        {
            "MethodInfo getItemsInSlots = GetItemsInSlots;",
            "object beltSlotsArgument = BeltSlotsArgument;",
            "Type itemType = ItemType;",
            "Type magazineType = MagazineType;",
            "Type returnType = ReturnType;",
            "Func<object, IEnumerable> getAllParentItems = GetAllParentItems;",
            "Func<object, string> readTemplateId = ReadTemplateId;"
        };
        int previous = append;
        foreach (string token in captures)
        {
            int index = source.IndexOf(token, previous, StringComparison.Ordinal);
            if (index < 0 || index >= helper)
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: missing transaction capture: " + token);
            previous = index;
        }

        if (body.Split(PinToken, StringSplitOptions.None).Length - 1 < 4)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: caller-array content must be pinned at all four bounded stages.");
        if (body.Split(ExecutionProof, StringSplitOptions.None).Length - 1 < 4)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: complete captured execution authority must be re-proven at entry, pre-query, post-query and pre-publication.");

        foreach (string token in new[]
        {
            "HasExactFallbackQueryContract(getItemsInSlots, beltSlotsArgument, itemType, returnType)",
            "returnType.IsInstanceOfType(vanillaResult)",
            "itemType.IsInstanceOfType(item)",
            "returnType.IsInstanceOfType(beltResult)",
            "magazineType.IsInstanceOfType(item)",
            "HasExactMagazineBeltAncestor(item, getAllParentItems, readTemplateId)",
            "getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })",
            "Array.CreateInstance(itemType, merged.Count)",
            "return returnType.IsInstanceOfType(result) ? result : vanillaResult;"
        })
            Require(body, token, "bridge must execute exclusively through captured local authority");

        foreach (string forbidden in new[]
        {
            "GetItemsInSlots.Invoke(inventory",
            "ItemType.IsInstanceOfType(item)",
            "MagazineType.IsInstanceOfType(item)",
            "ReturnType.IsInstanceOfType(vanillaResult)",
            "ReturnType.IsInstanceOfType(beltResult)",
            "Array.CreateInstance(ItemType",
            "HasExactMagazineBeltAncestor(item)"
        })
            if (body.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: lazy execution re-read mutable static authority: " + forbidden);

        string helpers = source.Substring(helper, reset - helper);
        foreach (string token in new[]
        {
            "ReferenceEquals(GetItemsInSlots, getItemsInSlots)",
            "ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)",
            "ReferenceEquals(ItemType, itemType)",
            "ReferenceEquals(MagazineType, magazineType)",
            "ReferenceEquals(ReturnType, returnType)",
            "ReferenceEquals(GetAllParentItems, getAllParentItems)",
            "ReferenceEquals(ReadTemplateId, readTemplateId)",
            "static bool HasExactFallbackQueryContract(MethodInfo getItems, object beltArgument, Type itemType, Type declaredReturn)",
            "Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);",
            "declaredReturn != exactReturn || getItems.ReturnType != exactReturn",
            "parameters[0].ParameterType != exactSlotEnumerable",
            "!exactSlotEnumerable.IsInstanceOfType(beltArgument)",
            "Convert.ToInt32(value) != RuntimeIdentity.DedicatedBeltEquipmentSlotValue",
            "return count == 1;"
        })
            Require(helpers, token, "execution/query contract helper lost exact fail-closed proof");

        if (helpers.Contains("AppDomain.CurrentDomain.GetAssemblies", StringComparison.Ordinal)
            || helpers.Contains("ReflectionTools.FindType", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: hot-path proof restored runtime discovery.");
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: " + message + ": " + token);
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs"))) return current.FullName;
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

using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPrimaryFallbackContractRegression
{
    private const string PinToken = "ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)";

    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int append = source.IndexOf("internal static object AppendCandidates(", StringComparison.Ordinal);
        int capture = append < 0 ? -1 : source.IndexOf("object beltSlotsArgument = BeltSlotsArgument;", append, StringComparison.Ordinal);
        int firstPin = capture < 0 ? -1 : source.IndexOf(PinToken, capture, StringComparison.Ordinal);
        int firstReference = firstPin < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", firstPin, StringComparison.Ordinal);
        int helperCall = firstReference < 0 ? -1 : source.IndexOf("HasExactFallbackQueryContract(beltSlotsArgument)", firstReference, StringComparison.Ordinal);
        int exactVanillaType = helperCall < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(vanillaResult)", helperCall, StringComparison.Ordinal);
        int vanillaEnumerable = exactVanillaType < 0 ? -1 : source.IndexOf("!(vanillaResult is IEnumerable vanillaSequence)", exactVanillaType, StringComparison.Ordinal);
        int secondPin = firstPin < 0 ? -1 : source.IndexOf(PinToken, firstPin + PinToken.Length, StringComparison.Ordinal);
        int preInvokeReference = secondPin < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", secondPin, StringComparison.Ordinal);
        int preInvokeValue = preInvokeReference < 0 ? -1 : source.IndexOf("HasExactBeltSlotsArgument(beltSlotsArgument)", preInvokeReference, StringComparison.Ordinal);
        int invoke = preInvokeValue < 0 ? -1 : source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })", preInvokeValue, StringComparison.Ordinal);
        int exactFallbackType = invoke < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(beltResult)", invoke, StringComparison.Ordinal);
        int fallbackEnumerable = exactFallbackType < 0 ? -1 : source.IndexOf("!(beltResult is IEnumerable beltItems)", exactFallbackType, StringComparison.Ordinal);
        int thirdPin = secondPin < 0 ? -1 : source.IndexOf(PinToken, secondPin + PinToken.Length, StringComparison.Ordinal);
        int postQueryReference = thirdPin < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", thirdPin, StringComparison.Ordinal);
        int postQueryValue = postQueryReference < 0 ? -1 : source.IndexOf("HasExactBeltSlotsArgument(beltSlotsArgument)", postQueryReference, StringComparison.Ordinal);
        int merge = postQueryValue < 0 ? -1 : source.IndexOf("List<object> merged = null;", postQueryValue, StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactFallbackQueryContract(object beltArgument)", StringComparison.Ordinal);
        int reset = helper < 0 ? -1 : source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);

        if (append < 0 || capture < 0 || firstPin < 0 || firstReference < 0 || helperCall < 0 || exactVanillaType < 0 || vanillaEnumerable < 0
            || secondPin < 0 || preInvokeReference < 0 || preInvokeValue < 0 || invoke < 0
            || exactFallbackType < 0 || fallbackEnumerable < 0 || thirdPin < 0 || postQueryReference < 0 || postQueryValue < 0 || merge < 0
            || !(append < capture && capture < firstPin && firstPin < firstReference && firstReference < helperCall
                && helperCall < exactVanillaType && exactVanillaType < vanillaEnumerable
                && vanillaEnumerable < secondPin && secondPin < preInvokeReference && preInvokeReference < preInvokeValue
                && preInvokeValue < invoke && invoke < exactFallbackType && exactFallbackType < fallbackEnumerable
                && fallbackEnumerable < thirdPin && thirdPin < postQueryReference && postQueryReference < postQueryValue && postQueryValue < merge))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary AppendCandidates must capture the pseudo-slot argument, consume the shared slot-array pin, prove exact transaction-local reference/value + generic-interface contract, then re-prove both mutable inputs around the single slot15 query before merge.");

        int fourthPin = source.IndexOf(PinToken, thirdPin + PinToken.Length, StringComparison.Ordinal);
        if (fourthPin >= 0 && fourthPin < merge)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary bridge must use exactly three bounded shared-pin proofs before Belt enumeration; the fourth proof belongs post-enumeration/pre-publication.");

        if (source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", append, StringComparison.Ordinal) >= 0
            && source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", append, StringComparison.Ordinal) < helper)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: reflective fallback query re-read mutable BeltSlotsArgument instead of the transaction-local capture.");

        if (helper < 0 || reset < 0)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: bounded contract helper region was not found.");

        string contract = source.Substring(helper, reset - helper);
        foreach (string token in new[]
        {
            "Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);",
            "declaredReturn != exactReturn || getItems.ReturnType != exactReturn",
            "Type slotElementType = GetEnumerableElementType(beltArgument.GetType());",
            "Type exactSlotEnumerable = typeof(IEnumerable<>).MakeGenericType(slotElementType);",
            "parameters[0].ParameterType != exactSlotEnumerable",
            "!exactSlotEnumerable.IsInstanceOfType(beltArgument)",
            "static Type GetEnumerableElementType(Type runtimeType)",
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
            "parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(beltArgument)",
            "AppDomain.CurrentDomain.GetAssemblies",
            "ReflectionTools.FindType",
            "GetMethods("
        })
        {
            if (contract.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary hot-path contract proof widened or regressed to an obsolete concrete-array/assignability/runtime-discovery contract: " + forbidden);
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

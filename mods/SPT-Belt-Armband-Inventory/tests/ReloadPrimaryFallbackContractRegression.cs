using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPrimaryFallbackContractRegression
{
    private const string PinToken = "ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)";
    private const string MethodIdentityToken = "ReferenceEquals(GetItemsInSlots, getItemsInSlots)";

    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int append = source.IndexOf("internal static object AppendCandidates(", StringComparison.Ordinal);
        int methodCapture = append < 0 ? -1 : source.IndexOf("MethodInfo getItemsInSlots = GetItemsInSlots;", append, StringComparison.Ordinal);
        int argumentCapture = methodCapture < 0 ? -1 : source.IndexOf("object beltSlotsArgument = BeltSlotsArgument;", methodCapture, StringComparison.Ordinal);
        int firstPin = argumentCapture < 0 ? -1 : source.IndexOf(PinToken, argumentCapture, StringComparison.Ordinal);
        int firstMethodReference = firstPin < 0 ? -1 : source.IndexOf(MethodIdentityToken, firstPin, StringComparison.Ordinal);
        int firstArgumentReference = firstMethodReference < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", firstMethodReference, StringComparison.Ordinal);
        int helperCall = firstArgumentReference < 0 ? -1 : source.IndexOf("HasExactFallbackQueryContract(getItemsInSlots, beltSlotsArgument)", firstArgumentReference, StringComparison.Ordinal);
        int exactVanillaType = helperCall < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(vanillaResult)", helperCall, StringComparison.Ordinal);
        int vanillaEnumerable = exactVanillaType < 0 ? -1 : source.IndexOf("!(vanillaResult is IEnumerable vanillaSequence)", exactVanillaType, StringComparison.Ordinal);
        int secondPin = firstPin < 0 ? -1 : source.IndexOf(PinToken, firstPin + PinToken.Length, StringComparison.Ordinal);
        int preInvokeMethodReference = secondPin < 0 ? -1 : source.IndexOf(MethodIdentityToken, secondPin, StringComparison.Ordinal);
        int preInvokeArgumentReference = preInvokeMethodReference < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", preInvokeMethodReference, StringComparison.Ordinal);
        int preInvokeValue = preInvokeArgumentReference < 0 ? -1 : source.IndexOf("HasExactBeltSlotsArgument(beltSlotsArgument)", preInvokeArgumentReference, StringComparison.Ordinal);
        int invoke = preInvokeValue < 0 ? -1 : source.IndexOf("getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })", preInvokeValue, StringComparison.Ordinal);
        int exactFallbackType = invoke < 0 ? -1 : source.IndexOf("!ReturnType.IsInstanceOfType(beltResult)", invoke, StringComparison.Ordinal);
        int fallbackEnumerable = exactFallbackType < 0 ? -1 : source.IndexOf("!(beltResult is IEnumerable beltItems)", exactFallbackType, StringComparison.Ordinal);
        int thirdPin = secondPin < 0 ? -1 : source.IndexOf(PinToken, secondPin + PinToken.Length, StringComparison.Ordinal);
        int postQueryMethodReference = thirdPin < 0 ? -1 : source.IndexOf(MethodIdentityToken, thirdPin, StringComparison.Ordinal);
        int postQueryArgumentReference = postQueryMethodReference < 0 ? -1 : source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", postQueryMethodReference, StringComparison.Ordinal);
        int postQueryValue = postQueryArgumentReference < 0 ? -1 : source.IndexOf("HasExactBeltSlotsArgument(beltSlotsArgument)", postQueryArgumentReference, StringComparison.Ordinal);
        int merge = postQueryValue < 0 ? -1 : source.IndexOf("List<object> merged = null;", postQueryValue, StringComparison.Ordinal);
        int beltLoop = merge < 0 ? -1 : source.IndexOf("foreach (object item in beltItems)", merge, StringComparison.Ordinal);
        int fourthPin = beltLoop < 0 ? -1 : source.IndexOf(PinToken, beltLoop, StringComparison.Ordinal);
        int postEnumerationMethodReference = fourthPin < 0 ? -1 : source.IndexOf(MethodIdentityToken, fourthPin, StringComparison.Ordinal);
        int publication = postEnumerationMethodReference < 0 ? -1 : source.IndexOf("ShouldReuseVanillaReloadCandidates", postEnumerationMethodReference, StringComparison.Ordinal);
        int helper = source.IndexOf("static bool HasExactFallbackQueryContract(MethodInfo getItems, object beltArgument)", StringComparison.Ordinal);
        int reset = helper < 0 ? -1 : source.IndexOf("internal static void Reset()", helper, StringComparison.Ordinal);

        if (append < 0 || methodCapture < 0 || argumentCapture < 0 || firstPin < 0 || firstMethodReference < 0 || firstArgumentReference < 0
            || helperCall < 0 || exactVanillaType < 0 || vanillaEnumerable < 0 || secondPin < 0 || preInvokeMethodReference < 0
            || preInvokeArgumentReference < 0 || preInvokeValue < 0 || invoke < 0 || exactFallbackType < 0 || fallbackEnumerable < 0
            || thirdPin < 0 || postQueryMethodReference < 0 || postQueryArgumentReference < 0 || postQueryValue < 0 || merge < 0
            || beltLoop < 0 || fourthPin < 0 || postEnumerationMethodReference < 0 || publication < 0
            || !(append < methodCapture && methodCapture < argumentCapture && argumentCapture < firstPin
                && firstPin < firstMethodReference && firstMethodReference < firstArgumentReference && firstArgumentReference < helperCall
                && helperCall < exactVanillaType && exactVanillaType < vanillaEnumerable
                && vanillaEnumerable < secondPin && secondPin < preInvokeMethodReference && preInvokeMethodReference < preInvokeArgumentReference
                && preInvokeArgumentReference < preInvokeValue && preInvokeValue < invoke && invoke < exactFallbackType
                && exactFallbackType < fallbackEnumerable && fallbackEnumerable < thirdPin && thirdPin < postQueryMethodReference
                && postQueryMethodReference < postQueryArgumentReference && postQueryArgumentReference < postQueryValue
                && postQueryValue < merge && merge < beltLoop && beltLoop < fourthPin
                && fourthPin < postEnumerationMethodReference && postEnumerationMethodReference < publication))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: AppendCandidates must transaction-capture exact MethodInfo + pseudo-slot argument, consume shared slot-array pins, and re-prove both static references around the one bounded slot15 query and both lazy windows.");

        string appendBody = helper > append ? source.Substring(append, helper - append) : string.Empty;
        if (appendBody.Contains("GetItemsInSlots.Invoke(inventory", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: reflective fallback invoke re-read mutable GetItemsInSlots instead of the transaction-local MethodInfo capture.");
        if (appendBody.Split(MethodIdentityToken, StringSplitOptions.None).Length - 1 < 4)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: captured MethodInfo reference must be re-proven at contract entry, pre-query, post-query/pre-enumeration and post-enumeration/pre-publication.");
        if (appendBody.Split(PinToken, StringSplitOptions.None).Length - 1 < 4)
            throw new InvalidOperationException("Reload primary fallback-contract regression failed: retained caller slot-array content pin must survive all four bounded execution stages.");

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
            "MethodInfo getItems = GetItemsInSlots;",
            "MakeArrayType()",
            "parameters.Length != 1 || !parameters[0].ParameterType.IsInstanceOfType(beltArgument)",
            "AppDomain.CurrentDomain.GetAssemblies",
            "ReflectionTools.FindType",
            "GetMethods("
        })
        {
            if (contract.Contains(forbidden, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload primary fallback-contract regression failed: primary hot-path contract proof widened or re-read mutable discovery state: " + forbidden);
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

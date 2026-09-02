using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPseudoSlotReferencePinRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: module root could not be resolved.");

        string path = Path.Combine(root, "src", "FastAccessSlotPatches.cs");
        string source = File.ReadAllText(path);
        int append = source.IndexOf("internal static object AppendCandidates", StringComparison.Ordinal);
        int contract = source.IndexOf("static bool HasExactFallbackQueryContract(MethodInfo getItems, object beltArgument)", append, StringComparison.Ordinal);
        if (append < 0 || contract <= append)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: transaction-local fallback contract is missing.");

        int methodCapture = source.IndexOf("MethodInfo getItemsInSlots = GetItemsInSlots;", append, StringComparison.Ordinal);
        int argumentCapture = source.IndexOf("object beltSlotsArgument = BeltSlotsArgument;", methodCapture, StringComparison.Ordinal);
        int firstMethodIdentity = source.IndexOf("ReferenceEquals(GetItemsInSlots, getItemsInSlots)", argumentCapture, StringComparison.Ordinal);
        int firstArgumentIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", firstMethodIdentity, StringComparison.Ordinal);
        int vanillaLoop = source.IndexOf("foreach (object item in vanillaSequence)", firstArgumentIdentity, StringComparison.Ordinal);
        int preInvokeMethodIdentity = source.IndexOf("ReferenceEquals(GetItemsInSlots, getItemsInSlots)", vanillaLoop, StringComparison.Ordinal);
        int preInvokeArgumentIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", preInvokeMethodIdentity, StringComparison.Ordinal);
        int invoke = source.IndexOf("getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })", preInvokeArgumentIdentity, StringComparison.Ordinal);
        int postQueryMethodIdentity = source.IndexOf("ReferenceEquals(GetItemsInSlots, getItemsInSlots)", invoke, StringComparison.Ordinal);
        int postQueryArgumentIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", postQueryMethodIdentity, StringComparison.Ordinal);
        int beltLoop = source.IndexOf("foreach (object item in beltItems)", postQueryArgumentIdentity, StringComparison.Ordinal);
        int postEnumerationMethodIdentity = source.IndexOf("ReferenceEquals(GetItemsInSlots, getItemsInSlots)", beltLoop, StringComparison.Ordinal);
        int postEnumerationArgumentIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", postEnumerationMethodIdentity, StringComparison.Ordinal);
        int publication = source.IndexOf("ShouldReuseVanillaReloadCandidates", postEnumerationArgumentIdentity, StringComparison.Ordinal);

        if (methodCapture <= append || argumentCapture <= methodCapture
            || firstMethodIdentity <= argumentCapture || firstArgumentIdentity <= firstMethodIdentity || vanillaLoop <= firstArgumentIdentity
            || preInvokeMethodIdentity <= vanillaLoop || preInvokeArgumentIdentity <= preInvokeMethodIdentity || invoke <= preInvokeArgumentIdentity
            || postQueryMethodIdentity <= invoke || postQueryArgumentIdentity <= postQueryMethodIdentity || beltLoop <= postQueryArgumentIdentity
            || postEnumerationMethodIdentity <= beltLoop || postEnumerationArgumentIdentity <= postEnumerationMethodIdentity
            || publication <= postEnumerationArgumentIdentity || publication >= contract)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: captured MethodInfo/argument identity/invoke/publication ordering drifted.");

        string appendBody = source.Substring(append, contract - append);
        if (appendBody.Contains("GetItemsInSlots.Invoke(inventory", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: reflective fallback invoke reads mutable static MethodInfo instead of the transaction-local capture.");
        if (appendBody.Contains("new[] { BeltSlotsArgument }", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: reflective fallback invoke reads the mutable static pseudo-slot field instead of the transaction-local argument.");
        if (!source.Contains("static bool HasExactBeltSlotsArgument(object beltArgument)", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: exact-value proof does not consume the transaction-local argument.");
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

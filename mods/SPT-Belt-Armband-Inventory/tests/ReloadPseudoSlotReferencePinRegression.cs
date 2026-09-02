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
        int contract = source.IndexOf("static bool HasExactFallbackQueryContract(object beltArgument)", append, StringComparison.Ordinal);
        if (append < 0 || contract <= append)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: transaction-local fallback contract is missing.");

        int capture = source.IndexOf("object beltSlotsArgument = BeltSlotsArgument;", append, StringComparison.Ordinal);
        int firstIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", capture, StringComparison.Ordinal);
        int vanillaLoop = source.IndexOf("foreach (object item in vanillaSequence)", firstIdentity, StringComparison.Ordinal);
        int preInvokeIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", vanillaLoop, StringComparison.Ordinal);
        int invoke = source.IndexOf("GetItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })", preInvokeIdentity, StringComparison.Ordinal);
        int postQueryIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", invoke, StringComparison.Ordinal);
        int beltLoop = source.IndexOf("foreach (object item in beltItems)", postQueryIdentity, StringComparison.Ordinal);
        int postEnumerationIdentity = source.IndexOf("ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", beltLoop, StringComparison.Ordinal);
        int publication = source.IndexOf("ShouldReuseVanillaReloadCandidates", postEnumerationIdentity, StringComparison.Ordinal);

        if (capture <= append || firstIdentity <= capture || vanillaLoop <= firstIdentity
            || preInvokeIdentity <= vanillaLoop || invoke <= preInvokeIdentity
            || postQueryIdentity <= invoke || beltLoop <= postQueryIdentity
            || postEnumerationIdentity <= beltLoop || publication <= postEnumerationIdentity
            || publication >= contract)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: capture/identity/invoke/publication ordering drifted.");

        string appendBody = source.Substring(append, contract - append);
        if (appendBody.Contains("GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: reflective fallback invoke reads the mutable static field instead of the transaction-local argument.");
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

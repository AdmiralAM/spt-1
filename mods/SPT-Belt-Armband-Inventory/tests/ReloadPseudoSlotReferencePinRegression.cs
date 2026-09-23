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

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int append = source.IndexOf("internal static object AppendCandidates", StringComparison.Ordinal);
        int contract = source.IndexOf("static bool HasExactExecutionContract(", append, StringComparison.Ordinal);
        if (append < 0 || contract <= append)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: captured execution contract is missing.");

        string body = source.Substring(append, contract - append);
        Require(body, "MethodInfo getItemsInSlots = GetItemsInSlots;", "exact MethodInfo must be captured at bridge entry");
        Require(body, "object beltSlotsArgument = BeltSlotsArgument;", "pseudo-slot argument must be captured at bridge entry");
        Require(body, "Type itemType = ItemType;", "ItemType must be captured at bridge entry");
        Require(body, "Type magazineType = MagazineType;", "MagazineType must be captured at bridge entry");
        Require(body, "Type returnType = ReturnType;", "ReturnType must be captured at bridge entry");
        Require(body, "Func<object, IEnumerable> getAllParentItems = GetAllParentItems;", "parent delegate must be captured at bridge entry");
        Require(body, "Func<object, string> readTemplateId = ReadTemplateId;", "template-id delegate must be captured at bridge entry");

        const string proof = "HasExactExecutionContract(getItemsInSlots, beltSlotsArgument, itemType, magazineType, returnType, getAllParentItems, readTemplateId)";
        if (body.Split(proof, StringSplitOptions.None).Length - 1 < 4)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: complete execution snapshot must be re-proven at all four bounded stages.");

        int firstProof = body.IndexOf(proof, StringComparison.Ordinal);
        int vanillaLoop = body.IndexOf("foreach (object item in vanillaSequence)", firstProof, StringComparison.Ordinal);
        int secondProof = body.IndexOf(proof, vanillaLoop, StringComparison.Ordinal);
        int invoke = body.IndexOf("getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })", secondProof, StringComparison.Ordinal);
        int thirdProof = body.IndexOf(proof, invoke, StringComparison.Ordinal);
        int beltLoop = body.IndexOf("foreach (object item in beltItems)", thirdProof, StringComparison.Ordinal);
        int fourthProof = body.IndexOf(proof, beltLoop, StringComparison.Ordinal);
        int publication = body.IndexOf("ShouldReuseVanillaReloadCandidates", fourthProof, StringComparison.Ordinal);
        if (firstProof < 0 || vanillaLoop <= firstProof || secondProof <= vanillaLoop || invoke <= secondProof
            || thirdProof <= invoke || beltLoop <= thirdProof || fourthProof <= beltLoop || publication <= fourthProof)
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: proof/query/lazy-window/publication ordering drifted.");

        if (body.Contains("GetItemsInSlots.Invoke(inventory", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: reflective query re-read mutable static MethodInfo.");
        if (body.Contains("new[] { BeltSlotsArgument }", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: reflective query re-read mutable static pseudo-slot argument.");
        Require(source, "static bool HasExactBeltSlotsArgument(object beltArgument)", "exact pseudo-slot value proof must consume transaction-local argument");
        Require(source, "ReferenceEquals(GetItemsInSlots, getItemsInSlots)", "execution helper must re-prove MethodInfo identity");
        Require(source, "ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)", "execution helper must re-prove pseudo-slot identity");
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot reference-pin regression failed: " + message + ".");
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

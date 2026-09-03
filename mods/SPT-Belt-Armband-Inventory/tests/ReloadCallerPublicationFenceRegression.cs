using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadCallerPublicationFenceRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload caller publication fence regression failed: module root could not be resolved.");

        string path = Path.Combine(root, "src", "ReloadCallerPublicationFence.cs");
        string source = File.ReadAllText(path);

        Require(source, "readonly struct PublicationState",
            "per-call final publication state must remain a value type and avoid one managed allocation per GetItemsInSlots call");
        if (source.Contains("sealed class PublicationState", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: per-call publication state regressed to an allocating reference type.");
        Require(source, "CandidateFenceHarmonyId = \"com.admiralam.spt.belt-armband-inventory.reload-caller-publication.candidate\"",
            "ordered candidate postfixes must own a distinct rollback domain");
        Require(source, "CandidateBridgeHarmonyId = \"com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate\"",
            "final caller fence must order itself against the exact candidate-bridge Harmony owner");
        Require(source, "candidateOwner = harmonyCtor.Invoke(new object[] { CandidateFenceHarmonyId })",
            "candidate pair must be installed through its dedicated Harmony owner");
        Require(source, "patch.Invoke(candidateOwner, beforeArgs);",
            "vanilla-capture postfix must use the dedicated candidate owner");
        Require(source, "patch.Invoke(candidateOwner, afterArgs);",
            "final publication postfix must use the same dedicated candidate owner");
        Require(source, "bool rolledBack = TryRollback(candidateOwner, candidateRollback);",
            "partial candidate installation must roll back the dedicated owner");
        Require(source, "if (candidateOwner != null && !rolledBack)",
            "failed rollback of a possibly partial pair must become terminal rather than retrying unsafely");
        Require(source, "SetOrdering(beforePostfix, \"before\", CandidateBridgeHarmonyId)",
            "vanilla capture must execute before the candidate bridge postfix");
        Require(source, "SetOrdering(afterPostfix, \"after\", CandidateBridgeHarmonyId)",
            "publication reproof must execute after the candidate bridge postfix");
        Require(source, "states.Push(new PublicationState(__result, __0, ReloadEpochPublicationFence.CaptureForRegression()))",
            "the exact incoming vanilla result and caller slots must be captured before bridge publication");
        Require(source, "!state.Epoch.MayPublish()",
            "Reset generation/current-scope drift must invalidate final publication");
        Require(source, "!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(state.Slots)",
            "final publication must retain exact pinned caller-array content authority");
        Require(source, "__result = state.VanillaResult;",
            "final drift must restore the exact saved vanilla result object");

        // Exception safety: a foreign/intervening postfix may throw after CaptureVanilla
        // and prevent our ordinary final publication postfix from popping its frame. Prefix
        // entry depth + Harmony finalizer cleanup must restore only frames owned by this call,
        // preserving outer/reentrant frames and the exact exception object.
        Require(source, "static void CaptureEntryDepth(out int __state)",
            "candidate fence must capture ThreadStatic publication-stack entry depth in a Harmony prefix");
        Require(source, "__state = states == null ? 0 : states.Count;",
            "entry-depth snapshot must represent the exact pre-call stack depth");
        Require(source, "static Exception CleanupPublicationState(Exception __exception, int __state)",
            "candidate fence must install a Harmony finalizer for exception-safe state cleanup");
        Require(source, "while (states.Count > __state)",
            "cleanup must trim only frames created after this call's entry depth");
        Require(source, "states.Pop();",
            "cleanup must remove leaked inner/current frames rather than clearing outer/reentrant authority");
        Require(source, "return __exception;",
            "cleanup finalizer must preserve the exact incoming exception instead of suppressing or replacing it");
        Require(source, "BuildCandidateFenceArguments(",
            "entry prefix, capture postfix and cleanup finalizer must be installed as one candidate-owner patch contract");
        Require(source, "originalAssigned && prefixAssigned && postfixAssigned && finalizerAssigned",
            "candidate patch argument binding must fail closed unless Harmony exposes the full prefix/postfix/finalizer contract");
        Require(source, "if (!prefix || !postfix || !finalizer) continue;",
            "Harmony Patch discovery must reject overloads that cannot install exception-safe cleanup");

        if (source.Contains("patch.Invoke(harmonyOwner, beforeArgs)", StringComparison.Ordinal)
            || source.Contains("patch.Invoke(harmonyOwner, afterArgs)", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: candidate pair was returned to the lifecycle owner's non-atomic rollback domain.");
        if (source.Contains("AppendCandidates(", StringComparison.Ordinal)
            || source.Contains("GetItemsInSlots.Invoke", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: final fence must not query, retry, redirect, or invoke the candidate bridge itself.");
        if (source.Contains("publicationStates.Clear", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: exception cleanup may not clear outer/reentrant publication frames.");

        int candidateCreate = source.IndexOf("candidateOwner = harmonyCtor.Invoke(new object[] { CandidateFenceHarmonyId })", StringComparison.Ordinal);
        int candidateBefore = source.IndexOf("patch.Invoke(candidateOwner, beforeArgs);", candidateCreate, StringComparison.Ordinal);
        int candidateAfter = source.IndexOf("patch.Invoke(candidateOwner, afterArgs);", candidateBefore, StringComparison.Ordinal);
        int candidatePublish = source.IndexOf("candidateHarmonyOwner = candidateOwner;", candidateAfter, StringComparison.Ordinal);
        int rollback = source.IndexOf("bool rolledBack = TryRollback(candidateOwner, candidateRollback);", candidatePublish, StringComparison.Ordinal);
        if (candidateCreate < 0 || candidateBefore <= candidateCreate || candidateAfter <= candidateBefore
            || candidatePublish <= candidateAfter || rollback <= candidatePublish)
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: dedicated-owner create -> complete pair -> publish -> catch rollback sequence changed.");

        int entryMethod = source.IndexOf("static void CaptureEntryDepth(out int __state)", StringComparison.Ordinal);
        int cleanupMethod = source.IndexOf("static Exception CleanupPublicationState(Exception __exception, int __state)", StringComparison.Ordinal);
        int trim = source.IndexOf("while (states.Count > __state)", cleanupMethod, StringComparison.Ordinal);
        int preserveException = source.IndexOf("return __exception;", trim, StringComparison.Ordinal);
        if (entryMethod < 0 || cleanupMethod < 0 || trim <= cleanupMethod || preserveException <= trim)
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: entry-depth -> bounded cleanup -> exact exception preservation contract changed.");

        int captureOrder = source.IndexOf("SetOrdering(beforePostfix, \"before\", CandidateBridgeHarmonyId)", StringComparison.Ordinal);
        int finalOrder = source.IndexOf("SetOrdering(afterPostfix, \"after\", CandidateBridgeHarmonyId)", StringComparison.Ordinal);
        int vanillaCapture = source.IndexOf("states.Push(new PublicationState(__result, __0, ReloadEpochPublicationFence.CaptureForRegression()))", StringComparison.Ordinal);
        int epochProof = source.IndexOf("!state.Epoch.MayPublish()", StringComparison.Ordinal);
        int callerProof = source.IndexOf("!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(state.Slots)", epochProof, StringComparison.Ordinal);
        int fallback = source.IndexOf("__result = state.VanillaResult;", callerProof, StringComparison.Ordinal);
        if (captureOrder < 0 || finalOrder <= captureOrder || vanillaCapture < 0 || epochProof < 0
            || callerProof <= epochProof || fallback <= callerProof)
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: ordered capture -> generation/caller reproof -> exact vanilla fallback contract changed.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadCallerPublicationFence.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadCallerPublicationFence.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadCallerPublicationFence.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadCallerPublicationFence.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Reload caller publication fence regression failed: " + message + ".");
    }
}

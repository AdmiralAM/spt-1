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

        Require(source, "CandidateBridgeHarmonyId = \"com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate\"",
            "final caller fence must order itself against the exact candidate-bridge Harmony owner");
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

        if (source.Contains("AppendCandidates(", StringComparison.Ordinal)
            || source.Contains("GetItemsInSlots.Invoke", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Reload caller publication fence regression failed: final fence must not query, retry, redirect, or invoke the candidate bridge itself.");

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

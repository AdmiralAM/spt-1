using System;
using System.IO;
using System.Text.Json.Nodes;

namespace SPTBeltArmbandInventory.Tests;

internal static class HeadBandVisualAssetRegression
{
    internal static void Run()
    {
        string module = FindModuleRoot();
        string bundle = Path.Combine(module, "assets", "headband-rambo", "runtime", "bundles", "HeadBand", "headband_rambo_red.bundle");
        string entryPath = Path.Combine(module, "assets", "headband-rambo", "runtime", "bundle-entry.json");
        string icon = Path.Combine(module, "assets", "headband-rambo", "runtime", "icons", $"{RuntimeIdentity.EmergencyHeadBandItemId}.png");
        string itemSource = File.ReadAllText(Path.Combine(module, "server", "DedicatedWearableItems.cs"));
        string clientIconSource = File.ReadAllText(Path.Combine(module, "src", "HeadBandItemIconPatches.cs"));
        string clientProject = File.ReadAllText(Path.Combine(module, "src", "SPT-Belt-Armband-Inventory.csproj"));
        string deploySource = File.ReadAllText(Path.Combine(module, "tools", "Deploy-BAndHBHeadBandAsset.ps1"));

        Require(new FileInfo(bundle).Length > 100_000, "HeadBand bundle is missing or unexpectedly small");
        Require(new FileInfo(icon).Length > 2_000, "HeadBand inventory icon is missing or unexpectedly small");
        using (FileStream iconStream = File.OpenRead(icon))
        using (var iconReader = new BinaryReader(iconStream))
        {
            iconStream.Position = 16;
            int width = ReadBigEndianInt32(iconReader);
            int height = ReadBigEndianInt32(iconReader);
            Require(width == 64 && height == 64, "HeadBand client icon must stay at one-cell native size (64x64)");
        }
        using (FileStream stream = File.OpenRead(bundle))
        using (var reader = new BinaryReader(stream))
            Require(new string(reader.ReadChars(7)) == "UnityFS", "HeadBand runtime asset is not a UnityFS bundle");

        JsonObject entry = JsonNode.Parse(File.ReadAllText(entryPath))!.AsObject();
        Require(entry["key"]!.GetValue<string>() == "HeadBand/headband_rambo_red.bundle", "bundle key changed");
        Require(entry["dependencyKeys"]!.AsArray().Count == 0, "owned bundle unexpectedly gained dependencies");
        Require(itemSource.Contains("Path = \"HeadBand/headband_rambo_red.bundle\"", StringComparison.Ordinal), "server item prefab path is not wired to the owned bundle");
        Require(itemSource.Contains("/files/handbook/{RuntimeIdentity.EmergencyHeadBandItemId}", StringComparison.Ordinal), "server item icon route is not registered");
        Require(clientIconSource.Contains("ItemViewFactory", StringComparison.Ordinal)
            && clientIconSource.Contains("RuntimeIdentity.EmergencyHeadBandItemId", StringComparison.Ordinal), "client item-card icon override is not exact-template scoped");
        Require(clientProject.Contains("SPTBeltArmbandInventory.HeadBandIcon.png", StringComparison.Ordinal), "client item-card icon is not embedded in the plugin");
        Require(deploySource.Contains("SPT.Server','SPT.Launcher','EscapeFromTarkov", StringComparison.Ordinal), "asset deployment must refuse a running SPT/EFT process");
        Require(deploySource.Contains("headband-asset-$stamp", StringComparison.Ordinal), "asset deployment must preserve an out-of-tree backup");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HeadBand visual asset regression failed: " + message + ".");
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length != 4) throw new EndOfStreamException("Truncated HeadBand PNG header.");
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static string FindModuleRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DedicatedWearableItems.cs"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate SPT-Belt-Armband-Inventory module root.");
    }
}

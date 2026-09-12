using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildHeadBandBundle
{
    private const string ModelPath = "Assets/HeadBand/headband_rambo_red.fbx";
    private const string PrefabPath = "Assets/HeadBand/headband_rambo_red.prefab";
    private const string BundleName = "headband_rambo_red.bundle";

    public static void Run()
    {
        string templatePath = CommandLineValue("-template=");
        string outputPath = CommandLineValue("-output=");

        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
            ?? throw new InvalidOperationException("Unable to import " + ModelPath);

        AssetBundle templateBundle = AssetBundle.LoadFromFile(templatePath)
            ?? throw new InvalidOperationException("Unable to load template bundle " + templatePath);
        string templateAssetName = templateBundle.GetAllAssetNames()
            .Single(name => name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        GameObject template = templateBundle.LoadAsset<GameObject>(templateAssetName)
            ?? throw new InvalidOperationException("Template prefab is missing from " + templatePath);

        GameObject root = UnityEngine.Object.Instantiate(template);
        root.name = "headband_rambo_red";

        // The reference prefab contains EFT runtime MonoBehaviours that are
        // intentionally unavailable in this clean build project. The item
        // database owns behavior; this bundle owns only the inspect/loot mesh.
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);

        while (root.transform.childCount > 0)
            UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        GameObject visual = UnityEngine.Object.Instantiate(model, root.transform);
        visual.name = "HeadBand_Rambo_Red_Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        templateBundle.Unload(true);

        AssetImporter importer = AssetImporter.GetAtPath(PrefabPath);
        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        Directory.CreateDirectory(outputPath);
        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle,
            BuildTarget.StandaloneWindows64);

        string bundlePath = Path.Combine(outputPath, BundleName);
        if (!File.Exists(bundlePath))
            throw new InvalidOperationException("Unity did not produce " + bundlePath);
        Debug.Log($"HEADBAND_BUNDLE path={bundlePath} bytes={new FileInfo(bundlePath).Length}");
    }

    private static string CommandLineValue(string prefix)
    {
        string argument = Environment.GetCommandLineArgs()
            .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (argument == null || argument.Length == prefix.Length)
            throw new ArgumentException("Missing command-line argument " + prefix);
        return Path.GetFullPath(argument.Substring(prefix.Length));
    }
}

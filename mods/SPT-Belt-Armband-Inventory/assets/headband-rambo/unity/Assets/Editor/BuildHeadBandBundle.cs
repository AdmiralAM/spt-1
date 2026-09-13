using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildHeadBandBundle
{
    private const string ModelPath = "Assets/HeadBand/headband_rambo_red.fbx";
    private const string AlbedoPath = "Assets/HeadBand/headband_rambo_red_albedo.png";
    private const string NormalPath = "Assets/HeadBand/headband_rambo_red_normal.png";
    private const string MaterialPath = "Assets/HeadBand/headband_rambo_red.mat";
    private const string PrefabPath = "Assets/HeadBand/headband_rambo_red.prefab";
    private const string BundleName = "headband_rambo_red.bundle";

    public static void Run()
    {
        string templatePath = CommandLineValue("-template=");
        string outputPath = CommandLineValue("-output=");

        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AlbedoPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(NormalPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter
            ?? throw new InvalidOperationException("Unable to configure " + NormalPath);
        normalImporter.textureType = TextureImporterType.NormalMap;
        normalImporter.sRGBTexture = false;
        normalImporter.SaveAndReimport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
            ?? throw new InvalidOperationException("Unable to import " + ModelPath);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath)
            ?? throw new InvalidOperationException("Unable to import " + AlbedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath)
            ?? throw new InvalidOperationException("Unable to import " + NormalPath);

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
        visual.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        // EFT's inspect camera uses the prefab transform rather than fitting the
        // imported FBX bounds. Keep the model comfortably inside that frame.
        visual.transform.localScale = Vector3.one * 0.68f;

        AssetDatabase.DeleteAsset(MaterialPath);
        Shader shader = Shader.Find("Standard")
            ?? throw new InvalidOperationException("Standard shader is unavailable");
        Material material = new Material(shader) { name = "HeadBand_Rambo_Red" };
        material.SetTexture("_MainTex", albedo);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.16f);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 0.35f);
        material.EnableKeyword("_NORMALMAP");
        // EFT's item-inspect scene is deliberately dim. A restrained fabric-
        // colored emission keeps the approved crimson readable without making
        // the cloth look luminous in normal inventory lighting.
        material.SetTexture("_EmissionMap", albedo);
        material.SetColor("_EmissionColor", new Color(0.28f, 0.018f, 0.022f, 1f));
        material.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(material, MaterialPath);
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

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

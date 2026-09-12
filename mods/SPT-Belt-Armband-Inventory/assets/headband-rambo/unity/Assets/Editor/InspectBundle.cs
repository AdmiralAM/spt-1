using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InspectBundle
{
    public static void Run()
    {
        string arg = Environment.GetCommandLineArgs().First(value => value.StartsWith("-bundle=", StringComparison.Ordinal));
        string path = arg.Substring("-bundle=".Length);
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null) throw new InvalidOperationException("Unable to load bundle: " + path);
        foreach (string name in bundle.GetAllAssetNames())
        {
            UnityEngine.Object asset = bundle.LoadAsset(name);
            Debug.Log($"BUNDLE_ASSET name={name} type={asset?.GetType().FullName}");
            if (asset is GameObject go)
                Debug.Log("BUNDLE_COMPONENTS " + string.Join(",", go.GetComponentsInChildren<Component>(true).Select(c => c == null ? "MISSING" : c.GetType().FullName)));
        }
        bundle.Unload(true);
    }
}

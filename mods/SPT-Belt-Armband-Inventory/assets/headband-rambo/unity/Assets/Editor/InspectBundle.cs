using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InspectBundle
{
    public static void Run()
    {
        string arg = Environment.GetCommandLineArgs().First(value => value.StartsWith("-bundle=", StringComparison.Ordinal));
        string path = arg.Substring("-bundle=".Length);
        string outputArg = Environment.GetCommandLineArgs().FirstOrDefault(value => value.StartsWith("-output=", StringComparison.Ordinal));
        string output = outputArg == null ? null : outputArg.Substring("-output=".Length);
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null) throw new InvalidOperationException("Unable to load bundle: " + path);
        foreach (string name in bundle.GetAllAssetNames())
        {
            UnityEngine.Object asset = bundle.LoadAsset(name);
            Debug.Log($"BUNDLE_ASSET name={name} type={asset?.GetType().FullName}");
            if (asset is GameObject go)
            {
                Debug.Log("BUNDLE_COMPONENTS " + string.Join(",", go.GetComponentsInChildren<Component>(true).Select(c => c == null ? "MISSING" : c.GetType().FullName)));
                MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                int vertices = filters.Sum(filter => filter.sharedMesh == null ? 0 : filter.sharedMesh.vertexCount);
                long indices = filters.Sum(filter => filter.sharedMesh == null
                    ? 0L
                    : Enumerable.Range(0, filter.sharedMesh.subMeshCount).Sum(subMesh => (long)filter.sharedMesh.GetIndexCount(subMesh)));
                int missing = go.GetComponentsInChildren<Component>(true).Count(component => component == null);
                Debug.Log($"BUNDLE_GEOMETRY meshes={filters.Length} vertices={vertices} triangles={indices / 3} renderers={renderers.Length} missing={missing}");
                foreach (Renderer renderer in renderers)
                    foreach (Material material in renderer.sharedMaterials)
                        Debug.Log($"BUNDLE_MATERIAL name={material?.name} shader={material?.shader?.name} mainTexture={material?.mainTexture?.name} normalTexture={material?.GetTexture("_BumpMap")?.name} emissionTexture={material?.GetTexture("_EmissionMap")?.name}");
                if (output != null) RenderViews(go, output);
            }
        }
        bundle.Unload(true);
    }

    private static void RenderViews(GameObject prefab, string output)
    {
        Directory.CreateDirectory(output);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Bundle prefab has no renderer");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

        GameObject cameraObject = new GameObject("BundlePreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.018f, 0.021f, 1f);
        float distance = Mathf.Max(1f, bounds.extents.magnitude * 4f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = distance * 3f;

        GameObject lightObject = new GameObject("BundlePreviewLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        RenderTexture target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture = target;
        foreach (var view in new[] {
            new { Name = "front", Direction = new Vector3(0f, -1f, 0.24f) },
            new { Name = "side", Direction = new Vector3(1f, 0f, 0.18f) },
            new { Name = "back", Direction = new Vector3(0f, 1f, 0.24f) },
        })
        {
            Vector3 direction = view.Direction.normalized;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center, Vector3.up);
            camera.Render();
            RenderTexture.active = target;
            Texture2D image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, $"bundle_{view.Name}.png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(lightObject);
        UnityEngine.Object.DestroyImmediate(instance);
    }
}

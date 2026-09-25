using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AdmiralCompatibilitySuite;

internal sealed class TextureReadbackAdapter : IDisposable
{
    private const string HarmonyId = "com.admiralam.compatibility-suite.texture-readback";
    private readonly Action<string> warning;
    private Harmony harmony;

    internal TextureReadbackAdapter(Action<string> warning) => this.warning = warning;

    internal bool TryInstall()
    {
        try
        {
            MethodInfo target = AccessTools.Method(typeof(ImageConversion), nameof(ImageConversion.EncodeToPNG), new[] { typeof(Texture2D) });
            if (target == null) return false;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(TextureReadbackAdapter), nameof(Prefix)));
            return true;
        }
        catch (Exception exception)
        {
            warning?.Invoke("Compatibility Suite could not install unreadable texture readback: " + exception.Message);
            return false;
        }
    }

    private static void Prefix(ref Texture2D tex, out Texture2D __state)
    {
        __state = null;
        if (tex == null || tex.isReadable) return;

        RenderTexture temporary = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(tex, temporary);
            RenderTexture.active = temporary;
            var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply(false, false);
            __state = readable;
            tex = readable;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static void Postfix(Texture2D __state)
    {
        if (__state != null) UnityEngine.Object.Destroy(__state);
    }

    public void Dispose()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }
}

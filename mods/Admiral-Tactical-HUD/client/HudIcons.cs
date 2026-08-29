using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace SPTPopCounter
{
    internal sealed class HudIcons
    {
        const int Cell = 64;
        const string BotCensusPrefix = "AdmiralTacticalHUD.BotCensus.";

        readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> botCensus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"usec", "pmc.png"},
            {"bear", "pmc.png"},
            {"scav", "scav.png"},
            {"boss", "boss.png"},
            {"raider", "raider.png"},
            {"rogue", "rogue.png"}
        };
        readonly Dictionary<string, Vector2Int> cells = new Dictionary<string, Vector2Int>(StringComparer.OrdinalIgnoreCase)
        {
            {"usec",new Vector2Int(0,0)},{"bear",new Vector2Int(1,0)},{"scav",new Vector2Int(2,0)},{"boss",new Vector2Int(3,0)},
            {"raider",new Vector2Int(4,0)},{"water",new Vector2Int(5,0)},{"energy",new Vector2Int(6,0)},{"weight",new Vector2Int(7,0)},
            {"weight1",new Vector2Int(0,1)},{"weight2",new Vector2Int(1,1)},{"weight3",new Vector2Int(2,1)},{"head",new Vector2Int(3,1)},
            {"torso",new Vector2Int(4,1)},{"stomach",new Vector2Int(5,1)},{"left_arm",new Vector2Int(6,1)},{"right_arm",new Vector2Int(7,1)},
            {"left_leg",new Vector2Int(0,2)},{"right_leg",new Vector2Int(1,2)},{"self",new Vector2Int(2,2)}
        };
        Texture2D sheet;

        public HudIcons() { LoadSheet(); }

        void LoadSheet()
        {
            try
            {
                string path = Path.Combine(Paths.PluginPath,"Admiral Tactical HUD","assets","hud-sprites.png");
                if (!File.Exists(path))
                    path = Path.Combine(Paths.PluginPath,"assets","hud-sprites.png");
                if (!File.Exists(path)) return;

                byte[] bytes = File.ReadAllBytes(path);
                sheet = new Texture2D(2,2,TextureFormat.RGBA32,false);
                sheet.name = "Admiral Tactical HUD Reserve Sprite Sheet";
                sheet.filterMode = FilterMode.Bilinear;
                sheet.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(sheet,bytes,false)) sheet = null;
            }
            catch { sheet = null; }
        }

        public Texture2D Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key,out Texture2D cached)) return cached;

            Texture2D preferred = LoadBotCensus(key);
            if (preferred != null)
            {
                cache[key] = preferred;
                return preferred;
            }

            Texture2D reserve = LoadReserve(key);
            cache[key] = reserve;
            return reserve;
        }

        Texture2D LoadBotCensus(string key)
        {
            if (!botCensus.TryGetValue(key,out string file)) return null;
            try
            {
                Assembly assembly = typeof(HudIcons).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(BotCensusPrefix + file))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }
                    if (offset != bytes.Length) return null;

                    Texture2D texture = new Texture2D(2,2,TextureFormat.RGBA32,true);
                    texture.name = "Admiral HUD BotCensus " + key;
                    if (!ImageConversion.LoadImage(texture,bytes,false))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }
                    texture.filterMode = FilterMode.Trilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.anisoLevel = 1;
                    return texture;
                }
            }
            catch { return null; }
        }

        Texture2D LoadReserve(string key)
        {
            if (sheet == null || !cells.TryGetValue(key,out Vector2Int c)) return null;
            int x = c.x * Cell;
            int yTop = c.y * Cell;
            int y = sheet.height - yTop - Cell;
            try
            {
                Color[] px = sheet.GetPixels(x,y,Cell,Cell);
                Texture2D texture = new Texture2D(Cell,Cell,TextureFormat.RGBA32,false);
                texture.name = "Admiral HUD reserve " + key;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.anisoLevel = 1;
                texture.SetPixels(px,0);
                texture.Apply(false,true);
                return texture;
            }
            catch { return null; }
        }
    }
}

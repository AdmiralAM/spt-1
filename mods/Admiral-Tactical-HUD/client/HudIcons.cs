using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace SPTPopCounter
{
    internal sealed class HudIcons
    {
        const int Cell = 64;
        readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
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
                sheet.name = "Admiral Tactical HUD Sprite Sheet";
                sheet.filterMode = FilterMode.Bilinear;
                sheet.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(sheet,bytes,false)) sheet = null;
            }
            catch { sheet = null; }
        }

        public Texture2D Get(string key)
        {
            if (string.IsNullOrEmpty(key) || sheet == null) return null;
            if (cache.TryGetValue(key,out Texture2D t)) return t;
            if (!cells.TryGetValue(key,out Vector2Int c)) return null;

            int x = c.x * Cell;
            int yTop = c.y * Cell;
            int y = sheet.height - yTop - Cell;
            try
            {
                Color[] px = sheet.GetPixels(x,y,Cell,Cell);
                t = new Texture2D(Cell,Cell,TextureFormat.RGBA32,false);
                t.name = "Admiral HUD " + key;
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                t.anisoLevel = 1;
                t.SetPixels(px,0);
                t.Apply(false,true);
                cache[key] = t;
                return t;
            }
            catch { return null; }
        }
    }
}

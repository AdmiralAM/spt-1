using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            {"left_leg",new Vector2Int(0,2)},{"right_leg",new Vector2Int(1,2)},{"self",new Vector2Int(2,2)},{"weapon_unknown",new Vector2Int(3,2)},
            {"weapon_assault",new Vector2Int(4,2)},{"weapon_carbine",new Vector2Int(5,2)},{"weapon_smg",new Vector2Int(6,2)},{"weapon_lmg",new Vector2Int(7,2)},
            {"weapon_sniper",new Vector2Int(0,3)},{"weapon_dmr",new Vector2Int(1,3)},{"weapon_shotgun_pump",new Vector2Int(2,3)},{"weapon_shotgun_semi",new Vector2Int(3,3)},
            {"weapon_shotgun_sawedoff",new Vector2Int(4,3)},{"weapon_pistol",new Vector2Int(5,3)},{"weapon_revolver",new Vector2Int(6,3)},{"weapon_launcher",new Vector2Int(7,3)},
            {"weapon_frag",new Vector2Int(0,4)},{"weapon_impact",new Vector2Int(1,4)},{"weapon_incendiary",new Vector2Int(2,4)},{"weapon_melee",new Vector2Int(3,4)},
            {"weapon_throwing",new Vector2Int(4,4)},{"weapon_bolt",new Vector2Int(5,4)},{"weapon_pcc",new Vector2Int(6,4)},{"weapon_special",new Vector2Int(7,4)},
            {"weapon_crossbow",new Vector2Int(0,5)},{"weapon_tool",new Vector2Int(1,5)},{"weapon_explosive",new Vector2Int(2,5)}
        };
        Texture2D sheet;

        public HudIcons() { LoadSheet(); }

        void LoadSheet()
        {
            try
            {
                Version assemblyVersion = typeof(HudIcons).Assembly.GetName().Version;
                string semanticVersion = assemblyVersion == null
                    ? string.Empty
                    : assemblyVersion.Major + "." + assemblyVersion.Minor + "." + assemblyVersion.Build;

                string[] candidates =
                {
                    Path.Combine(Paths.PluginPath,"SPT Tactical HUD","assets","hud-sprites.png"),
                    string.IsNullOrEmpty(semanticVersion) ? string.Empty : Path.Combine(Paths.PluginPath,"SPT Tactical HUD v" + semanticVersion,"assets","hud-sprites.png"),
                    Path.Combine(Paths.PluginPath,"assets","hud-sprites.png")
                };
                string path = candidates.Where(p => !string.IsNullOrEmpty(p)).FirstOrDefault(File.Exists);
                if (path == null)
                {
                    // Compatibility fallback for older unpacked packages. Prefer the newest versioned folder
                    // deterministically instead of whichever recursive directory enumeration returns first.
                    path = Directory.GetDirectories(Paths.PluginPath,"SPT Tactical HUD v*",SearchOption.TopDirectoryOnly)
                        .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
                        .Select(p => Path.Combine(p,"assets","hud-sprites.png"))
                        .FirstOrDefault(File.Exists);
                }
                if (path == null)
                    path = Directory.GetFiles(Paths.PluginPath,"hud-sprites.png",SearchOption.AllDirectories).FirstOrDefault();
                if (path == null) return;
                byte[] bytes = File.ReadAllBytes(path);
                sheet = new Texture2D(2,2,TextureFormat.RGBA32,false);
                sheet.name = "SPT Tactical HUD Sprite Sheet";
                sheet.filterMode = FilterMode.Bilinear;
                sheet.wrapMode = TextureWrapMode.Clamp;
                if (!ImageConversion.LoadImage(sheet,bytes,false)) sheet = null;
            }
            catch { sheet = null; }
        }

        public Texture2D Get(string key)
        {
            if (string.IsNullOrEmpty(key) || sheet == null) return null;
            Texture2D t;
            if (cache.TryGetValue(key,out t)) return t;
            Vector2Int c;
            if (!cells.TryGetValue(key,out c)) c = cells["weapon_unknown"];
            int x = c.x * Cell;
            int yTop = c.y * Cell;
            int y = sheet.height - yTop - Cell;
            try
            {
                Color[] px = sheet.GetPixels(x,y,Cell,Cell);

                // Crop every atlas cell into an isolated, non-mipmapped UI texture. Bilinear sampling keeps
                // edges stable while avoiding the detail washout seen in 12–20 px trilinear mip levels.
                t = new Texture2D(Cell,Cell,TextureFormat.RGBA32,false);
                t.name = "HUD " + key;
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

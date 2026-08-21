using System;
using System.Collections.Generic;
using UnityEngine;

namespace SPTPopCounter
{
    internal sealed class HudIcons
    {
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public Texture2D Get(string key)
        {
            Texture2D t;
            if (_cache.TryGetValue(key, out t)) return t;
            t = Build(key);
            _cache[key] = t;
            return t;
        }

        private Texture2D Build(string key)
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            Clear(t);
            switch ((key ?? "").ToLowerInvariant())
            {
                case "usec": Usec(t); break;
                case "bear": Bear(t); break;
                case "scav": Balaclava(t); break;
                case "boss": Crown(t); break;
                case "raider": Helmet(t); break;
                case "water": Drop(t); break;
                case "energy": Bolt(t); break;
                case "weight": Weight(t); break;
                case "weight1": Chevrons(t, 1); break;
                case "weight2": Chevrons(t, 2); break;
                case "weight3": Chevrons(t, 3); break;
                case "head": Headshot(t); break;
                case "torso": Torso(t); break;
                case "arm": Arm(t); break;
                case "leg": Leg(t); break;
                case "stomach": Stomach(t); break;
                case "ak": WeaponAk(t); break;
                case "ar": WeaponAr(t); break;
                case "smg": WeaponSmg(t); break;
                case "shotgun": WeaponShotgun(t); break;
                case "sniper": WeaponSniper(t); break;
                case "pistol": WeaponPistol(t); break;
                default: WeaponGeneric(t); break;
            }
            t.Apply(false, true);
            return t;
        }

        private static void Clear(Texture2D t)
        {
            var c = new Color32(255, 255, 255, 0);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) t.SetPixel(x, y, c);
        }
        private static void P(Texture2D t, int x, int y, int r = 255, int g = 255, int b = 255, int a = 255)
        {
            if (x >= 0 && y >= 0 && x < 32 && y < 32) t.SetPixel(x, y, new Color32((byte)r, (byte)g, (byte)b, (byte)a));
        }
        private static void Rect(Texture2D t, int x, int y, int w, int h)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) P(t, xx, yy);
        }
        private static void Line(Texture2D t, int x0, int y0, int x1, int y1, int thickness = 1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                for (int oy = -thickness / 2; oy <= thickness / 2; oy++) for (int ox = -thickness / 2; ox <= thickness / 2; ox++) P(t, x0 + ox, y0 + oy);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
        private static void Circle(Texture2D t, int cx, int cy, int r, bool fill = true)
        {
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
            {
                int d = x * x + y * y;
                if ((fill && d <= r * r) || (!fill && d <= r * r && d >= (r - 2) * (r - 2))) P(t, cx + x, cy + y);
            }
        }

        private static void Usec(Texture2D t)
        {
            Line(t, 7, 8, 16, 4, 2); Line(t, 16, 4, 25, 8, 2); Line(t, 7, 8, 9, 22, 2); Line(t, 25, 8, 23, 22, 2); Line(t, 9, 22, 16, 28, 2); Line(t, 23, 22, 16, 28, 2);
            Line(t, 10, 13, 16, 10, 2); Line(t, 22, 13, 16, 10, 2); Line(t, 16, 10, 16, 22, 2); Line(t, 12, 17, 20, 17, 2);
        }
        private static void Bear(Texture2D t)
        {
            Circle(t, 10, 21, 4); Circle(t, 16, 24, 5); Circle(t, 22, 21, 4); Circle(t, 11, 12, 3); Circle(t, 16, 10, 3); Circle(t, 21, 12, 3);
        }
        private static void Balaclava(Texture2D t)
        {
            Circle(t, 16, 17, 11); Rect(t, 7, 4, 18, 13); Rect(t, 10, 15, 5, 3); Rect(t, 18, 15, 5, 3); Rect(t, 13, 22, 7, 2);
            for (int y = 14; y < 19; y++) for (int x = 9; x < 24; x++) if ((x < 15 || x > 17) && y < 18) t.SetPixel(x, y, new Color32(255, 255, 255, 0));
        }
        private static void Crown(Texture2D t)
        {
            Line(t, 5, 10, 8, 23, 3); Line(t, 27, 10, 24, 23, 3); Line(t, 8, 23, 24, 23, 3); Line(t, 5, 10, 11, 16, 3); Line(t, 11, 16, 16, 8, 3); Line(t, 16, 8, 21, 16, 3); Line(t, 21, 16, 27, 10, 3); Rect(t, 8, 24, 16, 3);
        }
        private static void Helmet(Texture2D t)
        {
            Circle(t, 16, 15, 10); Rect(t, 6, 14, 20, 5); Rect(t, 9, 18, 14, 7); Rect(t, 10, 13, 12, 3); Line(t, 8, 9, 24, 9, 2); Rect(t, 11, 20, 4, 2); Rect(t, 18, 20, 4, 2);
        }
        private static void Drop(Texture2D t)
        {
            for (int y = 5; y <= 27; y++) { int half = y < 17 ? (y - 5) / 2 : Math.Max(1, 10 - (y - 17) / 2); for (int x = 16 - half; x <= 16 + half; x++) P(t, x, y); }
        }
        private static void Bolt(Texture2D t)
        {
            Line(t, 20, 4, 10, 17, 4); Line(t, 10, 17, 17, 17, 4); Line(t, 17, 17, 11, 28, 4); Line(t, 11, 28, 24, 13, 4); Line(t, 24, 13, 17, 13, 4); Line(t, 17, 13, 20, 4, 4);
        }
        private static void Weight(Texture2D t)
        {
            Circle(t, 16, 10, 4, false); Rect(t, 7, 12, 18, 14); Line(t, 7, 26, 25, 26, 2);
        }
        private static void Chevrons(Texture2D t, int count)
        {
            for (int i = 0; i < count; i++) { int y = 8 + i * 7; Line(t, 8, y + 6, 16, y, 3); Line(t, 16, y, 24, y + 6, 3); }
        }
        private static void Headshot(Texture2D t)
        {
            Circle(t, 15, 17, 9); Rect(t, 9, 20, 12, 7); Circle(t, 12, 17, 2); Circle(t, 18, 17, 2); Rect(t, 14, 22, 3, 3); Line(t, 2, 24, 29, 7, 2); Circle(t, 25, 10, 2); Line(t, 27, 7, 30, 4, 1); Line(t, 27, 10, 31, 10, 1); Line(t, 26, 12, 30, 15, 1);
        }
        private static void Torso(Texture2D t)
        {
            Circle(t, 16, 24, 6); Rect(t, 12, 18, 8, 5); Line(t, 12, 18, 6, 14, 4); Line(t, 20, 18, 26, 14, 4); Line(t, 6, 14, 8, 5, 4); Line(t, 26, 14, 24, 5, 4); Rect(t, 10, 9, 12, 12);
        }
        private static void Arm(Texture2D t)
        {
            Circle(t, 11, 19, 7); Circle(t, 19, 18, 5); Line(t, 19, 18, 25, 25, 5); Line(t, 25, 25, 29, 21, 4); Line(t, 9, 12, 13, 6, 4); Line(t, 13, 6, 18, 9, 4);
        }
        private static void Leg(Texture2D t)
        {
            Line(t, 13, 5, 18, 17, 6); Line(t, 18, 17, 14, 28, 6); Line(t, 14, 28, 22, 28, 4); Circle(t, 14, 6, 4);
        }
        private static void Stomach(Texture2D t)
        {
            Circle(t, 16, 16, 9); Circle(t, 16, 16, 4, false); Line(t, 13, 9, 10, 5, 3); Line(t, 19, 23, 23, 27, 3);
        }
        private static void WeaponAk(Texture2D t)
        {
            Rect(t, 5, 15, 20, 4); Rect(t, 11, 11, 9, 4); Line(t, 16, 19, 19, 28, 3); Line(t, 19, 28, 24, 26, 3); Line(t, 6, 19, 2, 24, 3); Rect(t, 24, 16, 6, 2);
        }
        private static void WeaponAr(Texture2D t)
        {
            Rect(t, 5, 15, 21, 4); Rect(t, 12, 11, 9, 4); Line(t, 15, 19, 15, 27, 3); Line(t, 15, 27, 20, 27, 3); Line(t, 6, 19, 2, 22, 3); Rect(t, 26, 16, 5, 2);
        }
        private static void WeaponSmg(Texture2D t)
        {
            Rect(t, 6, 15, 18, 5); Rect(t, 11, 11, 7, 4); Rect(t, 15, 20, 4, 8); Rect(t, 24, 16, 6, 2);
        }
        private static void WeaponShotgun(Texture2D t)
        {
            Rect(t, 3, 15, 26, 3); Rect(t, 8, 18, 12, 3); Line(t, 7, 18, 3, 23, 3); Rect(t, 24, 13, 6, 2);
        }
        private static void WeaponSniper(Texture2D t)
        {
            Rect(t, 2, 15, 27, 3); Rect(t, 10, 11, 10, 3); Circle(t, 15, 12, 3, false); Line(t, 9, 18, 5, 24, 3); Rect(t, 27, 14, 4, 2);
        }
        private static void WeaponPistol(Texture2D t)
        {
            Rect(t, 8, 12, 17, 5); Rect(t, 18, 17, 5, 10); Line(t, 18, 27, 24, 27, 3);
        }
        private static void WeaponGeneric(Texture2D t)
        {
            Rect(t, 4, 15, 23, 4); Rect(t, 10, 11, 8, 4); Line(t, 15, 19, 18, 27, 3);
        }
    }
}

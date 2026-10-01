using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 포획 스테이지 배경 276×108 도트. 아래 34줄이 땅(포획대원·괴물이 서는 곳), 위가 하늘/원경.
    public static class StagePainter
    {
        const int W = GameData.StageW, H = GameData.StageH, G = GameData.StageGroundY;
        static Color32 C(string h) => Canvas.C(h);
        static readonly Color32 K = Canvas.K;

        // 4×4 베이어 디더
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // 세로 그라데이션을 steps 단계 + 디더로
        static void Grad(Canvas c, int y0, int y1, string bottom, string top, int steps = 5)
        {
            var a = C(bottom); var b = C(top);
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < W; x++)
                {
                    float t = (y - y0) / (float)Mathf.Max(1, y1 - y0 - 1) * (steps - 1);
                    int s = Mathf.FloorToInt(t);
                    float frac = t - s;
                    if (frac * 16f > Bayer[(y % 4) * 4 + x % 4]) s++;
                    c.Set(x, y, Color32.Lerp(a, b, Mathf.Clamp01(s / (float)(steps - 1))));
                }
        }

        // 능선: 두 사인파를 섞은 실루엣
        static void Ridge(Canvas c, int baseY, float amp, float period, float seed, Color32 col, bool jag = false)
        {
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.Sin(x / period + seed) * 0.6f + Mathf.Sin(x / (period * 0.37f) + seed * 2.3f) * 0.4f;
                if (jag) n = Mathf.Abs(Mathf.Sin(x / period + seed)) * 1.2f - 0.3f + Mathf.Sin(x / 3.1f + seed) * 0.08f;
                int h = baseY + Mathf.RoundToInt(n * amp);
                for (int y = G; y <= h; y++) c.Set(x, y, col);
            }
        }

        static void Tri(Canvas c, int cx, int baseY, int halfW, int h, Color32 col)
        {
            for (int j = 0; j < h; j++)
            {
                int w = Mathf.RoundToInt(halfW * (1f - j / (float)h));
                for (int i = -w; i <= w; i++) c.Set(cx + i, baseY + j, col);
            }
        }

        static void Pines(Canvas c, int count, int baseY, int size, Color32 col, int seed, Color32? snow = null)
        {
            var rng = new System.Random(seed);
            for (int k = 0; k < count; k++)
            {
                int x = rng.Next(0, W);
                int h = size + rng.Next(-size / 3, size / 3 + 1);
                c.Rect(x, baseY, 1, 3, col);
                for (int tier = 0; tier < 3; tier++) Tri(c, x, baseY + 2 + tier * h / 4, h / 3 - tier, h / 2, col);
                if (snow != null) { c.Set(x, baseY + h - 1, snow.Value); c.Set(x - 1, baseY + h / 2, snow.Value); c.Set(x + 1, baseY + h / 2 + 1, snow.Value); }
            }
        }

        static void DeadTree(Canvas c, int x, int baseY, int h, Color32 col)
        {
            c.Rect(x, baseY, 2, h, col);
            c.Rect(x - 4, baseY + h * 2 / 3, 4, 1, col); c.Set(x - 4, baseY + h * 2 / 3 + 1, col);
            c.Rect(x + 2, baseY + h / 2, 5, 1, col); c.Set(x + 6, baseY + h / 2 + 1, col); c.Set(x + 6, baseY + h / 2 + 2, col);
            c.Rect(x - 1, baseY + h, 1, 3, col); c.Rect(x + 2, baseY + h - 1, 1, 4, col);
        }

        static void Stars(Canvas c, int n, int seed, Color32 col, int minY = 60)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++) c.Set(rng.Next(W), rng.Next(minY, H), col);
        }

        static void Moon(Canvas c, int x, int y, int r, Color32 col)
        {
            c.Ellipse(x, y, r, r, col, true);
            c.Ellipse(x + r / 3, y + r / 4, r / 3, r / 3, Canvas.Shade(col, 0.88f), true);
            c.Ellipse(x - r / 3, y - r / 3, r / 4, r / 4, Canvas.Shade(col, 0.9f), true);
        }

        // 땅: 윗면 두 줄 + 흙 + 잡티 + 자갈
        static void Ground(Canvas c, string top, string body, string dark, int seed)
        {
            var b = C(body);
            c.Rect(0, 0, W, G, b);
            c.HLine(0, G - 1, W, C(top));
            c.HLine(0, G - 2, W, Canvas.Shade(C(top), 0.85f));
            var rng = new System.Random(seed);
            for (int x = 0; x < W; x += rng.Next(2, 5)) c.Set(x, G - 3, Canvas.Shade(C(top), 0.75f)); // 윗면 늘어짐
            c.Noise(0, 0, W, G - 3, 0.06f);
            for (int i = 0; i < 40; i++)
            {
                int x = rng.Next(W), y = rng.Next(1, G - 5);
                c.Set(x, y, C(dark)); c.Set(x + 1, y, C(dark));
                c.Set(x, y + 1, Canvas.Shade(b, 1.15f));
            }
            // 아래로 갈수록 어둡게(깊이감)
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < W; x++)
                    if ((x + y) % 2 == 0 || y < 4) c.Set(x, y, new Color32(10, 5, 15, (byte)(90 - y * 10)));
        }

        static void Fog(Canvas c, int y, int h, Color32 col)
        {
            for (int j = 0; j < h; j++)
                for (int x = 0; x < W; x++)
                {
                    float a = Mathf.Sin(x / 17f + j) * 0.5f + 0.5f;
                    if (a * 16 > Bayer[(j % 4) * 4 + x % 4]) c.Set(x, y + j, col);
                }
        }

        static void Crystal(Canvas c, int x, int baseY, int h, Color32 col)
        {
            for (int j = 0; j < h; j++)
            {
                int w = j < h - 3 ? 2 : Mathf.Max(0, h - j - 1);
                for (int i = -w; i <= w; i++) c.Set(x + i, baseY + j, i < 0 ? Canvas.Shade(col, 1.3f) : col);
            }
            c.Set(x - 1, baseY + h / 2, new Color32(255, 255, 255, 255));
        }

        // ── 스테이지별 ─────────────────────────────────────────
        static Canvas Forest()
        {
            var c = new Canvas(W, H, 11);
            Grad(c, G, H, "#5a3a6a", "#1a1030");
            Stars(c, 30, 1, C("#c8b8e0"));
            Moon(c, 220, 88, 9, C("#f0e0c0"));
            Ridge(c, 58, 6, 23, 1, C("#2e2040"));
            Pines(c, 22, G + 6, 22, C("#1e2a24"), 3);
            Pines(c, 14, G, 18, C("#142018"), 5);
            Fog(c, G + 2, 8, new Color32(120, 110, 150, 70));
            Ground(c, "#5a9a3a", "#3a2a22", "#2a1c16", 7);
            var rng = new System.Random(9);
            for (int i = 0; i < 8; i++) // 풀숲, 버섯
            {
                int x = rng.Next(W);
                c.Ellipse(x, G, 4, 2, C("#2e5a24"), true);
                if (i % 3 == 0) { c.Rect(x + 3, G, 1, 2, C("#f0e0c8")); c.Rect(x + 2, G + 2, 3, 1, C("#e04848")); }
            }
            return c;
        }

        static Canvas Lava()
        {
            var c = new Canvas(W, H, 12);
            Grad(c, G, H, "#7a2410", "#1a0606");
            Ridge(c, 62, 8, 17, 2, C("#3a1410"), true);
            // 종유석
            var rng = new System.Random(4);
            for (int i = 0; i < 18; i++) { int x = rng.Next(W); int h = rng.Next(6, 18); for (int j = 0; j < h; j++) c.Rect(x - (h - j) / 5, H - 1 - j, (h - j) / 5 * 2 + 1, 1, C("#2a0e0a")); }
            Ridge(c, G + 16, 6, 13, 5, C("#4a1a12"), true);
            // 용암 강 (앞쪽 바위 위에 흐른다)
            c.Rect(0, G, W, 7, C("#ff6020"));
            for (int x = 0; x < W; x++) { if (x % 7 < 3) c.Set(x, G + 6, C("#ffd040")); if (x % 11 == 0) c.Set(x, G + 3, C("#ffe080")); c.Set(x, G + 7, C("#c83a10")); }
            Ground(c, "#6a3a30", "#3a2424", "#241414", 8);
            for (int i = 0; i < 12; i++) // 균열
            {
                int x = rng.Next(W), y = rng.Next(4, G - 6);
                for (int k = 0; k < 6; k++) c.Set(x + k, y + (k % 3 == 0 ? 1 : 0), C("#ff8020"));
            }
            return c;
        }

        static Canvas Frost()
        {
            var c = new Canvas(W, H, 13);
            Grad(c, G, H, "#a8c8f0", "#2a3a6a");
            Stars(c, 20, 2, C("#e0f0ff"), 80);
            Ridge(c, 70, 14, 19, 3, C("#8aa0c8"), true);
            Ridge(c, 52, 5, 29, 6, C("#5a70a0"));
            Pines(c, 18, G + 3, 20, C("#2a4060"), 6, C("#f0f8ff"));
            Ground(c, "#ffffff", "#c8dcf0", "#98b0d0", 9);
            var rng = new System.Random(3);
            for (int i = 0; i < 6; i++) Crystal(c, rng.Next(W), G - 2, rng.Next(6, 12), C("#8ad8ff"));
            return c;
        }

        static Canvas Swamp()
        {
            var c = new Canvas(W, H, 14);
            Grad(c, G, H, "#5a6a3a", "#1a2418");
            Moon(c, 60, 86, 7, C("#d8e0a0"));
            Ridge(c, 54, 5, 21, 8, C("#2a3424"));
            var rng = new System.Random(8);
            for (int i = 0; i < 7; i++) DeadTree(c, rng.Next(W), G, rng.Next(18, 30), C("#1a2014"));
            Fog(c, G + 4, 14, new Color32(150, 190, 110, 60));
            Ground(c, "#5a6a2a", "#3a3a1e", "#2a2a14", 10);
            for (int i = 0; i < 5; i++) // 늪 웅덩이 + 연잎
            {
                int x = rng.Next(20, W - 20), y = rng.Next(6, G - 10);
                c.Ellipse(x, y, 10, 3, C("#2a4a3a"), true);
                c.HLine(x - 6, y + 1, 5, C("#4a7a5a"));
                c.Ellipse(x + 3, y, 2, 1, C("#6aa040"), true);
            }
            return c;
        }

        static Canvas Grave()
        {
            var c = new Canvas(W, H, 15);
            Grad(c, G, H, "#3a3a5a", "#0c0c18");
            Stars(c, 40, 3, C("#a0a8d0"));
            Moon(c, 200, 84, 12, C("#e0e8ff"));
            Ridge(c, 50, 4, 25, 9, C("#1c1c2a"));
            var rng = new System.Random(5);
            for (int i = 0; i < 4; i++) DeadTree(c, rng.Next(W), G + 4, rng.Next(20, 30), C("#14141e"));
            // 비석, 십자가
            for (int i = 0; i < 9; i++)
            {
                int x = rng.Next(4, W - 8), y = G - 3 + rng.Next(0, 6);
                if (i % 3 == 0) { c.Rect(x + 2, y, 2, 11, C("#6a6a7a")); c.Rect(x, y + 7, 6, 2, C("#6a6a7a")); }
                else { c.Rect(x, y, 7, 9, C("#5a5a6a")); c.Rect(x + 1, y + 9, 5, 1, C("#5a5a6a")); c.HLine(x + 2, y + 5, 3, C("#3a3a4a")); c.VLine(x, y, 9, C("#7a7a8a")); }
            }
            Fog(c, G - 2, 10, new Color32(140, 160, 210, 60));
            Ground(c, "#4a5a4a", "#2a2e2a", "#1c201c", 11);
            return c;
        }

        static Canvas Ruins()
        {
            var c = new Canvas(W, H, 16);
            Grad(c, G, H, "#f0c070", "#c85a3a");
            c.Ellipse(70, 70, 10, 10, C("#fff0b0"), true);
            Tri(c, 190, G + 4, 34, 40, C("#b0703a")); // 피라미드
            for (int j = 0; j < 40; j += 4) c.HLine(190 - 34 + j * 34 / 40, G + 4 + j, (34 - j * 34 / 40) * 2, C("#9a5a2a"));
            Ridge(c, 46, 4, 31, 4, C("#d89850"));
            // 부서진 기둥
            var rng = new System.Random(6);
            for (int i = 0; i < 5; i++)
            {
                int x = rng.Next(10, W - 10), h = rng.Next(12, 30);
                c.Rect(x, G - 2, 6, h, C("#c8a878")); c.VLine(x, G - 2, h, C("#e8c898")); c.VLine(x + 5, G - 2, h, C("#9a7a50"));
                c.Rect(x - 1, G - 2 + h, 8, 2, C("#d8b888"));
            }
            Ground(c, "#f0d090", "#d8b070", "#b08a50", 12);
            for (int x = 0; x < W; x += 9) c.HLine(x, 10 + (x / 9 % 3) * 6, 5, C("#e8c080")); // 모래결
            return c;
        }

        static Canvas CrystalCave()
        {
            var c = new Canvas(W, H, 17);
            Grad(c, G, H, "#3a2a5a", "#0e0a1a");
            var rng = new System.Random(7);
            for (int i = 0; i < 14; i++) { int x = rng.Next(W); int h = rng.Next(5, 14); for (int j = 0; j < h; j++) c.Rect(x - (h - j) / 4, H - 1 - j, (h - j) / 4 * 2 + 1, 1, C("#1a1428")); }
            Ridge(c, 58, 6, 19, 5, C("#241a38"), true);
            // 광산 버팀목
            for (int x = 20; x < W; x += 70) { c.Rect(x, G, 3, 40, C("#6a4a2a")); c.Rect(x + 30, G, 3, 40, C("#6a4a2a")); c.Rect(x - 2, G + 40, 37, 3, C("#7a5a30")); }
            string[] cols = { "#ff90e0", "#80f0ff", "#c080ff" };
            for (int i = 0; i < 14; i++) Crystal(c, rng.Next(W), G + rng.Next(-2, 10), rng.Next(6, 16), C(cols[i % 3]));
            Ground(c, "#5a4a7a", "#2e2440", "#1e1830", 13);
            for (int i = 0; i < 8; i++) Crystal(c, rng.Next(W), rng.Next(2, G - 10), rng.Next(3, 6), C(cols[i % 3]));
            return c;
        }

        static Canvas Storm()
        {
            var c = new Canvas(W, H, 18);
            Grad(c, G, H, "#5a6a90", "#141a30");
            var rng = new System.Random(2);
            for (int i = 0; i < 6; i++) c.Ellipse(rng.Next(W), rng.Next(80, 104), rng.Next(16, 30), rng.Next(4, 8), C("#2a3050"), true); // 먹구름
            // 번개
            foreach (int bx in new[] { 60, 190 })
            {
                int x = bx, y = H - 6;
                while (y > 52) { int nx = x + rng.Next(-3, 4); for (int k = 0; k < 4; k++) { c.Set(x, y - k, C("#fff8a0")); c.Set(x + 1, y - k, C("#c0d0ff")); } x = nx; y -= 4; }
            }
            Ridge(c, 64, 16, 15, 7, C("#2e3450"), true);
            Ridge(c, 48, 6, 11, 2, C("#3e4460"), true);
            Ground(c, "#8a8a9a", "#4a4a5a", "#34343f", 14);
            for (int i = 0; i < 6; i++) { int x = rng.Next(W); c.Ellipse(x, G, 5, 3, C("#5e5e6e"), true); c.HLine(x - 3, G + 2, 4, C("#7e7e8e")); }
            return c;
        }

        static Canvas Abyss()
        {
            var c = new Canvas(W, H, 19);
            Grad(c, G, H, "#0a3a50", "#02101c");
            // 위에서 내려오는 빛줄기
            for (int k = 0; k < 4; k++)
            {
                int x0 = 30 + k * 70;
                for (int y = G; y < H; y++)
                    for (int i = 0; i < 8; i++)
                        if ((i + y) % 3 == 0) c.Set(x0 + i + (H - y) / 4, y, new Color32(120, 220, 255, 26));
            }
            Ridge(c, 52, 7, 19, 3, C("#06202e"));
            var rng = new System.Random(10);
            for (int i = 0; i < 10; i++) // 해초
            {
                int x = rng.Next(W), h = rng.Next(14, 30);
                for (int j = 0; j < h; j++) c.Set(x + Mathf.RoundToInt(Mathf.Sin(j / 3f + i) * 1.5f), G - 2 + j, C("#1a6a4a"));
            }
            Ground(c, "#5a7a80", "#2a3a44", "#1a2830", 15);
            for (int i = 0; i < 6; i++) { int x = rng.Next(W); c.Ellipse(x, G + 1, 3, 2, C("#ff7090"), true); c.Set(x, G + 3, C("#ffb0c0")); }
            return c;
        }

        static Canvas Chaos()
        {
            var c = new Canvas(W, H, 20);
            Grad(c, G, H, "#5a1050", "#0a000c");
            Stars(c, 50, 7, C("#ff90ff"));
            // 균열
            var rng = new System.Random(12);
            int cx = 180, cy = 80;
            for (int k = 0; k < 30; k++) { c.Set(cx, cy, C("#ffffff")); c.Set(cx + 1, cy, C("#ff60ff")); c.Set(cx - 1, cy, C("#a020c0")); cx += rng.Next(-1, 2); cy -= 1; }
            // 떠 있는 바위섬
            for (int i = 0; i < 4; i++)
            {
                int x = rng.Next(20, W - 20), y = rng.Next(60, 96), w = rng.Next(8, 18);
                for (int j = 0; j < w / 2; j++) c.Rect(x - w + j, y - j, (w - j) * 2, 1, C("#2a1a34"));
                c.HLine(x - w, y + 1, w * 2, C("#6a3a7a"));
            }
            Ridge(c, 50, 6, 13, 4, C("#1e0e24"), true);
            Ground(c, "#6a2a7a", "#241428", "#160a18", 16);
            for (int i = 0; i < 10; i++) { int x = rng.Next(W), y = rng.Next(4, G - 6); for (int k = 0; k < 7; k++) c.Set(x + k, y + (k % 2), C("#e040ff")); }
            return c;
        }

        public static IEnumerable<(string name, Canvas canvas)> All()
        {
            yield return ("stage_forest", Forest());
            yield return ("stage_lava", Lava());
            yield return ("stage_frost", Frost());
            yield return ("stage_swamp", Swamp());
            yield return ("stage_grave", Grave());
            yield return ("stage_ruins", Ruins());
            yield return ("stage_crystal", CrystalCave());
            yield return ("stage_storm", Storm());
            yield return ("stage_abyss", Abyss());
            yield return ("stage_chaos", Chaos());
        }
    }
}

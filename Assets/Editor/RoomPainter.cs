using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 건물 내부 인테리어를 작은 도트로 그린다. 방 1칸 = 160×96 px.
    // 좌표는 왼쪽 아래 (0,0). 바닥면 y 0~9, 벽 y 10~91, 천장 보 y 92~95.
    public class Canvas
    {
        public readonly int W, H;
        readonly Color32[] px;
        public System.Random rng;

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 K = C("#1b1022");

        public Canvas(int w, int h, int seed)
        {
            W = w; H = h;
            px = new Color32[w * h];
            rng = new System.Random(seed);
        }

        public static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            if (c.a == 255 || c.a == 0) { if (c.a == 255) px[y * W + x] = c; return; }
            var o = px[y * W + x];
            float a = c.a / 255f;
            px[y * W + x] = new Color32((byte)(o.r + (c.r - o.r) * a), (byte)(o.g + (c.g - o.g) * a), (byte)(o.b + (c.b - o.b) * a), (byte)Mathf.Max(o.a, c.a));
        }

        public void Erase(int x, int y) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = Clear; }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        public void HLine(int x, int y, int w, Color32 c) => Rect(x, y, w, 1, c);
        public void VLine(int x, int y, int h, Color32 c) => Rect(x, y, 1, h, c);

        // 외곽선 + 윗면 밝게 + 아래/오른쪽 어둡게 = 도트 가구의 기본 상자
        public void Box(int x, int y, int w, int h, Color32 c)
        {
            Rect(x, y, w, h, K);
            Rect(x + 1, y + 1, w - 2, h - 2, c);
            HLine(x + 1, y + h - 2, w - 2, Shade(c, 1.3f));
            VLine(x + 1, y + 1, h - 3, Shade(c, 1.12f));
            HLine(x + 1, y + 1, w - 2, Shade(c, 0.7f));
            VLine(x + w - 2, y + 1, h - 2, Shade(c, 0.8f));
        }

        public void Noise(int x, int y, int w, int h, float amount)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                {
                    if (i < 0 || j < 0 || i >= W || j >= H) continue;
                    var o = px[j * W + i];
                    if (o.a == 0) continue;
                    float f = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
                    px[j * W + i] = Shade(o, f);
                }
        }

        public void Bricks(int x, int y, int w, int h, Color32 brick, Color32 mortar, int bw = 12, int bh = 6)
        {
            Rect(x, y, w, h, mortar);
            for (int row = 0; row * bh < h; row++)
            {
                int off = row % 2 == 0 ? 0 : bw / 2;
                for (int bx = -bw; bx < w; bx += bw)
                {
                    int sx = x + bx + off, sy = y + row * bh;
                    var c = Shade(brick, 0.9f + (float)rng.NextDouble() * 0.2f);
                    for (int j = sy; j < sy + bh - 1 && j < y + h; j++)
                        for (int i = Math.Max(sx, x); i < sx + bw - 1 && i < x + w; i++) Set(i, j, c);
                    for (int i = Math.Max(sx, x); i < sx + bw - 1 && i < x + w; i++) if (sy + bh - 2 < y + h) Set(i, sy + bh - 2, Shade(c, 1.12f));
                }
            }
        }

        public void PlanksH(int x, int y, int w, int h, Color32 c, int ph = 3)
        {
            for (int j = y; j < y + h; j++)
            {
                bool seam = (j - y) % ph == ph - 1;
                HLine(x, j, w, seam ? Shade(c, 0.7f) : c);
            }
            for (int j = y; j < y + h; j += ph)
            {
                int sx = x + rng.Next(0, 24);
                for (; sx < x + w; sx += 24 + rng.Next(0, 16)) VLine(sx, j, Math.Min(ph - 1, y + h - j), Shade(c, 0.75f));
            }
        }

        public void PlanksV(int x, int y, int w, int h, Color32 c, int pw = 6)
        {
            for (int i = x; i < x + w; i++)
            {
                bool seam = (i - x) % pw == pw - 1;
                var col = seam ? Shade(c, 0.7f) : Shade(c, 1f + (((i - x) / pw) % 2) * 0.06f);
                VLine(i, y, h, col);
            }
        }

        public void Tiles(int x, int y, int w, int h, Color32 a, Color32 b, int t = 8)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, (((i - x) / t) + ((j - y) / (t / 2))) % 2 == 0 ? a : b);
        }

        public void Ellipse(int cx, int cy, int rx, int ry, Color32 c, bool fill)
        {
            for (int j = -ry; j <= ry; j++)
                for (int i = -rx; i <= rx; i++)
                {
                    float d = (i * i) / (float)(rx * rx) + (j * j) / (float)(ry * ry);
                    if (fill ? d <= 1f : (d <= 1f && d >= 0.72f)) Set(cx + i, cy + j, c);
                }
        }

        // 텍스트 격자 도장 (행은 위→아래로 적는다). x,y = 왼쪽 아래
        public void Stamp(string[] rows, string palette, int x, int y)
        {
            var pal = ParsePalette(PixelArtData.Common + "," + palette);
            for (int r = 0; r < rows.Length; r++)
            {
                int yy = y + rows.Length - 1 - r;
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char ch = rows[r][i];
                    if (ch == '.') continue;
                    if (pal.TryGetValue(ch, out var c)) Set(x + i, yy, c);
                }
            }
        }

        public static Dictionary<char, Color32> ParsePalette(string s)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var part in s.Split(','))
            {
                var kv = part.Trim();
                if (kv.Length < 3 || kv[1] != '=') continue;
                if (ColorUtility.TryParseHtmlString(kv.Substring(2), out var c)) d[kv[0]] = c;
            }
            return d;
        }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }


    // 방 인테리어. 레벨이 오를수록 소품이 하나씩 늘어 점점 고급스러워진다(Lv.1 = 기본 방).
    // 공통 단계: 깔개 → 장식 → 금테 → 샹들리에 → 벽지 → 화분 → 금빛 마감. 방마다 자리에 맞게 순서를 조금씩 바꾼다.
    public static class RoomPainter
    {
        public const int W = 160, H = 96, FloorH = 10;

        static Color32 C(string h) => Canvas.C(h);
        static readonly Color32 K = Canvas.K;
        static readonly Color32 White = C("#f4f0e8");
        static readonly Color32 Gold = C("#d8a848"), GoldD = C("#8a5a24"), GoldL = C("#ffe08a");

        // 공통 틀: 천장 보, 양옆 기둥, 굽도리
        static void Shell(Canvas c, Color32 beam)
        {
            c.Box(0, 91, W, 5, beam);
            c.Rect(0, 0, 2, H, K);
            c.Rect(W - 2, 0, 2, H, K);
            c.HLine(2, FloorH, W - 4, K);
            c.HLine(2, FloorH + 1, W - 4, Canvas.Shade(beam, 0.8f));
        }

        static readonly string[] Torch =
        {
            "..Y..",
            ".YOY.",
            ".OYO.",
            "..O..",
            ".KWK.",
            "KWWWK",
            ".KbK.",
            "..K..",
        };
        const string TorchPal = "O=#ff8020,b=#6a4020";
        static readonly string[] Gem = { "..c..", ".cCc.", "cCWCc", ".cCc.", "..c.." };
        static readonly string[] Flags = { "#e04848", "#48a0e0", "#f0c040", "#70d060" };

        // ── 레벨 장식 조각 ─────────────────────────────────────
        // 천장 보 아래 금테 (Shell 다음에)
        static void GoldTrim(Canvas c)
        {
            c.HLine(2, 90, W - 4, Gold);
            c.HLine(2, 89, W - 4, GoldD);
            for (int x = 8; x < W - 6; x += 16) c.Set(x, 90, GoldL);
            c.Box(0, 91, 4, 5, Gold);
            c.Box(W - 4, 91, 4, 5, Gold);
        }

        // 촛불 샹들리에 (Shell 다음에)
        static void Chandelier(Canvas c, int cx)
        {
            c.VLine(cx, 83, 8, K);
            c.Box(cx - 10, 79, 21, 4, Gold);
            c.HLine(cx - 8, 78, 17, GoldD);
            foreach (int dx in new[] { -8, -3, 2, 7 })
            {
                c.Rect(cx + dx, 83, 2, 3, White);
                c.Set(cx + dx, 86, C("#ffd040"));
                c.Set(cx + dx + 1, 86, C("#ff8020"));
                c.Set(cx + dx, 87, C("#ffd040"));
            }
            c.Set(cx, 76, Gold); c.Set(cx - 1, 77, GoldD); c.Set(cx + 1, 77, GoldD);
        }

        // 금테 액자
        static void Painting(Canvas c, int x, int y, int w, int h, string sky, string moon)
        {
            c.Box(x, y, w, h, Gold);
            c.Rect(x + 2, y + 2, w - 4, h - 4, C(sky));
            c.Rect(x + 2, y + 2, w - 4, Mathf.Max(2, (h - 4) / 3), Canvas.Shade(C(sky), 0.6f));
            c.Ellipse(x + w - 6, y + h - 6, 2, 2, C(moon), true);
        }

        // 화분 (바닥에)
        static void Plant(Canvas c, int x, string leaf = "#4aa050")
        {
            c.Box(x, FloorH, 10, 8, C("#a85a3a"));
            c.HLine(x + 1, FloorH + 6, 8, C("#c8784a"));
            c.Stamp(new[]
            {
                "..g.g...",
                ".gGgGg..",
                "gGgGgGg.",
                ".gGgGgg.",
                "..gGg...",
                "...g....",
            }, $"g={leaf},G=#2a6a30", x + 1, FloorH + 8);
        }

        // 바닥 깔개 (가구보다 먼저)
        static void Rug(Canvas c, int x, int w, string col)
        {
            var rc = C(col);
            c.Rect(x, 1, w, 7, rc);
            c.HLine(x, 1, w, Canvas.Shade(rc, 0.7f));
            c.HLine(x + 1, 6, w - 2, Gold);
            c.HLine(x + 1, 2, w - 2, Gold);
            for (int i = x + 4; i < x + w - 3; i += 6) c.Set(i, 4, GoldL);
            for (int i = x; i < x + w; i += 2) c.Set(i, 0, Canvas.Shade(rc, 1.2f));
        }

        // 걸린 깃발 (top 에서 아래로 h)
        static void Banner(Canvas c, int x, int top, int h, string col)
        {
            var bc = C(col);
            c.HLine(x - 1, top, 10, GoldD);
            c.Rect(x, top - h, 8, h, bc);
            c.VLine(x, top - h, h, Canvas.Shade(bc, 1.2f));
            c.VLine(x + 7, top - h, h, Canvas.Shade(bc, 0.75f));
            c.Erase(x + 3, top - h); c.Erase(x + 4, top - h); c.Erase(x + 4, top - h + 1); c.Erase(x + 3, top - h + 1);
            c.Rect(x + 3, top - h / 2 - 1, 2, 3, Gold);
        }

        // 매달린 등불
        static void Lantern(Canvas c, int x, int top)
        {
            c.VLine(x, top - 10, 11, K);
            c.Stamp(new[] { ".KKK.", "KYYYK", "KYWYK", ".KKK." }, "", x - 2, top - 14);
        }

        // 깃발 줄 (x0 ~ x1, 높이 y)
        static void Bunting(Canvas c, int x0, int x1, int y)
        {
            for (int x = x0; x < x1; x += 6)
            {
                c.HLine(x, y + 4 - (x / 6 % 2), 6, K);
                c.Stamp(new[] { "fff", ".f." }, $"f={Flags[x / 6 % 4]}", x + 1, y);
            }
        }

        // 반짝이 (최고 레벨)
        static void Sparkles(Canvas c, int x, int y, int w, int h, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int sx = x + c.rng.Next(0, w), sy = y + c.rng.Next(0, h);
                c.Set(sx, sy, GoldL); c.Set(sx - 1, sy, Gold); c.Set(sx + 1, sy, Gold); c.Set(sx, sy - 1, Gold); c.Set(sx, sy + 1, Gold);
            }
        }

        static void Outline(Canvas c, int x, int y, int w, int h, Color32 col)
        {
            c.HLine(x, y, w, col); c.HLine(x, y + h - 1, w, col);
            c.VLine(x, y, h, col); c.VLine(x + w - 1, y, h, col);
        }

        static void Line(Canvas c, Vector2Int a, Vector2Int b, Color32 col)
        {
            int dx = Math.Abs(b.x - a.x), dy = -Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, err = dx + dy;
            int x = a.x, y = a.y;
            while (true)
            {
                c.Set(x, y, col);
                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        // ── 방 ─────────────────────────────────────────────────
        public static Canvas Empty()
        {
            var c = new Canvas(W, H, 1);
            c.Bricks(2, FloorH, W - 4, H - FloorH, C("#7c7c84"), C("#6c6c74"), 16, 8);
            c.Tiles(2, 0, W - 4, FloorH, C("#5e5e66"), C("#58585f"), 16);
            Shell(c, C("#6a6a72"));
            c.Noise(2, 0, W - 4, H - 5, 0.03f);
            // 거미줄 한 가닥
            for (int i = 0; i < 10; i++) { c.Set(3 + i, 90 - i, C("#a8a8b0")); c.Set(3, 90 - i, C("#a8a8b0")); c.Set(3 + i, 90, C("#a8a8b0")); }
            return c;
        }

        // 아직 증축하지 않은 칸: 벽돌로 막아 둔 방
        public static Canvas Sealed()
        {
            var c = new Canvas(W, H, 2);
            c.Bricks(0, 0, W, H, C("#4e4150"), C("#443846"), 20, 10);
            c.Noise(0, 0, W, H, 0.05f);
            c.Rect(0, 0, 2, H, K);
            c.Rect(W - 2, 0, 2, H, K);
            c.HLine(0, H - 1, W, K);
            c.HLine(0, 0, W, K);
            return c;
        }

        // 전시 우리 Lv.1~5: 먹이통 → 깃발 → 횃불 → 금테·명판
        public static Canvas Tank(int lv)
        {
            var c = new Canvas(W, H, 2);
            c.Bricks(2, FloorH, W - 4, H - FloorH, C("#3c3448"), C("#28222f"));
            // 짚이 깔린 바닥
            c.Rect(2, 0, W - 4, FloorH, C("#8a7436"));
            for (int i = 0; i < 260; i++)
            {
                int x = c.rng.Next(2, W - 4), y = c.rng.Next(0, FloorH);
                c.HLine(x, y, c.rng.Next(2, 5), c.rng.Next(2) == 0 ? C("#b09a4a") : C("#6a5626"));
            }
            if (lv >= 3) { Banner(c, 44, 88, 24, "#7a2a4a"); Banner(c, 108, 88, 24, "#7a2a4a"); }
            // 벽의 사슬
            foreach (int x in new[] { 24, 136 })
            {
                for (int y = 90; y > 58; y -= 3) { c.Set(x, y, C("#8a8a9a")); c.Set(x + 1, y - 1, C("#5a5a6a")); }
                c.Stamp(new[] { ".KKK.", "K...K", "K...K", ".KKK." }, "", x - 2, 52);
            }
            c.Stamp(Torch, TorchPal, 77, 62);
            if (lv >= 4) { c.Stamp(Torch, TorchPal, 58, 62); c.Stamp(Torch, TorchPal, 96, 62); }
            // 바위와 물통
            c.Box(8, 6, 14, 9, C("#6a6470"));
            c.Box(18, 6, 8, 6, C("#58525e"));
            c.Box(128, 6, 26, 9, C("#6a4a2a"));
            c.Rect(130, 12, 22, 2, C("#3a70c0"));
            if (lv >= 2)
            {
                // 고기가 담긴 먹이통
                c.Box(52, 2, 18, 6, C("#5a4a3a"));
                c.Stamp(new[] { ".WRRR.", "RRrRRR" }, "R=#c04040,r=#e07070", 58, 8);
            }
            Shell(c, C("#2a2430"));
            if (lv >= 5)
            {
                GoldTrim(c);
                c.Box(68, 76, 24, 9, Gold);
                c.Rect(70, 78, 20, 5, C("#3a2a18"));
                c.HLine(72, 80, 16, GoldL);
            }
            return c;
        }

        // 전시 우리 앞 창살 (괴물 위에 겹친다)
        public static Canvas TankFront()
        {
            var c = new Canvas(W, H, 3);
            var bar = C("#7a7a8a");
            var hi = C("#b8b8c8");
            for (int x = 10; x < W - 6; x += 22)
            {
                c.VLine(x, 3, 86, K);
                c.VLine(x + 1, 3, 86, bar);
                c.VLine(x + 2, 3, 86, K);
                for (int y = 6; y < 88; y += 7) c.Set(x + 1, y, hi);
            }
            c.Box(2, 84, W - 4, 6, C("#5a5a6a"));
            c.Box(2, 1, W - 4, 4, C("#5a5a6a"));
            return c;
        }

        // 식당 Lv.1~10: 깔개 → 식탁보 → 촛불 → 액자 → 금테 → 샹들리에 → 벽지 → 화분 → 금빛 메뉴판
        public static Canvas Restaurant(int lv)
        {
            var c = new Canvas(W, H, 4);
            // 윗벽 줄무늬 벽지 + 아래 나무 판벽
            c.Rect(2, 36, W - 4, H - 36, C(lv >= 8 ? "#6a1e2c" : "#5a1e24"));
            for (int x = 4; x < W - 2; x += 8) c.VLine(x, 38, 53, C(lv >= 8 ? "#7c2c3a" : "#6c2a32"));
            if (lv >= 8) for (int y = 42; y < 90; y += 8) for (int x = 8 + (y / 8 % 2) * 4; x < W - 4; x += 8) c.Set(x, y, Gold);
            c.PlanksV(2, FloorH, W - 4, 26, C("#6a3a22"));
            c.HLine(2, 36, W - 4, K);
            c.HLine(2, 37, W - 4, lv >= 6 ? Gold : C("#a06a3a"));
            c.PlanksH(2, 0, W - 4, FloorH, C("#8a5a34"));
            if (lv >= 2) Rug(c, 66, 88, "#8a2030");
            // 창문 (붉은 달)
            c.Box(62, 54, 26, 26, C("#4a2a18"));
            c.Rect(64, 56, 22, 22, C("#1a1e48"));
            c.Ellipse(80, 72, 3, 3, C("#e03040"), true);
            c.VLine(74, 56, 22, C("#4a2a18"));
            c.HLine(64, 66, 22, C("#4a2a18"));
            // 메뉴판
            c.Box(98, 56, 34, 22, C("#2a2a2a"));
            for (int y = 60; y < 74; y += 4) c.HLine(102, y, 18 + c.rng.Next(0, 8), C("#d8d8c8"));
            if (lv >= 10) { Outline(c, 97, 55, 36, 24, Gold); c.Rect(122, 60, 7, 14, C("#2a2a2a")); c.Stamp(new[] { "Y.Y.Y", "YYYYY", "YYYYY" }, "", 123, 64); }
            if (lv >= 5) Painting(c, 136, 50, 20, 26, "#2a4a6a", "#f0e0a0");
            // 매달린 등
            foreach (int x in new[] { 44, 146 })
            {
                c.VLine(x, 80, 11, K);
                c.Stamp(new[] { ".KKK.", "KYYYK", "KYWYK", ".KKK." }, "", x - 2, 76);
            }
            // 선반과 술병
            c.Box(4, 58, 48, 4, C("#7a4a28"));
            int bx = 7;
            foreach (var col in new[] { "#40a060", "#a03040", "#d0a040", "#4060c0", "#a060c0", "#40a060" })
            {
                c.Rect(bx, 62, 3, 6, K); c.Rect(bx + 1, 63, 1, 4, C(lv >= 10 ? "#ffd040" : col)); c.Set(bx + 1, 68, K);
                bx += 7;
            }
            // 조리대 + 솥
            c.Box(4, FloorH, 50, 24, C("#8a5a30"));
            c.Rect(5, 30, 48, 3, C("#c09060"));
            c.Stamp(new[]
            {
                "..w...w...",
                "...w...w..",
                ".KKKKKKKK.",
                "KgggGgggGK",
                "KKKKKKKKKK",
                ".KppppppK.",
                ".KpPppppK.",
                "..KKKKKK..",
            }, "w=#c0c0d8,g=#70c050,G=#3a8030,p=#4a4a5a,P=#6a6a80", 20, 33);
            if (lv >= 9) Plant(c, 56);
            // 식탁 2개 + 의자
            foreach (int x in new[] { 72, 116 })
            {
                c.Box(x, 22, 28, 4, C("#9a6a3a"));
                c.Rect(x + 12, FloorH, 4, 12, K);
                c.Rect(x + 13, FloorH, 2, 12, C("#6a4020"));
                if (lv >= 3)
                {
                    // 식탁보
                    c.Rect(x - 1, 24, 30, 2, White);
                    c.Rect(x - 1, 19, 2, 5, White);
                    c.Rect(x + 27, 19, 2, 5, White);
                    c.HLine(x - 1, 23, 30, C("#d0c8d8"));
                }
                c.Stamp(new[] { "K..", "Kb.", "Kb.", "KbK", "Kbb", "K.K", "K.K" }, "b=#7a4a28", x - 5, 5);
                c.Stamp(new[] { "..K", ".bK", ".bK", "KbK", "bbK", "K.K", "K.K" }, "b=#7a4a28", x + 30, 5);
                c.Stamp(new[] { ".W.", "KWK" }, "", x + 6, 26);
                if (lv >= 4) c.Stamp(new[] { "Y", "O", "W", "W", "W" }, "O=#ff8020", x + 20, 26);
            }
            Shell(c, C("#4a2a18"));
            if (lv >= 6) GoldTrim(c);
            if (lv >= 7) Chandelier(c, 112);
            if (lv >= 10) Sparkles(c, 60, 40, 90, 36, 6);
            return c;
        }

        // 기념품점 Lv.1~10: 등불 → 수정 진열대 → 포스터 → 깃발 줄 → 금테 → 유리 진열장 → 벽지 → 화분 → 금빛 선반
        public static Canvas Souvenir(int lv)
        {
            var c = new Canvas(W, H, 5);
            c.Rect(2, FloorH, W - 4, H - FloorH, C(lv >= 8 ? "#44285c" : "#3a2450"));
            for (int y = 16; y < 90; y += 10)
                for (int x = 8 + (y / 10 % 2) * 6; x < W - 4; x += 12) c.Set(x, y, lv >= 8 ? Gold : C("#6a4a88"));
            c.Tiles(2, 0, W - 4, FloorH, C("#4a3a50"), C("#40324a"), 12);
            if (lv >= 4)
            {
                // 포스터: 마왕성
                c.Box(10, 46, 30, 26, C("#e8d8b0"));
                c.Rect(12, 48, 26, 22, C("#2a1a3a"));
                c.Rect(18, 50, 14, 10, C("#4a3a5a"));
                c.Rect(16, 60, 4, 4, C("#4a3a5a")); c.Rect(30, 60, 4, 4, C("#4a3a5a")); c.Rect(23, 60, 4, 6, C("#4a3a5a"));
                c.Ellipse(33, 66, 2, 2, C("#e03040"), true);
            }
            if (lv >= 5) Bunting(c, 4, 50, 80);
            // 선반 3단 + 상품
            c.Box(94, FloorH, 62, 76, C("#5a3420"));
            c.Rect(97, FloorH + 2, 56, 71, C("#2a1a26"));
            string[] cols = { "#e04848", "#48a0e0", "#f0c040", "#70d060", "#c060e0", "#f08040", "#f0f0f0" };
            for (int s = 0; s < 3; s++)
            {
                int sy = 14 + s * 24;
                c.Box(95, sy, 60, 3, lv >= 10 ? Gold : C("#7a4a28"));
                for (int x = 99; x < 150;)
                {
                    int w = c.rng.Next(3, 6), h = c.rng.Next(4, 9);
                    var col = C(cols[c.rng.Next(cols.Length)]);
                    c.Rect(x, sy + 3, w, h, K);
                    c.Rect(x + 1, sy + 3, w - 2, h - 1, col);
                    c.Set(x + 1, sy + 3 + h - 2, Canvas.Shade(col, 1.3f));
                    x += w + 1;
                }
            }
            if (lv >= 10) Outline(c, 93, FloorH - 1, 64, 78, Gold);
            // 해골 깃발
            c.Rect(56, 58, 20, 30, C("#a02030"));
            for (int x = 56; x < 76; x += 4) { c.Erase(x + 1, 58); c.Erase(x + 2, 58); c.Erase(x + 2, 59); }
            c.Stamp(new[] { ".KKKK.", "KWWWWK", "KBWWBK", "KWWWWK", ".KWWK.", "..KK.." }, "", 63, 70);
            c.HLine(54, 88, 24, C("#c8a040"));
            if (lv >= 2) { Lantern(c, 50, 90); Lantern(c, 84, 90); }
            // 카운터 + 금전등록기
            c.Box(4, FloorH, 44, 22, C("#5a3060"));
            c.Rect(5, 28, 42, 3, C("#8a5a90"));
            c.Stamp(new[] { ".KKKKK.", "KgGgGgK", "KKKKKKK", "KmmmmmK", "KmYmmmK", "KKKKKKK" }, "g=#60e080,G=#309050,m=#8a8aa0", 14, 31);
            if (lv >= 7)
            {
                // 유리 진열장 + 왕관
                c.Box(26, 31, 20, 13, C("#9ad0e0"));
                c.Rect(28, 33, 16, 9, C("#2a3a4a"));
                c.Stamp(new[] { "Y.Y.Y", "YYYYY", "YEYEY" }, "", 33, 34);
                c.Set(29, 40, White); c.Set(30, 39, White);
            }
            c.Ellipse(70, 4, 20, 3, C("#8a2030"), true);
            c.Ellipse(70, 4, 20, 3, C("#c8a040"), false);
            if (lv >= 3)
            {
                // 수정 진열대
                c.Box(80, FloorH, 10, 12, C("#6a4a7a"));
                c.HLine(81, FloorH + 10, 8, C("#9a7aaa"));
                c.Stamp(Gem, "c=#60d0f0,C=#2a90c0", 83, FloorH + 12);
            }
            if (lv >= 9) Plant(c, 50, "#5ac060");
            Shell(c, C("#2a1a38"));
            if (lv >= 6) GoldTrim(c);
            if (lv >= 10) Sparkles(c, 50, 40, 40, 36, 5);
            return c;
        }

        // 연구소 Lv.1~5: 깔개 → 떠다니는 수정·물약 선반 → 오망성·금테·룬 → 샹들리에·수정 오브
        public static Canvas Lab(int lv)
        {
            var c = new Canvas(W, H, 6);
            c.Bricks(2, FloorH, W - 4, H - FloorH, C("#23403a"), C("#162a28"));
            c.Tiles(2, 0, W - 4, FloorH, C("#2a3a3a"), C("#243232"), 12);
            if (lv >= 2) Rug(c, 40, 64, "#4a2470");
            // 책장
            c.Box(4, FloorH, 32, 70, C("#4a2a18"));
            string[] books = { "#a03030", "#3050a0", "#308040", "#a08030", "#6a3a8a", "#c0c0c0" };
            for (int s = 0; s < 4; s++)
            {
                int sy = FloorH + 3 + s * 16;
                c.HLine(6, sy - 1, 28, C("#2a180e"));
                for (int x = 7; x < 33;)
                {
                    int w = c.rng.Next(2, 4), h = c.rng.Next(8, 13);
                    c.Rect(x, sy, w, h, C(books[c.rng.Next(books.Length)]));
                    c.VLine(x + w, sy, h, K);
                    x += w + 1;
                }
            }
            c.Stamp(new[] { ".KKK.", "KWWWK", "KBWBK", ".KWK." }, "", 16, 76);
            // 벽의 마법진 (가마솥 뒤)
            c.Ellipse(80, 56, 20, 20, C("#b060ff"), false);
            c.Ellipse(80, 56, 12, 12, C("#8040d0"), false);
            var pts = new Vector2Int[5];
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + Mathf.PI / 2f;
                pts[i] = new Vector2Int(80 + Mathf.RoundToInt(Mathf.Cos(a) * 16), 56 + Mathf.RoundToInt(Mathf.Sin(a) * 16));
            }
            if (lv >= 4) for (int i = 0; i < 5; i++) Line(c, pts[i], pts[(i + 2) % 5], C("#d090ff")); // 오망성이 빛난다
            foreach (var p in pts) c.Set(p.x, p.y, C("#ffd0ff"));
            if (lv >= 3)
                foreach (var (cx, cy) in new[] { (48, 66), (112, 72) })
                    c.Stamp(Gem, "c=#80f0ff,C=#30a0d0", cx - 2, cy - 2);
            // 가마솥 (불 → 솥 → 끓는 물약 → 기포)
            for (int x = 66; x < 96; x += 4) c.Stamp(new[] { ".O.", "OoO" }, "o=#ff8020,O=#ffd050", x, FloorH);
            c.Ellipse(80, 22, 15, 10, K, true);
            c.Ellipse(80, 22, 14, 9, C("#2a2a34"), true);
            c.Ellipse(75, 25, 4, 3, C("#4a4a5a"), true);
            c.Box(63, 29, 34, 5, lv >= 4 ? Gold : C("#3a3a48"));
            c.Rect(65, 32, 30, 2, C("#60e060"));
            foreach (var (bx, by) in new[] { (70, 36), (82, 38), (90, 35), (76, 42), (86, 46) })
                c.Stamp(new[] { ".g.", "gWg", ".g." }, "g=#80ff80", bx, by);
            // 실험대 + 플라스크
            c.Box(108, FloorH, 46, 20, C("#5a3a22"));
            c.Rect(109, 26, 44, 3, C("#8a6040"));
            string[] flask = { "..KK..", "..Kw..", "..Kw..", ".KwwK.", "KllllK", "KlLllK", ".KKKK." };
            c.Stamp(flask, "w=#a0b0c8,l=#50e0a0,L=#20a070", 112, 29);
            c.Stamp(flask, "w=#a0b0c8,l=#e05080,L=#a02050", 124, 29);
            c.Stamp(flask, "w=#a0b0c8,l=#5080f0,L=#2040a0", 136, 29);
            c.Stamp(new[] { ".Y.", ".O.", "KWK", "KWK", "KWK" }, "O=#ff8020", 148, 29);
            if (lv >= 3)
            {
                // 물약 선반
                c.Box(110, 48, 44, 3, C("#7a4a28"));
                int x = 113;
                foreach (var col in new[] { "#e05080", "#50e0a0", "#f0d040", "#5080f0", "#c060e0", "#50e0a0" })
                {
                    c.Rect(x, 51, 4, 5, K); c.Rect(x + 1, 51, 2, 3, C(col)); c.Set(x + 1, 56, K); c.Set(x + 2, 56, K);
                    x += 7;
                }
            }
            c.Stamp(Torch, TorchPal, 60, 62);
            c.Stamp(Torch, TorchPal, 96, 62);
            if (lv >= 5)
            {
                // 책장 위 수정 오브
                c.Box(22, 79, 12, 3, Gold);
                c.Ellipse(28, 85, 4, 3, K, true);
                c.Ellipse(28, 85, 3, 2, C("#70e0ff"), true);
                c.Set(27, 86, White);
            }
            Shell(c, C("#16262a"));
            if (lv >= 4)
            {
                GoldTrim(c);
                for (int x = 112; x < 152; x += 6) c.Stamp(new[] { "p.p", ".p.", "p.p" }, "p=#b070ff", x, 80);
            }
            if (lv >= 5) Chandelier(c, 80);
            return c;
        }

        // 직원 숙소 Lv.1~5: 커튼 → 액자 → 퀼트 이불 → 금테·화분
        public static Canvas Dorm(int lv)
        {
            var c = new Canvas(W, H, 7);
            c.PlanksV(2, FloorH, W - 4, H - FloorH, C("#4a3528"), 8);
            c.PlanksH(2, 0, W - 4, FloorH, C("#6a4a30"));
            // 창문
            c.Box(68, 52, 24, 24, C("#3a2616"));
            c.Rect(70, 54, 20, 20, C("#1a1e48"));
            c.Set(74, 70, C("#ffffff")); c.Set(84, 62, C("#ffffff")); c.Set(78, 58, C("#c0c0ff"));
            c.VLine(79, 54, 20, C("#3a2616"));
            if (lv >= 2)
            {
                // 커튼
                c.HLine(63, 78, 34, K);
                foreach (int x in new[] { 64, 92 })
                {
                    c.Rect(x, 50, 4, 28, C("#a02838"));
                    c.VLine(x + 1, 50, 28, C("#c03848"));
                    c.Rect(x, 62, 4, 2, Gold);
                }
            }
            if (lv >= 3) { Painting(c, 20, 75, 18, 13, "#3a5a3a", "#f0e0a0"); Painting(c, 120, 75, 18, 13, "#5a3a5a", "#e0e0ff"); }
            // 2층 침대 두 개
            foreach (int x in new[] { 6, 104 })
            {
                c.Rect(x, FloorH, 3, 62, K); c.Rect(x + 1, FloorH, 1, 62, C("#7a4a28"));
                c.Rect(x + 47, FloorH, 3, 62, K); c.Rect(x + 48, FloorH, 1, 62, C("#7a4a28"));
                foreach (int y in new[] { 14, 44 })
                {
                    c.Box(x + 2, y, 46, 6, C("#7a4a28"));
                    c.Box(x + 4, y + 5, 42, 5, C(y == 14 ? "#4a5ab0" : "#8a3a8a"));
                    if (lv >= 4) for (int i = x + 16; i < x + 44; i += 6) c.Rect(i, y + 6, 3, 3, C(y == 14 ? "#7a8ae0" : "#c06ac0")); // 퀼트 무늬
                    c.Box(x + 4, y + 8, 10, 5, C("#e8e4f0"));
                }
            }
            // 협탁 + 램프
            c.Box(70, FloorH, 20, 14, C("#6a4028"));
            c.Stamp(new[] { ".KKKK.", "KYYYYK", "KYWWYK", ".KKKK.", "..KK..", ".KKKK." }, "", 77, 24);
            c.Ellipse(80, 4, 14, 3, C("#3a5a3a"), true);
            if (lv >= 5) Plant(c, 58);
            Shell(c, C("#3a2616"));
            if (lv >= 5) GoldTrim(c);
            return c;
        }

        public static Canvas Rest()
        {
            var c = new Canvas(W, H, 8);
            c.Rect(2, FloorH, W - 4, H - FloorH, C("#23303a"));
            for (int y = 14; y < 90; y += 8)
                for (int x = 6 + (y / 8 % 2) * 4; x < W - 4; x += 8) c.Set(x, y, C("#34465a"));
            c.Rect(2, 0, W - 4, FloorH, C("#5a2a3a"));
            c.Noise(2, 0, W - 4, FloorH, 0.06f);
            // 그림 액자
            c.Box(58, 52, 44, 30, C("#c8a040"));
            c.Rect(61, 55, 38, 24, C("#2a1a3a"));
            c.Ellipse(88, 71, 4, 4, C("#e03040"), true);
            c.Rect(61, 55, 38, 6, C("#1a1020"));
            c.Stamp(new[] { "...K......K...", "..KKK....KKK..", ".KKKKK..KKKKK." }, "", 64, 60);
            // 소파
            c.Box(52, FloorH, 56, 12, C("#a02838"));
            c.Box(52, 20, 56, 14, C("#b83040"));
            c.Box(48, FloorH, 8, 18, C("#8a2030"));
            c.Box(104, FloorH, 8, 18, C("#8a2030"));
            // 화분 (마른 나무)
            c.Box(12, FloorH, 16, 12, C("#8a4a30"));
            c.Stamp(new[]
            {
                "K...K.K.",
                ".K.K..K.",
                "..K..K..",
                "..KKK...",
                "...K....",
                "...K....",
                "...K....",
            }, "", 16, 22);
            // 자판기
            c.Box(126, FloorH, 28, 50, C("#3a3a5a"));
            c.Rect(130, 30, 20, 24, C("#1a2a4a"));
            for (int y = 32; y < 52; y += 6)
                for (int x = 132; x < 148; x += 5) c.Rect(x, y, 3, 4, C(new[] { "#e04848", "#48a0e0", "#f0c040", "#70d060" }[(x + y) % 4]));
            c.Rect(132, 16, 16, 4, K);
            Shell(c, C("#1a2430"));
            return c;
        }

        // 투기장(콜로세움) Lv.1~10: 사암 2단 아치(아치마다 관중) · 붉은 띠를 두른 경기장 벽 · 모래 바닥.
        // 레벨마다 관중이 늘고 횃불 → 깃발 → 무기 거치대 → 금빛 띠 → 차양 → 황제석 → 깃발 줄 → 황금 트로피
        public static Canvas Arena(int lv)
        {
            var c = new Canvas(W, H, 9);
            var stone = C("#c2a57a");
            var stoneD = C("#9a8058");
            var stoneL = C("#e0c89a");
            var dark = C("#2e2226");
            c.Bricks(2, FloorH, W - 4, H - FloorH, stone, stoneD, 16, 8);
            c.Noise(2, FloorH, W - 4, H - FloorH, 0.04f);

            // 2단 아치 회랑 (아래층 y 38~62, 위층 y 64~88)
            string[] heads = { "#f0c8a0", "#c89070", "#8a5a40", "#e0b090", "#f4d8b8" };
            string[] shirts = { "#c04040", "#4060c0", "#40a060", "#d0a040", "#8a40a0", "#e07030", "#e0e0e0" };
            float crowd = Mathf.Clamp01(0.3f + 0.07f * lv);
            foreach (var (y0, h) in new[] { (38, 24), (64, 24) })
            {
                c.Box(2, y0 - 2, W - 4, 3, stoneL);                       // 처마돌
                for (int x = 6; x + 14 <= W - 4; x += 20)
                {
                    int cx = x + 7;
                    // 아치 안쪽 (어둡다) + 반원 머리
                    c.Rect(x, y0 + 1, 14, h - 8, dark);
                    c.Ellipse(cx, y0 + h - 8, 7, 6, dark, true);
                    c.Ellipse(cx, y0 + h - 8, 8, 7, stoneD, false);
                    c.VLine(x - 1, y0 + 1, h - 8, stoneD);
                    c.VLine(x + 14, y0 + 1, h - 8, stoneD);
                    // 기둥(아치 사이) 밝은 면
                    c.VLine(x + 17, y0, h, stoneL);
                    c.VLine(x + 18, y0, h, stone);
                    // 아치마다 관중 0~2명 (레벨이 오를수록 꽉 찬다)
                    for (int k = 0; k < 2; k++)
                    {
                        if (c.rng.NextDouble() > crowd) continue;
                        int px = x + 2 + k * 6;
                        bool cheer = lv >= 4 && c.rng.NextDouble() < 0.4;
                        c.Rect(px, y0 + 2, 4, 5, C(shirts[c.rng.Next(shirts.Length)]));
                        c.Rect(px, y0 + 7, 4, 4, C(heads[c.rng.Next(heads.Length)]));
                        c.Set(px + 1, y0 + 9, K); c.Set(px + 2, y0 + 9, K);
                        if (cheer) { c.Set(px - 1, y0 + 9, C(heads[0])); c.Set(px - 1, y0 + 10, C(heads[0])); c.Set(px + 4, y0 + 9, C(heads[0])); c.Set(px + 4, y0 + 10, C(heads[0])); }
                    }
                }
            }
            c.Box(2, 86, W - 4, 3, stoneL); // 맨 위 처마

            if (lv >= 8)
            {
                // 황제석: 가운데 위층 아치를 붉은 휘장과 금테로
                c.Box(64, 63, 32, 26, Gold);
                c.Rect(66, 65, 28, 20, C("#7a1424"));
                for (int x = 66; x < 94; x += 4) c.VLine(x, 65, 20, C("#9a2434"));
                c.Stamp(new[] { "Y.Y.Y", "YYYYY" }, "", 78, 80);                  // 왕관
                c.Rect(76, 67, 8, 6, C("#5a2a8a")); c.Rect(77, 73, 6, 5, C("#f0c8a0")); // 황제
            }

            // 경기장 벽 (돌 블록) + 붉은 띠(프리즈)
            c.Bricks(2, FloorH, W - 4, 24, C("#a88a62"), C("#7a6244"), 20, 8);
            c.Box(2, 32, W - 4, 5, lv >= 6 ? Gold : C("#8a2a2a"));
            for (int x = 6; x < W - 4; x += 8) c.Set(x, 34, lv >= 6 ? GoldL : C("#d8a848"));
            // 가운데 방패 문장 (교차한 칼)
            c.Stamp(new[]
            {
                ".KKKKKKK.",
                "KrrrrrrrK",
                "KrWrrrWrK",
                "KrrWrWrrK",
                "KrrrWrrrK",
                "KrrWrWrrK",
                ".KWrrrWK.",
                "..KrrrK..",
                "...KKK...",
            }, lv >= 10 ? "r=#d8a848" : "r=#9a2a2a", 76, 17);

            // 모래 바닥 + 발자국
            c.Rect(2, 0, W - 4, FloorH, C("#d8b878"));
            for (int i = 0; i < 140; i++) c.Set(c.rng.Next(2, W - 2), c.rng.Next(0, FloorH), c.rng.Next(2) == 0 ? C("#b8985a") : C("#ecd4a0"));
            for (int x = 30; x < 130; x += 9) { c.Set(x, 3 + (x / 9 % 2) * 3, C("#a8884a")); c.Set(x + 1, 3 + (x / 9 % 2) * 3, C("#a8884a")); }

            if (lv >= 2) { c.Stamp(Torch, TorchPal, 22, 37); c.Stamp(Torch, TorchPal, 134, 37); }
            if (lv >= 5)
            {
                // 무기 거치대: 칼·창
                c.Box(4, 26, 14, 3, C("#5a3a22"));
                c.Stamp(new[] { ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", "YYY", ".b.", ".b." }, "b=#6a4020", 5, 13);
                c.Stamp(new[] { ".W.", "WWW", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b." }, "b=#6a4020", 11, 13);
                c.Box(142, 26, 14, 3, C("#5a3a22"));
                c.Stamp(new[] { ".W.", ".W.", ".W.", ".W.", ".W.", ".W.", "YYY", ".b.", ".b." }, "b=#6a4020", 144, 13);
                c.Stamp(new[] { ".W.", "WWW", ".b.", ".b.", ".b.", ".b.", ".b.", ".b.", ".b." }, "b=#6a4020", 150, 13);
            }
            if (lv >= 10)
                c.Stamp(new[] { "KYYYYYK", "YKYYYKY", "Y.YYY.Y", "..KYK..", "...Y...", "..KYK..", ".KYYYK." }, "", 106, 37); // 황금 트로피

            Shell(c, C("#7a6244"));
            if (lv >= 7)
            {
                // 차양(벨라리움): 천장 아래 붉은·흰 줄무늬 삼각 천
                for (int x = 2; x < W - 2; x += 10)
                {
                    var col = C((x / 10) % 2 == 0 ? "#b83040" : "#f0e8e0");
                    for (int j = 0; j < 5; j++) c.HLine(x + j, 90 - j, 10 - j * 2, col);
                }
            }
            if (lv >= 3) { Banner(c, 26, 88, 18, "#8a1a2a"); Banner(c, 126, 88, 18, "#5a2a8a"); }
            if (lv >= 9) Bunting(c, 4, W - 6, 59);
            if (lv >= 6) GoldTrim(c);
            if (lv >= 10) Sparkles(c, 20, 40, 120, 40, 6);
            return c;
        }

        // Lv.1 = 기본 이름(room_x), Lv.2부터 room_x_{레벨}
        static IEnumerable<(string, Canvas)> Levels(string name, int max, Func<int, Canvas> paint)
        {
            yield return (name, paint(1));
            for (int lv = 2; lv <= max; lv++) yield return ($"{name}_{lv}", paint(lv));
        }

        public static IEnumerable<(string name, Canvas canvas)> All()
        {
            yield return ("room_empty", Empty());
            yield return ("room_sealed", Sealed());
            yield return ("room_tank_front", TankFront());
            yield return ("room_rest", Rest());
            foreach (var r in Levels("room_tank", 5, Tank)) yield return r;
            foreach (var r in Levels("room_restaurant", 10, Restaurant)) yield return r;
            foreach (var r in Levels("room_souvenir", 10, Souvenir)) yield return r;
            foreach (var r in Levels("room_lab", 5, Lab)) yield return r;
            foreach (var r in Levels("room_dorm", 5, Dorm)) yield return r;
            foreach (var r in Levels("room_arena", 10, Arena)) yield return r;
        }
    }
}

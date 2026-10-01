using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mawang.EditorTools
{
    // 플레이 스토어용 그림: 앱 아이콘(512), 피처 그래픽(1024×500, 한/영).
    // 게임과 같은 도트 규칙(1px 외곽선, 정수 배율, 제한된 팔레트)으로 코드로 그린 뒤 정수배 확대한다.
    // 결과: <프로젝트>/StoreAssets/ (에셋 아님). 앱 아이콘은 Assets/Icons/ 에도 복사해 빌드 아이콘으로 쓴다.
    public static class StoreArt
    {
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../StoreAssets"));
        const string IconAsset = "Assets/Icons/app_icon.png";

        [MenuItem("Mawang/Store/Make Icon + Feature Graphic")]
        public static void MakeAll()
        {
            Directory.CreateDirectory(OutDir);
            var icon = Icon();
            Save(icon.Scale(8), Path.Combine(OutDir, "icon_512.png"));
            Directory.CreateDirectory("Assets/Icons");
            Save(icon.Scale(8), IconAsset);
            // 안드로이드 적응형 아이콘: 108 도트 판(가운데 72만 보인다)에 64 아이콘을 두고 가장자리는 늘여 채운다 → ×4 = 432
            Save(Adaptive(icon).Scale(4), AdaptiveBg);
            var fg = new Texture2D(432, 432, TextureFormat.RGBA32, false);
            fg.SetPixels32(new Color32[432 * 432]);
            fg.Apply();
            Save(fg, AdaptiveFg);
            foreach (var path in new[] { AdaptiveBg, AdaptiveFg }) { AssetDatabase.ImportAsset(path); Configure(path); }
            Save(Feature(false).Scale(3, 1024, 500), Path.Combine(OutDir, "feature_ko_1024x500.png"));
            Save(Feature(true).Scale(3, 1024, 500), Path.Combine(OutDir, "feature_en_1024x500.png"));
            AssetDatabase.ImportAsset(IconAsset);
            Configure(IconAsset);
            Debug.Log($"[StoreArt] 아이콘·피처 그래픽 → {OutDir}");
        }

        public const string AdaptiveBg = "Assets/Icons/app_icon_adaptive_bg.png", AdaptiveFg = "Assets/Icons/app_icon_adaptive_fg.png";

        static void Configure(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.isReadable = true;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
        }

        static Pix Adaptive(Pix icon)
        {
            const int S = 108, O = (S - 64) / 2;
            var p = new Pix(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    p.Set(x, y, icon.Get(Mathf.Clamp(x - O, 0, 63), Mathf.Clamp(y - O, 0, 63)));
            Stars(p, 14, 21, 90);
            p.Blit(icon, O, O);
            return p;
        }

        // ── 도트 캔버스 ────────────────────────────────────────
        public class Pix
        {
            public readonly int W, H;
            public readonly Color32[] px;
            public Pix(int w, int h) { W = w; H = h; px = new Color32[w * h]; }

            public Color32 Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? px[y * W + x] : default;
            public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }

            // 반투명 덧칠
            public void Blend(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || c.a == 0) return;
                if (c.a == 255) { px[y * W + x] = c; return; }
                var d = px[y * W + x];
                float a = c.a / 255f;
                px[y * W + x] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a), (byte)Mathf.Max(d.a, c.a));
            }

            public void Rect(int x, int y, int w, int h, Color32 c) { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Blend(i, j, c); }

            public void Disc(float cx, float cy, float r, Color32 c)
            {
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                    for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                        if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) Blend(x, y, c);
            }

            public void Tri(int x0, int x1, int yBase, int yTop, Color32 c) // 이등변 삼각형 지붕
            {
                for (int y = yBase; y <= yTop; y++)
                {
                    float t = (y - yBase) / (float)Mathf.Max(1, yTop - yBase);
                    float half = (x1 - x0 + 1) / 2f * (1f - t);
                    float mid = (x0 + x1 + 1) / 2f;
                    for (int x = Mathf.RoundToInt(mid - half); x < Mathf.RoundToInt(mid + half); x++) Set(x, y, c);
                }
            }

            // 다른 도트 그림을 정수배로 붙인다 (flip = 좌우 반전)
            public void Blit(Pix s, int x, int y, int scale = 1, bool flip = false)
            {
                for (int j = 0; j < s.H; j++)
                    for (int i = 0; i < s.W; i++)
                    {
                        var c = s.px[j * s.W + (flip ? s.W - 1 - i : i)];
                        if (c.a == 0) continue;
                        for (int b = 0; b < scale; b++) for (int a = 0; a < scale; a++) Blend(x + i * scale + a, y + j * scale + b, c);
                    }
            }

            // 불투명 영역 바깥에 1px 테두리
            public Pix Outlined(Color32 k, int thick = 1)
            {
                var o = new Pix(W + thick * 2, H + thick * 2);
                for (int y = 0; y < o.H; y++)
                    for (int x = 0; x < o.W; x++)
                    {
                        bool near = false;
                        for (int dy = -thick; dy <= thick && !near; dy++)
                            for (int dx = -thick; dx <= thick && !near; dx++)
                                if (Get(x - thick + dx, y - thick + dy).a > 0) near = true;
                        if (near) o.Set(x, y, k);
                    }
                o.Blit(this, thick, thick);
                return o;
            }

            // 정수배 확대 (w,h 를 주면 가운데를 잘라 그 크기로)
            public Texture2D Scale(int s, int w = -1, int h = -1)
            {
                if (w < 0) { w = W * s; h = H * s; }
                int ox = (W * s - w) / 2, oy = (H * s - h) / 2;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var o = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        o[y * w + x] = Get((x + ox) / s, (y + oy) / s);
                t.SetPixels32(o);
                t.Apply();
                return t;
            }
        }

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color32 K = C("#1b1022");

        static void Save(Texture2D t, string path)
        {
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        // 구운 게임 스프라이트(Resources/Sprites)를 읽는다
        static Pix Sprite(string name)
        {
            var bytes = File.ReadAllBytes($"Assets/Resources/Sprites/{name}.png");
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(bytes);
            var p = new Pix(t.width, t.height);
            var src = t.GetPixels32();
            Array.Copy(src, p.px, src.Length);
            UnityEngine.Object.DestroyImmediate(t);
            return p;
        }

        static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        // 밤하늘: 위→아래 그라데이션을 4px 띠 + 체커 디더로
        static void Sky(Pix p, Color32 top, Color32 bottom, int bands)
        {
            for (int y = 0; y < p.H; y++)
            {
                float t = 1f - y / (float)(p.H - 1);
                float f = t * bands;
                int band = Mathf.FloorToInt(f);
                float frac = f - band;
                for (int x = 0; x < p.W; x++)
                {
                    int b = band + (frac > 0.5f && (x + y) % 2 == 0 ? 1 : 0);
                    p.Set(x, y, Lerp(top, bottom, Mathf.Clamp01(b / (float)bands)));
                }
            }
        }

        static void Stars(Pix p, int n, int seed, int minY)
        {
            var r = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                int x = r.Next(p.W), y = r.Next(minY, p.H);
                bool big = r.NextDouble() < 0.25;
                var c = r.NextDouble() < 0.3 ? C("#ffe6a0") : C("#e8e0ff");
                p.Set(x, y, c);
                if (big) { p.Set(x + 1, y, Lerp(c, K, 0.5f)); p.Set(x - 1, y, Lerp(c, K, 0.5f)); p.Set(x, y + 1, Lerp(c, K, 0.5f)); p.Set(x, y - 1, Lerp(c, K, 0.5f)); }
            }
        }

        static void Moon(Pix p, int cx, int cy, int r)
        {
            p.Disc(cx, cy, r + 3, new Color32(255, 230, 170, 40));  // 달무리
            p.Disc(cx, cy, r + 1.5f, new Color32(255, 230, 170, 60));
            p.Disc(cx, cy, r, C("#f4e8c0"));
            p.Disc(cx + r * 0.35f, cy + r * 0.3f, r * 0.9f, C("#fff8e0")); // 밝은 면
            p.Disc(cx - r * 0.35f, cy - r * 0.25f, r * 0.22f, C("#d8c898"));
            p.Disc(cx + r * 0.2f, cy - r * 0.5f, r * 0.14f, C("#d8c898"));
            p.Disc(cx - r * 0.5f, cy + r * 0.35f, r * 0.12f, C("#e0d0a8"));
        }

        // ── 앱 아이콘 64×64 → 512 ──────────────────────────────
        // 주인공(새끼 드래곤)이 앞에서 손님을 맞고, 뒤로 불 켜진 마왕성 + 큰 달.
        static Pix Icon()
        {
            var p = new Pix(64, 64);
            Sky(p, C("#1c1236"), C("#6a2e6e"), 7);
            Stars(p, 18, 5, 30);
            Moon(p, 47, 49, 9);

            // 먼 산 실루엣
            for (int x = 0; x < 64; x++)
            {
                int h = 18 + Mathf.RoundToInt(Mathf.Sin(x * 0.19f) * 3 + Mathf.Sin(x * 0.07f + 1) * 4);
                for (int y = 0; y < h; y++) p.Set(x, y, C("#3a2048"));
            }

            Castle(p, 8, 14);

            // 땅
            for (int x = 0; x < 64; x++)
            {
                int h = 10 + Mathf.RoundToInt(Mathf.Sin(x * 0.12f + 0.5f) * 1.5f);
                for (int y = 0; y < h; y++) p.Set(x, y, y == h - 1 ? C("#5a8a3a") : y == h - 2 ? C("#3e6a2e") : C("#2e3a28"));
            }

            // 성 앞 괴물 무리(작게 ×2, 앞줄·뒷줄) + 성을 찾아온 용사. 게임 스프라이트는 이미 외곽선이 있다
            p.Blit(Sprite("m_mushroom"), 9, 9, 1);           // 뒷줄 (작게)
            p.Blit(Sprite("m_werewolf"), 33, 9, 1, true);
            p.Blit(Sprite("p_toadstool"), 55, 9, 1, true);
            p.Blit(Sprite("m_slime"), 1, 3, 2);              // 앞줄
            p.Blit(Sprite("m_drake"), 19, 3, 2);
            p.Blit(Sprite("m_imp"), 43, 3, 2, true);
            p.Blit(Sprite("m_bat"), 5, 34, 2);               // 하늘
            p.Blit(Sprite("m_bat"), 52, 38, 1, true);
            p.Blit(Sprite("hero_warrior"), 55, 13, 1, true); // 멀리서 오는 손님
            // 반짝이
            foreach (var (x, y) in new[] { (40, 30), (58, 30), (8, 40) })
            {
                p.Set(x, y, C("#ffe6a0")); p.Set(x + 1, y, C("#ffe6a0")); p.Set(x - 1, y, C("#ffe6a0")); p.Set(x, y + 1, C("#ffe6a0")); p.Set(x, y - 1, C("#ffe6a0"));
            }
            return p;
        }

        // 마왕성 실루엣 (게임 속 성과 같은 돌색·붉은 지붕·노란 창)
        static void Castle(Pix p, int x0, int y0)
        {
            var stone = C("#7a6282"); var stoneD = C("#58465e"); var stoneL = C("#9a82a0");
            var roof = C("#c83848"); var roofD = C("#8a2030"); var win = C("#ffd040");
            var c = new Pix(48, 42);

            void Block(int x, int y, int w, int h)
            {
                c.Rect(x, y, w, h, stone);
                c.Rect(x, y, 1, h, stoneL);            // 왼쪽 빛
                c.Rect(x + w - 2, y, 2, h, stoneD);     // 오른쪽 그늘
                for (int by = y + 2; by < y + h; by += 3)  // 돌 줄눈
                    for (int bx = x + 1 + (by / 3 % 2) * 2; bx < x + w - 2; bx += 4) c.Set(bx, by, stoneD);
                for (int i = x; i < x + w; i += 2) c.Set(i, y + h, stone); // 성가퀴
            }
            void Window(int x, int y) { c.Rect(x, y, 2, 3, win); c.Set(x, y + 3, C("#ffe890")); }

            Block(6, 0, 36, 18);       // 본관
            Block(0, 0, 9, 28);        // 왼쪽 탑
            Block(39, 0, 9, 28);       // 오른쪽 탑
            Block(17, 12, 14, 18);     // 가운데 탑
            c.Tri(-1, 9, 28, 36, roof); c.Tri(-1, 4, 28, 33, roofD);
            c.Tri(38, 48, 28, 36, roof); c.Tri(38, 43, 28, 33, roofD);
            c.Tri(15, 32, 30, 41, roof); c.Tri(15, 23, 30, 37, roofD);
            c.Set(23, 41, C("#ffd040")); c.Set(24, 41, C("#ffd040")); // 깃대 끝
            // 문
            c.Rect(20, 0, 8, 8, C("#2a1a22")); c.Disc(24, 8, 4, C("#2a1a22"));
            c.Rect(21, 0, 6, 7, C("#6a3a24")); c.Rect(24, 0, 1, 7, C("#3a2014"));
            // 창
            Window(3, 18); Window(42, 18); Window(3, 10); Window(42, 10);
            Window(21, 20); Window(25, 20); Window(11, 9); Window(35, 9);
            p.Blit(c.Outlined(K), x0 - 1, y0 - 1);
        }

        // ── 피처 그래픽 342×167 (×3 → 1024×500) ───────────────
        // 인형의 집처럼 자른 마왕성 단면(게임 속 방 그림) 위에 로고 판.
        static Pix Feature(bool en)
        {
            var p = new Pix(342, 167);
            Sky(p, C("#1c1236"), C("#4a2458"), 8);
            Stars(p, 60, 11, 60);
            Moon(p, 300, 140, 14);

            // 방 두 줄: 아래 줄은 조금 잘리고, 가운데 엘리베이터
            const int RoomW = 160, RoomH = 96, Slab = 6;
            int ex = (342 - 32) / 2;                  // 엘리베이터 왼쪽
            int lx = ex - RoomW, rx = ex + 32;
            int row0 = -40, row1 = row0 + RoomH + Slab;
            var slab = Sprite("slab");
            void SlabRow(int y) { for (int x = lx - 8; x < rx + RoomW + 8; x += slab.W) p.Blit(slab, x, y); }
            SlabRow(row0 - Slab); SlabRow(row1 - Slab); SlabRow(row1 + RoomH);

            p.Blit(Sprite("room_tank"), lx, row1); p.Blit(Sprite("room_restaurant"), rx, row1);
            p.Blit(Sprite("room_lab"), lx, row0); p.Blit(Sprite("room_souvenir"), rx, row0);
            // 엘리베이터 통로
            p.Rect(ex, row0 - Slab, 32, RoomH * 2 + Slab * 3, C("#140e1a"));
            p.Rect(ex + 5, row0 - Slab, 1, RoomH * 2 + Slab * 3, C("#3a3044"));
            p.Rect(ex + 26, row0 - Slab, 1, RoomH * 2 + Slab * 3, C("#3a3044"));
            var elev = Sprite("elevator");
            p.Blit(elev, ex + (32 - elev.W) / 2, row1); p.Blit(elev, ex + (32 - elev.W) / 2, row0);
            // 성가퀴
            var bat = Sprite("battlement");
            for (int x = lx - 8; x < rx + RoomW + 8; x += bat.W) p.Blit(bat, x, row1 + RoomH + Slab);

            // 전시 우리 속 괴물들 + 창살
            int foot = row1 + 4;
            p.Blit(Sprite("m_slime"), lx + 18, foot); p.Blit(Sprite("m_drake"), lx + 46, foot);
            p.Blit(Sprite("m_mushroom"), lx + 80, foot, 1, true); p.Blit(Sprite("m_bat"), lx + 104, foot + 20);
            p.Blit(Sprite("m_werewolf"), lx + 126, foot, 1, true); p.Blit(Sprite("p_toadstool"), lx + 64, foot);
            p.Blit(Sprite("room_tank_front"), lx, row1);
            // 구경하는 용사들
            p.Blit(Sprite("hero_mage"), lx + 34, foot); p.Blit(Sprite("hero_archer"), lx + 96, foot, 1, true);
            // 식당: 직원과 손님
            p.Blit(Sprite("castle_succ"), rx + 40, row1 + 4); p.Blit(Sprite("hero_warrior"), rx + 60, row1 + 4, 1, true);
            p.Blit(Sprite("hero_cleric"), rx + 112, row1 + 4);
            // 위층: 연구소 마녀, 기념품점
            p.Blit(Sprite("castle_witch"), lx + 60, row0 + RoomH - 30 < 0 ? row0 + 4 : row0 + 4);
            p.Blit(Sprite("castle_garg"), rx + 30, row0 + 4);
            p.Blit(Sprite("castle_skel"), ex + 12, row1 + 4);

            // 로고 판: 어두운 판 + 금 테두리, 제목(2배) + 부제
            string title = en ? "Demon Crossing" : "놀러와요 마왕의 성";
            string sub = en ? "Monster Zoo Management Sim" : "몬스터 동물원 경영 시뮬레이션";
            var t = TextPix(title, "Galmuri11-Bold", 12, C("#ffd24a"));
            var titleArt = Logo(t);
            var s = TextPix(sub, "Galmuri11", 12, C("#f4e8d8"));
            int boardW = Mathf.Max(titleArt.W, s.W) + 26, boardH = titleArt.H + s.H + 18;
            int bx = (p.W - boardW) / 2, by = 64;
            p.Rect(bx, by, boardW, boardH, new Color32(20, 12, 28, 225));
            Frame(p, bx, by, boardW, boardH);
            p.Blit(titleArt, (p.W - titleArt.W) / 2, by + s.H + 10);
            p.Blit(s.Outlined(K), (p.W - s.W - 2) / 2, by + 5);
            return p;
        }

        // 금 테두리 (게임 창과 같은 모양)
        static void Frame(Pix p, int x, int y, int w, int h)
        {
            var gold = C("#d8a848"); var goldD = C("#8a5a24"); var goldL = C("#f4d488");
            p.Rect(x - 1, y - 1, w + 2, 1, K); p.Rect(x - 1, y + h, w + 2, 1, K);
            p.Rect(x - 1, y, 1, h, K); p.Rect(x + w, y, 1, h, K);
            p.Rect(x, y, w, 1, goldD); p.Rect(x, y + h - 1, w, 1, goldL);
            p.Rect(x, y, 1, h, gold); p.Rect(x + w - 1, y, 1, h, goldD);
            p.Rect(x + 1, y + 1, w - 2, 1, gold); p.Rect(x + 1, y + h - 2, w - 2, 1, gold);
            foreach (var (cx, cy) in new[] { (x + 3, y + 3), (x + w - 4, y + 3), (x + 3, y + h - 4), (x + w - 4, y + h - 4) }) p.Set(cx, cy, goldL);
        }

        // 제목 글자: 2배 → 위쪽 밝은 금색 그라데이션 → 두꺼운 외곽선 → 아래 그림자
        static Pix Logo(Pix text)
        {
            var big = new Pix(text.W * 2, text.H * 2);
            big.Blit(text, 0, 0, 2);
            for (int y = 0; y < big.H; y++)
                for (int x = 0; x < big.W; x++)
                {
                    var c = big.Get(x, y);
                    if (c.a == 0) continue;
                    float t = y / (float)big.H;
                    big.Set(x, y, t > 0.55f ? C("#fff0a0") : t > 0.3f ? C("#ffd24a") : C("#f0a030"));
                }
            var o1 = big.Outlined(C("#5a1a2a"));
            var o2 = o1.Outlined(K);
            var art = new Pix(o2.W + 2, o2.H + 2);
            // 그림자
            for (int y = 0; y < o2.H; y++) for (int x = 0; x < o2.W; x++) if (o2.Get(x, y).a > 0) art.Set(x + 2, y, K);
            art.Blit(o2, 0, 2);
            return art;
        }

        // 도트 폰트를 원래 크기로 찍어 픽셀 마스크로 (글자 = color, 나머지 투명)
        static Pix TextPix(string text, string fontName, int size, Color32 color)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>($"Assets/Resources/Fonts/{fontName}.ttf");
            var go = new GameObject("StoreText") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var canvas = go.AddComponent<UnityEngine.Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform;
                var tgo = new GameObject("T", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                tgo.transform.SetParent(go.transform, false);
                var txt = tgo.AddComponent<Text>();
                txt.font = font; txt.fontSize = size; txt.text = text; txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow;
                int w = Mathf.CeilToInt(txt.preferredWidth) + 4, h = size + 6;
                rt.sizeDelta = new Vector2(w, h);
                ((RectTransform)tgo.transform).sizeDelta = new Vector2(w, h);
                go.transform.position = new Vector3(10000, 10000, 0);

                var camGo = new GameObject("StoreCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = h / 2f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.transform.position = new Vector3(10000, 10000, -10);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 100;
                var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rtex;
                canvas.worldCamera = cam;
                UnityEngine.Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rtex;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var src = tex.GetPixels32();
                // 글자 영역만 잘라낸다
                int minX = w, maxX = -1, minY = h, maxY = -1;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (src[y * w + x].a > 110) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                var p = new Pix(Mathf.Max(1, maxX - minX + 1), Mathf.Max(1, maxY - minY + 1));
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) if (src[y * w + x].a > 110) p.Set(x - minX, y - minY, color);
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rtex);
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(camGo);
                return p;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}

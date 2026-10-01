using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang.EditorTools
{
    // UI 도트: 9-slice 창/버튼 테두리와 16×16 아이콘을 코드로 그린다.
    // 아이콘은 도형 마스크를 칠하면 자동으로 외곽선(K) + 좌상단 하이라이트 + 우하단 그림자가 들어간다.
    // 좌표는 왼쪽 아래 (0,0), 픽셀 중심 = (x+0.5, y+0.5).
    public class UISprite
    {
        public string name;
        public Texture2D tex;
        public Vector4 border; // 9-slice (L, B, R, T)
    }

    public class Mask
    {
        public readonly int W, H;
        public readonly bool[] m;

        public Mask(int w = 16, int h = 16) { W = w; H = h; m = new bool[w * h]; }

        public bool this[int x, int y] => x >= 0 && y >= 0 && x < W && y < H && m[y * W + x];

        Mask Each(Func<float, float, bool> f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (f(x + 0.5f, y + 0.5f)) m[y * W + x] = true;
            return this;
        }

        public Mask Rect(int x0, int y0, int x1, int y1) =>
            Each((x, y) => x >= x0 && x <= x1 + 1 && y >= y0 && y <= y1 + 1);

        public Mask Disc(float cx, float cy, float r) =>
            Each((x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r);

        public Mask Ellipse(float cx, float cy, float rx, float ry) =>
            Each((x, y) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1f);

        public Mask Line(float x0, float y0, float x1, float y1, float width) =>
            Each((x, y) => SegDist(x, y, x0, y0, x1, y1) <= width * 0.5f);

        public Mask Poly(params float[] p) => Each((x, y) => Inside(x, y, p));

        public Mask Minus(Mask o) { for (int i = 0; i < m.Length; i++) if (o.m[i]) m[i] = false; return this; }
        public Mask And(Mask o) { for (int i = 0; i < m.Length; i++) m[i] &= o.m[i]; return this; }

        static float SegDist(float px, float py, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / Mathf.Max(0.0001f, dx * dx + dy * dy));
            float ex = x0 + t * dx - px, ey = y0 + t * dy - py;
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        static bool Inside(float x, float y, float[] p)
        {
            bool c = false;
            int n = p.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }
    }

    public class Ico
    {
        public readonly int W, H;
        readonly Color32[] px;
        public static readonly Color32 K = Canvas.K;
        static readonly Color32 Warm = Canvas.C("#fff4d8");
        public static readonly Color32 W8 = Canvas.C("#f4f0e8");

        public Ico(int w = 16, int h = 16) { W = w; H = h; px = new Color32[w * h]; }

        public static Mask M => new Mask();
        public static Color32 C(string hex) => Canvas.C(hex);
        public static Color32 Light(Color32 c, float t = 0.38f) => Color32.Lerp(c, Warm, t);
        public static Color32 Dark(Color32 c, float t = 0.38f) => Color32.Lerp(c, K, t);

        public Color32 Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? px[y * W + x] : default;
        public void Dot(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }

        static bool Edge(Mask m, int x, int y) => !m[x + 1, y] || !m[x - 1, y] || !m[x, y + 1] || !m[x, y - 1];

        // 외곽선 + 입체 음영
        public Ico Paint(Mask m, Color32 c, bool shade = true)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!m[x, y]) continue;
                    if (Edge(m, x, y)) { px[y * W + x] = K; continue; }
                    var col = c;
                    if (shade)
                    {
                        bool lit = Edge(m, x, y + 1) || Edge(m, x - 1, y);
                        bool dim = Edge(m, x, y - 1) || Edge(m, x + 1, y);
                        if (lit) col = Light(c);
                        else if (dim) col = Dark(c, 0.28f);
                    }
                    px[y * W + x] = col;
                }
            return this;
        }

        // 외곽선 위는 건드리지 않고 칠한다(무늬, 액체, 면 분할)
        public Ico Flat(Mask m, Color32 c)
        {
            for (int i = 0; i < px.Length; i++)
                if (m.m[i] && px[i].a > 0 && !Same(px[i], K)) px[i] = c;
            return this;
        }

        // 외곽선 없이 그대로 칠한다(이펙트용)
        public Ico Fill(Mask m, Color32 c)
        {
            for (int i = 0; i < px.Length; i++) if (m.m[i]) px[i] = c;
            return this;
        }

        public Ico Checker(Mask m, Color32 c)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (m[x, y] && (x + y) % 2 == 0 && px[y * W + x].a > 0 && !Same(px[y * W + x], K)) px[y * W + x] = c;
            return this;
        }

        public Ico Dots(Color32 c, params int[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) Dot(xy[i], xy[i + 1], c);
            return this;
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public Texture2D ToTexture()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }

    public static class UIPainter
    {
        static Color32 C(string h) => Canvas.C(h);
        static Mask M => new Mask();

        // ── 9-slice 틀 ─────────────────────────────────────────
        // 층층이 테두리: 바깥부터 색을 한 겹씩 두르고 안쪽을 채운다. top/bottom 은 윗면 밝기/아랫면 두께 조절.
        static Ico Frame(int w, int h, Color32 fill, params (Color32 c, int t)[] rings)
        {
            var ic = new Ico(w, h);
            int inset = 0;
            foreach (var (c, t) in rings)
            {
                for (int k = 0; k < t; k++, inset++)
                    for (int y = inset; y < h - inset; y++)
                        for (int x = inset; x < w - inset; x++)
                            if (x == inset || y == inset || x == w - 1 - inset || y == h - 1 - inset)
                                ic.Dot(x, y, c);
            }
            for (int y = inset; y < h - inset; y++)
                for (int x = inset; x < w - inset; x++)
                    ic.Dot(x, y, fill);
            // 모서리 1px 깎기(둥근 도트 느낌)
            ic.Dot(0, 0, default); ic.Dot(w - 1, 0, default); ic.Dot(0, h - 1, default); ic.Dot(w - 1, h - 1, default);
            return ic;
        }

        static UISprite S(string name, Ico ic, int l, int b, int r, int t) =>
            new UISprite { name = name, tex = ic.ToTexture(), border = new Vector4(l, b, r, t) };

        // 입체 버튼: 외곽선, 윗줄 하이라이트, 아래 2px 입술(눌리면 사라짐)
        static UISprite Button(string name, Color32 baseC, bool down)
        {
            const int w = 12, h = 12;
            var ic = new Ico(w, h);
            var K = Ico.K;
            var hi = Ico.Light(baseC, 0.45f);
            var lip = Ico.Dark(baseC, 0.45f);
            int lipH = down ? 0 : 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    Color32 c;
                    if (border) c = K;
                    else if (y <= lipH) c = lip;                    // 아래 입술
                    else if (y == h - 2) c = down ? Ico.Dark(baseC, 0.25f) : hi; // 윗줄
                    else if (x == 1 && !down) c = Ico.Light(baseC, 0.2f);
                    else c = baseC;
                    ic.Dot(x, y, c);
                }
            if (!down) { ic.Dot(2, h - 3, hi); } // 반짝이는 점
            ic.Dot(0, 0, default); ic.Dot(w - 1, 0, default); ic.Dot(0, h - 1, default); ic.Dot(w - 1, h - 1, default);
            return S(name, ic, 4, 4, 4, 4);
        }

        static UISprite Window()
        {
            // 금 테두리 + 어두운 철판 + 리벳
            var gold = C("#d8a848"); var goldD = C("#8a5a24"); var iron = C("#3b2d45"); var ironD = C("#2a1f33");
            var ic = Frame(24, 24, C("#1e1726"), (Ico.K, 1), (gold, 1), (goldD, 1), (iron, 3), (Ico.K, 1));
            // 철판 윗면 밝게, 아랫면 어둡게
            for (int x = 3; x < 21; x++) { ic.Dot(x, 20, Ico.Light(iron, 0.15f)); ic.Dot(x, 3, ironD); }
            // 금테 하이라이트
            for (int x = 2; x < 22; x++) ic.Dot(x, 22, Ico.Light(gold, 0.4f));
            // 네 모서리 리벳
            foreach (var (x, y) in new[] { (4, 4), (19, 4), (4, 19), (19, 19) })
            {
                ic.Dot(x, y, gold); ic.Dot(x, y + 0, gold);
            }
            return S("ui_window", ic, 8, 8, 8, 8);
        }

        public static IEnumerable<UISprite> Frames()
        {
            yield return Window();

            // 보조 패널(사이드바 섹션, 카드)
            var p = Frame(12, 12, C("#2a2034"), (Ico.K, 1), (C("#4d3d5e"), 1));
            for (int x = 2; x < 10; x++) p.Dot(x, 9, C("#352a42"));
            yield return S("ui_panel", p, 3, 3, 3, 3);

            // 목록 행 카드
            var row = Frame(10, 10, C("#2f2540"), (C("#140e1a"), 1), (C("#43365a"), 1));
            yield return S("ui_row", row, 3, 3, 3, 3);

            // 움푹 들어간 슬롯(재화 표시, 진행 바 배경, 아이콘 칸)
            var inset = new Ico(10, 10);
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                {
                    bool border = x == 0 || y == 0 || x == 9 || y == 9;
                    Color32 c = border ? Ico.K : (y == 8 || x == 1) ? C("#0c0810") : (y == 1 || x == 8) ? C("#3e3150") : C("#17111e");
                    inset.Dot(x, y, c);
                }
            inset.Dot(0, 0, default); inset.Dot(9, 0, default); inset.Dot(0, 9, default); inset.Dot(9, 9, default);
            yield return S("ui_inset", inset, 3, 3, 3, 3);

            // 제목 리본
            var gold = C("#d8a848");
            var title = Frame(16, 12, C("#5a2a3a"), (Ico.K, 1), (gold, 1), (C("#7a3a4a"), 1));
            for (int x = 3; x < 13; x++) { title.Dot(x, 8, C("#8a4a5a")); title.Dot(x, 3, C("#4a2230")); }
            yield return S("ui_title", title, 5, 4, 5, 4);

            // 툴팁
            var tip = Frame(10, 10, C("#f0e2c0"), (Ico.K, 1), (C("#a8845a"), 1));
            yield return S("ui_tip", tip, 3, 3, 3, 3);

            // 둥근 배지(단축키, 개수)
            var badge = new Ico(8, 8);
            badge.Paint(new Mask(8, 8).Disc(4, 4, 4), C("#c83848"));
            yield return S("ui_badge", badge, 3, 3, 3, 3);

            // 진행 바 채움(흰색 → 코드에서 색 지정), 윗줄 광택
            var fill = new Ico(4, 6);
            for (int y = 0; y < 6; y++) for (int x = 0; x < 4; x++) fill.Dot(x, y, y >= 4 ? new Color32(255, 255, 255, 255) : y == 0 ? new Color32(150, 150, 150, 255) : new Color32(215, 215, 215, 255));
            yield return S("ui_fill", fill, 1, 2, 1, 2);

            // 스크롤 핸들
            var handle = Frame(8, 12, C("#8a6aa8"), (Ico.K, 1), (C("#b898d0"), 1));
            yield return S("ui_handle", handle, 3, 4, 3, 4);

            // 단계 눈금(업그레이드 10단계)
            var pipOn = new Ico(6, 8); pipOn.Paint(new Mask(6, 8).Rect(0, 0, 5, 7), C("#f2c040"));
            yield return S("ui_pip_on", pipOn, 0, 0, 0, 0);
            var pipOff = new Ico(6, 8);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 6; x++) pipOff.Dot(x, y, x == 0 || y == 0 || x == 5 || y == 7 ? Ico.K : C("#2a2034"));
            yield return S("ui_pip_off", pipOff, 0, 0, 0, 0);
            var pipNext = new Ico(6, 8);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 6; x++) pipNext.Dot(x, y, x == 0 || y == 0 || x == 5 || y == 7 ? Ico.K : C("#6a5a3a"));
            yield return S("ui_pip_next", pipNext, 0, 0, 0, 0);

            // 버튼: 보라(기본) / 초록(확정) / 빨강(위험) / 회색(보조) / 금색(강조) / 비활성
            foreach (var (n, c) in new[] { ("p", "#7a4aa8"), ("g", "#3f9a54"), ("r", "#b83a44"), ("n", "#56506a"), ("y", "#d09a30"), ("d", "#3a3640") })
            {
                yield return Button($"ui_btn_{n}", C(c), false);
                yield return Button($"ui_btn_{n}_down", C(c), true);
            }
        }

        // ── 아이콘 16×16 ───────────────────────────────────────
        static readonly Color32 Wood = C("#b07038");
        static readonly Color32 GoldC = C("#f2b632");
        static readonly Color32 Steel = C("#9aa4b4");
        static readonly Color32 Paper = C("#efe4c8");

        static UISprite I(string name, Action<Ico> draw)
        {
            var ic = new Ico();
            draw(ic);
            return S(name, ic, 0, 0, 0, 0);
        }

        public static IEnumerable<UISprite> Icons()
        {
            // 재화
            yield return I("ic_gold", c => c
                .Paint(M.Disc(8, 8, 7.6f), GoldC)
                .Flat(M.Disc(8, 8, 4.6f).Minus(M.Disc(8, 8, 3.6f)), C("#c88418"))
                .Flat(M.Rect(7, 6, 8, 10), C("#c88418"))
                .Dots(Ico.W8, 4, 11, 5, 12, 4, 10));

            yield return I("ic_mat", c =>
            {
                var brick = C("#c8683a");
                c.Paint(M.Rect(0, 1, 5, 5), brick).Paint(M.Rect(5, 1, 10, 5), brick).Paint(M.Rect(10, 1, 15, 5), brick);
                c.Paint(M.Rect(2, 5, 8, 9), brick).Paint(M.Rect(8, 5, 13, 9), brick);
                c.Paint(M.Rect(5, 9, 10, 13), brick);
            });

            yield return I("ic_rp", c => c
                .Paint(M.Poly(8, 16, 15.5f, 8, 8, 0, 0.5f, 8), C("#5ad8f8"))
                .Flat(M.Poly(8, 14.5f, 14, 8, 8, 1.5f), C("#2c98d0"))
                .Flat(M.Poly(8, 14.5f, 9, 8, 8, 1.5f, 7, 8), C("#9af0ff"))
                .Dots(Ico.W8, 5, 9, 6, 10));

            // 메뉴
            yield return I("ic_build", c => c
                .Paint(M.Line(2, 2, 9.5f, 9.5f, 3.2f), Wood)
                .Paint(M.Poly(4.2f, 11.2f, 8.8f, 15.8f, 15.8f, 8.8f, 11.2f, 4.2f), Steel)
                .Dots(Ico.W8, 8, 13));

            yield return I("ic_hunt", c => c
                .Paint(M.Line(1.5f, 1.5f, 7.5f, 7.5f, 3.2f), Wood)
                .Paint(M.Disc(10.5f, 10.5f, 5.5f), C("#e8dcc0"))
                .Checker(M.Disc(10.5f, 10.5f, 4.2f), C("#8a7a60"))
                .Flat(M.Disc(10.5f, 10.5f, 4.6f).Minus(M.Disc(10.5f, 10.5f, 3.6f)), Wood));

            yield return I("ic_shop", c =>
            {
                c.Paint(M.Rect(2, 5, 3, 10), C("#6a4020")).Paint(M.Rect(12, 5, 13, 10), C("#6a4020"));
                c.Paint(M.Rect(1, 1, 14, 6), Wood);
                c.Paint(M.Poly(0, 9, 16, 9, 14, 15, 2, 15), C("#d04040"));
                c.Flat(M.Poly(4, 9.6f, 6.4f, 9.6f, 6.4f, 14.4f, 4.6f, 14.4f), C("#f0e8e0"))
                 .Flat(M.Poly(9.6f, 9.6f, 12, 9.6f, 11.4f, 14.4f, 9.6f, 14.4f), C("#f0e8e0"));
                c.Paint(M.Disc(6, 7.5f, 1.8f), GoldC).Paint(M.Disc(10, 7.5f, 1.8f), C("#6ab04a"));
            });

            yield return I("ic_bag", c => c
                .Paint(M.Rect(1, 1, 14, 8), C("#a86432"))
                .Paint(M.Rect(1, 8, 14, 12).Rect(2, 13, 13, 13), C("#bc7a40"))
                .Paint(M.Rect(3, 1, 5, 13), Steel).Paint(M.Rect(10, 1, 12, 13), Steel)
                .Paint(M.Rect(6, 6, 9, 10), GoldC)
                .Dots(Ico.K, 7, 7, 8, 7));

            yield return I("ic_research", c => c
                .Paint(M.Rect(3, 0, 14, 14), Paper)
                .Flat(M.Rect(13, 1, 13, 13), C("#c8b890"))
                .Paint(M.Rect(1, 1, 12, 15), C("#6a3aa8"))
                .Flat(M.Rect(2, 2, 3, 14), C("#4a2478"))
                .Paint(M.Disc(7.5f, 8.5f, 2.9f), GoldC)
                .Dots(C("#e03048"), 7, 8, 8, 8));

            yield return I("ic_book", c => c
                .Paint(M.Poly(0, 1, 8, 0, 16, 1, 16, 3, 0, 3), C("#6a3aa8"))
                .Paint(M.Poly(0, 3, 7.6f, 2, 7.6f, 14, 0, 15), Paper)
                .Paint(M.Poly(8.4f, 2, 16, 3, 16, 15, 8.4f, 14), Paper)
                .Flat(M.Rect(2, 10, 5, 10).Rect(2, 8, 5, 8).Rect(2, 6, 4, 6), C("#a09070"))
                .Paint(M.Ellipse(12, 7.5f, 2.8f, 2.3f), C("#6ee07a"))
                .Dots(Ico.K, 11, 7, 13, 7));

            yield return I("ic_save", c => c
                .Paint(M.Rect(2, 3, 13, 12), Paper)
                .Paint(M.Rect(0, 11, 15, 14), C("#d8c498"))
                .Paint(M.Rect(0, 1, 15, 4), C("#d8c498"))
                .Flat(M.Rect(4, 9, 11, 9).Rect(4, 7, 9, 7), C("#a89870"))
                .Paint(M.Disc(11.5f, 5.5f, 2.6f), C("#c83030")));

            yield return I("ic_reset", c =>
            {
                var ring = M.Disc(8, 8, 7.4f).Minus(M.Disc(8, 8, 3.8f)).Minus(M.Poly(8, 8, 16, 7, 16, 16, 10, 16));
                c.Paint(ring.Poly(9, 16, 9, 9.5f, 15.5f, 12.5f), C("#e8a040"));
            });

            yield return I("ic_close", c => c
                .Paint(M.Line(3, 3, 13, 13, 3.6f).Line(3, 13, 13, 3, 3.6f), C("#e04848")));

            yield return I("ic_check", c => c
                .Paint(M.Line(2, 8, 6, 3.5f, 3.4f).Line(6, 3.5f, 14, 12, 3.4f), C("#5ad060")));

            yield return I("ic_lock", c => c
                .Paint(M.Disc(8, 9.5f, 4.8f).Minus(M.Disc(8, 9.5f, 2.2f)).And(M.Rect(0, 8, 15, 15)), Steel)
                .Paint(M.Rect(2, 1, 13, 9), GoldC)
                .Dots(Ico.K, 7, 5, 8, 5, 7, 4, 8, 4, 7, 3));

            yield return I("ic_space", c => c
                .Paint(M.Rect(1, 1, 14, 14), C("#a8743a"))
                .Flat(M.Line(2.5f, 2.5f, 13.5f, 13.5f, 1.6f).Line(2.5f, 13.5f, 13.5f, 2.5f, 1.6f), C("#7a4e24")));

            yield return I("ic_star", c => c
                .Paint(M.Poly(8, 16, 10, 10.5f, 16, 10, 11.5f, 6.5f, 13, 0.5f, 8, 3.5f, 3, 0.5f, 4.5f, 6.5f, 0, 10, 6, 10.5f), GoldC));

            yield return I("ic_time", c => c
                .Paint(M.Disc(8, 8, 7.6f), C("#f0ece0"))
                .Flat(M.Rect(7, 8, 8, 12).Rect(8, 7, 11, 8), Ico.K)
                .Dots(C("#c83848"), 8, 14, 14, 8, 8, 1, 1, 8));

            // 포획 이펙트 (흰색 위주 → 코드에서 색을 입힌다)
            yield return I("fx_slash", c => c
                .Fill(M.Disc(7, 8, 7.5f).Minus(M.Disc(10, 7, 6.5f)), C("#b8c8ff"))
                .Fill(M.Disc(7, 8, 7.5f).Minus(M.Disc(8.4f, 7.7f, 7f)), C("#ffffff")));

            yield return I("fx_orb", c => c
                .Fill(M.Disc(8, 8, 6), new Color32(255, 255, 255, 110))
                .Fill(M.Disc(8, 8, 4.4f), C("#e8d0ff"))
                .Fill(M.Disc(8, 8, 2.6f), C("#ffffff")));

            yield return I("fx_net", c =>
            {
                var rope = C("#f0e4c8");
                c.Fill(M.Disc(8, 8, 7.5f).Minus(M.Disc(8, 8, 6.6f)), rope);
                for (int i = 2; i <= 14; i += 3) c.Fill(M.Line(i, 1, i, 15, 1).And(M.Disc(8, 8, 7)), rope).Fill(M.Line(1, i, 15, i, 1).And(M.Disc(8, 8, 7)), rope);
                c.Dots(C("#a88a60"), 8, 0, 0, 8, 15, 8, 8, 15);
            });

            yield return I("fx_cage", c => c
                .Paint(M.Rect(1, 0, 14, 2).Rect(1, 12, 14, 14).Rect(1, 2, 2, 12).Rect(13, 2, 14, 12).Rect(5, 2, 6, 12).Rect(9, 2, 10, 12), C("#d8a848"))
                .Paint(M.Rect(6, 14, 9, 15), C("#d8a848")));

            yield return I("fx_hit", c => c
                .Fill(M.Poly(8, 16, 9.5f, 10, 16, 12, 11, 7.5f, 14, 1, 8, 5, 2, 1, 5, 7.5f, 0, 12, 6.5f, 10), C("#fff4a0"))
                .Fill(M.Disc(8, 7.5f, 2.6f), C("#ffffff")));

            yield return I("fx_spark", c => c
                .Fill(M.Rect(7, 2, 8, 13).Rect(2, 7, 13, 8), C("#fff8c0"))
                .Fill(M.Rect(6, 6, 9, 9), C("#ffffff")));

            yield return I("fx_poof", c => c
                .Paint(M.Disc(5, 6, 3.6f).Disc(10.5f, 6.5f, 3.8f).Disc(8, 10, 4).Disc(3.5f, 9.5f, 2.6f).Disc(12.5f, 10, 2.6f), C("#e8e4f0")));

            yield return I("trash_bottle", c => c
                .Paint(M.Rect(4, 1, 11, 9).Poly(4, 9, 12, 9, 10, 12, 6, 12), new Color32(170, 220, 240, 255))
                .Fill(M.Rect(5, 4, 10, 6), C("#e05050"))
                .Paint(M.Rect(6, 12, 9, 14), C("#3a8ae0")));

            yield return I("trash_can", c => c
                .Paint(M.Rect(3, 1, 12, 12), C("#d83a3a"))
                .Paint(M.Rect(3, 11, 12, 13), C("#c8ccd8"))
                .Fill(M.Rect(5, 5, 10, 7), C("#f0f0f0")));

            // 미끼
            yield return I("bait_worm", c =>
            {
                var m = M;
                foreach (var (x, y) in new[] { (2.5f, 4f), (4.5f, 5.5f), (6.5f, 5f), (8.5f, 4.5f), (10.5f, 5.5f), (12f, 7.5f), (12.5f, 10f) })
                    m.Disc(x, y, 2.4f);
                c.Paint(m, C("#e8708a"));
                c.Flat(M.Rect(5, 3, 5, 7).Rect(9, 3, 9, 6), C("#b84860"));
                c.Dots(Ico.K, 12, 10);
            });

            yield return I("bait_meat", c => c
                .Paint(M.Line(10, 10, 14, 14, 2.6f).Disc(14.5f, 13, 1.7f).Disc(13, 14.5f, 1.7f), C("#f0ead8"))
                .Paint(M.Ellipse(7, 7, 6.6f, 5.8f), C("#d24a52"))
                .Flat(M.Ellipse(6, 7, 3, 2.4f).Minus(M.Ellipse(6, 7, 2, 1.4f)), C("#f4c0c4"))
                .Dots(C("#f4c0c4"), 10, 5, 9, 4, 3, 10));

            yield return I("bait_candy", c => c
                .Paint(M.Poly(0, 4, 5, 7, 5, 9, 0, 12), C("#f080d0"))
                .Paint(M.Poly(16, 4, 11, 7, 11, 9, 16, 12), C("#f080d0"))
                .Paint(M.Disc(8, 8, 4.6f), C("#a040e0"))
                .Dots(Ico.W8, 6, 9, 7, 10, 8, 10, 9, 9, 9, 8, 8, 7));

            yield return I("bait_bone", c => c
                .Paint(M.Line(4, 4, 12, 12, 3.2f).Disc(2.6f, 5, 2.2f).Disc(5, 2.6f, 2.2f).Disc(11, 13.4f, 2.2f).Disc(13.4f, 11, 2.2f), C("#d4d4a8"))
                .Dots(C("#8a9a50"), 7, 8, 9, 9, 6, 6, 12, 12));

            // 음식
            yield return I("food_1", c => c
                .Paint(M.Poly(0.5f, 8, 15.5f, 8, 12, 2, 4, 2).Rect(5, 1, 10, 2), C("#8a5a3a"))
                .Paint(M.Ellipse(8, 8.5f, 7.2f, 2.6f), C("#6ab040"))
                .Paint(M.Disc(8, 10.5f, 3), C("#f0ece0"))
                .Dots(Ico.K, 7, 10, 9, 10)
                .Dots(C("#c8c8d8"), 4, 13, 5, 14, 12, 13, 11, 14, 4, 15, 12, 15));

            yield return I("food_2", c => c
                .Paint(M.Ellipse(8, 6, 7.8f, 4.6f), C("#d8dce8"))
                .Paint(M.Ellipse(8, 8, 5.6f, 4.2f), C("#8a4a2a"))
                .Flat(M.Line(4.5f, 7, 7, 10.5f, 1).Line(7.5f, 6, 10.5f, 10, 1), C("#5a2a14"))
                .Paint(M.Disc(11.5f, 6.5f, 1.8f), C("#6ab040")));

            yield return I("food_3", c => c
                .Paint(M.Line(9, 7, 13, 3, 2.6f).Disc(13.8f, 3.2f, 1.7f).Disc(12.8f, 1.8f, 1.7f), C("#f0ead8"))
                .Paint(M.Disc(6.5f, 9.5f, 6), C("#c06a2a"))
                .Flat(M.Disc(6.5f, 9.5f, 4.5f).Minus(M.Disc(5.5f, 10.5f, 4.5f)), C("#8a4214"))
                .Dots(Ico.W8, 4, 13, 5, 13, 3, 12));

            // 기념품
            yield return I("gift_1", c => c
                .Paint(M.Disc(5, 11, 4.8f).Minus(M.Disc(5, 11, 1.6f))
                    .Line(7, 8.5f, 14.5f, 1.5f, 3.6f).Rect(10, 0, 12, 5).Rect(7, 3, 9, 7), GoldC)
                .Paint(M.Poly(0, 16, 2, 12, 3, 15), C("#c83848"))
                .Paint(M.Poly(9, 16, 7, 12, 6, 15), C("#c83848")));

            yield return I("gift_2", c => c
                .Paint(M.Poly(0, 13, 5, 10, 5, 5, 3, 6.5f, 1.5f, 5, 0, 8), C("#5a3080"))
                .Paint(M.Poly(16, 13, 11, 10, 11, 5, 13, 6.5f, 14.5f, 5, 16, 8), C("#5a3080"))
                .Paint(M.Disc(8, 8, 4.8f).Poly(4.5f, 11, 5, 15, 7, 12).Poly(11.5f, 11, 11, 15, 9, 12), C("#8a5ac0"))
                .Dots(Ico.W8, 6, 9, 10, 9, 7, 5, 9, 5)
                .Dots(C("#f08090"), 5, 7, 11, 7));

            yield return I("gift_3", c => c
                .Paint(M.Poly(4, 14, 12, 14, 15.5f, 0.5f, 0.5f, 0.5f), C("#c02a3a"))
                .Flat(M.Poly(6, 12.5f, 10, 12.5f, 12, 2, 4, 2), C("#3a1030"))
                .Paint(M.Poly(1.5f, 16, 14.5f, 16, 12, 12, 8, 13.5f, 4, 12), C("#6a3a9a"))
                .Paint(M.Disc(8, 13, 1.6f), GoldC));

            // 음식 4~10단계
            yield return I("food_4", c => c // 독버섯 리조또
                .Paint(M.Poly(0.5f, 8, 15.5f, 8, 12, 2, 4, 2).Rect(5, 1, 10, 2), C("#e8e0d0"))
                .Paint(M.Ellipse(8, 8.5f, 7, 2.6f), C("#f0d890"))
                .Paint(M.Disc(6, 10.5f, 2.4f), C("#c04040"))
                .Paint(M.Disc(10.5f, 10, 1.8f), C("#9a4ac0"))
                .Dots(Ico.W8, 5, 11, 7, 11, 10, 10));

            yield return I("food_5", c => c // 용암 피자
                .Paint(M.Poly(1, 14, 15, 14, 8, 1), C("#e8b050"))
                .Flat(M.Poly(3.5f, 12.5f, 12.5f, 12.5f, 8, 3.5f), C("#e04020"))
                .Paint(M.Rect(1, 13, 14, 15), C("#c08030"))
                .Dots(C("#ffd040"), 6, 10, 9, 8, 8, 11, 10, 11, 7, 7));

            yield return I("food_6", c => c // 서리 젤라토
                .Paint(M.Poly(4, 9, 12, 9, 8, 0), C("#d09050"))
                .Checker(M.Poly(4, 9, 12, 9, 8, 0), C("#a86a30"))
                .Paint(M.Disc(8, 10.5f, 4.4f), C("#a8e0ff"))
                .Paint(M.Disc(8, 13.6f, 2.4f), C("#f0f8ff"))
                .Dots(Ico.W8, 6, 12, 5, 11));

            yield return I("food_7", c => c // 심연 초밥
                .Paint(M.Rect(2, 2, 13, 7), C("#f4f0e8"))
                .Paint(M.Rect(1, 7, 14, 11), C("#40b0c8"))
                .Flat(M.Line(3, 8, 5, 10, 1).Line(7, 8, 9, 10, 1).Line(11, 8, 13, 10, 1), C("#a0f0ff"))
                .Flat(M.Rect(7, 2, 8, 11), C("#203020")));

            yield return I("food_8", c => c // 흑마법 케이크
                .Paint(M.Rect(1, 1, 14, 9), C("#5a2a6a"))
                .Flat(M.Rect(2, 5, 13, 5), C("#e080c0"))
                .Paint(M.Rect(1, 8, 14, 10), C("#f0d0f0"))
                .Paint(M.Rect(7, 10, 8, 13), C("#e0e0f0"))
                .Fill(M.Rect(7, 14, 8, 15), C("#80ff80")));

            yield return I("food_9", c => c // 마왕의 와인
                .Paint(M.Rect(4, 0, 11, 10).Rect(6, 10, 9, 15), C("#6a1030"))
                .Flat(M.Rect(5, 3, 10, 7), C("#e8d8b0"))
                .Flat(M.Rect(7, 4, 8, 6), C("#c83848"))
                .Paint(M.Rect(6, 13, 9, 15), C("#c8a040"))
                .Dots(Ico.W8, 5, 9, 5, 8));

            yield return I("food_10", c => c // 혼돈 코스요리 (덮개 접시)
                .Paint(M.Rect(0, 1, 15, 3), C("#d8dce8"))
                .Paint(M.Disc(8, 4, 6.6f).And(M.Rect(0, 4, 15, 15)), C("#b8b0d8"))
                .Paint(M.Disc(8, 12, 1.6f), GoldC)
                .Flat(M.Rect(2, 4, 13, 4), C("#8a6ac0"))
                .Dots(Ico.W8, 5, 8, 4, 7, 5, 9));

            // 기념품 4~10단계
            yield return I("gift_4", c => c // 해골 머그컵
                .Paint(M.Disc(12, 7.5f, 3.4f).Minus(M.Disc(12, 7.5f, 1.6f)), C("#e8e4d8"))
                .Paint(M.Rect(2, 1, 11, 13), C("#e8e4d8"))
                .Flat(M.Rect(4, 7, 5, 9).Rect(8, 7, 9, 9), Ico.K)
                .Flat(M.Rect(6, 4, 7, 5), Ico.K));

            yield return I("gift_5", c => c // 마법 수정구
                .Paint(M.Poly(3, 0, 13, 0, 11, 4, 5, 4), Wood)
                .Paint(M.Disc(8, 9.5f, 6), C("#9a6ae0"))
                .Flat(M.Disc(8, 9.5f, 4.4f).Minus(M.Disc(9.5f, 8, 4.4f)), C("#6a3ab0"))
                .Dots(Ico.W8, 5, 12, 6, 13, 5, 11));

            yield return I("gift_6", c => c // 용의 알 모형
                .Paint(M.Rect(4, 0, 11, 2), Wood)
                .Paint(M.Ellipse(8, 8.5f, 5, 7), C("#6ab04a"))
                .Dots(C("#3a7a2a"), 6, 6, 10, 9, 7, 11, 9, 5, 5, 9)
                .Dots(Ico.W8, 6, 12, 6, 13));

            yield return I("gift_7", c => c // 저주받은 거울
                .Paint(M.Rect(7, 0, 8, 3), GoldC)
                .Paint(M.Ellipse(8, 9, 6, 6.5f), GoldC)
                .Flat(M.Ellipse(8, 9, 4, 4.5f), C("#4a2a6a"))
                .Dots(C("#e03048"), 6, 9, 9, 9));

            yield return I("gift_8", c => c // 마검 레플리카
                .Paint(M.Line(5, 5, 14.5f, 14.5f, 3), C("#c8d0e0"))
                .Paint(M.Line(1.5f, 1.5f, 4.5f, 4.5f, 2.4f), C("#6a3a8a"))
                .Paint(M.Line(2, 8, 8, 2, 2.4f), GoldC)
                .Flat(M.Line(7, 7, 13.5f, 13.5f, 1), C("#e03048")));

            yield return I("gift_9", c => c // 마왕 왕관
                .Paint(M.Poly(1, 2, 15, 2, 15, 12, 11.5f, 7, 8, 13, 4.5f, 7, 1, 12), GoldC)
                .Dots(C("#e03048"), 8, 5, 3, 4, 12, 4)
                .Dots(C("#5ad8f8"), 5, 4, 10, 4));

            yield return I("gift_10", c => c // 마왕성 미니어처
                .Paint(M.Rect(0, 0, 15, 1), Wood)
                .Paint(M.Rect(2, 2, 13, 8).Rect(1, 8, 4, 12).Rect(11, 8, 14, 12).Rect(6, 8, 9, 10), C("#8a7080"))
                .Flat(M.Rect(7, 2, 8, 5), C("#4a2e20"))
                .Paint(M.Poly(0, 12.5f, 5, 12.5f, 2.5f, 16), C("#c83848"))
                .Paint(M.Poly(10, 12.5f, 15, 12.5f, 12.5f, 16), C("#c83848"))
                .Dots(C("#ffd040"), 2, 10, 12, 10));

            // 설정 · 소리
            yield return I("ic_gear", c =>
            {
                var m = M.Disc(8, 8, 5.4f).Rect(7, 0, 8, 15).Rect(0, 7, 15, 8).Line(2.5f, 2.5f, 13.5f, 13.5f, 2.4f).Line(2.5f, 13.5f, 13.5f, 2.5f, 2.4f);
                m.Minus(M.Disc(8, 8, 2.3f));
                c.Paint(m, Steel);
            });

            yield return I("ic_sound", c => c
                .Paint(M.Rect(1, 5, 4, 10).Poly(4, 5, 9, 1, 9, 15, 4, 11), Steel)
                .Fill(M.Disc(9, 8, 4.6f).Minus(M.Disc(9, 8, 3.4f)).And(M.Rect(11, 2, 15, 14)), C("#f0e8e0"))
                .Fill(M.Disc(9, 8, 6.9f).Minus(M.Disc(9, 8, 5.7f)).And(M.Rect(13, 0, 15, 15)), C("#f0e8e0")));

            // 업그레이드
            yield return I("up_speed", c => c
                .Paint(M.Poly(10, 16, 1.5f, 6.5f, 7, 6.5f, 3.5f, 0, 14.5f, 10, 9, 10, 13.5f, 16), C("#f8d040"))
                .Dots(Ico.W8, 9, 14, 8, 13));

            yield return I("up_dura", c => c
                .Paint(M.Disc(4.8f, 10.5f, 4).Disc(11.2f, 10.5f, 4).Poly(1, 9.5f, 15, 9.5f, 8, 1.5f), C("#e03a4a"))
                .Dots(Ico.W8, 3, 12, 4, 13));

            yield return I("up_rest", c => c
                .Paint(M.Disc(8, 6, 5.8f).Rect(6, 10, 9, 13), C("#c8e0f0"))
                .Flat(M.Disc(8, 6, 4.8f).And(M.Rect(0, 0, 15, 6)), C("#e04a60"))
                .Paint(M.Rect(5, 13, 10, 15), Wood)
                .Dots(Ico.W8, 5, 8, 5, 7));

            yield return I("up_adm", c => c
                .Paint(M.Rect(0, 3, 15, 12).Minus(M.Disc(0, 7.5f, 2.2f)).Minus(M.Disc(16, 7.5f, 2.2f)), C("#f0a040"))
                .Dots(Ico.K, 11, 4, 11, 6, 11, 9, 11, 11)
                .Flat(M.Poly(5.5f, 11, 6.5f, 8.5f, 9, 8, 6.5f, 7, 5.5f, 4.5f, 4.5f, 7, 2, 8, 4.5f, 8.5f), C("#fff0c0")));

            yield return I("up_cap", c => c
                .Paint(M.Rect(1, 0, 14, 15), C("#7a8494"))
                .Paint(M.Rect(3, 2, 12, 13), Steel)
                .Paint(M.Disc(8, 8, 3), C("#d8b040"))
                .Dots(Ico.K, 7, 7, 8, 8)
                .Flat(M.Rect(2, 3, 2, 4).Rect(2, 11, 2, 12), C("#4a5060")));

            yield return I("up_maki", c => c
                .Paint(M.Disc(8, 6, 5.8f).Poly(3, 8.5f, 13, 8.5f, 8, 16), C("#9a4ae0"))
                .Flat(M.Disc(9.5f, 5, 3).Minus(M.Disc(8.5f, 6, 3)), C("#5a2090"))
                .Dots(Ico.W8, 5, 8, 5, 7, 6, 10));

            yield return I("up_visitor", c => c
                .Paint(M.Rect(0, 5, 3, 11), Steel)
                .Paint(M.Poly(2, 5, 6, 5, 14, 1, 14, 15, 6, 11, 2, 11), C("#e84848"))
                .Paint(M.Ellipse(14, 8, 2, 7), C("#f0e0e0"))
                .Paint(M.Rect(5, 1, 7, 5), Steel));

            yield return I("up_wallet", c => c
                .Paint(M.Disc(8, 6, 6).Poly(4, 15, 12, 15, 10, 10, 6, 10), C("#b0784a"))
                .Flat(M.Rect(5, 11, 10, 11), C("#6a3a1a"))
                .Paint(M.Disc(8, 5.5f, 2.6f), GoldC));

            yield return I("up_patience", c => c
                .Paint(M.Disc(8, 8, 7.6f), C("#f0ece0"))
                .Flat(M.Disc(8, 8, 6.6f).Minus(M.Disc(8, 8, 5.6f)), C("#d8b040"))
                .Flat(M.Rect(7, 8, 8, 12).Rect(8, 7, 11, 8), Ico.K)
                .Dots(C("#c83848"), 8, 13, 13, 8, 8, 2, 2, 8));

            yield return I("up_sales", c => c
                .Paint(M.Poly(0, 8, 5, 14, 15.5f, 14, 15.5f, 2, 5, 2), C("#f2c040"))
                .Dots(Ico.K, 4, 8, 4, 7)
                .Flat(M.Rect(8, 10, 13, 10).Rect(8, 5, 13, 5).Rect(8, 7, 11, 7).Rect(8, 8, 12, 8), C("#b07818")));

            yield return I("up_lab", c => c
                .Paint(M.Poly(2.5f, 0.5f, 13.5f, 0.5f, 10, 9.5f, 6, 9.5f).Rect(6, 9, 9, 13).Rect(5, 13, 10, 15), C("#c8e0f0"))
                .Flat(M.Poly(3.5f, 1.5f, 12.5f, 1.5f, 10.6f, 6, 5.4f, 6), C("#40d0a0"))
                .Dots(Ico.W8, 7, 4, 9, 3, 8, 8, 5, 3));

            yield return I("up_trash", c => c
                .Paint(M.Poly(2, 12.5f, 14, 12.5f, 12.5f, 0.5f, 3.5f, 0.5f), Steel)
                .Flat(M.Rect(6, 2, 6, 10).Rect(9, 2, 9, 10), C("#6a7484"))
                .Paint(M.Rect(1, 11, 14, 13), C("#b8c0d0"))
                .Paint(M.Rect(6, 13, 9, 15), C("#6a7484")));

            yield return I("up_floor", c => c
                .Paint(M.Rect(3, 0, 12, 10).Rect(2, 10, 13, 12).Rect(2, 12, 4, 14).Rect(7, 12, 8, 14).Rect(11, 12, 13, 14), C("#8a7080"))
                .Flat(M.Rect(7, 5, 8, 7), C("#ffd040"))
                .Flat(M.Rect(6, 1, 9, 3), C("#4a2e20")));

            yield return I("up_region", c => c
                .Paint(M.Rect(1, 2, 14, 13), C("#e8d4a0"))
                .Flat(M.Rect(5, 3, 5, 12).Rect(10, 3, 10, 12), C("#c8b080"))
                .Dots(C("#a06040"), 3, 4, 4, 5, 5, 5, 6, 6, 7, 7, 8, 7, 9, 8)
                .Dots(C("#e03048"), 10, 9, 12, 11, 12, 9, 10, 11, 11, 10));
        }
    }
}

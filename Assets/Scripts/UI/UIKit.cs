using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mawang
{
    public enum Btn { Primary, Good, Danger, Alt, Accent }

    // 코드로 도트 UGUI를 조립하는 헬퍼.
    // 모든 테두리/버튼은 16 PPU 도트 스프라이트를 P배(화면 픽셀 3배)로 9-slice 해서 그린다.
    public static class UIKit
    {
        public const float P = 3f;                   // UI 1도트 = 3 UI 단위 (1920×1080 기준)
        public const int TS = 20, TM = 24, TL = 36;  // 글자 크기: 도트 폰트 배수(Galmuri9 10px ×2 / Galmuri11 12px ×2, ×3)

        // ── 폰트 ───────────────────────────────────────────────
        static Font font, fontBold, fontSmall;
        public static Font Font => font != null ? font : font = LoadFont("Galmuri11");
        public static Font FontBold => fontBold != null ? fontBold : fontBold = LoadFont("Galmuri11-Bold");
        public static Font FontSmall => fontSmall != null ? fontSmall : fontSmall = LoadFont("Galmuri9");

        static Font LoadFont(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            if (f == null) return Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Segoe UI", "Arial" }, 20);
            // 도트 폰트는 확대해도 뭉개지지 않도록 Point 필터
            void Crisp(Font changed) { if (changed == f && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point; }
            Font.textureRebuilt += Crisp;
            Crisp(f);
            return f;
        }

        // ── 색 ────────────────────────────────────────────────
        public static readonly Color TextMain = new Color(0.96f, 0.93f, 0.87f);
        public static readonly Color TextDim = new Color(0.66f, 0.6f, 0.72f);
        public static readonly Color TextDark = new Color(0.2f, 0.13f, 0.1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        public static readonly Color GoodText = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color BadText = new Color(1f, 0.45f, 0.45f);
        public static readonly Color MatText = new Color(0.95f, 0.66f, 0.45f);
        public static readonly Color RpText = new Color(0.5f, 0.9f, 1f);
        public static readonly Color Shadow = new Color(0.06f, 0.03f, 0.08f, 1f);

        // ── 기본 요소 ──────────────────────────────────────────
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h, Vector2? anchor = null)
        {
            var a = anchor ?? Vector2.zero;
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // 부모 가장자리 기준 배치: anchorMin/Max(0~1) + 가장자리로부터의 여백(offsetMin = 왼쪽·아래, offsetMax = 오른쪽·위)
        public static RectTransform Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            return le;
        }

        public static Image Box(Transform parent, Color color, string name = "Box")
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 9-slice 도트 판. style: ui_window / ui_panel / ui_row / ui_inset / ui_title / ui_tip / ui_btn_*
        public static Image Panel(Transform parent, string style, string name = "Panel")
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            SetSliced(img, style);
            img.raycastTarget = false;
            return img;
        }

        public static void SetSliced(Image img, string style)
        {
            img.sprite = Art.Get(style);
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            // 캔버스 기준 100 PPU, 스프라이트 16 PPU → 1도트 = 6.25 단위. P 단위가 되도록 배율 조정
            img.pixelsPerUnitMultiplier = 100f / PixelArtPPU / P;
        }

        const float PixelArtPPU = 16f;

        // 도트 아이콘/스프라이트: 정수배로 키워 box 안에 넣는다
        public static Image Icon(Transform parent, string sprite, float box = 48, Color? tint = null)
        {
            var img = Rect("Icon", parent).gameObject.AddComponent<Image>();
            img.sprite = Art.Get(sprite);
            img.color = tint ?? Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;
            Size(img, box, box);
            img.rectTransform.sizeDelta = new Vector2(box, box);
            if (img.sprite != null)
            {
                var r = img.sprite.rect;
                float scale = Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(r.width, r.height)));
                // 레이아웃 칸은 box, 그림은 정수배 크기로 가운데
                var inner = Rect("Pix", img.transform);
                img.enabled = false;
                var pix = inner.gameObject.AddComponent<Image>();
                pix.sprite = img.sprite;
                pix.color = img.color;
                pix.raycastTarget = false;
                inner.anchorMin = inner.anchorMax = inner.pivot = new Vector2(0.5f, 0.5f);
                inner.sizeDelta = new Vector2(r.width * scale, r.height * scale);
            }
            return img;
        }

        public static void Tint(Image icon, Color c)
        {
            foreach (var i in icon.GetComponentsInChildren<Image>(true)) i.color = c;
        }

        public static Text Label(Transform parent, string text, int size = TM, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, float width = -1, bool shadow = true)
        {
            var t = Rect("Label", parent).gameObject.AddComponent<Text>();
            t.font = size % 12 == 0 ? (size >= TL ? FontBold : Font) : FontSmall;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? TextMain;
            t.text = text;
            t.supportRichText = true;
            t.lineSpacing = 1.1f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = Shadow;
                sh.effectDistance = new Vector2(2, -2);
            }
            var le = t.gameObject.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width; else le.flexibleWidth = 1;
            return t;
        }

        // ── 버튼 ───────────────────────────────────────────────
        static string StyleName(Btn s) => s switch
        {
            Btn.Good => "g", Btn.Danger => "r", Btn.Alt => "n", Btn.Accent => "y", _ => "p",
        };

        public static Button Button(Transform parent, string text, Action onClick, float w = 140, float h = 48, Btn style = Btn.Primary, int size = TS, string icon = null, float iconBox = 32)
        {
            var img = Rect("Button", parent).gameObject.AddComponent<Image>();
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(() => { Sound.Play("click"); onClick(); });
            Size(img, w, h);
            img.rectTransform.sizeDelta = new Vector2(w, h);

            var content = Rect("Content", img.transform);
            Stretch(content, 6, 0, 6, 0);
            var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 6;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            if (icon != null) Icon(content, icon, iconBox);
            if (!string.IsNullOrEmpty(text) || icon == null)
            {
                var label = Label(content, text, size, TextAnchor.MiddleCenter);
                label.GetComponent<LayoutElement>().flexibleWidth = 0;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            img.gameObject.AddComponent<ButtonFx>().Init(btn, img, content, StyleName(style));
            return btn;
        }

        // 누르고 있으면 빠르게 반복(폰: 꾹 누르기 · PC: 우클릭 누르기). 짧게 누르면 한 번.
        // step 이 false 를 돌려주면 반복을 멈춘다.
        public static Button Hold(this Button b, Func<bool> step)
        {
            b.gameObject.AddComponent<HoldRepeat>().Init(b, step);
            return b;
        }

        // 자식을 바로 숨기고 지운다. Destroy 는 프레임 끝에 지우므로 그 사이 레이아웃이 두 배로 늘어 UI가 튄다.
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        public static RectTransform Content(this Button b) => (RectTransform)b.transform.Find("Content");

        public static void SetStyle(this Button b, Btn style) => b.GetComponent<ButtonFx>().SetStyle(StyleName(style));

        // ── 레이아웃 ───────────────────────────────────────────
        public static RectTransform Row(Transform parent, float h = 48, float spacing = 10, bool bg = false)
        {
            var rt = Rect("Row", parent);
            if (bg) { var img = rt.gameObject.AddComponent<Image>(); SetSliced(img, "ui_row"); img.raycastTarget = false; }
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.padding = bg ? new RectOffset(12, 12, 6, 6) : new RectOffset(4, 4, 0, 0);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 6, string bg = null, int pad = 0)
        {
            var rt = Rect("Column", parent);
            if (bg != null) { var img = rt.gameObject.AddComponent<Image>(); SetSliced(img, bg); img.raycastTarget = false; }
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.padding = new RectOffset(pad, pad, pad, pad);
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            return rt;
        }

        // 카드 격자 (상점/보관함/도감)
        public static RectTransform Grid(Transform parent, float cellW, float cellH, float spacing = 12)
        {
            var rt = Rect("Grid", parent);
            var gl = rt.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(cellW, cellH);
            gl.spacing = new Vector2(spacing, spacing);
            gl.childAlignment = TextAnchor.UpperLeft;
            gl.padding = new RectOffset(4, 4, 4, 4);
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        // 카드: 테두리 판 + 세로 배치
        public static RectTransform Card(Transform parent, string style = "ui_row", int pad = 12, float spacing = 6, TextAnchor align = TextAnchor.UpperCenter)
        {
            var c = Column(parent, spacing, style, pad);
            var vl = c.GetComponent<VerticalLayoutGroup>();
            vl.childAlignment = align;
            vl.childForceExpandWidth = false;
            return c;
        }

        public static void Header(Transform parent, string text, string icon = null)
        {
            var row = Row(parent, 44, 8);
            if (icon != null) Icon(row, icon, 32);
            var t = Label(row, text, TM, TextAnchor.MiddleLeft, Gold);
            t.GetComponent<LayoutElement>().flexibleWidth = 0;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            // 제목 옆 장식 선
            var line = Box(row, new Color(0.55f, 0.4f, 0.2f), "Rule");
            var le = Size(line, -1, 3);
            le.flexibleWidth = 1;
        }

        public static void Note(Transform parent, string text, int size = TS)
        {
            var row = Row(parent, size + 16);
            Label(row, text, size, TextAnchor.MiddleLeft, TextDim);
        }

        public static void Spacer(Transform parent, float h = 8)
        {
            Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        }

        public static RectTransform Flex(Transform parent)
        {
            var r = Rect("Flex", parent);
            r.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return r;
        }

        // ── 값 표시 ────────────────────────────────────────────
        // 아이콘 + 숫자. 부족하면 빨간색.
        public static RectTransform Amount(Transform parent, string icon, Func<string> text, Func<bool> ok = null, int size = TS, float iconBox = 24, Color? color = null)
        {
            var row = Rect("Amount", parent);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            Icon(row, icon, iconBox);
            var t = Label(row, "", size, TextAnchor.MiddleLeft, color);
            t.GetComponent<LayoutElement>().flexibleWidth = 0;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var baseColor = color ?? TextMain;
            t.Bind(text);
            if (ok != null) t.gameObject.AddComponent<Binding>().Init(() => t.color = ok() ? baseColor : BadText);
            return row;
        }

        public static RectTransform Amount(Transform parent, string icon, string text, int size = TS, float iconBox = 24, Color? color = null) =>
            Amount(parent, icon, () => text, null, size, iconBox, color);

        // 비용 묶음(골드/자재/RP), 보유량이 모자라면 빨간색
        public static RectTransform Cost(Transform parent, int gold, int mat = 0, int rp = 0, int size = TS)
        {
            var g = Game.I;
            var row = Rect("Cost", parent);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            if (rp > 0) Amount(row, "ic_rp", () => $"{rp:N0}", () => g.S.rp >= rp, size);
            if (gold > 0) Amount(row, "ic_gold", () => $"{gold:N0}", () => g.S.gold >= gold, size);
            if (mat > 0) Amount(row, "ic_mat", () => $"{mat:N0}", () => g.S.material >= mat, size);
            if (gold <= 0 && mat <= 0 && rp <= 0) Label(row, L.T("무료", "Free"), size, TextAnchor.MiddleCenter, GoodText).GetComponent<LayoutElement>().flexibleWidth = 0;
            return row;
        }

        // 진행 바
        public static RectTransform Bar(Transform parent, Func<float> value, Color color, float w = -1, float h = 18, Func<string> text = null)
        {
            var bg = Panel(parent, "ui_inset", "Bar");
            var le = Size(bg, w, h);
            if (w < 0) le.flexibleWidth = 1;
            var fill = Rect("Fill", bg.transform);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, 0.5f);
            fill.offsetMin = new Vector2(P, P);
            fill.offsetMax = new Vector2(P, -P);
            var img = fill.gameObject.AddComponent<Image>();
            SetSliced(img, "ui_fill");
            img.color = color;
            img.raycastTarget = false;
            var bar = bg.rectTransform;
            bg.gameObject.AddComponent<Binding>().Init(() =>
            {
                float v = Mathf.Clamp01(value());
                float inner = bar.rect.width - P * 2;
                fill.anchorMax = new Vector2(0, 1);
                fill.sizeDelta = new Vector2(Mathf.Round(inner * v / P) * P, -P * 2);
                fill.gameObject.SetActive(v > 0.001f);
            });
            if (text != null)
            {
                var t = Label(bg.transform, "", TS, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 0, 0, 0, 0);
                t.Bind(text);
            }
            return bar;
        }

        // 업그레이드 단계 눈금
        public static RectTransform Pips(Transform parent, int level, int max, float pipW = 18, float pipH = 24)
        {
            var row = Rect("Pips", parent);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 3;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            for (int i = 1; i <= max; i++)
            {
                var img = Rect("Pip", row).gameObject.AddComponent<Image>();
                img.sprite = Art.Get(i <= level ? "ui_pip_on" : i == level + 1 ? "ui_pip_next" : "ui_pip_off");
                img.raycastTarget = false;
                Size(img, pipW, pipH);
            }
            return row;
        }

        // 숫자 배지 (0이면 숨김)
        public static void Badge(Transform target, Func<int> count, Vector2 offset)
        {
            var b = Panel(target, "ui_badge", "Badge");
            var rt = b.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(30, 30);
            var t = Label(b.transform, "", TS, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 0, 2, 0, 0);
            b.gameObject.AddComponent<Binding>().Init(() =>
            {
                int n = count();
                bool on = n > 0;
                if (b.enabled != on) { b.enabled = on; t.enabled = on; }
                if (on)
                {
                    string s = n > 9 ? "9+" : n.ToString();
                    if (t.text != s) t.text = s;
                    rt.sizeDelta = new Vector2(n > 9 ? 40 : 30, 30);
                }
            });
        }

        // ── 바인딩 ─────────────────────────────────────────────
        public static Text Bind(this Text t, Func<string> f)
        {
            t.gameObject.AddComponent<Binding>().Init(() => { var s = f(); if (t.text != s) t.text = s; });
            return t;
        }

        public static Button BindEnabled(this Button b, Func<bool> f)
        {
            b.gameObject.AddComponent<Binding>().Init(() => b.interactable = f());
            return b;
        }

        // 숫자가 부드럽게 올라가는 재화 표시 + 증가 시 번쩍
        public static Text BindCounter(this Text t, Func<long> value, string format = "N0", bool compact = false)
        {
            t.gameObject.AddComponent<Counter>().Init(t, value, format, compact);
            return t;
        }

        public static T Tip<T>(this T c, Func<string> text) where T : Component
        {
            var go = c.gameObject;
            var gr = go.GetComponent<Graphic>();
            if (gr == null) { gr = go.AddComponent<Image>(); gr.color = Color.clear; }
            gr.raycastTarget = true;
            var ph = go.GetComponent<PointerHandler>() ?? go.AddComponent<PointerHandler>();
            ph.onEnter += () => Tooltip.Show(go, text);
            ph.onExit += () => Tooltip.Hide(go);
            return c;
        }

        public static T Tip<T>(this T c, string text) where T : Component => c.Tip(() => text);

        public static string Money(int g, int m)
        {
            if (g > 0 && m > 0) return L.T($"{g:N0}G / {m:N0}자재", $"{g:N0}G / {m:N0} Mat");
            if (g > 0) return $"{g:N0}G";
            if (m > 0) return L.T($"{m:N0}자재", $"{m:N0} Mat");
            return L.T("무료", "Free");
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static string Col(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";
    }

    public class Binding : MonoBehaviour
    {
        Action action;
        public void Init(Action a) { action += a; a(); }
        void LateUpdate() => action?.Invoke();
    }

    // 굴러가는 숫자
    public class Counter : MonoBehaviour
    {
        Text t;
        Func<long> value;
        string format;
        double shown;
        float flash;
        Color baseColor;

        bool compact;

        public void Init(Text text, Func<long> v, string fmt, bool compactNumbers = false)
        {
            t = text; value = v; format = fmt; compact = compactNumbers;
            shown = v();
            baseColor = t.color;
            t.text = Format((long)shown);
        }

        // 좁은 칸: 100,000 이상은 845.3K / 2.93M / 1.2B
        string Format(long v)
        {
            if (!compact || Math.Abs(v) < 100000) return v.ToString(format);
            if (Math.Abs(v) >= 1_000_000_000) return (v / 1e9).ToString("0.##") + "B";
            if (Math.Abs(v) >= 1_000_000) return (v / 1e6).ToString("0.##") + "M";
            return (v / 1e3).ToString("0.#") + "K";
        }

        void LateUpdate()
        {
            long target = value();
            if ((long)shown != target)
            {
                if (target > shown + 0.5) flash = 0.35f;
                double diff = target - shown;
                double step = Math.Max(1, Math.Abs(diff) * Math.Min(1, Time.unscaledDeltaTime * 10));
                shown = Math.Abs(diff) <= step ? target : shown + Math.Sign(diff) * step;
                t.text = Format((long)shown);
            }
            flash = Mathf.Max(0, flash - Time.unscaledDeltaTime);
            t.color = Color.Lerp(baseColor, Color.white, flash / 0.35f);
        }
    }

    // 도트 버튼 상태: 누르면 입술이 사라지고 내용이 내려간다, 비활성은 회색, 올리면 밝게
    public class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        Button btn;
        Image img;
        RectTransform content;
        string style;
        bool down, hover, lastInteractable = true, init;
        static readonly Vector2 Up = new Vector2(0, UIKit.P);       // 입술(2도트) 위로 내용 올림
        static readonly Vector2 Pressed = new Vector2(0, -UIKit.P);

        public void Init(Button b, Image i, RectTransform c, string s)
        {
            btn = b; img = i; content = c; style = s;
            init = true;
            Refresh(true);
        }

        public void SetStyle(string s) { style = s; Refresh(true); }

        void Refresh(bool force = false)
        {
            if (!init) return;
            bool inter = btn.interactable;
            if (!force && inter == lastInteractable && !dirty) return;
            dirty = false;
            lastInteractable = inter;
            string sprite = !inter ? "ui_btn_d" : down ? $"ui_btn_{style}_down" : $"ui_btn_{style}";
            UIKit.SetSliced(img, sprite);
            img.color = !inter || (hover && !down) ? Color.white : new Color(0.9f, 0.9f, 0.9f);
            var off = !inter ? Up : down ? Pressed : Up;
            content.offsetMin = new Vector2(content.offsetMin.x, off.y);
            content.offsetMax = new Vector2(content.offsetMax.x, off.y);
            foreach (var g in content.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = inter ? 1f : 0.45f;
                g.color = c;
            }
        }

        bool dirty;
        void Mark() { dirty = true; Refresh(); }

        void LateUpdate() => Refresh();

        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && btn.interactable) { down = true; Mark(); } }
        public void OnPointerUp(PointerEventData e) { down = false; Mark(); }
        public void OnPointerEnter(PointerEventData e) { hover = true; Mark(); }
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; Mark(); }
    }

    // 누르고 있으면 반복 실행. 폰은 꾹 누르기(0.4초 뒤부터), PC는 우클릭을 누르는 동안(바로 시작).
    public class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        const float Delay = 0.4f;
        Button btn;
        Func<bool> step;
        bool active, repeated;
        float next;
        int count;
        Vector2 downPos;

        public void Init(Button b, Func<bool> s) { btn = b; step = s; }

        bool Fire()
        {
            if (btn != null && !btn.interactable) return false;
            return step != null && step();
        }

        public void OnPointerDown(PointerEventData e)
        {
            bool right = e.button == PointerEventData.InputButton.Right;
            bool touchHold = e.button == PointerEventData.InputButton.Left && GameUI.Mobile;
            if (!right && !touchHold) return;
            active = true;
            repeated = false;
            count = 0;
            downPos = e.position;
            next = Time.unscaledTime + Delay;
            if (right) { repeated = true; if (!Fire()) active = false; }
        }

        public void OnPointerUp(PointerEventData e) => active = false;
        public void OnPointerExit(PointerEventData e) => active = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (repeated) { repeated = false; return; } // 꾹 눌러 반복한 뒤 뗀 것은 클릭이 아니다
            Fire();
        }

        void Update()
        {
            if (!active) return;
            if (Pointer.current != null && (Pointer.current.position.ReadValue() - downPos).sqrMagnitude > 25f * 25f) { active = false; return; } // 스크롤하려고 끈 것
            if (Time.unscaledTime < next) return;
            repeated = true;
            count++;
            if (!Fire()) { active = false; return; }
            next = Time.unscaledTime + Mathf.Max(0.035f, 0.2f * Mathf.Pow(0.82f, count)); // 점점 빨라진다
        }

        void OnDisable() => active = false;
    }

    // 좌/우 클릭, 호버를 전달하는 범용 핸들러
    public class PointerHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action onLeft, onRight, onEnter, onExit;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) onLeft?.Invoke();
            else if (e.button == PointerEventData.InputButton.Right) onRight?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
        void OnDisable() => onExit?.Invoke();
    }

    // 마우스를 따라다니는 툴팁 (양피지 판)
    public static class Tooltip
    {
        static RectTransform root, box;
        static Text text;
        static GameObject owner;
        static Func<string> source;
        static float delay;

        public static void Init(RectTransform parent)
        {
            root = parent;
            var bg = UIKit.Panel(parent, "ui_tip", "Tooltip");
            box = bg.rectTransform;
            box.pivot = new Vector2(0, 1);
            box.anchorMin = box.anchorMax = Vector2.zero;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vl = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(14, 14, 10, 12);
            vl.childControlWidth = vl.childControlHeight = true;
            text = UIKit.Label(bg.transform, "", UIKit.TS, TextAnchor.UpperLeft, UIKit.TextDark, -1, false);
            var le = text.GetComponent<LayoutElement>();
            le.flexibleWidth = 0;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            box.gameObject.SetActive(false);
        }

        public static void Show(GameObject o, Func<string> f) { owner = o; source = f; delay = 0.25f; }

        public static void Hide(GameObject o)
        {
            if (owner != o) return;
            owner = null;
            if (box != null) box.gameObject.SetActive(false);
        }

        public static void Tick()
        {
            if (box == null) return;
            if (owner == null || !owner.activeInHierarchy) { owner = null; box.gameObject.SetActive(false); return; }
            delay -= Time.unscaledDeltaTime;
            if (delay > 0) return;
            string s = source();
            if (string.IsNullOrEmpty(s)) { box.gameObject.SetActive(false); return; }
            if (text.text != s) text.text = s;
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
            box.SetAsLastSibling();

            var canvas = root.GetComponentInParent<Canvas>();
            Vector2 mouse = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            var pos = local + root.rect.size * root.pivot + new Vector2(22, -26);
            var size = box.rect.size;
            if (pos.x + size.x > root.rect.width - 8) pos.x = pos.x - size.x - 44;
            if (pos.y - size.y < 8) pos.y = size.y + 8;
            box.anchoredPosition = pos;
        }
    }

    // 창이 튀어나오는 연출
    public class PopIn : MonoBehaviour
    {
        float t;
        const float Dur = 0.14f;
        void OnEnable() { t = 0; transform.localScale = Vector3.one * 0.92f; }
        void Update()
        {
            if (t >= Dur) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Dur);
            float s = 1f + 0.08f * (1f - k) * Mathf.Sin(k * Mathf.PI) - 0.08f * (1f - k) * (1f - k);
            transform.localScale = Vector3.one * s;
        }
    }
}

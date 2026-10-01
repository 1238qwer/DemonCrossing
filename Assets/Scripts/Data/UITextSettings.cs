using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Mawang
{
    // UI 문구 수정용 에셋 (Assets/Resources/UIText.asset).
    // 코드의 L.T("한국어", "English") 문구를 메뉴 Mawang/UI Text 가 모아 이 목록을 만든다(스크립트가 바뀌면 자동으로 다시 맞춤).
    // 인스펙터에서 ko/en 을 고치면 게임에 그 문구가 대신 나온다. 플레이 중에 고쳐도 반영된다.
    // {0} {1} … 은 코드가 채우는 값(숫자·이름 등)이다. 옮기거나 빼도 되지만 새 번호를 만들 수는 없다.
    public class UITextSettings : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("한국어 문구")] [TextArea(1, 6)] public string ko;
            [Tooltip("English text")] [TextArea(1, 6)] public string en;
            [Tooltip("코드 위치 (참고용)")] public string where;
            [Tooltip("{번호} = 코드가 채우는 값 (참고용)")] public string args;
            [HideInInspector] public string origKo;
            [HideInInspector] public string origEn;

            public bool Edited => (!string.IsNullOrEmpty(ko) && ko != origKo) || (!string.IsNullOrEmpty(en) && en != origEn);
            public string OutKo => string.IsNullOrEmpty(ko) ? origKo : ko;
            public string OutEn => string.IsNullOrEmpty(en) ? origEn : en;
            public bool HasHoles => Hole.IsMatch(origKo ?? "") || Hole.IsMatch(origEn ?? "");
        }

        public List<Entry> entries = new List<Entry>();

        // ── 런타임 ─────────────────────────────────────────────
        static readonly Regex Hole = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);

        class Pattern
        {
            public Entry e;
            public Regex ko, en;
            public int literalLength;
        }

        static UITextSettings instance;
        static bool loaded;
        Dictionary<(string, string), Entry> literals;
        List<Pattern> patterns;
        readonly Dictionary<(string, string), string> cache = new Dictionary<(string, string), string>();

        public static UITextSettings I
        {
            get
            {
                if (!loaded)
                {
                    try { instance = Resources.Load<UITextSettings>("UIText"); }
                    catch (Exception) { return null; } // 직렬화 중 같은 곳에서는 불러올 수 없다 — 다음에 다시
                    loaded = true;
                }
                return instance;
            }
        }

        // 고친 문구가 있으면 그것을, 없으면 null (L.T 가 기본 문구를 쓴다)
        public static string Resolve(string ko, string en)
        {
            var s = I;
            if (s == null) return null;
            if (s.literals == null) s.Build();
            if (s.literals.Count == 0 && s.patterns.Count == 0) return null;

            if (s.literals.TryGetValue((ko, en), out var e)) return L.En ? e.OutEn : e.OutKo;
            if (s.patterns.Count == 0) return null;

            if (s.cache.TryGetValue((ko, en), out var hit)) return hit;
            string result = s.MatchPattern(ko, en);
            if (s.cache.Count > 4096) s.cache.Clear();
            s.cache[(ko, en)] = result;
            return result;
        }

        public static void ClearCache() { if (instance != null) instance.cache.Clear(); }

        void Build()
        {
            literals = new Dictionary<(string, string), Entry>();
            patterns = new List<Pattern>();
            cache.Clear();
            foreach (var e in entries)
            {
                if (e == null || !e.Edited) continue;
                if (!e.HasHoles) { literals[(e.origKo, e.origEn)] = e; continue; }
                patterns.Add(new Pattern
                {
                    e = e,
                    ko = Compile(e.origKo),
                    en = Compile(e.origEn),
                    literalLength = Hole.Replace(e.origKo + e.origEn, "").Length,
                });
            }
            // 글자가 더 많이 겹치는(더 구체적인) 문구부터 맞춰 본다
            patterns.Sort((a, b) => b.literalLength.CompareTo(a.literalLength));
        }

        string MatchPattern(string ko, string en)
        {
            foreach (var p in patterns)
            {
                var mk = p.ko.Match(ko);
                if (!mk.Success) continue;
                var me = p.en.Match(en);
                if (!me.Success) continue;
                // 지금 언어의 값을 먼저, 없으면 다른 언어 쪽 값을 쓴다
                var first = L.En ? me : mk;
                var second = L.En ? mk : me;
                return Hole.Replace(L.En ? p.e.OutEn : p.e.OutKo, m =>
                {
                    string name = "h" + m.Groups[1].Value;
                    if (first.Groups[name].Success) return first.Groups[name].Value;
                    if (second.Groups[name].Success) return second.Groups[name].Value;
                    return m.Value;
                });
            }
            return null;
        }

        // "관람료 {0}/분" → ^관람료 (?<h0>.*?)/분$
        static Regex Compile(string template)
        {
            var sb = new StringBuilder(@"\A");
            int last = 0;
            foreach (Match m in Hole.Matches(template ?? ""))
            {
                sb.Append(Regex.Escape(template.Substring(last, m.Index - last)));
                sb.Append("(?<h").Append(m.Groups[1].Value).Append(">.*?)");
                last = m.Index + m.Length;
            }
            if (template != null) sb.Append(Regex.Escape(template.Substring(last)));
            sb.Append(@"\z");
            return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
        }

        void OnValidate()
        {
            literals = null;
            if (!Application.isPlaying || Game.I == null) return;
            Game.I.MarkDirty(); // UI 다시 그리기
        }
    }
}

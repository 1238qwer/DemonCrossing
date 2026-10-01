using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // Assets/Scripts 의 L.T("한국어", "English") 문구를 모아 UIText 에셋과 맞춘다.
    // 스크립트가 컴파일될 때마다 자동으로 돌아서 새 문구는 추가하고, 사라진 문구는 빼고, 고친 값은 유지한다.
    // $"…{값}…" 문구의 {값} 자리는 {0} {1} … 로 바뀐다.
    [InitializeOnLoad]
    public static class UITextSync
    {
        const string AssetPath = "Assets/Resources/UIText.asset";
        const string ScriptRoot = "Assets/Scripts";

        static UITextSync()
        {
            EditorApplication.delayCall += () => Sync(false);
        }

        [MenuItem("Mawang/UI Text (UI 문구)")]
        static void Open() => Sync(true);

        struct Found
        {
            public string ko, en, args, where;
        }

        static void Sync(bool select)
        {
            var found = Scan();

            var s = AssetDatabase.LoadAssetAtPath<UITextSettings>(AssetPath);
            bool created = s == null;
            if (created)
            {
                s = ScriptableObject.CreateInstance<UITextSettings>();
                AssetDatabase.CreateAsset(s, AssetPath);
            }

            var old = new Dictionary<string, UITextSettings.Entry>();
            foreach (var e in s.entries) if (e != null) old[Key(e.origKo, e.origEn)] = e;

            var list = new List<UITextSettings.Entry>();
            foreach (var f in found)
            {
                if (old.TryGetValue(Key(f.ko, f.en), out var e))
                {
                    old.Remove(Key(f.ko, f.en));
                    e.where = f.where;
                    e.args = f.args;
                    list.Add(e);
                }
                else list.Add(new UITextSettings.Entry { ko = f.ko, en = f.en, origKo = f.ko, origEn = f.en, where = f.where, args = f.args });
            }
            foreach (var e in old.Values)
                if (e.Edited) Debug.LogWarning($"[UIText] 코드에서 사라진 문구라 목록에서 뺐습니다. 고친 값: \"{e.ko}\" / \"{e.en}\" (원문 \"{e.origKo}\")");

            bool changed = created || list.Count != s.entries.Count;
            for (int i = 0; !changed && i < list.Count; i++)
                changed = !ReferenceEquals(list[i], s.entries[i]) || list[i].where != s.entries[i].where || list[i].args != s.entries[i].args;
            if (changed)
            {
                s.entries = list;
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssets();
                Debug.Log($"[UIText] 문구 {list.Count}개 ({AssetPath})");
            }
            if (select) { Selection.activeObject = s; EditorGUIUtility.PingObject(s); }
        }

        static string Key(string ko, string en) => ko + "\u0001" + en;

        // ── 스크립트 읽기 ──────────────────────────────────────
        static List<Found> Scan()
        {
            var result = new List<Found>();
            var index = new Dictionary<string, int>();
            var extra = new Dictionary<string, int>();
            var files = Directory.GetFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
            foreach (var path in files)
            {
                string src = File.ReadAllText(path);
                string file = Path.GetFileName(path);
                foreach (var (at, ko, en, args) in Calls(src))
                {
                    string key = Key(ko, en);
                    if (index.TryGetValue(key, out int i)) { extra[key] = extra.TryGetValue(key, out int n) ? n + 1 : 1; continue; }
                    index[key] = result.Count;
                    result.Add(new Found { ko = ko, en = en, args = args, where = $"{file}:{LineOf(src, at)}" });
                }
            }
            for (int i = 0; i < result.Count; i++)
            {
                var f = result[i];
                if (extra.TryGetValue(Key(f.ko, f.en), out int n)) { f.where += $" 외 {n}곳"; result[i] = f; }
            }
            return result;
        }

        static int LineOf(string s, int at)
        {
            int line = 1;
            for (int i = 0; i < at; i++) if (s[i] == '\n') line++;
            return line;
        }

        // 주석·문자열 밖에 있는 L.T( 또는 T( 호출 중, 인자 두 개가 모두 문자열인 것
        static IEnumerable<(int at, string ko, string en, string args)> Calls(string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*') { int e = s.IndexOf("*/", i + 2); i = e < 0 ? s.Length : e + 2; continue; }
                if (IsLiteralStart(s, i)) { SkipLiteral(s, ref i); continue; }
                if (c == '\'') { SkipChar(s, ref i); continue; }

                if (c == 'T' && i + 1 < s.Length && s[i + 1] == '(' && IsCallName(s, i))
                {
                    int at = i;
                    int j = i + 2;
                    if (TryPair(s, ref j, out var ko, out var en, out var args)) { yield return (at, ko, en, args); i = j; continue; }
                }
                i++;
            }
        }

        // "L.T" 또는 식별자 경계의 "T" (Loc.cs 안의 T("골드", "Gold"))
        static bool IsCallName(string s, int i)
        {
            if (i == 0) return true;
            char p = s[i - 1];
            if (p == '.') return i >= 2 && s[i - 2] == 'L' && (i < 3 || !IsIdent(s[i - 3]));
            return !IsIdent(p);
        }

        static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

        static bool TryPair(string s, ref int j, out string ko, out string en, out string args)
        {
            ko = en = args = null;
            var holes = new List<string>();
            SkipSpace(s, ref j);
            if (!ParseLiteral(s, ref j, holes, out var a)) return false;
            SkipSpace(s, ref j);
            if (j >= s.Length || s[j] != ',') return false;
            j++;
            SkipSpace(s, ref j);
            if (!ParseLiteral(s, ref j, holes, out var b)) return false;
            SkipSpace(s, ref j);
            if (j >= s.Length || s[j] != ')') return false;
            j++;
            ko = a;
            en = b;
            var sb = new StringBuilder();
            for (int k = 0; k < holes.Count; k++)
            {
                if (k > 0) sb.Append("  ·  ");
                sb.Append('{').Append(k).Append("} = ").Append(holes[k]);
            }
            args = sb.ToString();
            return true;
        }

        static void SkipSpace(string s, ref int j)
        {
            while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
        }

        static bool IsLiteralStart(string s, int i)
        {
            int k = i;
            while (k < s.Length && k < i + 2 && (s[k] == '$' || s[k] == '@')) k++;
            if (k == i && (i > 0 && IsIdent(s[i - 1]))) return false;
            return k < s.Length && s[k] == '"';
        }

        // 문자열 하나를 읽어 {번호} 틀로 만든다. 같은 식은 같은 번호(한국어·영어 공통)
        static bool ParseLiteral(string s, ref int i, List<string> holes, out string template)
        {
            template = null;
            bool interp = false, verbatim = false;
            int k = i;
            while (k < s.Length && (s[k] == '$' || s[k] == '@')) { if (s[k] == '$') interp = true; else verbatim = true; k++; }
            if (k >= s.Length || s[k] != '"') return false;
            k++;
            var sb = new StringBuilder();
            while (k < s.Length)
            {
                char c = s[k];
                if (c == '"')
                {
                    if (verbatim && k + 1 < s.Length && s[k + 1] == '"') { sb.Append('"'); k += 2; continue; }
                    k++;
                    i = k;
                    template = sb.ToString();
                    return true;
                }
                if (c == '\\' && !verbatim)
                {
                    if (k + 1 >= s.Length) return false;
                    char n = s[k + 1];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '0': sb.Append('\0'); break;
                        case 'u':
                            if (k + 5 < s.Length) { sb.Append((char)System.Convert.ToInt32(s.Substring(k + 2, 4), 16)); k += 6; continue; }
                            return false;
                        default: sb.Append(n); break;
                    }
                    k += 2;
                    continue;
                }
                if (interp && c == '{')
                {
                    if (k + 1 < s.Length && s[k + 1] == '{') { sb.Append('{'); k += 2; continue; }
                    int start = k + 1;
                    k = start;
                    if (!SkipHole(s, ref k)) return false;
                    string expr = System.Text.RegularExpressions.Regex.Replace(s.Substring(start, k - start), @"\s+", " ").Trim();
                    int idx = holes.IndexOf(expr);
                    if (idx < 0) { idx = holes.Count; holes.Add(expr); }
                    sb.Append('{').Append(idx).Append('}');
                    k++; // 닫는 }
                    continue;
                }
                if (interp && c == '}' && k + 1 < s.Length && s[k + 1] == '}') { sb.Append('}'); k += 2; continue; }
                sb.Append(c);
                k++;
            }
            return false;
        }

        // { 다음부터 짝이 맞는 } 앞까지 건너뛴다 (안쪽 괄호·문자열 포함)
        static bool SkipHole(string s, ref int k)
        {
            int depth = 0;
            while (k < s.Length)
            {
                char c = s[k];
                if (IsLiteralStart(s, k)) { SkipLiteral(s, ref k); continue; }
                if (c == '\'') { SkipChar(s, ref k); continue; }
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == '}') { if (depth == 0) return true; depth--; }
                k++;
            }
            return false;
        }

        static void SkipLiteral(string s, ref int i)
        {
            var dummy = new List<string>();
            int k = i;
            if (ParseLiteral(s, ref k, dummy, out _)) i = k;
            else i++;
        }

        static void SkipChar(string s, ref int i)
        {
            int k = i + 1;
            while (k < s.Length && s[k] != '\'' && s[k] != '\n') { if (s[k] == '\\') k++; k++; }
            i = k + 1;
        }
    }

    // 검색·수정만 보기·쪽 넘김이 있는 인스펙터 (목록이 수백 줄이라)
    [CustomEditor(typeof(UITextSettings))]
    public class UITextSettingsEditor : Editor
    {
        const int PerPage = 40;
        string search = "";
        bool editedOnly;
        int page;

        public override void OnInspectorGUI()
        {
            var s = (UITextSettings)target;
            serializedObject.Update();
            var entries = serializedObject.FindProperty("entries");

            EditorGUILayout.HelpBox("ko / en 을 고치면 게임에 그 문구가 나옵니다. {0} {1} … 은 코드가 채우는 값이에요(옮기거나 지워도 됨). " +
                                    "칸을 비우면 원래 문구로 돌아갑니다. 목록은 스크립트가 컴파일될 때 자동으로 갱신됩니다.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField("검색", search);
            editedOnly = EditorGUILayout.Toggle("고친 문구만", editedOnly);
            if (EditorGUI.EndChangeCheck()) page = 0;

            var shown = new List<int>();
            for (int i = 0; i < s.entries.Count; i++)
            {
                var e = s.entries[i];
                if (editedOnly && !e.Edited) continue;
                if (!string.IsNullOrEmpty(search) && !Has(e.ko, search) && !Has(e.en, search) && !Has(e.origKo, search) && !Has(e.origEn, search) && !Has(e.where, search)) continue;
                shown.Add(i);
            }

            int pages = Mathf.Max(1, (shown.Count + PerPage - 1) / PerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{shown.Count}개 / 전체 {s.entries.Count}개 · 고친 것 {s.entries.Count(e => e.Edited)}개");
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("◀", GUILayout.Width(28))) page--;
                GUILayout.Label($"{page + 1}/{pages}");
                using (new EditorGUI.DisabledScope(page >= pages - 1)) if (GUILayout.Button("▶", GUILayout.Width(28))) page++;
            }

            var small = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            var area = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            for (int n = page * PerPage; n < Mathf.Min(shown.Count, (page + 1) * PerPage); n++)
            {
                int i = shown[n];
                var e = s.entries[i];
                var p = entries.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label((e.Edited ? "● " : "") + e.where, EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(!e.Edited))
                            if (GUILayout.Button("원래대로", EditorStyles.miniButton, GUILayout.Width(60)))
                            {
                                p.FindPropertyRelative("ko").stringValue = e.origKo;
                                p.FindPropertyRelative("en").stringValue = e.origEn;
                            }
                    }
                    if (!string.IsNullOrEmpty(e.args)) GUILayout.Label(e.args, small);
                    Field(p.FindPropertyRelative("ko"), "ko", e.origKo, area);
                    Field(p.FindPropertyRelative("en"), "en", e.origEn, area);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void Field(SerializedProperty prop, string label, string original, GUIStyle area)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(22));
                prop.stringValue = EditorGUILayout.TextArea(prop.stringValue ?? "", area);
            }
            if (!string.IsNullOrEmpty(prop.stringValue) && prop.stringValue != original)
                EditorGUILayout.LabelField(" ", "원문: " + original, EditorStyles.wordWrappedMiniLabel);
        }

        static bool Has(string s, string q) => s != null && s.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

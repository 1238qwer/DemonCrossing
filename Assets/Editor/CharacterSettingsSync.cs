using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // CharacterSettings 에셋을 만들고 GameData 목록과 맞춘다(새 캐릭터 추가, 사라진 캐릭터 제거, 고친 값은 유지).
    [InitializeOnLoad]
    public static class CharacterSettingsSync
    {
        const string Path = "Assets/Resources/CharacterSettings.asset";
        static readonly string[] Heroes = { "hero_warrior", "hero_mage", "hero_archer", "hero_cleric" };
        static readonly string[] HeroNames = { "전사 용사", "마법사 용사", "궁수 용사", "성직자 용사" };

        static CharacterSettingsSync()
        {
            // 에셋이 없으면 처음 한 번 만든다
            EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<CharacterSettings>(Path) == null) Sync(false); };
        }

        [MenuItem("Mawang/Character Settings (이름·크기)")]
        static void Open() => Sync(true);

        static void Sync(bool select)
        {
            var s = AssetDatabase.LoadAssetAtPath<CharacterSettings>(Path);
            bool created = s == null;
            if (created)
            {
                s = ScriptableObject.CreateInstance<CharacterSettings>();
                AssetDatabase.CreateAsset(s, Path);
            }

            var monsters = new List<(string id, string name, string group)>();
            foreach (var m in GameData.Monsters)
            {
                var r = GameData.RegionById(m.region);
                string kind = m.hidden ? "히든" : m.isPlant ? "마력초" : m.sellCurrency == Currency.Gold ? "골드" : "자재";
                monsters.Add((m.id, m.name, $"{r.stage}. {r.name} · {kind}"));
            }
            var staff = new List<(string, string, string)>();
            foreach (var d in GameData.Staff) staff.Add((d.id, d.name, d.role == StaffRole.Hunter ? "포획대원" : "성 관리자"));
            var visitors = new List<(string, string, string)>();
            for (int i = 0; i < Heroes.Length; i++) visitors.Add((Heroes[i], HeroNames[i], "용사 손님"));

            Merge(s.monsters, monsters);
            Merge(s.staff, staff);
            Merge(s.visitors, visitors);

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            if (created) Debug.Log($"[CharacterSettings] 생성: {Path} (괴물 {s.monsters.Count}, 직원 {s.staff.Count}, 손님 {s.visitors.Count})");
            if (select) { Selection.activeObject = s; EditorGUIUtility.PingObject(s); }
        }

        // 데이터 순서를 따르되, 이미 있는 항목의 이름·크기는 그대로 둔다
        static void Merge(List<CharacterSettings.Entry> list, List<(string id, string name, string group)> data)
        {
            var old = new Dictionary<string, CharacterSettings.Entry>();
            foreach (var e in list) if (!string.IsNullOrEmpty(e.id)) old[e.id] = e;
            list.Clear();
            foreach (var (id, name, group) in data)
            {
                if (old.TryGetValue(id, out var e)) { e.group = group; list.Add(e); }
                else list.Add(new CharacterSettings.Entry { id = id, name = name, group = group, scale = 1f });
            }
        }
    }
}

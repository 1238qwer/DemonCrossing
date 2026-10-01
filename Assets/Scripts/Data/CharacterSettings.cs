using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 캐릭터 이름·크기 조정용 에셋 (Assets/Resources/CharacterSettings.asset).
    // 메뉴 Mawang/Character Settings 로 열고 인스펙터에서 고친다. 플레이 중에 고쳐도 바로 반영된다.
    // 목록에 없는 캐릭터는 GameData 의 기본값을 쓴다.
    [CreateAssetMenu(fileName = "CharacterSettings", menuName = "Mawang/Character Settings")]
    public class CharacterSettings : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("화면에 표시되는 이름")] public string name;
            [Tooltip("데이터 id (바꾸지 마세요)")] public string id;
            [Tooltip("분류 (참고용)")] public string group;
            [Tooltip("크기 배율 (1 = 기본)")] [Range(0.25f, 4f)] public float scale = 1f;
        }

        [Header("전체 크기 — 마왕성")]
        [Tooltip("전시 우리 안 괴물")] [Range(0.25f, 4f)] public float castleMonsterScale = 1f;
        [Tooltip("용사 손님")] [Range(0.25f, 4f)] public float castleVisitorScale = 1.5f;
        [Tooltip("성 관리자")] [Range(0.25f, 4f)] public float castleStaffScale = 1f;

        [Header("전체 크기 — 포획장 (배경 도트 기준 배율)")]
        [Range(0.5f, 4f)] public float huntHunterScale = 2f;
        [Range(0.5f, 4f)] public float huntMonsterScale = 2f;

        [Header("괴물 (이름 · 개별 크기)")]
        public List<Entry> monsters = new List<Entry>();

        [Header("직원 (이름 · 개별 크기)")]
        public List<Entry> staff = new List<Entry>();

        [Header("용사 손님 (크기만 적용 — 이름은 화면에 나오지 않음)")]
        public List<Entry> visitors = new List<Entry>();

        // ── 런타임 접근 ────────────────────────────────────────
        static CharacterSettings instance;
        static bool loaded;
        Dictionary<string, float> scales;

        public static CharacterSettings I
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    instance = Resources.Load<CharacterSettings>("CharacterSettings");
                    if (instance == null) instance = CreateInstance<CharacterSettings>(); // 에셋이 없으면 기본값
                }
                return instance;
            }
        }

        // 개별 크기 배율 (목록에 없으면 1)
        public float Scale(string id)
        {
            if (scales == null)
            {
                scales = new Dictionary<string, float>();
                foreach (var list in new[] { monsters, staff, visitors })
                    foreach (var e in list) if (!string.IsNullOrEmpty(e.id)) scales[e.id] = e.scale;
            }
            return scales.TryGetValue(id, out var s) ? s : 1f;
        }

        // 이름 덮어쓰기: GameData 의 괴물·직원 이름을 이 에셋 값으로 바꾼다
        public void ApplyNames()
        {
            foreach (var e in monsters)
                if (!string.IsNullOrEmpty(e.name) && GameData.MonsterById.TryGetValue(e.id ?? "", out var m)) m.name = e.name;
            foreach (var e in staff)
            {
                var d = string.IsNullOrEmpty(e.id) ? null : GameData.StaffById(e.id);
                if (d != null && !string.IsNullOrEmpty(e.name)) d.name = e.name;
            }
        }

        void OnValidate()
        {
            scales = null;
            if (!Application.isPlaying || Game.I == null) return;
            ApplyNames();
            Game.I.MarkDirty(); // UI 다시 그리기
        }
    }
}

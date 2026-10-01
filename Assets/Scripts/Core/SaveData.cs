using System;
using System.Collections.Generic;

namespace Mawang
{
    // 저장되는 상태. ID 기반으로만 참조한다. (JsonUtility 호환: 필드 + List)

    [Serializable]
    public class CountEntry
    {
        public string id;
        public int count;
    }

    [Serializable]
    public class BuildingState
    {
        public int uid;
        public BuildingType type;
        public int floor, x;          // 왼쪽 아래 칸 (x: 0~7, 4 이상 = 오른쪽 날개)
        public int cw = 1, ch = 1;    // 차지하는 칸 수(가로) × 층 수(세로). 전시 우리만 여러 칸을 쓴다
        public int level = 1;
        public int investedGold, investedMat;
        // 전시 우리
        public List<CountEntry> contents = new List<CountEntry>();
        public float accumulated;
        // 연구소
        public int labRp;
        public float labTimer;

        public int Cells => cw * ch;
    }

    [Serializable]
    public class HunterState
    {
        public string staffId;
        public bool dispatched;
        public string regionId = "forest";
        public int bait;
        public Power power;
        public bool autoBait = true;
        public int durabilityLeft;
        public float timer;          // 포획 사이클 진행
        public float restTimer;      // >0 이면 회복/패널티 중
        public bool penalty;         // 수동 복귀 패널티인지
        // 이번 사이클에 잡을 괴물(사이클 시작에 미리 굴린다 → 포획 장면에서 그 괴물이 다가온다)
        public List<string> pending = new List<string>();
        public int pendingBait = -1;
    }

    [Serializable]
    public class TrashItem
    {
        public int uid;
        public string regionId;
        public float x, y;           // 포획 장면 안의 위치(도트)
        public bool can;             // 캔 = 골드, 페트병 = 자재
    }

    [Serializable]
    public class CastleStaffState
    {
        public int uid;
        public string staffId;
        public bool dispatched;
        public int wing = -1;        // 담당 구역: -1 = 전체, 0 = 왼쪽 날개, 1 = 오른쪽 날개
        public int floor = -1;       // 담당 층: -1 = 전체, 0~9
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 3; // 3: 가운데 엘리베이터 · 좌우 날개 10층 · 여러 칸 전시 우리
        public int version = CurrentVersion;
        public int gold, material, rp;       // rp = 보석
        public int floorsLeft = GameData.StartFloorsLeft, floorsRight = GameData.StartFloorsRight;
        public int unlockedFloors;           // (구버전) 층 수 → floorsLeft 로 옮긴다
        public int nextUid = 1;
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<CountEntry> monsters = new List<CountEntry>();     // 보관함
        public List<CountEntry> baits = new List<CountEntry>();
        public List<CountEntry> goods = new List<CountEntry>();        // 음식/기념품 재고
        public List<CountEntry> caught = new List<CountEntry>();       // 도감: 누적 포획 수
        public List<string> collectionClaimed = new List<string>();
        public List<string> research = new List<string>();
        public List<HunterState> hunters = new List<HunterState>();
        public List<CastleStaffState> castleStaff = new List<CastleStaffState>();
        public List<CountEntry> trash = new List<CountEntry>();        // (구버전) 지역별 쓰레기 수 → trashItems 로 옮긴다
        public List<TrashItem> trashItems = new List<TrashItem>();    // 포획 스테이지 바닥에 떨어진 쓰레기
        public int nextTrashUid = 1;
        public long lastDailyTicks;
        public bool tutorialDone;
    }

    public static class CountList
    {
        public static int Get(List<CountEntry> list, string id)
        {
            foreach (var e in list) if (e.id == id) return e.count;
            return 0;
        }

        public static void Add(List<CountEntry> list, string id, int delta)
        {
            foreach (var e in list)
                if (e.id == id)
                {
                    e.count += delta;
                    if (e.count <= 0) list.Remove(e);
                    return;
                }
            if (delta > 0) list.Add(new CountEntry { id = id, count = delta });
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // ─────────────────────────────────────────────────────────────
    // 정적 게임 데이터. 밸런스 조정은 이 파일에서만 한다.
    // 원작(아라드 수족관 메이커) 대응:
    //   물고기 → 괴물, 해초 → 마력초(수질 대신 '항마력' 회복), 어항 → 전시 우리
    //   낚시 직원 → 포획대원, 수족관 직원 → 성 관리자, 낚싯배 내구도 → 체력
    //
    // 밸런스 목표: 약 10시간 플레이.
    //   보석(연구소에서만 천천히 나오는 가장 귀한 재료)이 진행 속도를 정한다.
    //   보석은 연구에만 쓴다(업그레이드 · 건물 연구 · 자원 창고 · 스테이지 탐사). 증축·건설은 골드와 자재.
    //   연구소는 1개만 지을 수 있다: 초반 시간당 12개 → 후반(Lv.5 · 연구 효율) 시간당 ~43개.
    //   골드는 스테이지·층이 늘수록 기하급수로 커지므로 비용도 같은 비율로 커진다.
    // ─────────────────────────────────────────────────────────────

    public enum Currency { Gold, Material }
    public enum Power { Weak, Mid, Strong }
    public enum BuildingType { Tank, Restaurant, Souvenir, Lab, Dorm, Rest, Arena } // 세이브에 숫자로 저장 → 새 건물은 끝에 추가
    public enum BoutResult { GreatFail, Fail, Draw, Win, GreatWin }
    public enum StaffRole { Hunter, Castle }

    public class MonsterDef
    {
        public string id, name, en, region;
        public bool isPlant, hidden;
        public Currency sellCurrency;
        public int sellPrice;
        public int space;
        public int maki;          // 음수 = 항마력 소모, 양수 = 항마력 회복(마력초)
        public float admission;   // 분당 관람료
        public int power;         // 투기장 전투력 (마력초는 0 = 대련 불가)
        public Color color;
        public string Name => L.En && !string.IsNullOrEmpty(en) ? en : name;
    }

    public class RegionDef
    {
        public string id, name, en;
        public string unlockResearch; // null = 기본 개방
        // 미끼 × 힘 → 등장 괴물 id 목록 (히든은 별도)
        public string[,][] pools;
        public string hiddenId;
        public int hiddenBait;
        public Power hiddenPower;
        // 포획 장면 연출
        public int stage;             // 1~10
        public string bg;             // 배경 스프라이트 (StagePainter)
        public Color ambient;         // 떠다니는 입자 색
        public Vector2 ambientVel;    // 입자 속도(도트/초)
        public string desc, descEn;
        public string Name => L.En ? en : name;
        public string Desc => L.En ? descEn : desc;
    }

    public class BaitDef
    {
        public string id, name, en; public int price;
        public string Name => L.En ? en : name;
    }

    public class ShopItemDef
    {
        public string id, name, en;
        public BuildingType shop;
        public int tier;          // 필요한 가게 레벨
        public int buyPrice, sellPrice;
        public string Name => L.En ? en : name;
    }

    public class BuildingDef
    {
        public BuildingType type;
        public string name, en, icon;
        public int costGold, costMat; // 전시 우리는 칸당
        public int maxLevel;
        public int maxCount;      // 0 = 제한 없음
        public int[] upGold, upMat;   // index = 현재 레벨-1 → 다음 레벨 비용 (전시 우리는 칸당)
        public Color color;
        public string desc, descEn;
        public string Name => L.En ? en : name;
        public string Desc => L.En ? descEn : desc;
    }

    public class StaffDef
    {
        public string id, name, en;
        public StaffRole role;
        public int hireGold;      // 고용비 (성 관리자는 여러 명 고용할수록 비싸진다)
        // 포획대원
        public float interval; public int amount; public int durability;
        // 성 관리자: 특화 건물로 갈 때 더 빠르고, 그 일을 더 빨리 끝낸다
        public float moveSpeed; public float foodBonus, souvenirBonus, admissionBonus;
        public BuildingType specialty;
        public string Name => L.En ? en : name;
    }

    public class ResearchDef
    {
        public string id, name, en, desc, descEn, icon;
        public int rp;            // 연구 비용은 보석만
        public string requires;
        public string Name => L.En ? en : name;
        public string Desc => L.En ? descEn : desc;
    }

    // 10단계 업그레이드 라인. 단계 n 은 research 목록에 "{id}_{n}" 로 저장된다(1단계를 끝내야 2단계가 열린다).
    public class UpgradeLine
    {
        public string id, name, en, icon;
        public int category;
        public int maxLevel = 10;
        public int rp0;           // 연구 비용은 보석만
        public System.Func<int, string> effect;   // 누적 효과 설명 (레벨 n)
        public string Name => L.En ? en : name;

        public string TierId(int lv) => $"{id}_{lv}";
        public int Rp(int lv) => Mathf.RoundToInt(rp0 * Mathf.Pow(GameData.UpgradeRpGrowth, lv - 1));
    }

    // 업적: progress(게임) 가 target 이상이면 달성 → 골드 자동 지급
    public class AchievementDef
    {
        public string id, name, en, desc, descEn;
        public long target;
        public int gold;
        public System.Func<Game, long> progress;
        public string Name => L.En ? en : name;
        public string Desc => L.En ? descEn : desc;
    }

    public static class GameData
    {
        // ── 공통 수치 ───────────────────────────────────────────
        // 반복 골드 수입 배율: 가게 판매(매입가·판매가)·관람료·골드 괴물 판매가·쓰레기 캔·휴게실에 곱한다.
        // 업적·목표·도감 같은 1회성 보상과 투기장(보상 계수에 이미 반영)은 제외.
        public const float GoldIncomeMul = 0.5f;

        public const int StartGold = 5000;
        public const int StartMaterial = 5000;
        public const int StartGems = 2;

        // 마왕성: 가운데 엘리베이터, 왼쪽·오른쪽 날개에 4칸씩, 최대 10층.
        // 칸 번호 x: 0~3 = 왼쪽 날개(0이 가장 왼쪽), 4~7 = 오른쪽 날개.
        public const int WingWidth = 4;
        public const int TotalWidth = WingWidth * 2;
        public const int MaxFloors = 10;
        public const int StartFloorsLeft = 2, StartFloorsRight = 0;
        public const float ElevatorCells = 0.2f;      // 엘리베이터 폭(칸 단위, 32/160 px)

        // 성 증축: n층을 여는 비용 (왼쪽·오른쪽 날개 따로). 보석은 연구에만 쓰므로 골드·자재만 (보석 대신 25% 비싸다)
        public static int ExpandGold(int floor) => Mathf.RoundToInt(3750 * Mathf.Pow(1.75f, floor - 1) / 100f) * 100;
        public static int ExpandMat(int floor) => ExpandGold(floor) / 2;

        // 자원 최대 보유량 (연구 '자원 창고' n단계). 넘으면 자동 수입(가게 판매·전시 우리·투기장·연구소)이 멈춘다.
        // 골드 90만 → 10단계 약 9,900만 (상점 Lv.10 업그레이드 ~137만을 감당), 자재도 같은 한도.
        // 보석은 10개에서 단계마다 +10, 최대 50개 (가장 비싼 연구 = 스테이지 10 탐사 50개).
        public static int GoldCapAt(int lv) => Mathf.RoundToInt(30000 * Mathf.Pow(2f, lv) / 1000f) * 1000; // 90만 → 10단계 약 9,900만
        public const int GemCapBase = 10, GemCapPerLevel = 10, GemCapMax = 50;
        public static int GemCapAt(int lv) => Mathf.Min(GemCapMax, GemCapBase + GemCapPerLevel * lv);

        // 건물 연구 n단계 → 모든 건물을 Lv.(n+1)까지 올릴 수 있다. 투기장은 3단계부터 짓는다.
        public const int ArenaResearchLevel = 3;

        // 전시 우리 크기 = 칸 수 × (레벨 + 1)
        public const int TankSpacePerSize = 5;
        public const int TankMakiPerSize = 4;
        public const int TankCapPerSize = 2500;      // 원작: 칸당 2,500 누적 한도
        public const float TankCapMinutes = 6f;      // 관람료가 많으면 최소 6분치는 쌓인다
        public const int TankStaffCollectThreshold = 300;

        public const float HunterRestTime = 60f;     // 원작: 내구도 0 → 60초 회복
        public const float HunterRecallPenalty = 50f; // 원작: 복귀 시 약 50초 패널티

        // 보석: 가장 귀한 재료. 연구소 Lv.1 은 5분에 1개. 연구소 안에는 1개까지만 쌓인다(수거해야 다음 보석을 만든다).
        public const int LabRpCap = 1;
        public static readonly float[] LabInterval = { 300f, 255f, 215f, 180f, 150f };

        // 휴게실: 매일 06시 건물당 1,000골드(2,000 × 반복 수입 배율) × 1.3^(열린 층-2)
        public const float RestGoldPerFloor = 1.3f;
        public static int RestDailyGold(int floorsOpen) => Mathf.RoundToInt(2000 * GoldIncomeMul * Mathf.Pow(RestGoldPerFloor, Mathf.Max(0, floorsOpen - 2)) / 100f) * 100;
        public const int RestDailyHour = 6;

        // 전시 우리 추가 관람료·투기장 대련 확률 등 손님 행동 수치는 GuestSettings 에셋(Mawang/Guest Settings)에 있다.

        // 투기장: 1개만 짓는 대신 보상이 크다. 용사가 오면 가장 강한 몬스터가 대련한다.
        public const float ArenaHeroX = 0.34f, ArenaMonsterX = 0.66f; // 투기장 칸 안에서 용사·괴물이 서는 자리 (칸 폭 대비)
        public const float ArenaLevelBonus = 0.25f;    // 레벨당 보상 +25% (Lv.10 = ×3.25)
        public const float ArenaRewardPerPower = 0.3f; // 성공 보상 = 전투력 × 0.3 × 레벨 배율 (대련 1회 기대값 ≈ 가게 판매 3건)
        public static readonly float[] ArenaRewardMul = { 0f, 0f, 0.3f, 1f, 3f }; // 대실패·실패·무승부·성공·대성공

        public const float TrashInterval = 20f;          // 스테이지마다 20초에 하나씩 떨어진다

        // 포획 장면 크기(도트). 배경 = StageW×StageH, 땅 윗면 = StageGroundY
        public const int StageW = 276, StageH = 108, StageGroundY = 34;
        public const float TrashMinX = 24f;
        public const int TrashMaxPerRegion = 5;
        public const int TrashReward = 20;           // 원작: 페트병 자재 20 / 캔 20골드 (캔 골드는 반복 수입 배율을 곱한다)

        public const int CollectionRegisterCount = 30;
        public static int CollectionGold(MonsterDef m) => 500 * GameData.RegionById(m.region).stage * (m.hidden ? 3 : 1);
        public static int CollectionGems(MonsterDef m) => m.hidden ? 5 : 1;

        // 손님 수·소지금·행동 확률은 GuestSettings 에셋(Mawang/Guest Settings)에서 조정한다.

        public const float WalkSpeed = 0.5f;         // 칸/초 (1칸 = 방 하나)
        public const float ElevatorSecPerFloor = 0.8f;
        public const float ServiceTime = 1f;
        public const float SpecialtySpeed = 1.35f;   // 특화 건물로 갈 때 이동 속도
        public const float OffSpecialtySpeed = 0.9f;
        public const float SpecialtyWork = 0.6f;     // 특화 건물 작업 시간 배율

        // 직원 한도: 처음 2명(포획대원 1 + 성 관리자 1). 직원 숙소는 Lv.2부터 레벨마다 +1 (Lv.5 = 6명)
        public const int BaseStaffCap = 2;

        // ── 미끼 ───────────────────────────────────────────────
        public static readonly BaitDef[] Baits =
        {
            new BaitDef { id = "bait_worm",  name = "지옥 벌레", en = "Hell Worm",   price = 5 },
            new BaitDef { id = "bait_meat",  name = "생고기",    en = "Raw Meat",    price = 10 },
            new BaitDef { id = "bait_candy", name = "마력 사탕", en = "Mana Candy",  price = 15 },
            new BaitDef { id = "bait_bone",  name = "썩은 뼈",   en = "Rotten Bone", price = 8 },
        };

        public static string PowerName(int p) => L.En ? new[] { "Low", "Mid", "High" }[p] : new[] { "약", "중", "강" }[p];

        // ── 괴물 ─────────────────────────────────────────────
        public static readonly List<MonsterDef> Monsters = new List<MonsterDef>();
        public static readonly Dictionary<string, MonsterDef> MonsterById = new Dictionary<string, MonsterDef>();
        public static readonly List<RegionDef> Regions = new List<RegionDef>();

        static MonsterDef M(string id, string name, string en, string region, Currency cur, int price, int space, int maki, float adm, Color c, bool plant = false, bool hidden = false)
        {
            // 반복 골드 수입 배율: 관람료와 골드 괴물 판매가에 곱한다 (자재 괴물은 그대로)
            if (cur == Currency.Gold) price = Mathf.Max(1, Mathf.RoundToInt(price * GoldIncomeMul));
            var m = new MonsterDef { id = id, name = name, en = en, region = region, sellCurrency = cur, sellPrice = price, space = space, maki = maki, admission = adm * GoldIncomeMul, color = c, isPlant = plant, hidden = hidden };
            Monsters.Add(m);
            MonsterById[id] = m;
            return m;
        }

        // 원작: 지역당 골드 물고기 3 + 자재 물고기 3 + 골드 해초 1 + 자재 해초 1 (+ 히든)
        // 풀 배치 규칙: [bait, power] → 인덱스 (0~2 골드, 3~5 자재, 6 골드 마력초, 7 자재 마력초)
        static readonly int[,][] PoolLayout =
        {
            { new[] { 0, 3, 6 }, new[] { 0, 3, 7 }, new[] { 1, 3 } },   // 벌레
            { new[] { 1, 4 },    new[] { 1, 4, 6 }, new[] { 2, 4 } },   // 생고기
            { new[] { 0, 1, 7 }, new[] { 2, 5 },    new[] { 2, 5, 7 } },// 마력 사탕
            { new[] { 3, 4, 6 }, new[] { 5, 6, 7 }, new[] { 4, 5 } },   // 썩은 뼈
        };

        static RegionDef Region(string id, string name, string en, string unlock, MonsterDef[] species, MonsterDef hidden, int hiddenBait, Power hiddenPower, int baitShift)
        {
            var r = new RegionDef { id = id, name = name, en = en, unlockResearch = unlock, hiddenId = hidden.id, hiddenBait = hiddenBait, hiddenPower = hiddenPower,
                stage = Regions.Count + 1, bg = $"stage_{id}" };
            int baits = Baits.Length;
            r.pools = new string[baits, 3][];
            for (int b = 0; b < baits; b++)
                for (int p = 0; p < 3; p++)
                {
                    var idx = PoolLayout[(b + baitShift) % baits, p];
                    var ids = new string[idx.Length];
                    for (int i = 0; i < idx.Length; i++) ids[i] = species[idx[i]].id;
                    r.pools[b, p] = ids;
                }
            Regions.Add(r);
            return r;
        }

        // ── 건물 ───────────────────────────────────────────────
        // 레벨업 비용표: base × growth^(레벨-1)
        // 업그레이드 비용 배율 (건설비는 그대로)
        public const int UpgradeCostMul = 5, DormUpgradeCostMul = 10;
        static int[] Up(int[] a, int mul = UpgradeCostMul)
        {
            var r = new int[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[i] * mul;
            return r;
        }

        static int[] Curve(int levels, float baseCost, float growth)
        {
            var a = new int[Mathf.Max(0, levels - 1)];
            for (int i = 0; i < a.Length; i++) a[i] = Mathf.RoundToInt(baseCost * Mathf.Pow(growth, i) / 100f) * 100;
            return a;
        }

        static int[] Half(int[] a)
        {
            var r = new int[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[i] / 2;
            return r;
        }

        public const int ShopMaxLevel = 10;

        public static readonly Dictionary<BuildingType, BuildingDef> Buildings = new Dictionary<BuildingType, BuildingDef>
        {
            [BuildingType.Tank] = new BuildingDef { type = BuildingType.Tank, icon = "room_tank", name = "전시 우리", en = "Exhibit Cage", costGold = 2000, costMat = 1000, maxLevel = 5,
                upGold = Up(Curve(5, 1500, 2f)), upMat = Up(Curve(5, 1500, 2f)), color = new Color(0.25f, 0.35f, 0.6f),
                desc = "괴물을 전시해 관람료를 쌓는다. 끌어서 여러 칸을 하나로 지을 수 있다.",
                descEn = "Shows monsters for admission fees. Drag to build one cage over many cells." },
            [BuildingType.Restaurant] = new BuildingDef { type = BuildingType.Restaurant, icon = "room_restaurant", name = "식당", en = "Diner", maxCount = 4, costGold = 1500, costMat = 500, maxLevel = ShopMaxLevel,
                upGold = Up(Curve(ShopMaxLevel, 2000, 1.85f)), upMat = Up(Half(Curve(ShopMaxLevel, 2000, 1.85f))), color = new Color(0.65f, 0.35f, 0.2f),
                desc = "용사 손님에게 음식을 판다. 레벨이 오를수록 비싼 메뉴(10단계)를 판다.",
                descEn = "Sells food to hero guests. Higher levels unlock pricier dishes (10 tiers)." },
            [BuildingType.Souvenir] = new BuildingDef { type = BuildingType.Souvenir, icon = "room_souvenir", name = "기념품점", en = "Gift Shop", maxCount = 4, costGold = 1500, costMat = 500, maxLevel = ShopMaxLevel,
                upGold = Up(Curve(ShopMaxLevel, 2000, 1.85f)), upMat = Up(Half(Curve(ShopMaxLevel, 2000, 1.85f))), color = new Color(0.55f, 0.25f, 0.5f),
                desc = "용사 손님에게 기념품을 판다. 레벨이 오를수록 비싼 상품(10단계)을 판다.",
                descEn = "Sells souvenirs to hero guests. Higher levels unlock pricier goods (10 tiers)." },
            [BuildingType.Lab] = new BuildingDef { type = BuildingType.Lab, icon = "room_lab", name = "흑마법 연구소", en = "Dark Magic Lab", maxCount = 1, costGold = 1000, costMat = 1000, maxLevel = 5,
                upGold = Up(Curve(5, 4000, 2.2f)), upMat = Up(Curve(5, 4000, 2.2f)), color = new Color(0.2f, 0.5f, 0.45f),
                desc = "가장 귀한 재료인 보석을 아주 천천히 만든다. 탭하거나 관리자가 수거한다.",
                descEn = "Slowly makes Gems, the rarest resource. Tap or let staff collect them." },
            [BuildingType.Dorm] = new BuildingDef { type = BuildingType.Dorm, icon = "room_dorm", name = "직원 숙소", en = "Staff Dorm", maxCount = 1, costGold = 2000, costMat = 2000, maxLevel = 5,
                upGold = Up(Curve(5, 3000, 2f), DormUpgradeCostMul), upMat = Up(Curve(5, 3000, 2f), DormUpgradeCostMul), color = new Color(0.45f, 0.45f, 0.3f),
                desc = "Lv.2부터 레벨마다 직원 고용 한도 +1.",
                descEn = "Each level adds +1 staff capacity." },
            [BuildingType.Rest] = new BuildingDef { type = BuildingType.Rest, icon = "room_rest", name = "휴게실", en = "Lounge", maxCount = 8, costGold = 1000, costMat = 1000, maxLevel = 1,
                upGold = new int[0], upMat = new int[0], color = new Color(0.35f, 0.5f, 0.3f),
                desc = "매일 06시에 휴게실 1개당 지원금. 열린 층이 많을수록 많이 준다.",
                descEn = "Pays a daily bonus per lounge at 06:00. More floors, bigger bonus." },
            [BuildingType.Arena] = new BuildingDef { type = BuildingType.Arena, icon = "room_arena", name = "투기장", en = "Arena", maxCount = 1, costGold = 3000, costMat = 1500, maxLevel = ShopMaxLevel,
                upGold = Up(Curve(ShopMaxLevel, 2500, 1.85f)), upMat = Up(Half(Curve(ShopMaxLevel, 2500, 1.85f))), color = new Color(0.6f, 0.3f, 0.25f),
                desc = "용사가 몬스터와 대련한다. 가장 강한 몬스터부터 나서고, 레벨이 오를수록 보상이 커진다.",
                descEn = "Heroes spar with your monsters. The strongest fights first; higher levels pay more." },
        };

        // ── 음식 / 기념품: 각 10단계. 매입가 = 10 × 1.75^(단계-1), 판매가 = 매입가 × 2.5 ──
        public static readonly ShopItemDef[] ShopItems = MakeItems();

        static ShopItemDef[] MakeItems()
        {
            string[] foodKo = { "해골 수프", "마왕 스테이크", "드래곤 다리 구이", "독버섯 리조또", "용암 피자", "서리 젤라토", "심연 초밥", "흑마법 케이크", "마왕의 와인", "혼돈 코스요리" };
            string[] foodEn = { "Skull Soup", "Demon Steak", "Roast Dragon Leg", "Toadstool Risotto", "Lava Pizza", "Frost Gelato", "Abyss Sushi", "Hex Cake", "Demon Lord's Wine", "Chaos Full Course" };
            string[] giftKo = { "마왕 열쇠고리", "박쥐 인형", "마왕 망토", "해골 머그컵", "마법 수정구", "용의 알 모형", "저주받은 거울", "마검 레플리카", "마왕 왕관", "마왕성 미니어처" };
            string[] giftEn = { "Demon Keychain", "Bat Plush", "Demon Cape", "Skull Mug", "Crystal Ball", "Dragon Egg Replica", "Cursed Mirror", "Replica Sword", "Demon Crown", "Castle Miniature" };
            var list = new List<ShopItemDef>();
            for (int t = 1; t <= 10; t++)
            {
                float b = 10 * Mathf.Pow(1.75f, t - 1) * GoldIncomeMul; // 매입가·판매가 모두 반복 수입 배율 → 이익도 같은 비율
                list.Add(new ShopItemDef { id = $"food_{t}", name = foodKo[t - 1], en = foodEn[t - 1], shop = BuildingType.Restaurant, tier = t, buyPrice = Mathf.Max(1, Mathf.RoundToInt(b)), sellPrice = Mathf.RoundToInt(b * 2.5f) });
            }
            for (int t = 1; t <= 10; t++)
            {
                float b = 8 * Mathf.Pow(1.75f, t - 1) * GoldIncomeMul;
                list.Add(new ShopItemDef { id = $"gift_{t}", name = giftKo[t - 1], en = giftEn[t - 1], shop = BuildingType.Souvenir, tier = t, buyPrice = Mathf.Max(1, Mathf.RoundToInt(b)), sellPrice = Mathf.RoundToInt(b * 2.6f) });
            }
            return list.ToArray();
        }

        // ── 직원 ───────────────────────────────────────────────
        // 성 관리자 특화: 스켈레톤 = 전시 우리, 서큐버스 = 식당, 가고일 = 기념품점, 마녀 = 연구소
        public static readonly StaffDef[] Staff =
        {
            new StaffDef { id = "hunter_imp",  name = "임프 포획꾼",  en = "Imp Catcher",    role = StaffRole.Hunter, interval = 10f, amount = 1, durability = 20, hireGold = 0 },
            new StaffDef { id = "hunter_orc",  name = "오크 사냥꾼",  en = "Orc Hunter",     role = StaffRole.Hunter, interval = 14f, amount = 2, durability = 15, hireGold = 15000 },
            new StaffDef { id = "hunter_lich", name = "리치 소환사",  en = "Lich Summoner",  role = StaffRole.Hunter, interval = 8f,  amount = 1, durability = 30, hireGold = 60000 },
            new StaffDef { id = "castle_skel",  name = "스켈레톤 집사", en = "Skeleton Butler", role = StaffRole.Castle, moveSpeed = 1.0f,  specialty = BuildingType.Tank,       admissionBonus = 0.05f, hireGold = 5000 },
            new StaffDef { id = "castle_succ",  name = "서큐버스 점원", en = "Succubus Clerk",  role = StaffRole.Castle, moveSpeed = 1.15f, specialty = BuildingType.Restaurant, foodBonus = 0.10f, souvenirBonus = 0.03f, hireGold = 5000 },
            new StaffDef { id = "castle_garg",  name = "가고일 경비",  en = "Gargoyle Guard",  role = StaffRole.Castle, moveSpeed = 0.9f,  specialty = BuildingType.Souvenir,   foodBonus = 0.02f, souvenirBonus = 0.12f, admissionBonus = 0.03f, hireGold = 5000 },
            new StaffDef { id = "castle_witch", name = "마녀 연구원",  en = "Witch Researcher", role = StaffRole.Castle, moveSpeed = 1.05f, specialty = BuildingType.Lab,        hireGold = 5000 },
        };
        public static readonly string[] StartStaff = { "hunter_imp", "castle_skel" };
        public const float StaffHireGrowth = 1.8f;   // 성 관리자를 한 명 더 고용할 때마다 고용비 배율

        // ── 연구: 스테이지 탐사 (1회성) ─────────────────────────
        static readonly int[] ExploreGems = { 0, 0, 3, 5, 8, 12, 17, 23, 30, 40, 50 };

        public static readonly ResearchDef[] Research =
        {
            Explore(2, "용암 동굴", "Lava Cave"), Explore(3, "서리 빙굴", "Frost Cavern"), Explore(4, "독안개 늪", "Toxic Swamp"),
            Explore(5, "망자의 묘지", "Graveyard"), Explore(6, "모래 유적", "Sand Ruins"), Explore(7, "수정 광산", "Crystal Mine"),
            Explore(8, "폭풍 봉우리", "Storm Peak"), Explore(9, "심연 해구", "Abyssal Trench"), Explore(10, "혼돈의 균열", "Rift of Chaos"),
        };

        // 스테이지 탐사: 앞 스테이지를 열어야 다음을 열 수 있다
        static ResearchDef Explore(int stage, string name, string en) => new ResearchDef
        {
            id = $"region_{stage}", name = $"{name} 탐사", en = $"Explore {en}",
            desc = $"포획 스테이지 {stage} '{name}' 개방", descEn = $"Unlocks hunting stage {stage}: {en}", icon = "up_region",
            rp = Mathf.Max(1, ExploreGems[stage]),
            requires = stage > 2 ? $"region_{stage - 1}" : null,
        };

        // ── 연구: 10단계 업그레이드 ────────────────────────────
        public const float UpgradeRpGrowth = 1.25f;

        // shortName: 폰 연구 탭처럼 좁은 곳 (영어만 줄인다)
        public static string CategoryName(int i, bool shortName = false) => L.En
            ? (shortName ? new[] { "Hunt", "Exhibits", "Guests", "Ops" } : new[] { "Hunting", "Exhibits", "Guests & Shops", "Operations" })[i]
            : new[] { "포획", "전시", "손님 · 상점", "운영" }[i];
        public const int CategoryCount = 4;

        // effect(n): n단계일 때의 효과. 0단계(연구 전)는 지금 기본값을 설명한다.
        public static readonly UpgradeLine[] Upgrades =
        {
            new UpgradeLine { id = "speed",    name = "포획 속도",    en = "Hunt Speed",     icon = "up_speed",    category = 0, rp0 = 1, effect = n => n == 0
                ? L.T("포획 주기 기본 (포획대원마다 8~14초)", "Base hunt cycle (8–14s per hunter)")
                : L.T($"포획 주기 -{n * 4}%", $"Hunt cycle -{n * 4}%") },
            new UpgradeLine { id = "dura",     name = "탐험대 체력",  en = "Hunter Stamina", icon = "up_dura",     category = 0, rp0 = 1, effect = n => n == 0
                ? L.T("포획대원 기본 체력 (15~30, 포획 1회에 1 소모)", "Base hunter stamina (15–30, -1 per catch)")
                : L.T($"포획대원 체력 +{n * 3}", $"Hunter stamina +{n * 3}") },
            new UpgradeLine { id = "rest",     name = "빠른 회복",    en = "Quick Recovery", icon = "up_rest",     category = 0, rp0 = 1, effect = n => n == 0
                ? L.T($"체력 회복 {HunterRestTime:0}초", $"Recovery {HunterRestTime:0}s")
                : L.T($"체력 회복 {RestTimeAt(n):0}초 (기본 {HunterRestTime:0}초)", $"Recovery {RestTimeAt(n):0}s (base {HunterRestTime:0}s)") },
            new UpgradeLine { id = "adm",      name = "관람료 인상",  en = "Ticket Price",   icon = "up_adm",      category = 1, rp0 = 2, effect = n => n == 0
                ? L.T("관람료 기본 (괴물마다 다름)", "Base admission (varies by monster)")
                : L.T($"관람료 +{n * 10}%", $"Admission +{n * 10}%") },
            new UpgradeLine { id = "cap",      name = "우리 금고",    en = "Cage Vault",     icon = "up_cap",      category = 1, rp0 = 1, effect = n => n == 0
                ? L.T($"관람료 누적 한도 기본 (우리 크기당 {TankCapPerSize:N0} 또는 {TankCapMinutes:0}분치)", $"Base fee storage ({TankCapPerSize:N0} per cage size or {TankCapMinutes:0} min)")
                : L.T($"관람료 누적 한도 +{n * 15}%", $"Fee storage +{n * 15}%") },
            new UpgradeLine { id = "maki",     name = "항마력 강화",  en = "Ward Boost",     icon = "up_maki",     category = 1, rp0 = 1, effect = n => n == 0
                ? L.T($"전시 우리 크기당 항마력 {TankMakiPerSize}", $"Ward {TankMakiPerSize} per cage size")
                : L.T($"전시 우리 크기당 항마력 {TankMakiPerSize + n} (+{n})", $"Ward {TankMakiPerSize + n} per cage size (+{n})") },
            new UpgradeLine { id = "visitor",  name = "손님 유치",    en = "Advertising",    icon = "up_visitor",  category = 2, rp0 = 2, effect = n => n == 0
                ? L.T($"기본 최대 손님 {GuestSettings.I.maxBase:0}명 + 방문 가능 칸당 {GuestSettings.I.maxPerVisitableCell:0.#}명", $"Base max guests {GuestSettings.I.maxBase:0} + {GuestSettings.I.maxPerVisitableCell:0.#} per visitable cell")
                : L.T($"손님 방문 빈도 +{n * 12}% · 최대 손님 +{n * GuestSettings.I.maxPerResearchLevel}명", $"Guest rate +{n * 12}% · Max guests +{n * GuestSettings.I.maxPerResearchLevel}") },
            new UpgradeLine { id = "wallet",   name = "용사 지갑",    en = "Rich Heroes",    icon = "up_wallet",   category = 2, rp0 = 1, effect = n => n == 0
                ? L.T($"용사 기본 소지금 {GuestSettings.I.walletMin}~{GuestSettings.I.walletMax}골드 (열린 층마다 ×{GuestSettings.I.walletPerFloor:0.##})", $"Base hero wallet {GuestSettings.I.walletMin}–{GuestSettings.I.walletMax} Gold (×{GuestSettings.I.walletPerFloor:0.##} per floor)")
                : L.T($"용사 소지금 +{n * 20}%", $"Hero wallets +{n * 20}%") },
            new UpgradeLine { id = "patience", name = "친절 교육",    en = "Hospitality",    icon = "up_patience", category = 2, rp0 = 1, effect = n => n == 0
                ? L.T($"손님 대기 시간 {GuestSettings.I.patience:0.#}초", $"Guest patience {GuestSettings.I.patience:0.#}s")
                : L.T($"손님 대기 시간 {GuestSettings.I.patience + n * 1.5f:0.#}초 (+{n * 1.5f:0.#}초)", $"Guest patience {GuestSettings.I.patience + n * 1.5f:0.#}s (+{n * 1.5f:0.#}s)") },
            new UpgradeLine { id = "sales",    name = "상술",         en = "Salesmanship",   icon = "up_sales",    category = 2, rp0 = 2, effect = n => n == 0
                ? L.T("음식·기념품 기본 판매가 (상품 목록 가격)", "Base food & gift prices (as listed)")
                : L.T($"음식·기념품 판매가 +{n * 5}%", $"Food & gift prices +{n * 5}%") },
            new UpgradeLine { id = "lab",      name = "연구 효율",    en = "Lab Efficiency", icon = "up_lab",      category = 3, rp0 = 1, effect = n => n == 0
                ? L.T($"보석 생산 기본 속도 (연구소 Lv.1: {LabInterval[0] / 60f:0}분에 1개)", $"Base Gem output (Lab Lv.1: 1 per {LabInterval[0] / 60f:0} min)")
                : L.T($"보석 생산 속도 +{n * 8}%", $"Gem output +{n * 8}%") },
            new UpgradeLine { id = "trash",    name = "쓰레기 재활용", en = "Recycling",     icon = "up_trash",    category = 3, rp0 = 1, effect = n => n == 0
                ? L.T($"쓰레기 하나에 캔 {TrashReward * GoldIncomeMul:0}골드 · 병 자재 {TrashReward}", $"Per trash: can {TrashReward * GoldIncomeMul:0} Gold · bottle {TrashReward} Mat")
                : L.T($"쓰레기 수거 보상 +{n * 50}%", $"Trash rewards +{n * 50}%") },
            new UpgradeLine { id = "build",    name = "건물 연구",    en = "Architecture",   icon = "up_build",    category = 3, rp0 = 2, maxLevel = 9, effect = n => L.T($"건물 Lv.{n + 1}까지 업그레이드{(n >= ArenaResearchLevel ? " · 투기장 개방" : "")}", $"Buildings up to Lv.{n + 1}{(n >= ArenaResearchLevel ? " · Arena unlocked" : "")}") },
            new UpgradeLine { id = "storage",  name = "자원 창고",    en = "Storehouse",     icon = "up_storage",  category = 3, rp0 = 1, effect = n => L.T($"최대 보유 골드·자재 {GoldCapAt(n):N0} · 보석 {GemCapAt(n)}", $"Max Gold & Mat {GoldCapAt(n):N0} · Gems {GemCapAt(n)}") },
        };

        public static float RestTimeAt(int lv) => Mathf.Max(10f, HunterRestTime - lv * 5f);

        public static UpgradeLine UpgradeById(string id)
        {
            foreach (var u in Upgrades) if (u.id == id) return u;
            return null;
        }

        public static ResearchDef ResearchById(string id)
        {
            foreach (var r in Research) if (r.id == id) return r;
            return null;
        }

        public static StaffDef StaffById(string id)
        {
            foreach (var s in Staff) if (s.id == id) return s;
            return null;
        }

        public static ShopItemDef ShopItemById(string id)
        {
            foreach (var s in ShopItems) if (s.id == id) return s;
            return null;
        }

        public static RegionDef RegionById(string id)
        {
            foreach (var r in Regions) if (r.id == id) return r;
            return null;
        }

        static GameData()
        {
            const string R1 = "forest", R2 = "lava";
            Color pl = new Color(0.35f, 0.75f, 0.35f);

            var forest = new[]
            {
                M("m_slime",    "슬라임",     "Slime",            R1, Currency.Gold,     30,  1, -1, 6,  new Color(0.4f, 0.8f, 0.5f)),
                M("m_bat",      "흡혈 박쥐",   "Vampire Bat",      R1, Currency.Gold,     60,  1, -2, 12, new Color(0.4f, 0.3f, 0.5f)),
                M("m_mushroom", "버섯 요정",   "Mushroom Sprite",  R1, Currency.Gold,     120, 2, -3, 24, new Color(0.9f, 0.4f, 0.4f)),
                M("m_skeleton", "해골 병사",   "Skeleton Soldier", R1, Currency.Material, 30,  1, -1, 5,  new Color(0.85f, 0.85f, 0.8f)),
                M("m_treant",   "트렌트 새싹", "Treant Sapling",   R1, Currency.Material, 60,  2, -2, 10, new Color(0.45f, 0.35f, 0.2f)),
                M("m_werewolf", "늑대인간",    "Werewolf",         R1, Currency.Material, 120, 2, -4, 22, new Color(0.5f, 0.45f, 0.45f)),
                M("p_toadstool","독버섯 군락", "Toadstool Patch",  R1, Currency.Gold,     20,  1, 4,  1,  pl, plant: true),
                M("p_vine",     "저주 덩굴",   "Cursed Vine",      R1, Currency.Material, 20,  1, 4,  1,  pl * 0.8f, plant: true),
            };
            var crow = M("m_crowking", "그림자 까마귀왕", "Shadow Crow King", R1, Currency.Gold, 500, 2, -3, 60, new Color(0.15f, 0.1f, 0.2f), hidden: true);
            Region(R1, "저주받은 숲", "Cursed Forest", null, forest, crow, 3, Power.Strong, 0);

            var lava = new[]
            {
                M("m_imp",       "파이어 임프",  "Fire Imp",        R2, Currency.Gold,     80,  1, -2, 15, new Color(1f, 0.45f, 0.2f)),
                M("m_golem",     "용암 골렘",    "Lava Golem",      R2, Currency.Gold,     160, 2, -3, 30, new Color(0.6f, 0.25f, 0.1f)),
                M("m_salamander","샐러맨더",     "Salamander",      R2, Currency.Gold,     300, 2, -5, 55, new Color(0.95f, 0.3f, 0.1f)),
                M("m_spider",    "흑요석 거미",  "Obsidian Spider", R2, Currency.Material, 80,  1, -2, 14, new Color(0.2f, 0.2f, 0.25f)),
                M("m_worm",      "마그마 웜",    "Magma Worm",      R2, Currency.Material, 160, 2, -3, 28, new Color(0.8f, 0.35f, 0.3f)),
                M("m_hellhound", "헬하운드",     "Hellhound",       R2, Currency.Material, 300, 3, -6, 52, new Color(0.35f, 0.1f, 0.1f)),
                M("p_firemoss",  "불꽃 이끼",    "Flame Moss",      R2, Currency.Gold,     50,  1, 6,  2,  new Color(0.9f, 0.6f, 0.2f), plant: true),
                M("p_sulfur",    "유황 수정",    "Sulfur Crystal",  R2, Currency.Material, 50,  1, 6,  2,  new Color(0.9f, 0.9f, 0.3f), plant: true),
            };
            var drake = M("m_drake", "새끼 드래곤", "Baby Dragon", R2, Currency.Gold, 1200, 3, -6, 150, new Color(0.7f, 0.1f, 0.2f), hidden: true);
            Region(R2, "용암 동굴", "Lava Cave", "region_2", lava, drake, 0, Power.Strong, 2);

            // 스테이지 3~10: 이름 [골드 소·중·대, 자재 소·중·대, 골드 마력초, 자재 마력초, 히든]
            Stage(3, "frost", "서리 빙굴", "Frost Cavern", new Color(0.6f, 0.8f, 1f), 2, Power.Weak,
                "서리 슬라임,얼음 박쥐,설인 늑대,얼음 해골,빙결 거미,서리 골렘,얼음 이끼,빙정 수정,빙룡의 새끼",
                "Frost Slime,Ice Bat,Yeti Wolf,Ice Skeleton,Frost Spider,Frost Golem,Ice Moss,Ice Crystal,Ice Wyrmling");
            Stage(4, "swamp", "독안개 늪", "Toxic Swamp", new Color(0.5f, 0.9f, 0.3f), 1, Power.Mid,
                "독 슬라임,늪 뱀,독버섯 거인,늪지 트렌트,맹독 거미,거머리 웜,독포자 군락,늪 덩굴,늪의 히드라",
                "Poison Slime,Swamp Snake,Giant Toadstool,Bog Treant,Venom Spider,Leech Worm,Spore Patch,Bog Vine,Swamp Hydra");
            Stage(5, "grave", "망자의 묘지", "Graveyard", new Color(0.55f, 0.75f, 1f), 3, Power.Mid,
                "떠도는 유령,흡혼 박쥐,구울 늑대,해골 기사,무덤 벌레,뼈 골렘,묘지 이끼,망령 덩굴,망자의 왕",
                "Wandering Ghost,Soul Bat,Ghoul Wolf,Skeleton Knight,Grave Worm,Bone Golem,Grave Moss,Wraith Vine,King of the Dead");
            Stage(6, "ruins", "모래 유적", "Sand Ruins", new Color(0.95f, 0.8f, 0.5f), 0, Power.Weak,
                "모래 슬라임,사막 전갈,보물 미믹,미라 병사,모래 웜,사막 골렘,황금 이끼,모래 장미,황금 미믹",
                "Sand Slime,Desert Scorpion,Treasure Mimic,Mummy Soldier,Sand Worm,Desert Golem,Golden Moss,Sand Rose,Golden Mimic");
            Stage(7, "crystal", "수정 광산", "Crystal Mine", new Color(1f, 0.6f, 0.9f), 2, Power.Strong,
                "수정 슬라임,보석 박쥐,수정 거미,광부 해골,수정 골렘,보석 웜,빛 이끼,무지개 수정,수정룡의 새끼",
                "Crystal Slime,Gem Bat,Crystal Spider,Miner Skeleton,Crystal Golem,Gem Worm,Glow Moss,Rainbow Crystal,Crystal Wyrmling");
            Stage(8, "storm", "폭풍 봉우리", "Storm Peak", new Color(0.7f, 0.85f, 1f), 1, Power.Weak,
                "번개 정령,폭풍 박쥐,뇌운 늑대,폭풍 임프,번개 전갈,바위 골렘,뇌전 이끼,번개 수정,뇌조왕",
                "Lightning Spirit,Storm Bat,Thunder Wolf,Storm Imp,Volt Scorpion,Rock Golem,Thunder Moss,Volt Crystal,Thunderbird King");
            Stage(9, "abyss", "심연 해구", "Abyssal Trench", new Color(0.5f, 1f, 1f), 3, Power.Strong,
                "심해 슬라임,심연 눈알,심해 뱀,익사한 해골,심해 거미,심연 웜,심해 산호,발광 덩굴,레비아탄의 새끼",
                "Deep Slime,Abyssal Eye,Sea Serpent,Drowned Skeleton,Deep Spider,Abyss Worm,Deep Coral,Glow Vine,Leviathan Spawn");
            Stage(10, "chaos", "혼돈의 균열", "Rift of Chaos", new Color(1f, 0.4f, 1f), 0, Power.Mid,
                "혼돈 정령,공허 눈알,균열 미믹,공허 기사,혼돈 헬하운드,공허 골렘,혼돈 이끼,공허 수정,혼돈룡",
                "Chaos Spirit,Void Eye,Rift Mimic,Void Knight,Chaos Hellhound,Void Golem,Chaos Moss,Void Crystal,Chaos Dragon");

            // 포획 장면 연출: 떠다니는 입자 색·속도, 설명
            Visual("forest", new Color(0.8f, 1f, 0.4f), new Vector2(0, 2), "안개 낀 숲. 초보 포획대원의 첫 사냥터.", "A misty forest. Every rookie hunter starts here.");
            Visual("lava", new Color(1f, 0.55f, 0.2f), new Vector2(1, 10), "끓어오르는 용암 사이로 불의 괴물이 산다.", "Fire monsters live among the bubbling lava.");
            Visual("frost", new Color(1f, 1f, 1f), new Vector2(-3, -8), "눈보라가 그치지 않는 얼음 동굴.", "An ice cave where the blizzard never stops.");
            Visual("swamp", new Color(0.6f, 0.9f, 0.3f), new Vector2(1, 4), "독안개가 깔린 늪지대.", "A marsh blanketed in toxic fog.");
            Visual("grave", new Color(0.5f, 0.7f, 1f), new Vector2(2, 3), "망자가 잠들지 못하는 묘지.", "A graveyard where the dead cannot rest.");
            Visual("ruins", new Color(0.95f, 0.8f, 0.5f), new Vector2(-18, 1), "모래바람 속에 보물이 잠든 유적.", "Ruins hiding treasure in the sandstorm.");
            Visual("crystal", new Color(1f, 0.6f, 0.95f), new Vector2(0, 0), "빛나는 수정이 자라는 폐광.", "An abandoned mine full of glowing crystals.");
            Visual("storm", new Color(0.7f, 0.85f, 1f), new Vector2(-20, -40), "번개가 끊이지 않는 봉우리.", "A peak struck by endless lightning.");
            Visual("abyss", new Color(0.6f, 1f, 1f), new Vector2(0, 8), "빛이 닿지 않는 바다 밑바닥.", "The sea floor where no light reaches.");
            Visual("chaos", new Color(1f, 0.4f, 1f), new Vector2(0, 5), "마계와 이어진 공간의 균열.", "A rift in space leading to the demon realm.");

            foreach (var r in Regions) ComputePower(r);
            foreach (var a in Achievements) if (a.id == "book_all") a.target = Monsters.Count; // 필드 초기화 시점엔 괴물 목록이 비어 있다
        }

        // ── 투기장 전투력 ─────────────────────────────────────
        // 스테이지 배율(관람료와 같은 비율): 1스테이지 1배, 2스테이지 2.6배, 이후 1.6배씩
        public static float StageMul(int stage) => stage <= 1 ? 1f : 2.6f * Mathf.Pow(1.6f, stage - 2);

        // 차지하는 공간·항마력에 비해 관람료가 적은(저밸류) 괴물일수록 전투력이 높다.
        // 효율 = 분당 관람료 / (공간 + 소모 항마력). 전투력 = 100 × 스테이지 배율 × (지역 평균 효율 / 이 괴물 효율)².
        static void ComputePower(RegionDef r)
        {
            float sum = 0; int n = 0;
            foreach (var m in Monsters)
                if (m.region == r.id && !m.isPlant && !m.hidden) { sum += Efficiency(m); n++; }
            float avg = n > 0 ? sum / n : 1f;
            foreach (var m in Monsters)
            {
                if (m.region != r.id || m.isPlant) continue;
                float rel = avg / Mathf.Max(0.01f, Efficiency(m));
                m.power = Mathf.Max(1, Mathf.RoundToInt(100f * StageMul(r.stage) * rel * rel));
            }
        }

        static float Efficiency(MonsterDef m) => m.admission / (m.space + Mathf.Max(0, -m.maki));

        // 대련 결과 확률. r = 전투력 / 용사 힘 (용사는 열린 가장 높은 스테이지만큼 강하다).
        // r = 1 → 대실패 12% · 실패 28% · 무승부 25% · 성공 25% · 대성공 10%. 강할수록 성공 쪽으로 기운다.
        public static float HeroPower(int maxStage) => 100f * StageMul(maxStage) * 0.8f;

        public static float[] BoutOdds(float power, float heroPower)
        {
            float r = Mathf.Clamp(power / Mathf.Max(1f, heroPower), 0.2f, 4f);
            float sr = Mathf.Sqrt(r);
            var w = new[] { 0.12f / r, 0.28f / sr, 0.25f, 0.25f * sr, 0.10f * r };
            float total = 0;
            foreach (var x in w) total += x;
            for (int i = 0; i < w.Length; i++) w[i] /= total;
            return w;
        }

        public static string BoutName(BoutResult r) => r switch
        {
            BoutResult.GreatFail => L.T("대실패", "Disaster"),
            BoutResult.Fail => L.T("실패", "Defeat"),
            BoutResult.Draw => L.T("무승부", "Draw"),
            BoutResult.Win => L.T("성공", "Victory"),
            _ => L.T("대성공", "Triumph"),
        };

        // ── 업적: 처음부터 끝까지. 보상은 소량의 골드 ───────────
        static AchievementDef A(string id, string name, string en, string desc, string descEn, long target, int gold, System.Func<Game, long> progress) =>
            new AchievementDef { id = id, name = name, en = en, desc = desc, descEn = descEn, target = target, gold = gold, progress = progress };

        static long Sum(List<CountEntry> list) { long n = 0; foreach (var e in list) n += e.count; return n; }
        static long MaxLevel(Game g, BuildingType t) { int lv = 0; foreach (var b in g.S.buildings) if (b.type == t) lv = Mathf.Max(lv, b.level); return lv; }
        static long Explored(Game g) { int n = 1; foreach (var r in Regions) if (r.unlockResearch != null && g.S.research.Contains(r.unlockResearch)) n++; return n; }
        static long HiddenFound(Game g) { int n = 0; foreach (var m in Monsters) if (m.hidden && CountList.Get(g.S.caught, m.id) > 0) n++; return n; }

        public static readonly AchievementDef[] Achievements =
        {
            A("build_1",    "첫 삽",          "Groundbreaking",   "건물을 처음 짓는다",                 "Build your first room",              1, 200,   g => g.S.buildings.Count),
            A("catch_1",    "첫 포획",        "First Catch",      "괴물을 처음 잡는다",                 "Catch your first monster",           1, 200,   g => Sum(g.S.caught)),
            A("exhibit_1",  "개관",           "Grand Opening",    "전시 우리에 괴물을 넣는다",          "Put a monster on display",           1, 300,   g => g.S.buildings.Exists(b => b.type == BuildingType.Tank && b.contents.Count > 0) ? 1 : 0),
            A("sale_1",     "첫 손님",        "First Customer",   "음식이나 기념품을 처음 판다",        "Make your first sale",               1, 200,   g => g.S.stats.sales),
            A("staff_1",    "출근 완료",      "Clocked In",       "성 관리자를 파견한다",               "Dispatch a castle staff member",     1, 200,   g => g.S.castleStaff.Exists(c => c.dispatched) ? 1 : 0),
            A("lab_1",      "흑마법 입문",    "Dark Arts 101",    "흑마법 연구소를 짓는다",             "Build the Dark Magic Lab",           1, 300,   g => g.CountOf(BuildingType.Lab)),
            A("research_1", "첫 연구",        "Eureka",           "연구를 처음 완료한다",               "Complete your first research",       1, 300,   g => g.S.research.Count),
            A("wing_r",     "오른쪽 날개",    "Right Wing",       "오른쪽 날개를 연다",                 "Open the right wing",                1, 500,   g => g.S.floorsRight),
            A("trash_50",   "청소 반장",      "Clean Sweep",      "쓰레기 50개를 줍는다",               "Pick up 50 pieces of trash",         50, 500,  g => g.S.stats.trash),
            A("sale_100",   "단골 장사",      "Regulars",         "100번 판다",                         "Make 100 sales",                     100, 800,  g => g.S.stats.sales),
            A("catch_100",  "포획 전문가",    "Seasoned Hunter",  "괴물 100마리를 잡는다",              "Catch 100 monsters",                 100, 800,  g => Sum(g.S.caught)),
            A("book_1",     "도감 첫 장",     "First Entry",      "도감에 괴물을 처음 등록한다",        "Register your first monster",        1, 500,   g => g.S.collectionClaimed.Count),
            A("stage_3",    "탐험가",         "Explorer",         "스테이지 3을 연다",                  "Unlock stage 3",                     3, 1000,  Explored),
            A("hidden_1",   "숨은 그림 찾기", "Hidden Gem",       "히든 괴물을 발견한다",               "Find a hidden monster",              1, 1500,  HiddenFound),
            A("floors_8",   "증축 공사",      "Renovation",       "성을 모두 합쳐 8층 연다",            "Open 8 floors in total",             8, 1500,  g => g.TotalFloorsOpen),
            A("shop_5",     "맛집 등극",      "Rising Star",      "식당이나 기념품점을 Lv.5로 올린다",  "Raise a Diner or Gift Shop to Lv.5", 5, 1500,  g => System.Math.Max(MaxLevel(g, BuildingType.Restaurant), MaxLevel(g, BuildingType.Souvenir))),
            A("arena_1",    "투기장 개장",    "Let the Games Begin", "투기장을 짓는다",                 "Build an Arena",                     1, 1500,  g => g.CountOf(BuildingType.Arena)),
            A("arena_win",  "첫 승리",        "First Blood",      "투기장 대련에서 이긴다",             "Win an arena bout",                  1, 1000,  g => g.S.stats.arenaWins),
            A("visitors_60","북새통",         "Packed House",     "손님 60명이 동시에 머문다",          "Host 60 guests at once",             60, 2000, g => g.S.stats.peakVisitors),
            A("earn_100k",  "십만장자",       "Hundred Grand",    "누적 수입 100,000골드",              "Earn 100,000 Gold in total",         100000, 2000, g => g.S.stats.goldEarned),
            A("stage_5",    "개척자",         "Pathfinder",       "스테이지 5를 연다",                  "Unlock stage 5",                     5, 3000,  Explored),
            A("sale_1000",  "장사의 신",      "Merchant Lord",    "1,000번 판다",                       "Make 1,000 sales",                   1000, 3000, g => g.S.stats.sales),
            A("catch_1000", "괴물 사냥꾼",    "Monster Hunter",   "괴물 1,000마리를 잡는다",            "Catch 1,000 monsters",               1000, 3000, g => Sum(g.S.caught)),
            A("book_20",    "도감 수집가",    "Collector",        "도감에 20종을 등록한다",             "Register 20 monsters",               20, 3000, g => g.S.collectionClaimed.Count),
            A("arena_great","관중 열광",      "Crowd Goes Wild",  "투기장 대성공 10번",                 "10 arena triumphs",                  10, 3000, g => g.S.stats.arenaGreat),
            A("research_30","대마법사",       "Archmage",         "연구를 30단계 완료한다",             "Complete 30 research levels",        30, 4000, g => g.S.research.Count),
            A("earn_1m",    "백만장자",       "Millionaire",      "누적 수입 1,000,000골드",            "Earn 1,000,000 Gold in total",       1000000, 5000, g => g.S.stats.goldEarned),
            A("shop_10",    "전설의 가게",    "Legendary Shop",   "식당이나 기념품점을 Lv.10으로 올린다", "Raise a Diner or Gift Shop to Lv.10", 10, 6000, g => System.Math.Max(MaxLevel(g, BuildingType.Restaurant), MaxLevel(g, BuildingType.Souvenir))),
            A("stage_8",    "심연의 문턱",    "Edge of the Abyss","스테이지 8을 연다",                  "Unlock stage 8",                     8, 6000,  Explored),
            A("arena_100",  "투기장의 제왕",  "Arena Champion",   "투기장 대련에서 100번 이긴다",       "Win 100 arena bouts",                100, 6000, g => g.S.stats.arenaWins),
            A("hidden_5",   "히든 헌터",      "Secret Seeker",    "히든 괴물 5종을 발견한다",           "Find 5 hidden monsters",             5, 6000,  HiddenFound),
            A("visitors_120","인산인해",      "Sea of Heroes",    "손님 120명이 동시에 머문다",         "Host 120 guests at once",            120, 8000, g => g.S.stats.peakVisitors),
            A("floors_20",  "마왕성 완공",    "Castle Complete",  "양쪽 날개를 모두 10층까지 연다",     "Open all 20 floors",                 20, 10000, g => g.TotalFloorsOpen),
            A("stage_10",   "혼돈 정복",      "Chaos Conquered",  "스테이지 10을 연다",                 "Unlock stage 10",                    10, 10000, Explored),
            A("earn_10m",   "마계 재벌",      "Demon Tycoon",     "누적 수입 10,000,000골드",           "Earn 10,000,000 Gold in total",      10000000, 12000, g => g.S.stats.goldEarned),
            A("book_all",   "완전한 도감",    "Complete Book",    "모든 괴물을 도감에 등록한다",        "Register every monster",             999, 20000, g => g.S.collectionClaimed.Count),
        };

        static void Visual(string id, Color c, Vector2 vel, string desc, string descEn)
        {
            var r = RegionById(id);
            r.ambient = c; r.ambientVel = vel; r.desc = desc; r.descEn = descEn;
        }

        // 스테이지 괴물 수치: 슬롯별 기본값 × 스테이지 배율 (용암 동굴 ≈ 2.6배, 이후 스테이지마다 1.6배)
        static readonly (Currency cur, int price, int space, int maki, float adm)[] Slot =
        {
            (Currency.Gold, 30, 1, -1, 6), (Currency.Gold, 60, 1, -2, 12), (Currency.Gold, 120, 2, -3, 24),
            (Currency.Material, 30, 1, -1, 5), (Currency.Material, 60, 2, -2, 10), (Currency.Material, 120, 2, -4, 22),
            (Currency.Gold, 20, 1, 4, 1), (Currency.Material, 20, 1, 4, 1),
        };

        // 스테이지 괴물 id: s{스테이지}_{슬롯 0~7}, 히든 = 슬롯 8 (스프라이트 이름과 같다)
        public static string StageMonsterId(int stage, int slot) => $"s{stage}_{slot}";

        static void Stage(int stage, string id, string name, string en, Color tint, int hiddenBait, Power hiddenPower, string namesKo, string namesEn)
        {
            var ko = namesKo.Split(',');
            var eng = namesEn.Split(',');
            float mul = 2.6f * Mathf.Pow(1.6f, stage - 2);
            float makiMul = 1f + 0.25f * (stage - 1);
            var species = new MonsterDef[8];
            for (int i = 0; i < 8; i++)
            {
                var t = Slot[i];
                bool plant = i >= 6;
                int maki = plant ? 4 + stage : Mathf.RoundToInt(t.maki * makiMul);
                int space = t.space + (!plant && i % 3 == 2 && stage >= 6 ? 1 : 0);
                species[i] = M(StageMonsterId(stage, i), ko[i], eng[i], id, t.cur, Mathf.RoundToInt(t.price * mul), space, maki, Mathf.Round(t.adm * mul), tint, plant);
            }
            var hidden = M(StageMonsterId(stage, 8), ko[8], eng[8], id, Currency.Gold, Mathf.RoundToInt(500 * mul * 1.5f), 3, Mathf.RoundToInt(-5 * makiMul), Mathf.Round(60 * mul * 1.4f), tint, hidden: true);
            Region(id, name, en, $"region_{stage}", species, hidden, hiddenBait, hiddenPower, stage % 4);
        }
    }
}

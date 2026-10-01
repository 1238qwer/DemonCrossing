using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // ─────────────────────────────────────────────────────────────
    // 정적 게임 데이터. 밸런스 조정은 이 파일에서만 한다.
    // 원작(아라드 수족관 메이커) 대응:
    //   물고기 → 괴물, 해초 → 마초(수질 대신 '마기' 회복), 어항 → 전시 우리
    //   낚시 직원 → 포획대원, 수족관 직원 → 성 관리자, 낚싯배 내구도 → 체력
    //
    // 밸런스 목표: 약 10시간 플레이.
    //   보석(연구소에서만 천천히 나오는 가장 귀한 재료)이 진행 속도를 정한다.
    //   보석 총 사용처 ≈ 800개(업그레이드 ~500 · 스테이지 탐사 ~190 · 성 증축 ~125),
    //   연구소 생산은 초반 시간당 12개 → 후반(3개 · Lv.5 · 연구 효율) 시간당 ~130개.
    //   골드는 스테이지·층이 늘수록 기하급수로 커지므로 비용도 같은 비율로 커진다.
    // ─────────────────────────────────────────────────────────────

    public enum Currency { Gold, Material }
    public enum Power { Weak, Mid, Strong }
    public enum BuildingType { Tank, Restaurant, Souvenir, Lab, Dorm, Rest }
    public enum StaffRole { Hunter, Castle }

    public class MonsterDef
    {
        public string id, name, en, region;
        public bool isPlant, hidden;
        public Currency sellCurrency;
        public int sellPrice;
        public int space;
        public int maki;          // 음수 = 마기 소모, 양수 = 마기 회복(마초)
        public float admission;   // 분당 관람료
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
        public int rp, gold;
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
        public int rp0, gold0;
        public System.Func<int, string> effect;   // 누적 효과 설명 (레벨 n)
        public string Name => L.En ? en : name;

        public string TierId(int lv) => $"{id}_{lv}";
        public int Rp(int lv) => Mathf.RoundToInt(rp0 * Mathf.Pow(GameData.UpgradeRpGrowth, lv - 1));
        public int Gold(int lv) => Mathf.RoundToInt(gold0 * Mathf.Pow(GameData.UpgradeGoldGrowth, lv - 1) / 100f) * 100;
    }

    public static class GameData
    {
        // ── 공통 수치 ───────────────────────────────────────────
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

        // 성 증축: n층을 여는 비용 (왼쪽·오른쪽 날개 따로)
        public static int ExpandGold(int floor) => Mathf.RoundToInt(3000 * Mathf.Pow(1.75f, floor - 1) / 100f) * 100;
        public static int ExpandMat(int floor) => ExpandGold(floor) / 2;
        public static int ExpandGems(int floor) => floor + 1;

        // 전시 우리 크기 = 칸 수 × (레벨 + 1)
        public const int TankSpacePerSize = 5;
        public const int TankMakiPerSize = 4;
        public const int TankCapPerSize = 2500;      // 원작: 칸당 2,500 누적 한도
        public const float TankCapMinutes = 6f;      // 관람료가 많으면 최소 6분치는 쌓인다
        public const int TankStaffCollectThreshold = 300;

        public const float HunterRestTime = 60f;     // 원작: 내구도 0 → 60초 회복
        public const float HunterRecallPenalty = 50f; // 원작: 복귀 시 약 50초 패널티

        // 보석: 가장 귀한 재료. 연구소 Lv.1 은 5분에 1개.
        public const int LabRpCap = 10;
        public static readonly float[] LabInterval = { 300f, 255f, 215f, 180f, 150f };

        public const int RestDailyGold = 2000;       // 원작: 매일 06시 건물당 2,000골드
        public const int RestDailyHour = 6;

        public const float TrashInterval = 20f;          // 스테이지마다 20초에 하나씩 떨어진다

        // 포획 장면 크기(도트). 배경 = StageW×StageH, 땅 윗면 = StageGroundY
        public const int StageW = 276, StageH = 108, StageGroundY = 34;
        public const float TrashMinX = 24f;
        public const int TrashMaxPerRegion = 5;
        public const int TrashReward = 20;           // 원작: 페트병 자재 20 / 캔 20골드

        public const int CollectionRegisterCount = 30;
        public static int CollectionGold(MonsterDef m) => 500 * GameData.RegionById(m.region).stage * (m.hidden ? 3 : 1);
        public static int CollectionGems(MonsterDef m) => m.hidden ? 5 : 1;

        public const float VisitorBaseInterval = 7f;
        public const float VisitorMinInterval = 1.2f;
        public const int VisitorMaxBase = 20, VisitorMaxPerFloor = 4;
        public const float VisitorPatience = 12f;
        public const int VisitorWalletMin = 60, VisitorWalletMax = 220;
        public const float VisitorWalletPerFloor = 1.3f; // 열린 층이 늘수록 부유한 용사가 온다

        public const float WalkSpeed = 0.5f;         // 칸/초 (1칸 = 방 하나)
        public const float ElevatorSecPerFloor = 0.8f;
        public const float ServiceTime = 1f;
        public const float SpecialtySpeed = 1.35f;   // 특화 건물로 갈 때 이동 속도
        public const float OffSpecialtySpeed = 0.9f;
        public const float SpecialtyWork = 0.6f;     // 특화 건물 작업 시간 배율

        public const int BaseStaffCap = 3;

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
            var m = new MonsterDef { id = id, name = name, en = en, region = region, sellCurrency = cur, sellPrice = price, space = space, maki = maki, admission = adm, color = c, isPlant = plant, hidden = hidden };
            Monsters.Add(m);
            MonsterById[id] = m;
            return m;
        }

        // 원작: 지역당 골드 물고기 3 + 자재 물고기 3 + 골드 해초 1 + 자재 해초 1 (+ 히든)
        // 풀 배치 규칙: [bait, power] → 인덱스 (0~2 골드, 3~5 자재, 6 골드 마초, 7 자재 마초)
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
                upGold = Curve(5, 1500, 2f), upMat = Curve(5, 1500, 2f), color = new Color(0.25f, 0.35f, 0.6f),
                desc = "괴물을 전시해 관람료를 쌓는다. 끌어서 여러 칸을 하나로 지을 수 있다.",
                descEn = "Shows monsters for admission fees. Drag to build one cage over many cells." },
            [BuildingType.Restaurant] = new BuildingDef { type = BuildingType.Restaurant, icon = "room_restaurant", name = "마족 식당", en = "Demon Diner", maxCount = 3, costGold = 1500, costMat = 500, maxLevel = ShopMaxLevel,
                upGold = Curve(ShopMaxLevel, 2000, 1.85f), upMat = Half(Curve(ShopMaxLevel, 2000, 1.85f)), color = new Color(0.65f, 0.35f, 0.2f),
                desc = "용사 손님에게 음식을 판다. 레벨이 오를수록 비싼 메뉴(10단계)를 판다.",
                descEn = "Sells food to hero guests. Higher levels unlock pricier dishes (10 tiers)." },
            [BuildingType.Souvenir] = new BuildingDef { type = BuildingType.Souvenir, icon = "room_souvenir", name = "기념품점", en = "Gift Shop", maxCount = 3, costGold = 1500, costMat = 500, maxLevel = ShopMaxLevel,
                upGold = Curve(ShopMaxLevel, 2000, 1.85f), upMat = Half(Curve(ShopMaxLevel, 2000, 1.85f)), color = new Color(0.55f, 0.25f, 0.5f),
                desc = "용사 손님에게 기념품을 판다. 레벨이 오를수록 비싼 상품(10단계)을 판다.",
                descEn = "Sells souvenirs to hero guests. Higher levels unlock pricier goods (10 tiers)." },
            [BuildingType.Lab] = new BuildingDef { type = BuildingType.Lab, icon = "room_lab", name = "흑마법 연구소", en = "Dark Magic Lab", maxCount = 3, costGold = 1000, costMat = 1000, maxLevel = 5,
                upGold = Curve(5, 4000, 2.2f), upMat = Curve(5, 4000, 2.2f), color = new Color(0.2f, 0.5f, 0.45f),
                desc = "가장 귀한 재료인 보석을 아주 천천히 만든다. 탭하거나 관리자가 수거한다.",
                descEn = "Slowly makes Gems, the rarest resource. Tap or let staff collect them." },
            [BuildingType.Dorm] = new BuildingDef { type = BuildingType.Dorm, icon = "room_dorm", name = "직원 숙소", en = "Staff Dorm", costGold = 2000, costMat = 2000, maxLevel = 5,
                upGold = Curve(5, 3000, 2f), upMat = Curve(5, 3000, 2f), color = new Color(0.45f, 0.45f, 0.3f),
                desc = "레벨마다 직원 고용 한도 +1.",
                descEn = "Each level adds +1 staff capacity." },
            [BuildingType.Rest] = new BuildingDef { type = BuildingType.Rest, icon = "room_rest", name = "휴게실", en = "Lounge", costGold = 1000, costMat = 1000, maxLevel = 1,
                upGold = new int[0], upMat = new int[0], color = new Color(0.35f, 0.5f, 0.3f),
                desc = "매일 06시에 휴게실 1개당 2,000골드 지원금.",
                descEn = "Pays 2,000 Gold per lounge every day at 06:00." },
        };

        // ── 음식 / 기념품: 각 10단계. 매입가 = 10 × 1.75^(단계-1), 판매가 = 매입가 × 2.5 ──
        public static readonly ShopItemDef[] ShopItems = MakeItems();

        static ShopItemDef[] MakeItems()
        {
            string[] foodKo = { "해골 수프", "마왕 스테이크", "드래곤 다리 구이", "독버섯 리조또", "용암 피자", "서리 젤라토", "심연 초밥", "흑마법 케이크", "마왕의 와인", "혼돈 코스요리" };
            string[] foodEn = { "Skull Soup", "Demon Steak", "Roast Dragon Leg", "Toadstool Risotto", "Lava Pizza", "Frost Gelato", "Abyss Sushi", "Hex Cake", "Demon Lord's Wine", "Chaos Full Course" };
            string[] giftKo = { "마왕 열쇠고리", "박쥐 인형", "마왕 망토", "해골 머그컵", "마법 수정구", "용의 알 모형", "저주받은 거울", "마검 레플리카", "마왕 왕관", "마왕성 미니어처" };
            string[] giftEn = { "Demon Keychain", "Bat Plush", "Demon Cape", "Skull Mug", "Crystal Ball", "Dragon Egg Replica", "Cursed Mirror", "Demon Sword Replica", "Demon Crown", "Castle Miniature" };
            var list = new List<ShopItemDef>();
            for (int t = 1; t <= 10; t++)
            {
                int buy = Mathf.RoundToInt(10 * Mathf.Pow(1.75f, t - 1));
                list.Add(new ShopItemDef { id = $"food_{t}", name = foodKo[t - 1], en = foodEn[t - 1], shop = BuildingType.Restaurant, tier = t, buyPrice = buy, sellPrice = Mathf.RoundToInt(buy * 2.5f) });
            }
            for (int t = 1; t <= 10; t++)
            {
                int buy = Mathf.RoundToInt(8 * Mathf.Pow(1.75f, t - 1));
                list.Add(new ShopItemDef { id = $"gift_{t}", name = giftKo[t - 1], en = giftEn[t - 1], shop = BuildingType.Souvenir, tier = t, buyPrice = buy, sellPrice = Mathf.RoundToInt(buy * 2.6f) });
            }
            return list.ToArray();
        }

        // ── 직원 ───────────────────────────────────────────────
        // 성 관리자 특화: 스켈레톤 = 전시 우리, 서큐버스 = 식당, 가고일 = 기념품점, 마녀 = 연구소
        public static readonly StaffDef[] Staff =
        {
            new StaffDef { id = "hunter_imp",  name = "임프 포획꾼",  en = "Imp Catcher",    role = StaffRole.Hunter, interval = 10f, amount = 1, durability = 20, hireGold = 0 },
            new StaffDef { id = "hunter_orc",  name = "오크 사냥꾼",  en = "Orc Hunter",     role = StaffRole.Hunter, interval = 14f, amount = 2, durability = 15, hireGold = 3000 },
            new StaffDef { id = "hunter_lich", name = "리치 소환사",  en = "Lich Summoner",  role = StaffRole.Hunter, interval = 8f,  amount = 1, durability = 30, hireGold = 12000 },
            new StaffDef { id = "castle_skel",  name = "스켈레톤 집사", en = "Skeleton Butler", role = StaffRole.Castle, moveSpeed = 1.0f,  specialty = BuildingType.Tank,       admissionBonus = 0.05f, hireGold = 1000 },
            new StaffDef { id = "castle_succ",  name = "서큐버스 점원", en = "Succubus Clerk",  role = StaffRole.Castle, moveSpeed = 1.15f, specialty = BuildingType.Restaurant, foodBonus = 0.10f, souvenirBonus = 0.03f, hireGold = 1000 },
            new StaffDef { id = "castle_garg",  name = "가고일 경비",  en = "Gargoyle Guard",  role = StaffRole.Castle, moveSpeed = 0.9f,  specialty = BuildingType.Souvenir,   foodBonus = 0.02f, souvenirBonus = 0.12f, admissionBonus = 0.03f, hireGold = 1000 },
            new StaffDef { id = "castle_witch", name = "마녀 연구원",  en = "Witch Researcher", role = StaffRole.Castle, moveSpeed = 1.05f, specialty = BuildingType.Lab,        hireGold = 1000 },
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
            rp = ExploreGems[stage],
            gold = Mathf.RoundToInt(5000 * Mathf.Pow(2.1f, stage - 2) / 1000f) * 1000,
            requires = stage > 2 ? $"region_{stage - 1}" : null,
        };

        // ── 연구: 10단계 업그레이드 ────────────────────────────
        public const float UpgradeRpGrowth = 1.25f;
        public const float UpgradeGoldGrowth = 1.7f;

        public static string CategoryName(int i) => L.En
            ? new[] { "Hunting", "Exhibits", "Guests & Shops", "Operations" }[i]
            : new[] { "포획", "전시", "손님 · 상점", "운영" }[i];
        public const int CategoryCount = 4;

        public static readonly UpgradeLine[] Upgrades =
        {
            new UpgradeLine { id = "speed",    name = "포획 속도",    en = "Hunt Speed",     icon = "up_speed",    category = 0, rp0 = 1, gold0 = 1000, effect = n => L.T($"포획 주기 -{n * 4}%", $"Hunt cycle -{n * 4}%") },
            new UpgradeLine { id = "dura",     name = "탐험대 체력",  en = "Hunter Stamina", icon = "up_dura",     category = 0, rp0 = 1, gold0 = 1000, effect = n => L.T($"포획대원 체력 +{n * 3}", $"Hunter stamina +{n * 3}") },
            new UpgradeLine { id = "rest",     name = "빠른 회복",    en = "Quick Recovery", icon = "up_rest",     category = 0, rp0 = 1, gold0 = 1500, effect = n => L.T($"체력 회복 {HunterRestTime:0}초 → {RestTimeAt(n):0}초", $"Recovery {HunterRestTime:0}s → {RestTimeAt(n):0}s") },
            new UpgradeLine { id = "adm",      name = "관람료 인상",  en = "Ticket Price",   icon = "up_adm",      category = 1, rp0 = 2, gold0 = 1500, effect = n => L.T($"관람료 +{n * 10}%", $"Admission +{n * 10}%") },
            new UpgradeLine { id = "cap",      name = "우리 금고",    en = "Cage Vault",     icon = "up_cap",      category = 1, rp0 = 1, gold0 = 1200, effect = n => L.T($"관람료 누적 한도 +{n * 15}%", $"Fee storage +{n * 15}%") },
            new UpgradeLine { id = "maki",     name = "마기 정화",    en = "Miasma Purifier", icon = "up_maki",    category = 1, rp0 = 1, gold0 = 2000, effect = n => L.T($"전시 우리 크기당 마기 +{n}", $"Miasma +{n} per cage size") },
            new UpgradeLine { id = "visitor",  name = "손님 유치",    en = "Advertising",    icon = "up_visitor",  category = 2, rp0 = 2, gold0 = 1500, effect = n => L.T($"손님 방문 빈도 +{n * 12}% · 최대 손님 +{n * VisitorMaxPerLevel}명", $"Guest rate +{n * 12}% · Max guests +{n * VisitorMaxPerLevel}") },
            new UpgradeLine { id = "wallet",   name = "용사 지갑",    en = "Rich Heroes",    icon = "up_wallet",   category = 2, rp0 = 1, gold0 = 1500, effect = n => L.T($"용사 소지금 +{n * 20}%", $"Hero wallets +{n * 20}%") },
            new UpgradeLine { id = "patience", name = "친절 교육",    en = "Hospitality",    icon = "up_patience", category = 2, rp0 = 1, gold0 = 800,  effect = n => L.T($"손님 대기 시간 +{n * 1.5f:0.#}초", $"Guest patience +{n * 1.5f:0.#}s") },
            new UpgradeLine { id = "sales",    name = "상술",         en = "Salesmanship",   icon = "up_sales",    category = 2, rp0 = 2, gold0 = 2000, effect = n => L.T($"음식·기념품 판매가 +{n * 5}%", $"Food & gift prices +{n * 5}%") },
            new UpgradeLine { id = "lab",      name = "연구 효율",    en = "Lab Efficiency", icon = "up_lab",      category = 3, rp0 = 1, gold0 = 800,  effect = n => L.T($"보석 생산 속도 +{n * 8}% · 보관 한도 +{n * 2}", $"Gem output +{n * 8}% · Storage +{n * 2}") },
            new UpgradeLine { id = "trash",    name = "쓰레기 재활용", en = "Recycling",     icon = "up_trash",    category = 3, rp0 = 1, gold0 = 500,  effect = n => L.T($"쓰레기 수거 보상 +{n * 50}%", $"Trash rewards +{n * 50}%") },
        };

        public const int VisitorMaxPerLevel = 4;
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

            // 스테이지 3~10: 이름 [골드 소·중·대, 자재 소·중·대, 골드 마초, 자재 마초, 히든]
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
        }

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

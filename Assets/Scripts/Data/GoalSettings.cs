using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 사이드바 '다음 목표' = 처음 하는 사람을 위한 1회성 임무 (Assets/Resources/GoalSettings.asset).
    // 메뉴 Mawang/Goal Settings 로 열고 인스펙터에서 고친다.
    //   · 위에서부터 한 단계씩 진행한다. 달성하면 보상을 주고 다음 단계로 넘어가며, 절대 이전 단계로 돌아가지 않는다.
    //   · 이미 조건을 채운 단계는 도착하자마자 달성된다.
    //   · 중반까지 게임의 모든 기능을 한 번씩 해보게 하고, 마지막 단계를 마치면 목표 칸이 사라진다.
    // 종류는 세이브·에셋에 숫자로 저장되므로 새 종류는 끝에 추가한다.
    public enum GoalKind
    {
        BuildBuilding,    // 대상 건물을 수치 개 이상 짓기
        BuildingLevel,    // 대상 건물 중 하나를 Lv.수치 이상으로
        ResearchLevel,    // 연구 id 를 수치 단계 이상
        ExploreStage,     // 스테이지 수치 개방
        Floors,           // 왼쪽/오른쪽/합계 층 수 수치 이상
        ShopsToCap,       // 식당·기념품점 모두 지금 건물 연구 한도 레벨까지
        ShopsToMax,       // 식당·기념품점 최대 개수까지
        DispatchStaff,    // 성 관리자 수치 명 이상 파견
        StockGoods,       // 재고 수치 개 이상 (대상이 식당이면 음식, 기념품점이면 기념품, 그 외 전체)
        DispatchHunter,   // 포획대원 수치 명 이상 파견(회복 중 포함)
        CatchMonsters,    // 괴물 누적 포획 수치 마리
        ExhibitMonsters,  // 전시 우리에 괴물 수치 마리 이상
        ArenaFighters,    // 투기장 출전 괴물 수치 마리 이상
        HireStaff,        // 직원(포획대원 + 성 관리자) 수치 명 이상
        RegisterBook,     // 도감 등록 수치 종
        CollectGems,      // 연구소 보석 수거 수치 번
        CollectAdmission, // 전시 우리 관람료 수령 수치 번
        SellMonsters,     // 괴물 판매 수치 마리
        Sales,            // 음식·기념품 누적 판매 수치 번
        AnyResearch,      // 업그레이드 연구(10단계 라인)를 합쳐서 수치 단계
        AnyBuildingLevel, // 아무 건물이나 Lv.수치 이상
    }

    public enum FloorSide { Left, Right, Total }

    [CreateAssetMenu(fileName = "GoalSettings", menuName = "Mawang/Goal Settings")]
    public class GoalSettings : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            [Tooltip("끄면 이 단계를 건너뛴다")] public bool enabled = true;
            [Tooltip("목표 종류")] public GoalKind kind;
            [Tooltip("대상 건물 (건물 짓기 · 건물 레벨 · 재고 채우기)")] public BuildingType building;
            [Tooltip("수치: 개수 · 레벨 · 단계 · 스테이지 · 층 수 · 마리 수 · 횟수")] public int amount = 1;
            [Tooltip("연구 id (연구 단계): speed dura rest adm cap maki visitor wallet patience sales lab trash build storage")] public string research = "build";
            [Tooltip("증축: 왼쪽 / 오른쪽 / 합계")] public FloorSide side;
            [Tooltip("달성 보상 골드")] public int rewardGold = 500;
            [Tooltip("달성 보상 보석")] public int rewardGems;
            [Tooltip("한국어 안내. 비우면 자동 문구. {0} = 수치, {1} = 지금 값, {2} = 대상 이름")] [TextArea(1, 3)] public string ko;
            [Tooltip("English text. Empty = automatic. {0} = amount, {1} = current, {2} = target name")] [TextArea(1, 3)] public string en;
        }

        [Header("단계 (위에서부터 한 번씩)")]
        public List<Step> steps = Defaults();

        static Step S(GoalKind k, int amount, int gold, string ko, string en, BuildingType b = BuildingType.Tank, int gems = 0, string research = "build", FloorSide side = FloorSide.Left) =>
            new Step { kind = k, amount = amount, building = b, research = research, side = side, rewardGold = gold, rewardGems = gems, ko = ko, en = en };

        // 기본: 처음 하는 사람이 중반까지 모든 기능을 한 번씩 해보는 순서 (26단계)
        public static List<Step> Defaults() => new List<Step>
        {
            S(GoalKind.BuildBuilding, 1, 500, "빈 칸을 탭해 흑마법 연구소를 지으세요.\n연구에 쓰는 보석을 만듭니다.", "Tap an empty cell and build a Dark Magic Lab.\nIt makes Gems for research.", BuildingType.Lab),
            S(GoalKind.CollectGems, 1, 300, "연구소에 보석이 생기면 탭해서 수거하세요.", "When the Lab has a Gem, tap it to collect."),
            S(GoalKind.BuildBuilding, 1, 500, "빈 칸에 식당을 지으세요.\n용사 손님에게 음식을 팝니다.", "Build a Diner on an empty cell.\nIt sells food to hero guests.", BuildingType.Restaurant),
            S(GoalKind.StockGoods, 10, 300, "[상점 > 음식 · 기념품]에서 음식을 10개 이상 사 두세요. (지금 {1}개)\n재고가 없으면 팔 수 없습니다.", "Buy 10+ food in [Shop > Food & Gifts]. (now {1})\nNo stock, no sales.", BuildingType.Restaurant),
            S(GoalKind.Sales, 3, 500, "손님이 식당에서 기다리면 식당을 탭해 직접 팔아 보세요. ({1}/{0})", "When guests wait at the Diner, tap it to sell. ({1}/{0})"),
            S(GoalKind.DispatchStaff, 1, 300, "[직원]에서 성 관리자를 파견하세요.\n판매와 수거를 대신 해 줍니다.", "Dispatch castle staff in [Staff].\nThey sell and collect for you."),
            S(GoalKind.DispatchHunter, 1, 300, "[포획장]에서 포획대원을 파견하세요.\n괴물을 잡아 옵니다.", "Send a hunter from the [Hunt] tab.\nThey catch monsters."),
            S(GoalKind.CatchMonsters, 10, 500, "괴물을 10마리 잡으세요. (지금 {1}마리)", "Catch 10 monsters. (now {1})"),
            S(GoalKind.BuildBuilding, 1, 500, "[건설]에서 전시 우리를 끌어서 넓게 지어 보세요.\n괴물을 전시하면 관람료가 쌓입니다.", "In [Build], drag to make a wide Exhibit Cage.\nExhibited monsters earn admission.", BuildingType.Tank),
            S(GoalKind.ExhibitMonsters, 3, 300, "전시 우리를 탭해 괴물을 3마리 넣으세요. (지금 {1}마리)\n항마력이 모자라면 마력초를 함께 넣으세요.", "Tap the cage and put in 3 monsters. (now {1})\nAdd Mana Herbs if Ward runs low."),
            S(GoalKind.CollectAdmission, 1, 500, "전시 우리에 관람료가 쌓이면 탭해서 받으세요.", "When fees pile up in the cage, tap it to collect."),
            S(GoalKind.SellMonsters, 1, 300, "[상점 > 괴물 판매]에서 남는 괴물을 팔아 보세요.\n바로 돈이 됩니다.", "Sell a spare monster in [Shop > Sell].\nIt pays right away."),
            S(GoalKind.AnyResearch, 1, 500, "[연구]에서 보석으로 업그레이드를 하나 해 보세요.", "Spend Gems on any upgrade in [Research].", gems: 1),
            S(GoalKind.ResearchLevel, 1, 500, "[연구 > 운영] 건물 연구 1단계를 완료하세요.\n건물을 Lv.2로 올릴 수 있게 됩니다.", "Complete Architecture Lv.1 in [Research > Ops].\nBuildings can then reach Lv.2.", gems: 2),
            S(GoalKind.AnyBuildingLevel, 2, 1000, "건물을 길게 눌러(우클릭) Lv.2로 업그레이드하세요.\n식당은 레벨이 오를수록 비싼 메뉴를 팝니다.", "Hold (right-click) a building and upgrade it to Lv.2.\nHigher-level Diners sell pricier dishes."),
            S(GoalKind.BuildBuilding, 1, 500, "기념품점을 지으세요.\n음식과 다른 상품으로 손님 지갑을 엽니다.", "Build a Gift Shop.\nAnother way to open guests' wallets.", BuildingType.Souvenir),
            S(GoalKind.Floors, 1, 1000, "잠긴 오른쪽 1층을 탭해 성을 증축하세요.\n지을 자리가 늘어납니다.", "Tap the locked right-wing floor to expand.\nMore room to build.", side: FloorSide.Right),
            S(GoalKind.BuildBuilding, 1, 500, "직원 숙소를 지으세요.\n직원을 더 고용할 수 있습니다.", "Build a Staff Dorm.\nIt lets you hire more staff.", BuildingType.Dorm),
            S(GoalKind.HireStaff, 3, 1000, "직원 숙소를 Lv.2로 올리고 [직원]에서 직원을 한 명 더 고용하세요.", "Upgrade the Staff Dorm to Lv.2, then hire one more in [Staff]."),
            S(GoalKind.ExploreStage, 2, 1000, "[연구 > 증축 · 탐사]에서 용암 동굴을 탐사하세요.\n새로운 괴물이 나타납니다.", "Explore the Lava Cave in [Research > Expand].\nNew monsters appear there.", gems: 2),
            S(GoalKind.ResearchLevel, 1, 1000, "[연구 > 운영] 자원 창고 연구로 최대 보유량을 늘리세요.\n보유량이 가득 차면 수입이 멈춥니다.", "Raise your storage in [Research > Ops] Storehouse.\nIncome stops when you're full.", research: "storage"),
            S(GoalKind.RegisterBook, 1, 1500, "같은 괴물을 30마리 잡아 [도감]에 등록하세요.\n보석을 받습니다.", "Catch 30 of one monster and register it in the [Book].\nIt rewards Gems."),
            S(GoalKind.ResearchLevel, 3, 1500, "건물 연구를 3단계까지 올리세요. (지금 {1}단계)\n투기장을 지을 수 있게 됩니다.", "Raise Architecture to Lv.3. (now {1})\nIt unlocks the Arena."),
            S(GoalKind.BuildBuilding, 1, 2000, "투기장을 지으세요.\n용사가 괴물과 대련하고 돈을 냅니다.", "Build the Arena.\nHeroes spar with your monsters and pay for it.", BuildingType.Arena),
            S(GoalKind.ArenaFighters, 1, 1500, "투기장을 길게 눌러 출전 괴물을 넣으세요.\n전투력이 높은 괴물부터 나섭니다.", "Hold the Arena and add fighters.\nThe strongest fights first."),
            S(GoalKind.BuildBuilding, 1, 1500, "휴게실을 지으세요.\n매일 06시에 지원금을 줍니다. 이제 마왕성은 당신 뜻대로!", "Build a Lounge.\nIt pays a bonus every day at 06:00. The castle is yours now!", BuildingType.Rest),
        };

        // ── 런타임 접근 ────────────────────────────────────────
        static GoalSettings instance;
        static bool loaded;

        public static GoalSettings I
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    instance = Resources.Load<GoalSettings>("GoalSettings");
                    if (instance == null) instance = CreateInstance<GoalSettings>(); // 에셋이 없으면 기본값
                }
                return instance;
            }
        }
    }
}

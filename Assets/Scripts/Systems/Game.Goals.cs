using UnityEngine;

namespace Mawang
{
    // 다음 목표(1회성 임무): S.goalStep 단계부터 하나씩. 달성하면 보상을 주고 다음 단계로 — 이전 단계로는 돌아가지 않는다.
    public partial class Game
    {
        float goalTimer;

        // 지금 진행 중인 단계 (꺼진 단계는 건너뛴다). 모두 마쳤으면 null → 목표 칸을 숨긴다.
        public GoalSettings.Step CurrentGoal
        {
            get
            {
                var steps = GoalSettings.I.steps;
                for (int i = S.goalStep; i < steps.Count; i++)
                    if (steps[i] != null && steps[i].enabled) return steps[i];
                return null;
            }
        }

        // 0.5초마다 확인. 한 번에 한 단계만 넘긴다(이미 채운 단계가 이어져도 보상 알림이 하나씩 뜨게).
        void TickGoals(float dt)
        {
            goalTimer -= dt;
            if (goalTimer > 0) return;
            goalTimer = 0.5f;

            var steps = GoalSettings.I.steps;
            while (S.goalStep < steps.Count && (steps[S.goalStep] == null || !steps[S.goalStep].enabled)) S.goalStep++;
            if (S.goalStep >= steps.Count) return;

            var s = steps[S.goalStep];
            if (!GoalDone(s)) return;
            S.goalStep++;
            EarnGold(s.rewardGold);
            S.rp += s.rewardGems;
            Sound.Play("upgrade");
            string reward = s.rewardGems > 0 ? L.T($"+{s.rewardGold:N0}골드 · 보석 +{s.rewardGems}", $"+{s.rewardGold:N0} Gold · +{s.rewardGems} Gems") : L.T($"+{s.rewardGold:N0}골드", $"+{s.rewardGold:N0} Gold");
            Notify(L.T($"[목표 달성] {reward}", $"[Goal complete] {reward}"), "ic_star");
            MarkDirty();
        }

        static bool IsShopType(BuildingType t) => t == BuildingType.Restaurant || t == BuildingType.Souvenir;

        public int MaxLevelOf(BuildingType t)
        {
            int lv = 0;
            foreach (var b in S.buildings) if (b.type == t) lv = Mathf.Max(lv, b.level);
            return lv;
        }

        // 지금 건물 연구 한도까지 올리지 못한 가장 낮은 가게 (없으면 null)
        public BuildingState LowestShopBelowCap()
        {
            BuildingState low = null;
            foreach (var b in S.buildings)
            {
                if (!IsShopType(b.type)) continue;
                int target = Mathf.Min(Mods.buildingLevelCap, GameData.Buildings[b.type].maxLevel);
                if (b.level < target && (low == null || b.level < low.level)) low = b;
            }
            return low;
        }

        int GoodsOf(BuildingType t)
        {
            int n = 0;
            foreach (var e in S.goods)
            {
                var it = GameData.ShopItemById(e.id);
                if (it != null && (!IsShopType(t) || it.shop == t)) n += e.count;
            }
            return n;
        }

        public int FloorsOf(FloorSide side) => side == FloorSide.Left ? S.floorsLeft : side == FloorSide.Right ? S.floorsRight : TotalFloorsOpen;

        // 목표의 지금 값 (안내 문구의 {1})
        public long GoalCurrent(GoalSettings.Step s)
        {
            switch (s.kind)
            {
                case GoalKind.BuildBuilding: return CountOf(s.building);
                case GoalKind.BuildingLevel: return MaxLevelOf(s.building);
                case GoalKind.ResearchLevel: return Modifiers.Level(S, s.research);
                case GoalKind.ExploreStage: return MaxStageOpen;
                case GoalKind.Floors: return FloorsOf(s.side);
                case GoalKind.DispatchStaff: return S.castleStaff.FindAll(c => c.dispatched).Count;
                case GoalKind.StockGoods: return GoodsOf(s.building);
                case GoalKind.DispatchHunter: return S.hunters.FindAll(h => h.dispatched || h.restTimer > 0).Count;
                case GoalKind.CatchMonsters: { long n = 0; foreach (var e in S.caught) n += e.count; return n; }
                case GoalKind.ExhibitMonsters: { long n = 0; foreach (var b in S.buildings) if (b.type == BuildingType.Tank) foreach (var e in b.contents) n += e.count; return n; }
                case GoalKind.ArenaFighters: { long n = 0; foreach (var e in S.arena) n += e.count; return n; }
                case GoalKind.HireStaff: return StaffCount;
                case GoalKind.RegisterBook: return S.collectionClaimed.Count;
                case GoalKind.CollectGems: return S.stats.labCollects;
                case GoalKind.CollectAdmission: return S.stats.tankCollects;
                case GoalKind.SellMonsters: return S.stats.monstersSold;
                case GoalKind.Sales: return S.stats.sales;
                case GoalKind.AnyResearch: { long n = 0; foreach (var u in GameData.Upgrades) n += Modifiers.Level(S, u.id); return n; }
                case GoalKind.AnyBuildingLevel: { int lv = 0; foreach (var b in S.buildings) lv = Mathf.Max(lv, b.level); return lv; }
            }
            return 0;
        }

        public bool GoalDone(GoalSettings.Step s)
        {
            switch (s.kind)
            {
                case GoalKind.ShopsToCap: return LowestShopBelowCap() == null;
                case GoalKind.ShopsToMax:
                    foreach (var t in new[] { BuildingType.Restaurant, BuildingType.Souvenir })
                        if (CountOf(t) < GameData.Buildings[t].maxCount) return false;
                    return true;
                case GoalKind.BuildBuilding:
                {
                    int max = GameData.Buildings[s.building].maxCount;
                    return CountOf(s.building) >= (max > 0 ? Mathf.Min(s.amount, max) : s.amount);
                }
                case GoalKind.BuildingLevel: return MaxLevelOf(s.building) >= Mathf.Min(s.amount, GameData.Buildings[s.building].maxLevel);
                case GoalKind.ResearchLevel:
                {
                    var u = GameData.UpgradeById(s.research);
                    return u == null || Modifiers.Level(S, s.research) >= Mathf.Min(s.amount, u.maxLevel);
                }
                case GoalKind.ExploreStage: return MaxStageOpen >= Mathf.Min(s.amount, GameData.Regions.Count);
                case GoalKind.Floors: return FloorsOf(s.side) >= Mathf.Min(s.amount, s.side == FloorSide.Total ? GameData.MaxFloors * 2 : GameData.MaxFloors);
            }
            return GoalCurrent(s) >= s.amount;
        }
    }
}

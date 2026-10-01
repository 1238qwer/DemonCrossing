using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 연구 / 직원 고용 / 상점 구매
    public partial class Game
    {
        // ── 연구 ───────────────────────────────────────────────
        public string ResearchBlock(ResearchDef r)
        {
            if (S.research.Contains(r.id)) return L.T("완료", "Done");
            if (!string.IsNullOrEmpty(r.requires) && !S.research.Contains(r.requires))
                return L.T($"선행: {GameData.ResearchById(r.requires).Name}", $"Requires: {GameData.ResearchById(r.requires).Name}");
            if (S.rp < r.rp) return L.T("보석 부족", "Not enough Gems");
            if (S.gold < r.gold) return L.T("골드 부족", "Not enough Gold");
            return null;
        }

        public void DoResearch(ResearchDef r)
        {
            var block = ResearchBlock(r);
            if (block != null) { Fail(block); return; }
            S.rp -= r.rp;
            S.gold -= r.gold;
            S.research.Add(r.id);
            Mods.Recalculate(S);
            Sound.Play("upgrade");
            Notify(L.T($"[연구 완료] {r.Name}: {r.Desc}", $"[Research] {r.Name}: {r.Desc}"), "ic_research");
            MarkDirty();
        }

        // ── 10단계 업그레이드 ──────────────────────────────────
        public int UpgradeLevel(UpgradeLine u) => Modifiers.Level(S, u.id);

        public string UpgradeBlock(UpgradeLine u)
        {
            int next = UpgradeLevel(u) + 1;
            if (next > u.maxLevel) return L.T("최대 단계", "Maxed");
            if (S.rp < u.Rp(next)) return L.T("보석 부족", "Not enough Gems");
            if (S.gold < u.Gold(next)) return L.T("골드 부족", "Not enough Gold");
            return null;
        }

        public void DoUpgrade(UpgradeLine u)
        {
            var block = UpgradeBlock(u);
            if (block != null) { Fail(block); return; }
            int next = UpgradeLevel(u) + 1;
            S.rp -= u.Rp(next);
            S.gold -= u.Gold(next);
            S.research.Add(u.TierId(next));
            Mods.Recalculate(S);
            Sound.Play("upgrade");
            Notify(L.T($"[업그레이드] {u.Name} {next}단계: {u.effect(next)}", $"[Upgrade] {u.Name} Lv.{next}: {u.effect(next)}"), "ic_research");
            MarkDirty();
        }

        // ── 직원 ───────────────────────────────────────────────
        public int StaffCount => S.hunters.Count + S.castleStaff.Count;

        // 포획대원은 종류별 1명, 성 관리자는 여러 명 고용할 수 있다
        public List<StaffDef> HireCandidates()
        {
            var list = new List<StaffDef>();
            foreach (var d in GameData.Staff)
            {
                if (d.role == StaffRole.Hunter && S.hunters.Exists(h => h.staffId == d.id)) continue;
                list.Add(d);
            }
            return list;
        }

        public int HireCost(StaffDef d)
        {
            if (d.role == StaffRole.Hunter) return d.hireGold;
            int n = Mathf.Max(0, S.castleStaff.Count - 1); // 처음 받은 한 명은 빼고
            return Mathf.RoundToInt(d.hireGold * Mathf.Pow(GameData.StaffHireGrowth, n) / 100f) * 100;
        }

        public string HireBlock(StaffDef d)
        {
            if (StaffCount >= StaffCapacity()) return L.T("직원 숙소를 짓거나 업그레이드해야 더 고용할 수 있습니다.", "Build or upgrade a Staff Dorm to hire more.");
            if (S.gold < HireCost(d)) return L.T("골드 부족", "Not enough Gold");
            return null;
        }

        public void Hire(StaffDef d)
        {
            var block = HireBlock(d);
            if (block != null) { Fail(block); return; }
            S.gold -= HireCost(d);
            AddStaff(S, d);
            Sound.Play("upgrade");
            Notify(L.T($"{d.Name} 고용!", $"Hired {d.Name}!"), "ic_star");
            MarkDirty();
        }

        public void SetCastleDispatch(CastleStaffState c, bool on)
        {
            if (c.dispatched == on) return;
            c.dispatched = on;
            Sim.OnStaffDispatchChanged(c);
            Sound.Play("click");
            MarkDirty();
        }

        // 담당 구역: wing -1/0/1, floor -1/0~9
        public void SetStaffArea(CastleStaffState c, int wing, int floor)
        {
            c.wing = Mathf.Clamp(wing, -1, 1);
            c.floor = Mathf.Clamp(floor, -1, GameData.MaxFloors - 1);
            Sim.OnStaffAreaChanged(c);
            MarkDirty();
        }

        public static bool InArea(CastleStaffState c, BuildingState b)
        {
            if (c.wing >= 0 && WingOf(b.x) != c.wing) return false;
            if (c.floor >= 0 && (c.floor < b.floor || c.floor >= b.floor + b.ch)) return false;
            return true;
        }

        // ── 상점 ───────────────────────────────────────────────
        public bool BuyBait(int index, int count)
        {
            var b = GameData.Baits[index];
            if (!Spend(b.price * count, 0)) return false;
            CountList.Add(S.baits, b.id, count);
            Sound.Play("coin");
            MarkDirty();
            return true;
        }

        // 그 상품을 팔 수 있는 가게(레벨 충족)가 있어야 살 수 있다
        public bool CanSellItem(ShopItemDef item) => S.buildings.Exists(b => b.type == item.shop && b.level >= item.tier);

        public bool BuyGoods(ShopItemDef item, int count)
        {
            if (!CanSellItem(item)) { Fail(L.T($"{GameData.Buildings[item.shop].Name} Lv.{item.tier} 이상이 있어야 살 수 있습니다.", $"Requires a {GameData.Buildings[item.shop].Name} Lv.{item.tier}+.")); return false; }
            if (!Spend(item.buyPrice * count, 0)) return false;
            CountList.Add(S.goods, item.id, count);
            Sound.Play("coin");
            MarkDirty();
            return true;
        }
    }
}

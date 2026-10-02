using System;
using UnityEngine;

namespace Mawang
{
    // 사이드바 '다음 목표': 진행 중인 1회성 임무의 안내 + 보상. 모두 마치면 목표 칸이 사라진다(진행은 Game.Goals).
    public partial class GameUI
    {
        string GoalText()
        {
            var s = g.CurrentGoal;
            if (s == null) return "";
            string reward = s.rewardGems > 0 ? L.T($"보상 {s.rewardGold:N0}골드 · 보석 {s.rewardGems}", $"Reward {s.rewardGold:N0} Gold · {s.rewardGems} Gems") : L.T($"보상 {s.rewardGold:N0}골드", $"Reward {s.rewardGold:N0} Gold");
            return $"{GoalBody(s)}\n{UIKit.Col(reward, UIKit.Gold)}";
        }

        string TargetName(GoalSettings.Step s)
        {
            switch (s.kind)
            {
                case GoalKind.BuildBuilding:
                case GoalKind.BuildingLevel:
                case GoalKind.StockGoods: return GameData.Buildings[s.building].Name;
                case GoalKind.ResearchLevel: return GameData.UpgradeById(s.research)?.Name ?? s.research;
                case GoalKind.ExploreStage: return s.amount >= 1 && s.amount <= GameData.Regions.Count ? GameData.Regions[s.amount - 1].Name : "";
                case GoalKind.Floors: return s.side == FloorSide.Left ? L.T("왼쪽 날개", "Left wing") : s.side == FloorSide.Right ? L.T("오른쪽 날개", "Right wing") : L.T("마왕성", "Castle");
            }
            return "";
        }

        string GoalBody(GoalSettings.Step s)
        {
            string custom = L.En ? s.en : s.ko;
            if (!string.IsNullOrEmpty(custom))
            {
                try { return string.Format(custom, s.amount, g.GoalCurrent(s), TargetName(s)); }
                catch (FormatException) { return custom; }
            }
            return AutoText(s);
        }

        // 문구를 비웠을 때 쓰는 자동 안내
        string AutoText(GoalSettings.Step s)
        {
            string name = TargetName(s);
            long now = g.GoalCurrent(s);
            switch (s.kind)
            {
                case GoalKind.BuildBuilding:
                    if (s.building == BuildingType.Arena && !g.Mods.arenaUnlocked) return L.T($"[연구 > 운영] 건물 연구 {GameData.ArenaResearchLevel}단계를 완료하면 투기장을 지을 수 있습니다.", $"[Research > Ops] Architecture Lv.{GameData.ArenaResearchLevel} unlocks the Arena.");
                    return s.amount <= 1 ? L.T($"[건설] {name}을(를) 지으세요.", $"[Build] Build a {name}.") : L.T($"[건설] {name}을(를) {s.amount}개까지 지으세요. (지금 {now}개)", $"[Build] Build {s.amount} {name}s. (now {now})");
                case GoalKind.BuildingLevel:
                    if (g.CountOf(s.building) == 0) return L.T($"[건설] {name}을(를) 지으세요.", $"[Build] Build a {name}.");
                    return L.T($"{name}을(를) 길게 눌러 Lv.{s.amount}로 업그레이드하세요.", $"Hold the {name} and upgrade it to Lv.{s.amount}.");
                case GoalKind.AnyBuildingLevel: return L.T($"건물을 길게 눌러 Lv.{s.amount}로 업그레이드하세요.", $"Hold a building and upgrade it to Lv.{s.amount}.");
                case GoalKind.ShopsToCap:
                {
                    var b = g.LowestShopBelowCap();
                    return b == null ? "" : L.T($"{GameData.Buildings[b.type].Name}을(를) Lv.{b.level + 1}로 업그레이드하세요.", $"Upgrade the {GameData.Buildings[b.type].Name} to Lv.{b.level + 1}.");
                }
                case GoalKind.ShopsToMax: return L.T("[건설] 식당과 기념품점을 최대 개수까지 지으세요.", "[Build] Build Diners and Gift Shops up to the limit.");
                case GoalKind.ResearchLevel: return L.T($"[연구] {name} {s.amount}단계를 완료하세요. (지금 {now}단계)", $"[Research] Complete {name} Lv.{s.amount}. (now {now})");
                case GoalKind.AnyResearch: return L.T("[연구] 보석으로 업그레이드를 해 보세요.", "[Research] Spend Gems on an upgrade.");
                case GoalKind.ExploreStage: return L.T($"[연구 > 증축 · 탐사] {name} 탐사", $"[Research > Expand] Explore {name}");
                case GoalKind.Floors: return L.T($"{name}을(를) {s.amount}층까지 증축하세요. (지금 {now}층)", $"Expand the {name} to {s.amount} floors. (now {now})");
                case GoalKind.DispatchStaff: return L.T("[직원] 성 관리자를 파견하세요.", "[Staff] Dispatch castle staff.");
                case GoalKind.StockGoods: return L.T($"[상점] 재고를 {s.amount}개 이상 채우세요. (지금 {now}개)", $"[Shop] Stock {s.amount}+ goods. (now {now})");
                case GoalKind.DispatchHunter: return L.T("[포획장] 포획대원을 파견하세요.", "[Hunt] Send a hunter.");
                case GoalKind.CatchMonsters: return L.T($"괴물을 {s.amount}마리 잡으세요. (지금 {now}마리)", $"Catch {s.amount} monsters. (now {now})");
                case GoalKind.ExhibitMonsters: return L.T($"전시 우리에 괴물을 {s.amount}마리 넣으세요. (지금 {now}마리)", $"Put {s.amount} monsters in cages. (now {now})");
                case GoalKind.ArenaFighters: return L.T($"투기장에 출전 괴물을 {s.amount}마리 넣으세요.", $"Add {s.amount} fighters to the Arena.");
                case GoalKind.HireStaff: return L.T($"[직원] 직원을 {s.amount}명까지 고용하세요. (지금 {now}명)", $"[Staff] Have {s.amount} staff. (now {now})");
                case GoalKind.RegisterBook: return L.T($"[도감] 괴물을 {s.amount}종 등록하세요. (지금 {now}종)", $"[Book] Register {s.amount} monsters. (now {now})");
                case GoalKind.CollectGems: return L.T("연구소를 탭해 보석을 수거하세요.", "Tap the Lab to collect Gems.");
                case GoalKind.CollectAdmission: return L.T("전시 우리를 탭해 관람료를 받으세요.", "Tap a cage to collect admission.");
                case GoalKind.SellMonsters: return L.T("[상점 > 괴물 판매]에서 괴물을 팔아 보세요.", "Sell a monster in [Shop > Sell].");
                case GoalKind.Sales: return L.T($"음식이나 기념품을 {s.amount}번 파세요. ({now}/{s.amount})", $"Make {s.amount} sales. ({now}/{s.amount})");
            }
            return "";
        }
    }
}

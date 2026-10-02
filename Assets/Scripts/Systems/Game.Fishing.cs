using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mawang
{
    // 포획(원작: 낚시). 파견된 포획대원만 작업한다.
    // 미끼 × 힘 조합이 잡히는 괴물 목록을 결정한다(성능 차이가 아니라 종류 차이).
    // 사이클이 시작될 때 미끼를 쓰고 잡을 괴물을 미리 굴린다 → 포획 장면에서 그 괴물이 다가와 잡힌다.
    public partial class Game
    {
        const float HiddenWeight = 0.08f;

        public event Action<HunterState, List<string>> HuntCaught;   // 포획 성공 (장면 연출)
        public event Action<TrashItem, Currency, int> TrashCollected; // 쓰레기 수거 (장면 연출)

        public StaffDef Def(HunterState h) => GameData.StaffById(h.staffId);

        public float HuntInterval(HunterState h) => Def(h).interval * Mods.fishingIntervalMul;
        public int HuntDurabilityMax(HunterState h) => Def(h).durability + Mods.durabilityAdd;

        // UI 미리보기용: 히든은 표시하지 않는다
        public string[] PreviewPool(string regionId, int bait, Power power) =>
            GameData.RegionById(regionId).pools[bait, (int)power];

        public string HunterStatus(HunterState h)
        {
            if (h.restTimer > 0) return h.penalty ? L.T($"복귀 대기 {h.restTimer:0}초", $"Returning {h.restTimer:0}s") : L.T($"체력 회복 중 {h.restTimer:0}초", $"Resting {h.restTimer:0}s");
            if (!h.dispatched) return PickBait(h) < 0 ? L.T("대기 중 (미끼 없음)", "Idle (no bait)") : L.T("대기 중", "Idle");
            return L.T($"포획 중 ({h.timer / HuntInterval(h) * 100f:0}%)", $"Hunting ({h.timer / HuntInterval(h) * 100f:0}%)");
        }

        // 대기 중인 포획대원의 스테이지 지정
        public void Assign(HunterState h, string regionId)
        {
            if (h.dispatched || h.regionId == regionId) return;
            h.regionId = regionId;
            MarkDirty();
        }

        public void Dispatch(HunterState h)
        {
            if (h.restTimer > 0) { Fail(L.T("아직 파견할 수 없습니다.", "Can't dispatch yet.")); return; }
            var r = GameData.RegionById(h.regionId);
            if (!Modifiers.RegionUnlocked(S, r)) { Fail(L.T("잠긴 스테이지입니다.", "This stage is locked.")); return; }
            if (CountList.Get(S.baits, GameData.Baits[h.bait].id) <= 0 && !(h.autoBait && PickBait(h) >= 0))
            {
                Fail(L.T("미끼가 없습니다. 상점에서 구입하세요.", "No bait. Buy some in the Shop."));
                return;
            }
            h.dispatched = true;
            h.timer = 0;
            h.pending.Clear();
            if (h.durabilityLeft <= 0) h.durabilityLeft = HuntDurabilityMax(h);
            Sound.Play("click");
            MarkDirty();
        }

        // 원작: 수동 복귀 시 약 50초 패널티. 쓰던 미끼는 돌려준다.
        public void Recall(HunterState h)
        {
            if (!h.dispatched) return;
            RefundPending(h);
            h.dispatched = false;
            h.restTimer = GameData.HunterRecallPenalty;
            h.penalty = true;
            MarkDirty();
        }

        void RefundPending(HunterState h)
        {
            if (h.pending.Count > 0 && h.pendingBait >= 0) CountList.Add(S.baits, GameData.Baits[h.pendingBait].id, 1);
            h.pending.Clear();
            h.pendingBait = -1;
        }

        int PickBait(HunterState h)
        {
            for (int i = 0; i < GameData.Baits.Length; i++)
            {
                int b = (h.bait + i) % GameData.Baits.Length;
                if (CountList.Get(S.baits, GameData.Baits[b].id) > 0) return b;
            }
            return -1;
        }

        void TickFishing(float dt)
        {
            foreach (var h in S.hunters)
            {
                if (h.restTimer > 0)
                {
                    h.restTimer -= dt;
                    if (h.restTimer <= 0)
                    {
                        h.restTimer = 0;
                        h.durabilityLeft = HuntDurabilityMax(h);
                        if (!h.penalty)
                        {
                            // 체력 회복 후 같은 설정으로 자동 재출발
                            h.dispatched = true;
                            h.timer = 0;
                            h.pending.Clear();
                            Notify(L.T($"[{Def(h).Name}] 회복 완료, 다시 출발합니다.", $"[{Def(h).Name}] Recovered and heading out again."));
                        }
                        h.penalty = false;
                        MarkDirty();
                    }
                    continue;
                }
                if (!h.dispatched) continue;
                if (h.pending.Count == 0 && !StartCycle(h)) continue;

                h.timer += dt;
                if (h.timer < HuntInterval(h)) continue;
                h.timer = 0;
                HuntOnce(h);
            }
        }

        // 사이클 시작: 미끼 1개 사용, 잡을 괴물을 미리 결정
        bool StartCycle(HunterState h)
        {
            var def = Def(h);
            if (CountList.Get(S.baits, GameData.Baits[h.bait].id) <= 0)
            {
                int next = h.autoBait ? PickBait(h) : -1;
                if (next < 0)
                {
                    h.dispatched = false;
                    Notify(L.T($"[{def.Name}] 미끼가 떨어져 포획을 멈췄습니다.", $"[{def.Name}] Out of bait, stopped hunting."), "bait_worm");
                    MarkDirty();
                    return false;
                }
                h.bait = next;
                Notify(L.T($"[{def.Name}] 미끼를 {GameData.Baits[next].Name}(으)로 자동 교체", $"[{def.Name}] Switched bait to {GameData.Baits[next].Name}"));
                MarkDirty();
            }
            CountList.Add(S.baits, GameData.Baits[h.bait].id, -1);
            h.pendingBait = h.bait;
            h.timer = 0;
            var region = GameData.RegionById(h.regionId);
            for (int i = 0; i < def.amount; i++) h.pending.Add(RollCatch(region, h.bait, h.power));
            return true;
        }

        void HuntOnce(HunterState h)
        {
            var def = Def(h);
            var caught = new List<string>(h.pending);
            foreach (var id in caught) Catch(id);
            h.pending.Clear();
            h.pendingBait = -1;
            HuntCaught?.Invoke(h, caught);

            h.durabilityLeft--;
            if (h.durabilityLeft <= 0)
            {
                h.dispatched = false;
                h.restTimer = Mods.restTime;
                h.penalty = false;
                Notify(L.T($"[{def.Name}] 체력이 다해 귀환했습니다. ({Mods.restTime:0}초 회복)", $"[{def.Name}] Exhausted, resting for {Mods.restTime:0}s."));
            }
            MarkDirty();
        }

        public static string RollCatch(RegionDef region, int bait, Power power)
        {
            var pool = region.pools[bait, (int)power];
            bool hiddenHere = region.hiddenBait == bait && region.hiddenPower == power;
            float total = pool.Length + (hiddenHere ? HiddenWeight : 0f);
            float roll = Random.value * total;
            if (roll >= pool.Length) return region.hiddenId;
            return pool[Mathf.Min((int)roll, pool.Length - 1)];
        }

        void Catch(string monsterId)
        {
            var m = GameData.MonsterById[monsterId];
            bool first = CountList.Get(S.caught, monsterId) == 0;
            CountList.Add(S.monsters, monsterId, 1);
            CountList.Add(S.caught, monsterId, 1);
            if (first) Notify(m.hidden ? L.T($"[히든 발견!] {m.Name}", $"[Hidden found!] {m.Name}") : L.T($"[신규 포획] {m.Name}", $"[New catch] {m.Name}"), "ic_star");
        }

        // ── 판매 ───────────────────────────────────────────────
        public bool SellMonster(string id, int count)
        {
            count = Mathf.Min(count, CountList.Get(S.monsters, id));
            if (count <= 0) return false;
            var m = GameData.MonsterById[id];
            CountList.Add(S.monsters, id, -count);
            Earn(m.sellCurrency, m.sellPrice * count);
            S.stats.monstersSold += count;
            Sound.Play("coin");
            MarkDirty();
            return true;
        }

        // ── 쓰레기: 스테이지 바닥에 떨어지고, 클릭해서 줍는다 ──
        float trashTimer;

        public int TrashCount(string regionId) => S.trashItems.FindAll(t => t.regionId == regionId).Count;

        void TickTrash(float dt)
        {
            // 구버전 세이브: 지역별 개수 → 바닥 쓰레기
            if (S.trash.Count > 0)
            {
                foreach (var e in S.trash) for (int i = 0; i < e.count; i++) SpawnTrash(e.id);
                S.trash.Clear();
            }

            trashTimer += dt;
            if (trashTimer < GameData.TrashInterval) return;
            trashTimer = 0;
            foreach (var r in GameData.Regions)
            {
                if (!Modifiers.RegionUnlocked(S, r)) continue;
                if (TrashCount(r.id) < GameData.TrashMaxPerRegion) SpawnTrash(r.id); // 장면이 직접 따라 그린다(UI 재구성 없음)
            }
        }

        void SpawnTrash(string regionId)
        {
            S.trashItems.Add(new TrashItem
            {
                uid = S.nextTrashUid++,
                regionId = regionId,
                x = Random.Range(GameData.TrashMinX, GameData.StageW - 12f),
                y = Random.Range(3f, GameData.StageGroundY - 8f),
                can = Random.value < 0.5f,
            });
        }

        // 원작: 페트병 → 자재 20 / 캔 → 20골드
        public void CollectTrash(TrashItem t)
        {
            if (!S.trashItems.Remove(t)) return;
            var cur = t.can ? Currency.Gold : Currency.Material;
            int amt = Mathf.RoundToInt(GameData.TrashReward * Mods.trashMul * (cur == Currency.Gold ? GameData.GoldIncomeMul : 1f)); // 캔(골드)은 반복 수입 배율
            Earn(cur, amt);
            S.stats.trash++;
            TrashCollected?.Invoke(t, cur, amt);
            Sound.Play("coin");
        }

        // ── 도감 ───────────────────────────────────────────────
        public bool CanClaimCollection(string id) =>
            CountList.Get(S.caught, id) >= GameData.CollectionRegisterCount && !S.collectionClaimed.Contains(id);

        public void ClaimCollection(string id)
        {
            if (!CanClaimCollection(id)) return;
            S.collectionClaimed.Add(id);
            var m = GameData.MonsterById[id];
            int gold = GameData.CollectionGold(m), gems = GameData.CollectionGems(m);
            EarnGold(gold);
            S.rp += gems;
            Sound.Play("gem");
            Notify(L.T($"[도감 등록] {m.Name} (+{gold:N0}골드, 보석 +{gems})", $"[Collection] {m.Name} (+{gold:N0} Gold, +{gems} Gems)"), "ic_book");
            MarkDirty();
        }

        public IEnumerable<MonsterDef> MonstersOf(string regionId)
        {
            foreach (var m in GameData.Monsters) if (m.region == regionId) yield return m;
        }
    }
}

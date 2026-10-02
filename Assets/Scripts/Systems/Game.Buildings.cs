using UnityEngine;

namespace Mawang
{
    // 건설 / 업그레이드 / 철거 / 성 증축, 전시 우리, 연구소
    public partial class Game
    {
        // ── 칸 좌표 ────────────────────────────────────────────
        // x: 0~3 왼쪽 날개, 4~7 오른쪽 날개. 시뮬레이션 x(칸 단위)는 엘리베이터 폭만큼 오른쪽 날개가 밀린다.
        public static int WingOf(int x) => x < GameData.WingWidth ? 0 : 1;
        public static float SimX(int x) => x < GameData.WingWidth ? x : x + GameData.ElevatorCells;
        public static float ElevatorX => GameData.WingWidth + GameData.ElevatorCells / 2f;

        public int FloorsOpen(int wing) => wing == 0 ? S.floorsLeft : S.floorsRight;
        public int TotalFloorsOpen => S.floorsLeft + S.floorsRight;
        public bool CellOpen(int floor, int x) => floor >= 0 && x >= 0 && x < GameData.TotalWidth && floor < FloorsOpen(WingOf(x));

        public int CountOf(BuildingType t) => S.buildings.FindAll(x => x.type == t).Count;

        public BuildingState BuildingAt(int floor, int x)
        {
            foreach (var b in S.buildings)
                if (floor >= b.floor && floor < b.floor + b.ch && x >= b.x && x < b.x + b.cw) return b;
            return null;
        }

        public BuildingState BuildingByUid(int uid)
        {
            foreach (var b in S.buildings) if (b.uid == uid) return b;
            return null;
        }

        // 사각형 영역이 한 날개 안에 있고 모두 열린 빈 칸인지
        public bool IsAreaFree(int floor, int x, int w, int h)
        {
            if (w < 1 || h < 1 || WingOf(x) != WingOf(x + w - 1)) return false;
            for (int f = floor; f < floor + h; f++)
                for (int i = x; i < x + w; i++)
                    if (!CellOpen(f, i) || BuildingAt(f, i) != null) return false;
            return true;
        }

        // 건설 비용: 전시 우리는 칸 수만큼
        public static int BuildGold(BuildingType t, int cells) => GameData.Buildings[t].costGold * (t == BuildingType.Tank ? cells : 1);
        public static int BuildMat(BuildingType t, int cells) => GameData.Buildings[t].costMat * (t == BuildingType.Tank ? cells : 1);

        public string BuildBlock(BuildingType type, int floor, int x, int w = 1, int h = 1)
        {
            var def = GameData.Buildings[type];
            if (type == BuildingType.Arena && !Mods.arenaUnlocked) return L.T($"[연구 > 건물 연구] {GameData.ArenaResearchLevel}단계를 완료해야 지을 수 있습니다.", $"Requires [Research > Architecture] Lv.{GameData.ArenaResearchLevel}.");
            if (def.maxCount > 0 && CountOf(type) >= def.maxCount) return L.T($"{def.Name}은(는) 최대 {def.maxCount}개까지 지을 수 있습니다.", $"You can build up to {def.maxCount} {def.Name}.");
            if (!IsAreaFree(floor, x, w, h)) return L.T("그 자리에는 지을 수 없습니다.", "Can't build there.");
            if (!CanAfford(BuildGold(type, w * h), BuildMat(type, w * h))) return L.T("재화가 부족합니다.", "Not enough resources.");
            return null;
        }

        public bool TryBuild(BuildingType type, int floor, int x, int w = 1, int h = 1, bool quiet = false)
        {
            if (type != BuildingType.Tank) { w = 1; h = 1; }
            var block = BuildBlock(type, floor, x, w, h);
            if (block != null) { if (!quiet) Fail(block); return false; }
            int gold = BuildGold(type, w * h), mat = BuildMat(type, w * h);
            Spend(gold, mat);
            S.buildings.Add(new BuildingState
            {
                uid = S.nextUid++, type = type, floor = floor, x = x, cw = w, ch = h,
                investedGold = gold, investedMat = mat,
            });
            Sound.Play("build");
            Notify(w * h > 1 ? L.T($"{GameData.Buildings[type].Name} {w}×{h} 건설 완료", $"Built a {w}×{h} {GameData.Buildings[type].Name}") : L.T($"{GameData.Buildings[type].Name} 건설 완료", $"Built {GameData.Buildings[type].Name}"));
            MarkDirty();
            return true;
        }

        public bool CanUpgrade(BuildingState b, out int gold, out int mat, out string reason)
        {
            var def = GameData.Buildings[b.type];
            gold = mat = 0;
            reason = null;
            if (b.level >= def.maxLevel) { reason = L.T("최대 레벨", "Max level"); return false; }
            int mul = b.type == BuildingType.Tank ? b.Cells : 1;
            gold = def.upGold[b.level - 1] * mul;
            mat = def.upMat[b.level - 1] * mul;
            if (b.level >= Mods.buildingLevelCap) { reason = L.T($"[연구 > 건물 연구] {b.level}단계를 완료해야 Lv.{b.level + 1}로 올릴 수 있습니다.", $"Requires [Research > Architecture] Lv.{b.level} for Lv.{b.level + 1}."); return false; }
            if (!CanAfford(gold, mat)) { reason = L.T("재화가 부족합니다.", "Not enough resources."); return false; }
            return true;
        }

        public void Upgrade(BuildingState b)
        {
            if (!CanUpgrade(b, out int gold, out int mat, out string reason)) { Fail(reason); return; }
            if (!Spend(gold, mat)) return;
            b.level++;
            b.investedGold += gold;
            b.investedMat += mat;
            Sound.Play("upgrade");
            Notify($"{GameData.Buildings[b.type].Name} Lv.{b.level}");
            MarkDirty();
        }

        // 원작: 철거 시 투자한 재화 100% 환급
        public void Demolish(BuildingState b)
        {
            if (b.type == BuildingType.Tank)
            {
                EarnGold(Mathf.FloorToInt(b.accumulated)); // 철거는 행동이라 최대 보유량과 무관하게 다 받는다
                foreach (var e in b.contents) CountList.Add(S.monsters, e.id, e.count);
            }
            if (b.type == BuildingType.Lab) S.rp += b.labRp;

            S.gold += b.investedGold;
            S.material += b.investedMat;
            S.buildings.Remove(b);
            Sim.OnBuildingRemoved(b);
            Sound.Play("build");
            Notify(L.T($"{GameData.Buildings[b.type].Name} 철거 (투자금 {b.investedGold:N0}G / {b.investedMat:N0}자재 환급)",
                       $"Demolished {GameData.Buildings[b.type].Name} (refunded {b.investedGold:N0}G / {b.investedMat:N0} Mat)"));
            MarkDirty();
        }

        public int StaffCapacity()
        {
            int cap = GameData.BaseStaffCap;
            foreach (var b in S.buildings) if (b.type == BuildingType.Dorm) cap += b.level - 1; // 숙소는 Lv.2부터 +1
            return cap;
        }

        // ── 성 증축: 왼쪽·오른쪽 날개를 한 층씩 ─────────────────
        public int NextFloor(int wing) => FloorsOpen(wing) + 1; // 열 층 번호(1부터)

        public string ExpandBlock(int wing)
        {
            int n = NextFloor(wing);
            if (n > GameData.MaxFloors) return L.T("최대 층", "Max floor");
            if (!CanAfford(GameData.ExpandGold(n), GameData.ExpandMat(n))) return L.T("재화 부족", "Not enough resources");
            return null;
        }

        public void Expand(int wing)
        {
            var block = ExpandBlock(wing);
            if (block != null) { Fail(block); return; }
            int n = NextFloor(wing);
            Spend(GameData.ExpandGold(n), GameData.ExpandMat(n));
            if (wing == 0) S.floorsLeft = n; else S.floorsRight = n;
            Sound.Play("upgrade");
            Notify(L.T($"[성 증축] {(wing == 0 ? "왼쪽" : "오른쪽")} {n}층 개방!", $"[Expansion] {(wing == 0 ? "Left" : "Right")} wing floor {n} opened!"), "up_floor");
            MarkDirty();
        }

        // ── 전시 우리 ──────────────────────────────────────────
        // 크기 = 칸 수 × (레벨 + 1)
        public static int TankSize(BuildingState b) => b.Cells * (b.level + 1);
        public int TankSpaceMax(BuildingState b) => TankSize(b) * GameData.TankSpacePerSize;
        public int TankMakiBase(BuildingState b) => TankSize(b) * (GameData.TankMakiPerSize + Mods.makiPerCellAdd);
        public float TankCap(BuildingState b) => Mathf.Max(TankSize(b) * GameData.TankCapPerSize, TankRatePerMin(b) * GameData.TankCapMinutes) * Mods.tankCapMul;

        public int TankSpaceUsed(BuildingState b)
        {
            int n = 0;
            foreach (var e in b.contents) n += GameData.MonsterById[e.id].space * e.count;
            return n;
        }

        // 남은 항마력 = 기본 항마력 + Σ(괴물 항마력). 0 미만이 되면 넣을 수 없다.
        public int TankMaki(BuildingState b)
        {
            int n = TankMakiBase(b);
            foreach (var e in b.contents) n += GameData.MonsterById[e.id].maki * e.count;
            return n;
        }

        public float TankRatePerMin(BuildingState b)
        {
            float r = 0;
            foreach (var e in b.contents) r += GameData.MonsterById[e.id].admission * e.count;
            return r * Mods.admissionMul;
        }

        public string CanAddToTank(BuildingState b, string monsterId)
        {
            var m = GameData.MonsterById[monsterId];
            if (CountList.Get(S.monsters, monsterId) <= 0) return L.T("보관함에 없음", "None in storage");
            if (TankSpaceUsed(b) + m.space > TankSpaceMax(b)) return L.T("공간 부족", "Not enough space");
            if (TankMaki(b) + m.maki < 0) return L.T("항마력 부족 (마력초를 넣으세요)", "Not enough Ward (add Mana Herbs)");
            return null;
        }

        public bool AddToTank(BuildingState b, string monsterId)
        {
            var err = CanAddToTank(b, monsterId);
            if (err != null) { Fail(err); return false; }
            CountList.Add(S.monsters, monsterId, -1);
            CountList.Add(b.contents, monsterId, 1);
            Sound.Play("pop");
            MarkDirty();
            return true;
        }

        public bool RemoveFromTank(BuildingState b, string monsterId)
        {
            if (CountList.Get(b.contents, monsterId) <= 0) return false;
            var m = GameData.MonsterById[monsterId];
            // 마력초를 빼서 항마력이 음수가 되면 안 된다
            if (m.maki > 0 && TankMaki(b) - m.maki < 0) { Fail(L.T("마력초를 빼면 항마력이 부족해집니다.", "Removing this herb would drop Ward below zero.")); return false; }
            CountList.Add(b.contents, monsterId, -1);
            CountList.Add(S.monsters, monsterId, 1);
            Sound.Play("click");
            MarkDirty();
            return true;
        }

        // 관람료 수령: 최대 보유 골드까지만 받고, 못 받은 몫은 우리에 남는다
        public int CollectTank(BuildingState b, float bonus)
        {
            int whole = Mathf.FloorToInt(b.accumulated);
            if (whole <= 0) return 0;
            int room = Mathf.Max(0, GoldCap - S.gold);
            int take = Mathf.Min(whole, Mathf.FloorToInt(room / (1f + bonus)));
            if (take <= 0) return 0;
            b.accumulated -= take;
            S.stats.tankCollects++;
            return EarnAutoGold(Mathf.FloorToInt(take * (1f + bonus)));
        }

        void TickBuildings(float dt)
        {
            foreach (var b in S.buildings)
            {
                if (b.type == BuildingType.Tank)
                {
                    if (GoldFull) continue; // 최대 보유 골드에 닿으면 관람료도 멈춘다
                    b.accumulated = Mathf.Min(TankCap(b), b.accumulated + TankRatePerMin(b) / 60f * dt);
                }
                else if (b.type == BuildingType.Lab && b.labRp < LabRpCap && !GemFull)
                {
                    b.labTimer += dt;
                    float interval = LabInterval(b);
                    if (b.labTimer >= interval) { b.labTimer -= interval; b.labRp++; }
                }
            }
        }

        // ── 투기장 ─────────────────────────────────────────────
        // 출전 괴물은 모든 투기장이 함께 쓰는 재고(S.arena). 용사가 오면 가장 강한 괴물이 대련한다.
        public int MaxStageOpen
        {
            get
            {
                int s = 1;
                foreach (var r in GameData.Regions) if (Modifiers.RegionUnlocked(S, r)) s = Mathf.Max(s, r.stage);
                return s;
            }
        }

        public float HeroPower => GameData.HeroPower(MaxStageOpen);
        public float ArenaLevelMul(BuildingState b) => 1f + GameData.ArenaLevelBonus * (b.level - 1);
        public int ArenaReward(BuildingState b, MonsterDef m, BoutResult r) => Mathf.RoundToInt(m.power * GameData.ArenaRewardPerPower * GameData.ArenaRewardMul[(int)r] * ArenaLevelMul(b));

        public MonsterDef ArenaChampion()
        {
            MonsterDef best = null;
            foreach (var e in S.arena)
            {
                var m = GameData.MonsterById[e.id];
                if (best == null || m.power > best.power) best = m;
            }
            return best;
        }

        public string CanAddToArena(string monsterId)
        {
            if (GameData.MonsterById[monsterId].isPlant) return L.T("마력초는 대련할 수 없습니다.", "Mana Herbs can't fight.");
            if (CountList.Get(S.monsters, monsterId) <= 0) return L.T("보관함에 없음", "None in storage");
            return null;
        }

        public bool AddToArena(string monsterId)
        {
            var err = CanAddToArena(monsterId);
            if (err != null) { Fail(err); return false; }
            CountList.Add(S.monsters, monsterId, -1);
            CountList.Add(S.arena, monsterId, 1);
            Sound.Play("pop");
            MarkDirty();
            return true;
        }

        public bool RemoveFromArena(string monsterId)
        {
            if (CountList.Get(S.arena, monsterId) <= 0) return false;
            CountList.Add(S.arena, monsterId, -1);
            CountList.Add(S.monsters, monsterId, 1);
            Sound.Play("click");
            MarkDirty();
            return true;
        }

        // 대련 1회. 출전 괴물이 없거나 최대 보유 골드면 대련하지 않는다(null). 대실패면 괴물이 죽는다.
        public (BoutResult result, int gold, MonsterDef monster)? ArenaBout(BuildingState b)
        {
            var m = ArenaChampion();
            if (m == null || GoldFull) return null;
            var odds = GameData.BoutOdds(m.power, HeroPower);
            float roll = Random.value;
            int i = 0;
            for (; i < odds.Length - 1; i++) { roll -= odds[i]; if (roll < 0) break; }
            var r = (BoutResult)i;
            int gold = EarnAutoGold(ArenaReward(b, m, r));
            S.stats.arenaBouts++;
            if (r >= BoutResult.Win) S.stats.arenaWins++;
            if (r == BoutResult.GreatWin) S.stats.arenaGreat++;
            if (r == BoutResult.GreatFail)
            {
                CountList.Add(S.arena, m.id, -1);
                Notify(L.T($"[투기장] {m.Name}이(가) 대련 중 쓰러졌습니다.", $"[Arena] {m.Name} fell in battle."));
                MarkDirty();
            }
            return (r, gold, m);
        }

        public int LabRpCap => GameData.LabRpCap;
        public float LabInterval(BuildingState b) => GameData.LabInterval[Mathf.Clamp(b.level - 1, 0, GameData.LabInterval.Length - 1)] / Mods.labSpeedMul;

        // 보석 수거: 최대 보유량까지만, 남은 보석은 연구소에 둔다. 받은 개수를 돌려준다.
        public int CollectLab(BuildingState b)
        {
            int n = Mathf.Min(b.labRp, Mathf.Max(0, GemCap - S.rp));
            if (n <= 0) return 0;
            S.rp += n;
            S.stats.labCollects++;
            b.labRp -= n;
            return n;
        }

        // 모두 수령: 모든 전시 우리 관람료 + 연구소 보석
        public (int gold, int gems) CollectAll()
        {
            int gold = 0, gems = 0;
            foreach (var b in S.buildings)
            {
                if (b.type == BuildingType.Tank) gold += CollectTank(b, 0f);
                else if (b.type == BuildingType.Lab) gems += CollectLab(b);
            }
            if (gold > 0 || gems > 0) Sound.Play(gems > 0 ? "gem" : "coin");
            return (gold, gems);
        }
    }
}

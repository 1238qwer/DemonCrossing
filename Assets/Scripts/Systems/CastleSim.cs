using System.Collections.Generic;
using UnityEngine;

namespace Mawang
{
    // 성 내부 시뮬레이션: 손님과 성 관리자의 이동, 작업 큐.
    // 좌표: x = 칸 단위(왼쪽 날개 0~4, 엘리베이터 4~4.2, 오른쪽 날개 4.2~8.2), y = 층(0 = 1층).
    public class Agent
    {
        public Vector2 pos;
        public readonly List<Vector2> path = new List<Vector2>();
        public float speedMul = 1f;
        public bool Moving => path.Count > 0;
        // 엘리베이터로 층 사이를 이동 중 (화면에서는 사라졌다가 도착 층에서 나타난다)
        public bool InElevator => path.Count > 0 && Mathf.Abs(path[0].x - pos.x) < 0.001f && Mathf.Abs(path[0].y - pos.y) > 0.001f;

        public void GoTo(Vector2 target)
        {
            path.Clear();
            Vector2 from = pos;
            float ex = Game.ElevatorX;
            if (Mathf.Abs(from.y - target.y) > 0.01f)
            {
                path.Add(new Vector2(ex, from.y));
                path.Add(new Vector2(ex, target.y));
            }
            path.Add(target);
        }

        public void Step(float dt)
        {
            while (path.Count > 0 && dt > 0)
            {
                Vector2 t = path[0];
                bool vertical = Mathf.Abs(t.x - pos.x) < 0.001f && Mathf.Abs(t.y - pos.y) > 0.001f;
                float speed = vertical ? 1f / GameData.ElevatorSecPerFloor : GameData.WalkSpeed * speedMul;
                float dist = Vector2.Distance(pos, t);
                float move = speed * dt;
                if (move >= dist)
                {
                    pos = t;
                    path.RemoveAt(0);
                    dt -= dist / speed;
                }
                else
                {
                    pos = Vector2.MoveTowards(pos, t, move);
                    dt = 0;
                }
            }
        }
    }

    public enum VisitorState { Walking, Watching, WaitingService, Sparring, Leaving }

    // 용사 손님 (롤러코스터 타이쿤처럼 소지금·체력·기분을 가지고 한 곳씩 골라 들른다)
    public class Visitor : Agent
    {
        public int wallet;
        public float energy, happy;  // 체력(0이면 집에 간다) · 기분(0~100, 낮으면 집에 간다)
        public float appetite;       // 식욕 0~1: 높으면 식당, 낮으면 기념품점을 좋아한다
        public int visited;          // 들른 장소 수
        public readonly HashSet<int> seen = new HashSet<int>(); // 들른 건물 uid (다시 고르지 않는다)
        public VisitorState state;
        public int currentStop;
        public float timer, patience;
        public ShopItemDef wantItem;
        public Job job;
        public Color color;
        public int look;   // 외형 스프라이트 번호
    }

    public enum JobType { Serve, CollectLab, CollectTank }

    public class Job
    {
        public JobType type;
        public BuildingState building;
        public Visitor visitor;
        public StaffAgent staff;
    }

    public class StaffAgent : Agent
    {
        public CastleStaffState state;
        public StaffDef def;
        public Job job;
        public float workTimer;
        public bool working;
    }

    public class CastleSim
    {
        /// <summary>게임 인스턴스 참조</summary>
        /// <summary>게임 인스턴스 참조</summary>
        readonly Game g;
        public readonly List<Visitor> visitors = new List<Visitor>();
        public readonly List<StaffAgent> staff = new List<StaffAgent>();
        readonly List<Job> jobs = new List<Job>();
        float spawnTimer;

        public int visitorsServed, visitorsLeftUnhappy;

        public CastleSim(Game game)
        {
            g = game;
            foreach (var c in g.S.castleStaff) if (c.dispatched) SpawnStaff(c);
        }

        static Vector2 Entrance => new Vector2(Game.ElevatorX, 0);

        public static float Center(BuildingState b) => Game.SimX(b.x) + b.cw * 0.5f;

        // ── 이벤트 ─────────────────────────────────────────────
        public void OnStaffDispatchChanged(CastleStaffState c)
        {
            if (c.dispatched) { SpawnStaff(c); return; }
            var a = staff.Find(s => s.state == c);
            if (a == null) return;
            DropJob(a);
            staff.Remove(a);
        }

        // 담당 구역이 바뀌면 구역 밖 일을 내려놓는다
        public void OnStaffAreaChanged(CastleStaffState c)
        {
            var a = staff.Find(s => s.state == c);
            if (a != null && a.job != null && !Game.InArea(c, a.job.building)) DropJob(a);
        }

        static void DropJob(StaffAgent a)
        {
            if (a.job != null) a.job.staff = null;
            a.job = null;
            a.working = false;
        }

        void SpawnStaff(CastleStaffState c)
        {
            var def = GameData.StaffById(c.staffId);
            staff.Add(new StaffAgent { state = c, def = def, pos = Entrance, speedMul = def.moveSpeed });
        }

        public void OnBuildingRemoved(BuildingState b)
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                if (j.building != b) continue;
                if (j.staff != null) { j.staff.job = null; j.staff.working = false; }
                if (j.visitor != null) { j.visitor.job = null; NextStop(j.visitor); }
                jobs.RemoveAt(i);
            }
            foreach (var v in visitors)
                if (v.currentStop == b.uid && v.state != VisitorState.Leaving) NextStop(v);
        }

        public bool HasWaitingCustomer(BuildingState b) => jobs.Exists(j => j.building == b && j.type == JobType.Serve);
        public int WaitingCustomers(BuildingState b) => jobs.FindAll(j => j.building == b && j.type == JobType.Serve).Count;

        // ── 틱 ─────────────────────────────────────────────────
        public void Tick(float dt)
        {
            TickSpawn(dt);
            CreateMaintenanceJobs();

            for (int i = visitors.Count - 1; i >= 0; i--) TickVisitor(visitors[i], dt);
            foreach (var s in staff) TickStaff(s, dt);
        }

        // ── 손님 (롤러코스터 타이쿤 방식) ───────────────────────
        // 모든 확률·변수는 GuestSettings 에셋(메뉴 Mawang/Guest Settings)에서 고친다.
        static GuestSettings GS => GuestSettings.I;

        // 최대 손님 = 기본 + 방문 가능한 칸 수 × 칸당 + 손님 유치 연구 (층 수와 무관, 상한 있음)
        public int VisitorMax => Mathf.Min(GS.maxCap, Mathf.RoundToInt(GS.maxBase + GS.maxPerVisitableCell * VisitableCells) + g.Mods.visitorMaxAdd);

        // 방문 가능한 칸 수 (전시 우리는 묶어 지은 칸 전체)
        public int VisitableCells
        {
            get
            {
                int cells = 0;
                foreach (var b in g.S.buildings) if (Visitable(b)) cells += b.Cells;
                return cells;
            }
        }

        // 방문할 수 있는 건물: 재고가 있는 가게 · 괴물이 있는 전시 우리 · 출전 괴물이 있는 투기장
        bool Visitable(BuildingState b)
        {
            switch (b.type)
            {
                case BuildingType.Restaurant:
                case BuildingType.Souvenir: return BestItem(b, int.MaxValue) != null;
                case BuildingType.Tank: return b.contents.Count > 0;
                case BuildingType.Arena: return g.S.arena.Count > 0;
            }
            return false;
        }

        // 볼거리 비율 = 방문 가능한 건물이 차지한 칸 수 / 열린 칸 수
        public float AttractionRatio
        {
            get
            {
                int open = g.TotalFloorsOpen * GameData.WingWidth;
                return open <= 0 ? 0f : (float)VisitableCells / open;
            }
        }

        // 붐빔 물결: 0 ~ crowdWave 사이를 주기적으로 부드럽게 오르내린다 (기계적으로 느껴지지 않게)
        public float CrowdWave => GS.crowdWave * Mathf.PerlinNoise(Time.time / Mathf.Max(1f, GS.crowdWavePeriod), 0.37f);

        // 목표 손님 수: 볼거리가 없으면 0, 있으면 최대 손님 × (볼거리 비율 × 배율, 하한·상한 사이) × (1 + 붐빔 물결)
        public int VisitorTarget
        {
            get
            {
                float ratio = AttractionRatio;
                if (ratio <= 0f) return 0;
                return Mathf.RoundToInt(VisitorMax * Mathf.Clamp(ratio * GS.appealMultiplier, GS.minCrowd, GS.maxCrowd) * (1f + CrowdWave));
            }
        }

        // 목표까지 남은 자리에 비례해 들어온다. 손님 유치 연구는 채우는 속도를 올린다.
        void TickSpawn(float dt)
        {
            int room = VisitorTarget - visitors.Count;
            if (room <= 0) { spawnTimer = 0; return; }

            float perSec = room / Mathf.Max(0.1f, GS.fillSeconds) * g.Mods.visitorRateMul;
            float interval = Mathf.Max(GS.minSpawnInterval, 1f / perSec);

            spawnTimer += dt;
            if (spawnTimer < interval) return;
            spawnTimer = 0;
            SpawnVisitor();
        }

        // 취향: 식욕 a 가 높을수록 식당, 낮을수록 기념품점
        static float Taste(float a) => 1f - GS.tasteSpread + a * 2f * GS.tasteSpread;

        float VenueWeight(Visitor v, BuildingState b)
        {
            if (!Visitable(b)) return 0f;
            switch (b.type)
            {
                case BuildingType.Restaurant: return GS.weightRestaurant * Taste(v.appetite);
                case BuildingType.Souvenir: return GS.weightSouvenir * Taste(1f - v.appetite);
                case BuildingType.Tank: return Mathf.Min(GS.weightTankMax, GS.weightTankBase + g.TankRatePerMin(b) * GS.weightTankPerAdmission);
                case BuildingType.Arena: return GS.weightArena;
            }
            return 0f;
        }

        // 아직 들르지 않은 방문지 하나를 가중치로 고른다 (없으면 null)
        BuildingState PickVenue(Visitor v)
        {
            BuildingState pick = null;
            float total = 0;
            foreach (var b in g.S.buildings)
            {
                if (v.seen.Contains(b.uid)) continue;
                float w = VenueWeight(v, b);
                if (w <= 0) continue;
                total += w;
                if (Random.value * total < w) pick = b; // 가중 저수지 표본
            }
            return pick;
        }

        void SpawnVisitor()
        {
            float rich = Mathf.Pow(GS.walletPerFloor, Mathf.Max(0, g.TotalFloorsOpen - 2));
            var v = new Visitor
            {
                pos = Entrance,
                wallet = Mathf.RoundToInt(Random.Range(GS.walletMin, GS.walletMax) * g.Mods.walletMul * rich),
                energy = Random.Range(GS.energyMin, GS.energyMax),
                happy = Random.Range(GS.happyMin, GS.happyMax),
                appetite = Random.value,
                speedMul = Random.Range(0.7f, 1.0f),
                color = Color.HSVToRGB(Random.value, 0.15f, 1f),
                look = Random.Range(0, 4),
            };
            visitors.Add(v);
            NextStop(v);
        }

        // 더 둘러볼지: 지쳤거나 기분이 나쁘거나 많이 돌았으면 안 간다.
        // 확률 = (기본 + 돈 보너스 × 남은 돈 / (가장 비싼 상품 × 배수)) × 감쇠^들른 곳 수
        bool WantsMore(Visitor v)
        {
            if (v.energy <= 0f || v.happy < GS.leaveBelowHappy || v.visited >= GS.maxStops) return false;
            float refPrice = 100f;
            foreach (var b in g.S.buildings)
                if (b.type == BuildingType.Restaurant || b.type == BuildingType.Souvenir)
                {
                    var top = BestItem(b, int.MaxValue);
                    if (top != null) refPrice = Mathf.Max(refPrice, top.sellPrice);
                }
            float money = Mathf.Clamp01(v.wallet / (refPrice * Mathf.Max(0.1f, GS.moneyRefMultiple)));
            float p = (GS.continueBase + GS.continueMoneyBonus * money) * Mathf.Pow(GS.continueDecay, v.visited);
            return Random.value < p;
        }

        // 한 곳을 마치면(들어오자마자 포함) 다음 방문지를 고르거나 집에 간다
        void NextStop(Visitor v)
        {
            v.job = null;
            if (v.currentStop != 0) { v.visited++; v.energy -= GS.energyPerStop; }
            v.currentStop = 0;
            v.happy = Mathf.Clamp(v.happy, 0f, 100f);

            var next = v.visited == 0 || WantsMore(v) ? PickVenue(v) : null;
            if (next == null)
            {
                if (v.happy < GS.leaveBelowHappy) visitorsLeftUnhappy++;
                v.state = VisitorState.Leaving;
                v.GoTo(Entrance);
                return;
            }
            v.seen.Add(next.uid);
            v.currentStop = next.uid;
            v.state = VisitorState.Walking;
            int row = next.floor + Random.Range(0, next.ch);
            // 투기장: 용사는 왼쪽에 서서 오른쪽의 출전 괴물과 맞선다 (CastleView.ArenaFighter)
            float x = next.type == BuildingType.Arena ? GameData.ArenaHeroX + Random.Range(-0.04f, 0.04f) : Random.Range(0.3f, next.cw - 0.3f);
            v.GoTo(new Vector2(Game.SimX(next.x) + x, row));
        }

        void TickVisitor(Visitor v, float dt)
        {
            v.Step(dt);
            switch (v.state)
            {
                case VisitorState.Leaving:
                    if (!v.Moving) visitors.Remove(v);
                    break;

                case VisitorState.Walking:
                    if (v.Moving) break;
                    ArriveAt(v, g.BuildingByUid(v.currentStop));
                    break;

                case VisitorState.Watching:
                    v.timer -= dt;
                    if (v.timer <= 0) { v.happy += GS.happyWatch; TipTank(v, g.BuildingByUid(v.currentStop)); NextStop(v); }
                    break;

                case VisitorState.Sparring:
                    v.timer -= dt;
                    if (v.timer <= 0) { v.happy += GS.happySpar; Spar(v, g.BuildingByUid(v.currentStop)); NextStop(v); }
                    break;

                case VisitorState.WaitingService:
                    if (v.job != null && v.job.staff != null) break; // 직원이 오는 중이면 기다린다
                    v.timer -= dt;
                    if (v.timer <= 0)
                    {
                        jobs.Remove(v.job);
                        v.happy -= GS.happyWaitTimeout; // 기다리다 지쳐 떠난다
                        NextStop(v);
                    }
                    break;
            }
        }

        // 도착: 들어가서 즐기거나, 여러 이유로 그냥 지나친다
        void ArriveAt(Visitor v, BuildingState b)
        {
            if (b == null) { NextStop(v); return; }
            switch (b.type)
            {
                case BuildingType.Tank:
                    if (b.contents.Count == 0) { NextStop(v); return; }
                    v.state = VisitorState.Watching;
                    v.timer = Random.Range(GS.watchMin, GS.watchMax);
                    return;

                case BuildingType.Arena:
                    if (g.ArenaChampion() == null) { v.happy -= GS.happyNoFighter; NextStop(v); return; }
                    // 최대 보유 골드면 대련하지 않는다. 대련 신청 확률에 못 들면 구경만 하고 지나친다.
                    if (g.GoldFull || Random.value >= GS.sparChance) { NextStop(v); return; }
                    v.state = VisitorState.Sparring;
                    v.timer = GS.boutTime;
                    return;
            }

            // 식당 · 기념품점
            if (g.GoldFull) { NextStop(v); return; }                                                      // 최대 보유 골드: 팔지 않는다
            if (WaitingCustomers(b) >= GS.shopQueueLimit) { v.happy -= GS.happyQueueFull; NextStop(v); return; } // 줄이 꽉 참
            v.wantItem = BestItem(b, v.wallet);                                                            // 원작: 살 수 있는 가장 비싼 상품
            if (v.wantItem == null) { v.happy -= GS.happyNoMoney; NextStop(v); return; }                   // 돈 부족 · 재고 없음
            float notInterested = Mathf.Max(GS.notInterestedMin, GS.notInterestedBase - GS.notInterestedPerLevel * b.level);
            if (Random.value < notInterested) { NextStop(v); return; }                                     // 관심 없음
            v.state = VisitorState.WaitingService;
            v.timer = v.patience = GS.patience + g.Mods.patienceAdd;
            v.job = new Job { type = JobType.Serve, building = b, visitor = v };
            jobs.Add(v.job);
        }

        ShopItemDef BestItem(BuildingState shop, int wallet)
        {
            ShopItemDef best = null;
            foreach (var item in GameData.ShopItems)
            {
                if (item.shop != shop.type || item.tier > shop.level || item.sellPrice > wallet) continue;
                if (CountList.Get(g.S.goods, item.id) <= 0) continue;
                if (best == null || item.sellPrice > best.sellPrice) best = item;
            }
            return best;
        }

        void CreateMaintenanceJobs()
        {
            foreach (var b in g.S.buildings)
            {
                JobType? t = null;
                if (b.type == BuildingType.Lab && b.labRp > 0 && !g.GemFull) t = JobType.CollectLab;
                else if (b.type == BuildingType.Tank && b.accumulated >= GameData.TankStaffCollectThreshold && !g.GoldFull) t = JobType.CollectTank;
                if (t == null || jobs.Exists(j => j.building == b && j.type == t)) continue;
                jobs.Add(new Job { type = t.Value, building = b });
            }
        }

        static bool IsSpecialty(StaffAgent s, BuildingState b) => b.type == s.def.specialty;

        void TickStaff(StaffAgent s, float dt)
        {
            s.Step(dt);

            if (s.job == null)
            {
                s.job = PickJob(s);
                if (s.job == null) return;
                s.job.staff = s;
                s.working = false;
                var b = s.job.building;
                // 특화 건물로 갈 때는 빠르게, 다른 일은 조금 느리게
                s.speedMul = s.def.moveSpeed * (IsSpecialty(s, b) ? GameData.SpecialtySpeed : GameData.OffSpecialtySpeed);
                float sx = Game.SimX(b.x);
                float tx = s.job.visitor != null ? s.job.visitor.pos.x : Center(b);
                float ty = s.job.visitor != null ? s.job.visitor.pos.y : b.floor;
                s.GoTo(new Vector2(Mathf.Clamp(tx, sx + 0.2f, sx + b.cw - 0.2f), ty));
                return;
            }
            if (s.Moving) return;

            if (!s.working) { s.working = true; s.workTimer = GameData.ServiceTime * (IsSpecialty(s, s.job.building) ? GameData.SpecialtyWork : 1f); }
            s.workTimer -= dt;
            if (s.workTimer > 0) return;

            Complete(s, s.job);
            jobs.Remove(s.job);
            s.job = null;
            s.working = false;
        }

        Job PickJob(StaffAgent s)
        {
            Job best = null;
            float bestCost = float.MaxValue;
            foreach (var j in jobs)
            {
                if (j.staff != null || !Game.InArea(s.state, j.building)) continue;
                float cost = Mathf.Abs(j.building.floor - s.pos.y) * 3f + Mathf.Abs(Center(j.building) - s.pos.x);
                if (j.type != JobType.Serve) cost += 20f; // 손님 응대 우선
                if (IsSpecialty(s, j.building)) cost -= 12f; // 특화 일을 먼저
                if (cost < bestCost) { bestCost = cost; best = j; }
            }
            return best;
        }

        // 판매 1건: 가격 = 판매가 × (1 + 직원 보너스 + 상술)
        // 최대 보유 골드에 닿았으면 팔지 않는다(재고도 줄지 않는다)
        int Sell(BuildingState shop, Visitor v, float staffBonus)
        {
            if (g.GoldFull) return 0;
            var item = BestItem(shop, v.wallet);
            if (item == null) return 0;
            int price = Mathf.RoundToInt(item.sellPrice * (1f + staffBonus + g.Mods.salesAdd));
            CountList.Add(g.S.goods, item.id, -1);
            price = g.EarnAutoGold(price);
            v.wallet -= item.sellPrice;
            visitorsServed++;
            v.happy += GS.happyBuy;
            g.S.stats.sales++;
            return price;
        }

        // 전시 우리 관람을 마친 용사: 확률적으로 분당 관람료의 일부를 더 낸다 (지갑 안에서, 수치는 GuestSettings)
        void TipTank(Visitor v, BuildingState b)
        {
            if (b == null || b.type != BuildingType.Tank || g.GoldFull) return;
            if (Random.value >= GS.tipChance) return;
            int amt = Mathf.Min(v.wallet, Mathf.RoundToInt(g.TankRatePerMin(b) * Random.Range(GS.tipMin, GS.tipMax)));
            amt = g.EarnAutoGold(amt);
            if (amt <= 0) return;
            v.wallet -= amt;
            FloatingText.Emit(new Vector2(Center(b), b.floor + b.ch - 1), $"+{amt:N0}");
            Sound.Play("sale");
        }

        static readonly Color[] BoutColors =
        {
            new Color(1f, 0.35f, 0.35f), new Color(0.75f, 0.72f, 0.8f), new Color(0.85f, 0.85f, 0.95f), UIKit.Gold, new Color(1f, 0.6f, 0.2f),
        };

        // 투기장 대련: 결과와 받은 골드를 바로 띄운다. 용사는 받은 만큼 지갑에서 낸다.
        void Spar(Visitor v, BuildingState b)
        {
            if (b == null || b.type != BuildingType.Arena) return;
            var bout = g.ArenaBout(b);
            if (bout == null) return;
            var (r, gold, m) = bout.Value;
            v.wallet = Mathf.Max(0, v.wallet - gold);
            var at = new Vector2(Center(b), b.floor + b.ch - 1);
            string text = r == BoutResult.GreatFail ? L.T($"{GameData.BoutName(r)}! {m.Name} 사망", $"{GameData.BoutName(r)}! {m.Name} fell")
                        : gold > 0 ? $"{GameData.BoutName(r)} +{gold:N0}" : GameData.BoutName(r);
            FloatingText.Emit(at, text, BoutColors[(int)r]);
            Sound.Play(r >= BoutResult.Win ? "coin" : r == BoutResult.GreatFail ? "error" : "click");
        }

        // 플레이어가 가게를 탭: 기다리는 손님 모두에게 바로 판다(직원이 가는 중인 손님 포함)
        public int ServeAll(BuildingState b)
        {
            int total = 0;
            if (g.GoldFull) return 0;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                if (j.building != b || j.type != JobType.Serve) continue;
                total += Sell(b, j.visitor, 0f);
                if (j.staff != null) { j.staff.job = null; j.staff.working = false; }
                jobs.RemoveAt(i);
                NextStop(j.visitor);
            }
            if (total > 0) { Sound.Play("coin"); FloatingText.Emit(new Vector2(Center(b), b.floor), $"+{total:N0}"); }
            return total;
        }

        void Complete(StaffAgent s, Job j)
        {
            var at = new Vector2(Center(j.building), j.building.floor);
            switch (j.type)
            {
                case JobType.Serve:
                {
                    float bonus = j.building.type == BuildingType.Restaurant ? s.def.foodBonus : s.def.souvenirBonus;
                    int price = Sell(j.building, j.visitor, bonus);
                    if (price > 0) { FloatingText.Emit(at, $"+{price:N0}"); Sound.Play("sale"); }
                    NextStop(j.visitor);
                    break;
                }
                case JobType.CollectLab:
                {
                    int rp = g.CollectLab(j.building);
                    if (rp > 0) FloatingText.Emit(at, L.T($"+{rp} 보석", $"+{rp} Gems"));
                    break;
                }
                case JobType.CollectTank:
                {
                    int amt = g.CollectTank(j.building, s.def.admissionBonus);
                    if (amt > 0) FloatingText.Emit(at, $"+{amt:N0}");
                    break;
                }
            }
        }
    }
}

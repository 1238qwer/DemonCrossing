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

    public enum VisitorState { Walking, Watching, WaitingService, Leaving }

    public class Visitor : Agent
    {
        public int wallet;
        public readonly Queue<int> stops = new Queue<int>(); // 건물 uid
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

        public int VisitorMax => GameData.VisitorMaxBase + GameData.VisitorMaxPerFloor * g.TotalFloorsOpen + g.Mods.visitorMaxAdd;

        void TickSpawn(float dt)
        {
            bool anyVenue = g.S.buildings.Exists(b => b.type == BuildingType.Tank || b.type == BuildingType.Restaurant || b.type == BuildingType.Souvenir);
            if (!anyVenue || visitors.Count >= VisitorMax) return;

            float attraction = 0;
            foreach (var b in g.S.buildings) if (b.type == BuildingType.Tank) attraction += g.TankRatePerMin(b);
            float interval = Mathf.Max(GameData.VisitorMinInterval, GameData.VisitorBaseInterval / (1f + attraction / 150f) / g.Mods.visitorRateMul);

            spawnTimer += dt;
            if (spawnTimer < interval) return;
            spawnTimer = 0;
            SpawnVisitor();
        }

        void SpawnVisitor()
        {
            float rich = Mathf.Pow(GameData.VisitorWalletPerFloor, Mathf.Max(0, g.TotalFloorsOpen - 2));
            var v = new Visitor
            {
                pos = Entrance,
                wallet = Mathf.RoundToInt(Random.Range(GameData.VisitorWalletMin, GameData.VisitorWalletMax) * g.Mods.walletMul * rich),
                speedMul = Random.Range(0.7f, 1.0f),
                color = Color.HSVToRGB(Random.value, 0.15f, 1f),
                look = Random.Range(0, 4),
            };

            // 방문지 1~3곳을 가중치로 고른다. 전시 우리는 관람 가치가 높을수록 인기.
            var cands = new List<BuildingState>();
            var weights = new List<float>();
            foreach (var b in g.S.buildings)
            {
                float w = b.type switch
                {
                    BuildingType.Tank => 1f + g.TankRatePerMin(b) / 20f,
                    BuildingType.Restaurant => 3f,
                    BuildingType.Souvenir => 3f,
                    _ => 0f,
                };
                if (w > 0) { cands.Add(b); weights.Add(w); }
            }
            int n = Mathf.Min(cands.Count, Random.Range(1, 4));
            for (int k = 0; k < n; k++)
            {
                float total = 0;
                foreach (var w in weights) total += w;
                float r = Random.value * total;
                int pick = 0;
                for (; pick < weights.Count - 1; pick++) { r -= weights[pick]; if (r <= 0) break; }
                v.stops.Enqueue(cands[pick].uid);
                cands.RemoveAt(pick);
                weights.RemoveAt(pick);
            }

            visitors.Add(v);
            NextStop(v);
        }

        void NextStop(Visitor v)
        {
            v.job = null;
            while (v.stops.Count > 0)
            {
                var b = g.BuildingByUid(v.stops.Dequeue());
                if (b == null) continue;
                v.currentStop = b.uid;
                v.state = VisitorState.Walking;
                int row = b.floor + Random.Range(0, b.ch);
                v.GoTo(new Vector2(Game.SimX(b.x) + Random.Range(0.3f, b.cw - 0.3f), row));
                return;
            }
            v.currentStop = 0;
            v.state = VisitorState.Leaving;
            v.GoTo(Entrance);
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
                    if (v.timer <= 0) NextStop(v);
                    break;

                case VisitorState.WaitingService:
                    if (v.job != null && v.job.staff != null) break; // 직원이 오는 중이면 기다린다
                    v.timer -= dt;
                    if (v.timer <= 0)
                    {
                        jobs.Remove(v.job);
                        visitorsLeftUnhappy++;
                        NextStop(v);
                    }
                    break;
            }
        }

        void ArriveAt(Visitor v, BuildingState b)
        {
            if (b == null) { NextStop(v); return; }
            if (b.type == BuildingType.Tank)
            {
                v.state = VisitorState.Watching;
                v.timer = Random.Range(2f, 4f);
                return;
            }
            // 원작: 비싼 상품부터 팔린다. 재고가 없으면 손님이 떠난다.
            v.wantItem = BestItem(b, v.wallet);
            if (v.wantItem == null) { visitorsLeftUnhappy++; NextStop(v); return; }
            v.state = VisitorState.WaitingService;
            v.timer = v.patience = GameData.VisitorPatience + g.Mods.patienceAdd;
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
                if (b.type == BuildingType.Lab && b.labRp > 0) t = JobType.CollectLab;
                else if (b.type == BuildingType.Tank && b.accumulated >= GameData.TankStaffCollectThreshold) t = JobType.CollectTank;
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
        int Sell(BuildingState shop, Visitor v, float staffBonus)
        {
            var item = BestItem(shop, v.wallet);
            if (item == null) return 0;
            int price = Mathf.RoundToInt(item.sellPrice * (1f + staffBonus + g.Mods.salesAdd));
            CountList.Add(g.S.goods, item.id, -1);
            g.S.gold += price;
            v.wallet -= item.sellPrice;
            visitorsServed++;
            return price;
        }

        // 플레이어가 가게를 탭: 기다리는 손님 모두에게 바로 판다(직원이 가는 중인 손님 포함)
        public int ServeAll(BuildingState b)
        {
            int total = 0;
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
                    int rp = j.building.labRp;
                    g.CollectLab(j.building);
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

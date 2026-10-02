using System;
using System.IO;
using UnityEngine;

namespace Mawang
{
    // 게임 루트. 상태(SaveData)를 소유하고 각 시스템(partial)을 틱한다.
    public partial class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public SaveData S { get; private set; }
        public Modifiers Mods { get; private set; } = new Modifiers();
        public CastleSim Sim { get; private set; }

        public event Action Changed;                  // 구조적 변화 → UI 재구성
        public event Action<string, string> Notified; // 알림 (문구, 토스트 아이콘 — null 이면 로그만)

        const float AutoSaveInterval = 30f;
        float autoSaveTimer;
        bool dirty;

        static string SavePath => Path.Combine(Application.persistentDataPath, "mawang_save.json");

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // 지켜보는 게임이라 화면이 꺼지지 않게
            Load();
            CharacterSettings.I.ApplyNames(); // 인스펙터에서 고친 괴물·직원 이름
            Sim = new CastleSim(this);
            Sound.Init();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickFishing(dt);
            TickBuildings(dt);
            TickTrash(dt);
            TickDaily();
            Sim.Tick(dt);
            TickAchievements(dt);
            TickGoals(dt);

            autoSaveTimer += dt;
            if (autoSaveTimer >= AutoSaveInterval) { autoSaveTimer = 0; Save(); }

            if (dirty) { dirty = false; Changed?.Invoke(); }
        }

        void OnApplicationQuit() => Save();

        // 폰에서는 종료 이벤트 없이 백그라운드에서 꺼질 수 있다 → 내려갈 때 저장
        void OnApplicationPause(bool paused) { if (paused) Save(); }

        public void MarkDirty() => dirty = true;
        public void Notify(string msg, string icon = null) => Notified?.Invoke(msg, icon);

        // 실패 알림: 토스트 + 효과음
        public void Fail(string msg)
        {
            Sound.Play("error");
            Notify(msg, "ic_close");
        }

        // ── 저장 ───────────────────────────────────────────────
        public void Save()
        {
            if (DevCapture.Active) return; // 개발용 자동 캡처 실행은 세이브를 건드리지 않는다
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(S)); }
            catch (Exception e) { Debug.LogWarning($"저장 실패: {e.Message}"); }
        }

        // 에디터 전용: 스토어 스크린샷 촬영 등에서 디스크 세이브 대신 쓸 상태 (저장하지 않는다)
        public static SaveData PendingState;

        void Load()
        {
            S = null;
            if (PendingState != null) { S = PendingState; Mods.Recalculate(S); return; }
            try
            {
                if (File.Exists(SavePath)) S = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e) { Debug.LogWarning($"불러오기 실패, 새 게임 시작: {e.Message}"); }

            if (S != null && S.version == 2) Migrate2(S);
            if (S != null && S.version != SaveData.CurrentVersion)
            {
                Debug.Log("이전 버전 세이브라 새 게임으로 시작합니다.");
                S = null;
            }
            if (S == null) S = NewGame();
            S.stats ??= new Stats();
            S.arena ??= new System.Collections.Generic.List<CountEntry>();
            S.achievements ??= new System.Collections.Generic.List<string>();
            foreach (var c in S.castleStaff) if (c.uid == 0) c.uid = S.nextUid++;
            Mods.Recalculate(S);
        }

        // v2 → v3: 4칸 × 4층(엘리베이터 왼쪽) → 왼쪽 날개. 보석은 10배 귀해졌으므로 1/10.
        static void Migrate2(SaveData s)
        {
            s.version = SaveData.CurrentVersion;
            s.floorsLeft = Mathf.Clamp(s.unlockedFloors, GameData.StartFloorsLeft, GameData.MaxFloors);
            s.floorsRight = 0;
            s.rp /= 10;
            s.research.RemoveAll(r => r == "floor_3" || r == "floor_4");
            foreach (var b in s.buildings) { b.cw = Mathf.Max(1, b.cw); b.ch = Mathf.Max(1, b.ch); b.labRp = 0; b.labTimer = 0; }
            s.tutorialDone = true;
        }

        public void ResetGame()
        {
            S = NewGame();
            Mods.Recalculate(S);
            Sim = new CastleSim(this);
            Save();
            MarkDirty();
            Notify(L.T("새 게임을 시작했습니다.", "Started a new game."));
        }

        static SaveData NewGame()
        {
            var s = new SaveData
            {
                gold = GameData.StartGold,
                material = GameData.StartMaterial,
                rp = GameData.StartGems,
                floorsLeft = GameData.StartFloorsLeft,
                floorsRight = GameData.StartFloorsRight,
                lastDailyTicks = DateTime.Now.Ticks,
            };
            CountList.Add(s.baits, GameData.Baits[0].id, 20);
            foreach (var id in GameData.StartStaff) AddStaff(s, GameData.StaffById(id));
            return s;
        }

        static void AddStaff(SaveData s, StaffDef def)
        {
            if (def.role == StaffRole.Hunter)
                s.hunters.Add(new HunterState { staffId = def.id, durabilityLeft = def.durability });
            else
                s.castleStaff.Add(new CastleStaffState { staffId = def.id, uid = s.nextUid++ });
        }

        // ── 재화 ───────────────────────────────────────────────
        public bool CanAfford(int gold, int mat, int gems = 0) => S.gold >= gold && S.material >= mat && S.rp >= gems;

        public bool Spend(int gold, int mat, int gems = 0)
        {
            if (!CanAfford(gold, mat, gems))
            {
                Fail(S.rp < gems ? L.T("보석이 부족합니다.", "Not enough Gems.") : L.T("재화가 부족합니다.", "Not enough resources."));
                return false;
            }
            S.gold -= gold;
            S.material -= mat;
            S.rp -= gems;
            return true;
        }

        // 행동 수입(괴물 판매·쓰레기 등): 최대 보유량을 넘어도 받는다
        public void Earn(Currency c, int amount)
        {
            if (c == Currency.Gold) EarnGold(amount); else S.material += amount;
        }

        public void EarnGold(int amount)
        {
            if (amount <= 0) return;
            S.gold += amount;
            S.stats.goldEarned += amount;
        }

        // ── 최대 보유량: 자동 수입(가게 판매·전시 우리·투기장·연구소)만 막는다 ──
        // 테스트용: Game 인스펙터의 '자원 상한 끄기' 버튼 (에디터 전용, 저장되지 않는다)
#if UNITY_EDITOR
        public static bool DevNoCaps;
#else
        public const bool DevNoCaps = false;
#endif
        public int GoldCap => DevNoCaps ? int.MaxValue : Mods.goldCap;
        public int MatCap => DevNoCaps ? int.MaxValue : Mods.goldCap;
        public int GemCap => DevNoCaps ? int.MaxValue : Mods.gemCap;
        public bool GoldFull => S.gold >= GoldCap;
        public bool GemFull => S.rp >= GemCap;

        // 자동 골드 수입: 최대 보유량까지만 받고, 실제로 받은 양을 돌려준다
        public int EarnAutoGold(int amount)
        {
            amount = Mathf.Min(amount, Mathf.Max(0, GoldCap - S.gold));
            EarnGold(amount);
            return amount;
        }

        // 원작: 골드와 건설 자재는 1:1 교환
        public void Exchange(Currency from, int amount)
        {
            if (from == Currency.Gold && S.gold >= amount) { S.gold -= amount; S.material += amount; }
            else if (from == Currency.Material && S.material >= amount) { S.material -= amount; S.gold += amount; }
            else { Fail(L.T("교환할 재화가 부족합니다.", "Not enough to exchange.")); return; }
            Sound.Play("coin");
            MarkDirty();
        }

        // ── 휴게실 일일 지원금 ─────────────────────────────────
        float dailyCheckTimer;

        void TickDaily()
        {
            dailyCheckTimer -= Time.deltaTime;
            if (dailyCheckTimer > 0) return;
            dailyCheckTimer = 10f;

            var now = DateTime.Now;
            var last = new DateTime(S.lastDailyTicks);
            var boundary = now.Date.AddHours(GameData.RestDailyHour);
            if (now < boundary) boundary = boundary.AddDays(-1);
            if (last >= boundary) return;

            S.lastDailyTicks = now.Ticks;
            int rests = 0;
            foreach (var b in S.buildings) if (b.type == BuildingType.Rest) rests++;
            if (rests == 0) return;
            int gold = rests * GameData.RestDailyGold(TotalFloorsOpen);
            EarnGold(gold);
            Sound.Play("coin");
            Notify(L.T($"[휴게실] 일일 지원금 {gold:N0}골드를 받았습니다.", $"[Lounge] Received a daily bonus of {gold:N0} Gold."), "ic_gold");
            MarkDirty();
        }

        // ── 업적: 1초마다 확인, 달성하면 골드 자동 지급 ─────────
        float achievementTimer;

        public bool AchievementDone(AchievementDef a) => S.achievements.Contains(a.id);

        void TickAchievements(float dt)
        {
            S.stats.peakVisitors = Mathf.Max(S.stats.peakVisitors, Sim.visitors.Count);
            achievementTimer -= dt;
            if (achievementTimer > 0) return;
            achievementTimer = 1f;
            foreach (var a in GameData.Achievements)
            {
                if (AchievementDone(a) || a.progress(this) < a.target) continue;
                S.achievements.Add(a.id);
                EarnGold(a.gold);
                Sound.Play("upgrade");
                Notify(L.T($"[업적 달성] {a.Name} (+{a.gold:N0}골드)", $"[Achievement] {a.Name} (+{a.gold:N0} Gold)"), "ic_trophy");
                MarkDirty();
            }
        }
    }
}

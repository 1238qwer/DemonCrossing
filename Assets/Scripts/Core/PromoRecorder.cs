#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mawang
{
    // 에디터 전용: 홍보 영상용 장면 녹화.
    // 쇼케이스 상태(StoreShots, 저장 안 함)로 장면마다 30fps JPG 프레임을 찍는다 → ffmpeg 로 편집.
    // 플레이 중에 PromoRecorder.Run(dir, lang) → dir/NN_이름/f_0000.jpg (게임 화면 크기, 1920×1080)
    public class PromoRecorder : MonoBehaviour
    {
        public const int Fps = 30;
        public static bool Done { get; private set; }
        public static string Log = "";

        public static void Run(string dir, int lang, string only = null)
        {
            Done = false; Log = "";
            var go = new GameObject("PromoRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<PromoRecorder>();
            r.dir = dir; r.lang = lang; r.only = only;
        }

        string dir, only;
        int lang;

        class Seg
        {
            public string name;
            public float sec, warm = 0.8f;   // 녹화 길이, 녹화 전 준비 시간(창 열림·확대가 자리 잡게)
            public float warmScale = 1f;      // 준비 시간 동안의 게임 속도
            public Action setup, end;
            public Action<float> tick;        // 녹화 중 매 프레임 (0~1)
        }

        IEnumerator Start()
        {
            int prevLang = L.Lang;
            DevCapture.Active = true;               // 저장 끔
            DevCapture.ForceMobileInEditor = false; // PC 배치
            L.Lang = lang;
            Game.PendingState = State();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null; yield return null;
            // 손님이 모이도록 잠깐 빨리 돌린다
            Time.timeScale = 6f;
            yield return new WaitForSecondsRealtime(10f);
            Time.timeScale = 1f;

            Directory.CreateDirectory(dir);
            Time.captureFramerate = Fps;
            int n = 0;
            foreach (var s in Plan())
            {
                n++;
                if (only != null && only != s.name) continue;
                string sd = Path.Combine(dir, $"{n:00}_{s.name}");
                if (Directory.Exists(sd)) Directory.Delete(sd, true);
                Directory.CreateDirectory(sd);
                ResetView();
                ClearToasts();
                camTarget = null;
                try { s.setup?.Invoke(); } catch (Exception e) { Log += $"{s.name} setup: {e.Message}\n"; }
                Time.timeScale = s.warmScale;
                for (int i = 0; i < Mathf.RoundToInt(s.warm * Fps); i++) { HoldCamera(); yield return null; }
                Time.timeScale = 1f;
                ClearToasts();
                int frames = Mathf.RoundToInt(s.sec * Fps);
                for (int i = 0; i < frames; i++)
                {
                    try { s.tick?.Invoke(i / (float)(frames - 1)); } catch (Exception e) { Log += $"{s.name} tick: {e.Message}\n"; s.tick = null; }
                    HoldCamera();
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(sd, $"f_{i:0000}.jpg"), tex.EncodeToJPG(92));
                    Destroy(tex);
                    yield return null;
                }
                try { s.end?.Invoke(); } catch (Exception e) { Log += $"{s.name} end: {e.Message}\n"; }
                Log += $"{s.name}: {frames}\n";
            }
            Time.captureFramerate = 0;
            ResetView();
            L.Lang = prevLang;
            Game.PendingState = null;
            Log += $"done {Screen.width}x{Screen.height}\n";
            Done = true;
        }

        // ── 장면 목록 ─────────────────────────────────────────
        List<Seg> Plan()
        {
            BuildingState upB = null;
            int upAt = 0;
            bool expanded = false, built = false;
            return new List<Seg>
            {
                // 성 전체 → 확대
                new Seg { name = "intro", sec = 4f, setup = () => View(0, 41f, 5f),
                    tick = t => { if (t > 0.3f && Zoom != 2) { Zoom = 2; Center(30f, 1.2f); } } },
                // 1층 가게 줄: 손님이 사 가며 골드가 튄다
                new Seg { name = "shops", sec = 5f, setup = () => View(4, 6f, 0f),
                    tick = t => Center(Mathf.Lerp(6f, 44f, Ease(t)), 0f) },
                // 포획
                new Seg { name = "hunt_lava", sec = 3.5f, warm = 1.5f, setup = () => UI.ShowHunt("lava") },
                new Seg { name = "hunt_crystal", sec = 3f, warm = 1.5f, setup = () => UI.ShowHunt("crystal") },
                // 전시 우리
                new Seg { name = "tanks", sec = 4f, setup = () => View(4, 10f, 1.5f),
                    tick = t => Center(Mathf.Lerp(10f, 22f, Ease(t)), 1.5f) },
                // 투기장
                // 투기장: 잠깐 손님을 투기장으로 몰아 대련 장면을 만든다 (설정 에셋은 끝나면 되돌린다)
                new Seg { name = "arena", sec = 4.5f, warm = 6f, warmScale = 3f,
                    setup = () => { arenaWeight = GuestSettings.I.weightArena; GuestSettings.I.weightArena = 25f; View(4, 74f, 5f); },
                    end = RestoreArenaWeight },
                // 업그레이드: 방 모습이 바뀐다
                new Seg { name = "upgrade", sec = 4f, warm = 1f,
                    setup = () => { upB = G.S.buildings.Find(b => b.type == BuildingType.Restaurant && b.floor == 6); upAt = 0; View(4, 15f, 6f); },
                    tick = t => { if (upB != null && t > 0.25f && upAt == 0) { G.Upgrade(upB); upAt = 1; } if (upB != null && t > 0.6f && upAt == 1) { G.Upgrade(upB); upAt = 2; } } },
                // 식당 메뉴
                new Seg { name = "diner", sec = 3f, setup = () => { UI.OpenBuilding(G.S.buildings.Find(b => b.type == BuildingType.Restaurant && b.level >= 7)); Scroll(0.45f); } },
                // 방 배치 (끌어서 짓기)
                new Seg { name = "build", sec = 4f, setup = () => { built = false; View(3, 20.5f, 6f); Castle.placing = BuildingType.Tank; Castle.previewRect = (new Vector2Int(2, 6), new Vector2Int(2, 6)); },
                    tick = t =>
                    {
                        if (built) return;
                        if (t < 0.6f) Castle.previewRect = (new Vector2Int(2, 6), new Vector2Int(t < 0.3f ? 2 : 3, 6));
                        else { Castle.previewRect = null; Castle.CancelPlacing(); G.TryBuild(BuildingType.Tank, 6, 2, 2, 1); built = true; }
                    } },
                // 연구
                new Seg { name = "research", sec = 3f, setup = () => { SetField("researchTab", 3); Menu(4); } },
                // 증축
                new Seg { name = "expand", sec = 4f, warm = 1f, setup = () => { expanded = false; View(3, 20.5f, 7f); },
                    tick = t => { if (t > 0.25f && !expanded) { G.Expand(0); expanded = true; } Center(20.5f, Mathf.Lerp(7f, 7.6f, Ease(t))); } },
                // 도감
                new Seg { name = "book", sec = 3.5f, setup = () => { Menu(5); Scroll(1f); }, tick = t => Scroll(1f - 0.6f * Ease(t)) },
                // 직원
                new Seg { name = "staff", sec = 3f, setup = () => Menu(1) },
                // 마무리: 성 전체로 빠진다
                new Seg { name = "outro", sec = 4f, warm = 0.5f, setup = () => View(2, 41f, 2f),
                    tick = t => { if (t > 0.15f && Zoom != 0) Zoom = 0; } },
            };
        }

        float arenaWeight = -1f;

        void RestoreArenaWeight()
        {
            if (arenaWeight >= 0f) GuestSettings.I.weightArena = arenaWeight;
            arenaWeight = -1f;
        }

        void OnDestroy() => RestoreArenaWeight();

        static float Ease(float t) => t * t * (3f - 2f * t);

        // ── 헬퍼 (StoreShots 와 같은 방식) ──────────────────────
        static Game G => Game.I;
        static GameUI UI => FindAnyObjectByType<GameUI>();
        static CastleView Castle => FindAnyObjectByType<CastleView>();
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static void Call(string method, params object[] args) => typeof(GameUI).GetMethod(method, Any).Invoke(UI, args);
        static void SetField(string field, object v) => typeof(GameUI).GetField(field, Any).SetValue(UI, v);
        static void Menu(int i) => Call("OpenMenu", i);

        static void Scroll(float v)
        {
            var sr = (UnityEngine.UI.ScrollRect)typeof(GameUI).GetField("modalScroll", Any).GetValue(UI);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
            sr.verticalNormalizedPosition = v;
        }

        static int Zoom
        {
            get => (int)typeof(CastleView).GetField("zoomStep", Any).GetValue(Castle);
            set { typeof(CastleView).GetField("zoomStep", Any).SetValue(Castle, value); typeof(CastleView).GetField("zoomAnchorScreen", Any).SetValue(Castle, null); }
        }

        // 확대 중에는 카메라가 그때 배율로 가장자리에 막히므로, 목표 위치를 매 프레임 다시 넣는다
        static Vector2? camTarget;

        static void Center(float x, float floor)
        {
            camTarget = new Vector2(x, floor * CastleView.FloorH + CastleView.SlabH + CastleView.RoomH / 2f);
            HoldCamera();
        }

        static void HoldCamera()
        {
            if (camTarget != null) typeof(CastleView).GetField("viewCenter", Any).SetValue(Castle, camTarget.Value);
        }

        static void View(int zoom, float x, float floor) { Zoom = zoom; Center(x, floor); }

        void ResetView()
        {
            var ui = UI;
            if (ui == null) return;
            ui.CloseModal();
            ui.HideHunt();
            if (ui.TutorialVisible) Call("CloseTutorial");
            var c = Castle;
            c.CancelPlacing();
            c.previewRect = null;
        }

        static void ClearToasts()
        {
            var root = (RectTransform)typeof(GameUI).GetField("toastRoot", Any).GetValue(UI);
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        // 쇼케이스 + 투기장·건물 연구·목표/업적 완료(알림이 영상에 끼지 않게)
        static SaveData State()
        {
            var s = StoreShots.Showcase();
            for (int i = 1; i <= 9; i++) s.research.Add($"build_{i}");
            var arena = new BuildingState { uid = s.nextUid++, type = BuildingType.Arena, floor = 5, x = 7, level = 6 };
            s.buildings.Add(arena);
            s.buildings.Add(new BuildingState { uid = s.nextUid++, type = BuildingType.Souvenir, floor = 5, x = 6, level = 5 }); // 투기장 옆 빈 칸 채우기
            MonsterDef best = null;
            foreach (var m in GameData.Monsters)
                if (!m.isPlant && GameData.RegionById(m.region).stage <= 7 && (best == null || m.power > best.power)) best = m;
            CountList.Add(s.arena, best.id, 3);
            s.goalStep = GoalSettings.I.steps.Count;
            foreach (var a in GameData.Achievements) s.achievements.Add(a.id);
            return s;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace Mawang
{
    // 에디터 전용: 플레이 스토어 스크린샷 자동 촬영.
    // 중반쯤 진행한 '쇼케이스' 상태(디스크 세이브와 무관, 저장 안 함)로 폰 배치 화면을 여러 장면 찍는다.
    // 플레이 중에 StoreShots.Run() → <프로젝트>/StoreAssets/screenshots/{ko,en}/NN_이름.png (게임 화면 크기, 1920×1080 권장)
    public class StoreShots : MonoBehaviour
    {
        public static string OutRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../StoreAssets/screenshots"));
        public static bool Done { get; private set; }
        public static string Log = "";

        public static void Run(bool withEnglish = true)
        {
            Done = false; Log = "";
            var go = new GameObject("StoreShots");
            DontDestroyOnLoad(go);
            go.AddComponent<StoreShots>().english = withEnglish;
        }

        bool english;
        int prevLang;

        IEnumerator Start()
        {
            prevLang = L.Lang;
            DevCapture.Active = true;              // 저장 끔
            DevCapture.ForceMobileInEditor = true; // 폰 배치
            yield return Shoot(0, "ko", 30);
            if (english) yield return Shoot(1, "en", 12);
            L.Lang = prevLang;
            Game.PendingState = null;
            DevCapture.ForceMobileInEditor = false;
            Log += "done\n";
            Done = true;
        }

        // ── 한 언어 촬영 ───────────────────────────────────────
        IEnumerator Shoot(int lang, string folder, int count)
        {
            L.Lang = lang;
            Game.PendingState = Showcase();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null; yield return null;
            // 손님이 모이도록 잠깐 빨리 돌린다
            Time.timeScale = 6f;
            yield return new WaitForSecondsRealtime(9f);
            Time.timeScale = 1f;

            string dir = Path.Combine(OutRoot, folder);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);

            var shots = Plan();
            int n = 0;
            foreach (var (name, setup) in shots)
            {
                if (n >= count) break;
                ResetView();
                float wait = 1.2f;
                try { wait = setup(); } catch (Exception e) { Log += $"{name}: {e.Message}\n"; continue; }
                yield return new WaitForSecondsRealtime(wait);
                ClearToasts();
                yield return null;
                n++;
                yield return new WaitForEndOfFrame();
                SaveCropped(ScreenCapture.CaptureScreenshotAsTexture(), Path.Combine(dir, $"{n:00}_{name}.png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }
            ResetView();
            Log += $"{folder}: {n}\n";
        }

        // ── 촬영 목록 (이름, 준비 → 기다릴 초) ──────────────────
        List<(string, Func<float>)> Plan() => new List<(string, Func<float>)>
        {
            ("castle_left",     () => View(3, 20.5f, 2)),
            ("castle_busy",     () => View(3, 30f, 1)),
            ("castle_right",    () => View(3, 62f, 2)),
            ("castle_all",      () => View(0, 41f, 5)),
            ("castle_fit",      () => View(1, 41f, 3)),
            ("tank_closeup",    () => View(4, 15f, 1.5f)),
            ("shops_closeup",   () => View(4, 18f, 0)),
            ("castle_upper",    () => View(2, 41f, 6.5f)),
            ("drag_build",      () => { View(3, 62f, 5); Castle.placing = BuildingType.Tank; Castle.previewRect = (new Vector2Int(6, 5), new Vector2Int(7, 5)); return 1.5f; }),
            ("expand_floor",    () => { View(3, 20.5f, 7.5f); return 1.2f; }),
            ("hunt_lava",       () => { UI.ShowHunt("lava"); return 3.5f; }),
            ("hunt_crystal",    () => { UI.ShowHunt("crystal"); return 3.5f; }),
            ("hunt_grave",      () => { UI.ShowHunt("grave"); return 3.5f; }),
            ("hunt_forest",     () => { UI.ShowHunt("forest"); return 3f; }),
            ("hunt_stages",     () => { UI.ShowHunt("frost"); Call("ToggleStageList"); return 2f; }),
            ("tank_panel",      () => { UI.OpenBuilding(BigTank()); return 1.2f; }),
            ("diner_menu",      () => { UI.OpenBuilding(G.S.buildings.Find(b => b.type == BuildingType.Restaurant && b.level >= 7)); Scroll(0.45f); return 1.2f; }),
            ("staff",           () => { Menu(1); return 1.2f; }),
            ("staff_hire",      () => { Menu(1); Scroll(0.05f); return 1.2f; }),
            ("build",           () => { Menu(0); return 1.2f; }),
            ("research_expand", () => { SetField("researchTab", 0); Menu(4); return 1.2f; }),
            ("research_hunt",   () => { SetField("researchTab", 1); Menu(4); return 1.2f; }),
            ("research_guests", () => { SetField("researchTab", 3); Menu(4); return 1.2f; }),
            ("book",            () => { Menu(5); return 1.2f; }),
            ("book_later",      () => { Menu(5); Scroll(0.55f); return 1.2f; }),
            ("shop_goods",      () => { SetField("shopTab", 1); Menu(2); Scroll(0.35f); return 1.2f; }),
            ("shop_sell",       () => { SetField("shopTab", 2); Menu(2); return 1.2f; }),
            ("storage",         () => { Menu(3); return 1.2f; }),
            ("sidebar",         () => { View(3, 20.5f, 2); Call("ToggleSidebar"); return 1.2f; }),
            ("tutorial",        () => { UI.ShowTutorial(); SetField("tutorialPage", 2); Call("FillTutorial"); return 1.2f; }),
        };

        // 플레이 스토어: 긴 변이 짧은 변의 2배를 넘으면 안 된다 → 가운데를 2:1 로 자른다
        static void SaveCropped(Texture2D src, string path)
        {
            int w = src.width, h = src.height;
            int cw = Mathf.Min(w, h * 2), ch = Mathf.Min(h, w * 2);
            var dst = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            dst.SetPixels(src.GetPixels((w - cw) / 2, (h - ch) / 2, cw, ch));
            dst.Apply();
            File.WriteAllBytes(path, dst.EncodeToPNG());
            Destroy(src);
            Destroy(dst);
        }

        // ── 헬퍼 ───────────────────────────────────────────────
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

        // 카메라: 확대 단계(0 전체 · 1 꽉 채움 · 2 ×1.5 · 3 ×2 · 4 ×3) + 보이는 가운데(월드 x, 층)
        static float View(int zoom, float x, float floor)
        {
            var c = Castle;
            var t = typeof(CastleView);
            t.GetField("zoomStep", Any).SetValue(c, zoom);
            t.GetField("zoomAnchorScreen", Any).SetValue(c, null);
            t.GetField("viewCenter", Any).SetValue(c, new Vector2(x, floor * CastleView.FloorH + CastleView.SlabH + CastleView.RoomH / 2f));
            return 1.2f;
        }

        void ResetView()
        {
            var ui = UI;
            if (ui == null) return;
            ui.CloseModal();
            ui.HideHunt();
            if (ui.TutorialVisible) Call("CloseTutorial");
            var side = (RectTransform)typeof(GameUI).GetField("sidePanel", Any).GetValue(ui);
            if (side.gameObject.activeSelf) Call("ToggleSidebar");
            if (typeof(GameUI).GetField("stageListOpen", Any).GetValue(ui) is bool open && open) SetField("stageListOpen", false);
            var c = Castle;
            c.CancelPlacing();
            c.previewRect = null;
        }

        static void ClearToasts()
        {
            var root = (RectTransform)typeof(GameUI).GetField("toastRoot", Any).GetValue(UI);
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        static BuildingState BigTank()
        {
            BuildingState best = null;
            foreach (var b in G.S.buildings) if (b.type == BuildingType.Tank && (best == null || b.Cells > best.Cells)) best = b;
            return best;
        }

        // ── 쇼케이스 상태: 중반(스테이지 7, 좌 7층·우 6층) ────────
        internal static SaveData Showcase() // 홍보 영상(PromoRecorder)도 같은 상태를 쓴다
        {
            var r = new Random(7);
            var s = new SaveData
            {
                gold = 2_845_320, material = 1_204_560, rp = 86,
                floorsLeft = 7, floorsRight = 6, tutorialDone = true,
                lastDailyTicks = DateTime.Now.Ticks,
            };
            for (int st = 2; st <= 7; st++) s.research.Add($"region_{st}");
            void Up(string id, int lv) { for (int i = 1; i <= lv; i++) s.research.Add($"{id}_{i}"); }
            Up("speed", 5); Up("dura", 4); Up("rest", 3); Up("adm", 6); Up("cap", 4); Up("maki", 5);
            Up("visitor", 5); Up("wallet", 4); Up("patience", 3); Up("sales", 4); Up("lab", 3); Up("trash", 2);

            // 전시할 괴물 후보 (스테이지 1~7, 히든 약간)
            var pool = new List<MonsterDef>();
            foreach (var m in GameData.Monsters)
            {
                int stage = GameData.RegionById(m.region).stage;
                if (stage <= 7 && (!m.hidden || r.NextDouble() < 0.5)) pool.Add(m);
            }

            void B(BuildingType t, int f, int x, int cw = 1, int ch = 1, int lv = 1)
            {
                var b = new BuildingState { uid = s.nextUid++, type = t, floor = f, x = x, cw = cw, ch = ch, level = lv };
                b.investedGold = Game.BuildGold(t, b.Cells); b.investedMat = Game.BuildMat(t, b.Cells);
                if (t == BuildingType.Tank)
                {
                    int space = Game.TankSize(b) * GameData.TankSpacePerSize * 85 / 100, used = 0, guard = 0;
                    while (used < space && guard++ < 200)
                    {
                        var m = pool[r.Next(pool.Count)];
                        if (used + m.space > space) continue;
                        CountList.Add(b.contents, m.id, 1);
                        used += m.space;
                    }
                    b.accumulated = r.Next(200, 4000);
                }
                if (t == BuildingType.Lab) { b.labRp = r.Next(0, 4); b.labTimer = r.Next(0, 100); }
                s.buildings.Add(b);
            }

            // 왼쪽 날개 (0~3)
            B(BuildingType.Restaurant, 0, 0, lv: 7); B(BuildingType.Restaurant, 0, 1, lv: 6); B(BuildingType.Souvenir, 0, 2, lv: 7); B(BuildingType.Souvenir, 0, 3, lv: 5);
            B(BuildingType.Tank, 1, 0, 3, 2, 3); B(BuildingType.Lab, 1, 3, lv: 4); B(BuildingType.Lab, 2, 3, lv: 3);
            B(BuildingType.Tank, 3, 0, 2, 1, 4); B(BuildingType.Dorm, 3, 2, lv: 4); B(BuildingType.Rest, 3, 3);
            B(BuildingType.Tank, 4, 0, 4, 2, 2);
            B(BuildingType.Tank, 6, 0, 1, 1, 5); B(BuildingType.Restaurant, 6, 1, lv: 4);
            // 오른쪽 날개 (4~7)
            B(BuildingType.Souvenir, 0, 4, lv: 8); B(BuildingType.Tank, 0, 5, 3, 1, 3);
            B(BuildingType.Tank, 1, 4, 2, 3, 3); B(BuildingType.Lab, 1, 6, lv: 2); B(BuildingType.Dorm, 1, 7, lv: 3);
            B(BuildingType.Tank, 2, 6, 2, 2, 2);
            B(BuildingType.Tank, 4, 4, 4, 1, 4);
            B(BuildingType.Rest, 5, 4); B(BuildingType.Dorm, 5, 5, lv: 2);

            foreach (var it in GameData.ShopItems) if (it.tier <= 8) CountList.Add(s.goods, it.id, r.Next(40, 220));
            foreach (var bd in GameData.Baits) CountList.Add(s.baits, bd.id, r.Next(120, 600));
            foreach (var m in GameData.Monsters)
            {
                int stage = GameData.RegionById(m.region).stage;
                if (stage > 7 || (m.hidden && stage > 3)) continue;
                int caught = stage <= 3 ? r.Next(20, 80) : r.Next(3, 35);
                CountList.Add(s.caught, m.id, caught);
                if (caught >= GameData.CollectionRegisterCount && r.NextDouble() < 0.7) s.collectionClaimed.Add(m.id);
                if (r.NextDouble() < 0.5) CountList.Add(s.monsters, m.id, r.Next(1, 12));
            }

            void Hunter(string id, string region, int bait, Power p)
            {
                var d = GameData.StaffById(id);
                s.hunters.Add(new HunterState { staffId = id, dispatched = true, regionId = region, bait = bait, power = p, durabilityLeft = d.durability + 8, timer = (float)r.NextDouble() * 5f });
            }
            Hunter("hunter_imp", "lava", 1, Power.Mid); Hunter("hunter_orc", "crystal", 2, Power.Strong); Hunter("hunter_lich", "grave", 0, Power.Weak);

            void Staff(string id, int wing, int floor) => s.castleStaff.Add(new CastleStaffState { uid = s.nextUid++, staffId = id, dispatched = true, wing = wing, floor = floor });
            Staff("castle_succ", 0, 0); Staff("castle_garg", -1, 0); Staff("castle_skel", 0, -1); Staff("castle_skel", 1, -1);
            Staff("castle_witch", -1, -1); Staff("castle_succ", 0, 6); Staff("castle_garg", 1, 0);

            foreach (var reg in new[] { "lava", "crystal", "grave", "forest", "frost" })
                for (int i = 0; i < 3; i++)
                    s.trashItems.Add(new TrashItem { uid = s.nextTrashUid++, regionId = reg, x = 60 + r.Next(0, 190), y = 4 + r.Next(0, 20), can = r.NextDouble() < 0.5 });
            return s;
        }
    }
}
#endif

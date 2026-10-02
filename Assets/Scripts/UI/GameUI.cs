using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Mawang
{
    // 화면 전체 구성: 상단 재화 바 / 중앙 마왕성 / 우측 현황·알림 / 하단 메뉴 독 / 모달 창 / 토스트
    public partial class GameUI : MonoBehaviour
    {
        // 화면 비율이 달라도 틀이 화면 전체(안전 영역)를 채운다.
        // 폰은 기준 해상도를 낮춰 버튼·글자를 PC보다 약 2.2배 크게 그린다.
        // 폰 화면이 좁아지므로 사이드바는 접었다 펴는 방식, 메뉴 창은 화면 전체, 포획장은 전용 배치.
        public static bool Mobile => Application.isMobilePlatform || DevCapture.ForceMobile;
        static Vector2 RefRes => Mobile ? new Vector2(880f, 495f) : new Vector2(1920f, 1080f);
        static float TopH => Mobile ? 64f : 72f;
        static float DockH => Mobile ? 70f : 116f;   // 폰 하단 탭은 30% 작게
        static float SideW => Mobile ? 520f : 470f; // 폰은 여닫는 덮개라 넓게
        const float SideMargin = 14f, Gap = 16f, SideToggleW = 52f;
        static float CastleRight => Mobile ? SideMargin + SideToggleW + 8f : SideMargin + SideW + Gap; // 마왕성 영역 오른쪽 여백
        static float CastleCenterX => (20f - CastleRight) / 2f; // 화면 가운데 대비 마왕성 영역 가운데
        RectTransform sidePanel, dockBar;

        // 모달·포획장·튜토리얼이 떠 있으면 마왕성 끌기·확대를 막는다
        public bool BlocksWorld => (modal != null && modal.gameObject.activeSelf) || HuntVisible || TutorialVisible;

        Game g;
        RectTransform frame;
        CastleView castle;

        // 모달 창
        RectTransform modal, modalWindow, modalContent;
        Text modalTitle;
        UnityEngine.UI.Image modalIcon;
        ScrollRect modalScroll;
        Action<RectTransform> modalBuilder;
        bool needRebuild;
        float rebuildCooldown;

        // 알림 로그 / 토스트
        RectTransform logRoot, toastRoot;
        readonly Queue<GameObject> logLines = new Queue<GameObject>();

        RectTransform sidebarContent, logBox;
        UnityEngine.UI.Button sideToggle;
        int seenGoalStep = -1; // 폰: 사이드바를 열어 본 목표 단계. 지금 단계와 다르면 별 버튼에 '!' 배지 (처음 시작할 때 포함)
        struct MenuDef
        {
            public string label, icon, title;
            public Action<RectTransform> build;
            public Func<int> badge;
            public string tip;
        }
        MenuDef[] menus;

        void Start()
        {
            g = Game.I;
            BuildCanvas();
            g.Changed += OnChanged;
            g.Notified += OnNotify;
            AddLog(L.T("마왕성에 오신 것을 환영합니다! 빈 칸을 탭해 건물을 지어 보세요.", "Welcome to the Demon Castle! Tap an empty cell to build."));
            if (!g.S.tutorialDone) ShowTutorial();
        }

        void OnDestroy()
        {
            if (g == null) return;
            g.Changed -= OnChanged;
            g.Notified -= OnNotify;
        }

        void OnChanged() => needRebuild = true;

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform);
            }

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform);
            var canvas = canvasGo.GetComponent<Canvas>();
            var cam = Camera.main;
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                if (cam.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // 월드 스프라이트보다 항상 위 (그리기 + 클릭 판정)
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            frame = UIKit.Rect("Frame", canvasGo.transform);
            UIKit.Stretch(frame);
            frame.gameObject.AddComponent<SafeArea>(); // 노치·펀치홀 피하기

            DefineMenus();
            BuildTopBar();
            BuildDock();
            BuildSidebar();

            // 성은 월드 스프라이트로 그린다. UI에는 카메라를 맞출 빈 영역만 둔다(그래픽 없음 → 클릭이 월드로 통과).
            var castleArea = UIKit.Rect("CastleArea", frame);
            UIKit.Anchor(castleArea, Vector2.zero, Vector2.one, new Vector2(20, DockH + 8), new Vector2(-CastleRight, -(TopH + 8)));
            castle = new GameObject("CastleWorld").AddComponent<CastleView>();
            castle.Init(this, castleArea);
            if (!Mobile) BuildZoomControl(castleArea); // 폰은 핀치로 확대 (버튼이 마왕성을 가린다)
            BuildHuntScreen(castleArea);
#if UNITY_EDITOR
            if (!DevCapture.Active) BuildDevPanel(); // 에디터에서만: 테스트용 재화 추가 (촬영 중엔 숨김)
#endif

            BuildModal();
            BuildToasts(); // 모달 위에 보이도록 나중에 만든다
            Tooltip.Init(frame);
        }

        // 마왕성 확대/축소 단계 버튼 (핀치·마우스 휠과 같은 단계)
        void BuildZoomControl(RectTransform castleArea)
        {
            var panel = UIKit.Panel(castleArea, "ui_panel", "Zoom");
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.zero;
            prt.anchoredPosition = new Vector2(8, 8);
            prt.sizeDelta = new Vector2(262, 64);
            var row = UIKit.Row(panel.transform, 64, 6);
            UIKit.Stretch(row, 6, 0, 6, 0);
            UIKit.Button(row, "-", () => castle.ZoomStep(-1), 56, 52, Btn.Alt, UIKit.TL).BindEnabled(() => castle.CanZoomOut).Tip(L.T("축소 (마우스 휠)", "Zoom out (mouse wheel)"));
            UIKit.Label(row, "", UIKit.TS, TextAnchor.MiddleCenter, UIKit.Gold).Bind(() => castle.ZoomLabel);
            UIKit.Button(row, "+", () => castle.ZoomStep(+1), 56, 52, Btn.Alt, UIKit.TL).BindEnabled(() => castle.CanZoomIn).Tip(L.T("확대 (마우스 휠)\n끌어서 이동합니다.", "Zoom in (mouse wheel)\nDrag to pan."));
        }

        void DefineMenus()
        {
            menus = new[]
            {
                new MenuDef { label = L.T("건설", "Build"), icon = "ic_build", title = L.T("건설", "Build"), build = BuildBuildPanel,
                    tip = L.T("건물을 고른 뒤 성의 빈 칸을 누르거나 끌어서 짓습니다.\n빈 칸을 바로 탭해도 지을 수 있습니다.", "Pick a building, then tap or drag over empty cells.\nYou can also tap an empty cell directly.") },
                new MenuDef { label = L.T("직원", "Staff"), icon = "castle_skel", title = L.T("직원 관리", "Staff"), build = BuildStaffPanel,
                    badge = () => g.S.castleStaff.FindAll(c => !c.dispatched).Count,
                    tip = L.T("성 관리자를 파견하고 담당 구역을 정합니다.\n빨간 숫자 = 대기 중인 성 관리자", "Dispatch castle staff and assign areas.\nRed number = idle staff") },
                new MenuDef { label = L.T("상점", "Shop"), icon = "ic_shop", title = L.T("상점", "Shop"), build = BuildShopPanel,
                    tip = L.T("미끼, 음식·기념품 재고를 사고 괴물을 팝니다.", "Buy bait and goods, sell monsters.") },
                new MenuDef { label = L.T("보관함", Mobile ? "Items" : "Storage"), icon = "ic_bag", title = L.T("보관함", "Storage"), build = BuildInventoryPanel,
                    tip = L.T("가진 괴물·미끼·상품을 한눈에 봅니다.", "See your monsters, bait and goods.") },
                new MenuDef { label = L.T("연구", Mobile ? "Tech" : "Research"), icon = "ic_research", title = L.T("흑마법 연구 · 증축", "Research & Expansion"), build = BuildResearchPanel,
                    badge = AffordableUpgrades,
                    tip = L.T("보석으로 연구하고, 골드·자재로 성을 증축합니다.\n빨간 숫자 = 지금 할 수 있는 연구", "Spend Gems on research and Gold on expansions.\nRed number = affordable now") },
                new MenuDef { label = L.T("도감", "Book"), icon = "ic_book", title = L.T("괴물 도감", "Monster Book"), build = BuildCollectionPanel,
                    badge = () => GameData.Monsters.FindAll(m => g.CanClaimCollection(m.id)).Count,
                    tip = L.T("잡은 괴물을 등록하고 보상(보석 포함)을 받습니다.\n빨간 숫자 = 등록 가능한 괴물", "Register monsters for rewards (including Gems).\nRed number = ready to register") },
                new MenuDef { label = L.T("업적", Mobile ? "Goals" : "Achieve"), icon = "ic_trophy", title = L.T("업적", "Achievements"), build = BuildAchievementPanel,
                    tip = L.T("달성하면 골드를 자동으로 받습니다.", "Complete them to earn Gold automatically.") },
            };
        }

        int AffordableUpgrades()
        {
            int n = 0;
            foreach (var u in GameData.Upgrades) if (g.UpgradeBlock(u) == null) n++;
            foreach (var r in GameData.Research) if (g.ResearchBlock(r) == null) n++;
            for (int w = 0; w < 2; w++) if (g.ExpandBlock(w) == null) n++;
            return n;
        }

        void OpenMenu(int i) { Sound.Play("open"); OpenModal(menus[i].title, menus[i].build, menus[i].icon); }

        // ── 상단 바: 재화 알약 + 손님 + 시스템 버튼 ─────────────
        void BuildTopBar()
        {
            var bar = UIKit.Panel(frame, "ui_panel", "TopBar").rectTransform;
            UIKit.Anchor(bar, new Vector2(0, 1), Vector2.one, new Vector2(-6, -TopH), new Vector2(6, 6));
            var row = UIKit.Row(bar, TopH, Mobile ? 8 : 14);
            UIKit.Stretch(row, 0, 0, 0, 6);
            row.GetComponent<HorizontalLayoutGroup>().padding = Mobile ? new RectOffset(14, 14, 6, 6) : new RectOffset(26, 26, 10, 10);
            if (Mobile) row.GetComponent<HorizontalLayoutGroup>().spacing = 6;
            float tabW = Mobile ? 112 : 176;

            // 화면 전환 탭: 마왕성 / 포획장 (Tab 키)
            castleTab = UIKit.Button(row, L.T("마왕성", "Castle"), HideHunt, tabW, 52, Btn.Accent, Mobile ? UIKit.TS : UIKit.TM, "up_floor", Mobile ? 24 : 32);
            castleTab.Tip(L.T("마왕성\n건물을 짓고 손님을 맞습니다.  [Tab]", "Castle\nBuild rooms and welcome guests.  [Tab]"));
            huntTab = UIKit.Button(row, L.T("포획장", "Hunt"), () => ShowHunt(), tabW, 52, Btn.Alt, Mobile ? UIKit.TS : UIKit.TM, "ic_hunt", Mobile ? 24 : 32);
            huntTab.Tip(L.T("포획장\n포획대원을 파견해 괴물을 잡고, 쓰레기를 줍습니다.  [Tab]\n빨간 숫자 = 쉬고 있는 포획대원", "Hunting Grounds\nSend hunters to catch monsters and pick up trash.  [Tab]\nRed number = idle hunters"));
            UIKit.Badge(huntTab.transform, () => g.S.hunters.FindAll(h => !h.dispatched && h.restTimer <= 0).Count, new Vector2(-6, -6));
            if (!Mobile) UIKit.Size(UIKit.Box(row, new Color(0.3f, 0.24f, 0.36f), "Divider"), 3, 44);

            Pill(row, "ic_gold", () => g.S.gold, () => g.GoldFull, UIKit.Gold, Mobile ? 160 : 230, () => L.T($"골드 (최대 {CapText(g.GoldCap)})\n건설·구매·고용에 씁니다.\n최대 보유량에 닿으면 가게·전시 우리·투기장 수입이 멈춥니다.", $"Gold (max {CapText(g.GoldCap)})\nFor building, buying and hiring.\nAt the max, shop, cage and arena income stops."));
            Pill(row, "ic_mat", () => g.S.material, () => g.S.material >= g.MatCap, UIKit.MatText, Mobile ? 140 : 200, () => L.T($"건설 자재 (최대 {CapText(g.MatCap)})\n건설·업그레이드에 씁니다.\n상점에서 골드와 1:1 교환", $"Materials (max {CapText(g.MatCap)})\nFor building and upgrades.\nExchange 1:1 with Gold in the Shop."));
            Pill(row, "ic_rp", () => g.S.rp, () => g.GemFull, UIKit.RpText, Mobile ? 100 : 150, () => L.T($"보석 — 가장 귀한 재료 (최대 {CapText(g.GemCap)})\n흑마법 연구소에서 아주 천천히 나옵니다.\n연구에만 씁니다.", $"Gems — the rarest resource (max {CapText(g.GemCap)})\nSlowly made by the Dark Magic Lab.\nUsed only for research."));

            var visitors = UIKit.Panel(row, "ui_inset", "Visitors");
            UIKit.Size(visitors, Mobile ? 120 : 230, 50);
            var vr = UIKit.Row(visitors.transform, 50, 8);
            UIKit.Stretch(vr, 8, 0, 8, 0);
            UIKit.Icon(vr, "hero_warrior", Mobile ? 28 : 36);
            var vLabel = UIKit.Label(vr, "", Mobile ? UIKit.TS : UIKit.TM);
            vLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            vLabel.Bind(() => Mobile
                ? $"{g.Sim.visitors.Count}<color=#{UIKit.Hex(UIKit.TextDim)}>/{g.Sim.VisitorMax}</color>"
                : $"{g.Sim.visitors.Count}<size=20><color=#{UIKit.Hex(UIKit.TextDim)}> / {g.Sim.VisitorMax}{L.T("명", "")}</color></size>");
            visitors.Tip(() => L.T($"용사 손님 {g.Sim.visitors.Count}명 (최대 {g.Sim.VisitorMax}명)\n판매 {g.Sim.visitorsServed}회 · 불만 이탈 {g.Sim.visitorsLeftUnhappy}명\n\n방문할 수 있는 가게·전시 우리·투기장을 늘리거나 [연구 > 손님 유치]로 손님을 늘릴 수 있습니다.",
                                   $"Hero guests {g.Sim.visitors.Count} (max {g.Sim.VisitorMax})\nSales {g.Sim.visitorsServed} · Left unhappy {g.Sim.visitorsLeftUnhappy}\n\nMore shops, cages and the Arena, or [Research > Advertising], bring more guests."));

            UIKit.Flex(row);
            if (!Mobile) // 폰은 상단에 시계가 있다
                UIKit.Label(row, "", UIKit.TS, TextAnchor.MiddleRight, UIKit.TextDim, 150).Bind(() => DateTime.Now.ToString("HH:mm"))
                    .Tip(L.T("현재 시각\n휴게실 지원금은 매일 06시에 지급됩니다.", "Current time\nLounge bonus is paid daily at 06:00."));
            if (!Mobile) UIKit.Button(row, "", () => { g.Save(); Toast(L.T("저장했습니다.", "Saved."), "ic_save"); }, 56, 52, Btn.Alt, UIKit.TS, "ic_save").Tip(L.T("저장 (30초마다 자동 저장)", "Save (auto-saves every 30s)"));
            UIKit.Button(row, "", OpenSettings, 56, 52, Btn.Alt, UIKit.TS, "ic_gear").Tip(L.T("설정 — 소리 · 언어 · 새로 시작", "Settings — sound, language, new game"));
        }

        static string CapText(int cap) => cap == int.MaxValue ? L.T("무제한", "unlimited") : cap.ToString("N0"); // 테스트용 상한 끄기

        void Pill(Transform parent, string icon, Func<long> value, Func<bool> full, Color color, float w, Func<string> tip)
        {
            var box = UIKit.Panel(parent, "ui_inset", "Pill");
            UIKit.Size(box, w, 50);
            var r = UIKit.Row(box.transform, 50, 8);
            UIKit.Stretch(r, 8, 0, 12, 0);
            UIKit.Icon(r, icon, Mobile ? 32 : 36);
            var num = UIKit.Label(r, "", UIKit.TM, TextAnchor.MiddleRight, color);
            num.horizontalOverflow = HorizontalWrapMode.Overflow; // 큰 숫자가 두 줄로 꺾이지 않게
            num.BindCounter(value, "N0", Mobile, full); // 폰: 2.93M 처럼 줄여 쓴다. 최대 보유량이면 빨간색
            box.Tip(tip);
        }

        // ── 설정: 소리 · 언어 · 튜토리얼 · 새로 시작 ────────────
        void OpenSettings()
        {
            Sound.Play("open");
            OpenModal(L.T("설정", "Settings"), BuildSettingsPanel, "ic_gear");
        }

        void BuildSettingsPanel(RectTransform c)
        {
            UIKit.Header(c, L.T("소리", "Sound"), "ic_sound");
            VolumeRow(c, L.T("배경음", "Music"), () => Sound.BgmVolume, v => Sound.BgmVolume = v);
            VolumeRow(c, L.T("효과음", "Effects"), () => Sound.SfxVolume, v => { Sound.SfxVolume = v; Sound.Play("coin"); });

            UIKit.Header(c, L.T("언어 · Language", "Language · 언어"), "up_region");
            var lang = UIKit.Row(c, 64, 12, true);
            UIKit.Button(lang, "한국어", () => SetLanguage(0), 220, 52, L.Lang == 0 ? Btn.Accent : Btn.Alt, UIKit.TM);
            UIKit.Button(lang, "English", () => SetLanguage(1), 220, 52, L.Lang == 1 ? Btn.Accent : Btn.Alt, UIKit.TM);

            UIKit.Header(c, L.T("게임", "Game"), "ic_star");
            var row = UIKit.Row(c, 64, 12, true);
            UIKit.Button(row, L.T("튜토리얼 다시 보기", "Replay tutorial"), () => { CloseModal(); ShowTutorial(); }, 300, 52, Btn.Primary, UIKit.TS, "ic_book", 24);
            UIKit.Flex(row);
            UIKit.Button(row, L.T("새로 시작", "New game"), ConfirmReset, 220, 52, Btn.Danger, UIKit.TS, "ic_reset", 24);
            UIKit.Note(c, L.T("진행 상황은 30초마다, 그리고 앱을 내릴 때 자동으로 저장됩니다.", "Progress auto-saves every 30 seconds and when the app is closed."));
        }

        void VolumeRow(RectTransform c, string label, Func<float> get, Action<float> set)
        {
            var row = UIKit.Row(c, 64, 12, true);
            UIKit.Label(row, label, UIKit.TM, TextAnchor.MiddleLeft, UIKit.TextMain, 160);
            UIKit.Button(row, "-", () => set(Mathf.Round((get() - 0.1f) * 10f) / 10f), 56, 52, Btn.Alt, UIKit.TL).BindEnabled(() => get() > 0.001f);
            UIKit.Bar(row, get, new Color(0.7f, 0.5f, 0.95f), -1, 32, () => $"{Mathf.RoundToInt(get() * 100)}%");
            UIKit.Button(row, "+", () => set(Mathf.Round((get() + 0.1f) * 10f) / 10f), 56, 52, Btn.Alt, UIKit.TL).BindEnabled(() => get() < 0.999f);
        }

        // 언어를 바꾸면 저장 후 장면을 다시 불러 모든 글자를 새 언어로 만든다
        void SetLanguage(int lang)
        {
            if (L.Lang == lang) return;
            g.Save();
            L.Lang = lang;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void ConfirmReset()
        {
            OpenModal(L.T("새 게임", "New game"), c =>
            {
                UIKit.Spacer(c, 20);
                UIKit.Label(UIKit.Row(c, 60).transform, L.T("모든 진행 상황이 삭제됩니다. 정말 새로 시작할까요?", "All progress will be deleted. Start over?"), UIKit.TM, TextAnchor.MiddleCenter);
                UIKit.Spacer(c, 20);
                var row = UIKit.Row(c, 60, 20);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UIKit.Button(row, L.T("새로 시작", "Start over"), () => { CloseModal(); g.ResetGame(); seenGoalStep = -1; castle.Rebuild(true); RebuildSidebar(); ShowTutorial(); }, 220, 56, Btn.Danger, UIKit.TM, "ic_reset");
                UIKit.Button(row, L.T("취소", "Cancel"), OpenSettings, 160, 56, Btn.Alt, UIKit.TM);
            }, "ic_reset");
        }

        // ── 하단 독: 아이콘 버튼 + 단축키 + 알림 배지 ─────────
        void BuildDock()
        {
            var bar = dockBar = UIKit.Panel(frame, "ui_panel", "Dock").rectTransform;
            UIKit.Anchor(bar, Vector2.zero, new Vector2(1, 0), new Vector2(-6, -6), new Vector2(6, DockH));
            var row = UIKit.Row(bar, DockH, Mobile ? 6 : 10);
            UIKit.Stretch(row, 0, 6, 0, 0);
            row.GetComponent<HorizontalLayoutGroup>().padding = Mobile ? new RectOffset(10, 10, 4, 4) : new RectOffset(24, 24, 10, 10);

            float bw = Mobile ? 73 : 120, bh = Mobile ? 59 : 94;
            for (int i = 0; i < menus.Length; i++)
            {
                int idx = i;
                var m = menus[i];
                var b = UIKit.Button(row, "", () => OpenMenu(idx), bw, bh, Btn.Primary, UIKit.TS);
                var content = b.Content();
                DestroyImmediate(content.GetComponent<HorizontalLayoutGroup>());
                var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.childAlignment = TextAnchor.MiddleCenter;
                vl.spacing = Mobile ? -2 : 2;
                vl.childControlWidth = vl.childControlHeight = true;
                vl.childForceExpandWidth = vl.childForceExpandHeight = false;
                for (int k = content.childCount - 1; k >= 0; k--) DestroyImmediate(content.GetChild(k).gameObject);
                UIKit.Icon(content, m.icon, Mobile ? 32 : 48);
                var lbl = UIKit.Label(content, m.label, UIKit.TS, TextAnchor.MiddleCenter);
                lbl.GetComponent<LayoutElement>().flexibleWidth = 0;
                lbl.horizontalOverflow = HorizontalWrapMode.Overflow;

                // 단축키 번호
                if (!Mobile) // 폰은 키보드가 없다
                {
                    var key = UIKit.Label(b.transform, $"{i + 1}", UIKit.TS, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.5f));
                    UIKit.Place(key.rectTransform, 9, -7, 30, 26, new Vector2(0, 1));
                    key.rectTransform.pivot = new Vector2(0, 1);
                }
                if (m.badge != null) UIKit.Badge(b.transform, m.badge, Mobile ? new Vector2(-4, -4) : new Vector2(-6, -6));
                var tip = m.tip;
                b.Tip(() => $"{m.label}  [{idx + 1}]\n{tip}");
            }

            // 모두 수령: 전시 우리 관람료 + 연구소 보석
            UIKit.Spacer(row, 0);
            var all = UIKit.Button(row, Mobile ? "" : L.T("모두 수령", "Collect all"), CollectAll, Mobile ? 73 : 170, bh, Btn.Good, UIKit.TS, "ic_gold", Mobile ? 32 : 40);
            if (Mobile)
            {
                var content = all.Content();
                DestroyImmediate(content.GetComponent<HorizontalLayoutGroup>());
                var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.childAlignment = TextAnchor.MiddleCenter;
                vl.childControlWidth = vl.childControlHeight = true;
                vl.childForceExpandWidth = vl.childForceExpandHeight = false;
                var l = UIKit.Label(content, L.T("수령", "Collect"), UIKit.TS, TextAnchor.MiddleCenter);
                l.GetComponent<LayoutElement>().flexibleWidth = 0;
                l.horizontalOverflow = HorizontalWrapMode.Overflow;
                ((RectTransform)content.GetChild(0)).GetComponent<LayoutElement>().preferredHeight = 32;
            }
            all.BindEnabled(() => g.S.buildings.Exists(b => (b.type == BuildingType.Tank && b.accumulated >= 1f) || (b.type == BuildingType.Lab && b.labRp > 0)));
            all.Tip(L.T("모든 전시 우리의 관람료와 연구소의 보석을 한 번에 받습니다.", "Collect all cage fees and lab Gems at once."));

            if (!Mobile) UIKit.Flex(row);
            var hint = UIKit.Panel(row, "ui_inset", "Hint");
            var hle = UIKit.Size(hint, Mobile ? -1 : 560, Mobile ? 56 : 70);
            if (Mobile) { hle.flexibleWidth = 1; hle.minWidth = 0; } // 폰: 남는 폭만큼
            var hr = UIKit.Row(hint.transform, Mobile ? 56 : 70, 10);
            UIKit.Stretch(hr, 14, 0, 14, 0);
            if (!Mobile) UIKit.Icon(hr, "ic_build", 32);
            UIKit.Label(hr, "", UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim).Bind(() => castle != null && castle.placing != null
                ? $"<color=#{UIKit.Hex(UIKit.Gold)}>[{GameData.Buildings[castle.placing.Value].Name}]</color> " + (Mobile ? L.T("탭·끌기: 짓기\n길게: 취소", "tap/drag: build\nhold: cancel") : L.T("배치 중\n빈 칸 클릭·끌기: 짓기 · 우클릭/ESC: 취소", "placing\nClick/drag empty cells · Right-click/ESC: cancel"))
                : Mobile ? L.T("탭: 수령·판매\n길게: 관리", "Tap: collect/sell\nHold: manage")
                : L.T("건물 좌클릭: 수령·판매 · 우클릭: 관리\n빈 칸 클릭: 건설 · 숫자키 1~7 · Tab · ESC", "Left-click: collect/sell · Right-click: manage\nClick empty cell: build · Keys 1-7 · Tab · ESC"));
        }

        void CollectAll()
        {
            var (gold, gems) = g.CollectAll();
            if (gold == 0 && gems == 0) return;
            string msg = gems > 0 ? L.T($"+{gold:N0} 골드 · 보석 +{gems}", $"+{gold:N0} Gold · +{gems} Gems") : L.T($"+{gold:N0} 골드", $"+{gold:N0} Gold");
            Toast(msg, gems > 0 ? "ic_rp" : "ic_gold");
        }

        // ── 우측 사이드바: 목표 / 포획대 / 알림 ──────────────
        void BuildSidebar()
        {
            var sideImg = UIKit.Panel(frame, "ui_panel", "Sidebar");
            var side = sidePanel = sideImg.rectTransform;
            if (Mobile)
            {
                // 폰: 오른쪽 끝 버튼으로 여닫는 덮개. 마왕성은 가로 전체를 쓴다.
                sideImg.raycastTarget = true;
                float right = SideMargin + SideToggleW + 8;
                UIKit.Anchor(side, new Vector2(1, 0), Vector2.one, new Vector2(-(right + SideW), 8), new Vector2(-right, -(TopH + 8)));
                var toggle = UIKit.Button(frame, "", ToggleSidebar, SideToggleW, 140, Btn.Primary, UIKit.TS, "ic_star", 32);
                var trt = (RectTransform)toggle.transform;
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 1);
                trt.anchoredPosition = new Vector2(-SideMargin, -(TopH + 8));
                toggle.Tip(L.T("현황 · 알림 열기/닫기", "Status · notifications"));
                sideToggle = toggle;
            }
            else UIKit.Anchor(side, new Vector2(1, 0), Vector2.one, new Vector2(-(SideMargin + SideW), DockH + 8), new Vector2(-SideMargin, -(TopH + 8)));

            sidebarContent = UIKit.Column(side, 6);
            sidebarContent.anchorMin = new Vector2(0, 1);
            sidebarContent.anchorMax = new Vector2(1, 1);
            sidebarContent.pivot = new Vector2(0.5f, 1);
            sidebarContent.offsetMin = new Vector2(14, 0);
            sidebarContent.offsetMax = new Vector2(-14, -12);
            sidebarContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RebuildSidebar();

            // 알림 로그 (아래 고정). 줄은 위에서부터 쌓고 넘치면 잘린다(줄끼리 겹치지 않게 높이를 줄이지 않는다).
            logBox = UIKit.Panel(side, "ui_inset", "Log").rectTransform;
            logBox.anchorMin = new Vector2(0, 0);
            logBox.anchorMax = new Vector2(1, 0);
            logBox.pivot = new Vector2(0.5f, 0);
            logBox.offsetMin = new Vector2(12, 12);
            logBox.offsetMax = new Vector2(-12, 300);
            var logTitle = UIKit.Row(logBox, 34, 6);
            logTitle.anchorMin = new Vector2(0, 1); logTitle.anchorMax = new Vector2(1, 1); logTitle.pivot = new Vector2(0.5f, 1);
            logTitle.offsetMin = new Vector2(10, -40); logTitle.offsetMax = new Vector2(-10, -6);
            UIKit.Icon(logTitle, "ic_research", 24);
            UIKit.Label(logTitle, L.T("알림", "Log"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.Gold);
            var logView = UIKit.Rect("LogView", logBox);
            UIKit.Stretch(logView, 12, 8, 12, 44);
            logView.gameObject.AddComponent<RectMask2D>();
            logRoot = UIKit.Column(logView, 6);
            logRoot.anchorMin = new Vector2(0, 1); logRoot.anchorMax = new Vector2(1, 1); logRoot.pivot = new Vector2(0.5f, 1);
            logRoot.offsetMin = logRoot.offsetMax = Vector2.zero;
            var vl = logRoot.GetComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlHeight = true;
            logRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            FitLogBox();
            if (Mobile)
            {
                side.gameObject.SetActive(false);
                // 배지는 사이드바를 닫은 뒤에 단다 (먼저 달면 열려 있는 것으로 보고 '본 것'으로 처리된다)
                UIKit.Badge(sideToggle.transform, () => NewGoalUnseen ? 1 : 0, new Vector2(-8, -8), () => "!");
            }
        }

        void ToggleSidebar()
        {
            bool on = !sidePanel.gameObject.activeSelf;
            sidePanel.gameObject.SetActive(on);
            if (on) { seenGoalStep = g.S.goalStep; sidePanel.SetSiblingIndex(modal.GetSiblingIndex()); FitLogBox(); } // 메뉴 창보다는 아래
        }

        // 폰: 아직 보지 않은 새 목표가 있다 (사이드바가 열려 있으면 바로 본 것으로 친다)
        bool NewGoalUnseen
        {
            get
            {
                if (g.CurrentGoal == null) return false;
                if (sidePanel.gameObject.activeSelf) seenGoalStep = g.S.goalStep;
                return seenGoalStep != g.S.goalStep;
            }
        }

        // 알림창은 위쪽 내용(목표·포획대) 아래 남는 높이만 쓴다 → 낮은 화면에서도 겹치지 않는다
        void FitLogBox()
        {
            if (logBox == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(sidebarContent);
            float sideH = ((RectTransform)logBox.parent).rect.height;
            float room = sideH - sidebarContent.rect.height - 12f - 12f;
            logBox.gameObject.SetActive(room >= 90f); // 자리가 모자라면 숨긴다(중요한 알림은 토스트로 뜬다)
            logBox.offsetMax = new Vector2(-12, Mathf.Clamp(room, 90f, Mobile ? 420f : 300f));
        }

        void RebuildSidebar()
        {
            for (int i = sidebarContent.childCount - 1; i >= 0; i--) DestroyImmediate(sidebarContent.GetChild(i).gameObject); // 바로 지워야 높이 계산이 맞다

            // 다음 목표: 1회성 임무를 모두 마치면 칸 자체를 띄우지 않는다
            if (g.CurrentGoal != null)
            {
                UIKit.Header(sidebarContent, L.T("다음 목표", "Next goal"), "ic_star");
                var goal = UIKit.Row(sidebarContent, 62, 8, true).Grow(); // 안내 + 보상이 여러 줄이면 늘어난다
                UIKit.Label(goal, "", UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextMain).Bind(GoalText);
            }

            UIKit.Header(sidebarContent, L.T("포획대 현황", "Hunters"), "ic_hunt");
            foreach (var h in g.S.hunters)
            {
                var hs = h;
                var card = UIKit.Card(sidebarContent, "ui_row", 10, 4, TextAnchor.UpperLeft);
                card.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
                var top = UIKit.Row(card, 30, 8);
                UIKit.Icon(top, hs.staffId, 24);
                UIKit.Label(top, "", UIKit.TS).Bind(() => $"{g.Def(hs).Name} <color=#{UIKit.Hex(UIKit.TextDim)}>· {GameData.RegionById(hs.regionId).Name}</color>");
                UIKit.Amount(top, "up_dura", () => $"{hs.durabilityLeft}/{g.HuntDurabilityMax(hs)}", null, UIKit.TS, 20);
                var bottomRow = UIKit.Row(card, 26, 8);
                UIKit.Bar(bottomRow, () => HunterProgress(hs), new Color(0.45f, 0.85f, 0.5f), -1, 24, () => g.HunterStatus(hs));
                card.Tip(() => L.T($"{g.Def(hs).Name}\n{g.HunterStatus(hs)}\n미끼: {GameData.Baits[hs.bait].Name} · 힘 {GameData.PowerName((int)hs.power)}\n\n클릭하면 포획장에서 지켜봅니다.",
                                   $"{g.Def(hs).Name}\n{g.HunterStatus(hs)}\nBait: {GameData.Baits[hs.bait].Name} · Power {GameData.PowerName((int)hs.power)}\n\nClick to watch in the hunting grounds."));
                card.gameObject.GetComponent<PointerHandler>().onLeft += () => ShowHunt(hs.regionId);
            }

            FitLogBox();
        }

        float HunterProgress(HunterState h)
        {
            if (h.restTimer > 0) return 1f - h.restTimer / (h.penalty ? GameData.HunterRecallPenalty : g.Mods.restTime);
            if (!h.dispatched) return 0f;
            return h.timer / g.HuntInterval(h);
        }

        // 다음 목표: GameUI.Goals.cs (단계는 GoalSettings 에셋에서 고친다)

        // ── 알림: 로그 + 토스트 ────────────────────────────────
        void OnNotify(string msg, string icon)
        {
            AddLog(msg);
            if (icon != null) Toast(msg, icon);
        }

        void AddLog(string msg)
        {
            if (logRoot == null) return;
            // [태그] 부분은 금색
            string body = msg;
            if (msg.StartsWith("[") && msg.IndexOf(']') > 0)
            {
                int e = msg.IndexOf(']') + 1;
                body = UIKit.Col(msg.Substring(0, e), UIKit.Gold) + msg.Substring(e);
            }
            var t = UIKit.Label(logRoot, $"{UIKit.Col(DateTime.Now.ToString("HH:mm"), UIKit.TextDim)} {body}", UIKit.TS, TextAnchor.UpperLeft, UIKit.TextMain, -1, false);
            t.transform.SetAsFirstSibling();
            logLines.Enqueue(t.gameObject);
            while (logLines.Count > 12) { var old = logLines.Dequeue(); old.SetActive(false); Destroy(old); }
        }

        void BuildToasts()
        {
            toastRoot = UIKit.Column(frame, 8);
            toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(0.5f, 1);
            toastRoot.pivot = new Vector2(0.5f, 1);
            toastRoot.anchoredPosition = new Vector2(CastleCenterX, -TopH - (Mobile ? 40 : 76)); // 모달 제목 리본 아래
            toastRoot.sizeDelta = new Vector2(Mobile ? 720 : 900, 0);
            var vl = toastRoot.GetComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childForceExpandWidth = false;
        }

        public void Toast(string msg, string icon)
        {
            if (toastRoot == null) return;
            var card = UIKit.Panel(toastRoot, "ui_panel", "Toast");
            var hl = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 20, 10, 10);
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.Icon(card.transform, icon, 32);
            var t = UIKit.Label(card.transform, msg, UIKit.TS);
            var le = t.GetComponent<LayoutElement>();
            le.flexibleWidth = 0;
            // 긴 문장은 줄바꿈(화면 밖으로 넘치거나 겹치지 않게)
            float maxW = (Mobile ? 720 : 900) - 90;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (t.preferredWidth > maxW) { t.horizontalOverflow = HorizontalWrapMode.Wrap; le.preferredWidth = maxW; }
            card.gameObject.AddComponent<CanvasGroup>();
            card.gameObject.AddComponent<ToastLife>();
            card.gameObject.AddComponent<PopIn>();
            while (toastRoot.childCount > 3) DestroyImmediate(toastRoot.GetChild(0).gameObject);
        }

        // ── 모달 창 ────────────────────────────────────────────
        void BuildModal()
        {
            modal = UIKit.Rect("Modal", frame);
            UIKit.Stretch(modal);
            var dim = UIKit.Box(modal, new Color(0.03f, 0.01f, 0.05f, 0.55f), "Dim");
            dim.raycastTarget = true;
            UIKit.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<PointerHandler>().onLeft = CloseModal;

            var win = UIKit.Panel(modal, "ui_window", "Window");
            win.raycastTarget = true;
            modalWindow = win.rectTransform;
            if (Mobile) UIKit.Anchor(modalWindow, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-16, -30));
            else
            {
                UIKit.Place(modalWindow, 0, 0, 1240, Mathf.Min(850f, RefRes.y - 130f), new Vector2(0.5f, 0.5f));
                modalWindow.anchoredPosition = new Vector2(CastleCenterX, -10);
            }
            win.gameObject.AddComponent<PopIn>();

            // 제목 리본 (창 위에 걸친다)
            var ribbon = UIKit.Panel(win.transform, "ui_title", "Title").rectTransform;
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1);
            ribbon.pivot = new Vector2(0.5f, 0.5f);
            ribbon.anchoredPosition = new Vector2(0, -2);
            var rhl = ribbon.gameObject.AddComponent<HorizontalLayoutGroup>();
            rhl.padding = new RectOffset(28, 34, 10, 14);
            rhl.spacing = 12;
            rhl.childAlignment = TextAnchor.MiddleCenter;
            rhl.childControlWidth = rhl.childControlHeight = true;
            rhl.childForceExpandWidth = rhl.childForceExpandHeight = false;
            var rfit = ribbon.gameObject.AddComponent<ContentSizeFitter>();
            rfit.horizontalFit = rfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            modalIcon = UIKit.Icon(ribbon, "ic_build", 48);
            modalTitle = UIKit.Label(ribbon, "", UIKit.TL, TextAnchor.MiddleCenter, UIKit.Gold);
            modalTitle.GetComponent<LayoutElement>().flexibleWidth = 0;
            modalTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            var close = UIKit.Button(win.transform, "", CloseModal, 56, 56, Btn.Danger, UIKit.TS, "ic_close");
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
            crt.anchoredPosition = new Vector2(10, 10);
            close.Tip(L.T("닫기 (ESC)", "Close (ESC)"));

            var view = UIKit.Rect("Viewport", win.transform);
            UIKit.Stretch(view, 34, 30, 60, 64);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.001f);

            modalContent = UIKit.Column(view, 8);
            modalContent.anchorMin = new Vector2(0, 1);
            modalContent.anchorMax = new Vector2(1, 1);
            modalContent.pivot = new Vector2(0.5f, 1);
            modalContent.offsetMin = modalContent.offsetMax = Vector2.zero;
            modalContent.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 10, 8, 16);
            modalContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            modalScroll = view.gameObject.AddComponent<ScrollRect>();
            modalScroll.content = modalContent;
            modalScroll.horizontal = false;
            modalScroll.viewport = view;
            modalScroll.scrollSensitivity = 50f;
            modalScroll.movementType = ScrollRect.MovementType.Clamped;

            // 도트 스크롤바
            var track = UIKit.Panel(win.transform, "ui_inset", "Scrollbar");
            track.raycastTarget = true;
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(1, 0); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(1, 0.5f);
            trt.offsetMin = new Vector2(-54, 34); trt.offsetMax = new Vector2(-30, -68);
            var slide = UIKit.Rect("Slide", trt);
            UIKit.Stretch(slide, 3, 3, 3, 3);
            var handle = UIKit.Panel(slide, "ui_handle", "Handle");
            handle.raycastTarget = true;
            UIKit.Stretch(handle.rectTransform);
            var sb = track.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle.rectTransform;
            sb.targetGraphic = handle;
            var nav = sb.navigation; nav.mode = Navigation.Mode.None; sb.navigation = nav;
            modalScroll.verticalScrollbar = sb;
            modalScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            modal.gameObject.SetActive(false);
        }

        public void OpenModal(string title, Action<RectTransform> builder, string icon = null)
        {
            castle.CancelPlacing();
            modalTitle.text = title;
            UIKit.Tint(modalIcon, Color.white);
            var pix = modalIcon.transform.Find("Pix");
            if (pix != null && icon != null)
            {
                var sp = Art.Get(icon);
                var img = pix.GetComponent<UnityEngine.UI.Image>();
                img.sprite = sp;
                if (sp != null)
                {
                    float scale = Mathf.Max(1f, Mathf.Floor(48f / Mathf.Max(sp.rect.width, sp.rect.height)));
                    ((RectTransform)pix).sizeDelta = sp.rect.size * scale;
                }
            }
            modalIcon.gameObject.SetActive(icon != null);
            modalBuilder = builder;
            bool wasOpen = modal.gameObject.activeSelf;
            modal.gameObject.SetActive(true);
            if (!wasOpen) modalWindow.GetComponent<PopIn>().enabled = true; // 창을 바꿀 때는 튀어나오지 않는다
            RebuildModal();
            modalScroll.verticalNormalizedPosition = 1f;
        }

        public void CloseModal()
        {
            modalBuilder = null;
            modal.gameObject.SetActive(false);
        }

        void RebuildModal()
        {
            if (modalBuilder == null) return;
            float scroll = modalScroll.verticalNormalizedPosition;
            float oldH = modalContent.rect.height;
            UIKit.Clear(modalContent);
            modalBuilder(modalContent);
            LayoutRebuilder.ForceRebuildLayoutImmediate(modalContent);
            // 높이가 같으면 스크롤 위치를 그대로 (위치가 튀지 않게)
            if (Mathf.Abs(modalContent.rect.height - oldH) < 1f) modalScroll.verticalNormalizedPosition = scroll;
            else
            {
                float viewH = ((RectTransform)modalScroll.viewport).rect.height;
                float y = (1f - scroll) * Mathf.Max(0, oldH - viewH);
                float range = Mathf.Max(1f, modalContent.rect.height - viewH);
                modalScroll.verticalNormalizedPosition = Mathf.Clamp01(1f - y / range);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (TutorialVisible) CloseTutorial();
                    else if (castle.placing != null) castle.CancelPlacing();
                    else if (modalBuilder != null) CloseModal();
                    else if (HuntVisible) HideHunt();
                }
                if (!TutorialVisible)
                    for (int i = 0; i < menus.Length; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) OpenMenu(i);
                if (kb.tabKey.wasPressedThisFrame && modalBuilder == null && !TutorialVisible) { if (HuntVisible) HideHunt(); else ShowHunt(); }
            }

            Tooltip.Tick();

            rebuildCooldown -= Time.deltaTime;
            bool pressing = (Pointer.current != null && Pointer.current.press.isPressed) || (Mouse.current != null && Mouse.current.rightButton.isPressed);
            if (needRebuild && rebuildCooldown <= 0 && !pressing)
            {
                needRebuild = false;
                rebuildCooldown = 0.3f;
                castle.Rebuild();
                RebuildSidebarIfNeeded();
                RebuildModal();
                RebuildHunt();
            }
        }

        int sidebarSig = -1;

        void RebuildSidebarIfNeeded()
        {
            int sig = g.S.hunters.Count * 100 + g.S.research.Count + g.S.goalStep * 100000; // 목표 단계가 넘어가면 높이가 바뀔 수 있다
            if (sig == sidebarSig) return;
            sidebarSig = sig;
            RebuildSidebar();
        }

        public void StartPlacing(BuildingType t)
        {
            CloseModal();
            HideHunt();
            castle.placing = t;
            Toast(t == BuildingType.Tank
                ? L.T("빈 칸을 탭하거나, 끌어서 여러 칸을 하나의 우리로 지으세요.", "Tap an empty cell, or drag to make one big cage.")
                : L.T("빈 칸을 탭하거나 끌어서 짓습니다.", "Tap or drag over empty cells to build."), "ic_build");
        }

        // 잠긴 층을 탭: 바로 다음 층이면 증축 창, 아니면 안내
        public void OnLockedCell(int floor, int x)
        {
            int wing = Game.WingOf(x);
            if (floor == g.FloorsOpen(wing)) { Sound.Play("open"); OpenExpand(wing); }
            else g.Fail(L.T($"먼저 {(wing == 0 ? "왼쪽" : "오른쪽")} {g.NextFloor(wing)}층을 증축하세요.", $"Expand {(wing == 0 ? "left" : "right")} floor {g.NextFloor(wing)} first."));
        }
    }

    // 토스트: 3초 뒤 사라진다
    public class ToastLife : MonoBehaviour
    {
        float t;
        CanvasGroup cg;
        void Awake() => cg = GetComponent<CanvasGroup>();
        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > 2.6f) cg.alpha = Mathf.Clamp01(1f - (t - 2.6f) / 0.4f);
            if (t > 3f) Destroy(gameObject);
        }
    }
}

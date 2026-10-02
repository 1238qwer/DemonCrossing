using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Mawang
{
    // 마왕성 단면도를 월드 2D 도트 스프라이트로 그린다. UI(Canvas)와 분리되어 있다.
    // 1아트픽셀 = 1/16 유닛. 방 1칸 = 160×96 px, 층 사이 바닥 6 px, 엘리베이터 32 px.
    // 가운데 엘리베이터 왼쪽·오른쪽에 날개가 있고, 증축한 층 + 바로 위 한 층(잠김)까지 그린다.
    public class CastleView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        const float PPU = 16f;
        public const float CellW = 160 / PPU, RoomH = 96 / PPU, SlabH = 6 / PPU, ElevW = 32 / PPU, PillarW = 8 / PPU;
        public const float FloorH = RoomH + SlabH;
        const float FootY = 4 / PPU; // 방 바닥에서 캐릭터 발 위치
        public static float TotalW => (GameData.TotalWidth + GameData.ElevatorCells) * CellW;
        float TotalH => visibleFloors * FloorH + SlabH;
        static float ElevLeft => GameData.WingWidth * CellW;

        // 정렬 순서
        const int OrderSky = -100, OrderShaft = -80, OrderSlab = -10;
        const int OrderRoom = 0, OrderMonster = 3, OrderRoomFront = 4, OrderPlate = 6, OrderLabel = 7;
        const int OrderAgent = 10, OrderAgentLabel = 12, OrderLock = 30, OrderGhost = 40, OrderFx = 50;

        static readonly string[] HeroLooks = { "hero_warrior", "hero_mage", "hero_archer", "hero_cleric" };

        GameUI ui;
        Game g;
        Camera cam;
        RectTransform area;
        SpriteRenderer sky, ghost;
        WorldText ghostLabel;
        Transform staticRoot, roomRoot, agentRoot;
        BoxCollider2D hitArea;
        readonly List<(BuildingState b, WorldText text)> statusLabels = new List<(BuildingState, WorldText)>();
        string signature;
        int visibleFloors = -1;

        public BuildingType? placing;
        public (Vector2Int a, Vector2Int b)? previewRect; // 에디터 촬영용 배치 미리보기

        readonly List<SpriteRenderer> visitorPool = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer sr, WorldText name)> staffPool = new List<(SpriteRenderer, WorldText)>();
        readonly Vector3[] corners = new Vector3[4];

        // 엘리베이터 순간이동 연출: 에이전트별 직전 상태
        class AgentView { public bool inElevator; public Vector2 world; }
        readonly Dictionary<Agent, AgentView> agentViews = new Dictionary<Agent, AgentView>();
        readonly HashSet<Agent> seenAgents = new HashSet<Agent>();
        bool agentsPrimed;

        public static Vector2 RoomOrigin(int floor, int x) => new Vector2(Game.SimX(x) * CellW, floor * FloorH + SlabH);

        // 시뮬레이션 좌표(칸, 층) → 월드
        public static Vector2 ToWorld(Vector2 sim) => new Vector2(sim.x * CellW, sim.y * FloorH + SlabH + FootY);

        public void Init(GameUI owner, RectTransform castleArea)
        {
            ui = owner;
            g = Game.I;
            area = castleArea;
            cam = Camera.main;
            cam.orthographic = true;
            if (cam.GetComponent<Physics2DRaycaster>() == null) cam.gameObject.AddComponent<Physics2DRaycaster>();

            // 성 전체를 덮는 클릭 영역 하나로 칸/건물을 판정한다
            hitArea = gameObject.AddComponent<BoxCollider2D>();

            sky = Art.Sprite(transform, "Sky", Art.Background("sky"), OrderSky, Vector2.zero);
            sky.color = new Color(0.42f, 0.36f, 0.5f); // 붉은 하늘을 어둡고 차분하게 (성에 시선이 가도록)
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(transform, false);
            roomRoot = new GameObject("Rooms").transform;
            roomRoot.SetParent(transform, false);
            agentRoot = new GameObject("Agents").transform;
            agentRoot.SetParent(transform, false);

            ghost = Art.Sprite(transform, "Ghost", Art.White, OrderGhost, Vector2.zero);
            ghost.gameObject.SetActive(false);
            ghostLabel = Art.Text(transform, "", Vector2.zero, 0.55f, Color.white, OrderGhost + 1);
            ghostLabel.gameObject.SetActive(false);

            FloatingText.Emitted += OnFloatingText;
            Rebuild(true);
            FitCamera();
        }

        void OnDestroy() => FloatingText.Emitted -= OnFloatingText;

        // ── 고정 요소: 외벽, 바닥, 엘리베이터, 잠긴 층 ─────────
        void BuildStatic()
        {
            for (int i = staticRoot.childCount - 1; i >= 0; i--) Destroy(staticRoot.GetChild(i).gameObject);
            var root = staticRoot;
            float H = TotalH;
            hitArea.offset = new Vector2(TotalW / 2f, H / 2f);
            hitArea.size = new Vector2(TotalW, H);

            float ex = ElevLeft;
            var shaft = Art.Sprite(root, "Shaft", Art.White, OrderShaft, new Vector2(ex + ElevW / 2f, H / 2f));
            shaft.color = new Color(0.08f, 0.06f, 0.1f);
            shaft.transform.localScale = new Vector3(ElevW, H, 1);
            foreach (float rx in new[] { ex + 0.3f, ex + ElevW - 0.3f })
            {
                var rail = Art.Sprite(root, "Rail", Art.White, OrderShaft + 1, new Vector2(rx, H / 2f));
                rail.color = new Color(0.25f, 0.22f, 0.3f);
                rail.transform.localScale = new Vector3(1 / PPU, H, 1);
            }

            for (int f = 0; f <= visibleFloors; f++)
            {
                var slab = Art.Sprite(root, "Slab", Art.Get("slab"), OrderSlab, new Vector2(TotalW / 2f, f * FloorH + SlabH / 2f));
                slab.drawMode = SpriteDrawMode.Tiled;
                slab.size = new Vector2(TotalW + PillarW * 2, SlabH);
            }

            for (int f = 0; f < visibleFloors; f++)
            {
                float y0 = f * FloorH + SlabH;
                Art.Sprite(root, "ElevatorDoor", Art.Get("elevator"), OrderShaft + 2, new Vector2(ex + ElevW / 2f, y0));
                Art.Text(root, $"{f + 1}F", new Vector2(ex + ElevW / 2f, y0 + RoomH - 0.45f), 0.5f, new Color(1f, 0.85f, 0.5f), OrderLabel);
                for (int wing = 0; wing < 2; wing++)
                    if (f >= g.FloorsOpen(wing)) LockCover(root, f, wing);
            }

            // 좌우 외벽 기둥과 지붕 성가퀴
            foreach (float px in new[] { -PillarW / 2f, TotalW + PillarW / 2f })
            {
                var p = Art.Sprite(root, "Pillar", Art.Get("pillar"), OrderSlab, new Vector2(px, H / 2f));
                p.drawMode = SpriteDrawMode.Tiled;
                p.size = new Vector2(PillarW, H);
            }
            var top = Art.Sprite(root, "Battlement", Art.Get("battlement"), OrderSlab, new Vector2(TotalW / 2f, H));
            top.drawMode = SpriteDrawMode.Tiled;
            top.size = new Vector2(TotalW + PillarW * 2, 8 / PPU);
        }

        // 잠긴 층: 벽돌로 막아 둔다. 바로 다음 층이면 증축 안내, 그 위는 '잠김'
        void LockCover(Transform root, int floor, int wing)
        {
            float wx = wing == 0 ? 0f : ElevLeft + ElevW;
            float ww = GameData.WingWidth * CellW;
            var lockRoot = new GameObject($"Lock{wing}_{floor + 1}").transform;
            lockRoot.SetParent(root, false);
            lockRoot.localPosition = new Vector3(wx + ww / 2f, floor * FloorH + SlabH + RoomH / 2f, 0);
            bool next = floor == g.FloorsOpen(wing);
            for (int i = 0; i < GameData.WingWidth; i++)
            {
                var brick = Art.Sprite(lockRoot, "Sealed", Art.Get("room_sealed"), OrderLock, new Vector2(-ww / 2f + i * CellW, -RoomH / 2f));
                brick.color = next ? Color.white : new Color(0.72f, 0.7f, 0.76f); // 다음 층만 조금 밝게
            }
            var plate = Art.Sprite(lockRoot, "Plate", Art.White, OrderLock + 1, Vector2.zero);
            plate.color = new Color(0f, 0f, 0f, next ? 0.6f : 0.4f);
            plate.transform.localScale = next ? new Vector3(CellW * 2.2f, 2.3f, 1) : new Vector3(CellW * 0.7f, 1.1f, 1);
            if (next)
            {
                int n = floor + 1;
                Art.Text(lockRoot, L.T($"탭하여 {n}층 증축", $"Tap to build floor {n}"), new Vector2(0, 0.55f), 0.8f, UIKit.Gold, OrderLock + 2);
                Art.Text(lockRoot, L.T($"{GameData.ExpandGold(n):N0}G · 자재 {GameData.ExpandMat(n):N0}",
                                       $"{GameData.ExpandGold(n):N0}G · {GameData.ExpandMat(n):N0} Mat"),
                         new Vector2(0, -0.6f), 0.5f, new Color(0.85f, 0.8f, 0.9f), OrderLock + 2);
            }
            else Art.Text(lockRoot, L.T("잠김", "Locked"), Vector2.zero, 0.6f, new Color(0.75f, 0.7f, 0.8f), OrderLock + 2);
        }

        // ── 카메라: 확대 단계(프리셋) + 끌어서 이동 ────────────
        // 단계: 전체 보기 → 가로 꽉 채우기 → 1.5배 → 2배(기본: 한쪽 날개가 화면에 꽉 찬다) → 3배. 핀치/휠/버튼으로 한 단계씩.
        static (float mul, string label)[] ZoomSteps => new[] { (0f, L.T("전체", "All")), (1f, L.T("꽉 채움", "Fit")), (1.5f, "×1.5"), (2f, "×2"), (3f, "×3") };
        const int DefaultZoom = 3;
        int zoomStep = DefaultZoom;        // 원하는 단계(ZoomSteps 기준). 화면에 따라 합쳐진 단계로 바꿔 쓴다
        float upp;                 // 화면 픽셀당 월드 유닛 (목표값으로 부드럽게)
        Vector2 viewCenter;        // 성 영역 한가운데에 보이는 월드 좌표
        bool camInit;
        Vector2? zoomAnchorScreen; // 확대/축소 중 고정할 화면 점(손가락 사이, 마우스 위치)
        Vector2 zoomAnchorWorld;
        readonly List<float> levels = new List<float>();
        readonly List<string> levelNames = new List<string>();
        readonly int[] stepToLevel = new int[5];

        Rect Content => new Rect(-PillarW - 0.25f, -0.25f, TotalW + PillarW * 2 + 0.5f, TotalH + 8 / PPU + 0.5f);

        int Eff => Mathf.Clamp(stepToLevel[zoomStep], 0, Mathf.Max(0, levels.Count - 1)); // 지금 화면에서 실제로 쓰는 단계
        public string ZoomLabel => levelNames.Count > 0 ? levelNames[Eff] : "";
        public bool CanZoomIn => Eff < levels.Count - 1;
        public bool CanZoomOut => Eff > 0;

        bool AreaScreenRect(out Rect r)
        {
            area.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            r = new Rect(min, max - min);
            return r.width >= 10 && r.height >= 10;
        }

        // 화면 크기에 맞춘 단계별 배율. 서로 거의 같은 단계는 합친다(한 번 눌러도 변화 없는 단계 방지).
        void BuildLevels(Rect ar)
        {
            levels.Clear(); levelNames.Clear();
            var c = Content;
            float uppAll = Mathf.Max(c.width / ar.width, c.height / ar.height);
            float uppW = c.width / ar.width;
            var steps = ZoomSteps;
            for (int i = 0; i < steps.Length; i++)
            {
                var (mul, label) = steps[i];
                float u = mul <= 0 ? uppAll : Mathf.Min(uppAll, uppW / mul);
                if (levels.Count > 0 && u > levels[levels.Count - 1] * 0.95f) levelNames[levelNames.Count - 1] = label; // 거의 같은 단계는 합친다
                else { levels.Add(u); levelNames.Add(label); }
                stepToLevel[i] = levels.Count - 1;
            }
        }

        public void ZoomStep(int dir, Vector2? screenAnchor = null)
        {
            if (!AreaScreenRect(out var ar)) return;
            BuildLevels(ar);
            int next = Mathf.Clamp(Eff + dir, 0, levels.Count - 1);
            if (next == Eff) return;
            zoomStep = System.Array.IndexOf(stepToLevel, next);
            var a = screenAnchor ?? ar.center;
            zoomAnchorScreen = a;
            zoomAnchorWorld = viewCenter + (a - ar.center) * upp;
        }

        void FitCamera()
        {
            if (!AreaScreenRect(out var ar)) return;
            BuildLevels(ar);
            float target = levels[Eff]; // 첫 프레임처럼 단계가 적게 계산돼도 원하는 단계는 잊지 않는다
            var c = Content;
            if (!camInit)
            {
                // 처음엔 1층, 왼쪽 날개부터 보이게
                camInit = true; upp = target;
                viewCenter = new Vector2(ElevLeft / 2f + ElevW, c.yMin + ar.height * target / 2f);
            }

            upp = Mathf.Abs(upp - target) < target * 0.002f ? target : Mathf.Lerp(upp, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            if (zoomAnchorScreen != null)
            {
                viewCenter = zoomAnchorWorld - (zoomAnchorScreen.Value - ar.center) * upp;
                if (upp == target) zoomAnchorScreen = null;
            }

            // 성 밖으로 너무 나가지 않게. 화면보다 작은 축은 가운데 정렬.
            Vector2 view = ar.size * upp;
            viewCenter.x = view.x >= c.width ? c.center.x : Mathf.Clamp(viewCenter.x, c.xMin + view.x / 2f, c.xMax - view.x / 2f);
            viewCenter.y = view.y >= c.height ? c.center.y : Mathf.Clamp(viewCenter.y, c.yMin + view.y / 2f, c.yMax - view.y / 2f);

            cam.orthographicSize = Screen.height * upp / 2f;
            var p = viewCenter - (ar.center - new Vector2(Screen.width, Screen.height) / 2f) * upp;
            p = new Vector2(Mathf.Round(p.x / upp) * upp, Mathf.Round(p.y / upp) * upp); // 화면 픽셀 단위로 스냅(떨림 방지)
            cam.transform.position = new Vector3(p.x, p.y, -10f);

            if (sky.sprite != null)
            {
                float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
                var sz = sky.sprite.bounds.size;
                float sc = Mathf.Max(viewW / sz.x, viewH / sz.y);
                sky.transform.position = new Vector3(p.x, p.y, 0);
                sky.transform.localScale = new Vector3(sc, sc, 1);
            }
        }

        // ── 끌어서 이동 · 핀치 · 휠 ────────────────────────────
        const float DragThreshold = 20f, PinchStep = 1.25f;
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        // 그 위치에 UI(사이드바, 버튼 등)가 있으면 성 끌기·확대를 하지 않는다
        static bool OverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, uiHits);
            foreach (var h in uiHits) if (h.module is UnityEngine.UI.GraphicRaycaster) return true;
            return false;
        }
        bool pressing, dragging, suppressClick, pinching;
        Vector2 dragStart, lastPointer;
        float pinchBase;

        void UpdatePanZoom()
        {
            if (ui.BlocksWorld || !AreaScreenRect(out var ar)) { pressing = dragging = pinching = false; return; }

            // 두 손가락: 핀치로 한 단계씩
            var ts = Touchscreen.current;
            int touches = 0;
            Vector2 t0 = default, t1 = default;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (touches == 0) t0 = t.position.ReadValue(); else if (touches == 1) t1 = t.position.ReadValue();
                    touches++;
                }
            if (touches >= 2)
            {
                float d = Vector2.Distance(t0, t1);
                var mid = (t0 + t1) / 2f;
                if (!pinching) { if (OverUI(mid)) return; pinching = true; pinchBase = d; suppressClick = true; pressTime = -1f; placeDrag = false; }
                else if (d > pinchBase * PinchStep) { ZoomStep(+1, mid); pinchBase = d; }
                else if (d < pinchBase / PinchStep) { ZoomStep(-1, mid); pinchBase = d; }
                lastPointer = t0;
                return;
            }
            pinching = false;

            // 마우스 휠
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                var mp = mouse.position.ReadValue();
                if (Mathf.Abs(wheel) > 0.01f && ar.Contains(mp) && !OverUI(mp)) ZoomStep(wheel > 0 ? 1 : -1, mp);
            }

            // 한 손가락 / 마우스 왼쪽: 끌어서 이동 (배치 중에는 끌기 = 배치 영역 선택)
            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();
            bool down = pointer.press.isPressed;
            if (down && !pressing)
            {
                pressing = ar.Contains(pos) && !OverUI(pos) && !placeDrag;
                dragging = false;
                dragStart = lastPointer = pos;
                if (pressing) suppressClick = false;
            }
            else if (!down) { pressing = false; dragging = false; }

            if (!pressing || placeDrag) return;
            if (!dragging && (pos - dragStart).sqrMagnitude > DragThreshold * DragThreshold)
            {
                dragging = true;
                suppressClick = true;   // 끌었다 놓은 것은 클릭이 아니다
                pressTime = -1f;        // 길게 누르기도 취소
            }
            if (dragging)
            {
                viewCenter -= (pos - lastPointer) * upp;
                zoomAnchorScreen = null;
            }
            lastPointer = pos;
        }

        // ── 입력 ───────────────────────────────────────────────
        // 화면 → 칸. 보이는 층(잠긴 층 포함)이면 true. 엘리베이터 위는 false.
        bool ScreenToCell(Vector2 screen, out int floor, out int x)
        {
            Vector2 w = cam.ScreenToWorldPoint(screen);
            floor = Mathf.FloorToInt((w.y - SlabH) / FloorH);
            x = -1;
            if (w.x >= 0 && w.x < ElevLeft) x = Mathf.FloorToInt(w.x / CellW);
            else if (w.x >= ElevLeft + ElevW && w.x < TotalW) x = GameData.WingWidth + Mathf.FloorToInt((w.x - ElevLeft - ElevW) / CellW);
            x = Mathf.Min(x, GameData.TotalWidth - 1);
            return floor >= 0 && floor < visibleFloors && x >= 0;
        }

        // 끄는 중인 칸: 시작한 날개·보이는 층 안으로 제한
        void DragCell(Vector2 screen, out int floor, out int x)
        {
            Vector2 w = cam.ScreenToWorldPoint(screen);
            floor = Mathf.Clamp(Mathf.FloorToInt((w.y - SlabH) / FloorH), 0, visibleFloors - 1);
            int wing = Game.WingOf(dragA.x);
            float wx = wing == 0 ? w.x : w.x - ElevLeft - ElevW;
            x = Mathf.Clamp(Mathf.FloorToInt(wx / CellW), 0, GameData.WingWidth - 1) + wing * GameData.WingWidth;
        }

        // 길게 누르기 = 우클릭 (폰에서 건물 관리 창 열기 / 배치 취소)
        const float LongPress = 0.5f;
        float pressTime = -1f;
        Vector2 pressPos;
        bool longFired;
        bool placeDrag;
        Vector2Int dragA, dragB;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressTime = Time.unscaledTime;
            pressPos = e.position;
            longFired = false;
            if (placing != null && ScreenToCell(e.position, out int f, out int x))
            {
                placeDrag = true;
                dragA = dragB = new Vector2Int(x, f);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressTime = -1f;
            if (e.button != PointerEventData.InputButton.Left || !placeDrag) return;
            placeDrag = false;
            suppressClick = true; // 배치로 끝난 누르기가 방금 지은 방을 여는 클릭이 되지 않게
            if (!longFired && placing != null) PlaceRect();
        }

        void Update()
        {
            UpdatePanZoom();
            if (placeDrag && Pointer.current != null)
            {
                DragCell(Pointer.current.position.ReadValue(), out int f, out int x);
                var b = new Vector2Int(x, f);
                if (b != dragA) pressTime = -1f; // 끌기 시작하면 길게 누르기 취소
                dragB = b;
            }
            if (pressTime < 0 || longFired || Pointer.current == null) return;
            if ((Pointer.current.position.ReadValue() - pressPos).sqrMagnitude > 30f * 30f) { pressTime = -1f; return; } // 끌면 취소
            if (Time.unscaledTime - pressTime < LongPress) return;
            longFired = true;
            if (placing != null) { placeDrag = false; CancelPlacing(); return; }
            HandleClick(pressPos, false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (placing != null) { if (e.button == PointerEventData.InputButton.Right) CancelPlacing(); return; } // 왼쪽은 손을 뗄 때 배치
            if (longFired) { longFired = false; return; } // 길게 누른 뒤 손을 뗀 것은 클릭이 아니다
            if (suppressClick) { suppressClick = false; return; } // 끌기·핀치 뒤 손을 뗀 것도 클릭이 아니다
            HandleClick(e.position, e.button == PointerEventData.InputButton.Left);
        }

        // 짧게(좌클릭): 수령 / 판매 / 빈 칸 건설 · 길게(우클릭): 관리 창
        void HandleClick(Vector2 screen, bool left)
        {
            if (!ScreenToCell(screen, out int floor, out int x)) return;
            if (!g.CellOpen(floor, x)) { ui.OnLockedCell(floor, x); return; }
            var b = g.BuildingAt(floor, x);
            if (b == null) { ui.OpenBuildAt(floor, x); return; }
            if (left) OnBuildingTap(b);
            else { Sound.Play("open"); ui.OpenBuilding(b); }
        }

        void Info(BuildingState b, string text) => OnFloatingText(new Vector2(CastleSim.Center(b), b.floor + b.ch - 1), text, new Color(0.85f, 0.8f, 0.95f));

        void OnBuildingTap(BuildingState b)
        {
            var at = new Vector2(CastleSim.Center(b), b.floor + b.ch - 1);
            switch (b.type)
            {
                case BuildingType.Tank:
                    // 원작: 수족관을 좌클릭해 관람료 수령
                    if (b.contents.Count == 0) { ui.OpenBuilding(b); return; } // 빈 우리는 괴물을 넣도록 바로 연다
                    if (b.accumulated >= 1f)
                    {
                        int amt = g.CollectTank(b, 0f);
                        if (amt > 0) { Sound.Play("coin"); OnFloatingText(at, $"+{amt:N0}"); }
                        else Info(b, GoldFullText);
                    }
                    else Info(b, L.T("쌓인 관람료 없음", "Nothing to collect"));
                    return;
                case BuildingType.Restaurant:
                case BuildingType.Souvenir:
                {
                    int sold = g.Sim.ServeAll(b);
                    if (sold == 0) Info(b, g.GoldFull ? GoldFullText : ShopStock(b) == 0 ? L.T("재고 없음! (길게: 관리)", "Out of stock! (hold: manage)") : L.T("기다리는 손님 없음", "No one waiting"));
                    return;
                }
                case BuildingType.Lab:
                    if (b.labRp > 0)
                    {
                        int rp = g.CollectLab(b);
                        if (rp > 0) { Sound.Play("gem"); OnFloatingText(at, L.T($"+{rp} 보석", $"+{rp} Gems"), UIKit.RpText); }
                        else Info(b, L.T("보석 최대 보유량", "Gems at max"));
                    }
                    else Info(b, L.T($"다음 보석 {g.LabInterval(b) - b.labTimer:0}초", $"Next gem in {g.LabInterval(b) - b.labTimer:0}s"));
                    return;
            }
            Sound.Play("open");
            ui.OpenBuilding(b);
        }

        static string GoldFullText => L.T("골드 최대 보유량", "Gold at max");

        int ShopStock(BuildingState b)
        {
            int stock = 0;
            foreach (var it in GameData.ShopItems)
                if (it.shop == b.type && it.tier <= b.level) stock += CountList.Get(g.S.goods, it.id);
            return stock;
        }

        // 끌어서 고른 영역에 짓는다. 전시 우리 = 하나로 이어진 우리, 다른 건물 = 빈 칸마다 하나씩.
        void PlaceRect()
        {
            var type = placing.Value;
            var def = GameData.Buildings[type];
            int x0 = Mathf.Min(dragA.x, dragB.x), x1 = Mathf.Max(dragA.x, dragB.x);
            int f0 = Mathf.Min(dragA.y, dragB.y), f1 = Mathf.Max(dragA.y, dragB.y);
            int w = x1 - x0 + 1, h = f1 - f0 + 1;
            if (type == BuildingType.Tank || w * h == 1)
            {
                if (!g.CellOpen(f0, x0) && w * h == 1) { ui.OnLockedCell(f0, x0); return; }
                if (!g.TryBuild(type, f0, x0, w, h)) return;
            }
            else
            {
                int built = 0;
                for (int f = f0; f <= f1; f++)
                    for (int x = x0; x <= x1; x++)
                        if (g.BuildBlock(type, f, x) == null && g.TryBuild(type, f, x, 1, 1, true)) built++;
                if (built == 0) { g.Fail(L.T("지을 수 있는 빈 칸이 없습니다.", "No free cells to build on.")); return; }
            }
            bool limitReached = def.maxCount > 0 && g.CountOf(type) >= def.maxCount;
            if (limitReached || !g.CanAfford(def.costGold, def.costMat)) CancelPlacing();
        }

        public void CancelPlacing()
        {
            placing = null;
            placeDrag = false;
            if (ghost != null) { ghost.gameObject.SetActive(false); ghostLabel.gameObject.SetActive(false); }
        }

        // ── 방 ─────────────────────────────────────────────────
        public void Rebuild(bool force = false)
        {
            int vf = GameData.MaxFloors; // 지을 수 있는 모든 층을 그린다(잠긴 층은 벽돌)
            var sb = new StringBuilder();
            sb.Append(g.S.floorsLeft).Append(',').Append(g.S.floorsRight).Append('|');
            foreach (var b in g.S.buildings)
            {
                sb.Append(b.uid).Append(',').Append(b.level).Append(',').Append(b.floor).Append(',').Append(b.x).Append(';');
                foreach (var e in b.contents) sb.Append(e.id).Append(e.count);
            }
            var sig = sb.ToString();
            if (!force && sig == signature) return;
            signature = sig;
            visibleFloors = vf;
            if (force) floorsSig = null;
            BuildStaticIfFloorsChanged();

            for (int i = roomRoot.childCount - 1; i >= 0; i--) Destroy(roomRoot.GetChild(i).gameObject);
            statusLabels.Clear();
            foreach (var b in g.S.buildings) CreateRoom(b);
            for (int f = 0; f < visibleFloors; f++)
                for (int x = 0; x < GameData.TotalWidth; x++)
                {
                    if (!g.CellOpen(f, x) || g.BuildingAt(f, x) != null) continue;
                    var o = RoomOrigin(f, x);
                    Art.Sprite(roomRoot, "Empty", Art.Get("room_empty"), OrderRoom, o);
                    // 빈 칸: 탭하면 건설 (+ 표시)
                    Art.Text(roomRoot, "+", o + new Vector2(CellW / 2f, RoomH / 2f), 1.4f, new Color(1f, 1f, 1f, 0.22f), OrderLabel);
                }
        }

        string floorsSig;
        void BuildStaticIfFloorsChanged()
        {
            var s = $"{g.S.floorsLeft},{g.S.floorsRight},{visibleFloors}";
            if (s == floorsSig) return;
            floorsSig = s;
            BuildStatic();
        }

        // 레벨마다 내부 인테리어가 조금씩 고급스러워진다: room_x (Lv.1), room_x_2 … (RoomPainter)
        static string RoomSprite(BuildingState b)
        {
            string name = GameData.Buildings[b.type].icon;
            return b.level > 1 ? $"{name}_{b.level}" : name;
        }

        void CreateRoom(BuildingState b)
        {
            var def = GameData.Buildings[b.type];
            var root = new GameObject(def.name).transform;
            root.SetParent(roomRoot, false);
            root.localPosition = RoomOrigin(b.floor, b.x);
            float W = b.cw * CellW, H = b.ch * FloorH - SlabH;

            for (int r = 0; r < b.ch; r++)
                for (int c = 0; c < b.cw; c++)
                    Art.Sprite(root, "Room", Art.Get(RoomSprite(b)), OrderRoom, new Vector2(c * CellW, r * FloorH));

            if (b.type == BuildingType.Tank)
            {
                float minX = 1f, maxX = W - 1f;
                int k = 0;
                foreach (var e in b.contents)
                {
                    var m = GameData.MonsterById[e.id];
                    for (int i = 0; i < e.count; i++, k++)
                    {
                        float y = FootY + (k % b.ch) * FloorH; // 여러 층 우리는 층마다 나눠 둔다
                        var sr = Art.Sprite(root, m.name, Art.Get(m.id), OrderMonster, new Vector2(Random.Range(minX, maxX), y));
                        sr.gameObject.AddComponent<MonsterWander>().Init(minX, maxX, y, m.isPlant, m.id);
                    }
                }
                for (int r = 0; r < b.ch; r++)
                    for (int c = 0; c < b.cw; c++)
                        Art.Sprite(root, "Bars", Art.Get("room_tank_front"), OrderRoomFront, new Vector2(c * CellW, r * FloorH));
                if (b.Cells > 1) Outline(root, W, H, new Color(1f, 0.8f, 0.35f, 0.9f)); // 하나로 이어진 우리
            }
            if (b.type == BuildingType.Arena)
            {
                // 출전 괴물: 모래 위에 서서 찾아온 용사와 대련한다
                var mon = Art.Sprite(root, "Champion", null, OrderAgent, new Vector2(GameData.ArenaMonsterX * CellW, FootY));
                mon.gameObject.AddComponent<ArenaFighter>().Init(b, OrderFx);
            }

            // 이름표: 맨 윗줄 위쪽 반투명 판 + 이름 + 상태
            float top = (b.ch - 1) * FloorH;
            var plate = Art.Sprite(root, "Plate", Art.White, OrderPlate, new Vector2(W / 2f, top + RoomH - 0.95f));
            plate.color = new Color(0f, 0f, 0f, 0.55f);
            plate.transform.localScale = new Vector3(Mathf.Min(W - 0.5f, CellW * 1.6f), 1.3f, 1);
            string size = b.Cells > 1 ? $" ({b.cw}×{b.ch})" : "";
            Art.Text(root, $"{def.Name} Lv.{b.level}{size}", new Vector2(W / 2f, top + RoomH - 0.55f), 0.46f, Color.white, OrderLabel);
            var status = Art.Text(root, "", new Vector2(W / 2f, top + RoomH - 1.25f), 0.36f, new Color(1f, 0.9f, 0.6f), OrderLabel);
            statusLabels.Add((b, status));
        }

        static void Outline(Transform root, float w, float h, Color c)
        {
            float t = 2f / PPU;
            void Bar(Vector2 center, Vector2 size)
            {
                var sr = Art.Sprite(root, "Edge", Art.White, OrderRoomFront + 1, center);
                sr.color = c;
                sr.transform.localScale = new Vector3(size.x, size.y, 1);
            }
            Bar(new Vector2(w / 2f, t / 2f), new Vector2(w, t));
            Bar(new Vector2(w / 2f, h - t / 2f), new Vector2(w, t));
            Bar(new Vector2(t / 2f, h / 2f), new Vector2(t, h));
            Bar(new Vector2(w - t / 2f, h / 2f), new Vector2(t, h));
        }

        string StatusText(BuildingState b)
        {
            switch (b.type)
            {
                case BuildingType.Tank:
                    return L.T($"관람료 {Mathf.FloorToInt(b.accumulated):N0}/{g.TankCap(b):N0} · {g.TankRatePerMin(b):N0}/분 · 공간 {g.TankSpaceUsed(b)}/{g.TankSpaceMax(b)}",
                               $"Fees {Mathf.FloorToInt(b.accumulated):N0}/{g.TankCap(b):N0} · {g.TankRatePerMin(b):N0}/min · Space {g.TankSpaceUsed(b)}/{g.TankSpaceMax(b)}");
                case BuildingType.Restaurant:
                case BuildingType.Souvenir:
                {
                    int stock = ShopStock(b);
                    int wait = g.Sim.WaitingCustomers(b);
                    if (stock == 0) return L.T("재고 없음!", "Out of stock!");
                    return wait > 0 ? L.T($"재고 {stock} · 손님 {wait}명 대기 (탭: 판매)", $"Stock {stock} · {wait} waiting (tap: sell)") : L.T($"재고 {stock}", $"Stock {stock}");
                }
                case BuildingType.Lab:
                    return L.T($"보석 {b.labRp}/{g.LabRpCap} · 다음 {g.LabInterval(b) - b.labTimer:0}초", $"Gems {b.labRp}/{g.LabRpCap} · next {g.LabInterval(b) - b.labTimer:0}s");
                case BuildingType.Dorm:
                    return L.T($"직원 {g.StaffCount}/{g.StaffCapacity()}", $"Staff {g.StaffCount}/{g.StaffCapacity()}");
                case BuildingType.Rest:
                    return L.T($"매일 06시 +{GameData.RestDailyGold(g.TotalFloorsOpen):N0}G", $"Daily 06:00 +{GameData.RestDailyGold(g.TotalFloorsOpen):N0}G");
                case BuildingType.Arena:
                {
                    var champ = g.ArenaChampion();
                    if (champ == null) return L.T("출전 괴물 없음!", "No fighters!");
                    return L.T($"출전 {champ.Name} · 전투력 {champ.power:N0} · 보상 ×{g.ArenaLevelMul(b):0.##}", $"{champ.Name} · Power {champ.power:N0} · Reward ×{g.ArenaLevelMul(b):0.##}");
                }
            }
            return "";
        }

        // ── 매 프레임 ──────────────────────────────────────────
        void LateUpdate()
        {
            FitCamera();
            foreach (var (b, text) in statusLabels) text.Set(StatusText(b));
            UpdateGhost();
            seenAgents.Clear();
            UpdateVisitors();
            UpdateStaff();
            CleanupAgents();
        }

        // 엘리베이터에 타면 사라지고, 도착 층에서 나타난다. 처음 나타날 때·떠날 때도 같은 연출.
        void TrackAgent(Agent a, SpriteRenderer sr, WorldText label)
        {
            seenAgents.Add(a);
            bool inEl = a.InElevator;
            var w = ToWorld(a.pos);
            if (!agentViews.TryGetValue(a, out var st))
            {
                agentViews[a] = st = new AgentView { inElevator = inEl, world = w };
                if (agentsPrimed && !inEl) Teleport(w, sr.transform.localScale.y);
            }
            else if (st.inElevator != inEl)
            {
                Teleport(inEl ? st.world : w, sr.transform.localScale.y);
                st.inElevator = inEl;
            }
            st.world = w;
            sr.enabled = !inEl;
            if (label != null && label.gameObject.activeSelf == inEl) label.gameObject.SetActive(!inEl);
        }

        void CleanupAgents()
        {
            List<Agent> gone = null;
            foreach (var kv in agentViews)
                if (!seenAgents.Contains(kv.Key)) (gone ??= new List<Agent>()).Add(kv.Key);
            if (gone != null)
                foreach (var a in gone)
                {
                    if (!agentViews[a].inElevator) Teleport(agentViews[a].world, 1f);
                    agentViews.Remove(a);
                }
            agentsPrimed = true;
        }

        void Teleport(Vector2 feet, float size)
        {
            var go = new GameObject("Teleport");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = feet;
            go.AddComponent<TeleportFx>().Init(OrderFx, size);
        }

        // 배치 미리보기: 끄는 중이면 영역 전체, 아니면 가리키는 칸
        void UpdateGhost()
        {
            if (placing == null || (Pointer.current == null && previewRect == null)) { ghost.gameObject.SetActive(false); ghostLabel.gameObject.SetActive(false); return; }
            var type = placing.Value;
            int x0, x1, f0, f1;
            if (previewRect != null) // 촬영용: 끌기 미리보기를 고정
            {
                var (a, c) = previewRect.Value;
                x0 = Mathf.Min(a.x, c.x); x1 = Mathf.Max(a.x, c.x); f0 = Mathf.Min(a.y, c.y); f1 = Mathf.Max(a.y, c.y);
            }
            else if (placeDrag)
            {
                x0 = Mathf.Min(dragA.x, dragB.x); x1 = Mathf.Max(dragA.x, dragB.x);
                f0 = Mathf.Min(dragA.y, dragB.y); f1 = Mathf.Max(dragA.y, dragB.y);
            }
            else
            {
                if (GameUI.Mobile || !ScreenToCell(Pointer.current.position.ReadValue(), out int f, out int x)) { ghost.gameObject.SetActive(false); ghostLabel.gameObject.SetActive(false); return; }
                x0 = x1 = x; f0 = f1 = f;
            }
            int w = x1 - x0 + 1, h = f1 - f0 + 1;
            bool ok;
            string label;
            if (type == BuildingType.Tank || w * h == 1)
            {
                int cells = type == BuildingType.Tank ? w * h : 1;
                ok = g.BuildBlock(type, f0, x0, type == BuildingType.Tank ? w : 1, type == BuildingType.Tank ? h : 1) == null;
                label = (cells > 1 ? $"{w}×{h}  " : "") + $"{Game.BuildGold(type, cells):N0}G · {Game.BuildMat(type, cells):N0}" + L.T(" 자재", " Mat");
            }
            else
            {
                int free = 0;
                for (int f = f0; f <= f1; f++) for (int x = x0; x <= x1; x++) if (g.IsAreaFree(f, x, 1, 1)) free++;
                ok = free > 0;
                label = L.T($"빈 칸 {free}곳에 짓기", $"Build on {free} free cells");
            }
            var o = RoomOrigin(f0, x0);
            float gw = w * CellW, gh = h * FloorH - SlabH;
            ghost.gameObject.SetActive(true);
            ghost.color = ok ? new Color(0.4f, 1f, 0.4f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);
            ghost.transform.localPosition = o + new Vector2(gw / 2f, gh / 2f);
            ghost.transform.localScale = new Vector3(gw, gh, 1);
            ghostLabel.gameObject.SetActive(true);
            ghostLabel.transform.localPosition = o + new Vector2(gw / 2f, gh / 2f);
            ghostLabel.Set(label);
        }

        void UpdateVisitors()
        {
            var list = g.Sim.visitors;
            while (visitorPool.Count < list.Count)
            {
                var sr = Art.Sprite(agentRoot, "Hero", null, OrderAgent, Vector2.zero);
                sr.gameObject.AddComponent<CharacterMotion>();
                visitorPool.Add(sr);
            }
            var cs = CharacterSettings.I;
            for (int i = 0; i < visitorPool.Count; i++)
            {
                var sr = visitorPool[i];
                bool on = i < list.Count;
                sr.gameObject.SetActive(on);
                if (!on) continue;
                var v = list[i];
                string look = HeroLooks[v.look % HeroLooks.Length];
                sr.sprite = Art.Get(look);
                float vs = cs.castleVisitorScale * cs.Scale(look);
                sr.transform.localScale = new Vector3(vs, vs, 1);
                sr.color = v.state == VisitorState.WaitingService ? Color.Lerp(Color.white, new Color(1f, 0.4f, 0.4f), 1f - v.timer / Mathf.Max(1f, v.patience)) : Color.white;
                sr.GetComponent<CharacterMotion>().MoveTo(ToWorld(v.pos));
                if (v.state == VisitorState.Sparring)
                {
                    // 투기장 대련: 오른쪽 괴물을 향해 휘두르고, 맞으면 붉게 번쩍이며 밀려난다
                    float p = ArenaFighter.Progress(v);
                    sr.flipX = false;
                    sr.transform.localPosition += new Vector3(ArenaFighter.Snap(ArenaFighter.HeroOffset(p) * CellW), ArenaFighter.Snap(ArenaFighter.Hop(p, true)), 0);
                    sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), ArenaFighter.HitFlash(p, false));
                }
                TrackAgent(v, sr, null);
            }
        }

        void UpdateStaff()
        {
            var list = g.Sim.staff;
            while (staffPool.Count < list.Count)
            {
                var sr = Art.Sprite(agentRoot, "Keeper", null, OrderAgent + 1, Vector2.zero);
                sr.gameObject.AddComponent<CharacterMotion>();
                var name = Art.Text(sr.transform, "", new Vector2(0, 1.05f), 0.3f, new Color(1f, 0.75f, 0.85f), OrderAgentLabel);
                staffPool.Add((sr, name));
            }
            for (int i = 0; i < staffPool.Count; i++)
            {
                var (sr, name) = staffPool[i];
                bool on = i < list.Count;
                sr.gameObject.SetActive(on);
                if (!on) continue;
                var s = list[i];
                sr.sprite = Art.Get(s.def.id);
                float ss = CharacterSettings.I.castleStaffScale * CharacterSettings.I.Scale(s.def.id);
                sr.transform.localScale = new Vector3(ss, ss, 1);
                name.transform.localScale = new Vector3(1f / ss, 1f / ss, 1); // 이름표 글자 크기는 그대로
                name.Set(s.def.Name.Split(' ')[0]);
                sr.GetComponent<CharacterMotion>().MoveTo(ToWorld(s.pos));
                TrackAgent(s, sr, name);
            }
        }

        void OnFloatingText(Vector2 simPos, string text) => OnFloatingText(simPos, text, UIKit.Gold);
        void OnFloatingText(Vector2 simPos, string text, Color? color) => OnFloatingText(simPos, text, color ?? UIKit.Gold);

        void OnFloatingText(Vector2 simPos, string text, Color color)
        {
            var wp = ToWorld(simPos) + new Vector2(0, RoomH * 0.45f);
            var t = Art.Text(transform, text, wp, 0.6f, color, OrderFx);
            t.gameObject.AddComponent<FloatUpWorld>();
        }
    }


    // 캐릭터: 이동 방향으로 뒤집고, 걸을 때 통통 튄다 (아트 픽셀 단위로 스냅)
    public class CharacterMotion : MonoBehaviour
    {
        SpriteRenderer sr;
        Vector2 last;
        float phase;
        bool init;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        public void MoveTo(Vector2 pos)
        {
            if (!init) { last = pos; init = true; }
            Vector2 d = pos - last;
            bool moving = d.sqrMagnitude > 0.000001f;
            if (Mathf.Abs(d.x) > 0.0005f) sr.flipX = d.x < 0;
            phase = moving ? phase + Time.deltaTime * 14f : 0f;
            float hop = moving ? Mathf.Round(Mathf.Abs(Mathf.Sin(phase)) * 1.5f) / 16f : 0f;
            transform.localPosition = new Vector3(Mathf.Round(pos.x * 16f) / 16f, Mathf.Round(pos.y * 16f) / 16f + hop, 0);
            last = pos;
        }

        void OnDisable() => init = false;
    }

    // 전시 우리 안에서 괴물이 좌우로 어슬렁거린다 (식물은 제자리)
    public class MonsterWander : MonoBehaviour
    {
        float minX, maxX, baseY, target, speed, wait, phase, x;
        bool plant;
        string id;
        SpriteRenderer sr;

        public void Init(float min, float max, float y, bool isPlant, string monsterId)
        {
            minX = min; maxX = max; baseY = y; plant = isPlant; id = monsterId;
            sr = GetComponent<SpriteRenderer>();
            speed = Random.Range(0.4f, 1.0f);
            phase = Random.value * 10f;
            x = target = transform.localPosition.x;
        }

        void Update()
        {
            phase += Time.deltaTime;
            float s = CharacterSettings.I.castleMonsterScale * CharacterSettings.I.Scale(id);
            transform.localScale = new Vector3(s, s, 1);
            if (plant) { sr.flipX = Mathf.Sin(phase * 0.8f) > 0; return; }
            if (wait > 0) wait -= Time.deltaTime;
            else if (Mathf.Abs(x - target) < 0.02f) { target = Random.Range(minX, maxX); wait = Random.Range(0.5f, 2.5f); }
            else
            {
                x = Mathf.MoveTowards(x, target, speed * Time.deltaTime);
                sr.flipX = target < x;
            }
            float bob = wait > 0 ? 0f : Mathf.Round(Mathf.Abs(Mathf.Sin(phase * 8f))) / 16f;
            transform.localPosition = new Vector3(Mathf.Round(x * 16f) / 16f, baseY + bob, 0);
        }
    }

    // 순간이동 연출: 번쩍 → 빛기둥이 잠깐 버티다 가늘어지며 사라지고, 보라빛 불티가 튀어 오른다 (도트 단위로 스냅)
    public class TeleportFx : MonoBehaviour
    {
        const float Life = 0.8f, Hold = 0.15f, Px = 1f / 16f;
        static readonly Color[] Palette = { new Color(0.8f, 0.55f, 1f), new Color(1f, 1f, 1f), new Color(0.55f, 0.85f, 1f), new Color(1f, 0.75f, 0.95f) };

        SpriteRenderer beam, core, flash, ring;
        readonly List<(SpriteRenderer sr, Vector2 pos, Vector2 vel, float delay)> sparks = new List<(SpriteRenderer, Vector2, Vector2, float)>();
        float t, size;

        public void Init(int order, float scale)
        {
            size = Mathf.Max(0.8f, scale);
            beam = Art.Sprite(transform, "Beam", Art.White, order, new Vector2(0, 0.8f * size));
            beam.color = new Color(0.75f, 0.5f, 1f, 0.8f);
            core = Art.Sprite(transform, "Core", Art.White, order + 1, new Vector2(0, 0.8f * size));
            core.color = new Color(1f, 0.95f, 1f, 0.95f);
            flash = Art.Sprite(transform, "Flash", Art.Get("fx_orb"), order + 2, new Vector2(0, 0.55f * size));
            flash.color = new Color(0.95f, 0.8f, 1f, 1f);
            ring = Art.Sprite(transform, "Ring", Art.Get("fx_poof"), order - 1, new Vector2(0, 2 * Px));
            ring.color = new Color(0.75f, 0.55f, 1f, 0.95f);
            for (int i = 0; i < 18; i++)
            {
                var sr = Art.Sprite(transform, "Spark", Art.White, order + 3, Vector2.zero);
                sr.color = Palette[i % Palette.Length];
                float s = (i % 3 == 2 ? 1 : 2) * Px;
                sr.transform.localScale = new Vector3(s, s, 1);
                var pos = new Vector2(Random.Range(-0.45f, 0.45f) * size, Random.Range(0f, 1.3f) * size);
                var vel = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(1.5f, 3.5f));
                sparks.Add((sr, pos, vel, Random.Range(0f, 0.2f)));
            }
            Update();
        }

        static Vector3 Snap(float x, float y) => new Vector3(Mathf.Round(x / Px) * Px, Mathf.Round(y / Px) * Px, 0);

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            // 빛기둥: 잠깐 버틴 뒤 가늘어진다
            float shrink = Mathf.Clamp01((t - Hold) / (Life * 0.5f));
            float bw = Mathf.Round(14f * size * (1f - shrink)) * Px;
            beam.transform.localScale = new Vector3(bw, 1.9f * size, 1);
            core.transform.localScale = new Vector3(Mathf.Round(4f * size * (1f - shrink)) * Px, 1.9f * size, 1);
            var bc = beam.color; bc.a = 0.8f * (1f - shrink); beam.color = bc;
            var cc = core.color; cc.a = 0.95f * (1f - shrink); core.color = cc;
            // 번쩍
            float f = Mathf.Clamp01(t / 0.25f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, f) * size;
            var fc = flash.color; fc.a = 1f - f; flash.color = fc;
            // 바닥 연기
            float rs = Mathf.Lerp(0.8f, 1.5f, k) * size;
            ring.transform.localScale = new Vector3(rs, rs * 0.45f, 1);
            var rc = ring.color; rc.a = 0.95f * (1f - k); ring.color = rc;
            // 불티 (반짝이며 떠오른다)
            foreach (var (sr, pos, vel, delay) in sparks)
            {
                float st = Mathf.Max(0, t - delay);
                var p = pos + vel * st;
                sr.transform.localPosition = Snap(p.x, p.y);
                var c = sr.color; c.a = (1f - k) * (Mathf.Sin(st * 30f) > -0.3f ? 1f : 0.25f); sr.color = c;
            }
            if (t >= Life) Destroy(gameObject);
        }
    }

    // 투기장의 출전 괴물 + 대련 연출 (도트 없이 스프라이트 이동·번쩍임과 이펙트로만).
    // 대련 시간 동안 용사(0.2) → 괴물(0.45) → 용사(0.7) 순서로 한 번씩 달려들어 친다. 결과(골드·사망)는 CastleSim.Spar 가 띄운다.
    public class ArenaFighter : MonoBehaviour
    {
        const float Px = 1f / 16f, FootY = 4f / 16f;
        static readonly (float t, bool hero)[] Strikes = { (0.2f, true), (0.45f, false), (0.7f, true) };
        const float Width = 0.09f;      // 한 번 달려드는 데 걸리는 진행 비율(반폭)
        const float Reach = 0.22f;      // 달려드는 거리 (칸 폭 대비)

        BuildingState b;
        SpriteRenderer sr;
        int fxOrder;
        string curId;
        Visitor cur;
        float lastP = -1f, phase;

        public void Init(BuildingState building, int order)
        {
            b = building;
            fxOrder = order;
            sr = GetComponent<SpriteRenderer>();
            phase = Random.value * 10f;
            Update();
        }

        public static float Snap(float v) => Mathf.Round(v / Px) * Px;
        public static float Progress(Visitor v) => Mathf.Clamp01(1f - v.timer / Mathf.Max(0.1f, GuestSettings.I.boutTime));

        // 0 → 1 → 0 로 부드럽게 (달려들었다 돌아오기)
        static float Bell(float p, float t) { float k = Mathf.Clamp01(1f - Mathf.Abs(p - t) / Width); return k * k * (3f - 2f * k); }

        // 오른쪽(+)으로 달려들고, 괴물에게 맞으면 살짝 밀려난다 (칸 폭 대비)
        public static float HeroOffset(float p)
        {
            float x = 0;
            foreach (var (t, hero) in Strikes) x += hero ? Bell(p, t) * Reach : -Bell(p, t + 0.05f) * 0.06f;
            return x;
        }

        public static float MonsterOffset(float p)
        {
            float x = 0;
            foreach (var (t, hero) in Strikes) x += hero ? Bell(p, t + 0.05f) * 0.06f : -Bell(p, t) * Reach;
            return x;
        }

        // 달려들 때 살짝 뛰어오른다 (월드 단위)
        public static float Hop(float p, bool isHero)
        {
            float y = 0;
            foreach (var (t, hero) in Strikes) if (hero == isHero) y += Mathf.Sin(Mathf.Clamp01((p - t + Width) / (Width * 2f)) * Mathf.PI) * 0.35f;
            return y;
        }

        // 맞은 쪽이 붉게 번쩍인다 (0~1)
        public static float HitFlash(float p, bool monster)
        {
            float f = 0;
            foreach (var (t, hero) in Strikes) if (hero == monster) f = Mathf.Max(f, Mathf.Clamp01(1f - Mathf.Abs(p - (t + 0.03f)) / 0.05f));
            return f;
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Sim == null) return;
            var champ = g.ArenaChampion();
            string id = champ?.id;
            if (id != curId)
            {
                // 쓰러져 다음 괴물로 바뀌면 연기와 함께 사라진다
                if (curId != null) Fx("fx_poof", transform.localPosition + new Vector3(0, 0.6f, 0), Vector2.up * 0.3f, 0.4f, Color.white, 0.8f, 1.4f, 0);
                curId = id;
                sr.sprite = champ != null ? Art.Get(champ.id) : null;
            }
            sr.enabled = champ != null;
            if (champ == null) return;

            // 지금 이 투기장에서 대련 중인 용사 (첫 번째)
            Visitor v = null;
            foreach (var x in g.Sim.visitors)
                if (x.state == VisitorState.Sparring && x.currentStop == b.uid) { v = x; break; }
            if (v != cur) { cur = v; lastP = -1f; }

            var cs = CharacterSettings.I;
            float s = cs.castleMonsterScale * cs.Scale(champ.id);
            transform.localScale = new Vector3(s, s, 1);
            sr.flipX = true; // 왼쪽(용사)을 본다

            float baseX = GameData.ArenaMonsterX * CastleView.CellW;
            phase += Time.deltaTime;
            if (v == null)
            {
                // 쉬는 중: 제자리에서 숨 쉬듯 통통
                sr.color = Color.white;
                transform.localPosition = new Vector3(Snap(baseX), FootY + Snap(Mathf.Abs(Mathf.Sin(phase * 2.2f)) * 0.12f), 0);
                return;
            }

            float p = Progress(v);
            transform.localPosition = new Vector3(Snap(baseX + MonsterOffset(p) * CastleView.CellW), FootY + Snap(Hop(p, false)), 0);
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), HitFlash(p, true));

            // 칠 때마다: 베기 + 충격 + 불티 (맞는 쪽 앞에서)
            foreach (var (t, hero) in Strikes)
            {
                float hit = t + 0.03f;
                if (lastP < hit && p >= hit)
                {
                    float hx = hero ? baseX - 0.05f * CastleView.CellW : (GameData.ArenaHeroX + 0.05f) * CastleView.CellW;
                    var at = new Vector3(hx, FootY + 0.9f, 0);
                    var col = hero ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.5f, 0.45f);
                    Fx("fx_slash", at + new Vector3(hero ? -0.3f : 0.3f, 0.2f, 0), new Vector2(hero ? 0.4f : -0.4f, -0.3f), 0.2f, col, 1.0f, 1.3f, hero ? -60f : 120f);
                    Fx("fx_hit", at, Vector2.zero, 0.16f, Color.white, 0.6f, 1.1f, 0);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = Random.value * Mathf.PI * 2f;
                        Fx("fx_spark", at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.6f, 1.2f), 0.3f, col, 0.5f, 0.2f, 0);
                    }
                    Sound.Play("click", 0.6f);
                }
            }
            lastP = p;
        }

        // 방 기준 좌표에 이펙트 하나
        void Fx(string sprite, Vector3 at, Vector2 move, float life, Color color, float s0, float s1, float rot)
        {
            var go = new GameObject("ArenaFx");
            go.transform.SetParent(transform.parent, false);
            go.transform.localPosition = at;
            go.AddComponent<WorldFx>().Init(Art.Get(sprite), fxOrder, move, life, color, s0, s1, rot);
        }
    }

    // 짧은 이펙트: 움직이며 커지고(작아지고) 사라진다
    public class WorldFx : MonoBehaviour
    {
        SpriteRenderer sr;
        Vector3 start;
        Vector2 move;
        float t, life, s0, s1;
        Color color;

        public void Init(Sprite sprite, int order, Vector2 moveBy, float lifetime, Color c, float scale0, float scale1, float rot)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            start = transform.localPosition;
            move = moveBy; life = lifetime; color = c; s0 = scale0; s1 = scale1;
            transform.localRotation = Quaternion.Euler(0, 0, rot);
            Update();
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            transform.localPosition = start + (Vector3)(move * k);
            float s = Mathf.Lerp(s0, s1, k);
            transform.localScale = new Vector3(s, s, 1);
            var c = color; c.a = 1f - k * k; sr.color = c;
            if (t >= life) Destroy(gameObject);
        }
    }

    public class FloatUpWorld : MonoBehaviour
    {
        float t;
        WorldText text;
        void Awake() => text = GetComponent<WorldText>();
        void Update()
        {
            t += Time.deltaTime;
            transform.localPosition += Vector3.up * 1.2f * Time.deltaTime;
            text.SetAlpha(1f - t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}

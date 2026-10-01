using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mawang
{
    // 모달 창 내용 구성. 목록은 카드/슬롯 격자, 모든 재화·수치는 아이콘과 함께 표시한다.
    public partial class GameUI
    {
        int shopTab, researchTab;

        static string Dim(string s) => UIKit.Col(s, UIKit.TextDim);
        static string Hi(string s) => UIKit.Col(s, UIKit.Gold);
        static string HoldHint => Mobile ? L.T("꾹 누르기: 빠르게 반복", "Hold: repeat fast") : L.T("우클릭 누르고 있기: 빠르게 반복", "Hold right-click: repeat fast");

        // ── 공용 조각 ──────────────────────────────────────────
        // 방 그림 미리보기 (160×96 도트 → 2배)
        static RectTransform Thumb(Transform parent, BuildingDef d, float scale = 2f)
        {
            var box = UIKit.Panel(parent, "ui_inset", "Thumb");
            float w = 160 * scale + UIKit.P * 2, h = 96 * scale + UIKit.P * 2;
            UIKit.Size(box, w, h);
            void Layer(string sprite)
            {
                var img = UIKit.Rect(sprite, box.transform).gameObject.AddComponent<Image>();
                img.sprite = Art.Get(sprite);
                img.raycastTarget = false;
                UIKit.Stretch(img.rectTransform, UIKit.P, UIKit.P, UIKit.P, UIKit.P);
            }
            Layer(d.icon);
            if (d.type == BuildingType.Tank) Layer("room_tank_front");
            return box.rectTransform;
        }

        // 아이템 슬롯: 큰 아이콘 + 이름 + 부가 정보(실시간). 누르고 있으면 반복(hold).
        static Button Slot(Transform parent, string sprite, string title, Func<string> sub, Func<bool> action, Btn style = Btn.Alt, bool silhouette = false)
        {
            var b = UIKit.Button(parent, "", null, 130, 150, style, UIKit.TS);
            var content = b.Content();
            DestroyImmediate(content.GetComponent<HorizontalLayoutGroup>());
            for (int k = content.childCount - 1; k >= 0; k--) DestroyImmediate(content.GetChild(k).gameObject);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.spacing = 2;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var icon = UIKit.Icon(content, sprite, 64);
            if (silhouette) UIKit.Tint(icon, new Color(0.08f, 0.05f, 0.12f));
            var t = UIKit.Label(content, title, UIKit.TS, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.GetComponent<LayoutElement>().flexibleWidth = 0;
            if (sub != null)
            {
                var s = UIKit.Label(content, "", UIKit.TS, TextAnchor.MiddleCenter, UIKit.Gold).Bind(sub);
                s.GetComponent<LayoutElement>().flexibleWidth = 0;
            }
            if (action != null) b.Hold(action);
            return b;
        }

        static string MonsterTip(MonsterDef m)
        {
            return L.T($"{Hi(m.Name)}  {Dim(m.isPlant ? "마초" : m.hidden ? "히든 괴물" : "괴물")}\n공간 {m.space} · 마기 {(m.maki > 0 ? "+" : "")}{m.maki}\n관람료 {m.admission:N0}/분\n판매가 {m.sellPrice:N0} {L.Cur(m.sellCurrency)}",
                       $"{Hi(m.Name)}  {Dim(m.isPlant ? "Plant" : m.hidden ? "Hidden" : "Monster")}\nSpace {m.space} · Miasma {(m.maki > 0 ? "+" : "")}{m.maki}\nAdmission {m.admission:N0}/min\nSells for {m.sellPrice:N0} {L.Cur(m.sellCurrency)}");
        }

        static void Tabs(Transform parent, string[] names, string[] icons, int current, Action<int> pick, Func<int, int> badge = null)
        {
            var row = UIKit.Row(parent, 60, 8);
            for (int i = 0; i < names.Length; i++)
            {
                int t = i;
                var b = UIKit.Button(row, names[i], () => pick(t), 0, 54, current == i ? Btn.Accent : Btn.Alt, UIKit.TS, icons[i], 32);
                var le = b.GetComponent<LayoutElement>();
                le.preferredWidth = -1; le.minWidth = Mobile ? 110 : 150; le.flexibleWidth = 1;
                if (badge != null && current != i) UIKit.Badge(b.transform, () => badge(t), new Vector2(-6, -6));
            }
            UIKit.Spacer(parent, 4);
        }

        // ── 건설 ───────────────────────────────────────────────
        void BuildBuildPanel(RectTransform c)
        {
            UIKit.Note(c, L.T("건물을 고른 뒤 빈 칸을 탭하거나 끌어서 짓습니다. 전시 우리는 끈 영역 전체가 하나의 우리가 됩니다. 철거하면 100% 환급.",
                              "Pick a building, then tap or drag over empty cells. A dragged cage becomes one big cage. Demolishing refunds 100%."));
            BuildingCards(c, (d, card) =>
            {
                bool Full() => d.maxCount > 0 && g.CountOf(d.type) >= d.maxCount;
                var btn = UIKit.Button(card, L.T("건설", "Build"), () => StartPlacing(d.type), 332, 50, Btn.Good, UIKit.TM, "ic_build", 32)
                    .BindEnabled(() => g.CanAfford(d.costGold, d.costMat) && !Full());
                btn.Tip(() => Full() ? L.T($"최대 {d.maxCount}개까지 지을 수 있습니다.", $"Limit: {d.maxCount}") : !g.CanAfford(d.costGold, d.costMat) ? L.T("재화가 부족합니다.", "Not enough resources.") : L.T("눌러서 배치할 칸을 고르세요.", "Then choose where to build."));
            });
        }

        void BuildingCards(RectTransform c, Action<BuildingDef, RectTransform> addButtons)
        {
            var grid = UIKit.Grid(c, 364, 444);
            foreach (var def in GameData.Buildings.Values)
            {
                var d = def;
                var card = UIKit.Card(grid, "ui_row", 12, 6);
                Thumb(card, d, 2f);
                var name = UIKit.Row(card, 32, 6);
                UIKit.Label(name, d.Name, UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold);
                if (d.maxCount > 0) UIKit.Label(name, "", UIKit.TS, TextAnchor.MiddleRight, UIKit.TextDim, 90).Bind(() => $"{g.CountOf(d.type)}/{d.maxCount}");
                UIKit.Size(name, 332);
                var desc = UIKit.Label(card, d.Desc, UIKit.TS, TextAnchor.UpperLeft, UIKit.TextDim);
                UIKit.Size(desc, 332, 78);
                var cost = UIKit.Row(card, 30, 4);
                cost.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UIKit.Cost(cost, d.costGold, d.costMat);
                if (d.type == BuildingType.Tank) UIKit.Label(cost, L.T("/칸", "/cell"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 60);
                UIKit.Size(cost, 332, 30);
                addButtons(d, card);
            }
        }

        // 빈 칸을 탭: 그 칸에 바로 짓기
        public void OpenBuildAt(int floor, int x)
        {
            Sound.Play("open");
            string wing = Game.WingOf(x) == 0 ? L.T("왼쪽", "Left") : L.T("오른쪽", "Right");
            OpenModal(L.T($"{wing} {floor + 1}층에 건설", $"Build on {wing} {floor + 1}F"), c =>
            {
                UIKit.Note(c, L.T("이 칸에 바로 짓습니다. 전시 우리를 여러 칸으로 크게 지으려면 '끌어서 짓기'를 누르세요.",
                                  "Builds right on this cell. For a bigger cage, press 'Drag to build'."));
                BuildingCards(c, (d, card) =>
                {
                    var row = UIKit.Row(card, 50, 8);
                    UIKit.Size(row, 332, 50);
                    string block() => g.BuildBlock(d.type, floor, x);
                    var here = UIKit.Button(row, L.T("여기에 건설", "Build here"), () => { if (g.TryBuild(d.type, floor, x)) CloseModal(); }, d.type == BuildingType.Tank ? 160 : 332, 50, Btn.Good, UIKit.TS, "ic_build", 24)
                        .BindEnabled(() => block() == null);
                    here.Tip(() => block() ?? L.T("이 칸에 짓습니다.", "Build on this cell."));
                    if (d.type == BuildingType.Tank)
                        UIKit.Button(row, L.T("끌어서 짓기", "Drag to build"), () => StartPlacing(d.type), 164, 50, Btn.Primary, UIKit.TS)
                            .Tip(L.T("성에서 끌어 여러 칸·여러 층을 하나의 우리로 짓습니다.", "Drag over the castle to make one cage across cells and floors."));
                });
            }, "ic_build");
        }

        // ── 성 증축 ────────────────────────────────────────────
        public void OpenExpand(int wing)
        {
            OpenModal(L.T("성 증축", "Castle Expansion"), c =>
            {
                UIKit.Note(c, L.T("엘리베이터 왼쪽·오른쪽 날개를 따로 한 층씩 올립니다. 최대 10층. 층이 늘면 손님도 많고 부유해집니다.",
                                  "Raise the left and right wings one floor at a time, up to 10. More floors bring more (and richer) guests."));
                ExpandRow(c, 0, wing == 0);
                ExpandRow(c, 1, wing == 1);
            }, "up_floor");
        }

        void ExpandRow(RectTransform c, int wing, bool highlight)
        {
            int n = g.NextFloor(wing);
            bool max = n > GameData.MaxFloors;
            var row = UIKit.Row(c, 100, 16, true);
            if (highlight) row.GetComponent<Image>().color = new Color(1f, 0.92f, 0.7f);
            var box = UIKit.Panel(row, "ui_inset", "IconBox");
            UIKit.Size(box, 76, 76);
            var ic = UIKit.Icon(box.transform, "up_floor", 64);
            UIKit.Stretch(ic.rectTransform, 6, 6, 6, 6);
            var mid = UIKit.Column(row, 4);
            mid.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            string wingName = wing == 0 ? L.T("왼쪽 날개", "Left wing") : L.T("오른쪽 날개", "Right wing");
            var title = UIKit.Row(mid, 34, 10);
            UIKit.Label(title, wingName, UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold, 200);
            UIKit.Pips(title, g.FloorsOpen(wing), GameData.MaxFloors);
            UIKit.Label(mid, max ? L.T("모든 층을 열었습니다.", "All floors open.") : L.T($"{n}층 개방 (현재 {g.FloorsOpen(wing)}층)", $"Open floor {n} (now {g.FloorsOpen(wing)})"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);
            if (max) return;
            var right = UIKit.Column(row, 6);
            UIKit.Size(right, 300);
            UIKit.Size(UIKit.Cost(right, GameData.ExpandGold(n), GameData.ExpandMat(n), GameData.ExpandGems(n)), 300, 28);
            var btn = UIKit.Button(right, L.T($"{n}층 증축", $"Build floor {n}"), () => g.Expand(wing), 300, 54, Btn.Good, UIKit.TM, "up_floor", 32);
            btn.BindEnabled(() => g.ExpandBlock(wing) == null);
            btn.Tip(() => g.ExpandBlock(wing) ?? wingName);
        }

        // ── 건물 관리 ──────────────────────────────────────────
        public void OpenBuilding(BuildingState b)
        {
            var def = GameData.Buildings[b.type];
            OpenModal(def.Name, c => BuildBuildingPanel(c, b), IconForBuilding(b.type));
        }

        static string IconForBuilding(BuildingType t) => t switch
        {
            BuildingType.Tank => "ic_space",
            BuildingType.Restaurant => "food_2",
            BuildingType.Souvenir => "gift_2",
            BuildingType.Lab => "up_lab",
            BuildingType.Dorm => "castle_skel",
            _ => "up_rest",
        };

        void BuildBuildingPanel(RectTransform c, BuildingState b)
        {
            if (g.BuildingByUid(b.uid) == null) { CloseModal(); return; }
            var def = GameData.Buildings[b.type];
            string size = b.Cells > 1 ? $"  {b.cw}×{b.ch}" : "";
            modalTitle.text = $"{def.Name}  Lv.{b.level}{size}";

            var top = UIKit.Row(c, 210, 18);
            Thumb(top, def, 2f);
            var info = UIKit.Column(top, 8);
            info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var lvRow = UIKit.Row(info, 34, 10);
            UIKit.Label(lvRow, $"Lv.{b.level}", UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold, 80);
            UIKit.Pips(lvRow, b.level, def.maxLevel, def.maxLevel > 5 ? 14 : 18);
            string wing = Game.WingOf(b.x) == 0 ? L.T("왼쪽", "Left") : L.T("오른쪽", "Right");
            UIKit.Label(lvRow, b.ch > 1 ? $"{wing} {b.floor + 1}~{b.floor + b.ch}F" : $"{wing} {b.floor + 1}F", UIKit.TS, TextAnchor.MiddleRight, UIKit.TextDim);
            var desc = UIKit.Label(info, def.Desc, UIKit.TS, TextAnchor.UpperLeft, UIKit.TextDim);
            UIKit.Size(desc, -1, 56);

            var actions = UIKit.Row(info, 60, 12);
            if (b.level < def.maxLevel)
            {
                g.CanUpgrade(b, out int ug, out int um, out _);
                string label = b.type == BuildingType.Tank ? L.T("확장", "Enlarge") : Mobile ? L.T("강화", "Up") : L.T("업그레이드", "Upgrade");
                var up = UIKit.Button(actions, label, () => g.Upgrade(b), Mobile ? 300 : 400, 56, Btn.Good, UIKit.TM);
                UIKit.Cost(up.Content(), ug, um);
                up.BindEnabled(() => g.CanAfford(ug, um));
                up.Tip(b.type == BuildingType.Tank ? L.T("우리를 넓혀 공간·마기·누적 한도를 늘립니다. (칸 수만큼 비용)", "Enlarges space, miasma and fee storage. (Cost × cells)")
                    : b.type == BuildingType.Lab ? L.T($"보석 생산 간격 {GameData.LabInterval[Mathf.Min(b.level, GameData.LabInterval.Length - 1)]:0}초", $"Gem interval {GameData.LabInterval[Mathf.Min(b.level, GameData.LabInterval.Length - 1)]:0}s")
                    : L.T("레벨이 오르면 더 비싼 상품을 팔거나 효율이 좋아집니다.", "Higher levels sell pricier goods or work better."));
            }
            else UIKit.Label(actions, L.T("최대 레벨", "Max level"), UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold, 200);
            UIKit.Flex(actions);
            var demo = UIKit.Button(actions, Mobile ? "" : L.T("철거", "Demolish"), () => { g.Demolish(b); CloseModal(); }, Mobile ? 64 : 150, 56, Btn.Danger, UIKit.TS, "ic_close", 24);
            demo.Tip(L.T($"철거하고 투자한 재화를 모두 돌려받습니다.\n환급: {UIKit.Money(b.investedGold, b.investedMat)}", $"Demolish and get everything back.\nRefund: {UIKit.Money(b.investedGold, b.investedMat)}"));

            UIKit.Spacer(c, 6);
            switch (b.type)
            {
                case BuildingType.Tank: BuildTankSection(c, b); break;
                case BuildingType.Restaurant:
                case BuildingType.Souvenir: BuildShopSection(c, b); break;
                case BuildingType.Lab:
                {
                    UIKit.Header(c, L.T("보석", "Gems"), "ic_rp");
                    var row = UIKit.Row(c, 60, 12, true);
                    UIKit.Icon(row, "ic_rp", 40);
                    UIKit.Bar(row, () => (float)b.labRp / g.LabRpCap, new Color(0.4f, 0.85f, 1f), -1, 32, () => $"{b.labRp} / {g.LabRpCap}");
                    UIKit.Button(row, L.T("수거", "Collect"), () => { int n = b.labRp; g.CollectLab(b); if (n > 0) Sound.Play("gem"); }, 150, 48, Btn.Good, UIKit.TS, "ic_rp", 24).BindEnabled(() => b.labRp > 0);
                    UIKit.Label(UIKit.Row(c, 36).transform, "", UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim)
                        .Bind(() => L.T($"{g.LabInterval(b):0}초마다 보석 1개 · 다음까지 {g.LabInterval(b) - b.labTimer:0}초 · 마녀 연구원이 수거를 잘합니다.",
                                        $"1 Gem every {g.LabInterval(b):0}s · next in {g.LabInterval(b) - b.labTimer:0}s · Witch Researchers are lab specialists."));
                    break;
                }
                case BuildingType.Dorm:
                    UIKit.Header(c, L.T("직원 숙소", "Staff Dorm"), "castle_skel");
                    UIKit.Note(c, L.T($"직원 한도 {g.StaffCapacity()}명 (기본 {GameData.BaseStaffCap} + 숙소 레벨 합). 고용은 [직원] 메뉴에서.", $"Staff capacity {g.StaffCapacity()} (base {GameData.BaseStaffCap} + dorm levels). Hire in [Staff]."));
                    break;
                case BuildingType.Rest:
                    UIKit.Header(c, L.T("휴게실", "Lounge"), "ic_time");
                    UIKit.Note(c, L.T($"매일 06시 이후 처음 접속한 시점에 휴게실 개수 × {GameData.RestDailyGold:N0}골드를 지급합니다.", $"Pays lounges × {GameData.RestDailyGold:N0} Gold on your first visit after 06:00 each day."));
                    break;
            }
        }

        void BuildTankSection(RectTransform c, BuildingState b)
        {
            var stats = UIKit.Row(c, 64, 26, true);
            UIKit.Amount(stats, "ic_space", () => L.T($"공간 {g.TankSpaceUsed(b)}/{g.TankSpaceMax(b)}", $"Space {g.TankSpaceUsed(b)}/{g.TankSpaceMax(b)}"), null, UIKit.TS, 32).Tip(L.T("괴물마다 차지하는 공간이 다릅니다.", "Each monster takes a different amount of space."));
            UIKit.Amount(stats, "up_maki", () => L.T($"마기 {g.TankMaki(b)}", $"Miasma {g.TankMaki(b)}"), () => g.TankMaki(b) >= 0, UIKit.TS, 32)
                .Tip(() => L.T($"남은 마기 {g.TankMaki(b)} (기본 {g.TankMakiBase(b)})\n괴물은 마기를 소모하고, 마초는 회복합니다.\n마기가 0 미만이 되는 괴물은 넣을 수 없습니다.",
                               $"Miasma left {g.TankMaki(b)} (base {g.TankMakiBase(b)})\nMonsters use miasma, plants restore it.\nYou can't add a monster that drops it below 0."));
            UIKit.Amount(stats, "ic_gold", () => L.T($"{g.TankRatePerMin(b):N0}/분", $"{g.TankRatePerMin(b):N0}/min"), null, UIKit.TS, 32).Tip(L.T("분당 관람료", "Admission per minute"));
            UIKit.Bar(stats, () => b.accumulated / g.TankCap(b), UIKit.Gold, -1, 32, () => $"{Mathf.FloorToInt(b.accumulated):N0} / {g.TankCap(b):N0}")
                .Tip(L.T("쌓인 관람료. 한도에 닿으면 더 쌓이지 않습니다.", "Stored fees. Stops at the cap."));
            UIKit.Button(stats, L.T("수령", "Collect"), () => { if (g.CollectTank(b, 0f) > 0) Sound.Play("coin"); }, 130, 48, Btn.Good, UIKit.TS, "ic_gold", 24)
                .BindEnabled(() => b.accumulated >= 1f);

            UIKit.Header(c, L.T("전시 중  (탭: 빼기)", "On display  (tap: remove)"), "ic_space");
            var shown = UIKit.Grid(c, 130, 150, 10);
            if (b.contents.Count == 0) UIKit.Note(c, L.T("비어 있습니다. 아래 보관함에서 괴물을 넣으세요.", "Empty. Add monsters from storage below."));
            foreach (var e in b.contents.ToArray())
            {
                var m = GameData.MonsterById[e.id];
                Slot(shown, m.id, m.Name, () => $"×{CountList.Get(b.contents, m.id)}", () => g.RemoveFromTank(b, m.id)).Tip(() => MonsterTip(m) + L.T("\n\n탭하면 보관함으로 뺍니다.\n", "\n\nTap to move back to storage.\n") + HoldHint);
            }

            UIKit.Header(c, L.T("보관함에서 넣기  (탭: 넣기)", "Add from storage  (tap: add)"), "ic_bag");
            var inv = UIKit.Grid(c, 130, 150, 10);
            bool any = false;
            foreach (var m in GameData.Monsters)
            {
                int have = CountList.Get(g.S.monsters, m.id);
                if (have <= 0) continue;
                any = true;
                var mm = m;
                var slot = Slot(inv, m.id, m.Name, () => $"×{CountList.Get(g.S.monsters, mm.id)}", () => g.AddToTank(b, mm.id), Btn.Primary);
                slot.BindEnabled(() => g.CanAddToTank(b, mm.id) == null);
                slot.Tip(() => { var err = g.CanAddToTank(b, mm.id); return MonsterTip(mm) + (err != null ? $"\n\n{UIKit.Col(err, new Color(0.7f, 0.1f, 0.1f))}" : L.T("\n\n탭하면 우리에 넣습니다.\n", "\n\nTap to put in the cage.\n") + HoldHint); });
            }
            if (!any) UIKit.Note(c, L.T("보관함이 비었습니다. [포획장]에서 포획대원을 파견하세요.", "Storage is empty. Send hunters from the [Hunt] tab."));
        }

        // 가게 건물: 이 가게가 파는 상품
        void BuildShopSection(RectTransform c, BuildingState b)
        {
            UIKit.Header(c, L.T("판매 상품", "Menu"), b.type == BuildingType.Restaurant ? "food_1" : "gift_1");
            UIKit.Note(c, L.T("손님은 살 수 있는 가장 비싼 상품부터 삽니다. 가게를 탭하면 기다리는 손님에게 바로 팝니다.", "Guests buy the priciest item they can afford. Tap the shop to sell to waiting guests."));
            foreach (var it in GameData.ShopItems)
            {
                if (it.shop != b.type) continue;
                GoodsRow(c, it);
            }
        }

        void GoodsRow(RectTransform c, ShopItemDef it)
        {
            var item = it;
            bool unlocked = g.CanSellItem(it);
            var row = UIKit.Row(c, 76, 14, true);
            var icon = UIKit.Icon(row, it.id, 48);
            if (!unlocked) UIKit.Tint(icon, new Color(0.4f, 0.4f, 0.45f));
            var name = UIKit.Column(row, 2);
            UIKit.Size(name, Mobile ? 250 : 280);
            UIKit.Label(name, it.Name, UIKit.TM, TextAnchor.MiddleLeft, unlocked ? UIKit.TextMain : UIKit.TextDim);
            string need = L.T($"{GameData.Buildings[it.shop].Name} Lv.{it.tier}{(unlocked ? "" : " 필요")}", $"{GameData.Buildings[it.shop].Name} Lv.{it.tier}{(unlocked ? "" : " needed")}");
            if (Mobile && unlocked) need = $"Lv.{it.tier} · {it.buyPrice:N0} → {UIKit.Col($"{it.sellPrice:N0}", UIKit.GoodText)}G"; // 폰: 가격을 이름 아래 한 줄로
            UIKit.Label(name, need, UIKit.TS, TextAnchor.MiddleLeft, unlocked ? UIKit.TextDim : UIKit.BadText);
            if (!Mobile)
            {
                var price = UIKit.Row(row, 40, 6);
                UIKit.Size(price, 250);
                UIKit.Amount(price, "ic_gold", $"{it.buyPrice:N0}", UIKit.TS, 24);
                UIKit.Label(price, "→", UIKit.TS, TextAnchor.MiddleCenter, UIKit.TextDim, 30);
                UIKit.Amount(price, "ic_gold", $"{it.sellPrice:N0}", UIKit.TS, 24, UIKit.GoodText);
                price.Tip(L.T($"매입가 {it.buyPrice:N0}골드 → 판매가 {it.sellPrice:N0}골드\n개당 이익 {it.sellPrice - it.buyPrice:N0}골드", $"Buy {it.buyPrice:N0} → Sell {it.sellPrice:N0} Gold\nProfit {it.sellPrice - it.buyPrice:N0} each"));
            }
            UIKit.Label(row, "", UIKit.TS, TextAnchor.MiddleLeft).Bind(() => L.T($"재고 {Hi(CountList.Get(g.S.goods, item.id).ToString())}", $"Stock {Hi(CountList.Get(g.S.goods, item.id).ToString())}"));
            if (!unlocked)
            {
                var locked = UIKit.Button(row, L.T("판매 불가", "Can't sell"), null, Mobile ? 170 : 210, 52, Btn.Alt, UIKit.TS, "ic_lock", 24);
                locked.interactable = false;
                locked.Tip(L.T($"{GameData.Buildings[it.shop].Name} Lv.{it.tier} 이상이 있어야 팔 수 있고, 살 수 있습니다.", $"You need a {GameData.Buildings[it.shop].Name} Lv.{it.tier}+ to sell (and buy) this."));
                return;
            }
            var buy = UIKit.Button(row, "+10", null, Mobile ? 170 : 210, 52, Btn.Good, UIKit.TS).Hold(() => g.BuyGoods(item, 10));
            UIKit.Cost(buy.Content(), it.buyPrice * 10);
            buy.BindEnabled(() => g.S.gold >= item.buyPrice * 10);
            buy.Tip(L.T($"{it.Name} 10개 구입\n", $"Buy 10 {it.Name}\n") + HoldHint);
        }

        // ── 직원 ───────────────────────────────────────────────
        static string SpecialtyName(BuildingType t) => GameData.Buildings[t].Name;

        void BuildStaffPanel(RectTransform c)
        {
            var cap = UIKit.Row(c, 50, 12, true);
            UIKit.Icon(cap, "castle_skel", 36);
            UIKit.Label(cap, "", UIKit.TM, TextAnchor.MiddleLeft, null, 200).Bind(() => L.T($"직원 {g.StaffCount}/{g.StaffCapacity()}명", $"Staff {g.StaffCount}/{g.StaffCapacity()}"));
            UIKit.Bar(cap, () => (float)g.StaffCount / g.StaffCapacity(), new Color(0.7f, 0.5f, 0.9f), -1, 24);
            if (!Mobile) UIKit.Label(cap, L.T("숙소를 짓거나 업그레이드하면 한도가 늘어납니다.", "Build or upgrade dorms for more."), UIKit.TS, TextAnchor.MiddleRight, UIKit.TextDim, 460);

            UIKit.Header(c, L.T("성 관리자 — 담당 구역·층을 정하면 그 안의 일만 합니다", "Castle staff — they only work in their assigned area"), "castle_skel");
            UIKit.Note(c, L.T("특화 건물로 갈 때 더 빠르고 일도 빨리 끝냅니다. (스켈레톤=전시 우리 · 서큐버스=식당 · 가고일=기념품점 · 마녀=연구소)",
                              "They move and work faster at their specialty. (Skeleton=Cages · Succubus=Diner · Gargoyle=Gift Shop · Witch=Lab)"));
            var grid = UIKit.Grid(c, 364, 356);
            foreach (var cs in g.S.castleStaff)
            {
                var st = cs;
                var d = GameData.StaffById(cs.staffId);
                var card = StaffCard(grid, d);
                // 담당 구역: 날개
                var wingRow = UIKit.Row(card, 44, 4);
                UIKit.Size(wingRow, 332, 44);
                UIKit.Label(wingRow, L.T("구역", "Area"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 60);
                string[] wings = { L.T("전체", "All"), L.T("왼쪽", "Left"), L.T("오른쪽", "Right") };
                for (int w = -1; w <= 1; w++)
                {
                    int ww = w;
                    UIKit.Button(wingRow, wings[w + 1], () => { Sound.Play("click"); g.SetStaffArea(st, ww, st.floor); }, 88, 42, st.wing == w ? Btn.Accent : Btn.Alt, UIKit.TS);
                }
                // 담당 층
                var floorRow = UIKit.Row(card, 44, 4);
                UIKit.Size(floorRow, 332, 44);
                UIKit.Label(floorRow, L.T("층", "Floor"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 60);
                UIKit.Button(floorRow, "<", () => g.SetStaffArea(st, st.wing, st.floor - 1), 52, 42, Btn.Alt, UIKit.TM).BindEnabled(() => st.floor > -1);
                UIKit.Label(floorRow, st.floor < 0 ? L.T("모든 층", "All floors") : $"{st.floor + 1}F", UIKit.TM, TextAnchor.MiddleCenter, UIKit.Gold, 150);
                UIKit.Button(floorRow, ">", () => g.SetStaffArea(st, st.wing, st.floor + 1), 52, 42, Btn.Alt, UIKit.TM).BindEnabled(() => st.floor < GameData.MaxFloors - 1);
                UIKit.Button(card, cs.dispatched ? L.T("근무 중 · 복귀", "Working · Recall") : L.T("파견", "Dispatch"), () => g.SetCastleDispatch(st, !st.dispatched), 332, 50, cs.dispatched ? Btn.Danger : Btn.Good, UIKit.TS);
            }

            UIKit.Header(c, L.T("포획대원 — 파견은 포획장에서", "Hunters — dispatch from the Hunt tab"), "ic_hunt");
            var hg = UIKit.Grid(c, 364, 250);
            foreach (var h in g.S.hunters)
            {
                var hh = h;
                var card = StaffCard(hg, g.Def(h));
                UIKit.Button(card, L.T("포획장에서 보기", "View in Hunt"), () => ShowHunt(hh.regionId), 332, 50, Btn.Alt, UIKit.TS, "ic_hunt", 24);
            }

            var cands = g.HireCandidates();
            if (cands.Count == 0) return;
            UIKit.Header(c, L.T("고용", "Hire"), "ic_star");
            var cg = UIKit.Grid(c, 364, 290);
            foreach (var d in cands)
            {
                var dd = d;
                var card = StaffCard(cg, d);
                var cost = UIKit.Row(card, 30, 4);
                UIKit.Size(cost, 332, 30);
                cost.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UIKit.Cost(cost, g.HireCost(d));
                var hire = UIKit.Button(card, L.T("고용", "Hire"), () => g.Hire(dd), 332, 50, Btn.Good, UIKit.TS);
                hire.BindEnabled(() => g.HireBlock(dd) == null);
                hire.Tip(() => g.HireBlock(dd) ?? L.T($"{dd.Name}을(를) 고용합니다.", $"Hire {dd.Name}."));
            }
        }

        RectTransform StaffCard(Transform grid, StaffDef d)
        {
            var card = UIKit.Card(grid, "ui_row", 14, 6, TextAnchor.UpperLeft);
            var head = UIKit.Row(card, 64, 12);
            UIKit.Size(head, 332);
            var portrait = UIKit.Panel(head, "ui_inset", "Portrait");
            UIKit.Size(portrait, 64, 64);
            var pr = UIKit.Icon(portrait.transform, d.id, 56);
            UIKit.Stretch(pr.rectTransform, 4, 4, 4, 4);
            var nc = UIKit.Column(head, 2);
            UIKit.Label(nc, d.Name, UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold);
            UIKit.Label(nc, d.role == StaffRole.Hunter ? L.T("포획대원", "Hunter") : L.T($"특화: {SpecialtyName(d.specialty)}", $"Specialty: {SpecialtyName(d.specialty)}"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);
            nc.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var stats = UIKit.Column(card, 2);
            UIKit.Size(stats, 332, d.role == StaffRole.Hunter ? 96 : 70);
            if (d.role == StaffRole.Hunter)
            {
                UIKit.Amount(stats, "ic_time", L.T($"포획 주기 {d.interval}초", $"Cycle {d.interval}s"), UIKit.TS);
                UIKit.Amount(stats, "ic_space", L.T($"1회 {d.amount}마리", $"{d.amount} per catch"), UIKit.TS);
                UIKit.Amount(stats, "up_dura", L.T($"체력 {d.durability}", $"Stamina {d.durability}"), UIKit.TS);
            }
            else
            {
                UIKit.Amount(stats, "up_speed", L.T($"이동 ×{d.moveSpeed:0.00} (특화 ×{d.moveSpeed * GameData.SpecialtySpeed:0.00})", $"Speed ×{d.moveSpeed:0.00} (specialty ×{d.moveSpeed * GameData.SpecialtySpeed:0.00})"), UIKit.TS);
                if (d.foodBonus > 0 || d.souvenirBonus > 0) UIKit.Amount(stats, "up_sales", L.T($"음식 +{d.foodBonus * 100:0}% · 기념품 +{d.souvenirBonus * 100:0}%", $"Food +{d.foodBonus * 100:0}% · Gifts +{d.souvenirBonus * 100:0}%"), UIKit.TS);
                else if (d.admissionBonus > 0) UIKit.Amount(stats, "up_adm", L.T($"관람료 수거 +{d.admissionBonus * 100:0}%", $"Fee collection +{d.admissionBonus * 100:0}%"), UIKit.TS);
                else UIKit.Amount(stats, "ic_rp", L.T("연구소 작업 빠름", "Fast lab work"), UIKit.TS);
            }
            return card;
        }

        // ── 상점 ───────────────────────────────────────────────
        void BuildShopPanel(RectTransform c)
        {
            Tabs(c, new[] { L.T("미끼", "Bait"), L.T("음식 · 기념품", "Food & Gifts"), L.T("괴물 판매", "Sell"), L.T("재화 교환", "Exchange") }, new[] { "bait_worm", "food_2", "ic_gold", "ic_mat" }, shopTab, t => { shopTab = t; RebuildModal(); modalScroll.verticalNormalizedPosition = 1; });

            switch (shopTab)
            {
                case 0:
                {
                    UIKit.Note(c, L.T("미끼마다 잡히는 괴물이 다릅니다. 힘(약·중·강)과 조합해 보세요. ", "Each bait attracts different monsters. Combine with power (Low/Mid/High). ") + HoldHint);
                    var grid = UIKit.Grid(c, 270, 262);
                    for (int i = 0; i < GameData.Baits.Length; i++)
                    {
                        int bi = i;
                        var bd = GameData.Baits[i];
                        var card = UIKit.Card(grid, "ui_row", 12, 6);
                        var box = UIKit.Panel(card, "ui_inset", "IconBox");
                        UIKit.Size(box, 110, 110);
                        var ic = UIKit.Icon(box.transform, bd.id, 96);
                        UIKit.Stretch(ic.rectTransform, 7, 7, 7, 7);
                        UIKit.Label(card, bd.Name, UIKit.TM, TextAnchor.MiddleCenter, UIKit.Gold).GetComponent<LayoutElement>().flexibleWidth = 0;
                        var info = UIKit.Row(card, 28, 12);
                        info.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                        UIKit.Amount(info, "ic_gold", L.T($"{bd.price}/개", $"{bd.price} each"), UIKit.TS);
                        UIKit.Label(info, "", UIKit.TS, TextAnchor.MiddleCenter, UIKit.TextDim).Bind(() => L.T($"보유 {CountList.Get(g.S.baits, bd.id)}", $"Own {CountList.Get(g.S.baits, bd.id)}"));
                        UIKit.Size(info, 246);
                        var btns = UIKit.Row(card, 52, 8);
                        foreach (int n in new[] { 10, 50 })
                        {
                            int amt = n;
                            var b = UIKit.Button(btns, $"+{n}", null, 115, 52, Btn.Good, UIKit.TS).Hold(() => g.BuyBait(bi, amt));
                            b.BindEnabled(() => g.S.gold >= bd.price * amt);
                            b.Tip(L.T($"{bd.Name} {n}개 구입\n{bd.price * n:N0}골드", $"Buy {n} {bd.Name}\n{bd.price * n:N0} Gold"));
                        }
                    }
                    break;
                }
                case 1:
                    UIKit.Note(c, L.T("그 상품을 파는 가게(레벨 충족)가 있어야 살 수 있습니다. ", "You can only buy goods that one of your shops can sell. ") + HoldHint);
                    foreach (var it in GameData.ShopItems) GoodsRow(c, it);
                    break;
                case 2:
                    BuildSellGrid(c);
                    break;
                case 3:
                {
                    UIKit.Note(c, L.T("골드와 건설 자재는 1:1로 교환됩니다.", "Gold and Materials exchange 1:1."));
                    var row = UIKit.Row(c, 330, 16);
                    ExchangeCard(row, Currency.Gold, "ic_gold", "ic_mat", L.T("골드 → 자재", "Gold → Materials"));
                    ExchangeCard(row, Currency.Material, "ic_mat", "ic_gold", L.T("자재 → 골드", "Materials → Gold"));
                    break;
                }
            }
        }

        void ExchangeCard(Transform parent, Currency from, string fromIcon, string toIcon, string title)
        {
            var card = UIKit.Card(parent, "ui_row", 16, 10);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var head = UIKit.Row(card, 64, 12);
            head.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UIKit.Icon(head, fromIcon, 48);
            UIKit.Label(head, "→", UIKit.TL, TextAnchor.MiddleCenter, UIKit.TextDim, 50);
            UIKit.Icon(head, toIcon, 48);
            UIKit.Label(card, title, UIKit.TM, TextAnchor.MiddleCenter, UIKit.Gold).GetComponent<LayoutElement>().flexibleWidth = 0;
            foreach (int amt in new[] { 100, 1000, 10000 })
            {
                int a = amt;
                var b = UIKit.Button(card, $"{a:N0}", () => g.Exchange(from, a), Mobile ? 300 : 360, 50, Btn.Primary, UIKit.TM, fromIcon, 24);
                b.BindEnabled(() => (from == Currency.Gold ? g.S.gold : g.S.material) >= a);
            }
        }

        void BuildSellGrid(RectTransform c)
        {
            UIKit.Note(c, L.T("판매는 즉시 수익, 전시는 느리지만 계속 들어오는 수익입니다. 탭: 1마리 · ", "Selling pays now; exhibiting pays over time. Tap: sell 1 · ") + HoldHint);
            var grid = UIKit.Grid(c, 130, 150, 10);
            bool any = false;
            foreach (var m in GameData.Monsters)
            {
                int have = CountList.Get(g.S.monsters, m.id);
                if (have <= 0) continue;
                any = true;
                var id = m.id;
                var mm = m;
                var slot = Slot(grid, m.id, m.Name, () => $"×{CountList.Get(g.S.monsters, id)}", () => g.SellMonster(id, 1), Btn.Alt);
                slot.BindEnabled(() => CountList.Get(g.S.monsters, id) > 0);
                slot.Tip(() => $"{MonsterTip(mm)}\n\n" + L.T("탭: 1마리 판매\n", "Tap: sell 1\n") + HoldHint);
            }
            if (!any) UIKit.Note(c, L.T("판매할 괴물이 없습니다.", "No monsters to sell."));
        }

        // ── 보관함 ─────────────────────────────────────────────
        void BuildInventoryPanel(RectTransform c)
        {
            UIKit.Header(c, L.T("괴물 · 마초", "Monsters · Plants"), "ic_space");
            var mg = UIKit.Grid(c, 130, 150, 10);
            bool any = false;
            foreach (var m in GameData.Monsters)
            {
                int have = CountList.Get(g.S.monsters, m.id);
                if (have <= 0) continue;
                any = true;
                var mm = m;
                Slot(mg, m.id, m.Name, () => $"×{CountList.Get(g.S.monsters, mm.id)}", null).Tip(() => MonsterTip(mm) + L.T("\n\n전시 우리를 탭해 넣거나 [상점]에서 팝니다.", "\n\nTap a cage to exhibit, or sell in the [Shop]."));
            }
            if (!any) UIKit.Note(c, L.T("잡은 괴물이 없습니다.", "No monsters yet."));

            UIKit.Header(c, L.T("미끼", "Bait"), "bait_worm");
            var bg = UIKit.Grid(c, 130, 150, 10);
            foreach (var bd in GameData.Baits)
            {
                var b = bd;
                Slot(bg, bd.id, bd.Name, () => $"×{CountList.Get(g.S.baits, b.id)}", null).Tip(L.T($"{bd.Name}\n개당 {bd.price}골드", $"{bd.Name}\n{bd.price} Gold each"));
            }

            UIKit.Header(c, L.T("음식 · 기념품 재고", "Food & gift stock"), "food_1");
            var gg = UIKit.Grid(c, 130, 150, 10);
            foreach (var it in GameData.ShopItems)
            {
                var item = it;
                if (CountList.Get(g.S.goods, it.id) <= 0 && !g.CanSellItem(it)) continue;
                Slot(gg, it.id, it.Name, () => $"×{CountList.Get(g.S.goods, item.id)}", null).Tip(L.T($"{it.Name}\n{GameData.Buildings[it.shop].Name} Lv.{it.tier} 상품\n판매가 {it.sellPrice:N0}골드", $"{it.Name}\n{GameData.Buildings[it.shop].Name} Lv.{it.tier}\nSells for {it.sellPrice:N0} Gold"));
            }
        }

        // ── 연구 · 업그레이드 ──────────────────────────────────
        void BuildResearchPanel(RectTransform c)
        {
            var names = new string[GameData.CategoryCount + 1];
            var icons = new string[names.Length];
            names[0] = L.T("증축 · 탐사", "Expand"); icons[0] = "up_floor";
            string[] catIcons = { "ic_hunt", "ic_space", "hero_warrior", "up_lab" };
            for (int i = 0; i < GameData.CategoryCount; i++) { names[i + 1] = GameData.CategoryName(i); icons[i + 1] = catIcons[i]; }
            Tabs(c, names, icons, researchTab, t => { researchTab = t; RebuildModal(); modalScroll.verticalNormalizedPosition = 1; }, TabAffordable);

            var rp = UIKit.Row(c, 44, 16);
            UIKit.Amount(rp, "ic_rp", () => L.T($"보석 {g.S.rp:N0}", $"Gems {g.S.rp:N0}"), null, UIKit.TM, 32, UIKit.RpText);
            UIKit.Amount(rp, "ic_gold", () => $"{g.S.gold:N0}", null, UIKit.TM, 32, UIKit.Gold);
            if (!Mobile) UIKit.Label(rp, L.T("보석은 가장 귀한 재료입니다. 아껴 쓰세요!", "Gems are precious — spend them wisely!"), UIKit.TS, TextAnchor.MiddleRight, UIKit.TextDim);

            if (researchTab == 0)
            {
                UIKit.Header(c, L.T("성 증축", "Castle expansion"), "up_floor");
                ExpandRow(c, 0, false);
                ExpandRow(c, 1, false);
                UIKit.Header(c, L.T("스테이지 탐사", "Explore stages"), "up_region");
                foreach (var r in GameData.Research) ResearchRow(c, r);
                return;
            }
            int cat = researchTab - 1;
            foreach (var u in GameData.Upgrades) if (u.category == cat) UpgradeRow(c, u);
        }

        int TabAffordable(int tab)
        {
            int n = 0;
            if (tab == 0)
            {
                foreach (var r in GameData.Research) if (g.ResearchBlock(r) == null) n++;
                for (int w = 0; w < 2; w++) if (g.ExpandBlock(w) == null) n++;
                return n;
            }
            foreach (var u in GameData.Upgrades) if (u.category == tab - 1 && g.UpgradeBlock(u) == null) n++;
            return n;
        }

        void UpgradeRow(RectTransform c, UpgradeLine u)
        {
            int lv = g.UpgradeLevel(u);
            bool max = lv >= u.maxLevel;
            var row = UIKit.Row(c, 124, 16, true);

            var box = UIKit.Panel(row, "ui_inset", "IconBox");
            UIKit.Size(box, 92, 92);
            var ic = UIKit.Icon(box.transform, u.icon, 64);
            UIKit.Stretch(ic.rectTransform, 6, 6, 6, 6);

            var mid = UIKit.Column(row, 6);
            mid.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var title = UIKit.Row(mid, 34, 12);
            UIKit.Label(title, u.Name, UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold, 190);
            UIKit.Pips(title, lv, u.maxLevel);
            UIKit.Label(title, max ? "MAX" : $"{lv}/{u.maxLevel}", UIKit.TS, TextAnchor.MiddleLeft, max ? UIKit.Gold : UIKit.TextDim);
            string now = lv > 0 ? u.effect(lv) : L.T("효과 없음", "No effect");
            UIKit.Label(mid, L.T($"현재: {now}", $"Now: {now}"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextMain);
            UIKit.Label(mid, max ? L.T("모든 단계를 완료했습니다.", "All levels complete.") : L.T($"다음: {UIKit.Col(u.effect(lv + 1), UIKit.GoodText)}", $"Next: {UIKit.Col(u.effect(lv + 1), UIKit.GoodText)}"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);

            if (max)
            {
                var done = UIKit.Row(row, 60, 6);
                UIKit.Size(done, 260);
                done.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UIKit.Icon(done, "ic_star", 32);
                UIKit.Label(done, L.T("최대 단계", "Maxed"), UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold).GetComponent<LayoutElement>().flexibleWidth = 0;
                return;
            }
            var right = UIKit.Column(row, 6);
            UIKit.Size(right, 260);
            right.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UIKit.Size(UIKit.Cost(right, u.Gold(lv + 1), 0, u.Rp(lv + 1)), 260, 28);
            var btn = UIKit.Button(right, L.T($"{lv + 1}단계 강화", $"Level {lv + 1}"), () => g.DoUpgrade(u), 260, 54, Btn.Good, UIKit.TM);
            btn.BindEnabled(() => g.UpgradeBlock(u) == null);
            btn.Tip(() => g.UpgradeBlock(u) ?? $"{u.Name} {lv + 1}\n{u.effect(lv + 1)}");
        }

        void ResearchRow(RectTransform c, ResearchDef r)
        {
            var rd = r;
            bool done = g.S.research.Contains(r.id);
            var row = UIKit.Row(c, 100, 16, true);
            var box = UIKit.Panel(row, "ui_inset", "IconBox");
            UIKit.Size(box, 76, 76);
            var ic = UIKit.Icon(box.transform, r.icon, 64);
            UIKit.Stretch(ic.rectTransform, 6, 6, 6, 6);
            if (done) UIKit.Tint(ic, new Color(0.55f, 0.55f, 0.6f));

            var mid = UIKit.Column(row, 4);
            mid.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UIKit.Label(mid, r.Name, UIKit.TM, TextAnchor.MiddleLeft, done ? UIKit.TextDim : UIKit.Gold);
            UIKit.Label(mid, r.Desc, UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);

            if (done)
            {
                var d = UIKit.Row(row, 60, 6);
                UIKit.Size(d, 260);
                d.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                UIKit.Icon(d, "ic_check", 32);
                UIKit.Label(d, L.T("완료", "Done"), UIKit.TM, TextAnchor.MiddleLeft, UIKit.GoodText).GetComponent<LayoutElement>().flexibleWidth = 0;
                return;
            }
            var right = UIKit.Column(row, 6);
            UIKit.Size(right, 260);
            UIKit.Size(UIKit.Cost(right, r.gold, 0, r.rp), 260, 28);
            bool needsPrev = !string.IsNullOrEmpty(r.requires) && !g.S.research.Contains(r.requires);
            var btn = UIKit.Button(right, needsPrev ? L.T("선행 연구 필요", "Needs previous") : L.T("탐사", "Explore"), () => g.DoResearch(rd), 260, 54, Btn.Good, UIKit.TM, needsPrev ? "ic_lock" : null, 24);
            btn.BindEnabled(() => g.ResearchBlock(rd) == null);
            btn.Tip(() => g.ResearchBlock(rd) ?? rd.Desc);
        }

        // ── 도감 ───────────────────────────────────────────────
        void BuildCollectionPanel(RectTransform c)
        {
            UIKit.Note(c, L.T($"같은 괴물을 {GameData.CollectionRegisterCount}마리 포획하면 도감에 등록하고 골드와 보석(히든은 5개)을 받습니다.",
                              $"Catch {GameData.CollectionRegisterCount} of a monster to register it for Gold and Gems (5 for hidden ones)."));
            foreach (var r in GameData.Regions)
            {
                int found = 0, total = 0;
                foreach (var m in g.MonstersOf(r.id)) { total++; if (CountList.Get(g.S.caught, m.id) > 0) found++; }
                bool open = Modifiers.RegionUnlocked(g.S, r);
                var head = UIKit.Row(c, 46, 12);
                UIKit.Icon(head, open ? "up_region" : "ic_lock", 32);
                UIKit.Label(head, $"{r.stage}. {r.Name}", UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold, 280);
                int f = found, tt = total;
                UIKit.Bar(head, () => (float)f / tt, new Color(0.9f, 0.6f, 0.3f), Mobile ? 220 : 300, 26, () => L.T($"발견 {f}/{tt}", $"Found {f}/{tt}"));
                UIKit.Flex(head);
                if (!open && found == 0) { UIKit.Note(c, L.T($"스테이지 {r.stage} — [연구 > 증축 · 탐사]에서 탐사하면 괴물이 나타납니다.", $"Stage {r.stage} — explore it in [Research > Expand]."));  continue; }

                var grid = UIKit.Grid(c, 270, 250);
                foreach (var m in g.MonstersOf(r.id))
                {
                    var id = m.id;
                    int caught = CountList.Get(g.S.caught, m.id);
                    bool known = caught > 0;
                    bool claimed = g.S.collectionClaimed.Contains(id);
                    var card = UIKit.Card(grid, claimed ? "ui_panel" : "ui_row", 12, 6);
                    var box = UIKit.Panel(card, "ui_inset", "IconBox");
                    UIKit.Size(box, 96, 96);
                    var ic = UIKit.Icon(box.transform, m.id, 80);
                    UIKit.Stretch(ic.rectTransform, 8, 8, 8, 8);
                    if (!known) UIKit.Tint(ic, new Color(0.08f, 0.05f, 0.12f));
                    var name = UIKit.Label(card, known ? m.Name : m.hidden ? L.T("??? (히든)", "??? (hidden)") : "???", UIKit.TM, TextAnchor.MiddleCenter, known ? UIKit.TextMain : UIKit.TextDim);
                    UIKit.Size(name, 246, 28);
                    UIKit.Bar(card, () => (float)caught / GameData.CollectionRegisterCount, UIKit.GoodText, 246, 26,
                        () => $"{Mathf.Min(caught, GameData.CollectionRegisterCount)}/{GameData.CollectionRegisterCount}");
                    if (claimed)
                    {
                        var d = UIKit.Row(card, 44, 6);
                        d.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                        UIKit.Size(d, 246);
                        UIKit.Icon(d, "ic_check", 24);
                        UIKit.Label(d, L.T("등록 완료", "Registered"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.GoodText).GetComponent<LayoutElement>().flexibleWidth = 0;
                    }
                    else
                    {
                        var b = UIKit.Button(card, L.T($"등록 (보석 +{GameData.CollectionGems(m)})", $"Register (+{GameData.CollectionGems(m)} Gems)"), () => g.ClaimCollection(id), 246, 44, Btn.Accent, UIKit.TS, "ic_book", 24);
                        b.interactable = g.CanClaimCollection(id);
                    }
                    var mm = m;
                    var rr = r;
                    card.Tip(() => known ? MonsterTip(mm) : mm.hidden ? L.T("특별한 미끼와 힘 조합에서만 나타난다고 한다...", "Said to appear only with a special bait and power...") : HintFor(rr, mm));
                }
            }
        }

        // 미발견 괴물: 등장 조합 하나를 힌트로 보여준다
        static string HintFor(RegionDef r, MonsterDef m)
        {
            for (int b = 0; b < GameData.Baits.Length; b++)
                for (int p = 0; p < 3; p++)
                    if (Array.IndexOf(r.pools[b, p], m.id) >= 0)
                        return L.T($"힌트: {GameData.Baits[b].Name} + 힘 {GameData.PowerName(p)}", $"Hint: {GameData.Baits[b].Name} + {GameData.PowerName(p)} power");
            return "";
        }
    }
}

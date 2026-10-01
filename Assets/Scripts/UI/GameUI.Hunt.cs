using UnityEngine;
using UnityEngine.UI;

namespace Mawang
{
    // 포획장 화면 (원작: 낚시 탭). 마왕성 영역을 덮고, 스테이지 목록 / 포획 장면 / 포획대원 카드로 구성한다.
    public partial class GameUI
    {
        RectTransform huntRoot, stageList, huntHeader, hunterCards;
        HuntScene huntScene;
        RegionDef viewRegion;
        Button castleTab, huntTab; // 상단 화면 전환 탭
        GameObject stageLock; // 잠긴 스테이지 안내판

        public bool HuntVisible => huntRoot != null && huntRoot.gameObject.activeSelf;

        const float StageListW = 270f, HuntPad = 24f, HeaderH = 56f;
        const float CardsWideH = 224f, CardsNarrowH = 268f; // 카드가 넓으면 힘·등장 괴물을 한 줄로 → 장면이 커진다
        const float SideCardsW = 470f;                      // 낮은 화면(폰): 카드를 오른쪽 세로줄로
        const float SideModeMaxH = 720f;
        RectTransform sceneSlot, cardsView;
        ScrollRect cardsScroll;
        Vector2 huntBaseOffsetMax, huntBaseOffsetMin;
        bool wideCards, sideCards;
        RectTransform stageView;   // 스테이지 목록(폰에서는 [목록] 버튼으로 여닫는 덮개)
        bool stageListOpen;
        const float CompactCardsW = 360f, CompactPad = 12f, CompactHeaderH = 48f;

        void BuildHuntScreen(RectTransform area)
        {
            var bg = UIKit.Panel(frame, "ui_window", "HuntScreen");
            bg.raycastTarget = true; // 뒤의 마왕성 클릭 차단
            huntRoot = bg.rectTransform;
            // 마왕성 영역과 같은 자리(조금 더 크게)
            UIKit.Anchor(huntRoot, area.anchorMin, area.anchorMax, area.offsetMin - new Vector2(8, 4), area.offsetMax + new Vector2(8, 4));
            huntBaseOffsetMax = huntRoot.offsetMax;
            huntBaseOffsetMin = huntRoot.offsetMin;

            // 왼쪽: 스테이지 목록 (넘치면 스크롤)
            stageView = UIKit.Rect("StageView", huntRoot);
            UIKit.Anchor(stageView, Vector2.zero, new Vector2(0, 1), new Vector2(HuntPad, HuntPad), new Vector2(HuntPad + StageListW, -HuntPad));
            stageList = MakeScroll(stageView, 4, out _);

            float rx = HuntPad + StageListW + 16;
            // 오른쪽 위: 스테이지 제목줄
            huntHeader = UIKit.Rect("Header", huntRoot);
            UIKit.Anchor(huntHeader, new Vector2(0, 1), Vector2.one, new Vector2(rx, -HuntPad - HeaderH), new Vector2(-HuntPad, -HuntPad));

            // 장면: 남는 공간을 채우되 배경 비율(276:108)을 지킨다
            var slot = sceneSlot = UIKit.Rect("SceneSlot", huntRoot);
            UIKit.Anchor(slot, Vector2.zero, Vector2.one, new Vector2(rx, HuntPad + CardsNarrowH + 10), new Vector2(-HuntPad, -(HuntPad + HeaderH + 8)));
            var frameImg = UIKit.Panel(slot, "ui_inset", "SceneFrame");
            var fr = frameImg.rectTransform;
            UIKit.Stretch(fr);
            var fit = fr.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)GameData.StageW / GameData.StageH;
            var sceneHost = UIKit.Rect("Scene", fr);
            UIKit.Stretch(sceneHost, UIKit.P, UIKit.P, UIKit.P, UIKit.P);
            huntScene = sceneHost.gameObject.AddComponent<HuntScene>();
            huntScene.Init(sceneHost);

            // 포획대원 카드 (배치는 ApplyHuntLayout 에서 화면 높이에 따라)
            cardsView = UIKit.Rect("CardsView", huntRoot);
            hunterCards = MakeScroll(cardsView, 12, out cardsScroll);

            huntRoot.gameObject.SetActive(false);
            viewRegion = GameData.Regions[0];
        }

        // 세로 스크롤 목록: view 안에 content 를 만들어 돌려준다
        static RectTransform MakeScroll(RectTransform view, float spacing, out ScrollRect scroll)
        {
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 빈 곳도 끌 수 있게
            var content = UIKit.Column(view, spacing);
            content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        // 배치 3가지
        //  PC(높은 화면): 스테이지 목록 | 장면 위, 카드 3장 아래 가로
        //  낮은 PC 창: 사이드바 자리까지 쓰고 카드는 오른쪽 세로줄
        //  폰: 하단 메뉴를 숨기고, 스테이지 목록은 [목록] 덮개 + ◀▶, 장면 | 카드 세로줄
        void ApplyHuntLayout()
        {
            bool compact = Mobile;
            float pad = compact ? CompactPad : HuntPad, headH = compact ? CompactHeaderH : HeaderH;
            huntRoot.offsetMax = huntBaseOffsetMax;
            huntRoot.offsetMin = compact ? new Vector2(huntBaseOffsetMin.x, 4) : huntBaseOffsetMin;
            sideCards = compact || huntRoot.rect.height < SideModeMaxH;
            float rx = compact ? pad : pad + StageListW + 16;
            float top = -(pad + headH + 8);

            // 스테이지 목록
            var listBg = stageView.GetComponent<Image>();
            var listVl = stageList.GetComponent<VerticalLayoutGroup>();
            if (compact)
            {
                UIKit.Anchor(stageView, Vector2.zero, new Vector2(0, 1), new Vector2(pad, pad), new Vector2(pad + StageListW + 16, top + 4));
                UIKit.SetSliced(listBg, "ui_panel");
                listBg.color = Color.white;
                listVl.padding = new RectOffset(8, 8, 8, 8);
                stageView.gameObject.SetActive(stageListOpen);
                stageView.SetAsLastSibling();
            }
            else
            {
                UIKit.Anchor(stageView, Vector2.zero, new Vector2(0, 1), new Vector2(pad, pad), new Vector2(pad + StageListW, -pad));
                listBg.sprite = null;
                listBg.color = new Color(0, 0, 0, 0.001f);
                listVl.padding = new RectOffset(0, 0, 0, 0);
                stageView.gameObject.SetActive(true);
            }

            UIKit.Anchor(huntHeader, new Vector2(0, 1), Vector2.one, new Vector2(rx, -pad - headH), new Vector2(-pad, -pad));

            float cardsW = compact ? CompactCardsW : SideCardsW;
            if (sideCards)
            {
                huntRoot.offsetMax = new Vector2(-SideMargin, huntBaseOffsetMax.y); // 사이드바 자리까지 덮는다(내용이 카드와 같다)
                UIKit.Anchor(cardsView, new Vector2(1, 0), Vector2.one, new Vector2(-pad - cardsW, pad), new Vector2(-pad, top));
                UIKit.Anchor(sceneSlot, Vector2.zero, Vector2.one, new Vector2(rx, pad), new Vector2(-(pad + cardsW + 10), top));
                cardsScroll.enabled = true;
                wideCards = !compact;
            }
            else
            {
                float cardW = (huntRoot.rect.width - rx - pad - 24f) / 3f;
                wideCards = cardW >= 440f;
                float cardsH = wideCards ? CardsWideH : CardsNarrowH;
                UIKit.Anchor(cardsView, Vector2.zero, new Vector2(1, 0), new Vector2(rx, pad), new Vector2(-pad, pad + cardsH));
                UIKit.Anchor(sceneSlot, Vector2.zero, Vector2.one, new Vector2(rx, pad + cardsH + 10), new Vector2(-pad, top));
                cardsScroll.enabled = false;
            }
            // 카드 목록의 배치 방향 바꾸기
            var content = hunterCards;
            var old = content.GetComponent<HorizontalOrVerticalLayoutGroup>();
            bool wantVertical = sideCards;
            if (old != null && (old is VerticalLayoutGroup) != wantVertical) { DestroyImmediate(old); old = null; }
            HorizontalOrVerticalLayoutGroup lg = old ?? (wantVertical ? (HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<VerticalLayoutGroup>() : content.gameObject.AddComponent<HorizontalLayoutGroup>());
            lg.spacing = sideCards ? 8 : 12;
            lg.childAlignment = TextAnchor.UpperLeft;
            lg.childControlWidth = lg.childControlHeight = true;
            lg.childForceExpandWidth = true;
            lg.childForceExpandHeight = !sideCards;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = sideCards ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            if (!sideCards) UIKit.Stretch(content);
            else { content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = new Vector2(0, content.offsetMin.y); content.offsetMax = new Vector2(0, content.offsetMax.y); }
        }

        void ToggleStageList()
        {
            stageListOpen = !stageListOpen;
            RebuildHunt();
        }

        void StepStage(int dir)
        {
            int i = GameData.Regions.IndexOf(viewRegion);
            i = (i + dir + GameData.Regions.Count) % GameData.Regions.Count;
            SelectStage(GameData.Regions[i]);
        }

        public void ShowHunt(string regionId = null)
        {
            CloseModal();
            castle.CancelPlacing();
            if (regionId != null) viewRegion = GameData.RegionById(regionId);
            huntRoot.gameObject.SetActive(true);
            if (Mobile) dockBar.gameObject.SetActive(false); // 폰: 장면에 높이를 준다
            huntScene.SetRegion(viewRegion);
            SetTabs(true);
            RebuildHunt();
        }

        public void HideHunt()
        {
            huntRoot.gameObject.SetActive(false);
            dockBar.gameObject.SetActive(true);
            SetTabs(false);
        }

        void SetTabs(bool hunt)
        {
            if (castleTab != null) castleTab.SetStyle(hunt ? Btn.Alt : Btn.Accent);
            if (huntTab != null) huntTab.SetStyle(hunt ? Btn.Accent : Btn.Alt);
        }

        void SelectStage(RegionDef r)
        {
            viewRegion = r;
            stageListOpen = false;
            huntScene.SetRegion(r);
            RebuildHunt();
        }

        void RebuildHunt()
        {
            if (!HuntVisible) return;
            ApplyHuntLayout();
            BuildStageList();
            BuildHuntHeader();
            BuildHunterCards();
        }

        // ── 스테이지 목록 ──────────────────────────────────────
        void BuildStageList()
        {
            UIKit.Clear(stageList);
            foreach (var r in GameData.Regions)
            {
                var rr = r;
                bool open = Modifiers.RegionUnlocked(g.S, r);
                var research = GameData.ResearchById(r.unlockResearch ?? "");
                bool explorable = !open && research != null && (string.IsNullOrEmpty(research.requires) || g.S.research.Contains(research.requires));
                var b = UIKit.Button(stageList, "", () => SelectStage(rr), StageListW, 66, viewRegion == r ? Btn.Accent : open ? Btn.Alt : Btn.Primary, UIKit.TS);
                var content = b.Content();
                for (int k = content.childCount - 1; k >= 0; k--) DestroyImmediate(content.GetChild(k).gameObject);
                content.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 0, 0);

                var num = UIKit.Panel(content, "ui_inset", "No");
                UIKit.Size(num, 44, 44);
                var nt = UIKit.Label(num.transform, open ? r.stage.ToString() : "", UIKit.TM, TextAnchor.MiddleCenter, UIKit.Gold);
                UIKit.Stretch(nt.rectTransform);
                if (!open) { var li = UIKit.Icon(num.transform, "ic_lock", 32); UIKit.Stretch(li.rectTransform, 6, 6, 6, 6); }

                var col = UIKit.Column(content, 2);
                col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                UIKit.Label(col, r.Name, UIKit.TS, TextAnchor.MiddleLeft, open ? UIKit.TextMain : UIKit.TextDim);
                var sub = UIKit.Row(col, 22, 4);
                if (open)
                {
                    foreach (var h in g.S.hunters)
                        if (h.regionId == r.id && (h.dispatched || h.restTimer > 0)) UIKit.Icon(sub, h.staffId, 20);
                    var trash = UIKit.Amount(sub, "trash_can", () => $"{g.TrashCount(rr.id)}", null, UIKit.TS, 20, UIKit.TextDim);
                    trash.gameObject.AddComponent<Binding>().Init(() => { bool on = g.TrashCount(rr.id) > 0; if (trash.gameObject.activeSelf != on) trash.gameObject.SetActive(on); });
                }
                else UIKit.Label(sub, explorable ? L.T("탐사 가능", "Explorable") : L.T("잠김", "Locked"), UIKit.TS, TextAnchor.MiddleLeft, explorable ? UIKit.GoodText : UIKit.TextDim);

                b.Tip(() => open ? $"{rr.Name}\n{rr.Desc}" : L.T($"{rr.Name}\n[연구 > 증축 · 탐사]에서 탐사하면 열립니다.", $"{rr.Name}\nExplore it in [Research > Expand]."));
            }
        }

        // ── 제목줄 ─────────────────────────────────────────────
        void BuildHuntHeader()
        {
            UIKit.Clear(huntHeader);
            var r = viewRegion;
            var row = UIKit.Row(huntHeader, 56, Mobile ? 8 : 14);
            UIKit.Stretch(row);
            if (Mobile)
            {
                UIKit.Button(row, L.T("목록", "List"), ToggleStageList, 100, 44, stageListOpen ? Btn.Accent : Btn.Alt, UIKit.TS, "up_region", 24).Tip(L.T("스테이지 목록", "Stage list"));
                UIKit.Button(row, "<", () => StepStage(-1), 48, 44, Btn.Alt, UIKit.TM).Tip(L.T("이전 스테이지", "Previous stage"));
                UIKit.Button(row, ">", () => StepStage(+1), 48, 44, Btn.Alt, UIKit.TM).Tip(L.T("다음 스테이지", "Next stage"));
            }
            UIKit.Label(row, $"STAGE {r.stage}", UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 100);
            var name = UIKit.Label(row, r.Name, UIKit.TL, TextAnchor.MiddleLeft, UIKit.Gold);
            name.GetComponent<LayoutElement>().flexibleWidth = 0;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (Mobile) UIKit.Flex(row); else UIKit.Label(row, r.Desc, UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);
            int found = 0, total = 0;
            foreach (var m in g.MonstersOf(r.id)) { total++; if (CountList.Get(g.S.caught, m.id) > 0) found++; }
            UIKit.Amount(row, "ic_book", $"{found}/{total}", UIKit.TS, 28).Tip(L.T("이 스테이지에서 발견한 괴물 수", "Monsters found in this stage"));
            UIKit.Amount(row, "trash_can", () => $"{g.TrashCount(r.id)}", null, UIKit.TS, 28).Tip(L.T("바닥에 떨어진 쓰레기. 장면에서 탭해 주우세요.", "Trash on the ground. Tap it in the scene."));

            // 잠긴 스테이지: 장면 위에 탐사 안내
            if (stageLock != null) DestroyImmediate(stageLock);
            if (Modifiers.RegionUnlocked(g.S, r)) return;
            var cover = UIKit.Box(huntScene.transform.parent, new Color(0.03f, 0.01f, 0.05f, 0.82f), "Lock");
            stageLock = cover.gameObject;
            cover.raycastTarget = true;
            UIKit.Stretch(cover.rectTransform, UIKit.P, UIKit.P, UIKit.P, UIKit.P);
            var col = UIKit.Column(cover.transform, 12);
            UIKit.Stretch(col, 0, 120, 0, 120);
            col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            col.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            UIKit.Icon(col, "ic_lock", 64);
            UIKit.Label(col, L.T("아직 탐사하지 않은 스테이지", "Unexplored stage"), UIKit.TM, TextAnchor.MiddleCenter, UIKit.TextMain).GetComponent<LayoutElement>().flexibleWidth = 0;
            var res = GameData.ResearchById(r.unlockResearch);
            if (res == null) return;
            UIKit.Cost(col, res.gold, 0, res.rp);
            var btn = UIKit.Button(col, $"{res.Name}", () => { g.DoResearch(res); RebuildHunt(); huntScene.SetRegion(viewRegion); }, 320, 56, Btn.Good, UIKit.TM, "up_region", 32);
            var block = g.ResearchBlock(res);
            bool needPrev = !string.IsNullOrEmpty(res.requires) && !g.S.research.Contains(res.requires);
            btn.interactable = block == null;
            if (block != null) btn.Tip(needPrev ? L.T($"먼저 이전 스테이지를 탐사하세요.\n({block})", $"Explore the previous stage first.\n({block})") : block);
        }

        // ── 포획대원 카드 ──────────────────────────────────────
        void BuildHunterCards()
        {
            UIKit.Clear(hunterCards);
            foreach (var h in g.S.hunters) HunterCard(h);
            for (int i = g.S.hunters.Count; i < 3 && !sideCards; i++) // 좁은 세로줄에서는 빈 자리 카드 생략
            {
                var empty = UIKit.Card(hunterCards, "ui_inset", 16, 8, TextAnchor.MiddleCenter);
                UIKit.Icon(empty, "ic_lock", 48);
                UIKit.Label(empty, L.T("빈 자리\n[직원]에서 포획대원을 고용하세요.", "Empty slot\nHire hunters in [Staff]."), UIKit.TS, TextAnchor.MiddleCenter, UIKit.TextDim).GetComponent<LayoutElement>().flexibleWidth = 0;
            }
        }

        void HunterCard(HunterState h)
        {
            var hs = h;
            var def = g.Def(h);
            var r = viewRegion;
            bool open = Modifiers.RegionUnlocked(g.S, r);
            bool here = h.regionId == r.id;
            bool locked = h.dispatched;
            var poolRegion = h.dispatched || h.restTimer > 0 ? GameData.RegionById(h.regionId) : r;

            var card = UIKit.Card(hunterCards, here && (h.dispatched || h.restTimer > 0) ? "ui_panel" : "ui_row", 12, 6, TextAnchor.UpperLeft);
            card.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

            var head = UIKit.Row(card, 52, 10);
            var portrait = UIKit.Panel(head, "ui_inset", "Portrait");
            UIKit.Size(portrait, 52, 52);
            var pi = UIKit.Icon(portrait.transform, def.id, 48);
            UIKit.Stretch(pi.rectTransform, 4, 4, 4, 4);
            var nc = UIKit.Column(head, 0);
            nc.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UIKit.Label(nc, def.Name, UIKit.TM, TextAnchor.MiddleLeft, UIKit.Gold);
            UIKit.Label(nc, h.dispatched || h.restTimer > 0 ? $"@ {GameData.RegionById(h.regionId).Name}" : L.T("마왕성 대기", "At the castle"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim);
            UIKit.Amount(head, "up_dura", () => $"{hs.durabilityLeft}/{g.HuntDurabilityMax(hs)}", null, UIKit.TS, 20).Tip(L.T("체력: 포획 1회마다 1 소모. 0이 되면 쉬었다가 자동으로 다시 나갑니다.", "Stamina: -1 per catch. At 0 they rest, then head out again."));
            if (wideCards) HunterAction(head, h, r, open, here, 50);

            var st = UIKit.Row(card, 26, 6);
            UIKit.Bar(st, () => HunterProgress(hs), hs.restTimer > 0 ? new Color(0.6f, 0.6f, 1f) : new Color(0.45f, 0.85f, 0.5f), -1, 26, () => g.HunterStatus(hs));

            // 미끼
            var baitRow = UIKit.Row(card, 46, 4);
            for (int i = 0; i < GameData.Baits.Length; i++)
            {
                int bi = i;
                var bd = GameData.Baits[i];
                var b = UIKit.Button(baitRow, $"{CountList.Get(g.S.baits, bd.id)}", () => { hs.bait = bi; Sound.Play("click"); g.MarkDirty(); }, Mobile ? 62 : 76, 46, h.bait == bi ? Btn.Accent : Btn.Alt, UIKit.TS, bd.id, 28);
                b.interactable = !locked || h.bait == bi;
                b.Tip(L.T($"{bd.Name}  (보유 {CountList.Get(g.S.baits, bd.id)})\n미끼에 따라 다가오는 괴물이 달라집니다.", $"{bd.Name}  (own {CountList.Get(g.S.baits, bd.id)})\nDifferent bait attracts different monsters."));
            }
            UIKit.Button(baitRow, h.autoBait ? L.T("자동", "Auto") : L.T("수동", "Man."), () => { hs.autoBait = !hs.autoBait; Sound.Play("click"); g.MarkDirty(); }, Mobile ? 54 : 60, 46, h.autoBait ? Btn.Good : Btn.Alt, UIKit.TS)
                .Tip(h.autoBait ? L.T("자동 교체 ON: 미끼가 떨어지면 다른 미끼를 씁니다.", "Auto-switch ON: uses other bait when out.") : L.T("자동 교체 OFF: 미끼가 떨어지면 포획을 멈춥니다.", "Auto-switch OFF: stops when out of bait."));

            // 힘 (넓은 카드면 등장 괴물과 같은 줄)
            var powerRow = UIKit.Row(card, 44, 4);
            if (!Mobile) UIKit.Label(powerRow, L.T("힘", "Pow"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 28);
            string[] powerTip = { L.T("약하게: 작은 괴물이 잘 나옵니다.", "Low: small monsters appear more."), L.T("보통으로", "Medium"), L.T("강하게: 큰 괴물이 잘 나옵니다.", "High: big monsters appear more.") };
            for (int p = 0; p < 3; p++)
            {
                var pw = (Power)p;
                var b = UIKit.Button(powerRow, GameData.PowerName(p), () => { hs.power = pw; Sound.Play("click"); g.MarkDirty(); }, Mobile ? 56 : 60, 44, h.power == pw ? Btn.Accent : Btn.Alt, UIKit.TM);
                b.interactable = !locked || h.power == pw;
                b.Tip(powerTip[p] + L.T("\n미끼 × 힘 조합이 등장 괴물을 정합니다.", "\nBait × power decides which monsters appear."));
            }
            if (!wideCards) { UIKit.Flex(powerRow); HunterAction(powerRow, h, r, open, here, 44); } // 좁은 카드: 힘 줄 오른쪽 빈자리

            // 등장 괴물 미리보기
            RectTransform pool = powerRow;
            if (wideCards) UIKit.Rect("Gap", powerRow).gameObject.AddComponent<LayoutElement>().preferredWidth = 12;
            else pool = UIKit.Row(card, 44, 6);
            UIKit.Label(pool, L.T("등장", "Pool"), UIKit.TS, TextAnchor.MiddleLeft, UIKit.TextDim, 44);
            if (Modifiers.RegionUnlocked(g.S, poolRegion))
                foreach (var id in g.PreviewPool(poolRegion.id, h.bait, h.power))
                {
                    var m = GameData.MonsterById[id];
                    bool known = CountList.Get(g.S.caught, id) > 0;
                    var chip = UIKit.Panel(pool, "ui_inset", "Chip");
                    chip.raycastTarget = true;
                    UIKit.Size(chip, 44, 44);
                    var ic = UIKit.Icon(chip.transform, m.id, 36);
                    UIKit.Stretch(ic.rectTransform, 4, 4, 4, 4);
                    if (!known) UIKit.Tint(ic, new Color(0.08f, 0.05f, 0.12f));
                    chip.Tip(known ? MonsterTip(m) : L.T("아직 잡은 적 없는 괴물", "Not caught yet"));
                }

        }

        // 행동 버튼: 파견 / 복귀 / 다른 스테이지 보러 가기 / 회복 대기
        void HunterAction(Transform head, HunterState h, RegionDef r, bool open, bool here, float Hh)
        {
            var hs = h;
            var def = g.Def(h);
            const float W = 128;
            if (!open && !(h.dispatched || h.restTimer > 0))
            {
                var b = UIKit.Button(head, L.T("탐사 필요", "Locked"), null, W, Hh, Btn.Alt, UIKit.TS);
                b.interactable = false;
            }
            else if (h.dispatched && here)
                UIKit.Button(head, L.T("복귀", "Recall"), () => g.Recall(hs), W, Hh, Btn.Danger, UIKit.TM).Tip(L.T($"수동 복귀하면 {GameData.HunterRecallPenalty:0}초 동안 다시 보낼 수 없습니다.\n쓰던 미끼는 돌려받습니다.", $"After a manual recall you cannot dispatch for {GameData.HunterRecallPenalty:0}s.\nThe bait in use is refunded."));
            else if (h.dispatched || (h.restTimer > 0 && !h.penalty && !here))
            {
                var other = GameData.RegionById(h.regionId);
                UIKit.Button(head, L.T("보러 가기", "Go watch"), () => SelectStage(other), W, Hh, Btn.Alt, UIKit.TS).Tip(L.T($"{other.Name}에서 포획 중입니다.", $"Hunting in {other.Name}."));
            }
            else if (h.restTimer > 0)
            {
                var b = UIKit.Button(head, "", null, W, Hh, Btn.Alt, UIKit.TS);
                b.interactable = false;
                b.Content().GetComponentInChildren<Text>().Bind(() => hs.restTimer > 0 ? (hs.penalty ? L.T($"대기 {hs.restTimer:0}초", $"Wait {hs.restTimer:0}s") : L.T($"회복 {hs.restTimer:0}초", $"Rest {hs.restTimer:0}s")) : L.T("준비 완료", "Ready"));
                b.Tip(h.penalty ? L.T("수동 복귀 대기 중", "Recall cooldown") : L.T("체력 회복 중. 끝나면 자동으로 다시 나갑니다.", "Resting. Heads out again automatically."));
            }
            else
            {
                var b = UIKit.Button(head, L.T("파견", "Send"), () => { g.Assign(hs, r.id); g.Dispatch(hs); }, W, Hh, Btn.Good, UIKit.TM, "ic_hunt", 28);
                b.Tip(L.T($"{def.Name}을(를) {r.Name}에 보냅니다.\n주기 {g.HuntInterval(h):0.#}초 · 1회 {def.amount}마리", $"Send {def.Name} to {r.Name}.\nCycle {g.HuntInterval(h):0.#}s · {def.amount} per catch"));
            }
        }
    }
}

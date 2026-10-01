using UnityEngine;
using UnityEngine.UI;

namespace Mawang
{
    // 처음 켰을 때 보여 주는 짧은 튜토리얼 (카드 5장, 언제든 건너뛰기). 설정에서 다시 볼 수 있다.
    public partial class GameUI
    {
        RectTransform tutorialRoot;
        int tutorialPage;

        public bool TutorialVisible => tutorialRoot != null && tutorialRoot.gameObject.activeSelf;

        struct TutorialPage { public string icon, title, body; }

        static TutorialPage[] TutorialPages => new[]
        {
            new TutorialPage { icon = "up_floor", title = L.T("마왕성에 오신 것을 환영합니다!", "Welcome to the Demon Castle!"),
                body = L.T("괴물을 잡아 전시하고, 구경 온 용사들에게\n음식과 기념품을 팔아 마왕성을 키우세요.",
                           "Catch monsters, put them on display,\nand sell food and gifts to visiting heroes.") },
            new TutorialPage { icon = "ic_build", title = L.T("짓기", "Building"),
                body = Mobile
                    ? L.T("빈 칸(+)을 탭하면 건물을 지을 수 있어요.\n전시 우리는 [건설]에서 고른 뒤 끌면 여러 칸을 하나로 지어요.",
                          "Tap an empty cell (+) to build.\nPick the Exhibit Cage in [Build] and drag to make one big cage.")
                    : L.T("빈 칸(+)을 클릭하면 건물을 지을 수 있어요.\n전시 우리는 [건설]에서 고른 뒤 끌면 여러 칸을 하나로 지어요.",
                          "Click an empty cell (+) to build.\nPick the Exhibit Cage in [Build] and drag to make one big cage.") },
            new TutorialPage { icon = "ic_gold", title = L.T("짧게 · 길게", "Tap & Hold"),
                body = Mobile
                    ? L.T("짧게 탭: 우리 관람료 수령 · 가게에서 기다리는 손님에게 판매\n꾹 누르기: 업그레이드·관리 창\n목록에서 꾹 누르면 빠르게 연속으로 사고팝니다.",
                          "Tap: collect cage fees · sell to waiting shop guests\nHold: upgrade & manage\nHold a list item to buy/sell repeatedly.")
                    : L.T("좌클릭: 우리 관람료 수령 · 가게에서 기다리는 손님에게 판매\n우클릭: 업그레이드·관리 창\n목록에서 우클릭을 누르고 있으면 빠르게 연속으로 사고팝니다.",
                          "Left-click: collect cage fees · sell to waiting shop guests\nRight-click: upgrade & manage\nHold right-click on a list item to buy/sell repeatedly.") },
            new TutorialPage { icon = "ic_hunt", title = L.T("포획장", "Hunting"),
                body = L.T("위쪽 [포획장] 탭에서 포획대원을 파견하세요.\n미끼 × 힘 조합에 따라 다른 괴물이 잡힙니다.",
                           "Open the [Hunt] tab and dispatch a hunter.\nThe bait × power combo decides which monsters appear.") },
            new TutorialPage { icon = "ic_rp", title = L.T("보석은 가장 귀한 재료", "Gems are precious"),
                body = L.T("흑마법 연구소가 보석을 아주 천천히 만듭니다.\n보석으로 성을 증축(좌우 날개 10층)하고 새 지역을 탐사하세요.\n[직원]을 파견하면 판매·수거를 대신해 줍니다.",
                           "The Dark Magic Lab slowly makes Gems.\nSpend them to expand the castle (two wings, 10 floors) and explore.\nDispatch [Staff] to sell and collect for you.") },
        };

        public void ShowTutorial()
        {
            if (tutorialRoot == null) BuildTutorial();
            tutorialPage = 0;
            tutorialRoot.gameObject.SetActive(true);
            tutorialRoot.SetAsLastSibling();
            FillTutorial();
        }

        void BuildTutorial()
        {
            tutorialRoot = UIKit.Rect("Tutorial", frame);
            UIKit.Stretch(tutorialRoot);
            var dim = UIKit.Box(tutorialRoot, new Color(0.02f, 0.01f, 0.04f, 0.7f), "Dim");
            dim.raycastTarget = true;
            UIKit.Stretch(dim.rectTransform);
            tutorialRoot.gameObject.SetActive(false);
        }

        void FillTutorial()
        {
            // 카드만 새로 만든다 (어두운 배경은 그대로)
            for (int i = tutorialRoot.childCount - 1; i >= 1; i--) { var c = tutorialRoot.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            var pages = TutorialPages;
            var p = pages[tutorialPage];

            var win = UIKit.Panel(tutorialRoot, "ui_window", "Card");
            win.raycastTarget = true;
            var rt = win.rectTransform;
            UIKit.Place(rt, 0, 0, Mobile ? 720 : 980, Mobile ? 350 : 460, new Vector2(0.5f, 0.5f));
            var col = UIKit.Column(win.transform, 14);
            UIKit.Stretch(col, 40, Mobile ? 22 : 30, 40, Mobile ? 26 : 36);
            var vl = col.GetComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childForceExpandWidth = true;

            var head = UIKit.Row(col, 72, 14);
            head.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UIKit.Icon(head, p.icon, 64);
            var title = UIKit.Label(head, p.title, UIKit.TL, TextAnchor.MiddleLeft, UIKit.Gold);
            title.GetComponent<LayoutElement>().flexibleWidth = 0;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            var body = UIKit.Label(col, p.body, UIKit.TM, TextAnchor.UpperCenter, UIKit.TextMain);
            var ble = body.GetComponent<LayoutElement>();
            ble.flexibleHeight = 1;

            // 쪽 표시
            var dots = UIKit.Row(col, 20, 8);
            dots.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < pages.Length; i++)
            {
                var d = UIKit.Box(dots, i == tutorialPage ? UIKit.Gold : new Color(0.35f, 0.3f, 0.42f), "Dot");
                UIKit.Size(d, 14, 14);
            }

            var btns = UIKit.Row(col, 60, 16);
            btns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UIKit.Button(btns, L.T("건너뛰기", "Skip"), CloseTutorial, 200, 56, Btn.Alt, UIKit.TM);
            bool last = tutorialPage == pages.Length - 1;
            UIKit.Button(btns, last ? L.T("시작하기!", "Let's go!") : L.T("다음", "Next"), () =>
            {
                if (last) { CloseTutorial(); return; }
                tutorialPage++;
                FillTutorial();
            }, 240, 56, Btn.Good, UIKit.TM, last ? "ic_star" : null, 32);

            win.gameObject.AddComponent<PopIn>();
        }

        void CloseTutorial()
        {
            if (tutorialRoot != null) tutorialRoot.gameObject.SetActive(false);
            g.S.tutorialDone = true;
            g.Save();
        }
    }
}

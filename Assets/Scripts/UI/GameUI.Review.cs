using UnityEngine;
using UnityEngine.UI;

namespace Mawang
{
    // 리뷰 요청 창: 목표가 Review.PromptGoalStep 단계에 닿으면 한 번만 띄운다 (보상 없음). 설정에서도 다시 열 수 있다.
    public partial class GameUI
    {
        float reviewCheck;

        // 다른 창·튜토리얼·배치 중에는 끼어들지 않는다
        void TickReviewPrompt()
        {
            reviewCheck -= Time.unscaledDeltaTime;
            if (reviewCheck > 0) return;
            reviewCheck = 1f;
            if (Review.Asked || !Review.Available) return;
            int step = Review.PromptGoalStep;
            if (step < 0 || g.S.goalStep < step) return;
            if (modalBuilder != null || TutorialVisible || castle.placing != null) return;
            Review.Asked = true;
            OpenReview();
        }

        void OpenReview()
        {
            Sound.Play("open");
            OpenModal(L.T("리뷰 부탁드려요", "Enjoying the game?"), BuildReviewPanel, "ic_star");
        }

        void BuildReviewPanel(RectTransform c)
        {
            UIKit.Spacer(c, 12);
            UIKit.Label(UIKit.Row(c, 60).transform, L.T("마왕성 경영은 즐거우신가요?", "Enjoying running the Demon Castle?"), UIKit.TM, TextAnchor.MiddleCenter);
            UIKit.Label(UIKit.Row(c, 60).transform, L.T("스토어에 리뷰를 남겨 주시면 개발에 큰 힘이 됩니다.", "A store review would really help us keep making the game."), UIKit.TS, TextAnchor.MiddleCenter, UIKit.TextDim);
            UIKit.Spacer(c, 16);
            var row = UIKit.Row(c, 60, 20);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UIKit.Button(row, L.T("리뷰 쓰기", "Write a review"), () => { CloseModal(); Review.Open(); }, 280, 56, Btn.Good, UIKit.TM, "ic_star");
            UIKit.Button(row, L.T("나중에", "Later"), CloseModal, 180, 56, Btn.Alt, UIKit.TM);
        }
    }
}

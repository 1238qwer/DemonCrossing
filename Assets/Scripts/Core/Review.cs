using UnityEngine;

namespace Mawang
{
    // 스토어 리뷰 요청: 초반 목표를 몇 단계 마친 뒤 한 번만 띄운다 (GameUI.Review).
    // 리뷰 보상은 주지 않는다 — 구글 플레이·스팀은 '리뷰를 쓰면 보상'을 정책으로 금지한다.
    // 물어봤는지는 세이브가 아니라 PlayerPrefs 에 둔다 — '새로 시작'을 해도 다시 묻지 않는다.
    // 스토어: 원스토어 빌드(ONESTORE) → 원스토어 앱, 그 외 안드로이드 → 구글 플레이, 윈도우 exe → 원스토어 웹 페이지.
    // 스팀 빌드는 STEAM 심볼을 켜고 SteamAppId 를 채우면 스팀 상점 페이지를 연다.
    public static class Review
    {
        public const string OneStoreProductId = "0001009768"; // 원스토어 상품 ID (놀러와요 마왕의 성)
        public const string SteamAppId = "";                  // 스팀에 올리면 앱 ID
        const string AskedKey = "review_asked";

        // 이 목표(보석으로 업그레이드 하나)를 포함해 몇 단계를 마치면 묻는가
        public const int StepsAfterFirstResearch = 3;

        public static string Url
        {
            get
            {
#if STEAM
                return string.IsNullOrEmpty(SteamAppId) ? null : $"https://store.steampowered.com/app/{SteamAppId}/";
#elif UNITY_ANDROID && ONESTORE
                return $"onestore://common/product/{OneStoreProductId}";
#elif UNITY_ANDROID
                return $"https://play.google.com/store/apps/details?id={Application.identifier}"; // 플레이 스토어 앱이 받아서 연다
#else
                return $"https://m.onestore.co.kr/v2/ko-kr/app/{OneStoreProductId}";
#endif
            }
        }

        public static bool Available => !string.IsNullOrEmpty(Url);

        public static bool Asked
        {
            get => PlayerPrefs.GetInt(AskedKey, 0) == 1;
            set { PlayerPrefs.SetInt(AskedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // 리뷰를 물어볼 목표 단계 번호: '아무 업그레이드 연구' 단계 + StepsAfterFirstResearch. 그 단계가 없으면 -1.
        public static int PromptGoalStep
        {
            get
            {
                var steps = GoalSettings.I.steps;
                for (int i = 0; i < steps.Count; i++)
                    if (steps[i] != null && steps[i].enabled && steps[i].kind == GoalKind.AnyResearch) return i + StepsAfterFirstResearch;
                return -1;
            }
        }

        public static void Open()
        {
            Asked = true;
            if (Available) Application.OpenURL(Url);
        }
    }
}

using UnityEngine;

namespace Mawang
{
    // 폰의 노치·펀치홀·둥근 모서리를 피해 UI 틀을 안전 영역에 맞춘다. 화면 회전·크기 변경도 따라간다.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform rt;
        Rect last;
        Vector2Int lastScreen;

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update() { if (Screen.safeArea != last || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply(); }

        void Apply()
        {
            last = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height);
            rt.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}

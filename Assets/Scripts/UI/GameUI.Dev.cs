#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Mawang
{
    // 에디터 전용 테스트 패널: 골드·자재·보석 추가. 빌드에는 들어가지 않는다.
    // 짧게 누르면 한 번, 꾹 누르기(우클릭 누르기)는 빠르게 반복.
    public partial class GameUI
    {
        void BuildDevPanel()
        {
            var panel = UIKit.Panel(frame, "ui_panel", "DevPanel");
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(24, -(TopH + 16));
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 10);
            vl.spacing = 4;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;

            UIKit.Label(panel.transform, "DEV", UIKit.TS, TextAnchor.MiddleLeft, UIKit.BadText).GetComponent<LayoutElement>().flexibleWidth = 0;
            DevButton(panel.transform, "ic_gold", "+100K", () => g.S.gold += 100000);
            DevButton(panel.transform, "ic_mat", "+100K", () => g.S.material += 100000);
            DevButton(panel.transform, "ic_rp", "+100", () => g.S.rp += 100);
        }

        void DevButton(Transform parent, string icon, string label, System.Action add)
        {
            float w = Mobile ? 120 : 150, h = Mobile ? 40 : 44;
            UIKit.Button(parent, label, null, w, h, Btn.Alt, UIKit.TS, icon, 24)
                .Hold(() => { add(); Sound.Play("coin"); g.MarkDirty(); return true; })
                .Tip("에디터 전용 치트\n탭: 한 번 · 꾹/우클릭 누르기: 반복");
        }
    }
}
#endif

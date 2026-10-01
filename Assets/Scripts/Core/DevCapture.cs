using System;
using System.Collections;
using UnityEngine;

namespace Mawang
{
    // 개발용: 빌드를 명령줄 옵션으로 실행하면 화면을 스스로 찍고 종료한다(실기기와 같은 빌드 결과 확인용).
    //   -mobile            폰 레이아웃(UI 크기)을 PC 빌드에서 흉내
    //   -autoshot <경로>    몇 초 뒤 스크린샷 저장 후 종료 (-hunt 를 붙이면 포획장 화면도 찍는다)
    // 옵션이 없으면 아무것도 하지 않는다.
    public class DevCapture : MonoBehaviour
    {
        public static bool ForceMobile => ForceMobileInEditor || HasArg("-mobile");
        public static bool ForceMobileInEditor; // 에디터 확인용: 켜고 장면을 다시 불러오면 폰 배치로 그린다
        public static bool Active { get; set; } // 캡처 실행 중에는 저장하지 않는다

        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        static string ArgValue(string a)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, a);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Application.isEditor || ArgValue("-autoshot") == null) return;
            Active = true;
            var go = new GameObject("DevCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCapture>();
        }

        IEnumerator Start()
        {
            string path = ArgValue("-autoshot");
            yield return new WaitForSecondsRealtime(6f);
            ScreenCapture.CaptureScreenshot(path + "_castle.png");
            yield return new WaitForSecondsRealtime(1f);
            var ui = FindAnyObjectByType<GameUI>();
            var castle = FindAnyObjectByType<CastleView>();
            if (HasArg("-zoom") && castle != null)
            {
                castle.ZoomStep(+1);
                yield return new WaitForSecondsRealtime(1.5f);
                ScreenCapture.CaptureScreenshot(path + "_zoomin.png");
                yield return new WaitForSecondsRealtime(0.5f);
                castle.ZoomStep(-1); castle.ZoomStep(-1);
                yield return new WaitForSecondsRealtime(1.5f);
                ScreenCapture.CaptureScreenshot(path + "_zoomall.png");
                yield return new WaitForSecondsRealtime(0.5f);
                castle.ZoomStep(+1);
            }
            if (HasArg("-side") && ui != null)
            {
                var toggle = typeof(GameUI).GetMethod("ToggleSidebar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (toggle != null)
                {
                    toggle.Invoke(ui, null);
                    yield return new WaitForSecondsRealtime(1f);
                    ScreenCapture.CaptureScreenshot(path + "_side.png");
                    yield return new WaitForSecondsRealtime(0.5f);
                    toggle.Invoke(ui, null);
                }
            }
            if (HasArg("-modal") && ui != null)
            {
                // 연구 창(가장 긴 목록) 열어 보기
                typeof(GameUI).GetMethod("OpenMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(ui, new object[] { 4 });
                yield return new WaitForSecondsRealtime(1f);
                ScreenCapture.CaptureScreenshot(path + "_modal.png");
                yield return new WaitForSecondsRealtime(0.5f);
                ui.CloseModal();
            }
            if (HasArg("-hunt"))
            {
                if (ui != null) ui.ShowHunt("lava");
                yield return new WaitForSecondsRealtime(3f);
                ScreenCapture.CaptureScreenshot(path + "_hunt.png");
                yield return new WaitForSecondsRealtime(1f);
            }
            Application.Quit(); // 저장은 하지 않도록 바로 종료 신호만 보낸다
        }
    }
}

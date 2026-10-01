using UnityEngine;

namespace Mawang
{
    // 빌드(폰 포함)에서 에러가 나면 검은 화면 대신 화면에 에러 내용을 띄운다.
    // 에디터에서는 콘솔이 있으니 동작하지 않는다. 화면을 탭하면 닫힌다.
    public class ErrorOverlay : MonoBehaviour
    {
        string message;
        int count;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Application.isEditor || !Debug.isDebugBuild) return; // 스토어(릴리스) 빌드에서는 끈다
            var go = new GameObject("ErrorOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<ErrorOverlay>();
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string text, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            count++;
            if (message != null) return; // 첫 에러를 보여준다(보통 원인)
            message = $"{text}\n\n{stack}";
        }

        void OnGUI()
        {
            if (message == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = Mathf.Max(14, Screen.height / 40) };
                style.normal.textColor = Color.white;
            }
            var rect = new Rect(10, 10, Screen.width - 20, Screen.height - 20);
            GUI.color = new Color(0.6f, 0f, 0f, 0.92f);
            GUI.Box(rect, "", style);
            GUI.color = Color.white;
            GUI.Label(rect, $"ERROR ({count}) - screenshot this and send it. Tap to close.\n\n{message}", style);
            if (Event.current.type == EventType.MouseUp) message = null;
        }
    }
}

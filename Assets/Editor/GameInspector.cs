using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // Game 오브젝트 인스펙터: 테스트용 버튼. 게임 화면에는 나오지 않는다.
    [CustomEditor(typeof(Game))]
    public class GameInspector : Editor
    {
        const string NoCapsKey = "Mawang.DevNoCaps";

        public static bool NoCaps
        {
            get => EditorPrefs.GetBool(NoCapsKey, false);
            set { EditorPrefs.SetBool(NoCapsKey, value); Game.DevNoCaps = value; }
        }

        const string PhoneKey = "Mawang.DevPhoneLayout";

        // 폰 레이아웃: PC 에디터에서 폰 UI 배치(큰 버튼·접는 사이드바 등)로 그린다
        public static bool PhoneLayout
        {
            get => EditorPrefs.GetBool(PhoneKey, false);
            set { EditorPrefs.SetBool(PhoneKey, value); DevCapture.ForceMobileInEditor = value; }
        }

        // 플레이를 시작할 때마다(도메인 리로드 후) 저장해 둔 설정을 다시 적용한다
        [InitializeOnLoadMethod]
        static void Apply()
        {
            Game.DevNoCaps = NoCaps;
            DevCapture.ForceMobileInEditor = PhoneLayout;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("테스트", EditorStyles.boldLabel);

            bool on = NoCaps;
            var old = GUI.backgroundColor;
            GUI.backgroundColor = on ? new Color(1f, 0.55f, 0.45f) : old;
            if (GUILayout.Button(on ? "자원 보유 상한: 꺼짐 (눌러서 켜기)" : "자원 보유 상한: 켜짐 (눌러서 끄기)", GUILayout.Height(28)))
            {
                NoCaps = !on;
                if (Application.isPlaying && Game.I != null) Game.I.MarkDirty();
            }
            GUI.backgroundColor = old;
            EditorGUILayout.HelpBox(on
                ? "골드·자재·보석 최대 보유량이 없습니다. 가게·전시 우리·투기장·연구소가 상한 없이 계속 벌어들입니다. (에디터에서만, 빌드에는 영향 없음)"
                : "끄면 테스트 중 골드·자재·보석 최대 보유량을 무시합니다. 설정은 플레이를 다시 시작해도 유지됩니다.", on ? MessageType.Warning : MessageType.None);

            bool phone = PhoneLayout;
            GUI.backgroundColor = phone ? new Color(0.55f, 0.75f, 1f) : old;
            if (GUILayout.Button(phone ? "화면 레이아웃: 폰 (눌러서 PC로)" : "화면 레이아웃: PC (눌러서 폰으로)", GUILayout.Height(28)))
            {
                PhoneLayout = !phone;
                // 플레이 중이면 저장 후 장면을 다시 불러 바로 바뀐 배치로 그린다 (언어 바꾸기와 같은 방식)
                if (Application.isPlaying && Game.I != null)
                {
                    Game.I.Save();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                }
            }
            GUI.backgroundColor = old;
            EditorGUILayout.HelpBox(phone
                ? "폰 UI 배치로 그립니다. 실제 폰 비율로 보려면 Game 창 해상도를 폰 비율(예: 2400×1080)로 바꾸세요. 터치·안전 영역(노치)은 실기기에서만 확인할 수 있습니다."
                : "누르면 PC 에디터에서 폰 UI 배치(큰 버튼·접는 사이드바 등)로 봅니다. 플레이 중에 누르면 저장 후 바로 다시 그립니다.", MessageType.None);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || Game.I == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("골드 +100K")) Add(s => s.gold += 100000);
                if (GUILayout.Button("자재 +100K")) Add(s => s.material += 100000);
                if (GUILayout.Button("보석 +100")) Add(s => s.rp += 100);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void Add(System.Action<SaveData> f)
        {
            f(Game.I.S);
            Game.I.MarkDirty();
        }
    }
}

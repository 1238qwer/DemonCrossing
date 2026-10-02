using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // GoalSettings 에셋을 만들고(없으면) 인스펙터에 띄운다. '기본값으로 되돌리기'도 여기서.
    [InitializeOnLoad]
    public static class GoalSettingsMenu
    {
        const string Path = "Assets/Resources/GoalSettings.asset";

        static GoalSettingsMenu()
        {
            EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<GoalSettings>(Path) == null) Ensure(); };
        }

        [MenuItem("Mawang/Goal Settings (다음 목표)")]
        static void Open()
        {
            var s = Ensure();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        [MenuItem("Mawang/Goal Settings - 기본 단계로 되돌리기")]
        static void ResetSteps()
        {
            if (!EditorUtility.DisplayDialog("다음 목표", "목표 단계를 기본 순서로 되돌릴까요? 고친 단계는 사라집니다.", "되돌리기", "취소")) return;
            var s = Ensure();
            Undo.RecordObject(s, "Reset goal steps");
            s.steps = GoalSettings.Defaults();
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
        }

        static GoalSettings Ensure()
        {
            var s = AssetDatabase.LoadAssetAtPath<GoalSettings>(Path);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<GoalSettings>();
            AssetDatabase.CreateAsset(s, Path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GoalSettings] 생성: {Path}");
            return s;
        }
    }
}

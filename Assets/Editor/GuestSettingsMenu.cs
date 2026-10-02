using UnityEditor;
using UnityEngine;

namespace Mawang.EditorTools
{
    // GuestSettings 에셋을 만들고(없으면) 인스펙터에 띄운다.
    [InitializeOnLoad]
    public static class GuestSettingsMenu
    {
        const string Path = "Assets/Resources/GuestSettings.asset";

        static GuestSettingsMenu()
        {
            EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<GuestSettings>(Path) == null) Ensure(); };
        }

        [MenuItem("Mawang/Guest Settings (손님 확률·변수)")]
        static void Open()
        {
            var s = Ensure();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        static GuestSettings Ensure()
        {
            var s = AssetDatabase.LoadAssetAtPath<GuestSettings>(Path);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<GuestSettings>();
            AssetDatabase.CreateAsset(s, Path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GuestSettings] 생성: {Path}");
            return s;
        }
    }
}

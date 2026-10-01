using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 플레이 스토어 제출용 빌드 (.aab, 업로드 키로 서명).
    //   Mawang/Store/Apply Player Settings : 앱 ID·이름·버전·SDK·가로 화면·아이콘·서명 설정
    //   Mawang/Store/Build AAB             : 위 설정 후 Builds/PlayStore/ 에 .aab
    //   Mawang/Store/Build AAB (Version Code +1) : 업데이트를 올릴 때 (스토어는 같은 버전 코드를 다시 받지 않는다)
    public static class StoreBuild
    {
        public const string AppId = "com.zhz.game";
        public const string Company = "ZHZ";
        public const string ProductName = "Demon Crossing";   // 한국어 기기: Plugins/Android/AppName.androidlib → "놀러와요 마왕의 성"
        public const string Version = "1.0.0";
        const string Scene = "Assets/Scenes/MawangCastle.unity";
        const string KeystoreDir = "Assets/Keystore";
        static readonly NamedBuildTarget Android = NamedBuildTarget.Android;

        [MenuItem("Mawang/Store/Apply Player Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(Android, AppId);
            PlayerSettings.bundleVersion = Version;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.SetScriptingBackend(Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(Android, ManagedStrippingLevel.Minimal);

            // 가로 화면만 (양쪽 가로는 자동 회전)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SplashScreen.show = false; // Unity 6: 퍼스널도 끌 수 있다
            ApplyIcons();
            ApplyKeystore();
            AssetDatabase.SaveAssets();
            Debug.Log($"[StoreBuild] 설정 적용: {AppId} v{Version} ({PlayerSettings.Android.bundleVersionCode}) target API 36");
        }

        static void ApplyIcons()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app_icon.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreArt.AdaptiveBg);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreArt.AdaptiveFg);
            if (icon == null || bg == null || fg == null) { StoreArt.MakeAll(); icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app_icon.png"); bg = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreArt.AdaptiveBg); fg = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreArt.AdaptiveFg); }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application); // 기본 아이콘
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(Android, kind);
                foreach (var i in icons) i.SetTextures(icon);
                PlayerSettings.SetPlatformIcons(Android, kind, icons);
            }
            var adaptive = PlayerSettings.GetPlatformIcons(Android, AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptive) i.SetTextures(bg, fg);
            PlayerSettings.SetPlatformIcons(Android, AndroidPlatformIconKind.Adaptive, adaptive);
        }

        // Assets/Keystore/keystore.properties (keystore=, alias=, storePass=, keyPass=)
        static void ApplyKeystore()
        {
            var props = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(KeystoreDir, "keystore.properties")))
            {
                int i = line.IndexOf('=');
                if (i > 0) props[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(Path.Combine(KeystoreDir, props["keystore"]));
            PlayerSettings.Android.keystorePass = props["storePass"];
            PlayerSettings.Android.keyaliasName = props["alias"];
            PlayerSettings.Android.keyaliasPass = props["keyPass"];
        }

        [MenuItem("Mawang/Store/Build AAB")]
        public static void BuildAab() => Build(false);

        [MenuItem("Mawang/Store/Build AAB (Version Code +1)")]
        public static void BuildAabNext() => Build(true);

        // 한 번에 세 가지: Builds/PlayStore/*.aab, Builds/Android/*.apk, Builds/Windows/DemonCrossing.exe
        // (유니티는 빌드를 하나씩만 돌리므로 차례로 만든다. 안드로이드 두 개를 먼저 만들고 윈도우로 바꾼다.)
        [MenuItem("Mawang/Store/Build All (AAB + APK + EXE)")]
        public static void BuildAllMenu() => Debug.Log("[StoreBuild] 전체 빌드\n" + BuildAll());

        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(Build(false));
            log.AppendLine(BuildApk());
            log.AppendLine(BuildExe());
            log.AppendLine(BuildInstaller());
            return log.ToString();
        }

        // Installer/DemonCrossing.iss 로 Builds/Installer/DemonCrossing_Setup_{버전}.exe (Inno Setup 6 필요)
        [MenuItem("Mawang/Store/Build Windows Installer")]
        public static void BuildInstallerMenu() => Debug.Log("[StoreBuild] " + BuildInstaller());

        public static string BuildInstaller()
        {
            string iscc = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs/Inno Setup 6/ISCC.exe");
            if (!File.Exists(iscc)) iscc = @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe";
            if (!File.Exists(iscc)) return "Failed installer: Inno Setup 6 이 없습니다 (winget install JRSoftware.InnoSetup)";
            string iss = Path.GetFullPath(Path.Combine(Application.dataPath, "../Installer/DemonCrossing.iss"));
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(iscc, $"/Q /DAppVersion={Version} \"{iss}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            });
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            string output = Out("Installer", $"DemonCrossing_Setup_{Version}.exe");
            return p.ExitCode == 0 ? $"Succeeded {output}" : $"Failed installer (exit {p.ExitCode}) {err}";
        }

        public static string BuildApk()
        {
            Apply();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.None;
            string path = Out("Android", $"DemonCrossing_{Version}_{PlayerSettings.Android.bundleVersionCode}.apk");
            return Report(BuildTarget.Android, path);
        }

        public static string BuildExe()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            string path = Out("Windows", "DemonCrossing.exe");
            return Report(BuildTarget.StandaloneWindows64, path);
        }

        static string Out(string folder, string file)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds", folder));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, file);
        }

        static string Report(BuildTarget target, string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[StoreBuild] {s.result} · {s.totalSize / 1024f / 1024f:0.0}MB · {s.totalTime} → {path}");
            return $"{s.result} {path} errors={s.totalErrors}";
        }

        public static string Build(bool bumpCode)
        {
            Apply();
            if (bumpCode) PlayerSettings.Android.bundleVersionCode++;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable; // 스토어 '네이티브 디버그 기호' 경고 해결용 zip
            UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.Zip;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/PlayStore"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"DemonCrossing_{Version}_{PlayerSettings.Android.bundleVersionCode}.aab");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[StoreBuild] {s.result} · {s.totalSize / 1024f / 1024f:0.0}MB · {s.totalTime} → {path}");
            return $"{s.result} {path} errors={s.totalErrors}";
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Mawang.EditorTools
{
    // 플레이 스토어 제출용 빌드 (.aab, 업로드 키로 서명).
    //   Mawang/Store/Apply Player Settings : 앱 ID·이름·SDK·가로 화면·아이콘·서명 설정
    //   Mawang/Store/Build AAB             : 위 설정 후 Builds/PlayStore/ 에 .aab
    // 버전(예: 1.2.0)은 Project Settings > Player 에 적은 그대로 쓴다(덮어쓰지 않는다).
    // 버전 코드는 안드로이드 빌드를 할 때마다 이전보다 1 올린다(스토어는 같은 코드를 다시 받지 않는다). 빌드가 실패하면 되돌린다.
    public static class StoreBuild
    {
        public const string AppId = "com.zhz.game";
        public const string Company = "ZHZ";
        public const string ProductName = "Demon Crossing";   // 한국어 기기: Plugins/Android/AppName.androidlib → "놀러와요 마왕의 성"
        public static string Version => PlayerSettings.bundleVersion; // Project Settings 에 적은 버전 그대로
        const string Scene = "Assets/Scenes/MawangCastle.unity";
        const string KeystoreDir = "Assets/Keystore";
        static readonly NamedBuildTarget Android = NamedBuildTarget.Android;

        [MenuItem("Mawang/Store/Apply Player Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(Android, AppId);
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
        public static void BuildAab() => Build(true);

        // 한 번에 전부: Builds/PlayStore/*.aab (구글 플레이), Builds/OneStore/*_onestore.aab (원스토어), Builds/Android/*.apk,
        // Builds/Windows/DemonCrossing.exe, 윈도우 설치 파일.
        // (유니티는 빌드를 하나씩만 돌리므로 차례로 만든다. 안드로이드를 먼저 만들고 윈도우로 바꾼다.)
        [MenuItem("Mawang/Store/Build All (AAB + ONE store + APK + EXE)")]
        public static void BuildAllMenu() => Debug.Log("[StoreBuild] 전체 빌드\n" + BuildAll());

        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(Build(true));   // 버전 코드 +1 (구글 AAB · 원스토어 AAB · APK 는 같은 코드)
            log.AppendLine(BuildOneStoreAab());
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

        // ── 원스토어: ONESTORE 심볼(원스토어 결제 코드) + 원스토어 결제 SDK 를 넣은 AAB → Builds/OneStore/ ──
        // 구글 결제 라이브러리는 빼고 원스토어 SDK 를 넣는다 (OneStoreGradle). 구글 플레이 빌드는 그대로다.
        public const string OneStoreDefine = "ONESTORE";
        public const string OneStoreIapSdk = "com.onestorecorp.sdk:sdk-iap:21.04.00";
        public static bool BuildingOneStore { get; private set; }

        [MenuItem("Mawang/Store/Build ONE store AAB")]
        public static void BuildOneStoreMenu() => Debug.Log("[StoreBuild] " + BuildOneStoreAab());

        public static string BuildOneStoreAab()
        {
            Apply();
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.Zip;
            string path = Out("OneStore", $"DemonCrossing_{Version}_{PlayerSettings.Android.bundleVersionCode}_onestore.aab");
            BuildingOneStore = true;
            try { return Report(BuildTarget.Android, path, OneStoreDefine); }
            finally { BuildingOneStore = false; }
        }

        // 원스토어 샌드박스 결제 테스트용: 폰에 바로 설치하는 원스토어 APK → Builds/OneStore/ (버전 코드는 올리지 않는다)
        [MenuItem("Mawang/Store/Build ONE store APK (sandbox test)")]
        public static void BuildOneStoreApkMenu() => Debug.Log("[StoreBuild] " + BuildOneStoreApk());

        public static string BuildOneStoreApk()
        {
            Apply();
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.None;
            string path = Out("OneStore", $"DemonCrossing_{Version}_{PlayerSettings.Android.bundleVersionCode}_onestore.apk");
            BuildingOneStore = true;
            try { return Report(BuildTarget.Android, path, OneStoreDefine); }
            finally { BuildingOneStore = false; }
        }

        public static string BuildExe()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = ProductName;
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

        static string Report(BuildTarget target, string path, params string[] defines)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = defines,
            });
            var s = report.summary;
            Debug.Log($"[StoreBuild] {s.result} · {s.totalSize / 1024f / 1024f:0.0}MB · {s.totalTime} → {path}");
            return $"{s.result} {path} errors={s.totalErrors}";
        }

        // bumpCode: 버전 코드를 이전보다 1 올려서 빌드 (실패하면 되돌린다)
        public static string Build(bool bumpCode)
        {
            Apply();
            int prevCode = PlayerSettings.Android.bundleVersionCode;
            if (bumpCode) { PlayerSettings.Android.bundleVersionCode = prevCode + 1; AssetDatabase.SaveAssets(); }
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
            if (bumpCode && s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                PlayerSettings.Android.bundleVersionCode = prevCode; // 실패한 빌드는 번호를 쓰지 않는다
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[StoreBuild] {s.result} · v{Version} ({PlayerSettings.Android.bundleVersionCode}) · {s.totalSize / 1024f / 1024f:0.0}MB · {s.totalTime} → {path}");
            return $"{s.result} {path} errors={s.totalErrors}";
        }
    }

    // 원스토어 빌드에서만: unityLibrary/build.gradle 의 구글 결제 라이브러리(Unity IAP 가 넣는 줄)를 빼고 원스토어 결제 SDK 를 넣는다.
    // Unity IAP 의 의존성 주입(callbackOrder 1) 뒤에 돈다.
    class OneStoreGradle : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!StoreBuild.BuildingOneStore) return;
            string gradle = Path.Combine(path, "build.gradle");
            var text = File.ReadAllText(gradle);
            var lines = new List<string>(text.Split('\n'));
            int removed = lines.RemoveAll(l => l.Contains("com.android.billingclient:billing"));
            text = string.Join("\n", lines) + $"\ndependencies {{\n    implementation '{StoreBuild.OneStoreIapSdk}'\n}}\n";
            File.WriteAllText(gradle, text);
            Debug.Log($"[StoreBuild] 원스토어 gradle: 구글 결제 {removed}줄 제거, {StoreBuild.OneStoreIapSdk} 추가");
            AddQueries(Path.Combine(path, "src/main/AndroidManifest.xml"));
        }

        // 원스토어 심사: 결제 서비스와 onestore:// 링크(리뷰 버튼)를 찾을 수 있게 <queries> 를 선언해야 바이너리 등록이 된다
        const string Queries =
            "  <queries>\n" +
            "    <intent>\n" +
            "      <action android:name=\"com.onestore.ipc.iap.IapService.ACTION\" />\n" +
            "    </intent>\n" +
            "    <intent>\n" +
            "      <action android:name=\"android.intent.action.VIEW\" />\n" +
            "      <data android:scheme=\"onestore\" />\n" +
            "    </intent>\n" +
            "  </queries>\n";

        static void AddQueries(string manifest)
        {
            var text = File.ReadAllText(manifest);
            if (text.Contains("com.onestore.ipc.iap.IapService.ACTION")) return;
            int i = text.IndexOf("<application", System.StringComparison.Ordinal);
            if (i < 0) { Debug.LogError($"[StoreBuild] 원스토어 <queries> 를 넣을 <application> 이 없습니다: {manifest}"); return; }
            File.WriteAllText(manifest, text.Insert(i, Queries.TrimStart()));
            Debug.Log("[StoreBuild] 원스토어 <queries> 추가");
        }
    }
}

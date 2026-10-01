; Demon Crossing (놀러와요 마왕의 성) 윈도우 설치 프로그램 — Inno Setup 6
; 빌드: Mawang/Store/Build All 이 자동으로 부른다. 직접 하려면
;   "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" Installer\DemonCrossing.iss
;
; 제출 정보
;   레지스트리      HKEY_LOCAL_MACHINE\SOFTWARE\ZHZ\DemonCrossing  (InstallPath, Version, Exe, Uninstaller)
;   제거 정보       HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DemonCrossing_is1
;   실행 파일       DemonCrossing.exe
;   제거 파일       unins000.exe (실행 파일과 같은 폴더)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "Demon Crossing"
#define AppExe "DemonCrossing.exe"
#define BuildDir "..\Builds\Windows"

[Setup]
AppId=DemonCrossing
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ZHZ
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=DemonCrossing.ico
OutputDir=..\Builds\Installer
OutputBaseFilename=DemonCrossing_Setup_{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
CloseApplications=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*_DoNotShip,*_BackUpThisFolder_ButDontShipItWithYourGame"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "SOFTWARE\ZHZ"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "SOFTWARE\ZHZ\DemonCrossing"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\ZHZ\DemonCrossing"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKLM; Subkey: "SOFTWARE\ZHZ\DemonCrossing"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"
Root: HKLM; Subkey: "SOFTWARE\ZHZ\DemonCrossing"; ValueType: string; ValueName: "Exe"; ValueData: "{app}\{#AppExe}"
Root: HKLM; Subkey: "SOFTWARE\ZHZ\DemonCrossing"; ValueType: string; ValueName: "Uninstaller"; ValueData: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

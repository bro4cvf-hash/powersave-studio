; PowerSave Studio — Inno Setup script
; ------------------------------------------------------------------
; Builds PowerSave-Setup.exe (a classic Windows setup.exe installer).
;
; Prereq: run the app publish first (installer\build-installer.ps1 does both).
;   dotnet publish -c Release -r win-x64 --self-contained true
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
;     -o installer\publish
;
; Compile (PowerShell):
;   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\PowerSave.iss
; Output: installer\output\PowerSave-Setup.exe
; ------------------------------------------------------------------

; Directory (relative to this .iss) containing the published app payload.
; Override on the ISCC command line with /DPayloadDir=... if needed.
#ifndef PayloadDir
  #define PayloadDir "publish"
#endif

#define MyAppName "PowerSave Studio"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Vizzy · FSOS Labs"
#define MyAppExeName "PowerSave.exe"
#define MyAppId "{{9B6C4E2A-7D41-4F87-B1E3-2C5A64F0D913}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/bro4cvf-hash/powersave-studio
AppSupportURL=https://github.com/bro4cvf-hash/powersave-studio/issues
AppUpdatesURL=https://github.com/bro4cvf-hash/powersave-studio/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install — no UAC prompt, matches the portable spirit of the app
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=PowerSave-Setup
SetupIconFile=..\Assets\app.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; The app holds this mutex while running — Inno asks the user to close it first
AppMutex=PowerSaveStudio.SingleInstance.v1
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Single self-contained exe payload (~70 MB) — keep installer quiet about it
DisableReadyPage=no
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startafterinstall"; Description: "Launch {#MyAppName} after installation"; GroupDescription: "Other:"; Flags: checkedonce

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; Tasks: startafterinstall

[UninstallRun]
; Make sure a tray-resident instance is stopped before files are removed
Filename: "{cmd}"; Parameters: "/c taskkill /IM ""{#MyAppExeName}"" /F"; Flags: runhidden; RunOnceId: "KillApp"
; Remove the app's own HKCU Run entry (Start with Windows)
Filename: "{cmd}"; Parameters: "/c reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v PowerSave /f"; Flags: runhidden; RunOnceId: "DelRunKey"

[UninstallDelete]
; Remove the unpacked single-file bundle cache, if any
Type: filesandordirs; Name: "{localappdata}\PowerSave"

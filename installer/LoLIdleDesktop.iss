; Instalador do LoL Idle Desktop (Inno Setup 6)
; Compile com: installer\build-installer.ps1
; Ou diretamente:
;   ISCC.exe /DMyAppVersion=1.1.0 /DMyAppVersion4=1.1.0.0 /DSourceDir=..\artifacts\publish installer\LoLIdleDesktop.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif

#ifndef MyAppVersion4
  #define MyAppVersion4 "1.1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif

#define MyAppName "LoL Idle Desktop"
#define MyAppExeName "LoL Idle.exe"
#define MyAppPublisher "Zephrus15"
#define MyAppURL "https://github.com/Zephrus15/LOLIdleApp"
#define MyAppId "{{7E1B5B6A-6C1E-4F3B-9E2D-2A4C8F5D9B31}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\LoL Idle
DefaultGroupName=LoL Idle
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=LoLIdleDesktop-Setup-{#MyAppVersion}-x64
SetupIconFile=..\Assets\lol-idle.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion4}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion4}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AllowNoIcons=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na &área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "redist\MicrosoftEdgeWebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not WebView2RuntimeInstalled

[Icons]
Name: "{autoprograms}\LoL Idle Desktop"; Filename: "{app}\{#MyAppExeName}"; AppUserModelID: "Zephrus15.LoLIdle.Desktop"
Name: "{autodesktop}\LoL Idle Desktop"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; AppUserModelID: "Zephrus15.LoLIdle.Desktop"

[Run]
Filename: "{tmp}\MicrosoftEdgeWebView2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Instalando o Microsoft Edge WebView2 Runtime..."; Flags: waituntilterminated; Check: not WebView2RuntimeInstalled
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir o LoL Idle Desktop"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2ClientId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function WebView2RuntimeInstalled(): Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId, 'pv', Version) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId, 'pv', Version) or
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId, 'pv', Version) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\' + WebView2ClientId, 'pv', Version);
end;

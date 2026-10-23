; Baixaki installer script (Inno Setup 6). Built by build-installer.bat.
; Installs per-user (no admin needed) so yt-dlp can update itself.
; The version comes from Baixaki.cs (build-installer.ps1 passes it in).
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{B7A6C1E2-5D4F-4C0B-9A57-3E1F2D8C4A11}
AppName=Baixaki
AppVersion={#MyAppVersion}
AppPublisher=Agência Duvidiu
DefaultDirName={autopf}\Baixaki
DefaultGroupName=Baixaki
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Output
OutputBaseFilename=BaixakiSetup
SetupIconFile=Baixaki.ico
UninstallDisplayIcon={app}\Baixaki.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "Baixaki.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "yt-dlp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Baixaki.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\Baixaki"; Filename: "{app}\Baixaki.exe"
Name: "{group}\Uninstall Baixaki"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Baixaki"; Filename: "{app}\Baixaki.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Baixaki.exe"; Description: "{cm:LaunchProgram,Baixaki}"; Flags: nowait postinstall skipifsilent
; after a silent auto-update, reopen the app automatically
Filename: "{app}\Baixaki.exe"; Flags: nowait runasoriginaluser; Check: WizardSilent

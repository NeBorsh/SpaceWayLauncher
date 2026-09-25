#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "SpaceWay Launcher"
#define AppExe "SpaceWay.Launcher.exe"
#define SourceDir "..\artifacts\win-x64"

[Setup]
AppId={{8E6F1F1C-6B0F-4C3E-9E2E-6E6C2C0A7A11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}

PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\SpaceWayLauncher
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

DisableWelcomePage=no
DisableDirPage=no

OutputDir=..\artifacts
OutputBaseFilename=SpaceWayLauncher-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

SetupIconFile=..\src\SpaceWay.Launcher\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExe}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

AppMutex=SpaceWayLauncher

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

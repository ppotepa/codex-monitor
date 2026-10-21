#define MyAppName "Codex Monitor"
#define MyAppVersion "0.5.1"
#define MyAppExeName "CodexMonitor.exe"

[Setup]
AppId={{A44E9A95-38F2-487B-9D02-753B24AA3A5B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Codex Monitor
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
MinVersion=10.0
AppMutex=Local\CodexMonitor
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\installer
OutputBaseFilename=CodexMonitor-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\build\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CodexMonitor');
end;

; Explorer Organizer - Inno Setup 6 script
; Built by ..\build.ps1:  ISCC /DMyAppVersion=1.0.0 /DSourceDir=<publish folder> setup.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#define MyAppName "Explorer Organizer"
#define MyAppExe "ExplorerOrganizer.exe"

[Setup]
AppId={{8F3C2A5E-6B1D-4C7E-9A2F-3D5E7B9C1A40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=Explorer Organizer
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install by default (no admin); the user can choose "all users" in the first dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename=ExplorerOrganizer-Setup
SetupIconFile=..\src\ExplorerOrganizer.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
ChangesAssociations=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
italian.GroupIntegration=Integrazione con Windows:
english.GroupIntegration=Windows integration:
italian.TaskContextMenu=Aggiungi "Organizza con Explorer Organizer" al menu contestuale di Explorer
english.TaskContextMenu=Add "Organize with Explorer Organizer" to the Explorer context menu
italian.TaskSendTo=Aggiungi Explorer Organizer al menu "Invia a"
english.TaskSendTo=Add Explorer Organizer to the "Send to" menu
italian.TaskAutostart=Avvia Explorer Organizer all'accesso a Windows (nell'area di notifica)
english.TaskAutostart=Start Explorer Organizer when Windows starts (in the notification area)
italian.MenuOrganize=Organizza con Explorer Organizer
english.MenuOrganize=Organize with Explorer Organizer
italian.MenuOrganizeHere=Organizza questa cartella con Explorer Organizer
english.MenuOrganizeHere=Organize this folder with Explorer Organizer
italian.RemoveUserData=Eliminare anche impostazioni, profili e registri di Explorer Organizer per questo utente?%n%n(%1)%n%nI tuoi file organizzati NON vengono toccati.
english.RemoveUserData=Also delete Explorer Organizer settings, profiles and logs for this user?%n%n(%1)%n%nYour organized files are NOT touched.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "contextmenu"; Description: "{cm:TaskContextMenu}"; GroupDescription: "{cm:GroupIntegration}"
Name: "sendto"; Description: "{cm:TaskSendTo}"; GroupDescription: "{cm:GroupIntegration}"
Name: "autostart"; Description: "{cm:TaskAutostart}"; GroupDescription: "{cm:GroupIntegration}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon
Name: "{usersendto}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Parameters: "--process"; Tasks: sendto

[Registry]
; Explorer context menu (HKA = HKCU for per-user installs, HKLM for all-users installs)
Root: HKA; Subkey: "Software\Classes\*\shell\ExplorerOrganizer"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:MenuOrganize}"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\*\shell\ExplorerOrganizer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExe}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\*\shell\ExplorerOrganizer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" --process ""%1"""; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ExplorerOrganizer"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:MenuOrganize}"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ExplorerOrganizer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExe}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\ExplorerOrganizer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" --process ""%1"""; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ExplorerOrganizer"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:MenuOrganizeHere}"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ExplorerOrganizer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExe}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\ExplorerOrganizer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExe}"" --process ""%V"""; Tasks: contextmenu
; Autostart
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ExplorerOrganizer"; ValueData: """{app}\{#MyAppExe}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart
; Clean uninstall of the per-user entries the app itself can create from Settings
Root: HKCU; Subkey: "Software\Classes\*\shell\ExplorerOrganizer"; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\ExplorerOrganizer"; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\ExplorerOrganizer"; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ExplorerOrganizer"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#MyAppExe} /F"; Flags: runhidden; RunOnceId: "StopExplorerOrganizer"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    DataDir := ExpandConstant('{userappdata}\ExplorerOrganizer');
    if DirExists(DataDir) then
      if MsgBox(FmtMessage(CustomMessage('RemoveUserData'), [DataDir]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;

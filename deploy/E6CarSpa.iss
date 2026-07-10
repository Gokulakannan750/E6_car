; Inno Setup script for E6 Car Spa.
; Installs the API as an auto-starting Windows Service and the desktop app with shortcuts.
; Build the publish output first (see deploy/README.md), then compile this with Inno Setup 6:
;     "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" deploy\E6CarSpa.iss
; The installer (E6CarSpa-Setup.exe) is produced in deploy\Output.

#define MyAppName "E6 Car Spa"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "trovotechsolutions"
#define ApiServiceName "E6CarSpaApi"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
LicenseFile=license.txt
DefaultDirName={autopf}\E6 Car Spa
DefaultGroupName=E6 Car Spa
DisableProgramGroupPage=yes
; Always show the "Select Destination Location" page — even on an upgrade — so the
; installer can pick where to install (default is 'auto', which hides it once installed).
DisableDirPage=no
OutputDir=Output
OutputBaseFilename=E6CarSpa-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\Desktop\logo.ico
; Installer .exe icon (shown in File Explorer and the UAC prompt)
SetupIconFile=logo.ico
; Wizard sidebar banner  (164 x 314 px BMP)
WizardImageFile=wizard-banner.bmp
; Wizard top-right small image (55 x 58 px BMP)
WizardSmallImageFile=wizard-small.bmp
; In-place upgrades: close the running desktop app (Restart Manager) so its exe can be replaced.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut for the billing app"; GroupDescription: "Shortcuts:"

[Files]
; ---- API server (self-contained) ----
; Everything except the config, which we ship from a clean template and never overwrite.
Source: "..\dist\api\*"; DestDir: "{app}\Api"; Excludes: "appsettings.json,appsettings.Development.json,run.log,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; Config template installed as appsettings.json only on first install (preserved on upgrades).
Source: "appsettings.template.json"; DestDir: "{app}\Api"; DestName: "appsettings.json"; Flags: onlyifdoesntexist

; ---- Desktop client (self-contained single file) ----
Source: "..\dist\desktop\E6CarSpa.Desktop.exe"; DestDir: "{app}\Desktop"; Flags: ignoreversion
Source: "..\dist\desktop\READ-ME-FIRST.txt"; DestDir: "{app}\Desktop"; Flags: ignoreversion
; Logo icon alongside the exe so shortcuts resolve to it correctly
Source: "logo.ico"; DestDir: "{app}\Desktop"; Flags: ignoreversion

[Icons]
Name: "{group}\E6 Car Spa"; Filename: "{app}\Desktop\E6CarSpa.Desktop.exe"; IconFilename: "{app}\Desktop\logo.ico"
Name: "{group}\Edit API Configuration"; Filename: "notepad.exe"; Parameters: """{app}\Api\appsettings.json"""
Name: "{group}\Uninstall E6 Car Spa"; Filename: "{uninstallexe}"; IconFilename: "{app}\Desktop\logo.ico"
Name: "{commondesktop}\E6 Car Spa"; Filename: "{app}\Desktop\E6CarSpa.Desktop.exe"; IconFilename: "{app}\Desktop\logo.ico"; Tasks: desktopicon

[Run]
; Remove any previous service instance (ignored if absent), then (re)create and start it.
Filename: "{sys}\sc.exe"; Parameters: "stop {#ApiServiceName}"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete {#ApiServiceName}"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "create {#ApiServiceName} binPath= ""{app}\Api\E6CarSpa.Api.exe"" start= auto DisplayName= ""E6 Car Spa API"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#ApiServiceName} ""E6 Car Spa billing API server"""; Flags: runhidden
; Auto-restart the service if it ever crashes.
Filename: "{sys}\sc.exe"; Parameters: "failure {#ApiServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000"; Flags: runhidden
; Allow the API port through the firewall (needed only if other PCs connect over the LAN).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""E6 Car Spa API"" dir=in action=allow protocol=TCP localport=5080"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#ApiServiceName}"; Flags: runhidden

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ApiServiceName}"; Flags: runhidden; RunOnceId: "StopSvc"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ApiServiceName}"; Flags: runhidden; RunOnceId: "DelSvc"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""E6 Car Spa API"""; Flags: runhidden; RunOnceId: "DelFw"

[Code]
{ Stop the API service BEFORE files are copied, so an in-place upgrade can replace the locked
  E6CarSpa.Api.exe. The [Run] section then recreates and starts the service. appsettings.json is
  preserved (onlyifdoesntexist), so the database connection survives the upgrade. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ApiServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2500);
  Result := '';
end;

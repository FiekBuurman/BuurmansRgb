; BuurmansRgb with OpenRGB as a service and the PawnIO driver, in one setup with one UAC prompt.
; Build it with installer\build.ps1, which publishes the apps and fetches the dependencies first.
; See docs\openrgb-meeleveren.md for why it works this way.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef ArtifactsDir
  #define ArtifactsDir "..\artifacts\installer"
#endif

#define AppName "BuurmansRgb"
#define EngineExe "BuurmansRgb.Engine.exe"
#define SettingsExe "BuurmansRgb.UI.exe"
#define OpenRgbMsi "OpenRGB_1.1_Windows_64_8e16de2.msi"
#define OpenRgbService "OpenRGB"
#define PawnIoSetup "PawnIO_setup.exe"
#define StateKey "Software\BuurmansRgb"

[Setup]
AppId={{1728D1BE-C512-48E3-B3BB-977F2884725F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ArtifactsDir}\out
OutputBaseFilename=BuurmansRgb-setup
SetupIconFile=..\design\icon\buurmansrgb.ico
UninstallDisplayIcon={app}\{#EngineExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[CustomMessages]
dutch.InstallingPawnIo=PawnIO-driver installeren...
dutch.InstallingOpenRgb=OpenRGB installeren als achtergrondservice...
dutch.PawnIoFailed=PawnIO kon niet worden geïnstalleerd (code %1). Moederbord, RAM en videokaart werken dan misschien niet. De rest van BuurmansRgb werkt wel.
dutch.OpenRgbFailed=OpenRGB kon niet worden geïnstalleerd (code %1). BuurmansRgb toont dan alleen de gesimuleerde apparaten.
dutch.StartEngine=BuurmansRgb starten
dutch.RemoveOpenRgb=OpenRGB is samen met BuurmansRgb geïnstalleerd. Wil je OpenRGB ook verwijderen?
dutch.RemovePawnIo=De PawnIO-driver is samen met BuurmansRgb geïnstalleerd. Wil je PawnIO ook verwijderen?%n%nAndere programma's (bijv. FanControl of LibreHardwareMonitor) kunnen PawnIO ook gebruiken.
dutch.RemoveSettings=Wil je ook je BuurmansRgb-instellingen (kleuren en effecten) verwijderen?

[Files]
Source: "{#ArtifactsDir}\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ArtifactsDir}\deps\{#PawnIoSetup}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "{#ArtifactsDir}\deps\{#OpenRgbMsi}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#EngineExe}"

[Run]
; As the user who started setup, not as the elevated installer: the engine is a normal user app
; and --first-run writes autostart into that user's HKCU.
Filename: "{app}\{#EngineExe}"; Parameters: "--first-run"; Description: "{cm:StartEngine}"; Flags: nowait postinstall runasoriginaluser

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#EngineExe}"; Flags: runhidden; RunOnceId: "StopEngine"
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#SettingsExe}"; Flags: runhidden; RunOnceId: "StopSettingsApp"

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  ErrorSuccessRebootRequired = 3010;
  ErrorSuccessRebootInitiated = 1641;

var
  RestartNeeded: Boolean;

function Succeeded(ResultCode: Integer): Boolean;
begin
  Result := (ResultCode = 0) or (ResultCode = ErrorSuccessRebootRequired) or (ResultCode = ErrorSuccessRebootInitiated);
  if (ResultCode = ErrorSuccessRebootRequired) or (ResultCode = ErrorSuccessRebootInitiated) then
    RestartNeeded := True;
end;

function RunHidden(const FileName, Params: String): Integer;
begin
  if not Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, Result) then
    Result := -1;
end;

function ServiceExists(const Name: String): Boolean;
begin
  Result := RunHidden(ExpandConstant('{sys}\sc.exe'), 'query "' + Name + '"') = 0;
end;

function PawnIoInstalled(): Boolean;
begin
  Result := RegKeyExists(HKLM64, UninstallKey + '\PawnIO');
end;

// The uninstall entry of an MSI product is its product code; find OpenRGB's by display name.
function FindUninstallEntry(const DisplayName: String; var Entry: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Value: String;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, UninstallKey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if RegQueryStringValue(HKLM64, UninstallKey + '\' + Names[I], 'DisplayName', Value) and (Value = DisplayName) then
      begin
        Entry := Names[I];
        Result := True;
        Exit;
      end;
end;

procedure InstallPawnIo();
var
  ResultCode: Integer;
begin
  if PawnIoInstalled() then
    Exit;

  WizardForm.StatusLabel.Caption := CustomMessage('InstallingPawnIo');
  ResultCode := RunHidden(ExpandConstant('{tmp}\{#PawnIoSetup}'), '-install -silent');
  Log(Format('PawnIO setup returned %d', [ResultCode]));
  if Succeeded(ResultCode) then
    RegWriteDWordValue(HKLM64, '{#StateKey}', 'InstalledPawnIo', 1)
  else
    SuppressibleMsgBox(FmtMessage(CustomMessage('PawnIoFailed'), [IntToStr(ResultCode)]), mbError, MB_OK, IDOK);
end;

procedure InstallOpenRgb();
var
  ResultCode: Integer;
begin
  if ServiceExists('{#OpenRgbService}') then
  begin
    Log('OpenRGB service already exists, leaving it as it is');
    Exit;
  end;

  WizardForm.StatusLabel.Caption := CustomMessage('InstallingOpenRgb');
  ResultCode := RunHidden(ExpandConstant('{sys}\msiexec.exe'),
    ExpandConstant('/i "{tmp}\{#OpenRgbMsi}" /qn /norestart ADDLOCAL=OpenRGBApplication,OpenRGBRegisterService /l*v "{log}.openrgb.txt"'));
  Log(Format('OpenRGB msiexec returned %d', [ResultCode]));
  if Succeeded(ResultCode) then
    RegWriteDWordValue(HKLM64, '{#StateKey}', 'InstalledOpenRgb', 1)
  else
    SuppressibleMsgBox(FmtMessage(CustomMessage('OpenRgbFailed'), [IntToStr(ResultCode)]), mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // An update replaces the running engine; Restart Manager may miss the tray app.
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#EngineExe}');
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#SettingsExe}');
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallPawnIo();
    InstallOpenRgb();
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := RestartNeeded;
end;

function InstalledByUs(const Name: String): Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, '{#StateKey}', Name, Value) and (Value = 1);
end;

// Only the value the engine wrote for this install; an engine elsewhere keeps its autostart.
procedure RemoveAutoStart();
var
  Command: String;
begin
  if RegQueryStringValue(HKCU, RunKey, '{#AppName}', Command) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Command)) > 0) then
    RegDeleteValue(HKCU, RunKey, '{#AppName}');
end;

procedure UninstallOpenRgb();
var
  Entry: String;
begin
  if not InstalledByUs('InstalledOpenRgb') or not FindUninstallEntry('OpenRGB', Entry) then
    Exit;
  if SuppressibleMsgBox(CustomMessage('RemoveOpenRgb'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
    Log(Format('OpenRGB uninstall returned %d', [RunHidden(ExpandConstant('{sys}\msiexec.exe'), '/x ' + Entry + ' /qn /norestart')]));
end;

procedure UninstallPawnIo();
var
  Command: String;
begin
  if not InstalledByUs('InstalledPawnIo') or
     not RegQueryStringValue(HKLM64, UninstallKey + '\PawnIO', 'UninstallString', Command) then
    Exit;
  if SuppressibleMsgBox(CustomMessage('RemovePawnIo'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
    Log(Format('PawnIO uninstall returned %d', [RunHidden(RemoveQuotes(Command), '-uninstall -silent')]));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveAutoStart();

  if CurUninstallStep = usPostUninstall then
  begin
    UninstallOpenRgb();
    UninstallPawnIo();
    RegDeleteKeyIncludingSubkeys(HKLM64, '{#StateKey}');
    if SuppressibleMsgBox(CustomMessage('RemoveSettings'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
      DelTree(ExpandConstant('{userappdata}\{#AppName}'), True, True, True);
  end;
end;

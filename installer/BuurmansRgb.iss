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
#define ExitArgument "--exit"
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
; From Instellingen -> Apps a log in the TEMP of whoever answered UAC, with who started it.
UninstallLogging=yes

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

; The uninstaller stops the engine and settings app itself, in StopApps: only those of the user
; who started the uninstall, and the engine first with --exit.

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall';
  UserShellFoldersKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders';
  ShellFoldersKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders';
  ProfileListKey = 'Software\Microsoft\Windows NT\CurrentVersion\ProfileList';
  ErrorSuccessRebootRequired = 3010;
  ErrorSuccessRebootInitiated = 1641;
  // How long the uninstaller waits for the engine after --exit. It saves and is gone within a
  // few seconds (at most 2 s waiting for OpenRGB); after that taskkill stops it.
  EngineExitTimeoutMs = 10000;
  EngineExitPollMs = 250;
  // When who started the uninstall is not known there is no SID to look for: a fixed short wait.
  EngineExitFixedWaitMs = 3000;

var
  RestartNeeded: Boolean;
  // Who started the uninstall (see InitializeUninstall); empty when that is not known.
  OriginalUserSid: String;
  OriginalUserName: String;
  OriginalUserIsCurrent: Boolean;
  Wmi: Variant;
  HaveWmi: Boolean;

function GetCurrentProcessId(): Cardinal;
  external 'GetCurrentProcessId@kernel32.dll stdcall uninstallonly';

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

// The uninstaller runs twice: the first copy runs as the user who started it (from Instellingen
// -> Apps), starts this second copy elevated and waits for it. When another admin answered the
// UAC prompt with a password, this copy runs as that admin, so HKCU, {userappdata} and
// {username} are the admin's, not those of the user who wanted BuurmansRgb gone. The owner of
// the first copy, the parent of this one, is that user. ExecAsOriginalUser does not work in
// Uninstall, so it is read here through WMI. When it cannot be read, OriginalUserSid stays empty
// and the uninstall leaves the autostart and settings of every user alone.
function InitializeUninstall(): Boolean;
var
  Locator, Current, Parent, Owner, OwnerSid, CurrentSid: Variant;
  ParentId, OwnerResult, OwnerSidResult, CurrentSidResult: Integer;
  ParentStarted, CurrentStarted, Domain, User, Sid: String;
begin
  Result := True;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Wmi := Locator.ConnectServer('.', 'root\CIMV2');
    HaveWmi := True;

    Current := Wmi.Get('Win32_Process.Handle="' + IntToStr(GetCurrentProcessId()) + '"');
    ParentId := Current.ParentProcessId;
    Parent := Wmi.Get('Win32_Process.Handle="' + IntToStr(ParentId) + '"');

    // A parent that started after this copy is another process that got the same id.
    ParentStarted := Parent.CreationDate;
    CurrentStarted := Current.CreationDate;
    if CompareStr(ParentStarted, CurrentStarted) > 0 then
    begin
      Log('The process that started the uninstall is gone; leaving autostart and settings alone');
      Exit;
    end;

    OwnerSid := Parent.ExecMethod_('GetOwnerSid');
    Owner := Parent.ExecMethod_('GetOwner');
    CurrentSid := Current.ExecMethod_('GetOwnerSid');
    OwnerSidResult := OwnerSid.ReturnValue;
    OwnerResult := Owner.ReturnValue;
    CurrentSidResult := CurrentSid.ReturnValue;
    if (OwnerSidResult <> 0) or (OwnerResult <> 0) or (CurrentSidResult <> 0) then
    begin
      Log(Format('The owner of the uninstall could not be read (%d, %d, %d); leaving autostart and settings alone', [OwnerSidResult, OwnerResult, CurrentSidResult]));
      Exit;
    end;

    Sid := OwnerSid.Sid;
    Domain := Owner.Domain;
    User := Owner.User;
    OriginalUserSid := Sid;
    OriginalUserName := Domain + '\' + User;
    Sid := CurrentSid.Sid;
    OriginalUserIsCurrent := CompareText(OriginalUserSid, Sid) = 0;
    if OriginalUserIsCurrent then
      Log('Uninstall started by ' + OriginalUserName + ', the same user as this uninstaller')
    else
      Log('Uninstall started by ' + OriginalUserName + ' (' + OriginalUserSid + '), elevated as ' + ExpandConstant('{username}'));
  except
    OriginalUserSid := '';
    Log('Who started the uninstall could not be read: ' + GetExceptionMessage());
  end;
end;

// Whether an engine of the user with this SID still runs, in any session.
function EngineRunsFor(const Sid: String): Boolean;
var
  Processes, Process, Owner: Variant;
  I, Count, OwnerResult: Integer;
  OwnerSid: String;
begin
  Result := False;
  if not HaveWmi then
    Exit;

  try
    Processes := Wmi.ExecQuery('SELECT * FROM Win32_Process WHERE Name = ''{#EngineExe}''');
    Count := Processes.Count;
    for I := 0 to Count - 1 do
    begin
      Process := Processes.ItemIndex(I);
      Owner := Process.ExecMethod_('GetOwnerSid');
      OwnerResult := Owner.ReturnValue;
      if OwnerResult = 0 then
      begin
        OwnerSid := Owner.Sid;
        if CompareText(OwnerSid, Sid) = 0 then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  except
    // A process that exits between the query and its owner: look again on the next round.
    Result := True;
  end;
end;

procedure WaitForEngineExit();
var
  Waited: Integer;
begin
  Waited := 0;
  while EngineRunsFor(OriginalUserSid) and (Waited < EngineExitTimeoutMs) do
  begin
    Sleep(EngineExitPollMs);
    Waited := Waited + EngineExitPollMs;
  end;

  if EngineRunsFor(OriginalUserSid) then
    Log(Format('The engine still runs %d ms after --exit; taskkill stops it', [Waited]))
  else
    Log(Format('The engine exited after --exit (%d ms)', [Waited]));
end;

// Only the engine and settings app of the user who started the uninstall, never those of
// another user on this pc (in another session, after switching users). The engine first gets
// --exit, as "Afsluiten" in the tray menu, so it saves the last change; taskkill afterwards is
// the safety net. --exit only reaches the engine of the user who runs this uninstaller: the
// signal has the default DACL of its owner, so a process of another admin cannot open it and
// taskkill stops the engine of the user who started the uninstall at once.
procedure StopApps();
var
  UserName: String;
  ResultCode: Integer;
begin
  if OriginalUserSid = '' then
  begin
    // Most likely the same user; the smallest step that never touches another user.
    UserName := ExpandConstant('{username}');
    Log('Stopping only the apps of ' + UserName + ', who runs this uninstaller');
  end
  else
    UserName := OriginalUserName;

  if (OriginalUserSid = '') or OriginalUserIsCurrent then
  begin
    ResultCode := RunHidden(ExpandConstant('{app}\{#EngineExe}'), '{#ExitArgument}');
    Log(Format('Engine {#ExitArgument} returned %d', [ResultCode]));
    if OriginalUserSid = '' then
    begin
      Sleep(EngineExitFixedWaitMs);
      Log(Format('Waited %d ms after {#ExitArgument}; taskkill stops the engine if it still runs', [EngineExitFixedWaitMs]));
    end
    else
      WaitForEngineExit();
  end
  else
    Log('Engine {#ExitArgument} skipped: it cannot reach the engine of another user');

  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /FI "USERNAME eq ' + UserName + '" /IM {#EngineExe}');
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /FI "USERNAME eq ' + UserName + '" /IM {#SettingsExe}');
end;

// Only the value the engine wrote for this install; an engine elsewhere keeps its autostart.
procedure RemoveAutoStartFrom(const RootKey: HKEY; const SubKey: String);
var
  Command: String;
begin
  if RegQueryStringValue(RootKey, SubKey, '{#AppName}', Command) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Command)) > 0) then
  begin
    RegDeleteValue(RootKey, SubKey, '{#AppName}');
    Log('Removed the autostart of ' + OriginalUserName);
  end;
end;

procedure RemoveAutoStart();
begin
  if OriginalUserSid = '' then
    Log('Autostart left alone: who started the uninstall is not known')
  else if OriginalUserIsCurrent then
    RemoveAutoStartFrom(HKCU, RunKey)
  else
    // The hive of the user is loaded, as the user is logged on to start the uninstall.
    RemoveAutoStartFrom(HKU, OriginalUserSid + '\' + RunKey);
end;

// %AppData% of the user who started the uninstall, also when another admin runs this copy.
function OriginalUserAppData(var Dir: String): Boolean;
var
  Profile: String;
begin
  Result := False;
  if OriginalUserSid = '' then
    Exit;

  if OriginalUserIsCurrent then
  begin
    Dir := ExpandConstant('{userappdata}');
    Result := True;
    Exit;
  end;

  // Normally "%USERPROFILE%\AppData\Roaming"; an absolute path when it is redirected.
  if RegQueryStringValue(HKU, OriginalUserSid + '\' + UserShellFoldersKey, 'AppData', Dir) then
  begin
    if (Pos('%USERPROFILE%', Uppercase(Dir)) = 1) and
       RegQueryStringValue(HKLM64, ProfileListKey + '\' + OriginalUserSid, 'ProfileImagePath', Profile) then
      Dir := Profile + Copy(Dir, Length('%USERPROFILE%') + 1, Length(Dir));
    Result := (Dir <> '') and (Pos('%', Dir) = 0);
  end;

  if not Result then
    Result := RegQueryStringValue(HKU, OriginalUserSid + '\' + ShellFoldersKey, 'AppData', Dir) and (Dir <> '');
end;

// Only the settings of the user who started the uninstall; other users keep theirs.
procedure RemoveSettings();
var
  AppData, Dir: String;
begin
  if not OriginalUserAppData(AppData) then
  begin
    Log('Settings left alone: the AppData folder of who started the uninstall is not known');
    Exit;
  end;

  Dir := AddBackslash(AppData) + '{#AppName}';
  if not DirExists(Dir) then
    Exit;

  if SuppressibleMsgBox(CustomMessage('RemoveSettings'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
  begin
    DelTree(Dir, True, True, True);
    Log('Removed ' + Dir);
  end;
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
  begin
    StopApps();
    RemoveAutoStart();
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    UninstallOpenRgb();
    UninstallPawnIo();
    RegDeleteKeyIncludingSubkeys(HKLM64, '{#StateKey}');
    RemoveSettings();
  end;
end;

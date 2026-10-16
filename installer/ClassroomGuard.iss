; ClassroomGuard — one Setup for the whole classroom (Veyon-style).
; Choose "Student computer" or "Teacher computer"; Setup does all configuration.
;
; Silent / mass deployment:
;   Student: ClassroomGuard-Setup.exe /VERYSILENT /TYPE=student /CLASSPASS="..." /TEACHERPASS="..." [/STUDENTUSER="student"]
;   Teacher: ClassroomGuard-Setup.exe /VERYSILENT /TYPE=teacher /CLASSPASS="..."

#ifndef MyVersion
  #define MyVersion "1.0.0"
#endif

[Setup]
AppId={{6F3C9B2E-6E4B-4C55-9E77-5C1B7A0D2F11}
AppName=ClassroomGuard
AppVersion={#MyVersion}
AppVerName=ClassroomGuard {#MyVersion}
AppPublisher=Mirae
DefaultDirName={autopf}\ClassroomGuard
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=ClassroomGuard-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\app.ico
UninstallDisplayName=ClassroomGuard
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "student"; Description: "Student computer  (O'quvchi kompyuteri)"
Name: "teacher"; Description: "Teacher computer  (O'qituvchi kompyuteri)"

[Components]
Name: "student"; Description: "Student: protection service + student app with lessons and tests"; Types: student; Flags: fixed
Name: "teacher"; Description: "Teacher: classroom control console"; Types: teacher; Flags: fixed

[Files]
Source: "..\assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion
; Teacher console is installed directly.
Source: "..\artifacts\Admin\*"; DestDir: "{app}\Admin"; Components: teacher; Flags: ignoreversion recursesubdirs createallsubdirs
; Student payload is unpacked to Setup's private temp folder; install.ps1 installs it with locked-down permissions.
Source: "..\artifacts\Agent\*"; DestDir: "{tmp}\pkg\artifacts\Agent"; Components: student; Flags: dontcopy recursesubdirs createallsubdirs
Source: "..\artifacts\StudentShell\*"; DestDir: "{tmp}\pkg\artifacts\StudentShell"; Components: student; Flags: dontcopy recursesubdirs createallsubdirs
Source: "..\artifacts\Ctl\*"; DestDir: "{tmp}\pkg\artifacts\Ctl"; Components: student; Flags: dontcopy recursesubdirs createallsubdirs
Source: "..\scripts\*.ps1"; DestDir: "{tmp}\pkg\scripts"; Flags: dontcopy
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Components: student; Flags: dontcopy

[Icons]
Name: "{autoprograms}\ClassroomGuard Teacher"; Filename: "{app}\Admin\ClassroomGuard.Admin.exe"; Components: teacher
Name: "{autodesktop}\ClassroomGuard Teacher"; Filename: "{app}\Admin\ClassroomGuard.Admin.exe"; Components: teacher
Name: "{autoprograms}\ClassroomGuard"; Filename: "{app}\StudentShell\ClassroomGuard.StudentShell.exe"; Components: student

[Run]
Filename: "{app}\Admin\ClassroomGuard.Admin.exe"; Description: "Open the teacher console"; Components: teacher; Flags: postinstall nowait skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Recovery\uninstall.ps1"" -Emergency -FromInstaller"; Components: student; Flags: runhidden waituntilterminated; RunOnceId: "RemoveStudent"
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -Command ""Get-NetFirewallRule -Name 'ClassroomGuard-AdminDiscovery' -ErrorAction SilentlyContinue | Remove-NetFirewallRule"""; Components: teacher; Flags: runhidden waituntilterminated; RunOnceId: "RemoveTeacherFirewall"

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\ClassroomGuardTeacher"; Components: teacher

[Code]
var
  ClassPage: TInputQueryWizardPage;
  StudentPage: TInputQueryWizardPage;
  Progress: TOutputMarqueeProgressWizardPage;

procedure InitializeWizard;
begin
  ClassPage := CreateInputQueryPage(wpSelectComponents,
    'Classroom password  (Sinf paroli)',
    'Use the SAME classroom password on the teacher computer and on every student computer.',
    'This password links the computers of one classroom together. Nothing else needs to be copied between computers.' + #13#10 +
    'O''qituvchi va barcha o''quvchi kompyuterlarida bir xil sinf parolini kiriting.');
  ClassPage.Add('Classroom password (at least 8 characters):', True);
  ClassPage.Add('Repeat classroom password:', True);
  ClassPage.Values[0] := ExpandConstant('{param:CLASSPASS|}');
  ClassPage.Values[1] := ClassPage.Values[0];

  StudentPage := CreateInputQueryPage(ClassPage.ID,
    'Student computer  (O''quvchi kompyuteri)',
    'Teacher password and student Windows account for this computer.',
    'The teacher password unlocks this computer locally and is required for emergency removal.' + #13#10 +
    'The student account is created automatically (as a standard user without a password) if it does not exist.');
  StudentPage.Add('Teacher password for this computer (at least 8 characters):', True);
  StudentPage.Add('Repeat teacher password:', True);
  StudentPage.Add('Student Windows account(s), comma-separated:', False);
  StudentPage.Values[0] := ExpandConstant('{param:TEACHERPASS|}');
  StudentPage.Values[1] := StudentPage.Values[0];
  StudentPage.Values[2] := ExpandConstant('{param:STUDENTUSER|' + GetPreviousData('StudentUser', 'student') + '}');
  Progress := CreateOutputMarqueeProgressPage('Configuring this computer', 'Please wait. This can take a minute.');
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'StudentUser', StudentPage.Values[2]);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = StudentPage.ID) and not WizardIsComponentSelected('student');
end;

function ValidAccountNames(const S: String): Boolean;
var
  I, Len: Integer;
  C: Char;
begin
  Result := Trim(S) <> '';
  Len := 0;
  for I := 1 to Length(S) do
  begin
    C := S[I];
    if C = ',' then
    begin
      if Len = 0 then Result := False;
      Len := 0;
    end
    else if Pos(C, '"/\[]:;|=+*?<>@') > 0 then
      Result := False
    else if C <> ' ' then
      Len := Len + 1;
    if Len > 20 then Result := False;
  end;
  if Len = 0 then Result := False;
end;

function ValidateClassroom: String;
begin
  Result := '';
  if Length(ClassPage.Values[0]) < 8 then
    Result := 'The classroom password must contain at least 8 characters.'
  else if ClassPage.Values[0] <> ClassPage.Values[1] then
    Result := 'The two classroom passwords do not match.';
end;

function ValidateStudent: String;
begin
  Result := '';
  if Length(StudentPage.Values[0]) < 8 then
    Result := 'The teacher password must contain at least 8 characters.'
  else if StudentPage.Values[0] <> StudentPage.Values[1] then
    Result := 'The two teacher passwords do not match.'
  else if not ValidAccountNames(StudentPage.Values[2]) then
    Result := 'Enter valid student account names (max. 20 characters each, separated by commas).';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Msg: String;
begin
  Msg := '';
  if CurPageID = ClassPage.ID then Msg := ValidateClassroom
  else if CurPageID = StudentPage.ID then Msg := ValidateStudent;
  if Msg <> '' then MsgBox(Msg, mbError, MB_OK);
  Result := Msg = '';
end;

function WebView2Installed: Boolean;
var
  V: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0')) or
    (RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', V) and (V <> '') and (V <> '0.0.0.0'));
end;

procedure EnsureWebView2;
var
  Code: Integer;
begin
  if WebView2Installed then exit;
  Progress.SetText('Installing Microsoft Edge WebView2 Runtime (needed for lessons)...', '');
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
    Log('WebView2 Runtime install failed or needs internet (code ' + IntToStr(Code) + '). Lessons page will ask for it.');
end;

// Removes an inherited PowerShell 7 PSModulePath so Windows PowerShell 5.1 uses its own modules.
function SetEnvironmentVariable(lpName: String; lpValue: Cardinal): BOOL;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

function RunScript(const Script, Args: String): String;
var
  Code: Integer;
  Err: AnsiString;
  Answers: TArrayOfString;
begin
  Result := '';
  SetArrayLength(Answers, 3);
  Answers[0] := ClassPage.Values[0];
  Answers[1] := StudentPage.Values[0];
  Answers[2] := StudentPage.Values[2];
  if not SaveStringsToUTF8File(ExpandConstant('{tmp}\answers.txt'), Answers, False) then
  begin
    Result := 'Could not write temporary setup answers.';
    exit;
  end;
  SetEnvironmentVariable('PSModulePath', 0);
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\pkg\scripts\') + Script + '" -SetupFile "' + ExpandConstant('{tmp}\answers.txt') + '"' + Args,
      ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := 'Could not start Windows PowerShell: ' + SysErrorMessage(Code)
  else if Code <> 0 then
  begin
    Result := 'Configuring this computer failed (code ' + IntToStr(Code) + ').';
    if LoadStringFromFile(ExpandConstant('{commonappdata}\ClassroomGuard-setup-error.txt'), Err) then
      Result := Result + #13#10#13#10 + String(Err);
    Result := Result + #13#10#13#10 + 'Log: ' + ExpandConstant('{commonappdata}\ClassroomGuard-setup.log');
  end;
  DeleteFile(ExpandConstant('{tmp}\answers.txt'));
end;

// All configuration happens BEFORE Setup registers anything, so a failure aborts cleanly.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := ValidateClassroom;
  if (Result = '') and WizardIsComponentSelected('student') then Result := ValidateStudent;
  if Result <> '' then exit;
  Progress.Show;
  try
    Progress.SetText('Preparing...', '');
    ExtractTemporaryFiles('{tmp}\pkg\scripts\*');
    if WizardIsComponentSelected('student') then
    begin
      EnsureWebView2;
      Progress.SetText('Unpacking the student programs...', '');
      ExtractTemporaryFiles('{tmp}\pkg\artifacts\*');
      if not FileExists(ExpandConstant('{tmp}\pkg\artifacts\Agent\ClassroomGuard.Agent.exe')) or
         not FileExists(ExpandConstant('{tmp}\pkg\artifacts\StudentShell\Learning\index.html')) then
        Result := 'Setup package is incomplete.'
      else
      begin
        Progress.SetText('Installing the ClassroomGuard service and student account...', '');
        Result := RunScript('install.ps1', ' -PackagePath "' + ExpandConstant('{tmp}\pkg\artifacts') + '"');
      end;
    end
    else
    begin
      if RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\ClassroomGuardAgent') then
        Result := 'This computer is set up as a STUDENT computer. Uninstall ClassroomGuard first (Settings > Apps), then install the teacher version.'
      else
      begin
        Progress.SetText('Saving the classroom key and opening classroom discovery...', '');
        Result := RunScript('setup-teacher.ps1', '');
      end;
    end;
  finally
    Progress.Hide;
  end;
end;

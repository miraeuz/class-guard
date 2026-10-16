#Requires -RunAsAdministrator
[CmdletBinding()]
param(
 [string]$ClassroomKeyFile,
 [string[]]$StudentUsers,
 [string]$PackagePath=(Join-Path $PSScriptRoot '..\artifacts'),
 [switch]$RotateKey,
 # Used by ClassroomGuard-Setup.exe: classroom password, teacher password and student accounts.
 [string]$SetupFile
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'setup-common.ps1')
$fromSetup=[bool]$SetupFile
# Any terminating error: show it, record it for Setup.exe, exit non-zero.
trap { Write-Host $_.Exception.Message -ForegroundColor Red; Write-SetupError ($_.Exception.Message); if($fromSetup){try{Stop-Transcript|Out-Null}catch{}}; exit 1 }
$teacherPassword=$null
if($fromSetup){
 try{Remove-Item (Join-Path $env:ProgramData 'ClassroomGuard-setup-error.txt') -ErrorAction SilentlyContinue}catch{}
 Start-Transcript -Path (Join-Path $env:ProgramData 'ClassroomGuard-setup.log') -Force|Out-Null
 $answers=Read-SetupAnswers $SetupFile
 $key=Get-ClassroomKeyFromPassword $answers.ClassroomPassword
 if($answers.TeacherPassword.Length -lt 8){throw 'Teacher password must contain at least 8 characters.'}
 $teacherPassword=$answers.TeacherPassword
 $StudentUsers=$answers.StudentUsers
 $RotateKey=$true
 # Create missing student accounts as standard users without a password (Setup is Veyon-style: no prep work).
 foreach($name in $StudentUsers){
  if(!(Get-LocalUser -Name $name -ErrorAction SilentlyContinue)){
   New-LocalUser -Name $name -NoPassword -AccountNeverExpires -Description 'ClassroomGuard student account'|Out-Null
   Set-LocalUser -Name $name -PasswordNeverExpires $true
   Write-Host "Created standard student account: $name"
  }
  try{Add-LocalGroupMember -SID 'S-1-5-32-545' -Member $name -ErrorAction Stop}catch [Microsoft.PowerShell.Commands.MemberExistsException]{}
 }
}else{
 if(!$ClassroomKeyFile -or !$StudentUsers){throw 'Use -ClassroomKeyFile and -StudentUsers (or run ClassroomGuard-Setup.exe).'}
 $key=(Get-Content -LiteralPath $ClassroomKeyFile -Raw).Trim()
}
$installDir=Join-Path $env:ProgramFiles 'ClassroomGuard'
$dataDir=Join-Path $env:ProgramData 'ClassroomGuard'
function Native([string]$Command,[string[]]$Arguments){ & $Command @Arguments; if($LASTEXITCODE -ne 0){throw "$Command failed: $LASTEXITCODE"} }
if([Convert]::FromBase64String($key).Length -ne 32){throw 'A 32-byte classroom key is required.'}
foreach($pair in @(@('Agent','ClassroomGuard.Agent.exe'),@('StudentShell','ClassroomGuard.StudentShell.exe'),@('Ctl','cgctl.exe'))){
 if(!(Test-Path -LiteralPath (Join-Path (Join-Path $PackagePath $pair[0]) $pair[1]))){throw 'Build first: scripts\build.ps1. Incomplete deployment package.'}
}
function Get-AdministratorSids {
 try { return @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction Stop | ForEach-Object {$_.SID.Value}) }
 catch {
  # Get-LocalGroupMember fails on PCs with orphaned/cloud members; fall back to ADSI.
  $name=([Security.Principal.SecurityIdentifier]'S-1-5-32-544').Translate([Security.Principal.NTAccount]).Value.Split('\')[-1]
  $group=[ADSI]"WinNT://$env:COMPUTERNAME/$name,group"
  return @($group.Invoke('Members')|ForEach-Object{(New-Object Security.Principal.SecurityIdentifier ([byte[]]$_.GetType().InvokeMember('objectSid','GetProperty',$null,$_,$null)),0).Value})
 }
}
$adminMembers=Get-AdministratorSids
$sids=@($StudentUsers|ForEach-Object{
 $user=Get-LocalUser -Name $_
 if(!$user.Enabled){throw "Student account disabled: $_"}
 if($adminMembers -contains $user.SID.Value){throw "Student account must not be an administrator: $_"}
 $user.SID.Value
})
if($sids.Count -eq 0){throw 'At least one standard student account is required.'}
$legacy='HKLM:\SOFTWARE\Policies\Microsoft\Windows\Safer\CodeIdentifiers'
if((Get-ItemProperty -Path $legacy -Name '__ClassroomGuard' -ErrorAction SilentlyContinue)){
 throw 'Legacy SRP is armed. Restore the original Windows policy before installing v2. No safe original policy can be inferred.'
}
$existing=Get-Service ClassroomGuardAgent -ErrorAction SilentlyContinue
if((Test-Path (Join-Path $dataDir 'classroom.key')) -and ((Get-Content (Join-Path $dataDir 'classroom.key') -Raw).Trim() -ne $key) -and !$RotateKey){throw 'Key differs. Use -RotateKey after updating the teacher console intentionally.'}
if($existing){Stop-Service ClassroomGuardAgent -Force;$existing.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))}
New-Item -ItemType Directory -Force -Path $installDir,$dataDir|Out-Null
# Secret/state directory: remove inherited AND explicit student-readable grants.
function Protect-Item([string]$Path) {
 $item=Get-Item -LiteralPath $Path
 if($item.PSIsContainer){$acl=New-Object System.Security.AccessControl.DirectorySecurity;$inherit=[System.Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'}
 else{$acl=New-Object System.Security.AccessControl.FileSecurity;$inherit=[System.Security.AccessControl.InheritanceFlags]::None}
 $acl.SetAccessRuleProtection($true,$false)
 foreach($sid in @('S-1-5-18','S-1-5-32-544')){
  $rule=New-Object System.Security.AccessControl.FileSystemAccessRule([System.Security.Principal.SecurityIdentifier]$sid,'FullControl',$inherit,'None','Allow')
  $acl.AddAccessRule($rule)
 }
 Set-Acl -LiteralPath $Path -AclObject $acl
}
Protect-Item $dataDir
Get-ChildItem -LiteralPath $dataDir -Recurse -Force|ForEach-Object{Protect-Item $_.FullName}
# Exact Program Files ACL: SYSTEM/Admins write; Users read/execute only.
$programAcl=New-Object System.Security.AccessControl.DirectorySecurity
$programAcl.SetAccessRuleProtection($true,$false)
foreach($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-32-545','ReadAndExecute'))){
 $rule=New-Object System.Security.AccessControl.FileSystemAccessRule([System.Security.Principal.SecurityIdentifier]$entry[0],$entry[1],'ContainerInherit,ObjectInherit','None','Allow')
 $programAcl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $installDir -AclObject $programAcl
# Remove inherited/explicit historical grants on existing installed files.
Get-ChildItem -LiteralPath $installDir -Recurse -Force|ForEach-Object {
 $acl=Get-Acl -LiteralPath $_.FullName
 $acl.SetAccessRuleProtection($false,$false)
 foreach($rule in @($acl.Access|Where-Object{!$_.IsInherited})){$acl.RemoveAccessRuleSpecific($rule)}
 Set-Acl -LiteralPath $_.FullName -AclObject $acl
}
Get-Process -Name 'ClassroomGuard.StudentShell' -ErrorAction SilentlyContinue|Stop-Process -Force
foreach($project in @('Agent','StudentShell','Ctl')){
 $dest=Join-Path $installDir $project
 New-Item -ItemType Directory -Force -Path $dest|Out-Null
 Copy-Item -Path (Join-Path (Join-Path $PackagePath $project) '*') -Destination $dest -Recurse -Force
}
# Preserve an existing emergency credential on upgrade; initialize only a new install.
. (Join-Path $PSScriptRoot 'emergency-password.ps1')
$emergencyPath=Join-Path $dataDir 'emergency-password.json'
if($teacherPassword){
 # Setup: the teacher password also opens emergency removal on this PC. No public default.
 Save-EmergencyRecord $emergencyPath (New-EmergencyRecord $teacherPassword)
}elseif(!(Test-Path -LiteralPath $emergencyPath)){
 Save-EmergencyRecord $emergencyPath (New-EmergencyRecord '123mirae321')
}else{Read-EmergencyRecord $emergencyPath|Out-Null}
Protect-Item $emergencyPath
$recoveryDir=Join-Path $installDir 'Recovery'
New-Item -ItemType Directory -Force -Path $recoveryDir|Out-Null
foreach($scriptName in @('emergency-uninstall.ps1','emergency-password.ps1','uninstall.ps1','setup-common.ps1')){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $scriptName) -Destination $recoveryDir -Force
}
[IO.File]::WriteAllText((Join-Path $dataDir 'classroom.key'),$key)
if($teacherPassword){
 # Same format as CredentialStore (PBKDF2-SHA256, 100,000 iterations, 16-byte salt, 32-byte hash).
 $salt=New-Object byte[] 16;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();try{$rng.GetBytes($salt)}finally{$rng.Dispose()}
 $kdf=[Security.Cryptography.Rfc2898DeriveBytes]::new($teacherPassword,$salt,100000,[Security.Cryptography.HashAlgorithmName]::SHA256)
 try{$hash=[Convert]::ToBase64String($kdf.GetBytes(32))}finally{$kdf.Dispose()}
 $users=@{Users=@{teacher=@{Salt=[Convert]::ToBase64String($salt);Hash=$hash;MustChange=$false}}}
 [IO.File]::WriteAllText((Join-Path $dataDir 'users.json'),($users|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
 Remove-Item -LiteralPath (Join-Path $dataDir 'bootstrap.txt') -ErrorAction SilentlyContinue
 $teacherPassword=$null
}
$configPath=Join-Path $dataDir 'agent.json'
$config=@{EnableSrpEnforcement=$false;StudentUserSids=$sids;CommandPort=8787;AnnouncePort=8788;TelegramBotToken='';TelegramChatId=''}
if(Test-Path $configPath){
 $old=Get-Content $configPath -Raw|ConvertFrom-Json
 foreach($name in @('TelegramBotToken','TelegramChatId','CommandPort','AnnouncePort')){if($null -ne $old.$name){$config[$name]=$old.$name}}
}
foreach($port in @($config.CommandPort,$config.AnnouncePort)){if($port -lt 1024 -or $port -gt 65535){throw 'Invalid existing port configuration.'}}
$config|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $configPath -Encoding UTF8
$exe=Join-Path $installDir 'Agent\ClassroomGuard.Agent.exe'
$quoted='"'+$exe+'"'
if($existing){Native 'sc.exe' @('config','ClassroomGuardAgent','binPath=',$quoted,'start=','auto','obj=','LocalSystem')}
else{Native 'sc.exe' @('create','ClassroomGuardAgent','binPath=',$quoted,'start=','auto','obj=','LocalSystem')}
Native 'sc.exe' @('failure','ClassroomGuardAgent','reset=','86400','actions=','restart/5000/restart/10000/restart/30000')
Native 'sc.exe' @('failureflag','ClassroomGuardAgent','1')
Get-NetFirewallRule -Name 'ClassroomGuard-Agent' -ErrorAction SilentlyContinue|Remove-NetFirewallRule
New-NetFirewallRule -Name 'ClassroomGuard-Agent' -DisplayName 'ClassroomGuard Agent TLS' -Direction Inbound -Program $exe -Protocol TCP -LocalPort $config.CommandPort -RemoteAddress LocalSubnet -Profile Any -Action Allow|Out-Null
$action=New-ScheduledTaskAction -Execute (Join-Path $installDir 'StudentShell\ClassroomGuard.StudentShell.exe')
$trigger=New-ScheduledTaskTrigger -AtLogOn
$principal=New-ScheduledTaskPrincipal -GroupId 'S-1-5-32-545' -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName 'ClassroomGuard-StudentShell' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force|Out-Null
Start-Service ClassroomGuardAgent
(Get-Service ClassroomGuardAgent).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
Start-Sleep -Seconds 2
& (Join-Path $installDir 'Ctl\cgctl.exe') status
if($LASTEXITCODE -ne 0){throw 'Agent health endpoint failed. Files and recovery backups retained. Inspect the Windows event log.'}
Write-Host "Installed. Inspect status control health before using the lab."
if($fromSetup){
 Write-Host 'Teacher password set by Setup (also used for emergency removal). The teacher console finds this PC automatically.'
 try{Stop-Transcript|Out-Null}catch{}
 exit 0
}
Write-Host "Bootstrap credential: $dataDir\bootstrap.txt"
Write-Host "Change it: & '$installDir\Ctl\cgctl.exe' passwd teacher"
Write-Host 'The teacher console trusts this PC automatically when both use the same classroom key.'
Write-Host 'Configure Telegram on each Agent if needed, then restart the service.'

Write-Host 'Emergency removal: StudentShell Settings. Default emergency password: 123mirae321. Change it before student use.'

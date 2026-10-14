#Requires -RunAsAdministrator
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ClassroomKeyFile,
 [Parameter(Mandatory=$true)][string[]]$StudentUsers,
 [string]$PackagePath=(Join-Path $PSScriptRoot '..\artifacts'),
 [switch]$RotateKey
)
$ErrorActionPreference='Stop'
$installDir=Join-Path $env:ProgramFiles 'ClassroomGuard'
$dataDir=Join-Path $env:ProgramData 'ClassroomGuard'
function Native([string]$Command,[string[]]$Arguments){ & $Command @Arguments; if($LASTEXITCODE -ne 0){throw "$Command failed: $LASTEXITCODE"} }
$key=(Get-Content -LiteralPath $ClassroomKeyFile -Raw).Trim()
if([Convert]::FromBase64String($key).Length -ne 32){throw 'A 32-byte classroom key is required.'}
foreach($pair in @(@('Agent','ClassroomGuard.Agent.exe'),@('StudentShell','ClassroomGuard.StudentShell.exe'),@('Ctl','cgctl.exe'))){
 if(!(Test-Path -LiteralPath (Join-Path (Join-Path $PackagePath $pair[0]) $pair[1]))){throw 'Build first: scripts\build.ps1. Incomplete deployment package.'}
}
$adminMembers=@(Get-LocalGroupMember -SID 'S-1-5-32-544' | ForEach-Object {$_.SID.Value})
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
foreach($project in @('Agent','StudentShell','Ctl')){
 $dest=Join-Path $installDir $project
 New-Item -ItemType Directory -Force -Path $dest|Out-Null
 Copy-Item -Path (Join-Path (Join-Path $PackagePath $project) '*') -Destination $dest -Recurse -Force
}
# Preserve an existing emergency credential on upgrade; initialize only a new install.
. (Join-Path $PSScriptRoot 'emergency-password.ps1')
$emergencyPath=Join-Path $dataDir 'emergency-password.json'
if(!(Test-Path -LiteralPath $emergencyPath)){
 Save-EmergencyRecord $emergencyPath (New-EmergencyRecord '123mirae321')
}else{Read-EmergencyRecord $emergencyPath|Out-Null}
Protect-Item $emergencyPath
$recoveryDir=Join-Path $installDir 'Recovery'
New-Item -ItemType Directory -Force -Path $recoveryDir|Out-Null
foreach($scriptName in @('emergency-uninstall.ps1','emergency-password.ps1','uninstall.ps1')){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $scriptName) -Destination $recoveryDir -Force
}
[IO.File]::WriteAllText((Join-Path $dataDir 'classroom.key'),$key)
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
New-NetFirewallRule -Name 'ClassroomGuard-Agent' -DisplayName 'ClassroomGuard Agent TLS' -Direction Inbound -Program $exe -Protocol TCP -LocalPort $config.CommandPort -RemoteAddress LocalSubnet -Profile Private,Domain -Action Allow|Out-Null
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
Write-Host "Bootstrap credential: $dataDir\bootstrap.txt"
Write-Host "Change it: & '$installDir\Ctl\cgctl.exe' passwd teacher"
Write-Host "Copy $dataDir\device-enrollment.json securely to the teacher PC and enroll its certificate."
Write-Host 'Configure Telegram on each Agent if needed, then restart the service.'

Write-Host 'Emergency removal: StudentShell Settings. Default emergency password: 123mirae321. Change it before student use.'

#Requires -RunAsAdministrator
[CmdletBinding()]
param([switch]$Emergency,[switch]$RemoveData)
$ErrorActionPreference='Stop'
$installDir=Join-Path $env:ProgramFiles 'ClassroomGuard'
$dataDir=Join-Path $env:ProgramData 'ClassroomGuard'
$ctl=Join-Path $installDir 'Ctl\cgctl.exe'
if(!(Test-Path $ctl)){throw 'Recovery tool missing. Restore the published Ctl folder before removing any data.'}
$service=Get-Service ClassroomGuardAgent -ErrorAction SilentlyContinue
if(!$Emergency){
 & $ctl uninstall teacher
 if($LASTEXITCODE -ne 0){throw 'Restore failed. Files/backups retained. Use -Emergency only for an unavailable service after reviewing the cause.'}
}
if($service){Stop-Service ClassroomGuardAgent -Force -ErrorAction Stop;$service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))}
if($Emergency){
 & $ctl emergency-restore
 if($LASTEXITCODE -ne 0){throw 'Emergency restore incomplete. Service remains stopped; all recovery data retained.'}
}
# Re-run restoration idempotently, ensuring durable backup handling is complete.
& $ctl emergency-restore
if($LASTEXITCODE -ne 0){throw 'Final restoration check failed. No files removed.'}
if($service){& sc.exe delete ClassroomGuardAgent;if($LASTEXITCODE -ne 0){throw 'Service deletion failed.'}}
Unregister-ScheduledTask -TaskName 'ClassroomGuard-StudentShell' -Confirm:$false -ErrorAction SilentlyContinue
Get-Process -Name 'ClassroomGuard.StudentShell' -ErrorAction SilentlyContinue|Stop-Process -Force
Get-NetFirewallRule -Name 'ClassroomGuard-Agent' -ErrorAction SilentlyContinue|Remove-NetFirewallRule
Remove-Item -LiteralPath $installDir -Recurse -Force
if($RemoveData){Remove-Item -LiteralPath $dataDir -Recurse -Force}
else{Write-Host "Results and private backup/config retained at $dataDir. Use -RemoveData during removal if you intend to delete them."}
if(Test-Path $installDir){throw 'Program Files cleanup incomplete.'}
Write-Host 'Service, task, Agent firewall rule and installed programs removed. Owned policies restored.'

#Requires -RunAsAdministrator
# Called by ClassroomGuard-Setup.exe for the Teacher choice. Stores the classroom key
# (derived from the classroom password) for the teacher console and opens UDP discovery.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SetupFile,[int]$Port=8788)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'setup-common.ps1')
trap { Write-Host $_.Exception.Message -ForegroundColor Red; Write-SetupError ($_.Exception.Message); exit 1 }
Remove-Item (Join-Path $env:ProgramData 'ClassroomGuard-setup-error.txt') -ErrorAction SilentlyContinue
$answers=Read-SetupAnswers $SetupFile
$key=Get-ClassroomKeyFromPassword $answers.ClassroomPassword
$dir=Join-Path $env:ProgramData 'ClassroomGuardTeacher'
New-Item -ItemType Directory -Force -Path $dir|Out-Null
# Teacher PC: SYSTEM/Administrators full control, local users read (the console runs as the teacher).
$acl=New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
$inherit=[System.Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
foreach($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-32-545','ReadAndExecute'))){
 $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule([System.Security.Principal.SecurityIdentifier]$entry[0],$entry[1],$inherit,'None','Allow')))
}
Set-Acl -LiteralPath $dir -AclObject $acl
[IO.File]::WriteAllText((Join-Path $dir 'classroom.key'),$key)
Get-NetFirewallRule -Name 'ClassroomGuard-AdminDiscovery' -ErrorAction SilentlyContinue|Remove-NetFirewallRule
New-NetFirewallRule -Name 'ClassroomGuard-AdminDiscovery' -DisplayName 'ClassroomGuard Teacher Discovery' -Direction Inbound -Protocol UDP -LocalPort $Port -RemoteAddress LocalSubnet -Profile Any -Action Allow|Out-Null
Write-Host 'Teacher computer configured.'
exit 0

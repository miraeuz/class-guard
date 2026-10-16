#Requires -RunAsAdministrator
param([int]$Port=8788)
$ErrorActionPreference='Stop'
if($Port -lt 1024 -or $Port -gt 65535){throw 'Invalid port.'}
Get-NetFirewallRule -Name 'ClassroomGuard-AdminDiscovery' -ErrorAction SilentlyContinue|Remove-NetFirewallRule
New-NetFirewallRule -Name 'ClassroomGuard-AdminDiscovery' -DisplayName 'ClassroomGuard Teacher Discovery' -Direction Inbound -Protocol UDP -LocalPort $Port -RemoteAddress LocalSubnet -Profile Any -Action Allow|Out-Null

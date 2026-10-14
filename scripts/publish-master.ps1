param([string]$Out=(Join-Path $PSScriptRoot '..\artifacts\Admin'))
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
& dotnet publish (Join-Path $root 'src\ClassroomGuard.Admin\ClassroomGuard.Admin.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $Out
if($LASTEXITCODE -ne 0){throw 'Admin publish failed.'}
Write-Host "Published $Out. Run install-admin-firewall.ps1 elevated on the teacher PC."

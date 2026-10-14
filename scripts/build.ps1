[CmdletBinding()]
param([string]$Out = (Join-Path $PSScriptRoot '..\artifacts'))
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
function Run-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $Arguments" }
}
Run-Dotnet @('build', (Join-Path $root 'ClassroomGuard.sln'), '-c','Release')
Run-Dotnet @('run','--project',(Join-Path $root 'tests\ClassroomGuard.Tests\ClassroomGuard.Tests.csproj'),'-c','Release')
foreach($project in @('Agent','StudentShell','Admin','Ctl')) {
    Run-Dotnet @('publish',(Join-Path $root "src\ClassroomGuard.$project\ClassroomGuard.$project.csproj"),'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false','-o',(Join-Path $Out $project))
}
Write-Host "Build and regression checks passed. Full deployment folders: $Out"

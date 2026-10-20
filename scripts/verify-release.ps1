[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$ReleasePath,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint
)
$ErrorActionPreference='Stop'
$base=(Resolve-Path -LiteralPath $ReleasePath).Path
if(Test-Path -LiteralPath (Join-Path $base 'SIGNING-INCOMPLETE.txt')){throw 'Release signing did not complete.'}
$required=@('Agent\ClassroomGuard.Agent.exe','StudentShell\ClassroomGuard.StudentShell.exe','Admin\ClassroomGuard.Admin.exe','Ctl\cgctl.exe')
foreach($name in $required){if(!(Test-Path -LiteralPath (Join-Path "$base\artifacts" $name))){throw "Missing executable: $name"}}
$targets=@(Get-ChildItem -LiteralPath "$base\artifacts" -Recurse -File | Where-Object {$_.Name -match '^(ClassroomGuard\..*|cgctl)\.(exe|dll)$'})
$targets+=@(Get-ChildItem -LiteralPath "$base\scripts" -Filter '*.ps1' -File)
if($targets.Count -lt 5){throw 'Incomplete release.'}
foreach($file in $targets){
 $signature=Get-AuthenticodeSignature -LiteralPath $file.FullName
 if($signature.Status -ne 'Valid'){throw "Invalid or untrusted signature: $($file.FullName) [$($signature.Status)]"}
 if($signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint){throw "Unexpected publisher certificate: $($file.FullName)"}
 if(!$signature.TimeStamperCertificate){throw "Missing timestamp: $($file.FullName)"}
 Write-Host "Verified: $($file.FullName)"
}
Write-Host 'Publisher signatures and timestamps verified. SmartScreen reputation is a separate check.'

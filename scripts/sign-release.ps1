# Run on the Windows release PC after scripts\build.ps1.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
 [Parameter(Mandatory=$true)][string]$SignToolPath,
 [Parameter(Mandatory=$true)][uri]$TimestampUrl,
 [Parameter(Mandatory=$true)][uri]$ScriptTimestampUrl,
 [Parameter(Mandatory=$true)][string]$ReleasePath,
 [string]$ArtifactsPath=(Join-Path $PSScriptRoot '..\artifacts'),
 [ValidateSet('CurrentUser','LocalMachine')][string]$CertificateStore='CurrentUser'
)
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'Signing requires Windows and a trusted code-signing certificate.'}
foreach($url in @($TimestampUrl,$ScriptTimestampUrl)){
 if(!$url.IsAbsoluteUri -or $url.Scheme -notin @('http','https')){throw 'Specify the certificate provider timestamp server URLs.'}
}
$tool=(Resolve-Path -LiteralPath $SignToolPath).Path
$inputPath=(Resolve-Path -LiteralPath $ArtifactsPath).Path
$output=[IO.Path]::GetFullPath($ReleasePath)
if(Test-Path -LiteralPath $output){throw 'Choose a NEW release folder. Existing output is never overwritten.'}
if($output.StartsWith($inputPath.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Release folder must be outside the build artifacts.'}
$cert=Get-Item -LiteralPath "Cert:\$CertificateStore\My\$CertificateThumbprint"
if(!$cert.HasPrivateKey){throw 'Certificate private key is unavailable. Connect the signing token/provider.'}
if($cert.NotAfter -le (Get-Date) -or $cert.NotBefore -gt (Get-Date)){throw 'Certificate is not currently valid.'}
if(!($cert.EnhancedKeyUsageList | Where-Object {$_.ObjectId -eq '1.3.6.1.5.5.7.3.3'})){throw 'Certificate is not authorized for code signing.'}
$chain=New-Object System.Security.Cryptography.X509Certificates.X509Chain
try {if(!$chain.Build($cert)){throw 'Certificate chain is not trusted on this release PC.'}} finally {$chain.Dispose()}
foreach($pair in @(@('Agent','ClassroomGuard.Agent.exe'),@('StudentShell','ClassroomGuard.StudentShell.exe'),@('Admin','ClassroomGuard.Admin.exe'),@('Ctl','cgctl.exe'))){
 if(!(Test-Path -LiteralPath (Join-Path (Join-Path $inputPath $pair[0]) $pair[1]))){throw "Build all deployment folders first: $($pair[0]) is missing."}
}
# Sign staged copies only; source and previously built files stay untouched.
New-Item -ItemType Directory -Path $output | Out-Null
$marker=Join-Path $output 'SIGNING-INCOMPLETE.txt'
'Signing is incomplete. Do not distribute this folder.' | Set-Content -LiteralPath $marker
Copy-Item -LiteralPath $inputPath -Destination (Join-Path $output 'artifacts') -Recurse
Copy-Item -LiteralPath $PSScriptRoot -Destination (Join-Path $output 'scripts') -Recurse
foreach($name in @('DEPLOY.md','SIGNING.md')){Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) $name) -Destination $output}
$files=@(Get-ChildItem -LiteralPath "$output\artifacts" -Recurse -File | Where-Object {$_.Name -match '^(ClassroomGuard\..*|cgctl)\.(exe|dll)$'})
foreach($file in $files){
 $arguments=@('sign','/sha1',$CertificateThumbprint,'/s','My','/fd','SHA256','/tr',$TimestampUrl.AbsoluteUri,'/td','SHA256','/d','Mirae Class Guard')
 if($CertificateStore -eq 'LocalMachine'){$arguments+='/sm'}
 $arguments+=$file.FullName
 & $tool @arguments
 if($LASTEXITCODE -ne 0){throw "Signing failed: $($file.FullName)"}
 & $tool verify /pa /all /v $file.FullName
 if($LASTEXITCODE -ne 0){throw "Verification failed: $($file.FullName)"}
}
foreach($file in Get-ChildItem -LiteralPath "$output\scripts" -Filter '*.ps1' -File){
 $signed=Set-AuthenticodeSignature -LiteralPath $file.FullName -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $ScriptTimestampUrl.AbsoluteUri
 if($signed.Status -ne 'Valid' -or !$signed.TimeStamperCertificate){throw "Script signing/timestamp failed: $($file.FullName)"}
}
Remove-Item -LiteralPath $marker
try { & (Join-Path $PSScriptRoot 'verify-release.ps1') -ReleasePath $output -CertificateThumbprint $CertificateThumbprint }
catch { 'Verification failed. Do not distribute.' | Set-Content -LiteralPath $marker; throw }
# Hashes detect transfer damage; they do not independently prove publisher identity.
$rows=@(Get-ChildItem -LiteralPath $output -Recurse -File | ForEach-Object {
 [pscustomobject]@{Path=$_.FullName.Substring($output.TrimEnd('\').Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
ConvertTo-Json -InputObject $rows -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.json') -Encoding UTF8
Write-Host "Signed release created: $output"
Write-Host 'No SmartScreen, Defender, UAC or certificate-store settings were changed.'

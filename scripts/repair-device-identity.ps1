#Requires -RunAsAdministrator
[CmdletBinding()]
param([switch]$ConfirmRepair)
$ErrorActionPreference='Stop'
if(!$ConfirmRepair){throw 'Run only on a PC with a confirmed duplicate ID: add -ConfirmRepair. Its certificate must be re-enrolled on the teacher PC afterwards.'}
$data=Join-Path $env:ProgramData 'ClassroomGuard'
$service=Get-Service 'ClassroomGuardAgent'
if(!(Test-Path -LiteralPath $data)){throw 'ClassroomGuard is not installed.'}
$idPath=Join-Path $data 'device-id.txt'
$certPath=Join-Path $data 'device.pfx'
if(!(Test-Path -LiteralPath $certPath)){throw 'Device certificate missing. Repair installation first.'}
$hadId=Test-Path -LiteralPath $idPath
$oldId=if($hadId){Get-Content -LiteralPath $idPath -Raw}else{$null}
$newId=[guid]::NewGuid().ToString('D')
$backup=Join-Path $data ('device-certificate-backup-'+[guid]::NewGuid().ToString('N')+'.pfx')
Stop-Service 'ClassroomGuardAgent'
$service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
try {
 if(Test-Path -LiteralPath $certPath){Move-Item -LiteralPath $certPath -Destination $backup}
 [IO.File]::WriteAllText($idPath,$newId)
 Start-Service 'ClassroomGuardAgent'
 $ready=$false
 for($i=0;$i -lt 15;$i++){
  Start-Sleep -Seconds 1
  $enrollment=Join-Path $data 'device-enrollment.json'
  if(Test-Path -LiteralPath $enrollment){
   try{$entry=Get-Content -LiteralPath $enrollment -Raw | ConvertFrom-Json;if($entry.MachineId -eq $newId){$ready=$true;break}}catch{}
  }
 }
 if(!$ready){throw 'Agent did not publish the new enrollment file.'}
 Write-Host "New identity: $newId"
 Write-Host "Import $enrollment into teacher Settings. Transfer ONLY this public JSON, never device.pfx or its backup."
 Write-Host 'Sign out and back in on this student PC after completing enrollment. Policies, passwords and attempts are retained.'
} catch {
 Stop-Service 'ClassroomGuardAgent' -ErrorAction SilentlyContinue
 if($hadId){[IO.File]::WriteAllText($idPath,$oldId)}else{Remove-Item -LiteralPath $idPath -ErrorAction SilentlyContinue}
 if(Test-Path -LiteralPath $backup){Remove-Item -LiteralPath $certPath -ErrorAction SilentlyContinue;Move-Item -LiteralPath $backup -Destination $certPath}
 Start-Service 'ClassroomGuardAgent'
 throw
}

# Shared by the elevated installer and recovery UI. Never sourced from a user-writable path.
Set-StrictMode -Version Latest
function Get-EmergencyHash([string]$Secret,[byte[]]$Salt) {
 $kdf=[System.Security.Cryptography.Rfc2898DeriveBytes]::new($Secret,$Salt,210000,[System.Security.Cryptography.HashAlgorithmName]::SHA256)
 try { return [Convert]::ToBase64String($kdf.GetBytes(32)) } finally { $kdf.Dispose() }
}
function Save-EmergencyRecord([string]$Path,$Record) {
 $temp=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
 try {
  [IO.File]::WriteAllText($temp,($Record|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
  if([IO.File]::Exists($Path)){[IO.File]::Replace($temp,$Path,$null)}else{[IO.File]::Move($temp,$Path)}
 } finally { if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)} }
}
function New-EmergencyRecord([string]$Secret) {
 $salt=New-Object byte[] 16;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
 try{$rng.GetBytes($salt)}finally{$rng.Dispose()}
 return @{Version=1;Salt=[Convert]::ToBase64String($salt);Hash=(Get-EmergencyHash $Secret $salt);FailedAttempts=0;LockUntil=0}
}
function Read-EmergencyRecord([string]$Path) {
 if(!(Test-Path -LiteralPath $Path)){throw 'Favqulodda parol fayli yoʻq. Administrator yangilangan installer orqali tiklasin.'}
 $r=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json
 if($r.Version -ne 1 -or [Convert]::FromBase64String($r.Salt).Length -ne 16 -or [Convert]::FromBase64String($r.Hash).Length -ne 32 -or $r.FailedAttempts -lt 0 -or $r.FailedAttempts -gt 5 -or $r.LockUntil -lt 0){throw 'Favqulodda parol fayli buzilgan. Cheklovlar oʻzgartirilmadi.'}
 return $r
}
function Test-EmergencySecret([string]$Secret,$Record) {
 $actual=[Convert]::FromBase64String((Get-EmergencyHash $Secret ([Convert]::FromBase64String($Record.Salt))))
 $expected=[Convert]::FromBase64String($Record.Hash)
 $diff=0;for($i=0;$i -lt 32;$i++){$diff=$diff -bor ($actual[$i] -bxor $expected[$i])}
 return $diff -eq 0
}

# Shared by Setup.exe-driven installs. Never sourced from a user-writable path.

# Windows PowerShell 5.1 started from PowerShell 7 inherits PS7 module paths and then loads
# incompatible Security/LocalAccounts modules (Get-Acl, Set-Acl break). Keep only 5.1 paths.
if ($PSVersionTable.PSEdition -eq 'Desktop') {
 $env:PSModulePath = (($env:PSModulePath -split ';') | Where-Object { $_ -and $_ -notmatch '\\PowerShell\\7|\\Documents\\PowerShell\\|\\Program Files\\PowerShell\\' }) -join ';'
}

# The classroom key is derived from the classroom password, so the teacher PC and every
# student PC end up with the same 32-byte key without copying any key file.
# The salt is fixed on purpose (all PCs must derive the same key); the iteration count
# makes guessing the password expensive.
function Get-ClassroomKeyFromPassword([string]$Password) {
 if ($Password.Length -lt 8) { throw 'Classroom password must contain at least 8 characters.' }
 $salt = [Text.Encoding]::UTF8.GetBytes('ClassroomGuard/classroom-key/v1')
 $kdf = [Security.Cryptography.Rfc2898DeriveBytes]::new($Password, $salt, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256)
 try { return [Convert]::ToBase64String($kdf.GetBytes(32)) } finally { $kdf.Dispose() }
}

# Setup.exe writes the answers to a file in its private temp folder (never the command line).
# Line 1: classroom password. Line 2: teacher password (student PCs). Line 3: student account(s).
function Read-SetupAnswers([string]$Path) {
 $lines = [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)
 try { [IO.File]::Delete($Path) } catch { }
 $get = { param($i) if ($lines.Count -gt $i) { $lines[$i] } else { '' } }
 return [pscustomobject]@{
  ClassroomPassword = (& $get 0)
  TeacherPassword   = (& $get 1)
  StudentUsers      = @((& $get 2) -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
 }
}

function Write-SetupError([string]$Message) {
 try { [IO.File]::WriteAllText((Join-Path $env:ProgramData 'ClassroomGuard-setup-error.txt'), $Message) } catch { }
}

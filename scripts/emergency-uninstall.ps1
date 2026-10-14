#Requires -RunAsAdministrator
[CmdletBinding()]
param([switch]$ChangePassword)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
. (Join-Path $PSScriptRoot 'emergency-password.ps1')
$recordPath=Join-Path $env:ProgramData 'ClassroomGuard\emergency-password.json'
$mutex=New-Object Threading.Mutex($false,'Global\ClassroomGuard-EmergencyRecovery')
$owns=$false
function Ask-Secret([string]$Title,[string]$Message) {
 $window=New-Object Windows.Window
 $window.Title=$Title;$window.Width=470;$window.SizeToContent='Height';$window.WindowStartupLocation='CenterScreen';$window.ResizeMode='NoResize'
 $panel=New-Object Windows.Controls.StackPanel;$panel.Margin='24';$window.Content=$panel
 $text=New-Object Windows.Controls.TextBlock;$text.Text=$Message;$text.TextWrapping='Wrap';$text.Margin='0,0,0,16';$panel.Children.Add($text)|Out-Null
 $box=New-Object Windows.Controls.PasswordBox;$box.MaxLength=256;$box.Padding='10';$panel.Children.Add($box)|Out-Null
 $ok=New-Object Windows.Controls.Button;$ok.Content='Davom etish';$ok.IsDefault=$true;$ok.Padding='14,9';$ok.Margin='0,18,0,0';$panel.Children.Add($ok)|Out-Null
 $ok.Add_Click({if($box.Password.Length -gt 0){$window.DialogResult=$true}})
 $cancel=New-Object Windows.Controls.Button;$cancel.Content='Bekor qilish';$cancel.IsCancel=$true;$cancel.Margin='0,8,0,0';$cancel.Padding='14,8';$panel.Children.Add($cancel)|Out-Null
 $window.Add_ContentRendered({$box.Focus()|Out-Null})
 if($window.ShowDialog() -eq $true){return $box.Password};return $null
}
try {
 try{$owns=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owns=$true}
 if(!$owns){throw 'Boshqa tiklash oynasi ochiq. Avval uni yoping.'}
 $record=Read-EmergencyRecord $recordPath
 $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
 if([long]$record.LockUntil -gt $now){throw 'Koʻp xato urinish. 5 daqiqalik kutish tugagach qayta urinib koʻring.'}
 $secret=Ask-Secret 'Mirae · Favqulodda tiklash' 'Favqulodda parolni kiriting. Hozircha hech qanday cheklov yoki fayl oʻzgartirilmaydi.'
 if($null -eq $secret){exit 0}
 $valid=Test-EmergencySecret $secret $record;$secret=$null
 if(!$valid){
  if($record.LockUntil -gt 0){$record.FailedAttempts=0;$record.LockUntil=0}
  $record.FailedAttempts=[int]$record.FailedAttempts+1
  if($record.FailedAttempts -ge 5){$record.LockUntil=$now+300}
  Save-EmergencyRecord $recordPath $record
  throw 'Parol notoʻgʻri. Hech narsa olib tashlanmadi. 5 xatodan soʻng 5 daqiqa kutish talab qilinadi.'
 }
 $record.FailedAttempts=0;$record.LockUntil=0;Save-EmergencyRecord $recordPath $record
 if($ChangePassword){
  $next=Ask-Secret 'Yangi favqulodda parol' 'Kamida 12 belgili yangi parol kiriting.'
  if($null -eq $next){exit 0}
  if($next.Length -lt 12){throw 'Yangi parol kamida 12 belgi boʻlsin.'}
  $repeat=Ask-Secret 'Parolni tasdiqlash' 'Yangi parolni qayta kiriting.'
  if($null -eq $repeat){exit 0}
  if($next -cne $repeat){throw 'Parollar mos emas. Eski parol saqlangan.'}
  Save-EmergencyRecord $recordPath (New-EmergencyRecord $next);$next=$null;$repeat=$null
  [Windows.MessageBox]::Show('Favqulodda parol yangilandi. Uni xavfsiz saqlang.','Mirae')|Out-Null
  exit 0
 }
 $answer=[Windows.MessageBox]::Show('ClassroomGuard oʻzgartirgan cheklovlar tiklanadi, xizmat va oʻrnatilgan fayllar olib tashlanadi. Natijalar va tiklash zaxiralari saqlanadi. Davom etasizmi?','Olib tashlashni tasdiqlang','YesNo','Warning','No')
 if($answer -ne 'Yes'){exit 0}
 & (Join-Path $PSScriptRoot 'uninstall.ps1') -Emergency
 [Windows.MessageBox]::Show('Olib tashlash tugadi. ProgramData ichidagi natijalar va zaxiralar, shuningdek foydalanuvchilarning lokal oʻquv jurnallari saqlangan.','Mirae')|Out-Null
} catch {
 [Windows.MessageBox]::Show($_.Exception.Message,'Tiklash yakunlanmadi','OK','Error')|Out-Null
 exit 1
} finally {if($owns){$mutex.ReleaseMutex()};$mutex.Dispose()}

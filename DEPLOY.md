# O‘rnatish — ClassroomGuard repaired build

## 1. Windows’da tayyorlash

Talablar: Windows 10/11 x64, build qiluvchi kompyuterda .NET 8 SDK, administrator hisob qaydnomasi. O‘quvchilar standart (administrator bo‘lmagan) Windows hisobida ishlashi kerak.

Loyiha papkasida PowerShell oching:

```powershell
.\scripts\build.ps1
```

Bu barcha loyihalarni tekshiradi, regression testlarini bajaradi va `artifacts\Agent`, `artifacts\StudentShell`, `artifacts\Admin`, `artifacts\Ctl` papkalarini tayyorlaydi. To‘liq papkalarni ko‘chiring; faqat EXE faylini ajratib olmang. Tayyor natija self-contained, o‘quvchi kompyuterida SDK talab qilinmaydi.

## 2. O‘qituvchi kompyuteri

1. `artifacts\Admin\ClassroomGuard.Admin.exe` ni ishga tushiring.
2. Sozlamalar → **Yangi kalit yaratish** → `classroom.key` faylini xavfsiz joyga saqlang. Faylni o‘quvchilar o‘qiy olmaydigan joyda saqlang.
3. Administrator PowerShell’da UDP discovery qoidasi:

```powershell
.\scripts\install-admin-firewall.ps1
```

Admin ilovasini o‘quvchilar foydalanadigan umumiy Windows hisobida ishga tushirmang. Shu hisobdagi boshqa dasturlar ham uning konfiguratsiyasini o‘qishi mumkin.

## 3. Har bir o‘quvchi kompyuteri

Avval standart lokal Windows hisobi yarating (masalan `student`). Tayyor `artifacts` va `scripts` papkalarini, hamda sinfning bir xil `classroom.key` faylini administrator nazorati ostida olib o‘ting.

Administrator PowerShell’da:

```powershell
.\scripts\install.ps1 -ClassroomKeyFile C:\AdminOnly\classroom.key -StudentUsers student
```

Bir nechta standart hisob:

```powershell
.\scripts\install.ps1 -ClassroomKeyFile C:\AdminOnly\classroom.key -StudentUsers student1,student2
```

Avvalgi ochiq kalitli versiya ishlatilgan bo‘lsa, yangi kalit yarating va yangilash paytida ataylab `-RotateKey` qo‘shing. Barcha kompyuter va o‘qituvchi konsolida bir xil yangi sinf kaliti bo‘lishi kerak. Eski nusxalarni ham xavfsiz olib tashlang.

Installer tayyor EXE fayllarni ishlatadi; o‘quvchi kompyuterida qayta build qilmaydi. Tarmoq Private yoki Domain profilida bo‘lishi kerak. Public profil uchun avtomatik ruxsat ochilmaydi.

## 4. Birinchi parol va qurilma sertifikati

Administrator `%ProgramData%\ClassroomGuard\bootstrap.txt` faylidan dastlabki parolni oladi. Teacher login sahifasiga shu parolni kiriting, keyin **Parolni o‘zgartirish / birinchi sozlash** tugmasi orqali kamida 12 belgili yangi parol o‘rnating. Yoki:

```powershell
& "$env:ProgramFiles\ClassroomGuard\Ctl\cgctl.exe" passwd teacher
```

Parol terminalga yashirin kiritiladi, buyruq qatoriga yozilmaydi.

Har bir Agent yaratgan `%ProgramData%\ClassroomGuard\device-enrollment.json` faylini ishonchli yo‘l bilan o‘qituvchi kompyuteriga ko‘chiring. Admin → Sozlamalar → **Device sertifikatini yuklash…**. Qurilma ID va fingerprintni tekshirib tasdiqlang. Bu fayl maxfiy sinf kalitini o‘z ichiga olmaydi.

UDP’da topilgan qurilmalar avtomatik ishonchli hisoblanmaydi. Sertifikat import qilinmaguncha boshqaruv so‘rovlari yuborilmaydi. Agent sertifikati almashtirilsa, yangisini qayta tekshirib enroll qilish kerak.

## 5. Siyosat yuborish

- Saytlar: faqat aniq domenlar. `example.com` va `www.example.com` kerak bo‘lsa, ikkalasini alohida yozing. `*.` yoki to‘liq URL kiritmang.
- Downloads: Chrome/Edge siyosati. Boshqa usullarni to‘liq bloklamaydi.
- App filter: boshlanishda o‘chiq. Avval kerakli o‘quv dasturlarini tekshiring. EXE nomi faqat himoyalangan Windows/Program Files joylashuviga mos keladi; foydalanuvchi papkasidagi boshqa dastur nomini o‘zgartirib ruxsat ola olmaydi. Fayl/papka ACL’larini administrator tekshirishi kerak.
- Kutubxona: online uchun HTTPS; offline uchun o‘quvchi kompyuterida mavjud to‘liq PDF yo‘li. Push faylning o‘zini ko‘chirmaydi.
- Testlar: JSON massiv. Har savolda 2–8 xil variant, to‘g‘ri 0-based indeks; vaqt 0–180 daqiqa. Chegara: jami 1000 savol va taxminan 512 KiB siyosat. Bu versiya katta markaziy testbank emas.
- Push tasdiqlangandan so‘ng bazaviy siyosat saqlanadi. Offline qurilma qaytganda avtomatik tekshiriladi. Yangi siyosat olish o‘quv rejimini yoqadi.

Oddiy teacher unlock: 30 daqiqagacha, seans tugashi/almashishi yoki kompyuter qayta ishga tushishi bilan yakunlanadi. Admin’dagi unlock ham doimiy o‘chiq siyosat yaratmaydi.

## 6. Telegram

Natijalar avval Agentda saqlanadi. Har bir o‘quvchi PC’da administrator `agent.json` dagi `TelegramBotToken` va `TelegramChatId` ni sozlaydi, keyin:

```powershell
Restart-Service ClassroomGuardAgent
```

Admin sozlamasidagi Telegram sinovi faqat Admin ulanishini tekshiradi; Agentlarga token tarqatmaydi. Telegram ishlamasa, saqlangan natijalar navbatda qoladi. Har bir xabardagi natija ID takroriy yetkazishni aniqlashga yordam beradi.

## 7. Holat va olib tashlash

```powershell
& "$env:ProgramFiles\ClassroomGuard\Ctl\cgctl.exe" status
.\scripts\uninstall.ps1
```

Olib tashlash administrator huquqini talab qiladi. Odatiy rejim natijalar/configni saqlab qoladi. To‘liq ma’lumotni ham o‘chirish kerak bo‘lsa:

```powershell
.\scripts\uninstall.ps1 -RemoveData
```

Xizmat ishlamasa, administrator avval sababni tekshirib:

```powershell
.\scripts\uninstall.ps1 -Emergency
```

Tiklashda xato bo‘lsa, skript fayllar va backupni o‘chirmaydi. Faqat ClassroomGuard egalik qilgan browser qiymatlari va hosts bloki qaytariladi; avvaldan mavjud siyosatlar ko‘r-ko‘rona o‘chirilmaydi. Eski SRP holatini tiklash uchun original siyosat/VM snapshot talab qilinishi mumkin.

Teacher PC discovery firewall qoidasini ilovani olib tashlaganda administrator alohida o‘chirishi mumkin:

```powershell
Remove-NetFirewallRule -Name ClassroomGuard-AdminDiscovery
```

Avval bitta Windows VM, keyin bitta haqiqiy student PC’da `docs\WINDOWS_ACCEPTANCE.md` bo‘yicha sinov qiling.

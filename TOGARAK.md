# Mirae To‘garak — yangi versiya

Bu paket to‘liq Windows dasturining manba kodi, yangilangan o‘quv sahifasi, ustoz uchun test paketlari va o‘rnatish/tiklash skriptlarini o‘z ichiga oladi. Tayyor o‘rnatilgan EXE yoki MSI emas. Oldingi Agent, Admin, Ctl va service boshqaradigan Testlar tizimi saqlangan.

## Tez ko‘rish

`Mirae-Togarak.html` ni Edge yoki Chrome brauzerida oching. Internet darslarni o‘qish uchun kerak emas. Windows xizmatini o‘rnatish shart emas. To‘liq sahifa StudentShell ichida ham ishlaydi.

- 7 modul, berilgan ketma-ketlikdagi 70 mavzu.
- Har mavzuda tushuntirish, misol, amaliy vazifa, qabul mezoni, shaxsiy qayd va 2 savolli tekshiruv.
- Jami 140 noyob IT savoli. Mavzu testlari va modul imtihonlari shu bankdan foydalanadi.
- 7 modul imtihoni: mos ravishda 20, 20, 10, 10, 20, 30, 30 savol.
- Yakuniy nazariy sinov: har moduldan 5 tadan, jami 35 savol, 50 daqiqa.
- Qidirish, javoblar tahlili, qayta o‘rganishga havola, JSON zaxira/tiklash va chop etish.
- Amaliy ish belgisi va 2/2 mavzu testi birga «o‘zlashtirildi» holatini beradi. Belgi o‘quvchining o‘z bahosi; ustoz tasdig‘i emas.
- Word/Excel darslari boshlang‘ich umumiy kirish darajasida. Guruh ehtiyojiga qarab ularni bir nechta mashg‘ulotga bo‘ling.
- Darslar o‘zbek tilida. Mavjud desktop UZ/RU/EN interfeysi saqlangan; yangi darslar tarjima qilingan deb ko‘rsatilmaydi.

## Windows dasturiga o‘rnatish

1. To‘liq paketni alohida papkaga oching. Mavjud qurilma sozlamalari va natijalarini administrator sifatida zaxiralang.
2. Windows build kompyuterida .NET 8 SDK va internet orqali NuGet tiklash imkoniyati kerak.
3. PowerShell orqali `scripts\build.ps1` ni bajaring. U loyihalarni build qiladi, regression testlarni ishga tushiradi va `artifacts` ichida self-contained Windows papkalarini chiqaradi.
4. Student kompyuterga **machine-wide Microsoft Edge WebView2 Runtime** o‘rnating. Faqat foydalanuvchi profiliga o‘rnatilgan runtime cheklangan rejimda qo‘llab-quvvatlanmaydi. Runtime Microsoft rasmiy distributividan olinadi.
5. `DEPLOY.md` dagi classroom key va oddiy student hisoblari talablari bilan yangilangan `scripts\install.ps1` ni Administrator sifatida ishga tushiring. Faqat eski EXE ustiga nusxalash yetarli emas: installer `Recovery` skriptlari va favqulodda parol yozuvini ham joylaydi.
6. StudentShell → **To‘garak · 70 mavzu**. Boshlang‘ich oynada Agent ishlamasa ham offline darsga kirish bor; bu tugma cheklovlarni o‘zgartirmaydi.
7. Maktabning ijro siyosati PowerShell skriptlarini bloklasa, administrator tashkilot qoidalariga mos ravishda imzolashi yoki ruxsat berishi kerak. Tiklash tugmasi ExecutionPolicy ni o‘zgartirmaydi.

WebView2 lokal HTTPS virtual host bilan Learning papkasini ochadi. Tashqi sahifaga navigatsiya, yangi oynalar va native host bridge o‘chirilgan. Darsdagi kod avtomatik bajarilmaydi. Mavjud reaktiv process filtriga faqat Program Files ichidagi Microsoft EdgeWebView Application papkasining `msedgewebview2.exe` runtime yo‘li qo‘shilgan; bu oddiy browser yoki ixtiyoriy shu nomli student fayliga ruxsat emas.

## Favqulodda olib tashlash

**Boshlang‘ich parol: `123mirae321`**

- Tugma boshlang‘ich sahifa va Sozlamalarda mavjud. UI ochilmasa, Administrator PowerShell oynasida `& "$env:ProgramFiles\ClassroomGuard\Recovery\emergency-uninstall.ps1"` ni bajaring. Parol o‘zgartirish uchun shu buyruqqa `-ChangePassword` qo‘shing.
- Avval Windows UAC / administrator hisob tasdig‘i, keyin favqulodda parol so‘raladi. Parol Windows administrator huquqini almashtirmaydi.
- Parol tasdiqlangach ham alohida olib tashlash tasdig‘i bor. Bekor qilish tizim cheklovlarini o‘zgartirmaydi.
- 5 xato urinishdan keyin 5 daqiqalik kutish yozuvi saqlanadi. Parol PBKDF2-SHA256, tasodifiy salt va 210,000 iteratsiya bilan saqlanadi. Faqat SYSTEM/Admin ruxsatli ProgramData papkasida turadi.
- Birinchi foydalanishdan oldin **Sozlamalar → Favqulodda parolni o‘zgartirish** orqali yangi 12+ belgili parol belgilang.
- Yangilash mavjud parolni boshlang‘ich parolga qaytarmaydi. Parol yo‘qolsa haqiqiy Windows administrator uchun eski hujjatlashtirilgan recovery yo‘li mavjud.
- Agent xizmatiga ulanish shart emas: skript xizmatni to‘xtatadi, faqat ilovaga tegishli zaxiralardan cheklovlarni tiklaydi va tekshiradi. So‘ng xizmat, task, Agent firewall qoidasi va o‘rnatilgan fayllarni o‘chiradi.
- Tiklashda xato bo‘lsa olib tashlash to‘xtaydi. Xizmat to‘xtagan bo‘lishi mumkin; ko‘rsatilgan xatoni administrator tekshirishi kerak. Zaxira fayllarini o‘chirmang.
- Favqulodda UI natijalar va maxfiy tiklash zaxiralarini **saqlaydi**. Windows profillaridagi lokal o‘quv jurnali ham avtomatik o‘chirilmaydi. Oddiy HTML nusxa Windows ilovasini uninstall qila olmaydi.
- Administrator darajasida bu ikkinchi qulay tekshiruv; administrator o‘z tizimini boshqarish huquqidan mahrum qilinmaydi. Parol faqat matn faylidagi sirga ishonadigan «buzilmas himoya» deb taqdim etilmaydi.

## Ustoz uchun imtihonlar

`teacher\togarak-exams.json` — Admin dasturining mavjud Test JSON yuklash funksiyasiga mos 8 testdan iborat ro‘yxat. ClassName: `IT To‘garak`. Kerak bo‘lsa guruh nomini nusxada o‘zgartiring. Importni avval sinov kompyuteriga qo‘llang. Mavjud testlar bilan birlashtirishdan oldin Admin JSON ro‘yxatini zaxiralang: joriy ro‘yxatga ehtiyotkorlik bilan qo‘shing, ustiga bexosdan yozmang.

Bu paketlardagi savollar o‘quv sahifasida ham bor. Formal sinovda yodlangan savollarga tayanmaslik uchun ustoz variantlarni va qo‘shimcha amaliy ishni ko‘rib chiqishi kerak. Nazoratli service testlarida mavjud Agent javob saqlash, deadline va baholash yo‘li ishlatiladi; lokal o‘quv sahifasining yozuvlari rasmiy baho o‘rnini bosmaydi.

Tavsiya etilgan amaliy loyiha rubrikasi: talab 30%, ishlash 30%, test dalili 20%, UI 10%, tushuntirish/README 10%. Nazariy sinov va amaliy loyiha bahosini avtomatik aralashtirish yo‘q.

## Saqlash va cheklovlar

O‘quv jurnali shu Windows yoki brauzer profilida lokal. Har talabaga alohida Windows hisobi ishlating. Ism maydoni login emas. Qurilmalar yoki HTML nusxa bilan WebView2 orasida avtomatik sinxronlash yo‘q. JSON eksport/tiklash mavjud. Tiklash joriy jurnalni almashtiradi va avval tasdiq so‘raydi. Oxirgi 100 urinish saqlanadi; qaydlar mavzu bo‘yicha saqlanadi.

Darslar va barcha savollar qurilmaning o‘zida mavjud. Developer tools, fayl tahriri yoki OS soati o‘zgarishiga qarshi server kafolati yo‘q. Oddiy refresh sinov deadline yoki variant tartibini qayta boshlamaydi. Saqlash bloklansa foydalanuvchiga xabar beriladi. To‘garak va matematika sinovlarini bir vaqtda boshlash cheklangan.

Matematika bo‘limida oldingi 120 savol va darhol izoh berish usuli saqlangan. 34-savolning javobi 0 ga to‘g‘rilandi, aynan takrorlangan promptlar bitta urinishda qaytarilmaydi, guruh nomi HTML sifatida bajarilmaydi, tugagan test qayta topshirilmaydi. Ikki avtomatik Google POST o‘rniga bitta **Natijani markazga yuborish** tugmasi bor. Shaxsiy ma’lumot uzatishdan oldin tasdiq so‘raladi; noaniq yetkazishni muvaffaqiyat deb ko‘rsatmaydi. Tashqi Google endpointning haqiqiy qabul qilish natijasi bu ishda tekshirilmagan.

## Kontentni tahrirlash

`tools\learning` ichida o‘quv matnlari, CSS, JS, original math sahifasi va Python generatori bor. `python tools/learning/build.py` HTML fayllarni qayta yaratadi. `teacher/togarak-exams.json` alohida ustoz nusxasi: savollar yangilansa uni ham tekshiring va kerakli versiya ID sini yangilang. Lesson ID larini saqlang; jurnal shu ID larga bog‘langan. 70 ta mavzuning matni `curriculum.txt` da, har qator 14 maydon, ajratgich `§`.

## Tekshiruv holati

- 5 production loyiha .NET 8 Release cross-build: muvaffaqiyatli.
- Mavjud 35 regression tekshiruv: barchasi o‘tdi.
- Brauzerda 103 kontent/funksional tekshiruv: barchasi o‘tdi; JavaScript xatosi yoki avtomatik tashqi so‘rov yo‘q.
- 1440px desktop va 390px mobil sahifalar vizual tekshirildi; gorizontal chiqib ketish kuzatilmadi.
- Barcha 7 PowerShell skripti parser tekshiruvidan o‘tdi (PowerShell 7.4 parser). Bu Windows PowerShell 5.1 runtime testi emas.
- 120 matematika savoli bir nechta qiymatda raqamli izchillik tekshiruvidan o‘tdi; bu formal algebraik isbot emas.
- Haqiqiy Windows service, UAC, registry tiklash, WPF/WebView2 runtime, installer va uninstall bu Linux muhitida bajarilmadi. `docs/TOGARAK_WINDOWS_CHECKLIST.md` ni sinov Windows qurilmasida yakunlang.

## Rasmiy qo‘llanmalar

- https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands
- https://learn.microsoft.com/en-us/powershell/scripting/overview
- https://support.microsoft.com/word
- https://support.microsoft.com/excel
- https://flowgorithm.org/documentation/index.html
- https://docs.python.org/3/tutorial/
- https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content
- https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution

Hosting shartlari va dastur menyulari versiyaga qarab o‘zgaradi; deploy paytida rasmiy manbadan tekshiring.

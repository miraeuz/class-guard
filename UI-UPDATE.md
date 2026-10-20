# Mirae Class Guard — interface update

## What changed

- Shared purple/lavender palette, teal accents, clearer contrast and keyboard-focus outlines in student and teacher applications.
- Illustrated welcome, teacher login, install approval and unlocked pages. The three supplied coding illustrations are embedded resources; no image download is needed at runtime.
- Consistent Windows vector-font icons for student navigation, including To‘garak.
- Dashboard cards reflow from four to two columns, role cards stack in narrow windows, and the sidebar scrolls on shorter screens. Native WPF uses device-independent units and Windows DPI scaling, not CSS rem.
- Teacher dashboard actions wrap below the heading instead of competing for the same horizontal space. Teacher push/settings pages inherit the updated shared controls and palette.
- Learning home, modules, lessons, tests, results and guide share a responsive purple theme. The standalone learning page contains its illustration as an embedded data URI.
- “Kompyuterga o‘tish” minimizes the shell without sending an unlock command or modifying service policy. The header button is available on every shell page; a labeled button is also in the student sidebar. Restore using the taskbar or double-click the tray icon.
- Login Caps Lock feedback, manual connection check, copy-PC-name action, and a session-only reduced-animation option in settings.

## Existing behavior retained

Agent enforcement, core policy/authentication/attempt code, CLI and installation/recovery scripts were not changed. An active test's deadline continues while the dashboard is minimized. Hiding the shell is not an unlock. The existing emergency recovery path and password behavior remain unchanged.

## Build and use on Windows

This is the complete source project, not a ready-made installer. Use the .NET 8 SDK on Windows. From PowerShell inside this folder:

```powershell
.\scripts\build.ps1
```

This builds, runs the regression suite and publishes deployment folders. Follow `DEPLOY.md` for installation and protected service configuration. `Mirae-Togarak.html` can also be opened directly to preview/use the offline learning section.

## Verification

- All five solution projects cross-built successfully with .NET 8.
- Existing regression executable: 35 checks passed.
- Learning browser suite: 103 checks passed, zero JavaScript errors and zero external requests during testing.
- Learning layout checked at 1440, 800 and 390 CSS pixels without horizontal document overflow.
- Native XAML parsed; enforcement/core/CLI and recovery scripts compared unchanged with the previous source.
- Native WPF visual behavior and Windows service behavior cannot be executed on this Linux host. Earlier validation documents describe the prior release; current logs are `docs/UI_*`.

## Windows acceptance checks before classroom rollout

1. On one standard student PC, start strict mode. Use “Kompyuterga o‘tish”; confirm the shell minimizes and blocked applications/sites remain blocked. Restore from taskbar and tray.
2. Start a timed test, minimize and restore; confirm the same attempt resumes and the deadline keeps advancing.
3. Check 1366×768 and 1920×1080 at 100%, 125% and 150% Windows scale. Check all navigation sections, short windows, focus rings and keyboard-only login.
4. Check Caps Lock on/off, wrong-password feedback and the existing teacher-unlock flow. Confirm teacher access still expires normally.
5. Check offline/reconnected Agent status, library/site/app launch, lesson progress persistence, test scoring and result delivery.
6. Check install approval, teacher push/settings and emergency recovery on a spare managed PC using the existing documented procedure.

Supplied design mockups were used as visual references; their sample user names, fake metrics and branding were not inserted into the live dashboard. Supplied coding artwork was embedded unchanged.

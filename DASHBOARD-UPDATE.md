# Mirae classroom dashboard — responsive layout and day/night themes

## What changed

- Flexible PC cards with clearer status text, rounded surfaces and restrained purple gradients. The details panel is on the right at wide sizes and below the cards on narrower windows. The dashboard scrolls vertically on short screens. A seating map retains its chosen column count and scrolls horizontally if necessary, rather than shrinking labels below readable size.
- Day/night buttons in the teacher dashboard and student header. The choice is saved separately for each app and Windows user. Native pages, inputs, the rich question renderer and embedded learning pages follow the theme. The attention lock screen keeps its dedicated dark purple treatment.
- A **selection checkbox** on each PC. Search, select visible PCs, clear selection, and lock/unlock or change app restrictions for the selected set. Selection is tied to device identity AND endpoint; a different IP/identity does not silently inherit a selection. Searching does not clear previously selected PCs. The selected count includes hidden selections.
- All-PC and selected-PC actions are labeled separately. Confirmation shows the eligible target count and skipped count. Offline, unverified, unenrolled and identity-conflicting PCs are excluded. They are not queued for these actions. Existing policy synchronization still retains its previous offline reconciliation behavior.
- **O‘tirish rejasi**: assign seat numbers 1–120 through PC details and choose 2–6 columns. Assignments persist in the teacher's local profile. Duplicate seats are rejected. Unassigned PCs appear after the seating slots. A reserved slot can be cleared even when its PC is absent. This changes only layout, never enrollment or enforcement.
- PC details show endpoint, device ID, last contact, transport/command failure text, failed enforcement controls and suggested next steps. Successful later contact clears the current transport error; this panel is not a historical audit log.
- Existing individual and whole-class application restriction commands remain available under **Ilova cheklovlari**. They remain separate from the lesson screen lock.

## Screen confirmation: what the labels mean

**Qulf saqlangan · ekran tasdig‘i yo‘q** means the service retains the lock, but no sufficiently fresh display report is available. It must not be interpreted as a covered screen.

**Ekran yopilgan · N monitor** means the installed StudentShell recently reported visible, positioned cover windows spanning its detected monitor bounds. The Agent only accepts the heartbeat from the installed Program Files StudentShell path, an enrolled student or administrator account, and the active console session. Each explicit lock/unlock generates a new token so a report from an earlier lock cannot confirm a later one. The report expires after eight seconds at the Agent. The dashboard also ages the returned report locally, avoiding a dependency on matching teacher/student clock offsets. Student polling is every two seconds and teacher polling every four seconds; this is not instantaneous.

This is **application-reported confirmation, not Windows secure-desktop attestation**. It does not prove that no other desktop, secure UI or higher-privilege window is visible. The earlier overlay limitations in SCREEN-LOCK.md still apply. If StudentShell is killed or crashes, the durable lock survives but the cover disappears until StudentShell restarts; confirmation expires instead of remaining green.

## Install / upgrade

This ZIP contains full source, not a signed installer. Follow the existing build and installation instructions. Upgrade Admin, Agent, StudentShell and Ctl together. Existing ProgramData, classroom keys, enrollments and policy files must be retained. The installer and emergency recovery scripts have not been changed in this update.

For display confirmation, StudentShell must run from the normal installed path:
`C:\Program Files\ClassroomGuard\StudentShell\ClassroomGuard.StudentShell.exe`
(or the corresponding Windows Program Files location). Development copies running elsewhere deliberately cannot submit trusted display reports. Old agents/shells still lack this new confirmation capability.

Theme files and `dashboard-layout.json` are under `%LocalAppData%\ClassroomGuard`. They contain UI preferences, not passwords or classroom keys. Native sizing uses WPF device-independent units, Grid/WrapPanel and scrolling; the embedded web content uses responsive CSS.

## Validation completed

- Full Release build: Core, Agent, Ctl, Admin and StudentShell.
- 85 regression checks: existing persistence, scoring, network/security tests plus expiring/replayed/session-specific display reports and selected-target eligibility.
- 19 rich-question browser checks: HTML sanitization, code, LaTeX, responsive sizes and day/night rendering.
- 106 learning browser checks: all 70 lessons, practice and exam flows, original mathematics behavior, responsive pages and night/day restoration.
- Main text, secondary text and muted text were checked against main card/app backgrounds for at least 4.5:1 contrast in both native palettes. This is a scoped contrast check, not a complete accessibility certification.
- Existing files retained; install, signing and recovery scripts unchanged.

## Windows checks before classroom rollout

The WPF applications cannot be run in this Linux environment. Compile success and browser checks do not validate native interaction. On two spare Windows PCs, verify:

1. Dashboard at 800×600, 1280×720 and 1920×1080, with Windows display scaling at 100%, 125% and 150%. Check wrapped actions, scrolling, sidebar toggle, keyboard navigation and details placement.
2. Toggle day/night on both apps, restart them, and check login, settings, test options, rich code/LaTeX and a learning lesson opened via its internal navigation.
3. Assign seats, reject a duplicate seat, change columns, restart Admin and recover a reserved seat whose PC is absent.
4. Select one PC, then filter it out. Confirm the count still includes it; verify only the confirmed selected targets receive the command. Test offline, duplicate-ID and missing-enrollment cases.
5. Lock a signed-in student PC and watch the saved → reported-covered transition. Check multiple monitors and mixed DPI. Close/terminate StudentShell and verify confirmation expires. Relaunch it and verify the saved lock returns.
6. Reboot a locked PC, change console sessions, and disconnect the network. No stale confirmation should remain indefinitely. Verify teacher/local administrator recovery before wider rollout.
7. Run a normal test and existing restriction/unlock workflow to check classroom-specific integration. Screen locking still does not pause a test deadline.

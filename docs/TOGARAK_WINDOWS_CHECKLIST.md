# Windows acceptance — To‘garak update

Run on a disposable/test Windows machine with backups before student deployment. These checks have not been executed in this Linux build environment.

1. Run build.ps1 on Windows with .NET 8 SDK; verify Learning/index.html and WebView2 assemblies/native loader are in published StudentShell folder.
2. Install a machine-wide WebView2 Runtime. Install/update using current install.ps1; retain classroom key and device identity. Check service health and old teacher login/tests.
3. Open To‘garak as a standard student while app allowlisting is on. Confirm the genuine machine-wide WebView2 runtime remains open. A renamed executable in a student folder must not receive that exception.
4. Disconnect internet. Open all modules, lesson example, task checkbox and notes. Reload/restart StudentShell; verify state persists under that Windows account. A second Windows account must have its own journal.
5. Verify an 8-minute practice and a module test: answer, close/reopen, deadline unchanged, expiry submits once, answers not shown before submission, history/export/import/print work. Export may be constrained by the school's download policy; do not weaken policy silently.
6. Import teacher/togarak-exams.json into a saved copy of the Admin test list. Push to a test PC. Complete one service-controlled exam and verify deadline, result and score.
7. Runtime missing: To‘garak shows an actionable message; the rest of StudentShell remains usable. Verify retry after installation.
8. Emergency button: cancel UAC; nothing removed. Wrong password: nothing removed. Fifth wrong password: five-minute persisted wait. Retry after interval. Cancel the final removal confirmation; no restriction changed.
9. Change emergency password: wrong old password fails, mismatched new confirmation fails, correct 12+ character replacement works, old password no longer works. Upgrade retains replacement hash.
10. Recovery with Agent healthy: authenticate and confirm; owned hosts/browser settings return to prior state, unrelated school policies survive, service/task/Agent firewall rule/Program Files removed. ProgramData and per-user notes remain.
11. Repeat on a snapshot with Agent unavailable. The emergency path must work independently of Agent IPC. Test missing recovery backup: removal must halt and retain recovery data. Record the stopped service state and displayed error.
12. Review .ps1 execution policy and signed-script requirements under school policy. Do not deploy an execution-policy bypass or lower Windows account privileges as a workaround.
13. Check existing teacher password changes, TLS enrollment/pinning, student-mode policy reconciliation and install approval flows using the original WINDOWS_ACCEPTANCE.md.

Record Windows version, runtime version, account type, date, pass/fail and screenshots/logs for any failure. Do not mark this checklist passed solely because compilation succeeded.

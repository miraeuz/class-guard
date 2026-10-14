# Windows pilot checklist

Execute first in a disposable Windows VM with a snapshot, then on one real lab computer before wider installation. These are pending checks, not claimed test results.

- Clean Windows .NET 8 build; run regression test executable; publish all four application folders.
- Install with one standard student account. Confirm no student administrator membership.
- Inspect Program Files and ProgramData ACLs from the actual student account. Key, credentials, bot token, results and certificate private key must be unreadable; binaries must be unwritable.
- StudentShell connects to the LocalSystem-owned pipe without elevation. Test a spoofed owner and a remote pipe connection: reject both.
- Bootstrap password works for changing the password only; wrong/current-password and minimum-length checks work. Bootstrap text disappears.
- Enroll certificate from a trusted copy. Verify management rejects unenrolled devices, wrong fingerprints and wrong classroom keys.
- Unlock, log off, switch users, reboot and wait for timeout. Restricted baseline must resume according to lease rules. A new policy push must activate student mode.
- Kill/restart the Agent in the VM and interrupt WMI. Observe recovery and truthful partial/offline health. Confirm no claim of continuous process filtering during the outage.
- Test Word, Excel, Edge/Chrome, Python/IDE tools used at Mirae, printer helpers, antivirus, accessibility tools and Windows updates. No SYSTEM or teacher processes should be killed.
- Apply application filtering with a program already running. Test a renamed executable in a student-writable location. Test exact-file installer approval with filtering enabled; test secondary installer components separately.
- Record existing browser policies, apply/release repeatedly, switch download blocking off/on, and uninstall. Prior values must be restored exactly; externally changed values must not be blindly deleted. Test shared 32/64 registry views.
- Verify hosts edits preserve other mappings and file access rights. Test files with/without final newline, original encodings and external edits. Domains are exact, not wildcard.
- Corrupt primary policy only: known-good recovery. Corrupt both: limited default and logged error, not a claim of the former full policy.
- Restart Admin and save settings after a push. Intent must survive. Offline PC missing a new policy must reconcile by PolicyId when it returns.
- Try malformed/null/oversized policies, invalid answer indexes and duplicate options: reject before replacing valid policy.
- Start test, answer, navigate away/back, restart shell and expire deadline. Saved answers resume, late answers cannot change the score, finish is idempotent and another Windows SID cannot submit the attempt.
- Disable internet/Telegram. Results stay saved; notification status remains queued. Restore connectivity and verify result IDs and possible at-least-once delivery duplicates.
- Duplicate shell launch must not throw a mutex-ownership exception.
- Verify keyboard-only operation, Narrator labels, readable focus and text, and 100/125/150/200% DPI on 1024×768, 1366×768 and 1920×1080. Check Uzbek/Russian/English static pages. Service diagnostics have a documented translation limit.
- Start a bulk command with one unresponsive device and one untrusted device. Other devices must finish; timeouts/failures must remain visible.
- Test normal uninstall, unavailable service, interrupted cleanup, modified external policies and legacy SRP marker. Failed recovery must preserve files/backups.
- Confirm removal of service, scheduled task, Agent firewall rule and installed binaries. Remove teacher discovery rule separately if retiring the Admin console. Confirm whether data was intentionally retained or removed.

Keep the VM snapshot and exported original school policy until the pilot passes. Existing legacy SRP settings cannot be reconstructed safely without the original configuration.

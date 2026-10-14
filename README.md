# To‘garak update — 14 October 2026

Start with **[TOGARAK.md](TOGARAK.md)** for the new 70-topic learning section, teacher exam imports and emergency removal. The emergency default is `123mirae321`; Windows administrator approval is also required. This is source, not a ready-made installer.

# ClassroomGuard — repaired full source project

This package replaces the reviewed prototype with a buildable .NET 8 Windows solution, security and reliability repairs, service-owned test attempts, an updated student UI, and deployment/recovery scripts.

**Start with [DEPLOY.md](DEPLOY.md).** The ZIP is the complete source project. Run `scripts\build.ps1` on Windows with the .NET 8 SDK to create self-contained Windows deployment folders. Source ZIPs do not themselves install the application.

## What changed

- Teacher password changes require the current password, including first-time enrollment. Bootstrap text is removed after a successful change.
- Student IPC uses a bounded protocol, explicit Windows permissions and a SYSTEM-owned-pipe check. Privileged local recovery requires an elevated Windows account.
- Agent secrets are restricted to SYSTEM/Administrators. Privileged binaries install under protected Program Files paths.
- Teacher Admin verifies an explicitly enrolled certificate for every device before sending authentication or commands. UDP announcements are untrusted discovery hints.
- Teacher access is temporary: up to 30 minutes, and reset on restart or the tracked session ending/changing. It is not persisted as the baseline.
- Baseline policies have authoritative IDs, semantic validation, durable Admin storage, last-known-good recovery, and automatic reconnect reconciliation.
- Control failures are returned in health status; the service supervises enforcement and is configured for restart after failure.
- Browser-policy changes retain durable prior values and restore only owned values. Removal stops on recovery failures instead of deleting recovery data.
- Process filtering is scoped to configured standard student accounts. Name rules require a protected installation location; substring matching is removed. System/teacher processes are not indiscriminately killed.
- Installer hash approvals are shared with the application filter. Termination happens before hashing a previously unapproved file. Approval is for the exact file hash, not arbitrary installer children.
- Test answer keys remain in the protected Agent. Attempts, answers, deadlines, scores and notification state are stored by the service and bound to the Windows account SID.
- Static student pages support Uzbek, Russian and English with a saved language choice. Readability, selected navigation, keyboard-operable cards, scrollable forms and test resume behavior were improved.
- A regression executable covers policy validation, credentials, state recovery, assessment scoring/deadlines, synchronization, framing and real TLS certificate rejection.

## Deliberate limits

This version is **a managed classroom desktop, not a kiosk, application sandbox, antivirus, or tamper-proof exam platform**. Hosts filtering blocks exact configured hostnames, not every network route. WMI process filtering reacts after execution and cannot prevent every script, download, install or portable program. Windows tools remain available; those tools can perform actions without a separately blocked executable. Students must use standard Windows accounts, never local administrator accounts.

The previous registry-based SRP implementation is disabled. Setting `EnableSrpEnforcement=true` reports an error rather than changing SRP. Existing legacy ClassroomGuard SRP markers block installation/removal until an administrator restores a known-good original policy. Unknown historical settings are never guessed.

The UI can be hidden/terminated without removing the service. A dedicated kiosk experience is outside this release. The service can recover, but runtime WMI filtering is absent during a service outage. No claim of uninterrupted protection before service startup is made.

Attempts are associated with the Windows account, not an individually authenticated student identity. Shared Windows accounts share attempt ownership. Offline PDF metadata does not copy the PDF; deploy assets or a suitable read-only share separately. No PDF binaries are distributed with the sample data.

Telegram delivery uses a durable retry queue and includes a result ID. Telegram has no idempotency key here: a crash after acceptance but before recording delivery may produce a duplicate message. Service diagnostic messages may remain English or Uzbek even when static student navigation is translated; the teacher console uses Uzbek/English operational terminology.

## Verification

See `docs/VALIDATION.md` for the exact executed checks and limits, `docs/AUDIT_ACTIONS.md` for every original finding, and `docs/WINDOWS_ACCEPTANCE.md` for checks still needed on Windows.

Do not confuse a successful cross-build with tested Windows behavior. This package was not installed on a Windows desktop during this work.

# Multi-PC dashboard and rich tests — 20 October 2026

## Start here

This is the full source project. Build with `scripts\build.ps1` on Windows and deploy the resulting complete folders according to `DEPLOY.md`. Update the teacher Admin and student Agent/StudentShell together. Keep existing classroom keys, protected configuration, policy backups and attempts. Do not delete ProgramData to update. The signature workflow in SIGNING.md still applies to every newly built release.

## Why the dashboard could show one PC

The previous discovery dictionary used MachineId alone. Windows images cloned without unique MachineGuid values could announce the same ID, so each announcement replaced another PC's IP/name. This is a confirmed source-code overwrite defect and a plausible explanation of the reported alternating PC names, not a diagnosis of the physical LAN.

The new catalog tracks IP/command-port endpoints separately. Ten simultaneous endpoints remain ten entries even when their IDs match. The panel refreshes every three seconds independently of teacher credentials and network polling. Previously it did not refresh when the teacher key/sync service was unavailable. Authenticated polling and commands still require enrolled certificate pins.

Cards now show endpoint addresses, online/unverified/conflict states, search by name/IP/ID, and responsive card wrapping. Offline records remain visible for up to 24 hours while the teacher app stays open; the display is an endpoint list, so a PC using multiple interfaces or changing IP can temporarily appear twice. Discovery restarts empty and receives agents' announcements within their usual 15-second interval.

## When a card says ID ZIDDIYATI

This means the same MachineId is currently announcing from more than one endpoint. It can indicate cloned identities or a multi-interface PC. Confirm which physical computers the entries belong to before repairing anything. Conflicting endpoints are visible but management is disabled to avoid sending commands under an ambiguous identity.

For genuinely different PCs sharing an ID:

1. Deploy this update to those PCs first.
2. On one affected student PC at a time, run elevated PowerShell:

```powershell
.\scripts\repair-device-identity.ps1 -ConfirmRepair
```

3. The script stops the Agent briefly, assigns a ClassGuard-specific UUID, backs up and replaces its device certificate, restarts the Agent, and waits for the new public enrollment JSON. Policies, attempts and teacher passwords are retained. If initialization fails, it restores the previous identity/certificate and attempts to restart the service. This operation creates a short enforcement interruption; do it outside class.
4. Transfer ONLY `C:\ProgramData\ClassroomGuard\device-enrollment.json` to the teacher PC and import it through the existing Settings enrollment control. Never copy device.pfx, certificate backups or classroom.key to students or into chat.
5. Sign out/back in on that student PC to refresh the shell, then check its new card. Repeat for the remaining cloned PCs. Keep the original pin until no remaining PC uses that old identity.

Do not copy protected ClassGuard identity/certificate files between student PCs. The script changes only ClassGuard identity; it does not modify Windows MachineGuid or rename Windows accounts.

## If only one endpoint is discovered without an ID conflict

“Agent is on” in a student shell confirms its local pipe/service connection, not teacher-to-student LAN reachability. Check that all PCs are on a reachable LAN, Wi-Fi client isolation is disabled for that classroom network, and the existing documented discovery UDP/command TCP firewall rules match the configured ports. A VPN or another active adapter can affect broadcast routing. Preserve certificate enrollment checks. Do not disable the firewall globally. Discovery uses IPv4 broadcasts; routed subnets need suitable network configuration and are not magically bridged by this update.

## Student test experience

- Numbered question palette: current, answered and flagged states; click a number to navigate.
- Answered progress bar, clear-answer control, previous/next disabled at boundaries.
- Purple option cards and keyboard-focus states; session-only review flags persist while the app remains running but are not scored or stored by the Agent.
- Formatted HTML question text, syntax-colored code and offline KaTeX formulas.
- A plain-text/code/formula fallback if WebView2 cannot initialize.
- Optional finished-test answer/explanation review, disabled by default. Answers and explanations are not sent to the student during the attempt.
- Existing service-owned scoring, deadlines, save operations and Telegram delivery remain in use. Leaving/minimizing does not pause deadlines.

Both student and teacher sidebars now collapse to a 76-DIP icon rail, retaining menu tooltips; expanding restores labels. The compact student rail also keeps the Go to PC action. The setting is session-only.

## JSON import

Use `examples/tests-rich.json` in the existing teacher Tests JSON field. `examples/tests.schema.json` documents the format. It is an ARRAY of test packages, with the existing PascalCase field names. Existing plain questions need no edits. All questions remain single-answer multiple choice; this update does not silently reinterpret typed-answer or multi-select formats.

New optional question fields:

| Field | Purpose |
|---|---|
| TextHtml | Formatted question: paragraphs, bold/italic, lists, tables, pre/code, sub/sup, blockquote |
| Code | Literal source code with indentation; never executed |
| Language | Code label; basic highlighting for python/py, javascript/js and powershell |
| Latex | Standalone mathematical expression rendered locally by KaTeX |
| Topic / Difficulty | Small descriptive labels |
| Explanation | Plain explanation revealed only after completion when permitted |

`Text` remains required for accessibility/older-client fallback. Keep it semantically equivalent to TextHtml. Options remain plain strings. `CorrectIndex` is zero-based. `ShowReviewAfterFinish` belongs to the test package, defaults to false, and should stay false for exams where answers must not be disclosed after an individual finishes.

HTML is deliberately restricted: no scripts, event handlers, CSS, images, forms, links, frames or remote resources. Attributes are stripped. LaTeX trust is disabled, macro expansion/size are bounded, and unsupported expressions fall back to literal text. It supports KaTeX's mathematical subset, not complete TeX documents. Renderer messages can only request bounded layout height; they cannot submit answers or invoke system commands.

Limits remain 1–200 questions/test, 2–8 distinct options/question, 0–180 minutes, at most 1000 questions and 512 KiB serialized policy. Extra content consumes that same policy size budget.

## Verification and remaining checks

- All five .NET projects cross-built successfully.
- 60 regression checks passed, including all previous checks, ten real loopback UDP senders with colliding IDs, catalog collisions, independent same-name PCs, stale endpoints, ID replacement, rich-field cloning and answer/explanation secrecy.
- 16 browser checks passed for rich rendering, indentation, tables, offline LaTeX, malicious markup stripping, no external requests, fallback and 390/800/1200-pixel layouts.
- All ten PowerShell scripts parse successfully. Actual clone-repair service operations were not executed on Windows here.
- Existing learning content and installation/recovery/signing scripts were retained; the new identity repair script is opt-in.

Run a Windows acceptance test before broad rollout: ten real PCs simultaneously visible, one offline/reconnect, duplicate-ID repair/re-enrollment on a spare PC if required, strict-mode minimize/restore, both sidebar states, 100/125/150% display scale, rich and legacy tests, unanswered completion, deadline expiry, save failure/retry, optional review and Telegram results. Native WPF windows and your physical LAN cannot be exercised on this Linux host. This is not remote screen viewing/control like a complete Veyon deployment.

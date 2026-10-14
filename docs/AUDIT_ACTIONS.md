# Original audit — implementation record

Status vocabulary: **Repaired** means source changes are implemented; Windows integration tests may still be pending. **Redesigned/limited** means the unsafe behavior or misleading promise was removed rather than claiming an unimplemented Windows security boundary. See VALIDATION.md for executed evidence.

| Audit | Action in this package | Status / remaining validation |
|---|---|---|
| CG-01 secrets readable | Exact protected ProgramData ACL; key is provisioned by installer; no ephemeral fallback | Repaired; verify effective ACLs on Windows and rotate exposed old keys |
| CG-02 password reset | Current/bootstrap password required; minimum length; throttling; bootstrap deleted | Repaired; regression tests passed |
| CG-03 relay | Per-device persistent certificate, out-of-band enrollment and strict certificate fingerprint checking before HMAC | Repaired; real TLS mismatch rejection test passed; Windows enrollment pilot pending |
| CG-04 SRP wrong rule level | Unsafe SRP writer removed; attempts to enable it fail visibly; legacy policy marker blocks destructive cleanup | Redesigned/disabled; no SRP enforcement claimed |
| CG-05 pipe ACL | Explicit local interactive permissions with individual access rights; custom limited-rights client; SYSTEM owner verification | Repaired; Windows standard-user connection test pending |
| CG-06 unlock across boot | Baseline always restricted; 30-minute in-memory lease bound to session where available | Repaired; reboot/session validation pending |
| CG-07 crash and health | Service recovery configuration, bounded server loops, periodic control reconcile and watcher restart/error reporting | Mitigated; runtime filtering is unavailable during an outage and is not pre-boot protection |
| CG-08 filename/path bypass | Removed substring matching; exact full paths or names only under protected installation locations | Mitigated; Windows interpreters and WMI race remain explicitly supported limits |
| CG-09 process scope | Only enrolled standard student SIDs and nonzero sessions; no entire-tree kills; existing student processes checked | Repaired scope; compatibility testing with real Windows apps required |
| CG-10 approval conflict | Same hash exception consulted by installer and application filter; SRP removed | Repaired for exact approved file; child installers need their own approval or temporary teacher access |
| CG-11 hash-before-kill | Unapproved installer is terminated before hashing; failed kill is not reported as successful | Repaired sequence; WMI timing remains reactive |
| CG-12 stalled server | Eight bounded pipe listeners, short session deadlines, bounded TLS clients and frame lengths | Mitigated; sustained local abuse may delay UI; elevated offline recovery remains available |
| CG-13 client timeout | End-to-end deadlines for local and LAN requests; polling and bulk-operation gates | Repaired |
| CG-14 revision increments | Admin owns PolicyId/revision; Agent preserves it; reconcile compares IDs | Repaired; stale-version regression passed |
| CG-15 lost intended state | Persistent intent; no push before initialization; periodic verified reconnect reconcile | Repaired; persistence/no-default-push regressions passed |
| CG-16 false success | Structured control health; partial failures surfaced; persistence exceptions propagate | Repaired reporting; reactive filtering cannot attest that every launch was intercepted |
| CG-17 policy restoration | Durable prior browser values, both views captured before mutation, conflict detection on restoration | Repaired; Windows registry ownership/restore test pending |
| CG-18 uninstall partial failure | Checked exit codes; real service-stop wait; backup retained on restore failure; explicit emergency path | Repaired; interrupted Windows uninstall test pending |
| CG-19 service path | Agent and Ctl installed under Program Files with controlled permissions | Repaired; effective ACL test pending |
| CG-20 deployment | Build-once artifacts, standard student accounts, explicit shared key, config creation, firewall setup, recovery, exit-code checks | Repaired workflow; installer is fail-stop and retains recovery data, not a transactional MSI rollback engine |
| CG-21 invalid JSON | Validation before state replacement; bounds/null/index/duplicate checks; known-good recovery; unsupported modes rejected | Repaired; validation/recovery regressions passed |
| CG-22 untrusted scores | Student views omit answer keys; Agent scores service-owned attempts; arbitrary result text rejected | Repaired; attempt identity is Windows SID, not a separate student login |
| CG-23 timer/progress | Service deadline, persistent answers, resume, guarded finish and late-answer rejection | Repaired; deadline/durability regressions passed |
| CG-24 Telegram loss | Durable results and retry state; delivery reported separately; install notifications capped/deduplicated | Repaired durability; at-least-once notification can duplicate after crash; result IDs supplied |
| CG-25 Download checkbox | DownloadBlocking false is honored; no hidden forced-on rule | Repaired |
| CG-26 stale status | Student status refreshes effective mode and health; Admin distinguishes unverified/partial/teacher/student states | Repaired; Windows visual validation pending |
| CG-27 inverted site labels | Site restrictions/blocked-host wording; exact-host semantics | Repaired |
| CG-28 navigation/tray | Selected navigation background; tray routes to dashboard and refreshes status | Repaired |
| CG-29 contrast | Student muted text and primary button colors darkened | Repaired values; rendered/high-contrast Windows checks pending |
| CG-30 rigid layout | Scrollable entry forms, wrapping quick cards, wrapping answers and smaller Admin minimum size/toolbar wrap | Improved; full DPI/multilingual layout matrix pending |
| CG-31 mouse-only cards | Buttons replace clickable borders; focus styling and default login action | Repaired; keyboard/Narrator verification pending |
| CG-32 localization | All static student-page labels/tooltips have three-language resources; preference persisted | Improved; service diagnostics/dynamic messages and teacher console are not fully translated |
| CG-33 feedback/overlap | Persistent action result text, bounded bulk parallelism, busy and polling gates | Improved; device cards still rebuild on the slower refresh interval |
| CG-34 mutex release | Release only when the instance owns the mutex | Repaired; duplicate-launch Windows test pending |
| CG-35 shell dismissal | Explicit managed-desktop product behavior; hide/show route retained; no kiosk claim | Requirement clarified; kiosk mode not implemented |
| CG-36 library execution | HTTPS-only links, absolute PDF paths, per-device file existence checks, row errors | Repaired input; file distribution is explicitly manual/shared-folder |
| CG-37 domain validation | Exact hostnames only; reject URL/wildcard/newline input; safer hosts replacement | Repaired; Windows hosts metadata/encoding verification pending |
| CG-38 auth starvation/replay | Caller-scoped limits, elevated recovery, shorter 30-second TOTP steps and consumed-code tracking | Mitigated; local same-account abuse can delay login; TOTP consumption is in-memory |
| CG-39 ambiguous namespaces | WinForms/Drawing implicit imports removed from WPF shell | Repaired; all five projects compile |
| CG-40 icon resource | Admin icon packaged as WPF Resource | Repaired; source compiles, clean Windows launch still required |

The implementation deliberately does not claim all 40 items became fully verified Windows guarantees. The table separates fixes, restricted capabilities, and remaining integration/product limits.

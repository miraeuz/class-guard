# October update validation

See `../TOGARAK.md` and `TOGARAK_*` logs for current changes and pending Windows checks. The evidence below is retained from the September base package; it is not a claim that new Windows recovery/UI flows were run.

# Validation evidence

Date: 29 September 2026.

## Executed

- .NET SDK 8.0.425 / MSBuild: Release cross-build of all five production projects targeting .NET 8 Windows completed successfully. No compiler errors or warnings were printed in the successful build output. See BUILD_OUTPUT.txt.
- The regression project was built successfully earlier in this repair session. Its compiled executable was rerun after the final production build: all **35 checks passed**. See REGRESSION_OUTPUT.txt. The regression source did not change between that successful test build and the rerun.
- Coverage includes malformed/unsupported policies, duplicate options, invalid answer indexes, answer-key redaction, library target checks, key length, an RFC TOTP vector, authenticated bootstrap/password changes, bootstrap deletion, known-good policy recovery, nonpersistent teacher mode, durable answers, SID-bound attempts, deadline enforcement, idempotent scoring, install-code consumption, reported enforcer failure, policy-ID reconciliation, persistent Admin intent, no uninitialized push, bounded framing and a real TLS handshake rejecting an incorrect certificate pin.
- XAML and project XML parse successfully. Static-resource keys, wired event methods and dynamic student localization keys were checked. See STATIC_CHECKS.txt.
- ZIP integrity and expected project/docs/test content were checked when packaging.

## Environment limitation

These checks ran in a Linux workspace. The normal dotnet CLI and some later MSBuild invocations encountered an environment-specific process-information exception before compilation; invoking MSBuild directly produced the successful production builds. A later rebuild attempt of the unchanged regression project hit that environment exception; the previously compiled regression executable ran and passed again. This is recorded so that successful runs are not confused with every attempted tool invocation succeeding.

No Windows service, WMI filter, registry/SRP, pipe ACL, scheduled task, firewall rule, installer/uninstaller, WPF screen or physical classroom device was executed here. PowerShell deployment scripts were reviewed, not run on Windows. No Windows screenshot, installer success or production-readiness certification is claimed.

The delivered ZIP contains source, tests, setup scripts and these logs—not a preinstalled Windows service or prebuilt Windows deployment bundle. `scripts\build.ps1` produces the self-contained Windows deployment folders on a Windows build machine.

Before classroom use, complete WINDOWS_ACCEPTANCE.md. AUDIT_ACTIONS.md distinguishes repaired defects from limited/disabled capabilities and pending Windows checks.

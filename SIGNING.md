# Windows publisher identity and signed releases

## What the warning means

“Unknown publisher” means Windows has not verified the file's publisher through a trusted Authenticode signature. The source project previously supplied does not include such a signature. CompanyName/ProductName fields, an icon, a renamed EXE or a hash list do not establish a verified publisher.

“Windows protected your PC / Run anyway” is SmartScreen reputation. A valid publisher signature and a trusted certificate do not guarantee that a new application will bypass this warning. Even EV certificates no longer guarantee immediate reputation. A UAC prompt can still appear for a correctly signed installer because installing the service requires administrator privileges.

This update is SIGNING-READY, NOT SIGNED. No certificate/private key was provided. It does not suppress warnings or change Windows security settings.

## What Mirae needs

Obtain a publicly trusted Authenticode code-signing certificate for the actual publisher's legal identity, with its private key available through the certificate provider on the Windows release PC. The verified name comes from that certificate, not from a name typed in the application. Keep the private key/token and credentials on your signing PC; do not send them in chat or bundle them with the app.

Use the provider's supported Windows certificate-store/token setup. The included workflow selects one exact certificate thumbprint in CurrentUser\My (or LocalMachine\My), so it does not silently choose an unrelated certificate. Cloud services that require a SignTool plugin need a separate provider-specific integration; this script does not implement those plugins.

A self-signed certificate is not a public-distribution fix. An academy-managed PKI can establish trust on explicitly managed computers through IT administration, but this does not create public SmartScreen reputation. These scripts do not install trust roots or change SmartScreen, Defender, UAC or PowerShell execution policies.

## Release workflow on Windows

1. Install .NET 8 SDK and Windows SDK SignTool. Configure your code-signing provider and certificate.
2. In the project folder, run `scripts\build.ps1` as usual. Existing build and regression behavior is unchanged.
3. Run the new script using your real certificate thumbprint, actual SignTool path and timestamp endpoints supplied by your certificate provider. The script requires a NEW destination folder.

```powershell
# Replace every placeholder before running.
.\scripts\sign-release.ps1 `
  -CertificateThumbprint 'YOUR_40_HEX_CHARACTER_THUMBPRINT' `
  -SignToolPath 'C:\Path\To\WindowsSDK\signtool.exe' `
  -TimestampUrl 'https://YOUR_PROVIDER_RFC3161_ENDPOINT' `
  -ScriptTimestampUrl 'http://YOUR_PROVIDER_AUTHENTICODE_ENDPOINT' `
  -ReleasePath 'C:\MiraeReleases\ClassGuard-1'
```

`TimestampUrl` is the RFC 3161 endpoint for SignTool. `ScriptTimestampUrl` must support the timestamp protocol required by PowerShell Set-AuthenticodeSignature; ask the provider for the correct URL. They are not necessarily the same endpoint.

4. The script copies deployment folders and scripts, signs only Mirae application EXEs/DLLs and the copied PowerShell scripts, verifies them, and writes SHA256SUMS.json. Third-party runtime binaries retain their vendor signatures. Source files and the original build outputs remain untouched.
5. A failure leaves SIGNING-INCOMPLETE.txt. Do not distribute that output. Resolve the problem and use a new destination. A completed release contains `artifacts` and `scripts`, preserving the existing install.ps1 relative-path layout.
6. Copy the release to a test Windows computer and verify the exact certificate:

```powershell
.\scripts\verify-release.ps1 `
  -ReleasePath 'C:\MiraeReleases\ClassGuard-1' `
  -CertificateThumbprint 'YOUR_40_HEX_CHARACTER_THUMBPRINT'
```

The verifier checks expected executables, valid signatures, matching publisher thumbprints and timestamps. SHA256SUMS.json detects accidental transfer damage but is not an independent authenticity guarantee. Run the existing Windows acceptance checklist before classroom rollout.

## If you have a separate Setup.exe or MSI

This project currently distributes deployment folders and PowerShell installation scripts, not a compiled Setup.exe. If you or another tool creates an installer, sign the completed EXE/MSI after packaging as well. Signing only files inside an unsigned installer will not give that outer installer a verified publisher. Do not rebuild or modify signed files afterwards; rebuild then sign each new release.

An elevated powershell.exe window shows the publisher of the PowerShell host, not necessarily the publisher of the script it executes. Inspect the script's Authenticode signature separately.

## Verification performed for this update

All nine PowerShell scripts passed PowerShell parser validation. Application source, existing installation/recovery scripts, learning content and build scripts match the preceding redesigned release byte-for-byte. New additions are sign-release.ps1, verify-release.ps1, this guide and documentation/manifest updates. Actual certificate signing, timestamp requests and Windows trust validation remain untested because no Windows signing certificate is available here. Prior UI build/regression logs remain historical evidence for the unchanged application.

Microsoft references, checked 20 October 2026:
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool
- https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/set-authenticodesignature

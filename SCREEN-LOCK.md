> See DASHBOARD-UPDATE.md for the newer expiring StudentShell display reports and dashboard labels. The persistence and recovery instructions below still apply.

# Teacher-controlled lesson screen lock

This release adds a separate lesson screen lock. Existing app restrictions, tests, rich question rendering and multi-PC discovery remain in place.

## Teacher controls

- **Ekranlarni qulflash**: send a persistent screen lock to online, enrolled PCs. Review the per-PC result list; failed, offline and identity-conflicting PCs have not received the command. Commands are not queued for offline PCs.
- **Ekranlarni ochish**: explicitly release the screen lock on those PCs.
- Each PC card has its own screen-lock / screen-unlock button. Its label reflects the last reported service state, not proof that the display is currently covered.
- **Barchasini cheklash** and **30 daqiqa ruxsat** retain their old application-restriction behavior. Neither clears a lesson screen lock.

The Agent writes `screen-lock.json` under its protected ProgramData directory before reporting success. Rebooting, restarting the service, policy synchronization or expiry of a temporary restriction lease does not clear it. First installation starts unlocked; corrupted existing lock state defaults to locked. Upgrade Agent, StudentShell, Ctl and Admin together using the existing build/install instructions. Do not overwrite or delete existing ProgramData during upgrade.

## Student screen

Purple Mirae screen with Uzbek instructions, computer name and a collapsed teacher recovery form. A separate borderless topmost window covers each detected monitor; display layout is rechecked every second. Closing/minimizing the cover or using the dashboard's Go PC control does not release the lock. The local authenticated Agent status is polled every two seconds. A failed connection never releases an existing cover.

At sign-in, the existing scheduled task starts StudentShell. It initially displays the cover until the local Agent answers, so a service startup delay does not accidentally grant desktop access through the application. When the Agent reports unlocked, the cover closes. If the service cannot start, an administrator must repair it or perform recovery.

The supplied EPYax.jpg was unavailable in this workspace. This release uses a native purple Mirae design; it does not reproduce that unavailable reference.

## Recovery

The cover's **O‘qituvchi uchun mahalliy ochish** form accepts the existing teacher account/password or valid one-time recovery code, with the Agent's existing rate limits. It does not use the emergency-uninstall default password. A password change required for a bootstrap account must be completed using the existing `cgctl passwd teacher` workflow.

With a working Agent, an administrator may run elevated:

```powershell
& 'C:\Program Files\ClassroomGuard\Ctl\cgctl.exe' screen-unlock
# Press Enter at the password prompt when already elevated.
```

If the Agent cannot run, use an administrator account and elevated PowerShell (adjust paths if installed elsewhere):

```powershell
Stop-Service ClassroomGuardAgent -Force
& 'C:\Program Files\ClassroomGuard\Ctl\cgctl.exe' emergency-screen-unlock
Start-Service ClassroomGuardAgent
```

The offline recovery command refuses to run while an Agent process is present. It clears only the durable screen lock. Repair any underlying service failure; the startup cover remains until the Agent can confirm unlocked. Existing administrator emergency uninstall remains available through the installed Recovery scripts.

## Limits and required Windows acceptance

This is a classroom attention overlay, **not a tamper-proof Windows kiosk**. It starts after Windows sign-in and scheduled-task startup, not at firmware or on the Windows sign-in / secure desktop. It does not disable Ctrl+Alt+Delete, administrative recovery, alternate desktops, Task Manager or deliberate process termination. If StudentShell is terminated, its cover disappears until restarted; the Agent retains the lock for the next launch. Do not describe this as “impossible to bypass.” Use standard student Windows accounts and managed OS kiosk policies if a hardened security boundary is required.

Tests already in progress retain their server-owned deadlines while the screen is locked; locking does not pause or grant extra time.

The five-project solution compiles and 69 regression checks pass, including durable lock/release, repeated locks, corrupt state, failed saves and unchanged protocol IDs. Native WPF interaction, power-cycle behavior and multi-monitor positioning cannot be executed in this Linux build environment. Before classroom rollout, test on two spare Windows PCs:

1. Install matching components, verify credentials and enrollment, then lock both together. Confirm the per-PC outcomes and all connected monitors.
2. Try dashboard Go PC, Alt+F4, minimize, tray navigation, and monitor unplug/replug. Verify cover placement at mixed DPI settings.
3. Restart one locked PC and sign in. Confirm the cover returns; restart the Agent and push a policy while locked.
4. Disconnect classroom Wi-Fi. Confirm the lock remains; verify the local teacher recovery form works.
5. Reject an incorrect password; release from Admin; verify existing app restrictions still apply.
6. Confirm offline PCs are reported/recognized as not targeted, and deliberately terminate StudentShell to understand the documented limitation.
7. Rehearse elevated offline recovery before broad deployment.

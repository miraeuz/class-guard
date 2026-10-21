# Classroom tools and reliability update — 21 October 2026

This is a complete source update, not an installer or a signed Windows release. Read this guide before the older update notes; it describes the current behavior. Build on Windows using `scripts/build.ps1` and the .NET 8 SDK. Upgrade the teacher app, Agent and StudentShell together. Keep a backup of the current deployment and machine data. Pilot on two PCs before deploying to the classroom.

## What changed

- Existing confirmed screen-cover status, seating view, connection diagnostics, collapsible navigation, responsive layouts and day/night themes remain. Teacher cards now support saved student names and groups, a group selector, help requests, paused-test counts and lesson details.
- Selected-PC policy/test distribution persists a separate policy intent for each machine. Background synchronization no longer replaces that assignment with the global policy. A later explicit global policy push intentionally supersedes prior targeted assignments.
- A lesson applies a temporary policy built from the existing policy form, including permitted applications, site rules, resources and tests. Start with a title and group label. The Agent persists the lesson across service/computer restarts. Automatic baseline synchronization waits until the lesson ends. Ending restores the baseline policy and cancels temporary restriction leases.
- Students can raise/lower their hand through the dashboard or covered-screen window. The teacher sees the request on the PC card and can acknowledge it. The request is tied to the active console session; a cooldown limits repeated requests. Help requests are transient and do not survive service restarts.
- Teachers can pause/resume active assigned tests on selected PCs. Timing is owned by the Agent. Paused attempts preserve remaining time through restarts and reject answer updates and submission. Repeated pause/resume commands do not add time. A bounded timing history records the operations.
- Teacher activity history records an operation before dispatch and then its result. Status polls are excluded. History is viewable/exportable from the dashboard. If the initial journal write fails, the command is not sent; if the result write fails after dispatch, the response reports the history failure without disguising the command outcome.

## Reliability fixes

Attempt changes now commit to memory only after durable storage succeeds. Failed answer, pause or delivery saves no longer leave an in-memory state that falsely appears saved. Test definitions are snapshotted when an attempt starts. Status reads no longer rewrite attempt storage unnecessarily. Asynchronous answer-save responses cannot replace a different currently displayed attempt. Expired attempts cannot be revived by pausing. Settings writes use atomic storage. Classroom policy ownership now distinguishes global, targeted and active-lesson state.

## Using the dashboard

1. Enroll PCs and confirm unique identities and current contact status. Select a card to edit its student display name, group and seat. These are teacher-maintained labels, not verified student identities.
2. Select a group, then select individual PCs or use select-visible. Check the target count and confirmation list before any group action. Changing groups clears the selection.
3. To assign tests to selected PCs, fill the existing policy form and use its selected-PC push button. This distributes the **complete policy**, not just tests. Offline assignments remain pending; active lessons defer them until the lesson ends.
4. To start a lesson, fill the policy form first, enter the lesson title/group label, then start for the selected PCs (or current group when nothing is selected). Review skipped/failed devices. Start/end commands are not queued for offline PCs; retry when they reconnect.
5. Pause/resume operates on attempts already active on those PCs. New attempts are not automatically paused. The local learning/practice quizzes are separate from service-owned assigned tests.
6. End the lesson to restore baseline restrictions. This does **not** unlock covered screens or resume paused tests; use those explicit controls separately. Lesson-end summaries are written under `%LocalAppData%\ClassroomGuard\LessonSummaries` on the teacher PC.

## Data and recovery

Agent lesson state is stored in protected ProgramData `lesson.json`; existing attempts retain their storage location and add pause/timing fields. Teacher names/groups/seats remain local dashboard preferences. Per-device policy intent is stored beside the global intended-policy file with a `.devices.json` suffix. Teacher history is local `activity.json`, bounded to the latest 2,000 entries. Timing history is bounded to the latest 200 events per attempt.

History identifies the sending Windows account for local attribution. It is not a tamper-proof central log or cryptographically authenticated individual-teacher identity; classroom authentication remains the existing enrolled/pinned connection and classroom credential. Summaries count tests started during the lesson, not attendance, and include partial-command failures. The machine-local student label is not proof of who took a test.

Corrupt lesson state fails closed rather than silently discarding restrictions. If the Agent cannot start because of corrupt `lesson.json`, an administrator must preserve the file and service logs, restore a known-good backup or explicitly reset the lesson state while the service is stopped, then start the service and verify the intended baseline before returning the PC to students. Never delete classroom data as routine troubleshooting.

Screen covering is an application overlay, not Windows kiosk enforcement. Its confirmation is based on the existing live StudentShell heartbeat; killing the shell can remove coverage until it restarts. Do not interpret stored lock intent alone as proof that a screen is covered. Site/application restrictions retain their existing documented enforcement limitations.

## Verification completed

- All five solution projects compile in Release with .NET SDK 8.0.414.
- 116 C# regression checks pass, including failed-save rollback, durable pause/restart/resume timing, stale lesson-end rejection, policy clone isolation, targeted-policy synchronization and audit-before-dispatch behavior.
- 19 rich-test browser checks pass, including sanitization, offline LaTeX, responsive widths and day/night styling.
- 106 learning browser checks pass. Both browser suites report no page errors or external requests.
- Logs for this update: `docs/CLASSROOM_TOOLS_BUILD.txt`, `docs/CLASSROOM_TOOLS_REGRESSION.txt`, `docs/CLASSROOM_TOOLS_RICH.txt`, `docs/CLASSROOM_TOOLS_LEARNING.txt`.

These checks ran in a Linux environment with Windows targeting and headless Chromium. Native WPF rendering, Windows service installation, UAC/signing, actual network/reboot behavior and real classroom interactions have not been executed here. Earlier validation files describe their respective releases, not additional current test runs.

## Windows pilot acceptance checklist

- Run teacher and two students at 100%, 125% and 150% display scaling, narrow/wide windows and both themes. Check navigation, details, group editing, history and confirmations for clipping and keyboard access.
- Connect all ten uniquely enrolled PCs. Confirm all cards coexist; renew DHCP, disconnect/reconnect and simulate duplicate IDs. Verify diagnostics distinguish these states.
- Lock selected PCs, then the class. Verify command sent versus screen covered versus failure; stop StudentShell and verify confirmation expires. Restart a locked PC and verify coverage returns. Unlock explicitly.
- Save names/groups/seats, restart the teacher app, and confirm persistence. Change groups and ensure no hidden old selection receives commands.
- Assign different test policies to two PCs. Wait through several synchronization cycles and restart teacher/Agent. Check each retains its own assignment. Push a global policy and verify the deliberate override.
- Start a lesson with one PC offline. Confirm partial results; reconnect and explicitly retry. Restart an active-lesson Agent and verify the temporary policy persists. End the lesson and check baseline restoration and summary output.
- Pause a timed active test, wait, reboot the student, then resume. Check remaining time, disabled answers/submission and timing history. Repeat commands and verify no added time. Confirm new attempts and local practice quizzes behave as documented.
- Raise/lower/acknowledge help, including while covered and after console-user changes. Confirm help does not unlock the screen or alter test timing.
- Exercise journal/attempt storage write failures in a disposable test installation. Confirm commands do not dispatch without initial history, and failed answer/pause saves leave prior persisted state intact.
- Verify installer signature and normal administrator recovery with existing deployment documentation. Do not deploy the entire classroom until the pilot passes.

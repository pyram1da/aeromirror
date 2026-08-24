# Troubleshooting review builds

Review builds contain additional local diagnostics for receiver startup,
Bonjour discovery, UxPlay, and GStreamer. They do not upload telemetry. Please
attach a diagnostic log to a bug report when the receiver is missing from the
iPhone, the first connection fails, the stream window closes unexpectedly, or
the application reports that the receiver is running when it is not usable.

## Find the receiver log

Use either method:

1. On the main screen or in the tray menu, choose **Report a problem**.
   AeroMirror creates an additionally redacted temporary snapshot, opens a
   pre-filled GitHub Issue, and selects the file in Explorer. Review it and
   drag it into the Issue; no file is uploaded automatically.
2. Right-click the AeroMirror tray icon and choose **Open log**.
3. Press `Win+R`, paste the following path, and press Enter:

   ```text
   %LOCALAPPDATA%\AirPlayReceiverMvp\receiver.log
   ```

The full path normally expands to:

```text
C:\Users\<you>\AppData\Local\AirPlayReceiverMvp\receiver.log
```

Copy the log after reproducing the problem and before reinstalling or cleaning
application data. In the GitHub report, include the exact local time and time
zone of the failure so the relevant section can be identified.

GitHub and the browser intentionally prevent a desktop application from
silently attaching a local file. Signing in and adding the selected snapshot
is therefore a visible user action.

If installation or an in-place update itself fails, attach the reviewed
installer journal from:

```text
%LOCALAPPDATA%\AirPlayReceiverMvp\setup.log
```

## Protect private data before sharing

Review builds mask known PIN and password arguments, but always inspect the
file yourself before uploading it. Older builds may have written the fixed PIN
in plain text.

At minimum, replace:

- a PIN such as `-pin 3669` with `-pin ****`;
- any password or password-like advanced argument with `****`;
- your Windows account name in file paths with `<user>`;
- private receiver, PC, Wi-Fi, or network-profile names with descriptive
  placeholders;
- public IP addresses, MAC addresses, or other identifiers you do not want to
  publish.

Do **not** attach any of these files:

```text
receiver-key.pem
trusted-clients.txt
settings.ini
```

Do not paste an Apple ID, Wi-Fi password, Windows credentials, private key, or
the contents of the trusted-client register into an issue. If a maintainer
needs another diagnostic artifact, share only the specifically requested file
after reviewing it.

## Reproduce a first-run receiver problem

The safest complete first-run test is a clean Windows virtual machine or a
separate Windows user account. Bonjour is installed system-wide, so reinstalling
AeroMirror on the same account is not equivalent to a machine that has never
had Bonjour.

AeroMirror Setup extracts the pinned portable application runtime, but installs
no system-wide .NET/VC++ redistributable, driver, or framework prerequisite; a
full Windows reboot is not an expected normal prerequisite. One reported
Windows 10 installation became usable only after reboot, but no pre-reboot
evidence was retained. A stopped or stale Bonjour service lifecycle is only the
strongest current hypothesis, not a diagnosis. AeroMirror observes Bonjour but
does not start, stop, repair, uninstall, or otherwise mutate that machine-wide
service.

For a normal review test:

1. Record the AeroMirror version, Windows version, iPhone model and iOS
   version, installer/build type, connection type, and network profile.
2. Exit AeroMirror from its tray menu. Closing the window may only hide it.
3. Start the review build and note the exact local time.
4. Accept the Windows Firewall prompt for private/local networks if it appears.
   If a Bonjour firewall UAC prompt appears, record the choice and result. The
   0.12.20 headless core reports an absent Bonjour prerequisite but does not
   offer a bundled service installation. Before any reboot, record whether the
   Bonjour service exists, its
   state/start type, and whether its process is running. Do not remove or
   modify Bonjour manually on a daily-use PC.
5. Do not restart the receiver yet. Wait 60 seconds, open **Control Center →
   Screen Mirroring** on the iPhone, and record whether AeroMirror appears.
6. Attempt one connection. Record whether the PIN prompt, video window, and
   audio appear and whether any window closes unexpectedly.
7. If the receiver is still unavailable, use **Stop receiver**, wait five
   seconds, then use **Start receiver**. Record the time and whether this
   workaround changes the result.
8. Copy `receiver.log`, review and mask it as described above, then attach it
   to the bug report.

To simulate clean per-user settings without deleting data, exit AeroMirror and
rename `%LOCALAPPDATA%\AirPlayReceiverMvp` to a backup name. This resets the
receiver identity, PIN trust, and application settings for that Windows user.
Restore the folder only while AeroMirror is stopped. Prefer a VM or separate
Windows account if you are not comfortable handling this backup.

For a startup-after-reboot problem, also state whether AeroMirror was launched
by Windows startup or manually. Wait at least 60 seconds after signing in
before applying the stop/start workaround. If only a full reboot helps, retain
both pre- and post-reboot Setup/receiver logs, Bonjour service/process state,
pending-reboot indicators, sockets/readiness markers, and iPhone browse result.
Do not report the reboot as a normal installation requirement until a clean VM
reproduces the same lifecycle.

### 0.12.20 Private Bonjour firewall diagnostic

If diagnostics report that the exact Private Bonjour mDNS rule is missing, use
**Разрешить Bonjour** on the main network card only after recording the
existing firewall state. Clicking that scoped action is the application-level
confirmation; Windows then shows one UAC prompt and may add one inbound rule
for the validated `mDNSResponder.exe`: Private profile, UDP 5353, remote
`LocalSubnet`, no edge traversal. Declining UAC or a failed exact-path/policy
check must leave the machine unchanged. This action does not repair the Bonjour
service, does not run automatically, and 0.12.20 does not remove the external
rule during uninstall. If Bonjour itself is absent, the card reports that
prerequisite and offers no repair button. Record the before/after rule and
iPhone browse result.

## What the review log records

Depending on the review build, the log may include:

- application version, startup mode, timestamps, and unhandled shell errors;
- sanitized receiver arguments and settings changes;
- receiver process IDs, start/stop reasons, shell readiness checks, exit
  codes, and restart backoff;
- Bonjour service presence and running state, plus observed receiver
  server-socket initialization;
- guarded idle-discovery and Windows-unlock maintenance decisions, including
  the first ten-minute stage and every later 20-minute recurring renewal,
  whether work was scheduled, deferred, canceled, accepted, ready, failed, or
  timed out, and whether one of the first two renewals used the bounded legacy
  process-restart fallback;
- for a capable 0.12.13 core, the discovery-command capability plus exact
  request/generation/PID/RAOP-port/AirPlay-port deferred, accepted, ready, or
  failed markers. Ready means both records received local callbacks for that
  generation; it is not continuous iPhone-visibility proof;
- BLE helper lifecycle as complete stderr lines, including one unexpected
  start/exit failure. Intentional helper stop during maintenance is excluded;
- receiver-name input, registered, and RAOP-label byte counts plus a truncation
  flag. The original receiver name is intentionally not written by this marker;
- relevant physical-network changes without stream content;
- UxPlay standard output and error messages;
- feedback-gap episode count, longest duration, native recovery-marker
  capability state, and in the 0.12.8 proof gate carried into 0.12.9 the
  recovery epoch carried by
  `AEROMIRROR_CLIENT_FEEDBACK_RECOVERED`;
- one-shot post-gap `AEROMIRROR_VIDEO_PUSH_RECOVERED` flow/PTS,
  `AEROMIRROR_VIDEO_PUSH_PENDING`, `AEROMIRROR_VIDEO_SINK_RECOVERED` exact-PTS,
  and `AEROMIRROR_VIDEO_PRESENT_READY epoch=E gap_seconds=N
  proof=d3d11-present pts_delta_ms=D` stages, plus the exact
  `AEROMIRROR_VIDEO_PRESENT_PROOF_READY codec=h264|h265
  videosink=d3d11videosink` capability. The earlier stages are diagnostic only;
  only a present marker
  correlated to the current process, mirroring session, recovery epoch, and
  capability may authorize continuity handoff;
- the raw AirPlay geometry header, including an auxiliary width/height pair
  that is diagnostic only and is not a validated crop, pixel-aspect-ratio, or
  rotation field;
- for 0.12.14, one `AEROMIRROR_VIDEO_HEALTH` record every two seconds while
  mirroring is active. Its session/geometry, interval deltas, ages, flow/state,
  PTS counters, pause/resume state, proof availability, and `class` locate a
  stage boundary; no single line or `class=healthy` proves visible motion or a
  root cause. These records contain no media payload or pixels, but surrounding
  log lines still require normal privacy review;
- for 0.12.15, a fixed
  `AEROMIRROR_VIDEO_IMPLICIT_RESUME reason=valid-type0` line when a complete,
  decrypted, NAL-validated video unit arrives while the stream is still marked
  suspended. The same unit continues to the renderer. This marker proves the
  parser/action boundary only; correlate it with later health deltas, sink and
  Present progress, and a screen recording before saying visible video resumed;
- for 0.12.19, managed fullscreen/control/Escape and stale-borderless
  detection/manual-exit
  lines plus native `AEROMIRROR_VIDEO_FULLSCREEN` and
  `AEROMIRROR_VIDEO_SCALE` results. Photos presentation and fullscreen are
  expected to use 1000 permille. These records contain no pixels and do not
  prove that the complete visible image is correct;
- the actual GStreamer decoder/video sink selected at pipeline creation, plus
  renderer, pipeline warnings, and errors.

The log is intended not to contain:

- screen, photo, video, or audio content;
- Apple ID or iCloud credentials;
- receiver private-key contents;
- trusted-client register contents;
- unmasked PINs or passwords.

Diagnostics remain on the PC until the user chooses to share them. If a log
does contain a secret, do not publish it: mask the secret first and mention in
the report which field was removed.

## Information that makes a report actionable

Please include:

- the exact failure time and time zone;
- whether it happened on first install, first start after reboot, manual
  start, reconnection, or after changing settings;
- for a missing receiver after idle, the last successful session time, lock,
  sleep, sign-in and SessionUnlock times, every numbered timed or unlock
  renewal, any legacy fallback decision, each in-process discovery request and
  terminal generation, PID/ports before and after it, every iPhone browse
  attempt, and whether the first tap reached Windows before using **Restart
  discovery**;
- when testing **Restart discovery**, record the replacement PID, ports, fresh
  DNS-SD/BLE startup, and iPhone result. Starting with 0.12.13 this button deliberately
  remains the strong full-process path rather than the narrow automatic DNS-SD
  command;
- for the Windows 10 reboot symptom, whether the machine/VM had ever contained
  Bonjour, every installer/UAC/firewall prompt, pre/post-reboot Bonjour service
  and process state, and whether receiver Stop/Start changed the result;
- whether **Restart receiver** helped, and whether a full **Stop receiver** /
  **Start receiver** cycle helped;
- whether the receiver process or only the video window disappeared;
- whether the continuity view stayed at **Connection lost**, changed to
  **Connection restored / Waiting for image**, changed to the Screen Mirroring
  reconnect hint, or faded while the image was still frozen;
- the selected quality, latency, renderer, and audio options;
- for a Photos sizing report, the ordered raw/encoded geometry, whether a
  phone-shaped frame preceded or followed the exact media signature, outer
  renderer client bounds, separately measured visible inner-media bounds,
  phone orientation, automatic scale result, and whether the same session
  remained connected; for fullscreen also record tray/Alt+Enter/Esc entry and
  exit, Photos entry/exit while fullscreen, whether the title bar returned, and
  whether the normal window could again move and resize;
- for a receiver-name problem, include input/effective UTF-8 byte counts and
  whether the save notice appeared, but replace the actual private name. State
  whether the value came from an interactive save or a legacy profile;
- for stutter, the local Wi-Fi band/channel and PC connection type, a 60-second
  reproduction interval, visible freeze count, audio drift, CPU/GPU load, and
  the feedback-gap totals from diagnostics; public internet speed alone does
  not measure the local AirPlay path;
- Bonjour status from AeroMirror diagnostics, if available;
- whether VPN, Hyper-V, WSL, a mobile hotspot, or a virtual network adapter was
  active;
- a minimal numbered reproduction sequence;
- for a reconnect handoff, the feedback gap, recovery epoch, core PID, managed
  session generation and start/stop transition, and the complete sequence of
  push/PTS/sink/present markers, including any
  `AEROMIRROR_VIDEO_PRESENT_ARMED reason=mirror-start epoch=E` re-arm after
  manual reselection and whether the three-second proof wait then expired;
  do not treat feedback recovery, mirror-start, or `flow=ok` as proof that
  Windows displayed a fresh frame; `gap_seconds=0` is expected only for the
  matching mirror-start re-arm, not for ordinary feedback recovery;
- for a 0.12.14 frozen-last-frame run, keep the complete health sequence from
  mirror start through iPhone Stop and note the visible freeze time. Several
  consecutive interval deltas and ages are required to identify the stalled
  stage;
- for a 0.12.15 pause/freeze run, retain every implicit-resume marker, the
  preceding pause/action evidence, the next several health intervals, and the
  exact visible-motion result. Do not omit a marker from a failed run or treat
  it as proof that decode/Present succeeded;
- the reviewed and masked `receiver.log`.

Avoid posting only “it crashed.” A timestamp plus the distinction between the
AeroMirror window, the tray application, the receiver process, and the stream
window is especially useful.

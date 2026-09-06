# AeroMirror 0.12.26 — first-surface and stopped-Bonjour test plan

## Purpose

Verify that a fresh normal viewer displays the incoming stream without any
window-state workaround, that stale native work cannot cross session teardown,
and that a user can explicitly start an exact stopped Apple Bonjour service
without automatic elevation or receiver-process churn.

This plan separates local/native readiness from two physical observations:
visible pixels in the untouched normal viewer and actual iPhone discovery after
Bonjour recovery.

## Current evidence status

| Gate | Status | Required evidence |
|---|---|---|
| 0.12.25 physical disposition | FAIL / RETAINED | Untouched normal viewer was black; fullscreen made the same active stream visible |
| Real-HWND presentation source contract | PASS | Fresh READY -> SHOW/READY -> selected bind/commit -> PLAYING; replacement NULL -> new HWND -> READY -> SHOW/READY -> bind/commit -> PLAYING; exact-generation expose |
| Session lifetime and stale-work contracts | PASS | Start serialization, operation references, generation checks, bus flush, and stop/destroy invalidation |
| Explicit Bonjour safety contract | PASS | Contextual exact-Stopped action; service-object DACL and full protected-path revalidation; at most one explicit UAC; no startup/timer/monitor invocation or same-process live-helper overlap |
| Managed/native regression suites | PASS | Frozen managed build plus Bonjour, resilience, update, NativeHost, NativeCore, and NativeWorker transcripts |
| Reproducible native build and corresponding source | PASS | Two clean builds plus extracted no-Git rebuild produced `FAA8A1575EAC7C26BA41DF09A81EB08E03DE05A621FA3C504289EA8E98DAB84A` |
| Exact review payload and Setup | PASS | Exact 0.12.26 versions, entry set, embedded equality, and all four non-installing Setup checks |
| Physical fresh normal viewer | FAIL | September 4 user report: untouched normal viewer still black; automated ordering markers did not establish visible output |
| Physical initial mixed-window Z-order | FAIL | Viewer behind Explorer Installer and AeroMirror, but above Instagram/Google |
| Physical stopped-Bonjour recovery | PENDING | One explicit prompt; same-process/same-port DNS-SD ready; receiver appears on iPhone |
| Tag and publication | NOT AUTHORIZED | No push, tag, asset replacement, or GitHub Release |

## Verified local artifacts

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `AeroMirror-Setup-0.12.26.exe` | 1,559,040 | `90A62EB5F8A84F2BBB559D7E0E5500AA1292AADC6341D723B665F6755EE15072` |
| `AeroMirror-review-payload-x64-0.12.26.zip` | 1,252,665 | `F925193CE86A11C7B1986A60342F883566020FCDFF93F7F10F0F0BA25CF58ACF` |
| `AeroMirror-native-source-0.12.26.zip` | 909,852 | `BCCBAFDAAE8FE41DD35500EA1FF4EC078F4BAFDCC1008E516AA1DFD2171BC1F9` |
| packaged `AeroMirror.exe` | 827,904 | `9405460171A4AC07D06B4BA1D7F7576ABF2AA66464D9D75765FD82F558220E16` |
| packaged `core/uxplay-windows.exe` | 1,227,667 | `FAA8A1575EAC7C26BA41DF09A81EB08E03DE05A621FA3C504289EA8E98DAB84A` |

The review payload contains exactly 13 files. The corresponding-source archive
contains 149 entries / 145 files and its extracted no-Git tree rebuilt 57/57
targets to the same core hash. Shell and Setup file versions are both
`0.12.26.0`; Setup's embedded payload and provenance matched the final inputs,
and `/verify-runtime`, `/verify-shortcut-selection`,
`/verify-update-lifecycle`, and `/verify-bonjour-recovery` all returned zero.

## Test environments and retained evidence

Use the exact local 0.12.26 Setup on the affected Windows PC and a controlled
Windows machine or VM for the service-stop cases. Retain Windows build, GPU,
display scale, AeroMirror/Setup/core versions and hashes, iPhone/iOS version,
quality preset/codec, physical network category, shell/core PIDs, AirPlay ports,
validated Bonjour service name/path/status, action time, UAC result, and the
redacted receiver log.

Do not retain the active PIN, receiver key, trusted-client contents, Wi-Fi
password, Apple ID, mirrored pixels, or unrelated user paths. Do not
deliberately damage Bonjour on a daily-use machine.

## Automated acceptance

1. Verify every public/default source version is `0.12.26` and Windows PE/file
   version is `0.12.26.0`. Verify `update-repository.txt` remains
   `Nadejny/aeromirror`.
2. Verify renderer start is serialized. Fresh mirror pipelines must wait in
   READY until Qt acknowledges the exact visible/nonzero real child HWND, the
   selected codec sink is bound and committed for that generation, and only
   then the selected pipeline reaches PLAYING.
3. Verify that, after Qt supplies a replacement real child HWND, the selected
   pipeline reaches NULL before the sink accepts that handle, returns to READY,
   obtains a fresh SHOW/READY acknowledgement, rebinds and commits the selected
   sink for the unchanged current generation/HWND, and only then restores
   PLAYING. An expose request
   may execute only when lifecycle, READY, bound, and Present generations all
   match. Neither initial start nor replacement may depend on fullscreen,
   outer-window resize, expose as surface creation, a render rectangle, crop,
   scale, pixel inspection, or periodic refresh.
4. Verify every renderer bus callback carries an immutable session generation.
   State-changing bus paths, render, pause/resume, HLS, and presentation work
   must reject a generation that is no longer active.
5. Verify stop/destroy invalidates the active generation under the renderer
   state lock, flushes the old bus, removes publication, and waits for retained
   operations before unref/free. A queued old callback must not act on a later
   session.
6. Verify H.264 and H.265 choose and bind only their selected sink while unused
   renderer objects remain safely owned through final destruction.
7. Verify the normal shell performs only read-only Bonjour assessment until the
   user clicks the contextual action. Startup, periodic health checks, idle
   discovery maintenance, network-change work, receiver Stop/Start, and
   automatic update must never launch elevation or a service-start command.
8. Verify the action is visible only when the exact Apple service and canonical
   protected executable pass validation, the service-object owner/configuration
   DACL is trusted, and service state is `Stopped`. Verify every component of the
   protected Apple executable path and Windows `sc.exe` path is checked for
   owner, write ACL, and reparse points.
   `Running`, `StartPending`, `ContinuePending`, `StopPending`, `PausePending`,
   `Paused`, `Unknown`, absent, replaced, reparse, unsafe-owner, unsafe-ACL, or
   path-mismatch states must not create an unsafe start route.
9. Verify one click can own only one in-flight attempt, uses only the absolute
   system service-control executable with the exact allowlisted service name,
   requests `runas` at most once, and applies bounded process/service waits. No
   shell, user-writable helper, arbitrary arguments, or automatic retry loop is
   permitted.
10. Verify UAC cancellation, timeout, early command failure, identity change,
    and service-not-running result are reported without crashing, restarting the
    core, or scheduling another prompt. If the elevated process remains live at
    timeout, its one-flight latch must remain held until confirmed exit for that
    AeroMirror process lifetime; only then may a later user click retry.
11. Verify success revalidates the service as `Running`, releases the explicit-
    action latch, and enters the existing bounded Bonjour-return recovery. At
    most two same-process DNS-SD submissions may occur for that recovery event;
    a correlated `AEROMIRROR_DNSSD_READY` is required before ready returns.
12. Run the managed build, ReceiverResilience, Bonjour/firewall,
    AutomaticUpdate, NativeHost, NativeCore, and NativeWorker suites. Run
    `git diff --check` and retain complete transcripts.
13. Materialize the exact native patches and provenance, run two independent
    clean compatible native builds, stage/verify the runtime, create and extract
    the no-Git corresponding-source archive, and rebuild it. All three core
    hashes must equal the frozen expected value.
14. Build the exact review payload and x64 Setup. Verify the allowed entry set,
    shell/core/provenance equality, `0.12.26.0` versions, embedded-input
    equality, and all applicable non-installing Setup self-checks.

Any automatic UAC prompt, unvalidated service start, broad/elevated helper,
generation-crossing native callback, pre-bind PLAYING transition, version/hash
mismatch, unexpected package entry, secret in output, or failed self-check
blocks handoff and publication.

## Physical acceptance — untouched fresh viewer

For each fresh-start row, begin with no active mirroring session. After selecting
AeroMirror on the iPhone, do not move, resize, minimize, maximize, fullscreen,
restore, or use a tray window action before recording the initial result.

1. Select the H.265-capable `4k60` preset and connect from the portrait iPhone
   home screen. The first normal framed viewer must show moving video without a
   black interval that requires user intervention.
2. Stop Screen Mirroring from the iPhone, wait for native session cleanup, and
   repeat the H.265 fresh connection five times.
3. Select a non-H.265 preset and repeat at least two fresh H.264 connections.
4. Open the same portrait gallery photo accepted in 0.12.24. Its proportions
   and size must remain correct without changing the outer window as a gallery
   workaround.
5. In a successful stream, minimize and restore the viewer. Video must remain
   visible without resize, fullscreen, reconnect, or a periodic repaint loop.
6. Enter fullscreen once and exit with Escape only after the untouched-start row
   is recorded as PASS. Confirm saved normal placement and visible video return;
   fullscreen must not be credited as recovery for a failed first surface.
7. With an ordinary browser foreground, connect and confirm the viewer rises
   once without taking keyboard focus or becoming persistently topmost. Repeat
   with external fullscreen content and confirm AeroMirror stays behind it and
   defers an initial automatic fullscreen request.

Retain the generation, real HWND, NULL/READY/SHOW transitions, selected-sink
bind/commit, PLAYING, first-buffer/Present, exact-generation EXPOSE,
stop/destroy, codec, and timing markers for each applicable row. Those markers
prove order, not visible pixels; a screen recording or direct tester observation
is required.

## Physical acceptance — stopped Bonjour

1. On a controlled machine with the exact safely installed Apple Bonjour
   service, record the receiver/core PID and AirPlay ports, then stop the service
   and wait until Windows automatic recovery is exhausted. Confirm AeroMirror
   reports the prerequisite and shows **Запустить Bonjour** only in the contextual
   stopped state.
2. Leave AeroMirror untouched for at least two monitoring intervals. No UAC
   prompt or service-start process may appear automatically. Receiver Stop/Start
   must not be presented as the service fix.
3. Click **Запустить Bonjour**, cancel UAC, and confirm there is no repeated prompt,
   no core restart, and no false ready state. The action becomes available for a
   later deliberate retry.
4. Click again and approve UAC. There must be only one prompt and one bounded
   start attempt. Confirm the exact service becomes `Running` and no other
   service or firewall state changes.
5. Confirm paired DNS-SD recovery completes in the original core PID on the
   original RAOP/AirPlay ports and produces a correlated
   `AEROMIRROR_DNSSD_READY`. The iPhone must then list the receiver and start a
   real mirroring connection without a receiver-process restart.
6. Repeat a start failure or timeout with a controlled fixture if practical.
   AeroMirror must remain responsive, avoid automatic retries/UAC, and keep a
   truthful stopped-prerequisite state.
7. Return the controlled machine to its normal service state and retain the
   redacted Windows service events plus receiver log.

Local `Running` and `DNSSD_READY` evidence alone does not pass step 5; remote
iPhone listing and connection are required.

## Failure branch

If the initial normal viewer is black, do not enter fullscreen before preserving
the failed row's complete generation/READY/bind/PLAYING/buffer/Present timeline
and visible observation. If fullscreen later exposes the image, record that only
as failure localization. Do not add viewer geometry, crop, scale, or repeated
redraw workarounds to this candidate.

If Bonjour prompts automatically, prompts more than once per click, starts an
unvalidated service, restarts the core on success, changes listener ports, or
claims ready without a correlated DNS-SD acknowledgement, preserve the service
state/events and receiver log and mark the row FAIL.

## Publication boundary

This plan authorizes only local build and physical testing. It does not
authorize a source push, tag, GitHub Release, public asset replacement, or
updater-visible publication. Public 0.12.22 remains the immutable latest until
separate explicit authorization.

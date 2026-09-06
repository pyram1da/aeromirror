# AeroMirror 0.12.30 test plan

## Environment and evidence

Use the exact candidate shell/core hashes, Windows version, iPhone/iOS version,
codec, receiver PID and listener port. Retain privacy-safe protocol markers;
do not record phone content or pairing secrets. Automated lifecycle checks do
not prove iPhone Screen Mirroring UI state.

## Automated gates

- Production HTTP owner: close only the selected session, retain another
  connection, reject a delayed old-session close after same-socket replacement,
  complete duplicate/stale requests, preserve listener/port across reconnect,
  cancel an outstanding request when stopping, never reuse session IDs.
- Production shell: parse exact started/closed markers and reject unrelated
  text, missing IDs, zero IDs and overflow; stale Close cannot dismiss a newer
  managed session.
- Continuity warning: reject a stale process, managed generation or native
  stream ID; allow the old warning to be dismissed if its core already ended.
  Both native closed-marker sources use the same correlation. Recheck the PID
  under the lifecycle lock and again under the command writer lock.
- A matching completed Close, without the legacy stop line, ends managed
  activity and cancels that session's loss watchdog. Duplicate completion is
  idempotent; stale results, wrong embedded PIDs and replacement sessions are
  unaffected. This is not proof of the iPhone's remote UI state.
- Native build twice and source-archive rebuild; verify complete corresponding
  source, changed patch/source/binary hashes and pinned Qt/runtime versions.
- RendererShowBoundary, NativeHostContracts, ReceiverResilience, existing
  positive NativeCoreContracts, NativeWorkerLifecycle, update and Bonjour
  checks. Setup gates are non-installing.

## Automated results — 2026-09-06

PASS: production HTTP-owner loopback checks (including 50 close/reconnect
cycles), managed exact-session and completed-Close checks, two native builds,
extracted no-Git source rebuild, native host/core/worker/Qt-surface checks,
renderer child-placement regression, update/Bonjour/UI-work ownership checks,
all five packaged-shell test suites, the 13-entry review payload and all four
non-installing Setup gates. Runtime dependency validation and an isolated
self-test also pass. See [LOCAL_BUILD_REPORT.md](LOCAL_BUILD_REPORT.md) for
exact hashes, test scope and deployment warnings.

No physical .30 test has been recorded. Automated listener/worker checks do
not transmit iPhone media and do not establish the iOS Screen Mirroring state.

## Physical acceptance

1. Start mirroring without resizing or fullscreen. Verify visible pixels.
2. Caption Close: window disappears, phone leaves Screen Mirroring, no lost
   connection placeholder/reopened window. PID, port, DNS-SD/BLE stay unchanged.
3. Reconnect immediately and repeat Close. No old request may stop this session.
4. Lock the phone, wait for loss/frozen state, then close both the viewer and,
   in a separate run, the continuity warning. No window resurrection. Closing
   a previous session's lingering warning must not stop the new session.
5. Check portrait Photos size, Escape from fullscreen and normal-view placement.
6. Complete five fresh H.265 and two H.264 starts; mixed-window foreground/game
   protection and real Bonjour stop/recovery remain separate physical gates.

Any stopped listener, changed advertised port, loss of the newer session,
black normal viewer or iPhone still mirroring after a reported successful Close
blocks acceptance as a fix. On September 6 the user explicitly authorized a
normal-channel review publication with these physical rows pending. This is
not acceptance of those rows and does not authorize a local installation.
Public artifact and route evidence must be added to BUILD_REPORT.md after
clean exact-tag packaging and publication.

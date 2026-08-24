# AeroMirror roadmap

This file records product work that is intentionally outside the current
review build. It is not a promise that every item will ship. Protocol-facing
features must be validated against current iOS behavior, compatible hardware,
upstream licenses, and applicable platform rules before implementation.

## Reliability foundation

### 0.12 repository restructure

The 0.12 cleanup must be a sequence of reviewable, behavior-preserving
changes. Do not combine directory moves with AirPlay lifecycle fixes or native
protocol work. Preserve the installed executable name, persistent per-user
paths and registry identities, receiver key and trust state, native runtime
layout, public asset names, and source-provenance checks throughout the move.

#### Phase 1: managed application source

- [x] Record a clean build and resilience-test baseline before moving source.
- [x] Split the single managed shell source into focused files for program
  startup, settings, receiver supervision, discovery and recovery, renderer
  policy, networking, updates, diagnostics, UI controls, theming, and Win32
  interop.
- [x] Keep the existing namespace and runtime identifiers during the
  mechanical split; rename or redesign them only in a separate reviewed
  change with migration coverage.
- [x] Update the build to compile all managed source files deterministically.
- [x] Update resilience checks so they no longer assume one source filename.
- [x] Remove unreachable legacy forms only after the split passes the same
  build and tests, as a separately reviewed cleanup step.

Acceptance target: the shell is behaviorally unchanged, its executable still
targets Windows 10 1809+ x64 and Windows 11 x64, and existing settings,
autostart, pairing identity, logs, and update behavior remain compatible.

#### Phase 2: installer and packaging source

- [ ] Split installer UI, paths, logging, runtime acquisition, transaction,
  process shutdown, shortcuts, registry, and verification logic into focused
  files without changing embedded resource names or upgrade identity.
- [ ] Consolidate duplicated PowerShell helpers for safe child paths, PE
  inspection, hashes, manifests, and version validation.
- [ ] Keep stable root build and release entry points as thin wrappers while
  moving implementation scripts into a documented `scripts/` layout.
- [ ] Separate final deliverables, reusable downloaded caches, and temporary
  staging so failed builds cannot leave ambiguous release inputs.
- [ ] Add bounded, preview-first cleanup for generated output; cache deletion
  must remain an explicit separate choice.

Acceptance target: setup and in-place update verification pass with exactly
the same installed paths, registry identity, shortcuts, payload contract, and
rollback behavior.

#### Phase 3: version and documentation enforcement

- [ ] Introduce one checked-in public-version source and generate the required
  four-part Windows assembly versions during build.
- [ ] Make every build, installer, packaging, and release command reject a
  requested version that differs from the checked-in version.
- [ ] Add an automated documentation gate implementing
  `docs/DOCUMENTATION_POLICY.md`.
- [ ] Move historical build reports and test plans into
  `docs/releases/<version>/` with link-preserving documentation updates.
- [ ] Keep curated release notes in the repository and use them as the GitHub
  Release body instead of relying on an ignored local artifact.

Acceptance target: a patch cannot reach the release step with inconsistent
app, installer, asset, changelog, project-state, release-note, or test-plan
versions.

#### Phase 4: continuous verification

- [ ] Add `.editorconfig` and explicit UTF-8/LF rules compatible with Windows
  PowerShell and the current compiler.
- [ ] Add a single local command that runs the shell build, resilience checks,
  installer logic checks where inputs are available, documentation policy,
  and `git diff --check`.
- [ ] Add an isolated temporary-profile integration test that exercises the
  complete `AppSettings.Load()` and `Save()` wiring, including first save and
  an injected replacement failure, without touching a user's real settings.
- [x] Keep reflection-based resilience tests out of the production profile by
  setting one validated GUID temporary storage root before initialization,
  rejecting a different second root, draining asynchronous logging
  deterministically, and deleting only that exact root after success. The
  broader `Load()`/`Save()` integration and replacement-failure row above
  remains pending.
- [ ] Make native-source ZIP metadata and entry ordering deterministic so
  identical validated content produces a stable archive SHA-256. Until then,
  pin only the final generated release asset through `SHA256SUMS.txt`.
- [ ] Add a Windows CI workflow for network-free build and test gates; keep
  native/runtime download and public release jobs pinned and separately
  authorized.
- [ ] Document which gates are automated and which still require physical
  Windows and iPhone hardware.

Acceptance target: local and CI checks use the same entry point and cannot
publish, sign, or mutate a GitHub Release as a side effect.

### UI accessibility and physical DPI acceptance

- [ ] Make compact help controls keyboard-focusable without turning blank card
  space into a tooltip target.
- [ ] Draw an explicit focus cue and expose the same help text through keyboard
  interaction as through pointer hover.
- [ ] Add physical Windows 10/11 checks for tooltip hit testing, transparent
  compositing, optical centering, and per-monitor DPI transitions.

Acceptance target: pointer, keyboard, and assistive-technology users reach the
same help content, with retained screenshots at representative Windows display
scales.

### Native core IPC and a real ready state

Version 0.11.1 adds explicit DNS-SD/BLE stdout markers, listening-socket
checks, and bounded recovery. This is still a transitional one-way contract:
it is not versioned IPC and cannot provide commands, acknowledgements, or a
fully authoritative AirPlay session state.

Version 0.12.2 adds a managed one-shot discovery renewal after an explicit
lost-client marker and completed native cleanup. It does not replace the
native service registration in place, and it cannot force an iPhone to discard
a stale browse result that never reaches Windows.

Version 0.12.3 adds a managed, memory-only continuity placeholder for a
confirmed fatal loss. It deliberately stays separate from readiness: keeping
the last visible frame on screen does not mean discovery or reconnection has
completed.

Version 0.12.4 preserves UxPlay's same-process/same-port socket recovery after
completed cleanup and adds a compact feedback-health capability/recovered
marker pair. This avoids the shell immediately replacing an already recovered
core, but it is still stdout observation rather than acknowledged IPC or
in-place DNS-SD/BLE re-publication.

Version 0.12.5 makes the existing pre-fatal short-gap continuity path
deterministic: the native three-second warning arms a four-second local
deadline, and recovery before that deadline cancels it. Acknowledged recovery
changes the placeholder to connection-restored/waiting-for-image while the
existing renderer-gated handoff waits for visible video. This does not add a
native ready acknowledgement, re-publish discovery in place, or force an
iPhone to refresh a stale browse result.

Version 0.12.6 keeps the continuity view immediately above the external
renderer without activating it, and fatal cleanup replaces generic waiting
with explicit manual Screen Mirroring reconnect guidance. This improves the
truthfulness and visibility of a failed session; it does not claim that iOS
discovery or automatic reconnect has completed.

The same candidate adds explicit native HTTP initial/reset ready and failed
markers. Same-process recovery is preserved only after current-PID, same-port
reset evidence; failure exits into bounded full-process recovery, and
0.12.6 explicitly forced disconnect after a typed `TEARDOWN`. Source review
shows that this could remove the whole control connection. The affected
physical log proves that the shell/core processes stayed healthy while a
software request removed the connection, but does not identify which native
disconnect call site ran.

Version 0.12.7 removes that unconditional server-side disconnect, retains the
upstream typed-stream teardown behavior, and logs client-managed connection
ownership. This is a focused correction to the typed-`TEARDOWN` path; it does
not complete the unchecked DNS-SD/BLE re-publication work below or claim
automatic reconnection.

The 0.12.8 candidate addresses a separate false presentation handoff observed
after a longer feedback gap. HTTP feedback recovery and a still-visible cached
renderer HWND are not evidence that a new frame was displayed. The candidate
correlates one recovery epoch with the current core PID and managed mirroring
session, treats push/PTS/sink markers as diagnostics, and accepts
only a fresh Direct3D 11 present-path proof for automatic fade. Missing proof
keeps continuity visible and changes waiting text to reconnect guidance. This
does not yet repair the underlying half-open transport, decoder, sink, or clock
condition that may leave long-gap video frozen.

The 0.12.9 release adds a second, strictly bounded managed idle-discovery
allowance. After the existing first ten-minute renewal, a later Windows unlock
may consume one final refresh only after cooldown and only while core, socket,
discovery-marker, physical-IPv4, mirroring, client-grace, restart, and network
guards are safe. This mitigates one missing-after-idle report but does not prove
its cause, preserve a port across process replacement, or replace the native
same-port re-publication work below.

The local 0.12.10 candidate does not change discovery. Safe registration
ownership, an in-process same-port `refreshDiscovery` operation, and an
acknowledged DNS-SD/BLE ready marker remain `DESIGN/NEXT`; the unchecked items
below are not complete.
The local 0.12.11 candidate carries that boundary forward unchanged.

The local 0.12.12 candidate adds one additional managed timed stage 20 minutes
after the first idle renewal. It shares the existing strict two-renewal
allowance with `SessionUnlock`, preserves due work across active mirroring and
client grace, and retains the existing activity/manual/network epoch
boundaries. This is still a reporter-evidence-driven mitigation: startup-ready
markers cannot isolate DNS-SD, BLE, Bonjour browse state, or iOS cache and do
not prove continuous visibility.

The local 0.12.13 candidate implements the first narrow acknowledged command:
automatic idle, eligible unlock, and persistent discovery-health maintenance
prefer a request-correlated refresh of the paired RAOP/AirPlay DNS-SD
generation in the same process and on the same listener ports. Registration
callbacks are pumped on the native owner context; partial pairs roll back and
retry with bounded delays; active clients defer. Unsupported or failed
commands retain bounded full-process fallback. Manual **Restart discovery** and
physical IPv4 change remain full restarts because the separate BLE helper is
unchanged. This is local-registration evidence, not continuous iPhone browse
attestation or an AWDL implementation.

The local 0.12.14 candidate starts the media-liveness audit with one confirmed
source correction and passive evidence. Every video retry now starts from the
same immutable remote timestamp rather than compounding a previously mapped
value. A fixed two-second, content-free health summary separates VCL/config
ingress, appsrc, decode/sink, and Direct3D 11 Present progress without taking
recovery action. Physical frozen-frame reproduction and the exact causal stage
remain pending.

The local 0.12.15 candidate extends that audit across the supported default
native path. Mirror, HTTP, audio RTP, and NTP share explicit worker/join
ownership; socket and parser boundaries are bounded; SETUP publication is
transactional; crypto and allocation failures return status; and renderer/bus
operations retain their owning GStreamer objects. A fully decrypted and NAL-
validated type-0 access unit may request one nonblocking implicit resume while
still being delivered normally. Build, executable lifecycle/crypto, source-
contract, frozen-review, patch/provenance, and two-clean-build gates pass. The
validated source workflow creates a 147-entry archive. Extracted-source
rebuild, staged runtime/loader, managed build/resilience, and live discovery-
pipe gates now pass. The initial exact 13-entry package and x64 Setup gate also
passes with packaged-shell resilience, embedded-input equality, and all three
self-checks. The focused final rebuild against frozen embedded documentation
also passes with exact 13-entry/package-shell/provenance/Setup equality and
fresh-process resilience. Physical visible-recovery evidence remains pending.

The published 0.12.16 review release removes the managed two-renewal exhaustion
point without changing native registration ownership. A fresh idle epoch still
waits ten minutes once; every later terminal result rearms a 20-minute same-
process, same-port DNS-SD refresh. Active mirroring and client grace defer due
work.
Only renewals one and two may use the legacy full-process restart fallback;
later command failure keeps the receiver listening and retries on the recurring
schedule. This is a low-frequency availability policy, not proof that an iPhone
browse cache continuously contains the receiver.

Its managed build, deterministic resilience contracts, live same-PID/same-port
pipe refresh, exact 13-entry review payload, packaged-shell resilience,
versioned corresponding source, x64 Setup, embedded-input equality, and all
three Setup self-checks pass. The annotated tag, normal latest GitHub Release,
four-asset/API-digest set, and fresh public re-download checks also pass.
Installed and physical long-idle acceptance is still pending.

The 0.12.16 installer policy now treats an existing installation as an
unattended update/reinstall transaction after the application-level
confirmation. Existing Start menu and desktop shortcut choices are preserved,
AeroMirror is relaunched, a clean first install remains interactive, and an
automatic downgrade is refused. Deterministic Setup and source-contract checks
pass; a real installed update remains a physical release gate.

The 0.12.19 release adds a read-only assessment for the exact Private-network
Bonjour mDNS firewall rule and an explicit, confirmed, UAC-gated repair limited
to the validated Bonjour executable, UDP 5353, and `LocalSubnet`. It does not
mutate the Bonjour service or wire external-rule removal into uninstall.

The 0.12.19 corrective review adds a separate Windows prerequisite check for
the external Bonjour service. Startup assessment is read-only. Only an exact
missing Private/UDP 5353/LocalSubnet rule for the validated
`mDNSResponder.exe` exposes an explicit repair action; the user must confirm it
and approve Windows UAC. This does not change Bonjour service ownership and is
not evidence that an iPhone currently sees the receiver.

The local 0.12.20 candidate makes that repair visible on the main network card.
Clicking the scoped action is the application confirmation and proceeds to one
Windows UAC prompt; success refreshes discovery without a success modal. An
absent Bonjour service exposes only prerequisite guidance. The headless native
core no longer offers to register its bundled per-user responder as a machine-
wide service.

- [ ] Add a versioned local IPC contract, preferably JSON Lines over a
  per-user Windows named pipe.
- [ ] Emit explicit core states such as `starting`, `mdnsReady`, `ready`,
  `clientConnected`, `streamStarted`, `streamStopped`, `recovering`, and
  `error`.
- [ ] Include stable error codes and a short user-safe explanation with every
  failure event.
- [x] Add a compact native capability and feedback-recovered marker so the
  shell can distinguish acknowledged control recovery from an active gap; the
  current shell schedules its local deadline at four seconds. This marker no
  longer serves as displayed-frame evidence for handoff.
- [x] Implement and automatically verify current-PID/session/recovery-epoch
  presentation proof for continuity handoff. Feedback, appsrc push/PTS, sink
  probes, renderer visibility, cached HWND, and pixel sampling cannot authorize
  fade; physical recovered-frame evidence remains a separate pending gate.
- [x] Add bounded session-correlated VCL/config, appsrc, sink, Present, and
  timestamp progress evidence without content logging or automatic recovery.
- [ ] Diagnose and correct the underlying longer-gap frozen-video path only
  after the new physical trace identifies the boundary. Do not hot-replace a
  half-open socket or auto-reset the receiver without parser/crypto/physical
  tests.
- [x] Add a narrow version-1 `refresh-discovery` command with request,
  generation, PID, RAOP-port, and AirPlay-port correlation; keep it transitional
  redirected stdin/framed stdout rather than presenting it as the complete IPC
  contract above.
- [ ] Extend the narrow command boundary with graceful `shutdown`, `getStatus`,
  and complete session-state events only through the versioned named-pipe
  contract; do not use process presence or renderer-window discovery as a
  substitute for readiness.
- [x] Refactor DNS-SD registration ownership and re-publish the paired RAOP and
  AirPlay records on the same listener ports with current-generation callback
  acknowledgement, active-client deferral, partial-pair rollback, and bounded
  retry.
- [ ] Add safe in-process BLE helper reconfiguration and acknowledgement before
  describing discovery refresh as a same-process DNS-SD-and-BLE operation.
  Until then, manual discovery and physical IPv4 change remain full restarts.
- [x] Keep a guarded post-renewal `SessionUnlock` refresh available after any
  completed idle renewal, with cooldown, readiness/client/network guards, and
  activity re-arming; keep it documented as a mitigation rather than root-cause
  proof.
- [x] Keep timed idle renewal recurring every 20 minutes after stage 1, defer
  safely for active mirroring/client grace, saturate the lifetime counter, and
  limit disruptive legacy process-restart fallback to attempts one and two.
- [ ] Physically validate the long-idle/unlock mitigation on Windows 10 and 11,
  retaining same-PID/port DNS-SD generations, first-tap iPhone browse/request
  evidence, manual full-restart controls, and BLE state. Isolate whether the
  original absence was DNS-SD, BLE, Bonjour service state, iOS browse cache,
  socket/port publication, or another lifecycle boundary.
- [ ] Reproduce the reported Windows 10 first-install-only-after-reboot symptom
  on a clean VM. Retain pre-reboot Setup/receiver logs, Bonjour service state,
  pending-reboot indicators, firewall/network state, and the effect of a
  receiver stop/start. Do not add a generic reboot prompt or machine-wide
  Bonjour mutation without a proven prerequisite and rollback design.
- [x] Diagnose the exact missing Private Bonjour mDNS rule and offer only an
  explicit narrow repair; keep ordinary startup read-only.
- [ ] Physically validate UAC decline/approval, exact rule scope, no duplicate
  rule, and renewed iPhone visibility on a controlled Windows machine.
- [x] Diagnose the exact external Bonjour executable/firewall contract and
  keep any Private UDP 5353 LocalSubnet repair behind explicit confirmation
  and UAC; never add Public, TCP, Any-address, or automatic startup rules.
- [ ] Physically validate missing-rule detection, declined UAC, accepted
  repair, exact resulting rule scope, iPhone rediscovery, and behavior after
  reboot without treating local rule presence as browse attestation.
- [x] Skip the shortcut/launch option form for updates and same-version
  reinstalls, preserve the installed shortcut state, relaunch after success,
  and retain an explicit downgrade boundary.
- [x] Treat **Download and install** as the sole application confirmation and
  launch the exact digest-verified Setup without a second Yes/No.
- [ ] Run a real installed update and same-version reinstall, retaining Setup
  logs and verifying shortcut absence/presence, settings, identity, autostart,
  relaunch, and rollback on a controlled failure.
- [ ] Give every core launch and AirPlay session a correlation ID so shell and
  native logs can be matched.
- [ ] Define protocol version negotiation so an older shell can fail safely
  with a newer core and vice versa.

Acceptance target: the UI says “ready” only after the native core confirms
that its network advertisement and listening services are active.

### Native core audit and staged refactor

The 0.12.13 discovery work corrected one ownership/lifecycle boundary but is
not a full audit of the inherited `uxplay-windows`/libuxplay engine. Treat the
reported disconnects, Bonjour gaps, renderer behavior, and Windows resource
issues as possibly sharing deeper state-machine or ownership faults. Audit
first; do not combine a broad rewrite with Photos, AWDL, or viewer UI changes.

- [ ] Freeze one exact native baseline and map every AeroMirror patch hunk to
  its upstream owner, thread/context, lifetime, failure path, and automated or
  physical evidence. Keep the wrapper and libuxplay findings separate.
- [ ] Audit receiver/session state transitions from startup through DNS-SD,
  HTTP/RTSP setup, PIN/pairing, audio/video/mirroring, typed teardown, feedback
  loss, reset, reconnect, normal shutdown, and rapid restart. Identify every
  process-global, session-global, and connection-owned field explicitly.
- [ ] Audit allocation and cleanup symmetry for Bonjour refs/TXT records,
  GLib sources/contexts, sockets, timers, RAOP connections, RTP buffers,
  GStreamer objects, renderer hooks, BLE process/pipe handles, Qt objects, and
  Windows threads/handles. Exercise failure between each acquire/release pair.
- [ ] Audit cross-thread and callback safety: owner-context rules, atomics and
  lock ordering, callback-after-free risk, stop/join ordering, process-lifetime
  state that must reset, and commands admitted during internal loop recovery.
- [x] Fix the confirmed natural mirror-worker exit/join gap: a worker can set
  `running=false` before its callback tail is joined, allowing a restart wedge,
  leaked thread handle, or a narrow destroy/tail lifetime race. Add explicit
  join ownership plus natural-exit, socket-loss, rapid-restart, and
  unsupported-codec tests in a focused patch.
- [x] Apply the same explicit lifecycle helper to mirror, HTTP, audio RTP, and
  NTP workers. Cover create rollback, natural exit with join debt, one
  concurrent join owner, self-stop deferral, restart exclusion, and terminal
  join failure with the exact production helper and eight executable cases.
- [x] Bound the supported default mirror/HTTP/SETUP/pairing/RTP/NTP/metadata/
  buffer/crypto inputs and make partial setup transactional. Keep hostile-input
  validation source-bound; do not turn the release gate into iterative exploit
  reproduction.
- [x] Make video/audio renderer bus ownership explicit and retain GStreamer
  objects across unlocked work and callback teardown. Permit a valid-frame
  implicit resume only after complete decrypt/NAL validation, deliver the same
  access unit, and avoid leaky appsrc drop semantics.
- [x] Materialize the exact patch/provenance set and reproduce one reviewed
  executable in two clean compatible builds; validate the generated 147-entry
  corresponding-source archive and all 37 libuxplay/41 total patched-source
  hashes.
- [x] Rebuild the core from the no-Git extracted prepared source, inspect the
  staged runtime and loader, and pass managed build/resilience plus live
  same-PID/same-port discovery-pipe verification.
- [x] Complete the initial exact 13-entry review-payload and x64 Setup gate,
  including packaged-shell resilience, byte-exact embedded payload, and all
  three Setup self-checks.
- [x] Run the focused final package/Setup rebuild after embedded documentation
  freezes; repeat exact entry, shell equality, resilience, embedded provenance,
  version, and all three Setup self-check gates.
- [ ] Complete physical freeze/lifecycle acceptance for 0.12.15.
- [ ] Resolve the accepted P2 native backlog: define parent lifetime after a
  terminal platform join failure; broaden audio and optional HLS
  synchronization review; replace remaining startup assertions with checked
  rollback where practical; deepen optional PIN/SRP coverage; and consolidate
  the currently tolerant dual teardown paths under one authoritative owner.
- [ ] Audit protocol/error semantics against the pinned upstream source:
  request parsing, authentication/pairing, connection ownership, `TEARDOWN`,
  partial HTTP/reset failure, retry/backoff, media bus errors, and distinctions
  between clean disconnect, recoverable loss, and fatal state.
- [ ] Audit Windows-specific assumptions: Bonjour service/DLL lifecycle,
  UTF-8/UTF-16 boundaries, pipe framing, socket and handle widths, power and
  network transitions, long paths, firewall/profile behavior, and Win10 1809.
- [ ] Turn each confirmed issue into a focused patch with a deterministic
  replay or fault-injection test, two clean reproducible builds, prepared-
  source rebuild, runtime/loader checks, and a physical regression row. Avoid
  speculative fixes that change protocol behavior without evidence.
- [ ] Decide only after the audit whether to retain the pinned fork, update
  upstream, or extract a smaller dedicated `receiver-core.exe`. Any migration
  needs a patch/provenance map, IPC compatibility plan, rollback, and physical
  comparison before replacing the current core.

Acceptance target: every native lifecycle state and owned resource has one
documented owner and cleanup path; confirmed P0/P1/P2 findings have focused
tests and fixes; unchanged behavior remains reproducible from corresponding
source; and no physical reliability claim rests on source inspection alone.

### Crash diagnostics

- [x] Capture the native core's standard output and standard error in a
  rotating local log.
- [x] Add top-level exception reporting for the Windows shell and termination
  metadata for the native core.
- [ ] Produce opt-in minidumps for unexpected crashes using Windows Error
  Reporting LocalDumps or `MiniDumpWriteDump`.
- [ ] Extend the existing application/core version, Windows, renderer, and
  lifecycle logging with GPU details and explicit AirPlay session phases,
  without recording mirrored content.
- [ ] Add a “Create diagnostic package” action with preview, redaction, size
  limit, and explicit user consent before sharing.
- [ ] Never include a PIN, receiver key, trusted-client register, media
  payload, or unrelated personal files in a diagnostic package.
- [x] Rotate the log at 5 MB and retain one previous log file.

Acceptance target: a tester can report a crash with its exact time and attach
one redacted package that identifies whether the shell, native core, decoder,
renderer, or discovery layer failed.

## Streaming experience

### Live quality switching

The current quality values are advertised to the iPhone during AirPlay session
setup. Changing a selector in the Windows shell cannot by itself change the
already incoming encoded stream.

- [ ] Research whether the active AirPlay session supports safe display
  capability renegotiation with current iOS versions.
- [ ] Add an IPC command and acknowledgement only if the native core can
  perform a real renegotiation.
- [ ] Preserve the current preset and explain when a reconnect is required if
  the sender rejects a live change.
- [ ] Avoid silently rescaling the renderer and presenting that as a quality
  change.
- [ ] Test resolution, codec, frame rate, latency, audio continuity, and
  fallback behavior on multiple iPhone generations.

Acceptance target: the UI reports the quality actually acknowledged by the
sender, not merely the requested preset.

### Audio endpoint resilience

Version 0.12.7 makes the normal Windows audio path explicit:
`wasapi2sink continue-on-error=true`. The pinned redistributed GStreamer 1.28.1
runtime supports that property for its documented endpoint-open, device-I/O,
and device-removal failures. Muted output remains separate, and advanced
UxPlay arguments can still replace the managed sink deliberately. This does
not isolate every possible GStreamer bus error or change the shared native
media-pipeline boundary.

- [x] Select the resilient `wasapi2sink` property for normal default audio
  without changing mute or advanced-argument precedence.
- [ ] Physically test playback while changing the Windows default output,
  disabling/re-enabling the active endpoint, and removing a USB/Bluetooth
  endpoint where available.
- [ ] Define session-scoped handling for media errors outside the documented
  `wasapi2sink` device-failure scope before treating every audio bus error as
  nonfatal; do not use a process-lifetime failed latch that can mute later
  sessions.
- [ ] Retain timestamped native markers that distinguish the first media error
  from later teardown side effects without logging audio content.

Acceptance target: a supported Windows output-device failure may interrupt
audio but does not unnecessarily end video or permanently mute later sessions.

### Orientation and photo/video sizing

Version 0.12.3 extends the interim path: a newly discovered renderer receives a
provisional fit, and an early marker with a conservative phone shape is retained
before the stable-size debounce. This covers the observed `998x2160` then
`3840x2160` direct-in-Photos startup sequence. Later normalized ratios within
`0.03` follow physical rotation, while a different media-canvas ratio retains
the learned orientation. Interactive resize completion restores the learned
proportions by default, and normal renderer bounds/DPI persist across sessions
with work-area clamping. The unchecked work below covers richer metadata,
versioned IPC, sessions without any phone-shaped marker, inner encoded-canvas
sizing, device validation, and edge cases still required before this behavior
is considered complete.

Version 0.12.4 adds the complete raw AirPlay geometry header, including the
previously ignored auxiliary pair, and the actual selected GStreamer decoder
and sink. These are diagnostic fields only. The auxiliary pair has not been
validated as a content rectangle, pixel-aspect ratio, or rotation signal and
is not consumed as one.

Version 0.12.5 correlates that raw record with the following encoded-size
record and treats only the complete observed
`3840x2160 aux=0x0 encoded=3840x2160` Photos signature as ambiguous. It cannot
seed or persist a false device orientation; a later phone-shaped marker can
establish portrait in the same session. This is a narrow replay-backed rule,
not a generic interpretation of the auxiliary pair, and it does not crop the
small photo and black bars already encoded inside the canvas.

Version 0.12.6 clears the unimplemented photo, slideshow, and photo-preload
AirPlay feature bits as a mirror-only negotiation experiment. Its effect on
direct-in-Photos startup and inner canvas sizing is physically pending. It
does not provide a content rectangle or justify crop/zoom heuristics.

Version 0.12.9 settings schema 12 adds a default-off A/B that temporarily lets
only the exact recorded ambiguous Photos/media canvas drive the outer window,
similar to the older wide-window presentation. It does not promote that canvas
to device orientation, persist an automatic provisional landscape, modify the
native stream, or enlarge inner media. Its value is specifically to separate
an outer-window regression from the still-unsolved encoded-inner-canvas case.

The local 0.12.10 candidate gives every correlated geometry/size record a
monotonic core-lifetime sequence. Repeated identical pending candidates retain
the original 350 ms deadline rather than starving the debounce, while stable
duplicates do not reopen it. Applied fits now track target class plus exact
aspect, so same-orientation class/aspect changes refit once and scaled
same-class/same-aspect markers do not move the window again. Physical
Windows/iPhone validation remains pending.

The local 0.12.11 candidate retires the Photos-specific schema-12 checkbox and
persisted field. Only the complete recorded `3840x2160 aux=0x0
encoded=3840x2160` signature automatically becomes a provisional outer-window
`MediaCanvas` target. Legacy key values are ignored and omitted on save. The
canvas still cannot seed trusted orientation or make automatic placement
persistable, and the patch does not crop, zoom, or enlarge inner media.

The 0.12.17 review release adds explicit presentation controls without
claiming content detection. Native property-backed fullscreen affects the outer
renderer. Uniform 100–250% zoom is available only for the exact recorded Photos
media canvas, is not persisted, and resets on media-class/session boundaries.
It may crop bars, UI, or genuine image content and therefore remains opt-in.

The 0.12.18 review candidate replaces those incremental zoom controls with
one deterministic layout. The exact Photos canvas keeps the trusted device
shape; a direct-in-Photos session gets a non-authoritative portrait fallback.
Only a portrait target receives centered uniform fill. Actual fullscreen
suspends every shell fit/restore/persist path, uses 100% scale, and exits through
foreground Esc or native Alt+Enter before the normal-window fill is restored.
The versioned corresponding-source archive/extracted rebuild, exact 13-entry
review package, packaged-shell resilience, x64 Setup, embedded-input equality,
and all three non-installing Setup self-checks pass. Physical iPhone behavior
and a real installed update remain pending. The normal updater-visible
`v0.12.18` Release, exact four assets, checksums/API digests, canonical/legacy
latest routes, and fresh public re-download equality pass without converting
those pending physical rows into PASS.

The 0.12.19 corrective review removes the 0.12.18 cover scale after physical
testing showed lost image edges. The exact Photos canvas can still select a
portrait outer-window target, but presentation remains at neutral 100% so the
complete transport frame is contained. A shell-owned non-activating control
adds direct fullscreen entry/exit near the renderer chrome, and foreground
Escape is captured as an event only while actual fullscreen is active. A
  stale non-monitor borderless transition keeps the explicit exit control
  available without rewriting foreign window styles or sending a speculative
  second toggle; normal framed Alt+Enter exit is not changed. Physical
  Photos/button/Escape/transition acceptance remains pending.

Annotated `v0.12.19`, normal latest Release `375664260`, its exact four
assets, API digests, checksums, canonical/legacy latest routes, and fresh
public download equality pass. Installed behavior and the physical rows above
remain pending; published tag/assets are immutable.

The local 0.12.20 candidate removes the shell-owned overlay and global keyboard
hook. One native Qt viewer owns the framed window and child GStreamer surface;
caption fullscreen, Escape, Alt+Enter, and the shell's exact state request use
one GUI-thread setter with explicit acknowledgements. The sink keeps aspect-
ratio containment, neutral scale, and no crop/render rectangle. This preserves
the portrait outer-window request but can produce substantial letterboxing for
the full `3840x2160` Photos canvas. Caption Close intentionally minimizes the
active viewer rather than clearing its renderer-generation visibility; the
explicit tray restore action returns that visible minimized HWND even when the
taskbar option uses `WS_EX_TOOLWINDOW`. Physical size, edge, minimize/close,
and both taskbar-policy paths remain pending; content-aware enlargement still
requires trustworthy bounds. Two clean native builds, extracted corresponding-
source rebuild, package, and non-installing Setup gates pass.

- [ ] Log source dimensions, pixel aspect ratio, rotation metadata, and
  renderer dimensions for orientation transitions.
- [x] Suppress a different Photos/media canvas ratio after a device-frame
  baseline has been learned for the current session.
- [x] Retain an early phone-shaped raw marker before the debounce so the known
  direct-in-Photos startup sequence cannot lose its device baseline.
- [x] Prevent the exact replayed Photos-first `3840x2160 aux=0x0` canvas from
  becoming the device baseline or replacing a valid saved placement before a
  trustworthy frame or explicit user action exists.
- [x] Retire the schema-12 exact-canvas A/B and apply only that exact signature
  automatically without a native restart, trusted-orientation promotion, or
  provisional-placement save.
- [x] Make geometry debounce non-starving with a monotonic core-lifetime event
  sequence, retaining the first deadline across identical pending candidates
  and preserving reset/session invariants.
- [x] Refit on a fresh target-class or exact-aspect change, including
  same-orientation transitions, while consuming scaled exact-aspect duplicates
  without repeated movement.
- [ ] Run the same photo/video from clean and legacy `FollowPhotosMediaCanvas`
  profiles, verify identical automatic behavior and canonical key removal,
  measure outer client bounds and inner visible content separately, and retain
  direct-Photos startup/session-stability evidence. Do not call a wider outer
  window an inner-content fix.
- [ ] Resolve sessions that start with only a generic media canvas and no early
  phone-shaped marker, without guessing that the canvas is the physical device
  ratio.
- [ ] Pass source-size/orientation events through native IPC.
- [x] Add focus-independent native fullscreen and a bounded equal-X/Y scale
  command owned by the renderer's GLib context.
- [x] Detect actual monitor-sized borderless fullscreen, suspend every managed
  fit/restore/save path while active, and support foreground Esc without
  removing native Alt+Enter.
- [x] Replace incremental manual zoom with one exact-canvas portrait-fill
  calculation, preserving a trusted landscape target and resetting scale in
  fullscreen, on class exit, and at renderer-session start.
- [x] Remove the unverified portrait-fill scale after physical cropping and
  keep the complete Photos transport frame contained at neutral scale until a
  trustworthy content rectangle exists.
- [x] Add a shell-owned non-activating fullscreen control and event-driven
  foreground Escape path with bounded hook lifetime and unchanged Alt+Enter.
- [x] Replace that interim overlay/hook with one native framed viewer, embedded
  video surface, standard caption fullscreen action, deterministic Escape, and
  an acknowledged idempotent state setter shared with Alt+Enter and tray.
- [ ] Expose a trustworthy content rectangle/crop signal or validate a
  conservative pixel-analysis design for Photos canvases that contain their
  own encoded black bars; do not crop real dark content by guesswork.
- [ ] Emit the exact display-capability response selected for each new session
  and retain a physical HEVC 4K-versus-1080p Photos A/B. Treat that marker as
  negotiation evidence only; feature bits remain unchanged, and neither preset
  is a fix unless the visible inner-media measurement improves repeatedly.
- [ ] Resize or letterbox the viewer without cropping content, repeatedly
  shrinking the window, or creating a resize feedback loop.
- [ ] Respect iPhone orientation lock: AeroMirror should follow the stream
  metadata it receives rather than guessing from the displayed image.
- [x] Keep a user option for automatic fitting and preserve an explicit opt-out
  while snapping proportions only after an interactive resize completes.
- [ ] Test Photos, fullscreen video, Camera, home-screen rotation, and rapid
  portrait/landscape transitions on displays with different DPI scaling,
  including repeated tray/Alt+Enter/Esc exits and entry/exit from Photos while
  fullscreen.
- [ ] With **Show stream in taskbar** both enabled and disabled, press Caption
  Close during normal and fullscreen playback, verify that the stream remains
  alive, restore it through the taskbar or **Show stream window** as applicable,
  then confirm stop hides it and the next session shows exactly once.

Acceptance target: photos and videos keep their correct proportions and remain
legible while the viewer changes orientation only when the incoming stream
does.

### Frame pacing and viewer ownership

Version 0.12.4 makes two conservative experiments reviewable: Interactive
latency now applies only `-vsync no`, and explicit Direct3D choices pin matching
decoders and sinks. It also caches unchanged foreign-window policy and applies
saved placement from the early show event. These changes do not turn the
external GStreamer HWND into an AeroMirror-owned viewer.

Version 0.12.6 makes the pinned Direct3D 11 decoder and sink the stability
default. Settings schema 11 migrates legacy automatic selection while
preserving explicit Direct3D 12 as an experimental opt-in. The change is
covered by managed migration and argument tests, but physical Direct3D 11
versus Direct3D 12 Photos and resolution-change evidence remains required.

Version 0.12.7 corrects the headless wrapper boundary so an external `--uxplay`
launch preserves the shell's `-vs` and `-fs` values. This makes the existing
D3D11 default reach UxPlay instead of being silently replaced by the wrapper's
persisted Qt preference. Physical actual-sink and Photos transition evidence
is still required.

The 0.12.8 candidate adds recovery-stage telemetry around video push, target
PTS/sink observation, and the Direct3D 11 present path. Only the correlated
present proof is presentation-ready; earlier markers locate a stall but cannot
close continuity. Direct3D 12 and other advanced sinks require an equivalent
reviewed proof before they can use automatic handoff. Interactive `-vsync no`
deliberately skips synchronized PTS/Present proof in this candidate and retains
reconnect guidance.

- [x] Remove the aggressive 50 ms audio-buffer request from the Interactive
  profile while keeping Balanced as the default.
- [x] Pin both decoder and video sink for explicit Direct3D 11/12 comparisons.
- [ ] Add bounded frame-pacing, decoder QoS/drop, packet-jitter, and render-time
  telemetry that does not contain mirrored content.
- [ ] Retain physical evidence that a correlated D3D11 present marker coincides
  with real recovered video after both short and longer gaps; a successful
  appsrc push or matching PTS is not sufficient acceptance.
- [ ] Benchmark Balanced and Interactive on identical local-network conditions
  across Windows 10/11 and representative GPUs before changing the default.
- [ ] Embed or parent a native D3D surface behind a versioned local IPC contract
  before promising a Mac-style hover-only frame, borderless viewer, seamless
  frozen-frame handoff, or live aspect lock during edge dragging.
- [ ] Define keyboard-accessible move, close, fullscreen, and size controls for
  any hover-only chrome; do not remove the standard frame before those controls
  exist.

Acceptance target: physical evidence distinguishes transport jitter, decode,
and presentation pacing, while viewer chrome and resizing remain accessible
and never require unsafe cross-process window subclassing.

## Future capabilities

### Remote taps and swipes

Standard AirPlay screen mirroring is a receiver/output path and does not expose
a documented, general-purpose channel for injecting taps or swipes into iOS.
Apple's iPhone Mirroring integration on macOS should not be assumed to be part
of public AirPlay; it relies on Apple-controlled platform integration and
private capabilities.

- [ ] Treat arbitrary iPhone control from Windows as research, not an MVP
  commitment.
- [ ] Verify current public Apple APIs, iOS security boundaries, App Store
  rules, accessibility constraints, and device-pairing requirements before
  designing a control channel.
- [ ] Do not simulate support by drawing a pointer over the mirrored video.
- [ ] If a companion iOS app is considered, clearly document that it could
  control only content and actions exposed by that companion app, not the
  iOS home screen or arbitrary third-party apps.
- [ ] Consider limited receiver-side controls—mute, window sizing, pause where
  the sender protocol supports it—separately from iOS input injection.

Acceptance target: no control feature is advertised unless it performs a real,
authorized action on supported iOS versions and has a clear security model.

### AirDrop-like file transfer

AirDrop is separate from AirPlay and combines discovery, peer-to-peer
networking, identity, and encrypted transfer behavior. It must not be treated
as a small extension of screen mirroring.

- [ ] Research AWDL/Bluetooth/Wi-Fi hardware and driver constraints on
  supported Windows devices.
- [ ] Review protocol implementations and licenses independently from the
  GPL-licensed receiver stack.
- [ ] Define threat modeling, sender confirmation, destination selection,
  quarantine/Mark-of-the-Web behavior, duplicate names, and transfer limits.
- [ ] Evaluate a standards-based or companion-app transfer mode as a separate
  product path if native AirDrop interoperability is not reliable.
- [ ] Prototype a separately named **AeroDrop** path only after defining its
  trust model: an iOS Share Extension or companion can transfer over an
  authenticated local HTTPS session, default to `Downloads\AeroMirror`, show
  an explicit accept/open-folder notification, sanitize and deduplicate names,
  apply size limits, and never auto-open executable/archive content.
- [ ] Keep genuine AirDrop/AWDL interoperability as a separate feasibility
  track; appearing as an AirPlay/Apple TV receiver does not make AeroMirror an
  AirDrop target.
- [ ] Keep file transfer isolated from the receiver process and its firewall
  surface.

Acceptance target: transfers are encrypted, explicitly accepted, safely
stored, and tested on representative Wi-Fi/Bluetooth chipsets.

### Smart TV receiver research

Commercial Smart TV receivers are useful behavioral references, but their
firmware and proprietary applications are not reusable source code.

- [ ] Build a comparison matrix of connection time, discovery reliability,
  orientation changes, photo/video sizing, codec negotiation, buffering,
  reconnect behavior, and network changes.
- [ ] Prefer published standards, vendor documentation, and license-compatible
  open-source receiver implementations.
- [ ] Use black-box interoperability observations only where legally
  appropriate; do not copy proprietary firmware, keys, certificates, or
  decompiled code.
- [ ] Separate behavior caused by AirPlay negotiation from TV-specific
  hardware decoders, display pipelines, and vendor optimizations.
- [ ] Turn every useful observation into a reproducible test before changing
  AeroMirror.

Acceptance target: the research produces source-linked hypotheses and
repeatable tests, not claims based only on subjective visual comparison.

## Release gates for these items

Before any roadmap item is marked complete:

1. document its supported Windows and iOS versions;
2. add a failure and rollback path;
3. add automated tests where the boundary can be simulated;
4. complete manual interoperability tests on at least two physical PCs and
   two iPhone/iOS combinations;
5. update the privacy, security, third-party notice, and release-note
   documentation where applicable.

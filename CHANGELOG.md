# Changelog

## 0.12.22 — automatic discovery recovery, first-device trust, and safer updates (review candidate)

### Changes

- Removes Bonjour repair and discovery-restart controls from the main window
  and tray. Normal operation remains a background receiver: the shell monitors
  discovery health without elevation and republishes AirPlay automatically
  after the validated Apple Bonjour service returns to `Running`.
- Makes Bonjour resilience an installer responsibility. After the per-user
  application transaction commits, Setup best-effort configures only an exact,
  safely installed Apple Bonjour service for Automatic start, bounded Windows
  service-recovery actions, and one Private/UDP 5353/LocalSubnet firewall rule.
  A declined or failed administrator step does not roll back the application.
- Treats DNS-SD error `-65563` as an unavailable prerequisite instead of
  repeatedly restarting the receiver. BLE cannot create a false ready state,
  and a recovered service receives at most two same-process DNS-SD submissions
  on the existing AirPlay ports before another health event is required.
- Replaces fixed/no-PIN choices with per-device trust. An unknown iPhone gets a
  fresh cryptographic four-digit code in a high-contrast fullscreen PC overlay;
  the code is delivered to the current native request through redirected stdin,
  never through the process command line, and is not written to logs. Trusted
  devices reconnect without another code until the user resets trust.
- Makes pairing cancellation authoritative at the native admission boundary.
  Timeout, Escape, disconnect, malformed setup, a stale request, or a verified
  key mismatch rejects that SETUP instead of allowing registration to continue.
- Makes cancellation and Settings trust revocation durable across a native
  persistence race. A pending-reset marker blocks replacement core startup;
  after confirmed native exit, AeroMirror atomically empties the trust store
  before clearing the marker or permitting a restart.
- Separates machine-readable `AEROMIRROR_*` markers from ordinary native logs.
  Ordinary client/HLS metadata is flattened to one line, control bytes and
  marker tokens are neutralized, raw client identifiers are not logged, and the
  shell accepts only exact anchored control-line grammars.
- Hardens inherited HLS/gallery parsing: HTTP header names are matched without
  regard to ASCII case, language playlists use bounded checked parsing, URI
  replacement handles shorter and zero-match prefixes, media-URI terminators
  and condensed chunks stay within their own playlist lines, expanded output
  is capped at 32 MiB, and malformed fields or allocation failures reject the
  request instead of terminating the receiver.
- Keeps fullscreen inside the native renderer window. The normal caption
  maximize action and Alt+Enter enter the borderless monitor-sized view; Escape
  exits it and restores the saved normal geometry. No delayed shell-owned
  floating fullscreen button or keyboard hook is used.
- Adds optional automatic updates, disabled by default. When enabled, AeroMirror
  checks the fixed public repository, downloads only the exact versioned Setup
  through validated HTTPS redirects, enforces size and SHA-256 checks, protects
  the staged manifest for the current Windows user, and starts Setup only at a
  later safe application launch without interrupting the active receiver.
- Serializes 0.12.22-and-newer Setup transactions for the current Windows user.
  Version and recovery decisions are revalidated under the same lock, the
  primary installed executable is authoritative over stale registry/legacy
  metadata, and recovery never launches from a tree another current Setup is
  still replacing.
- Reworks the README opening and FAQ around the actual product value,
  first-device trust, Bonjour, privacy, supported Windows versions, updates,
  and the explicit absence of remote control, AirDrop, accounts, ads, and
  telemetry.

### Evidence and status

- Managed build and focused Bonjour, automatic-update, pairing, fullscreen, and
  native contract checks pass. Two clean native builds and the extracted
  no-Git corresponding source reproduce core SHA-256
  `E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6`.
  Runtime staging inspects 200 binaries, copies 148 dependency DLLs, resolves
  all 44 requested GStreamer features to 27 plug-ins, and passes isolated
  self-tests from ASCII and Cyrillic paths. The 149-entry corresponding-source
  archive rebuild, exact review payload, x64 Setup, embedded-input equality,
  and all four non-installing Setup self-checks pass. Exact-tag publication and
  public-asset re-download checks remain pre-publication gates. Source packaging
  tolerates only canonical LF/CRLF patch-index metadata differences while still
  pinning every reviewed patch and packaged modified/protected source by hash.
- Installed Windows 10/11 behavior, first/second-device pairing, iPhone
  visibility after idle/service recovery, fullscreen keys, and automatic-update
  handoff remain physical-test items until recorded in the 0.12.22 test plan.
- Concurrent current Setup invocations are guarded, but immutable pre-0.12.22
  Setup binaries cannot join that mutex and must not be deliberately run at the
  same time as a current transaction.
- Public `v0.12.20` and its four assets remain immutable and updater-visible
  latest until 0.12.22 is published. No public 0.12.21 tag or Release exists;
  that candidate was superseded by 0.12.22 and must not be reconstructed.

## 0.12.21 — stopped Bonjour recovery (unpublished, superseded candidate)

0.12.21 established the native stopped-Bonjour prerequisite state and bounded
same-process recovery contract, but its visible **Start Bonjour**/`sc.exe` flow
was not published. The user rejected that manual main/tray action, so 0.12.22
supersedes the candidate with installer-owned machine configuration and
read-only automatic runtime recovery. No `v0.12.21` tag or GitHub Release may
be reconstructed from this internal history.

## 0.12.20 — native fullscreen ownership and quieter repair flow (review release)

### Changes

- Replaced the delayed shell-owned floating fullscreen button with one native
  viewer window. Its standard caption control, Escape, Alt+Enter, and the tray
  action share one idempotent fullscreen setter and one acknowledged state.
- Kept Caption Close as a minimize-equivalent for an active stream. The
  minimized viewer remains recoverable through the taskbar or the explicit
  **Show stream window** tray action, including when the optional taskbar entry
  is disabled. Fullscreen entry from that minimized state preserves the latest
  normal position, while a stale fullscreen request after session stop cannot
  resurrect an empty viewer; session stop still hides it before the next
  stream.
- Embedded the GStreamer video surface in that viewer and explicitly retained
  aspect-ratio containment. AeroMirror does not set a crop rectangle; the
  exact Photos canvas remains at neutral 100% scale and may be letterboxed
  until AirPlay exposes trustworthy content bounds.
- Removed the second application confirmation after a verified update has
  downloaded. The original **Download and install** click remains the user's
  decision. Existing unsaved settings are resolved before download, navigation
  is locked during the handoff, and Setup still verifies its exact asset and
  performs the existing transactional update.
- Moved the exact Bonjour firewall prerequisite to the main network card. One
  click starts the narrow UAC-gated Private/UDP 5353/LocalSubnet repair without
  another application dialog, then refreshes discovery without a success
  modal. The assessment expires after two minutes and is refreshed on receiver
  start, restart, and manual discovery refresh. An unavailable or incorrectly
  installed Bonjour prerequisite is reported accurately but never replaced by
  a user-writable bundled system service.
- Fixed native runtime discovery under Unicode application paths. GStreamer,
  scanner, GIO, font, and PATH values now use the wide Windows environment API
  and fail closed if Windows rejects any value. Fresh-registry `--self-test`
  passes through both ASCII and Cyrillic application paths. Setup keeps its
  pinned-runtime loader compatibility check before installation; the broader
  self-test remains a staged-bundle gate because the thin network installer
  intentionally uses the upstream runtime layout.

### Evidence and status

- Source targets `0.12.20`/`0.12.20.0`. Managed build, complete resilience,
  focused Bonjour, native-host/core/lifecycle contracts, two clean 57/57
  native builds, staged-runtime loader, corresponding-source extraction and
  rebuild, the 13-entry review payload, and all three non-installing Setup
  self-checks pass.
- Physical Photos containment, native titlebar/Escape/DPI behavior, Caption
  Close/minimize under both taskbar policies, installed update, UAC repair, and
  long-idle iPhone visibility remain PENDING.
- Annotated tag `v0.12.20` and normal GitHub Release `376224221` are public
  with exactly four assets. Exact-tag packaging, API digests, both latest
  routes, and fresh public-download equality pass; the immutable `v0.12.19`
  assets were not replaced.

## 0.12.19 — non-cropping gallery and accessible fullscreen (review release)

### Changes

- Replaced the unverified 3.85x Photos cover transform with a strict
  non-cropping presentation. The exact observed canvas may still retain the
  trusted portrait outer-window shape, but it no longer authorizes discarding
  pixels without a real content rectangle.
- Added a managed fullscreen button that follows the native renderer title
  bar without cross-process subclassing. The bounded keyboard hook exists only
  during actual fullscreen, and Escape capture additionally requires the
  renderer PID/root to own foreground; tray and native Alt+Enter remain.
- Added focused Bonjour firewall diagnostics for a confirmed Windows case in
  which the core, ports, DNS-SD renewals, BLE, and service stayed healthy while
  the Private profile lacked an inbound mDNS rule for the Bonjour executable.
  Repair is explicit and UAC-gated, with only Private/UDP 5353/LocalSubnet
  scope for that exact executable.
- Centralized renderer geometry invariants in a typed policy, removed a
  duplicated scale literal, a redundant updater filename filter, unused
  imports, an unreachable network-start branch, and localized-text parsing
  from receiver readiness.

### Evidence and status

- Source targets `0.12.19`/`0.12.19.0`; automated, exact-tag packaging,
  Setup, and corresponding-source gates pass.
- Annotated `v0.12.19` and normal latest Release `375664260` publish the
  exact four-asset set. API digests, checksums, canonical/legacy latest routes,
  and fresh public re-download equality pass.
- The delivered native core/runtime and their pinned source inputs remain
  byte-identical to 0.12.18; the versioned corresponding-source ZIP has its
  own 0.12.19 archive identity. This patch changes the managed shell and
  installer-facing diagnostics only.
- Physical Photos, fullscreen/button/Escape, Private-firewall repair, and
  long-idle iPhone visibility remain governed by
  [`docs/releases/0.12.19/TEST_PLAN.md`](docs/releases/0.12.19/TEST_PLAN.md).
  Publication must label those rows PENDING rather than accepted.

## 0.12.18 — automatic Photos layout and safe fullscreen (review release)

### Changes

- Removed the incremental **Увеличить фото**, **Уменьшить фото**, and
  **Сбросить увеличение** controls. For the exact observed Photos
  `3840x2160 aux=0x0 encoded=3840x2160` transport canvas, AeroMirror now keeps
  the last trusted portrait phone shape (or a conservative portrait fallback)
  and applies one centered uniform fill automatically.
- Added actual fullscreen-state detection around the native D3D11 window.
  While fullscreen is active the shell no longer restores, fits, resizes,
  remembers, or persists the foreign renderer window. This prevents a Photos
  geometry transition from leaving a borderless but movable-state-less window.
- Added **Esc** exit when the fullscreen renderer owns foreground focus.
  Alt+Enter remains native behavior. Entering fullscreen temporarily restores
  100% presentation scale; leaving it reapplies the automatic portrait fill.
- Extended the existing native uniform-scale command from 100–250% to
  100–500%. The command still runs on the GLib owner, uses equal X/Y scale,
  resets at renderer start, and does not inspect pixels or rewrite media.

### Evidence and status

- Managed x64 Release, complete `ReceiverResilience`, the eight-case native
  worker harness, native core contracts, and the production crypto check pass.
- Two clean compatible 57/57 builds reproduce core SHA-256
  `C217386CBC916F8889A9C03774390FE7EC7D8C7EE0B6F64358215CACEEB35118`.
  Runtime staging (199 binaries, 148 DLLs, 44 features to 27 plug-ins) and the
  loader test pass.
- Wrapper patch SHA-256 remains
  `8F48A4E72D765B0549119BC6366CB970384BAB8116B4430CE60ED67228213F9C`;
  libuxplay patch SHA-256 is
  `11330A0D905CF4480958DAA59B950F3A2CE2B4AD51A18563EBCC77924DD782C4`.
- Source targets `0.12.18`/`0.12.18.0`. The final 147-entry corresponding-
  source ZIP is 829,835 bytes with SHA-256
  `F4B7F53CABB67E45E6497A8109A87841ED7FE06DBE3409F0E1EC95FF06EFDDFE`;
  its extracted no-Git tree verifies every pinned hash and rebuilds 57/57 to
  the same core. The tagged 13-entry package, x64 Setup and all three
  self-checks pass. Annotated `v0.12.18`, normal latest Release `373984443`,
  all four API digests, canonical/legacy latest routes, and fresh public
  re-download equality pass. Installed update and physical
  Photos/fullscreen/rotation remain pending; all `v0.12.17` assets stay
  immutable.

## 0.12.17 — Photos presentation controls (review release)

### Changes

- Added a tray presentation submenu for the active stream window. Fullscreen
  now toggles the selected Direct3D 11 sink through its native property instead
  of depending on window focus or simulated Alt+Enter input.
- Added explicit 100–250% uniform Photos zoom. It is available only for the
  exact observed `3840x2160 aux=0x0 encoded=3840x2160` presentation canvas,
  is not persisted, and resets when that canvas ends or a new renderer session
  starts.
- Serialized presentation and discovery writes in the managed shell, narrowed
  the wrapper command grammar, and marshalled renderer changes to the native
  GLib owner. Native result markers contain only state, scale, and status.
- Kept automatic crop and `rotate-method=auto` disabled. Fullscreen cannot
  remove black bars already encoded inside the iPhone canvas; manual zoom is
  an opt-in crop and still needs physical Photos/Camera/rotation validation.

### Evidence and status

- Managed Release and complete `ReceiverResilience` pass. The eight-case
  worker harness, native parser/transport/SETUP/renderer contracts, and NIST
  crypto check also pass.
- Two clean compatible native builds reproduce core SHA-256
  `53B13433B9308547D491417F11692361DFC5B6EBFBDA018B8D3EEE7B4436436F`.
  Staged dependency inspection and the loader test pass.
- Wrapper patch SHA-256 is
  `8F48A4E72D765B0549119BC6366CB970384BAB8116B4430CE60ED67228213F9C`;
  libuxplay patch SHA-256 is
  `91AF80A36C7D4ECEB6470A1394722F2EC98312407DFA51A9929FC40E4B220CF5`.
- Source targets `0.12.17`/`0.12.17.0`. The 147-entry corresponding-source ZIP,
  pinned-hash validation, and extracted clean 57/57 rebuild pass. The exact
  13-entry payload, packaged-shell resilience, x64 Setup, byte-exact embedding,
  and three Setup self-checks pass. Installed and physical-device acceptance
  remain pending. Annotated `v0.12.17`, the normal latest Release, exact four
  assets, API digests, canonical/legacy latest routes, and fresh public
  re-download equality pass; see the
  [build report](docs/releases/0.12.17/BUILD_REPORT.md). Earlier public assets
  stay immutable.

## 0.12.16 — persistent idle discovery public review release

### Release scope

- AeroMirror no longer stops automatic AirPlay re-registration after two idle
  maintenance attempts. The first eligible refresh remains ten minutes after
  the receiver becomes idle; every later eligible refresh is scheduled 20
  minutes after the previous terminal result for as long as the receiver keeps
  running.
- Every normal automatic attempt still prefers the acknowledged native
  `refresh-discovery` command. It replaces the paired RAOP/AirPlay DNS-SD
  registration generation inside the existing core process and preserves the
  current listener ports. Active mirroring and AirPlay client grace defer due
  work rather than interrupting a connection.
- The legacy full-process restart fallback is limited to the first two
  automatic attempts in one idle epoch. Later command unavailability, timeout,
  or failure leaves the listening receiver alive and rearms the recurring
  schedule instead of creating indefinite process churn. Manual **Restart
  discovery** and a real physical IPv4 change remain explicit full DNS-SD-and-
  BLE restarts.
- A guarded Windows unlock may request another same-process refresh after any
  completed idle renewal, subject to the existing ten-minute cooldown,
  readiness, physical-network, restart, mirroring, and client-grace checks.
  Incoming AirPlay activity and a real network-profile change reset the
  recurring epoch so the next automatic deadline starts again at ten minutes.
- An update started from AeroMirror, a newer Setup opened over an installed
  copy, and a same-version reinstall now run without showing the shortcut and
  launch option form. Setup preserves the exact existing Start menu and desktop
  shortcut choices and relaunches AeroMirror after replacement. A first install
  remains interactive, and a newer installed version is never downgraded
  automatically.

### Compatibility and verification status

- Source targets app/Setup `0.12.16`, PE/file `0.12.16.0`, Setup comparison
  0.12.16, and exactly five 0.12.16 release-script defaults. The frozen
  0.12.15 native-core source, runtime, patch, and provenance are reused without
  modification.
- The managed Release build and complete `ReceiverResilience` suite pass. The
  deterministic discovery tests cover the first ten-minute deadline,
  indefinite 20-minute recurrence, saturating process-lifetime counter,
  unlock recurrence, activity deferral, cooldown/readiness guards, and the
  first-two-attempt legacy restart boundary. The installer self-check also
  covers fresh-install interactivity, unattended update/reinstall selection,
  exact shortcut preservation, relaunch, and automatic-downgrade prevention.
- The earlier 0.12.16 package identities were invalidated by the installer
  behavior change. A fresh exact 13-entry payload, packaged-shell resilience
  run, x64 Setup with byte-exact embedded inputs and all three non-installing
  self-checks, corresponding-source build, clean-tag packaging, API digest,
  and fresh public re-download checks pass.
- This is a best-effort periodic DNS-SD re-registration policy, not proof that
  an iPhone continuously lists the receiver. Physical long-idle, sleep/unlock,
  router, and real iPhone browse-cache acceptance remain pending.
- [`v0.12.16`](https://github.com/pyram1da/aeromirror/releases/tag/v0.12.16)
  is published as the normal updater-visible latest review Release from commit
  `c012d51d5cf3194fd647c4c65c20659386043baf`. Its exact four-asset set,
  checksums, API digests, and fresh public re-downloads pass; see the
  [build report](docs/releases/0.12.16/BUILD_REPORT.md). The frozen 0.12.15
  candidate remains untagged internal history rather than being relabelled.

## 0.12.15 — native-core lifecycle and parser hardening candidate

### Candidate scope

- The supported default AirPlay path now uses one explicit worker-lifecycle
  contract for mirror, HTTP, audio RTP, and NTP threads. Natural exit preserves
  join debt, concurrent stop has one join owner, self-stop is deferred, failed
  startup rolls back its socket/thread state, and a new start is refused until
  the prior worker has been joined.
- Accepted mirror and HTTP sockets are restored to blocking mode explicitly;
  Windows timeout values and retryable receive/send results are handled without
  treating a fragmented request as a disconnect. Mirror payload EOF returns to
  accept, while fatal media failure remains distinct from a normal stop.
- Mirror, HTTP/RTSP, SETUP, pairing, FairPlay, RTP, NTP, metadata, cover-art,
  buffer, allocation, and crypto boundaries now reject missing, oversized,
  inconsistent, or failed inputs with bounded status/error paths. Session
  publication is atomic: partial mirror, timing, or audio startup is rolled
  back instead of returning a false successful SETUP response.
- A fully decrypted and NAL-validated video access unit is now authoritative
  evidence that the sender is active. If the renderer is still marked paused,
  the core requests one nonblocking implicit resume and then delivers that same
  access unit; it does not discard the recovery frame or use a leaky appsrc
  queue.
- Video renderer access now takes lock-protected GStreamer references before
  timestamp work. Bus callbacks are mapped to their owning renderer, teardown
  waits for callbacks already holding that renderer, and unused codec renderer
  objects stay alive until final destruction. Audio bus handling uses the same
  owning-pipeline mapping rather than a process-global current renderer.

### Compatibility and verification status

- A fresh complete native CMake/Ninja build passes. The exact production
  `crypto.c` passes a NIST AES-CTR known-answer test across a 5+11-byte split
  and after reset. The production worker-lifecycle helper passes eight
  executable create/exit/stop/join scenarios. Source-bound protocol, parser,
  ownership, and renderer contract checks pass.
- Independent frozen-source review approves the supported default mirroring
  path with no P0/P1 finding. This is source/build evidence only: no physical
  Windows/iPhone run has yet shown that the reported frozen-last-frame symptom
  is fixed.
- Two clean compatible native builds reproduce executable SHA-256
  `38C6A63CE3CA40D3D1E23E5ECB5E0D152F9978986C4384A780C5767EAE0650A4`.
  Patch/provenance materialization passes with libuxplay patch SHA-256
  `E8233FFD59BFC49181D32BBD64A6C94A338FD31939B28A18C7FC7A3B5F14195D`,
  37 pinned libuxplay files, and 41 patched-source hashes in total. The source
  archive workflow creates 147 entries and validates their inputs/hashes. The
  final ZIP is 826,213 bytes with SHA-256
  `DA95EC58A17C37DA53948F770DABEAF29FAD75405CDF69F005F84ACF56362EB7`;
  its no-Git extracted tree passes hash validation and a clean 57/57 rebuild
  reproduces the same core.
- Staged-runtime inspection passes across 199 binaries, 148 DLLs, and 44
  requested GStreamer features mapped to 27 plug-ins; a manual staged
  `--loader-test` exits 0. A fresh managed build, the complete receiver
  resilience suite with its reference-safe D3D11 snapshot assertion, and a
  live discovery-pipe refresh all pass. The latter preserves PID 38712 and
  AirPlay port 43214 for request 98569.
- Source targets app/Setup `0.12.15`, PE/file `0.12.15.0`, Setup comparison
  0.12.15, and exactly five 0.12.15 release-script defaults. The initial thin
  package contains exactly 13 entries and its packaged shell passes resilience.
  The initial x64 Setup has byte-exact embedded input, and all three Setup
  self-checks exit 0. The focused final package/Setup rebuild against the
  frozen embedded documentation also passes. Installed update, physical-device,
  and public release gates remain pending.
- Initial artifact identities are retained for traceability: thin ZIP
  1,169,388 bytes/SHA-256
  `2123412734FD089F1B65A41DC0451A8105349BED5778B53211340A997500141C`;
  packaged/current shell 753,152 bytes/SHA-256
  `330EA373212FA0C47B0C25747DACF3F45A27959D56F6643569AD13889E606B81`;
  Setup 1,397,760 bytes/SHA-256
  `BCFFBC8BAE6453A437783A82A6EB307C701CA422A2DBDC5019E3E7F0D6A397E7`.
  These are the initial gate identities; the final focused-build identities are
  retained with the local handoff evidence.
- Internal 0.12.10–0.12.14 candidates remain unrelabelled, unpublished
  history. Public `v0.12.9` remains the immutable normal latest Release; there
  is no 0.12.15 tag, GitHub Release, public asset, or `BUILD_REPORT.md`.
- Photos inner-content detection/crop and Camera rotation remain unresolved.
  Deferred P2 work includes terminal join-failure parent lifetime, broader
  audio/HLS synchronization, remaining startup assertions, optional PIN/SRP
  depth, and consolidation of tolerant dual teardown paths.

## 0.12.14 — media-liveness diagnostic candidate

### Candidate scope

- Video timestamp retries now derive every presentation candidate from the
  same immutable remote timestamp through a signed, overflow-checked offset.
  Session and clock-epoch guards prevent an old callback from correcting a
  newly reset video clock, and audio/video mappings no longer share mutable
  offset state. This corrects a confirmed cumulative-retry source defect; it
  does not yet prove that defect caused the physically reported frozen frame.
- An active mirror session now emits one passive, numeric, content-free
  `AEROMIRROR_VIDEO_HEALTH` summary every two seconds. Session/geometry
  generations correlate VCL and codec configuration ingress, pause/resume
  options, appsrc flow, decoded-sink progress, Direct3D 11 Present progress,
  timestamp outcomes, monotonic ages, and per-interval deltas.
- The health classifier distinguishes starting, reset, client-paused, no-VCL,
  pre-appsrc, appsrc-error, unavailable proof, decoder-stall, present-stall,
  and healthy observations. It does not inspect pixels or media payloads and
  does not reset, resume, reconnect, crop, or otherwise recover a pipeline.
- The managed-compatible geometry line is unchanged. A separate diagnostic
  geometry record carries option/action/suspension evidence.

### Compatibility and verification status

- Two clean compatible native builds and an extracted prepared-source rebuild
  reproduce core SHA-256
  `5A6C8AEBC381F6090AD87CBB622A370B1BA0F29923B387C72C2AE07D78605F36`.
  The reviewed libuxplay patch SHA-256 is
  `4B2AAF2C8B48BD3B993940011678DD25919C16788E1B061D733469463D4217EE`.
- Patch/provenance, runtime/loader, live redirected-pipe, and complete receiver
  resilience checks pass. The tests cover immutable retry arithmetic, signed
  bounds, stale epochs, separate audio/video clocks, fixed health cadence and
  schema, privacy/passivity, media-stage ordering, and legacy geometry.
- Source targets app/Setup `0.12.14`, PE/file `0.12.14.0`, Setup comparison
  0.12.14, and exactly five 0.12.14 release-script defaults.
- The exact 13-entry local payload, resilience against its packaged shell,
  Setup embedded payload/provenance equality, and all three waited Setup
  verification modes pass. Shell, Setup, and core are x64; version and five-
  default, link, UTF-8/no-BOM, diff, and stable-input gates pass.
- The 0.12.13 candidate remains frozen internal history after its physical
  last-frame freeze. Version 0.12.14 is also internal and pretag; installed
  update, physical Windows/iPhone acceptance, tag, GitHub Release, and public
  re-download remain pending. There is no 0.12.14
  `BUILD_REPORT.md`; public `v0.12.9` remains the immutable normal latest.
- This candidate does not claim that the physical freeze is fixed or its root
  cause is proven. It adds no automatic media recovery, Photos content
  rectangle/crop, Camera-orientation fix, discovery/AWDL/AirDrop change, full
  native-core audit completion, or correction for the separate natural mirror
  worker exit/join P1 lifecycle gap.

## 0.12.13 — persistent LAN discovery candidate

### Candidate scope

- Automatic idle, Windows-unlock, and native discovery-health maintenance now
  prefers a request-correlated refresh of the paired RAOP and AirPlay DNS-SD
  registrations inside the running receiver. A successful refresh preserves
  the native process and both advertised listener ports instead of replacing
  the receiver merely to republish Bonjour records.
- The native core owns its DNS-SD identity and TXT data for the complete
  service lifetime, processes real Bonjour registration callbacks on the
  owning GLib thread, treats RAOP and AirPlay as one generation, rolls back a
  partial pair, and retries failures with bounded 1, 2, 5, 10, and 30 second
  delays. An active AirPlay request, PIN flow, audio/video client, or mirroring
  session defers the operation without listener teardown.
- A narrow version-1 stdin/stdout protocol correlates capability, deferred or
  accepted progress, and terminal ready or failed markers to the request,
  generation, current PID, and both ports. Stale, malformed, wrong-PID, or
  wrong-port markers cannot settle a managed request. Unsupported, rejected,
  timed-out, or repeatedly failed refreshes retain the bounded full-process
  recovery fallback.
- **Restart discovery** deliberately remains a strong full receiver restart,
  republishing both DNS-SD and the separate BLE beacon. A real physical IPv4
  change also remains a full restart because the unchanged BLE helper receives
  its advertised address only at process startup.
- BLE helper output is now buffered into complete stderr lines so it cannot
  corrupt command framing on stdout. Unexpected helper start failure or exit
  is reported once; intentional shutdown during receiver maintenance is not
  mislabeled as failure.
- Receiver names are canonicalized to at most 50 UTF-8 bytes, leaving room for
  the 12-character device ID and `@` in Bonjour's 63-byte RAOP label. Managed
  settings preserve complete text elements, remove C0/DEL controls, replace
  unpaired UTF-16, trim whitespace, and fall back to `AeroMirror`; the native
  core independently truncates at a complete UTF-8 code point. Interactive
  saves inform the user and persist the effective iPhone-visible name, while
  legacy long values migrate silently on load/save. AirPlay, RAOP, and `/info`
  use the same canonical name.

### Compatibility and verification status

- Physical testing on 2026-08-13 found an unresolved frozen-last-frame media
  defect while RTSP/control and the mirror parser remained alive. This blocks
  publication of 0.12.13; any corrective code will use a newer version rather
  than replacing this tested candidate in place.

- Local app/Setup candidate version is `0.12.13`; Windows PE/file version is
  `0.12.13.0`. Setup's comparison version and exactly five release-script
  defaults target 0.12.13.
- Two clean official native builds and a rebuild from the extracted prepared
  corresponding-source archive reproduce core SHA-256
  `AD59F33907980122551458E5B97CE600D6AB8DBFF923B7BEE5EB30A26F521698`.
  Patch/provenance, reverse-apply, runtime/dependency, loader, process-lifetime,
  and source-diff audits pass.
- Four redirected-pipe integration cases pass, including same-PID/same-port
  refresh and ASCII, Cyrillic, and fallback receiver-name boundaries. The
  fresh managed x64 build and complete receiver resilience suite pass.
- Shell/Setup source versions, Setup comparison, exactly five release-script
  defaults, all 59 source-Markdown local links, strict UTF-8/no-added-BOM, and
  `git diff --check` pass.
- The initial full pretag package gate passes. Runtime/dependency inspection
  covers 199 binaries and 148 staged DLLs around the exact reviewed core. The
  prepared native-source archive has exactly 143 entries/139 files, with
  content, provenance, patch, and extracted-rebuild checks passing; its
  timestamp-dependent ZIP container hash is deliberately not treated as a
  reproducible input.
- The thin review payload contains exactly 13 entries, and the complete
  resilience suite passes against its exact packaged shell. Setup builds with
  exact embedded-payload/provenance equality; `/verify-runtime`,
  `/verify-shortcut-selection`, and `/verify-update-lifecycle` each exit 0.
  x64 architecture, version/five-default, link, UTF-8, diff, and release-input
  fingerprint gates pass. Volatile shell/payload/Setup sizes and hashes remain
  in the gate handoff rather than these evidence inputs.
- Source implementation and independent review found no blocker in the
  persistent-discovery scope. The later full media audit found the separate P1
  frozen-frame issues above. The focused post-evidence payload/Setup rebuild
  also passes:
  exact payload, packaged-shell resilience, embedded-input equality, all three
  Setup verification modes, version/link/UTF-8/diff, and input fingerprints
  remain green. Installed update, physical 30–40 minute iPhone visibility,
  Windows 10/11, exact tag, GitHub Release, and public re-download remain pending.
  There is no 0.12.13 `BUILD_REPORT.md`.
  Public `v0.12.9` remains the immutable normal latest review release.
- This candidate does not implement AWDL, AirDrop, BLE in-process refresh,
  continuous remote visibility attestation, a Photos content-rectangle/crop
  fix, or a full native-core audit. Bonjour callback readiness proves local
  paired registration, not that an iPhone currently lists the receiver or has
  invalidated a cached browse result.

## 0.12.12 — bounded second idle-discovery renewal candidate

### Candidate scope

- AeroMirror now schedules a second timed receiver/discovery renewal 20 minutes
  after the existing ten-minute idle renewal. This covers a reporter-machine
  0.12.11 episode in which the first renewal completed normally but the iPhone
  no longer listed the receiver roughly 23 minutes later; manual discovery
  restart restored visibility.
- The timed path and the existing post-renewal `SessionUnlock` path share the
  same strict limit of two renewals per idle epoch. Whichever consumes the
  second allowance prevents the other path from creating a third restart.
- Active mirroring and current client grace preserve a due timed stage for a
  later idle supervision pass. High-level client activity, mirroring start,
  manual discovery refresh, and a new eligible discovery epoch retain their
  existing reset/re-arm boundaries.
- This is a bounded managed mitigation, not a root-cause correction. The
  available log proves receiver processes and startup readiness markers, but
  cannot isolate DNS-SD, BLE, Bonjour browse state, or an iOS cache. It does
  not add in-place re-publication, acknowledged discovery-ready IPC, a stable
  AirPlay-port contract, or a continuous-visibility guarantee.

### Compatibility and verification status

- Local app/Setup candidate version is `0.12.12`; Windows PE/file version is
  `0.12.12.0`. Setup's internal comparison version and exactly five release-
  script defaults target 0.12.12.
- The final local automated pretag gate passes: a fresh managed x64 build, the
  complete receiver resilience suite, and its repeat against the exact shell
  in the review payload all pass. Independent source/evidence review reports no
  P0/P1/P2 finding. The suite covers both timed stages, the shared allowance,
  active/client-grace deferral, cooldown, and epoch reset boundaries.
- The native receiver, BLE helper, dependencies, runtime, patches, and
  provenance inputs are unchanged. The automatic Photos behavior and other
  managed improvements from the untagged 0.12.11 candidate are retained.
- Prepared native source retains the reviewed core/provenance and contains 143
  archive entries/139 files. The thin review payload contains exactly 13
  entries. Setup builds with embedded payload/provenance equality, and
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` each exit 0. x64 PE, version/five-default,
  strict-UTF-8, diff, release-input fingerprint, and all 29 local links across
  57 Markdown files pass. Exact container sizes/hashes remain in the gate
  handoff rather than these packaging inputs.
- Installed update, a physical 30–40 minute idle run, Windows 10/11 and iPhone
  acceptance, exact tag, GitHub Release, and public re-download remain
  pending. There is no 0.12.12 `BUILD_REPORT.md`. Public `v0.12.9` remains the
  immutable normal latest review release.

## 0.12.11 — automatic Photos outer-window fitting candidate

### Candidate scope

- The exact correlated Photos/media signature—primary, source, and encoded
  `3840x2160` with auxiliary `0x0`—now drives a temporary landscape fit of the
  outer renderer automatically. Users no longer need a separate experimental
  setting for the normal action of opening media in Photos.
- The temporary schema-12 `FollowPhotosMediaCanvas` field and its Advanced UI
  control are retired. Existing `true`, `false`, or malformed legacy values are
  ignored when loaded and are omitted the next time settings are saved. The
  settings schema remains 12, and the general automatic-window-fitting opt-out
  remains available.
- The exact media canvas remains provisional: it cannot become the trusted
  device-frame baseline or make an automatic placement persistable. A later
  phone-shaped `998x2160` frame returns the outer renderer to portrait, while
  generic `1920x1080` and near-miss signatures retain the existing conservative
  path. No decoded pixel, crop, zoom, native capability, receiver argument, or
  inner-media layout is changed.
- The 0.12.10 monotonic geometry sequence, non-starving debounce, target-class
  and exact-aspect fitting, retry behavior, and reflection-test storage/log
  isolation are retained.

### Compatibility and verification status

- Local app/Setup candidate version is `0.12.11`; Windows PE/file version is
  `0.12.11.0`. Setup's internal comparison version and exactly five release-
  script defaults target 0.12.11.
- The final local automated pretag gate passes: a fresh managed x64 build, the
  complete receiver resilience suite, and a repeat of that suite against the
  exact packaged shell all pass. Independent source and evidence-document
  review reports no P0/P1/P2 finding. The suite covers legacy-key retirement,
  exact-signature automatic fitting, return to the phone frame, near-miss
  exclusion, retry behavior, and placement protection.
- Prepared native source retains the reviewed core and provenance and contains
  exactly 143 archive entries/139 files. The review payload contains exactly
  13 entries. Setup builds with matching embedded payload/provenance, and
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` each exit 0. x64 PE, version/default, local-link,
  strict-UTF-8, diff, and release-input fingerprint-stability audits pass.
  Exact shell/payload/Setup/native-source container identities are retained in
  the final gate handoff rather than embedded in source documents.
- No native source, AirPlay capability, patch, runtime, dependency, discovery,
  reconnect, Camera-orientation, Bluetooth, short-gap, or borderless-window
  change is part of 0.12.11.
- Physical Photos/Camera/video behavior on Windows 10/11, installed update,
  exact tag, GitHub Release, and public-download gates are pending. Automated
  evidence does not establish physical Windows/iPhone behavior. There is no
  0.12.11 `BUILD_REPORT.md`. The untagged 0.12.10 candidate is superseded
  locally; public `v0.12.9` remains the immutable normal latest review release.

## 0.12.10 — renderer geometry and test-isolation candidate

### Candidate scope

- Correlated native geometry/size records now receive a monotonic event
  sequence for the lifetime of the native core. Repeated copies of the same
  pending candidate advance freshness without moving the original 350 ms
  deadline, so a noisy marker stream cannot postpone a stable decision
  indefinitely. A duplicate of the current stable value does not reopen the
  debounce, while a device-frame/media-canvas classification change remains a
  distinct event.
- Exact renderer fitting now tracks both the selected target class
  (`DeviceFrame` or `MediaCanvas`) and its exact aspect ratio. A newer event
  refits when either changes, including a same-orientation transition such as
  `3840x1776` device frame to `3840x2160` media canvas. A scaled size with the
  same class and exact aspect is consumed without moving the window again.
  Toggling the default-off Photos option re-evaluates the current stable frame.
  If that supervision pass is blocked by an active resize/mouse gesture, or a
  fit attempt fails, the class/aspect mismatch remains pending and is retried
  instead of being consumed.
- Reflection-based resilience tests redirect every persistent `AppSettings`
  path to one process-lifetime, GUID-named child of the system temporary
  directory before logging begins. They drain the asynchronous logger
  deterministically before inspection and cleanup, so the real per-user
  `receiver.log` and settings/trust/key files are not touched by the suite. A
  failed run preserves that exact temporary root and prints its path for
  diagnosis; only a fully successful, drained run removes it.

### Compatibility and verification status

- Local app/Setup candidate version is `0.12.10`; Windows PE/file version is
  `0.12.10.0`. Setup's internal comparison version and exactly five release-
  script defaults target 0.12.10.
- Settings remain at schema 12 and `FollowPhotosMediaCanvas` remains false for
  clean and migrated profiles. This candidate adds no settings migration and
  no native protocol, capability, patch, runtime, or dependency change.
- The managed implementation build and full receiver resilience suite pass,
  including deterministic geometry/debounce/refit and isolated-log checks.
  Independent source review reports no open P0/P1/P2 finding. The unchanged-
  native reuse/provenance and prepared-source checks, exact 13-entry review
  payload, Setup build, embedded payload/provenance, runtime loader, shortcut,
  and update-lifecycle gates pass in the initial full pretag run. A focused
  payload/Setup rebuild after the evidence documents stabilized also passes;
  exact final container hashes are retained in the gate handoff rather than
  embedded into their own source inputs.
- Same-process, same-port DNS-SD/BLE re-publication with an acknowledged ready
  marker is still `DESIGN/NEXT`; it is not implemented in 0.12.10.
- Physical iPhone and Windows 10/11 geometry, Photos, Camera, reconnect,
  discovery, and installed-update acceptance are pending.
  There is no 0.12.10 tag, GitHub Release, or `BUILD_REPORT.md`. Public
  `v0.12.9` remains the immutable normal latest review release.

## 0.12.9 — bounded discovery and Photos-window public review release

### Review scope

- After the existing ten-minute idle-discovery renewal has completed, a later
  Windows session unlock may schedule at most one final managed receiver
  restart and discovery re-registration. A ten-minute cooldown, ready local
  sockets and discovery marker, cached physical IPv4, idle mirroring/client
  state, and clear restart/network-maintenance guards are required. This is a
  bounded mitigation for a receiver reported missing after long idle, not a
  proven root-cause fix or a stable-port discovery contract.
- Settings schema 12 adds a default-off Advanced option that lets the exact
  ambiguous Photos/media `3840x2160`, source `3840x2160`, auxiliary `0x0`,
  encoded `3840x2160` canvas temporarily shape the outer renderer window. The
  provisional wide fit cannot become trusted device orientation or overwrite
  a valid saved placement. It changes no native AirPlay capability, feature
  bit, decoded pixel, crop, or zoom, so inner photo/video content may remain
  small.
- The untagged 0.12.8 Direct3D 11 presentation-proof handoff remains part of
  this public review release. Feedback, mirror-start, push/PTS, sink observation,
  cached HWND state, or a stale image still cannot close continuity without
  matching current-PID/session/epoch/reason/gap Present proof.
- A full Windows reboot is not an expected normal result of AeroMirror Setup.
  Setup extracts the pinned portable app runtime, but installs no system-wide
  .NET/VC++ redistributable, driver, or framework prerequisite. A stopped or
  stale system-wide Bonjour lifecycle is the strongest current hypothesis for
  one reported Windows 10 first-install case, but it is unproven. This patch
  does not mutate Bonjour; a clean Windows 10 VM run with pre-reboot evidence
  is the acceptance path.

### Compatibility and verification status

- Public/app/Setup version is `0.12.9`; Windows PE/file version is
  `0.12.9.0`. Setup's internal comparison version and all five
  release-script defaults target 0.12.9; source-version and settings-schema-12
  checks pass.
- The final managed x64 build and complete receiver resilience suite pass.
  Independent source review reports no P0/P1/P2 finding. A separate legacy
  `csc.exe` build confirms source semantics and `0.12.9.0` metadata; that
  compiler is not byte-deterministic, so the exact packaged shell hash belongs
  to the final package gate rather than the independent-build comparison.
- The unchanged 0.12.8 native core is reused at SHA-256
  `eb8162577689eed354c4382acfe099665a6d9e14eed466cb4da6ca6e087448d6`.
  Provenance, both patch reverse-apply checks, the extracted prepared-source
  rebuild, 143 archive entries/139 files, the loader test, and the runtime
  dependency audit covering 199 inspected binaries and 148 copied DLLs pass.
- The pre-documentation thin review package has the exact 13 entries. Setup
  builds; `/verify-runtime`, shortcut, and update-lifecycle checks pass; the
  embedded payload/provenance comparison passes; and shell plus Setup are x64
  `0.12.9.0`. Version/default/schema, local-link, strict-UTF-8, and diff checks
  pass.
- The focused package review and Setup rebuild against the final evidence
  documents pass: exact payload, embedded payload/provenance, runtime,
  shortcut/update lifecycle, version/default, link, UTF-8, and diff checks all
  pass again. Volatile shell/payload/Setup/native-source-ZIP container hashes
  are recorded in the versioned `BUILD_REPORT.md`.
- Annotated tag object
  `10deba1d48482da3500cf0bd7c796c87c7fce736` resolves to commit
  `b807d5dece26e972c58a3a2f7e5585dc8075672e`. GitHub Release `368804215`
  is normal, updater-visible latest, `draft=false`, and `prerelease=false`.
  Exactly four assets were published; all final local files, GitHub API
  digests, and fresh public re-downloads match. `SHA256SUMS.txt` contains
  exactly the three non-checksum assets. Canonical and configured legacy
  latest routes resolve to the same Release, tag, and Setup bytes.
- Physical Windows 10/Windows 11/iPhone, the installed update from public
  0.12.7, and actual discovery, Photos, and reconnect acceptance remain
  pending. This is a public review release, not a physically accepted build.
- Version 0.12.8 remains untagged and unpublished. Published 0.12.7 is
  immutable historical release evidence and was not modified.

## 0.12.8 — evidence-gated reconnect handoff candidate

### Candidate scope

- A recovered AirPlay feedback heartbeat is no longer treated as proof that
  video presentation resumed in the same-session feedback-gap path. The
  connection-loss view may change to
  **Connection restored / Waiting for image**, but it will close only after a
  current-process, current-session, current-recovery-epoch marker proves that
  fresh post-gap media reached the Direct3D 11 swap-chain present path.
- Recovery telemetry distinguishes
  `AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=N epoch=E`, appsrc
  push/PTS and exact sink observations, and the sole fade authority
  `AEROMIRROR_VIDEO_PRESENT_READY epoch=E gap_seconds=N
  proof=d3d11-present pts_delta_ms=D`. Push, timestamp, sink,
  renderer-window visibility, and a cached HWND remain diagnostic evidence
  only and cannot dismiss the view.
- If presentation proof does not arrive within the bounded three-second wait,
  AeroMirror keeps continuity visible and replaces the waiting text with
  explicit Screen Mirroring reconnect guidance. This candidate does not silently reset
  the receiver, replace a half-open video socket, or rebase the media clock.
- Selecting the receiver again may emit
  `AEROMIRROR_VIDEO_PRESENT_ARMED reason=mirror-start epoch=E` and restart the
  three-second presentation wait. A feedback challenge requires the exact
  stored positive gap; only an accepted mirror-start challenge expects
  `gap_seconds=0`. Mirroring-start alone does not expose the old renderer, and
  the new epoch still needs matching Direct3D 11 present proof.

### Compatibility and verification status

- Public/app/Setup version is prepared as `0.12.8`; Windows PE/file version is
  prepared as `0.12.8.0`. Setup's internal comparison version and all five
  release-script defaults target 0.12.8.
- The source implementation and independent final code review are complete
  with no remaining P0/P1/P2 finding. The managed build and receiver resilience
  suite pass.
- The official native build and extracted prepared-source rebuild reproduce
  core SHA-256
  `eb8162577689eed354c4382acfe099665a6d9e14eed466cb4da6ca6e087448d6`;
  the reviewed libuxplay patch SHA-256 is
  `c5be47ee96be25609677103cf85b3d98b07e2752a980d0d6d9fb975d187ad05e`.
  Both native patches reverse-apply. Native source generation produces 143
  archive entries/139 files; loader testing and the runtime/provenance/
  dependency audit covering 199 inspected binaries and 148 copied DLLs pass.
- The thin review package has the exact 13 entries. Setup builds,
  `verify-runtime`, shortcut, and update-lifecycle checks exit 0, and the
  embedded payload/provenance comparison passes. Shell, Setup, and core are x64
  `0.12.8.0`; version/default, documentation-link, and diff checks also pass.
- The focused final package review and Setup rebuild after the evidence-doc
  update also pass: exact 13-entry payload, embedded payload/provenance,
  `verify-runtime`, shortcut/update lifecycle, version, link, and diff gates all
  pass again. Volatile payload/Setup container hashes are retained in the gate
  handoff. Physical iPhone/Windows 10/Windows 11, exact tag,
  install-from-public, GitHub Release, checksum, and public re-download
  evidence were not completed before 0.12.8 was superseded. No 0.12.8 asset
  was published, so no post-publication 0.12.8 `BUILD_REPORT.md` exists.
- The change is scoped to a truthful continuity handoff. It does not yet claim
  to repair the underlying long-gap video freeze, automatic discovery or
  reconnect, immediate loss detection, or a first iPhone tap that never reaches
  Windows.
- Direct3D 12 and advanced sinks do not provide this proof. Interactive
  `-vsync no` deliberately skips the synchronized PTS/Present proof and retains
  reconnect guidance.
- Photos' small inner photo/video presentation is not fixed. The mirror-only
  AirPlay feature-bit experiment is unchanged, and this candidate does not
  infer a crop rectangle from the `3840x2160` presentation canvas.
- No exact display-capability marker landed in this patch. The HEVC
  4K-versus-Full-HD Photos comparison remains a physical A/B using existing
  launch and geometry evidence.

## 0.12.7 — media-session continuity hotfix

### Fixed

- Default Windows audio now selects GStreamer's `wasapi2sink` with
  `continue-on-error=true`. With the pinned GStreamer 1.28.1 runtime,
  supported endpoint open, I/O, and device-removal failures are reported as
  warnings while the sink keeps consuming buffers instead of ending the
  shared media pipeline. Muted output remains unchanged, and advanced UxPlay
  arguments still follow the managed default so an experienced tester can
  override it explicitly.
- Type-specific AirPlay `TEARDOWN` is no longer forced into a server-side
  disconnect of the whole control connection. The existing upstream
  `Connection: close` response header remains, but the client controls whether
  and when the socket closes while the requested media state is torn down.
- The headless native wrapper preserves the shell-provided `-vs` and `-fs`
  arguments instead of replacing them, so the requested Direct3D sink and
  fullscreen behavior reach UxPlay.

### Compatibility and verification status

- The managed 0.12.7 build and resilience suite pass in an isolated output
  directory. Two clean native rebuilds reproduce SHA-256
  `11b65324c83f23503f2d555d0064d1348c884407bf7f9b1c34d27b5d1c05fb9b`;
  patch/current-source/protected-audio hashes, the exact 143-file prepared
  native-source content and provenance, dependency collection, and the
  distinct redistributed GStreamer 1.28.1 versus build-toolchain 1.28.5
  contracts pass. Exact public-runtime loader and both reverse-apply gates also
  pass.
- The exact 13-entry review payload, shell and Setup `0.12.7.0` versions,
  Setup's internal comparison version and shortcut/update lifecycle checks,
  embedded-payload and `/verify-runtime` verification, all five release-script
  defaults, local links, and `git diff --check` pass. An extracted rebuild from
  the prepared native source reproduces the same core. The final pre-tag
  payload and Setup were regenerated after the evidence update, and their
  embedded-payload, lifecycle, version, link, and diff gates passed again.
- Annotated tag `v0.12.7` resolves to commit
  `dd343a44b0c9b6904815cd78e54a841e9f5ef6be`. Exact-tag packaging, the normal
  latest channel, exactly four public assets, the three-entry checksum file,
  GitHub API digests, and fresh public re-download size/SHA-256 checks pass.
  Canonical and configured legacy latest routes both return Release ID
  `368571434` and tag `v0.12.7`; the legacy-route Setup hash also matches.
- A public-build Windows 11/iPhone smoke on the reporter's system passes the
  urgent involuntary Photos/video session-drop target: direct Photos launch
  and a normal gallery/video session work without the former drop. This is a
  scoped result, not full physical acceptance. One first direct-Photos
  connection tap failed before the second succeeded, inner Photos media remains
  small, and a reporter-estimated wall-clock interruption of about 15 seconds
  cleared the placeholder after reconnect but left video frozen; the exact log
  interval records an 11-second feedback gap. The installed update from public
  0.12.6, Windows 10, and the complete repeated/interrupt matrix remain pending.
- This hotfix does not claim to crop or enlarge Photos content, repair
  discovery, or make reconnect automatic. Audio failures outside the documented
  `wasapi2sink continue-on-error` scope are not claimed to be isolated from the
  media session.
- AeroMirror project policy treats published `v0.12.7` and its four assets as
  immutable even though GitHub reports `immutable=false`. Any correction uses
  0.12.8 or later rather than moving the tag or replacing an asset.

## 0.12.6 — D3D11 stability and clearer reconnect guidance

### Changed

- New settings profiles now use an explicitly pinned Direct3D 11 decoder and
  video sink. Existing profiles that still used the legacy automatic renderer
  choice migrate to Direct3D 11; an explicit Direct3D 12 choice remains
  available as an experimental opt-in.
- The Advanced settings page now recommends Direct3D 11 instead of offering
  GStreamer's automatic selection. Advanced UxPlay arguments remain later on
  the command line and can still override the managed compatibility choice.
- The native receiver no longer advertises the unimplemented photo, slideshow,
  and photo-preload capabilities: exact feature bits 1, 5, and 13 are cleared.
  Supported audio, authentication, and metadata capabilities remain. This is
  a controlled Photos negotiation experiment whose physical effect remains
  pending, not a claim that inner Photos sizing is fixed.

### Fixed

- The connection-loss view is raised immediately above the external renderer
  without activating itself or making an ordinary stream permanently topmost.
  After fatal native cleanup it gives an explicit instruction to reconnect to
  the named receiver from iPhone Screen Mirroring instead of continuing to
  imply that recovery is automatic.
- A renewed or fatal loss during the 180 ms renderer handoff cancels that
  fade, restores full opacity, and leaves continuity visible.
- Native HTTP startup and fatal reset now emit explicit ready or failed
  markers. The shell preserves a recovered native process only after an exact
  same-port reset acknowledgement from the current process; a bind failure or
  port mismatch cleans up and exits for full-process recovery. AirPlay
  `TEARDOWN` now explicitly closes the connection so cleanup cannot remain in
  an ambiguous half-open state.

### Compatibility and verification status

- Direct3D 11 is a conservative stability candidate for streams that change
  resolution, including the observed Photos transition. Physical iPhone A/B
  evidence is still pending; this patch does not claim to crop or zoom the
  small photo and black bars already encoded inside the Photos canvas.
- Managed build, resilience, reproducible native-core/source, Setup/lifecycle,
  exact-payload, version/link, and exact-tag gates pass. The normal public
  review Release contains exactly four assets; their GitHub API digests and
  re-downloaded byte sizes/SHA-256 values match the final local files.
- Canonical and configured legacy `releases/latest` API routes return the same
  public `v0.12.6` Release ID. The installed 0.12.5-to-0.12.6 update and all
  physical Windows 10/11 plus iPhone gates remain pending.
- Receiver discovery and automatic reconnect are not claimed as fixed. A
  stale iOS browse result or a connection attempt that never reaches Windows
  remains an explicit physical test failure.
- AeroMirror project policy treats the published `v0.12.6` tag and four assets
  as immutable. Any correction uses 0.12.7 or later rather than replacing an
  existing public file.

## 0.12.5 — safer Photos startup and recovery feedback

### Fixed

- A session that starts with the exact recorded Photos
  `3840x2160 aux=0x0` presentation canvas no longer treats that canvas as the
  iPhone's device orientation. A later phone-shaped `998x2160` frame can
  establish portrait for the same session, while the observed real-landscape
  geometry and unrelated 16:9 streams remain accepted.
- An unresolved automatic/provisional fit no longer overwrites a valid saved
  stream-window placement. Placement is persisted after a trustworthy device
  frame or an explicit user move, resize, or manual fit.
- A native three-second feedback warning now schedules the capable pre-fatal
  continuity view for a four-second local deadline instead of depending on a
  later warning line that may never arrive. Recovery before the deadline
  cancels it. If the view has already appeared, acknowledged recovery now
  changes its text to **Connection restored / Waiting for image** while the
  existing renderer-gated handoff continues to wait for visible video.

### Compatibility and verification status

- This patch changes the managed shell only. The pinned UxPlay core, native
  source provenance, third-party runtime, receiver identity/trust state,
  settings location, update identity, and physical-network protection policy
  remain unchanged from 0.12.4.
- Managed build, resilience, exact 13-entry review-payload, Setup/lifecycle,
  embedded-payload hash, native-source/provenance-reuse, version/link,
  exact-tag, normal latest-channel, API-digest, and public re-download checks
  pass for the published review Release. The released native core is
  byte-identical to 0.12.4.
- The former `Nadejny/aeromirror` updater API and Setup URL redirect to the
  canonical `pyram1da/aeromirror` repository and expose 0.12.5 successfully.
  The actual installed 0.12.4-to-0.12.5 update and all physical Windows 10/11
  plus iPhone tests remain pending.
- The inner Photos problem is not fully fixed. iOS may still encode a small
  photo and black bars inside the `3840x2160` canvas; AeroMirror now protects
  outer orientation and saved placement but does not crop or zoom those
  pixels.
- Receiver visibility after delayed Wi-Fi join or a stale iOS browse result
  remains a physical discovery gate. This patch does not claim that a manual
  refresh can force an iPhone request that never reached Windows.
- The published `v0.12.5` tag and four assets are immutable. Any correction
  uses 0.12.6 or later rather than replacing an existing public file.

## 0.12.4 — smoother recovery, renderer handoff, and diagnostics

### Fixed

- Temporary AirPlay feedback gaps no longer force the shell to replace a core
  that has already recovered its listening socket. AeroMirror keeps the same
  native process and AirPlay port after completed in-process cleanup, and the
  UxPlay reset tolerance returns to its upstream 15-second default instead of
  declaring a six-second network interruption fatal.
- A patched core now announces feedback-health support and recovery. After a
  five-second feedback gap, AeroMirror can show continuity without ending the
  active session; the placeholder closes if feedback resumes and remains
  gated off for older cores that cannot report recovery.
- A reconnect no longer dismisses continuity on the protocol-start line alone.
  AeroMirror waits until the replacement renderer exists and has been placed,
  then fades the placeholder over 180 ms. Saved bounds are also applied from
  the renderer's early Windows show event, reducing the visible jump from the
  default position.
- The continuity snapshot now uses only the renderer client area and is
  rejected when another visible window overlaps it. This captures more useful
  last frames than the former foreground-only rule without copying unrelated
  foreground content.
- Repeated supervision no longer rewrites an unchanged renderer title,
  taskbar style, or topmost state four times per second. Automatic proportion
  fitting is queued after interactive resize completion rather than mutating
  the external window during the drag.

### Changed

- The former Minimal latency profile is now **Interactive**. It disables
  timestamp scheduling with `-vsync no` but no longer forces the aggressive
  50 ms audio-buffer request that caused extra stutter on some networks. Audio
  and video synchronization can be less exact in this mode; Balanced remains
  the default.
- Selecting Direct3D 11 or Direct3D 12 now pins both the matching Direct3D
  decoder family and video sink, with codec matching performed when the
  pipeline is created.
- Support diagnostics record feedback-gap episode count, longest duration,
  native capability state, the raw AirPlay geometry header (including its
  previously ignored auxiliary pair), and the decoder/video sink selected by
  GStreamer. The auxiliary fields are evidence only and are not treated as a
  crop, pixel-aspect-ratio, or rotation signal.
- The settings Back button is larger.

### Compatibility and verification status

- This patch changes both the managed shell and the reviewed native UxPlay
  patch. The pinned upstream revisions and third-party runtime stay unchanged;
  the rebuilt core, reviewed patch, modified source, build inputs, and prepared
  corresponding source match the locked native provenance.
- The managed build, resilience suite, review payload, Setup build and
  lifecycle verifier, native-source/provenance validation, version/link audit,
  `git diff --check`, clean exact-tag packaging, normal latest-channel, and
  public re-download/API digest verification pass for the published review
  release.
- Physical Windows 10/11 plus iPhone recovery, smoothness, Photos, placement,
  and installed-update tests remain pending and must not be inferred from the
  automated checks.
- The published `v0.12.4` tag and its four assets are immutable. Any correction
  must use 0.12.5 or later.
- Photos can still place a small image and black bars inside a
  `3840x2160` encoded canvas. This patch adds geometry evidence but does not
  guess a crop or fix that inner layout.
- The renderer still belongs to the external GStreamer process. A Mac-style
  hover-only frame, true borderless viewer, live aspect lock during dragging,
  and a single embedded surface require a separate native renderer/IPC design.
- AirDrop interoperability and localization are not included. Genuine AirDrop
  requires separate Bluetooth/AWDL, identity, and encrypted-transfer research;
  the resource-based language design remains tracked by decision D-006.

## 0.12.3 — connection-loss continuity and remembered stream windows

### Added

- After a confirmed fatal stream loss, AeroMirror can keep a softened view of
  the last visible renderer frame at the same bounds while the receiver renews
  discovery. The placeholder stays available through core replacement, offers
  an explicit close action, and disappears when a new mirroring session starts,
  the receiver is stopped, or AeroMirror exits. The captured frame remains in
  process memory and is never written to disk.
- The renderer's last normal position, outer size, and DPI are saved after a
  valid fit, move, or resize and restored for the next stream. Bounds from a
  disconnected monitor are clamped into an available Windows work area, and
  size follows a changed monitor DPI.

### Fixed

- When mirroring starts directly inside Photos, a phone-shaped raw size marker
  that arrives before the 350 ms debounce is retained as the device-frame
  candidate. A later `3840x2160` Photos canvas in the same startup burst no
  longer steals that portrait baseline.
- Automatic fitting now preserves the restored window center and approximate
  client area while applying the learned stream proportions.
- The tray fallback is labelled **Restore window proportions**, and the
  settings-page Back control has a larger arrow and hit target.

### Compatibility and verification status

- This patch changes the managed shell and settings schema only. The pinned
  UxPlay executable, third-party runtime, receiver identity and trust state,
  update path, and physical-network protection policy are unchanged.
- The managed build, resilience, installer, packaging, exact-tag, checksum,
  and public re-download/API digest gates pass. The installed updater path and
  physical Windows 10/11 plus iPhone acceptance remain pending for this public
  review release.
- This does **not** fully fix Photos content sizing. iOS can send a
  `3840x2160` encoded canvas with the photo and black bars already inside it;
  the shell can preserve the outer phone orientation but cannot safely crop or
  zoom those inner pixels without native content metadata or validated pixel
  analysis. A session that exposes only a generic media canvas and no early
  phone-shaped marker also remains ambiguous.
- Localization is not included in this patch. The current UI remains Russian;
  the planned resource-based `System / English / Russian` design is tracked by
  decision D-006.

## 0.12.2 — reconnect recovery, media orientation, and automatic fitting

### Fixed

- An abnormal Wi-Fi/client loss no longer loses its recovery decision when the
  native core completes mirror cleanup quickly. AeroMirror now performs one
  bounded discovery renewal after that cleanup; an ordinary clean disconnect
  still keeps the healthy receiver running without a restart.
- A Photos `3840x2160` presentation canvas no longer forces the renderer into
  landscape after AeroMirror has learned a `998x2160` iPhone device frame for
  the session, including when the manual tray fit is used. Physical rotation
  remains accepted when the normalized device aspect matches, including
  `1080x1920` and `1920x1080` devices.
- With automatic fitting enabled, completing a manual renderer resize now
  restores the learned stream proportions after a short delay. Moving,
  minimizing, or maximizing the window does not trigger the fit, and an
  explicit disabled setting remains authoritative.

### Changed

- The first exact video frame in each mirroring session now seeds the device
  aspect instead of requiring a modern tall-iPhone ratio. This fixes physical
  16:9 compatibility while retaining conservative suppression for later
  non-matching media canvases.
- The normal setting is now labelled **Automatically preserve stream-window
  proportions**, and the tray fallback is **Fit window now**.

### Compatibility and verification status

- The managed x64 shell build and receiver resilience suite pass, including
  one-shot abnormal-loss recovery, clean-disconnect, Photos-canvas,
  16:9-rotation, resize-end, and explicit-opt-out coverage.
- The pinned UxPlay core, third-party runtime, persisted settings format,
  receiver identity, and network-safety policy are unchanged.
- Physical Windows 10/11 and iPhone validation is pending. An iPhone may still
  expose a stale discovery row whose first tap never reaches Windows, and a
  session that starts directly in a media canvas can seed the wrong aspect
  until the next session. Same-port native DNS-SD/BLE re-publication remains
  future work.

## 0.12.1 — network-card alignment and tooltip polish

### Fixed

- The network-card title and detail text are vertically centered with the help
  control instead of sitting unevenly inside the card.
- The network help control uses a crisp, centered, DPI-aware question-mark
  glyph so it remains legible at different Windows display scales.
- On a Private physical network, the PIN guidance begins on the second tooltip
  line, keeping the network summary and optional-protection explanation
  visually separate.
- The receiver-state explanation is available when the pointer is over either
  the colored status dot or the adjacent status text.

### Compatibility and verification status

- This is a cosmetic managed-shell review patch. The native UxPlay core,
  receiver lifecycle, discovery/reconnect behavior, settings format, and
  network-safety decisions are unchanged.
- The final managed build and receiver regression suite pass. A focused
  synthetic render confirms the custom help glyph remains centered, crisp,
  and unclipped in light and dark palettes at 100%, 150%, and 200% DPI.
- Physical inspection of the complete settings window on Windows 11 and the
  Windows 10 compatibility smoke remain pending review checks.
- No physical AirPlay behavior is claimed by these interface-only changes.

## 0.12.0 — safer persisted settings and maintainable managed source

### Fixed

- Persisted pairing and selector values are normalized before use. Only the
  supported `none` mode or `pin` with exactly four ASCII digits remains valid;
  obsolete, unknown, or malformed values become unprotected so the existing
  Public/Unknown physical-network policy fails closed instead of starting a
  receiver under a misleading protection mode.
- Settings are written to a temporary file in the same directory and then
  replaced atomically. An interrupted save can no longer expose a partially
  written `settings.ini` as the new configuration.
- A stale end marker from the previous AirPlay session no longer clears a
  newer connection-request or PIN-entry grace period. Deferred settings and
  physical-network maintenance wait for that reconnect attempt instead of
  interrupting its handshake.
- The updater accepts only exact three-part public release tags such as
  `v0.12.0`. Two-part, four-part, suffixed, or otherwise malformed tags are
  rejected rather than interpreted as AeroMirror update versions.

### Changed

- The managed Windows shell is split into focused source files for startup,
  configuration, receiver supervision, rendering, diagnostics, UI, updates,
  network policy, and Win32 interop. These files still compile into the same
  `AeroMirror.exe` assembly and retain the existing namespace, persistence,
  autostart, update, and native-core contracts.
- Three unreachable legacy settings forms were removed after the active UI
  path was separated. No user-facing settings page was intentionally removed.
- The managed build and resilience checks discover all C# files below `src/`
  instead of assuming a single monolithic source file.

### Verification status

- The integrated shell build, resilience suite, review packaging, Setup
  build and lifecycle verifiers, native corresponding-source validation,
  exact-tag release packaging, and whitespace checks pass. All four public
  assets were re-downloaded with matching sizes, SHA-256 values, and GitHub
  API digest fields.
- Physical Windows/iPhone acceptance remains pending for the review candidate.
- The pinned native UxPlay core and third-party runtime are unchanged.

## 0.11.3 — faster reconnects without needless receiver restarts

### Fixed

- A normal mirroring disconnect no longer schedules a full native-receiver
  restart while the receiver is healthy. Its DNS-SD registration and
  listening endpoints remain stable so the iPhone can reconnect immediately
  instead of following a stale advertisement.
- A high-level incoming AirPlay request re-arms the single bounded ten-minute
  idle-discovery fallback and postpones deferred settings maintenance so
  neither can interrupt the handshake. Once mirroring actually starts,
  pending post-session maintenance is cancelled for that session; saved
  settings remain deferred until the next clean disconnect.
- Normal completion and benign feedback warnings do not trigger a full
  restart. Real fatal lost-client and mirror-receive failures retain bounded
  recovery; separately, at most one discovery renewal remains available after
  ten minutes of uninterrupted idle time.
- After iPhone Wi-Fi loss, UxPlay now uses a default lost-client reset bound of
  about six seconds instead of its upstream wait of about fifteen seconds.
  This bounds stale-session cleanup, not end-to-end discovery or connection
  time. An explicit advanced `-reset` argument can still override the default.

### Compatibility

- The reviewed native UxPlay executable and pinned runtime are unchanged from
  0.11.2; this patch updates the AeroMirror shell supervision behavior.

## 0.11.2 — reliable in-place updates

### Fixed

- In-app updates launched from AeroMirror 0.11.0 or 0.11.1 no longer leave
  Setup inside the installed application's working directory. Setup can now
  replace the previous application folder after AeroMirror exits instead of
  failing with a misleading "file is being used by another process" error.
- Setup shutdown is bounded and covers helper executables running from the
  AeroMirror installation directory, reducing transient locks during an
  update without terminating unrelated processes elsewhere on the PC.
- Moving the previous installation now tolerates short-lived file-system
  locks and records actionable attempts in the Setup log. A permanent failure
  still leaves the existing installation intact.

## 0.11.1 — session recovery and reliable discovery

### Fixed

- A stalled session no longer blocks later connections after an iPhone drops:
  AeroMirror waits a bounded amount of time for a normal shutdown, then
  restarts the native core and Bluetooth-beacon process tree.
- Stopping the core can no longer wait indefinitely for a process with
  redirected output. Forced termination is bounded and covers the full child
  process tree.
- After Windows 10/11 starts, AeroMirror now waits for a usable IPv4 address
  on the active physical Wi-Fi/Ethernet adapter. The core no longer starts
  against an empty interface or binds discovery to a temporary VPN address.
- The Bluetooth beacon receives the physical LAN IPv4 address, while the
  native core reports separate DNS-SD and BLE readiness markers. This makes it
  easier to diagnose cases where Bonjour is running but receiver registration
  did not complete.
- Manual discovery refresh now updates the network profile first, and bursts
  of network events are coalesced without the previous long delay.
- Report a problem no longer stacks a notification, Explorer, and the browser:
  AeroMirror selects the redacted log first, opens the GitHub Issue form next,
  and shows progress inside the application window.
- Status and network tooltips now activate only on their small indicators and
  the `?` button, and spacing on the home page is more consistent.
- Setup updates preserve the user's actual Start menu and desktop shortcut
  choices, including legacy shortcut names and the no-shortcuts configuration.

### Diagnostics and validation

- Diagnostic reports now include a compact snapshot of core, socket, active
  session, lost-client recovery, pending restart, and physical-network wait
  state.
- Stable DNS-SD/BLE readiness markers and automated checks were added for
  lost-session recovery, delayed network startup, and shortcut preservation
  during updates.

## 0.11.0 — interface, orientation, and discovery recovery

### Highlights

- The home page is more compact, with a short color-coded status, a concise
  physical-network row, and help available from a dedicated indicator.
- Settings, including theme and connection protection, are applied only after
  the user selects Save.
- If the PIN or another core setting changes during mirroring, AeroMirror
  saves the choice and restarts the receiver safely after the iPhone
  disconnects instead of interrupting the active session.
- A new stream window receives a provisional fit first and is then refined
  using the first exact video size reported by the native core. AeroMirror
  preserves the user's chosen scale across real portrait/landscape changes.
- AirPlay discovery receives bounded self-recovery after a completed session
  and during long idle periods, without repeated restart loops.

### Limitations

- Automatic rotation follows the video dimensions sent by the iPhone. If an
  iPhone app adds its own bars inside the video frame, AeroMirror preserves
  them to avoid cropping screen content.
- Mouse and gesture control of the iPhone is not part of AirPlay Screen
  Mirroring and is not included in this release.
- Changing UxPlay startup parameters still requires a receiver restart. During
  active mirroring, that restart is deferred until the session ends.

## 0.10.0 — review build: stability and diagnostics

This release was prepared for the first public distribution to testers and
for collecting reproducible reports.

### Fixed

- Removed blind Bonjour restarts 15 and 30 seconds after startup.
- Network events are debounced and restart UxPlay only after an actual change
  of the physical Wi-Fi/Ethernet network.
- An unknown network profile is rechecked for a bounded grace period. Without
  a PIN, the receiver fails closed and returns automatically after a safe
  network becomes available.
- Manual discovery refresh performs a full
  stop → short pause → start cycle.
- If readiness cannot be confirmed after the first start, AeroMirror performs
  one controlled full stop/start. A second failure no longer leaves the
  process running with a false green status.
- The shell waits for the previous process to exit before starting another
  instance, preventing two receivers from competing for the same ports.
- Fixed corruption of native UxPlay arguments during the first launch:
  storage for `argv` is now stabilized before pointers are passed to the
  core. This defect could cause unpredictable stalls or crashes.
- Three abnormal exits in one minute disable automatic restart. Persistent
  Windows loader errors (`0xC0000135`, `0xC0000139`, and `0xC000007B`)
  no longer trigger a useless retry loop.
- A normal Windows autostart no longer displays a receiver-started
  notification.
- Public/unknown-network warnings without a PIN are still shown during hidden
  startup, and the global notification setting now also covers core errors.
- A single left click on the tray icon opens AeroMirror.
- Scrolling over a closed list moves the page instead of changing the
  selection.
- Mouse-wheel handling moved to the Win32 level, so quality, latency, audio,
  protection, and theme values no longer change before WinForms receives the
  event. An open list closes when the page is scrolled instead of floating
  away from its field.
- Leaving a page with unsaved changes now offers Save, Discard, or Continue
  editing.
- Automatic fitting runs once and no longer forces landscape photos and
  videos back into portrait proportions.
- Manual fitting reuses an already detected UxPlay window and retries window
  discovery, so it does not depend on a temporary renderer-title change.
- Network-profile parsing now fails closed: physical Wi-Fi/Ethernet and
  VPN/virtual profiles are separated explicitly, the result is passed as
  JSON, and an unknown or malformed category can no longer be treated as
  trusted.

### Diagnostics

- `receiver.log` records UxPlay stdout/stderr, PID, stop/restart reason,
  shell version, Windows version, and physical-network details.
- PIN values are masked as `****`; the log rotates automatically after 5 MB.
- Added troubleshooting instructions and a GitHub Issue template for testers.
- Report a problem is available on the home page and in the tray. It creates a
  separate redacted log copy, opens a prefilled GitHub Issue, and selects the
  file for manual attachment. Logs are never uploaded automatically.
- Random UxPlay PINs, user-profile paths, MAC addresses, and network names are
  also removed before logs are written; existing logs are sanitized at
  startup.
- A separate `setup.log` records installation and update errors. Labelled
  cryptographic material in detailed UxPlay output is redacted.

### Review distribution

- The public Setup was reduced to a network review installer. It downloads the
  unchanged pinned third-party runtime from the upstream GitHub Release and
  accepts it only when its SHA-256 matches.
- Before replacing an installed version, Setup runs a separate loader test
  against the built core inside the downloaded runtime and cancels the
  installation if its DLL or Qt dependencies are incompatible.
- Setup waits for the shell, core, and Bluetooth beacon to exit fully before
  replacing the application directory, and restores the previous version if
  installation fails.
- Updates preserve the user's shortcut selection. If replacement fails, Setup
  restores the previous shortcuts, uninstall registration, and autostart
  configuration together with the old application directory.
- After SHA-256 verification, Setup stores the pinned upstream runtime in a
  content-addressed cache. Reinstallation and later updates using the same
  runtime reuse the cache, verify it again, and still run the loader test.
- AeroMirror source and the complete modified GPL core source tree are
  published alongside Setup. The offline portable package remains unpublished
  until the complete Qt/GStreamer/FFmpeg/MSYS2 SBOM review is finished.
- The public native core is built with deterministic path rewriting and
  without debug sections, so local user names and build paths are not embedded
  in it.
- The native-source archive includes both patches separately and already
  applied, plus `source-provenance.json`. Its rebuild script verifies patch,
  modified-source, and build-input hashes, generates `dnssd.lib` from the
  reviewed `dnssd.def`, and rejects a core with a different final SHA-256.
- The built-in updater accepts only a Setup asset named exactly
  `AeroMirror-Setup-<MAJOR.MINOR.PATCH>.exe` for the GitHub Release version;
  a similarly named or incorrectly versioned file is not launched.

### Interface

- The home page is more compact: status fits beside the product name, settings
  use a small gear button, and update checking shares a row with receiver
  actions.
- Added a short description of the main workflow: configure AeroMirror once,
  then let it start automatically and wait in the tray.
- The PIN suggestion disappears after configuration and can also be dismissed
  manually.
- Improved dark-theme contrast and readability.

## 0.9.0 — Windows 10, updates, and visual design

### Highlights

This release renamed the application to AeroMirror and prepared it for normal
installation and future updates. It supports Windows 10 1809+, updates an
existing installation in place, and shows a clear description of a new GitHub
Release before downloading it.

### Should I update?

- Yes, if version 0.7 or 0.8 is installed: Setup replaces it, preserves
  settings, and can restore the previous files if updating fails.
- Yes, if you use the Windows dark theme or want to choose light/dark
  appearance manually.
- Optional, if the installed version already works for you and you do not need
  the new features.

### What changed

- Added: Follow Windows, Light, and Dark appearance modes.
- Added: a GitHub update-check page with release notes.
- Added: the official `Nadejny/aeromirror` update channel.
- Added: SHA-256 verification of a downloaded installer before launch.
- Changed: the stream window appears on the taskbar by default again and can
  be hidden through settings.
- Changed: the AirPlay rediscovery action has a clearer name.
- Fixed: Setup detects and updates an earlier installation in place instead of
  creating a second entry in Installed apps.
- Fixed: Setup closes before the main application opens.
- Fixed: lists and fields render correctly in the dark theme.
- Clarified: the selected quality is guaranteed to apply to the next iPhone
  connection.

### Limitations

- Setup is unsigned, so SmartScreen may warn about an unknown publisher.
- Before the first GitHub Release is published, update checking reports that
  no releases are available.
- Instant incoming-quality changes without reconnecting the iPhone are not
  supported by the current UxPlay core.

# Project state

Last updated: 2026-08-25

This is the single current-state handoff for AeroMirror. Keep it concise and
update it whenever release status, accepted tests, blockers, or the immediate
next step changes.

## Latest public review release — 0.12.20

- Trigger: physical testing of public 0.12.19 confirmed that its separate
  shell-owned fullscreen control visibly lagged during window movement,
  appeared inconsistently, and did not make Escape dependable. The same run
  described the portrait Photos presentation as cropped and the update/repair
  confirmation flow as too repetitive.
- Viewer ownership: the separate top-level overlay and managed keyboard hook
  are removed. The native Qt wrapper owns one framed viewer and child video
  surface. Caption fullscreen, Escape, Alt+Enter, and exact shell requests use
  one idempotent GUI-thread setter and emit requested/actual/result/generation/
  source acknowledgements. Caption Close is a minimize-equivalent for the
  active renderer generation; the minimized HWND remains discoverable by the
  explicit tray restore action even when taskbar policy uses
  `WS_EX_TOOLWINDOW`. Fullscreen entry from minimized captures Qt's current
  normal geometry, and a delayed fullscreen request cannot show a lifecycle-
  hidden empty host. Stop/destroy alone clears requested visibility and hides
  the host before the next session.
- Photos boundary: the exact `3840x2160` canvas remains non-authoritative and
  neutral scale. The native sink explicitly keeps aspect-ratio containment and
  receives no render/crop rectangle. Keeping a portrait outer window can
  produce substantial letterboxing; a large guaranteed non-cropped portrait
  photo still requires trustworthy content bounds that AirPlay has not exposed.
- Latest user annotation shows the symptom more precisely: a portrait outer
  viewer contains a horizontal inner Photos region, and phone-side zoom remains
  constrained to that region. This is useful physical evidence, but the exact
  public 0.12.20 Setup still needs to be retested before any Photos acceptance
  claim.
- Update UX: the user's **Download and install** click is the sole application
  confirmation. Existing unsaved settings are resolved before the download,
  update-page navigation is locked during handoff, and exact-name/digest
  verification remains, but no second Yes/No appears before the existing
  unattended Setup transaction.
- Bonjour UX/security: a missing exact firewall rule appears on the main
  network card and proceeds directly to one Windows UAC prompt. Success
  refreshes discovery without a success modal. The assessment expires after
  two minutes and is refreshed on receiver start, restart, and manual discovery
  refresh. Unavailable or incorrectly installed Bonjour is reported as a
  prerequisite; the headless core does not show install dialogs or register a
  bundled per-user executable as a system service.
- Unicode runtime: all nine GStreamer/scanner/GIO/font/PATH values are written
  with `SetEnvironmentVariableW`; rejection emits one stable marker and exits
  instead of starting a partial runtime. The same staged bytes pass
  `--self-test` with separate fresh registries from both ASCII and Cyrillic
  application paths. Setup runs `--loader-test` before committing installation;
  the broader self-test is scoped to the staged bundle rather than the thinner
  upstream-runtime layout assembled by the network installer.
- Version: shell/Setup source targets `0.12.20.0`, Setup comparison 0.12.20,
  and exactly five script defaults target 0.12.20.
- Automated status: managed build, complete resilience, focused Bonjour,
  native-host/core/lifecycle contracts, and `git diff --check` pass. Two clean
  CRLF native builds complete all 57 targets and reproduce core SHA-256
  `4336B9DBFCDE87123EC4796FE43FAA4F1952E27224932B3DD5E8FEAFBAD41832`.
  Runtime staging inspects 199 binaries and copies 148 DLLs; its full
  self-test passes, while Setup `/verify-runtime` checks loader compatibility.
  The 148-entry corresponding-source ZIP extracts without Git
  metadata and rebuilds 57/57 to the same core hash. The final review payload,
  x64 Setup, embedded equality, shortcut-selection, and update-lifecycle gates
  pass. Exact-tag Setup remains x64 `0.12.20.0`; the final corresponding-source
  archive also rebuilds 57/57 from an extracted no-Git tree to the same core
  hash.
- Discovery log review: the installed public build kept PID 1136 and AirPlay
  port 62004 while idle renewals 6 through 11 completed `READY` from 17:10
  through 18:50 on 2026-08-24, each followed by `AEROMIRROR_DNSSD_READY`, with
  no error/fatal/crash line in that interval. This proves local renewal
  completion, not that the iPhone received the multicast advertisements.
- Current post-update discovery blocker: the public 0.12.20 in-place update
  completed successfully at 11:51 on 2026-08-25 and relaunched the exact
  `0.12.20.0` shell, but `Bonjour Service` had already crashed and remained
  `Stopped` with Windows exit code 1067. The new core opened its TCP listener
  and the BLE helper reported ready, while every DNS-SD attempt returned
  `error=-65563` and `AEROMIRROR_DNSSD_DEGRADED`; the receiver was not visible
  on the iPhone. This is not evidence that the updater caused the failure, but
  it confirms that post-update/startup UX needs an explicit safe path for a
  crashed external Bonjour prerequisite rather than relying on core restarts.
- Physical status: Photos edges/letterboxing, caption/Escape/Alt+Enter/tray,
  Caption Close/minimize with both taskbar policies, DPI/multi-monitor restore,
  installed update/reinstall, UAC decline/approval, and long-idle iPhone
  visibility remain PENDING in
  `docs/releases/0.12.20/TEST_PLAN.md`.
- Post-freeze observation: with AirPlay still active, the phone was locked and
  the viewer was then closed; later connection-loss/recovery events showed the
  window again several times. This has not yet been reproduced against the
  exact public Setup. A future correction should evaluate a per-session
  user-dismiss latch that only a new session or explicit **Show stream window**
  action clears; no such change is included in the immutable 0.12.20 tag.
- Publication: annotated tag `v0.12.20` resolves to commit
  `288b8976d413861ab77bf1721e20f047e0480952`. Normal GitHub Release
  `376224221` is `draft=false`, `prerelease=false`, and updater-visible latest.
  Exactly four assets, `SHA256SUMS.txt`, GitHub API digests, canonical/legacy
  latest routes, and fresh public re-download equality pass. Exact evidence is
  in `docs/releases/0.12.20/BUILD_REPORT.md`; immutable public `v0.12.19`
  remains unchanged.
- Immediate next step: retain the current failure evidence, diagnose the
  external `mDNSResponder.exe` crash and define a safe explicit recovery path;
  then continue the Photos/fullscreen/close/update matrix plus long-idle iPhone
  browse checks against the public Setup.

## Previous public review release — 0.12.19

- Trigger: physical testing on another PC reports that 0.12.18 can crop the
  Photos image, a normal Escape press can be missed, and fullscreen has no
  visible renderer-window control. These are accepted failure reports even
  though the PC inspected on 2026-08-24 was separately found to be running
  installed 0.12.17.
- Gallery behavior: the exact Photos transport signature is not a content
  rectangle. The managed 3.85x cover/fill transform is removed; the complete
  frame remains contained while the outer window can retain its trusted phone
  orientation. Letterboxing is safer than unverified pixel loss.
- Fullscreen behavior: one shell-owned titlebar-adjacent control is added
  without cross-process subclassing and replaces 250 ms key-state sampling
  with a bounded event hook installed only during actual fullscreen; capture also
  requires the renderer PID/root to own foreground and never consumes Escape.
- Discovery evidence: a later installed 0.12.18 run from 13:06 through 15:03
  did not crash. Same-PID/same-port renewals at 14:16 and 14:36 completed
  `READY`; a manual restart at 14:53 and the next 15:03 renewal also completed.
  No iPhone session reached the core. The physical Ethernet profile was Private,
  Bonjour/BLE/sockets were locally ready, but read-only inspection found no
  exact inbound Private/UDP 5353/LocalSubnet rule for `mDNSResponder.exe`.
  This is the strongest observed boundary, not proof of a captured packet drop.
  Version 0.12.19 diagnoses that exact condition and offers an explicit
  UAC-gated narrow repair.
- Maintainability: renderer invariants move to a typed policy; the updater's
  unreachable filename compatibility helper, duplicate scale literal, unused
  imports, unreachable network branch, and Russian-text readiness inference
  are removed. A broader ReceiverContext/SettingsForm decomposition remains a
  staged follow-up rather than being mixed into this corrective patch.
- Version: shell/Setup source targets `0.12.19.0`, Setup comparison 0.12.19,
  and exactly five script defaults target 0.12.19.
- Publication: annotated `v0.12.19` resolves to commit
  `997e29324ec092ad46ae83a00fa6d08525c1b863`. Normal latest GitHub Release
  `375664260` is `draft=false`, `prerelease=false`, and contains exactly
  four immutable assets. Canonical/legacy latest routes, API digests, checksum
  entries, and fresh public re-download equality pass.
- Automated/package status: managed build, complete resilience, focused
  firewall contracts, native contracts, eight-scenario worker lifecycle,
  unchanged core hash, corresponding-source build, final review payload, x64
  Setup, embedded equality, and non-installing self-checks pass. The standalone
  discovery-pipe harness was not run because an installed receiver owned its
  machine-wide BLE status file; the delivered native core executable is
  byte-identical to 0.12.18.
  Exact identities are recorded in
  `docs/releases/0.12.19/BUILD_REPORT.md`.
- Pending: installed update/reinstall, physical gallery/fullscreen/DPI,
  explicit UAC firewall repair, and long-idle iPhone visibility remain PENDING
  in `docs/releases/0.12.19/TEST_PLAN.md`.
- Immediate next step: install the immutable public Setup, explicitly repair
  the missing narrow Bonjour rule if offered, and record the one-hour/physical
  matrix without moving the tag or replacing any asset.

## Previous public review release — 0.12.18

- Status: published as an immutable prior normal-channel review Release. Annotated
  `v0.12.18` resolves to commit
  `419ed6b199e89cf3c01efa6728f64423d9f049ed`; GitHub Release `373984443`
  is `draft=false` and `prerelease=false`; 0.12.19 is now latest. Public
  `v0.12.17` and every earlier tag/asset remain immutable.
- User behavior: the tray exposes one direct **Полный экран (Esc — выйти)**
  action. The three incremental photo-zoom controls are removed. The exact
  observed Photos `3840x2160 aux=0x0 encoded=3840x2160` transport canvas uses
  the last trusted portrait phone shape, or a `900x1950` fallback when a
  session starts directly in Photos, and one centered automatic uniform fill.
- Fullscreen ownership: the shell detects the actual borderless monitor-sized
  D3D11 window. While it is active, automatic/manual fitting, saved-placement
  restore/write, continuity bounds capture, and normal policy mutation are
  suspended. Scale is 100% in fullscreen and the current normal Photos scale is
  reapplied after exit. Foreground Esc requests the same native toggle as the
  tray; Alt+Enter remains supported by the sink.
- Boundary: portrait fill is geometry-based and limited to the exact known
  Photos canvas plus a portrait target. It does not inspect pixels, infer an
  arbitrary content rectangle, rotate the stream, rewrite media, or crop a
  trusted landscape device target.
- Evidence: managed x64 Release and complete `ReceiverResilience` pass; the
  eight-case worker harness, native parser/transport/SETUP/renderer contracts,
  and crypto check pass. Two clean compatible 57/57 builds reproduce core
  SHA-256
  `C217386CBC916F8889A9C03774390FE7EC7D8C7EE0B6F64358215CACEEB35118`.
  Runtime staging inspects 199 binaries and copies 148 DLLs, resolves 44
  requested features to 27 plug-ins, and the loader test exits 0. Wrapper patch
  SHA-256 remains
  `8F48A4E72D765B0549119BC6366CB970384BAB8116B4430CE60ED67228213F9C`;
  libuxplay patch SHA-256 is
  `11330A0D905CF4480958DAA59B950F3A2CE2B4AD51A18563EBCC77924DD782C4`.
- Corresponding source: the final 147-entry ZIP is 829,835 bytes with SHA-256
  `F4B7F53CABB67E45E6497A8109A87841ED7FE06DBE3409F0E1EC95FF06EFDDFE`.
  Its extracted no-Git tree verifies all pinned inputs and cleanly rebuilds
  57/57 to the reviewed core hash.
- Version: shell/Setup source targets `0.12.18.0`, Setup comparison 0.12.18,
  and exactly five script defaults target 0.12.18.
- Package/Setup: the exact tagged 13-entry review ZIP is 1,179,937 bytes with
  SHA-256
  `E9250D3061E01471887E8C061A1E80F45CE203D216B71E6A8E2C6B91D7F1242F`.
  Packaged-shell resilience and shell/core/provenance equality pass. The x64
  `0.12.18.0` Setup is 1,412,608 bytes with SHA-256
  `93AA1A871A4A3AC0FC22A6960F4B4EC6D577DE5F32B42002656FB14E153E0174`;
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` each exit 0.
- Publication: the clean tag produced exactly four public assets. Their local
  SHA-256 values, `SHA256SUMS.txt`, GitHub API digests, canonical and legacy
  latest routes, and fresh unauthenticated public downloads all agree. Exact
  identities are recorded in `docs/releases/0.12.18/BUILD_REPORT.md`.
- Pending: installed update and physical portrait/landscape Photos transitions,
  repeated fullscreen/Esc/Alt+Enter, Camera/rotation, motion/audio/Stop, and
  long-idle discovery acceptance.
- Immediate next step: install the immutable public Setup when authorized and
  record the physical gallery/fullscreen matrix without moving the tag or
  replacing any asset.

## Previous public review release — 0.12.17

- Status: published as the normal updater-visible review Release. Annotated tag
  `v0.12.17` resolves to commit
  `16dffd57f7f105e6bdb90ef95137fc00f5282a68`; GitHub Release
  `373934492` is `draft=false` and `prerelease=false`. It remains immutable
  after `v0.12.18` became the normal latest Release.
- User behavior: the tray exposes native fullscreen plus 100–250% uniform zoom
  for only the exact observed Photos `3840x2160 aux=0x0 encoded=3840x2160`
  media canvas. Zoom is explicit, session-scoped, and resets when that class
  ends or a new renderer session starts.
- Ownership: the managed shell serializes command writes and the headless
  wrapper accepts a narrow command grammar. Libuxplay attaches presentation
  work to the active GLib context and takes a retained selected D3D11 sink
  reference before changing fullscreen or equal X/Y scale properties.
- Evidence: managed Release and complete resilience pass; the worker and native
  core/crypto contract suites pass. Two clean compatible 57/57 builds reproduce
  core SHA-256
  `53B13433B9308547D491417F11692361DFC5B6EBFBDA018B8D3EEE7B4436436F`.
  Staging checks 199 binaries and 148 DLLs, maps 44 requested features to 27
  plug-ins, and the loader test exits 0. Wrapper patch SHA-256 is
  `8F48A4E72D765B0549119BC6366CB970384BAB8116B4430CE60ED67228213F9C`;
  libuxplay patch SHA-256 is
  `91AF80A36C7D4ECEB6470A1394722F2EC98312407DFA51A9929FC40E4B220CF5`.
  The exact-tag 147-entry corresponding-source ZIP is 828,175 bytes with
  SHA-256
  `369497BB96F94EB2104A9741EDF36638D3EC29AAE17C3B433A95EC7F865AC86C`;
  its no-Git tree validates every pinned hash and rebuilds 57/57 to the same
  reviewed core. The exact 13-entry review payload, packaged-shell resilience,
  x64 `0.12.17.0` Setup, byte-exact embedding, and all three non-installing
  Setup self-checks pass. Clean-tag release packaging produces exactly four
  public assets; `SHA256SUMS.txt`, GitHub API digests, canonical and legacy
  latest routes, and fresh public re-download byte equality all pass. Exact
  publication evidence is in
  `docs/releases/0.12.17/BUILD_REPORT.md`.
- Boundary: fullscreen enlarges the outer renderer only. Manual zoom crops the
  iPhone-provided canvas and cannot distinguish real dark content from encoded
  bars. Automatic crop, pixel inspection, and `rotate-method=auto` remain off;
  Camera orientation and the physical Photos result are unresolved.
- Version: shell/Setup source targets `0.12.17.0`, Setup comparison 0.12.17,
  and exactly five script defaults target 0.12.17.
- Pending: installed update/reinstall and the physical Photos, Camera,
  fullscreen, zoom, rotation, and long-idle discovery matrix.
- Immediate next step: install the immutable public Setup when authorized and
  retain physical fullscreen/zoom/rotation plus discovery evidence without
  moving the tag or replacing any asset.

## Prior public review release — 0.12.16

- Status: published prior persistent idle-discovery review
  Release. Annotated tag `v0.12.16` resolves to commit
  `c012d51d5cf3194fd647c4c65c20659386043baf`; GitHub Release `373875353` is
  `draft=false` and `prerelease=false`. It remains immutable after
  `v0.12.17` became the normal latest Release.
  AeroMirror now keeps the automatic same-process DNS-SD refresh schedule
  active for the lifetime of an idle receiver instead of stopping after two
  attempts.
- Schedule: the first eligible re-registration remains ten minutes after a
  fresh idle epoch. Every later terminal result rearms a 20-minute recurring
  deadline. Active mirroring, incoming AirPlay/PIN grace, startup readiness,
  restart work, and an unknown physical IPv4 defer rather than consume due
  maintenance.
- Ownership: the capable frozen 0.12.15 core still replaces the paired RAOP and
  AirPlay registration generation in the same PID and on the same listener
  ports. A guarded Windows unlock can request another refresh after any prior
  renewal. Incoming AirPlay activity or a real network-profile change starts a
  fresh ten-minute epoch.
- Failure boundary: only automatic attempts one and two may fall back to the
  historical full receiver restart when the native command cannot complete.
  Attempt three and later leave the listening core alive and rearm the
  20-minute schedule. Manual **Restart discovery** and a physical IPv4 change
  remain deliberate full DNS-SD-and-BLE process restarts.
- Installed update behavior: after the user confirms an update in AeroMirror,
  Setup runs without the shortcut/launch option form, preserves the exact
  existing Start menu and desktop shortcut state, and relaunches the shell. A
  newer Setup opened manually and a same-version reinstall use that same path.
  Clean first install remains interactive, and automatic downgrade is refused.
- Source targets app/Setup `0.12.16`, Windows PE/file `0.12.16.0`, Setup
  comparison 0.12.16, and exactly five release-script defaults. Native source,
  patch, executable, and provenance remain the frozen 0.12.15 inputs: core
  SHA-256
  `38C6A63CE3CA40D3D1E23E5ECB5E0D152F9978986C4384A780C5767EAE0650A4`
  and libuxplay patch SHA-256
  `E8233FFD59BFC49181D32BBD64A6C94A338FD31939B28A18C7FC7A3B5F14195D`.
- Automated status: the managed Release build and complete
  `ReceiverResilience` suite pass. The deterministic policy checks cover the
  10-minute first delay, indefinite 20-minute recurrence, counter saturation,
  Windows unlock, anti-churn, active-session/client-grace deferral, readiness,
  the first-two-attempt legacy restart boundary, and the unattended
  update/reinstall decision. Setup's shortcut-selection self-check covers fresh
  install, update, reinstall, shortcut absence/presence, and downgrade refusal.
- Live native status: the redirected-pipe refresh passes in PID 30188 on
  unchanged RAOP/AirPlay port 45023 for request 98569.
- Package/Setup status: the focused final local gate after the unattended-
  update change passes. The review ZIP contains exactly 13 expected entries;
  its packaged shell is x64 `0.12.16.0` and passes complete resilience. The x64
  `0.12.16.0` Setup embeds the current payload/provenance exactly and
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` each exit 0. The corresponding-source workflow
  also passes. Clean-tag packaging, the exact four-asset set, checksum/API
  digest checks, canonical and legacy latest routes, and fresh public
  re-download byte equality pass. Exact evidence is recorded in
  `docs/releases/0.12.16/BUILD_REPORT.md`.
- Physical status: persistent local registration is not proof that an iPhone
  continuously lists the receiver. A real installed 0.12.16 run must cover at
  least two hours idle, repeated iPhone browse checks, lock/unlock, sleep/wake,
  successful mirroring, and manual recovery before visibility is accepted.
- Immediate next step: keep the published tag and assets immutable and run the
  separately authorized real installed-update/reinstall and two-hour physical
  Windows/iPhone visibility matrix. This publication run did not install or
  replace the existing AeroMirror on this PC.

## Prior frozen internal candidate — 0.12.15

- Status: internal pretag supported-default native-core hardening candidate.
  A fresh complete CMake/Ninja native build, the exact production crypto
  happy-path harness, the eight-case production worker-lifecycle harness, and
  source-bound protocol/parser/renderer contract checks pass. Independent
  frozen-source review reports no P0/P1 finding in the default mirroring path.
  Patch/provenance materialization, two clean compatible native builds, final
  corresponding-source creation and extracted rebuild, staged runtime/loader,
  fresh managed build, complete receiver resilience, and live discovery-pipe
  gates also pass. The initial exact review-payload and Setup gate passes.
  The focused final review-payload and Setup rebuild also passes against the
  frozen embedded documentation. Installed update, physical Windows/iPhone,
  exact-tag, GitHub Release, and public re-download gates remain pending.
- Source targets app/Setup `0.12.15`, Windows PE/file `0.12.15.0`, Setup
  comparison 0.12.15, and exactly five release-script defaults. It was
  superseded without being tagged; public `v0.12.16` is now the immutable
  normal latest review Release. Internal 0.12.10–0.12.15 source and artifact
  history is not tagged, reconstructed, or relabelled.
- Worker lifecycle: mirror, HTTP, audio RTP, and NTP use one explicit
  running/joining/joined contract. Natural exit preserves join debt, concurrent
  stop has one join owner, self-stop defers its join, start failure rolls back,
  and restart cannot overtake the previous worker tail. Accepted mirror and
  HTTP sockets explicitly restore blocking mode and use Windows-correct
  timeouts and lifecycle-aware retry loops.
- Protocol boundary: bounded mirror and HTTP parsing, strict SETUP mode and
  field validation, atomic mirror/timing/audio publication, RTP/NTP source and
  length checks, transactional buffers, checked allocation, and recoverable
  crypto status replace false success, unchecked input, and process-exit
  failure paths across the supported default receiver flow.
- Media recovery candidate: after full decryption and NAL validation, a valid
  type-0 video access unit proves sender activity. If the stream is still
  marked suspended, the core requests one nonblocking implicit resume and
  delivers that same access unit. No leaky/max appsrc experiment remains, so
  the candidate does not intentionally discard a recovery frame.
- Renderer ownership: video operations take lock-protected GStreamer
  references before timestamp work; bus callbacks map to their owning
  renderer; final teardown waits for callbacks already holding it; and unused
  codec objects remain alive until final destruction. Audio bus handling is
  likewise mapped to the originating pipeline. These changes correct
  source-level lifetime races but do not prove the physical freeze is gone.
- Native evidence: both clean compatible builds reproduce executable SHA-256
  `38C6A63CE3CA40D3D1E23E5ECB5E0D152F9978986C4384A780C5767EAE0650A4`.
  The materialized libuxplay patch SHA-256 is
  `E8233FFD59BFC49181D32BBD64A6C94A338FD31939B28A18C7FC7A3B5F14195D`;
  provenance pins 37 libuxplay files and 41 patched source files in total.
  The final 147-entry corresponding-source ZIP is 826,213 bytes with SHA-256
  `DA95EC58A17C37DA53948F770DABEAF29FAD75405CDF69F005F84ACF56362EB7`.
  Its no-Git extracted tree validates every pinned hash, and a clean 57/57
  rebuild reproduces the same core SHA-256.
- Runtime and managed evidence: staged inspection covers 199 binaries, 148
  DLLs, and 44 requested GStreamer features resolving to 27 plug-ins. A manual
  staged `--loader-test` exits 0. The fresh managed build and full
  `ReceiverResilience` suite pass after its D3D11 textual contract was updated
  to the new reference-safe snapshot; product implementation was unchanged by
  that test correction. The live discovery-pipe case passes with the same PID
  38712 and AirPlay port 43214 for request 98569.
- Initial package evidence: the thin ZIP contains exactly 13 entries, is
  1,169,388 bytes, and has SHA-256
  `2123412734FD089F1B65A41DC0451A8105349BED5778B53211340A997500141C`.
  The packaged shell is 753,152 bytes with SHA-256
  `330EA373212FA0C47B0C25747DACF3F45A27959D56F6643569AD13889E606B81`,
  equals the current shell, and passes the complete resilience suite.
- Initial Setup evidence: the x64 `0.12.15.0` Setup is 1,397,760 bytes with
  SHA-256
  `BCFFBC8BAE6453A437783A82A6EB307C701CA422A2DBDC5019E3E7F0D6A397E7`.
  Its embedded payload is byte-exact, and `/verify-runtime`,
  `/verify-shortcut-selection`, and `/verify-update-lifecycle` each exit 0.
  These remain the initial prepackage identities, not the final focused-build
  hashes.
- Focused package evidence: `package-review` passes again with exactly 13 ZIP
  entries. The packaged shell matches the built shell byte-for-byte, and the
  complete `ReceiverResilience` suite passes from a fresh PowerShell process.
  Setup rebuilds as x64 `0.12.15.0`; its embedded payload and
  `source-provenance.json` hashes exactly match the reviewed inputs, and
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` each exit 0. Final focused artifact sizes and
  SHA-256 values are intentionally left for the post-documentation gate
  handoff; no embedded source document changed after this rebuild.
- Physical status: no 0.12.15 device run has occurred. The last retained device
  evidence is still the installed 0.12.13 run: one H.265 picture appeared and
  then froze while the native process and control session stayed responsive,
  and iPhone Stop ended the PC session immediately. A current log must show
  `AEROMIRROR_VIDEO_IMPLICIT_RESUME`, health progression, and real visible
  motion together before the freeze can be called fixed.
- Scope boundary and P2 backlog: Photos inner-content detection/crop and Camera
  rotation remain unresolved. Terminal join-failure parent lifetime, broader
  audio/HLS synchronization, remaining startup assertions, optional PIN/SRP
  depth, and consolidation of tolerant dual teardown paths remain explicit P2
  follow-up. Long-idle discovery, in-process BLE refresh, AWDL/peer-to-peer
  AirPlay, AirDrop, Windows 10 first-install diagnosis, and signing also remain
  separate.
- Former next step: the frozen 0.12.15 candidate was ready for its physical
  plan, but the later persistent-discovery correction advanced the working tree
  to 0.12.16. Its source and artifacts remain exact internal history and are
  not relabelled or published.

## Prior frozen internal candidate — 0.12.14

- Status: superseded internal pretag media-liveness diagnostic candidate. Two independent
  clean compatible native builds, an extracted prepared-source rebuild,
  patch/provenance, runtime/loader, live redirected-pipe, and complete receiver
  resilience gates pass. The exact 13-entry local payload, packaged-shell
  resilience, Setup embedded-input equality, and all three Setup self-checks
  pass. Version/default, documentation-link, strict-UTF-8, no-added-BOM,
  stale-claim, diff, and stable-input gates pass. Installed update, physical
  Windows/iPhone, exact-tag, GitHub Release, and public re-download gates remain
  pending.
- Its frozen source targets app/Setup `0.12.14`, Windows PE/file `0.12.14.0`, Setup
  comparison 0.12.14, and exactly five release-script defaults. Public
  `v0.12.9` remains the immutable normal latest review Release. Internal
  0.12.10–0.12.13 history is not tagged, reconstructed, or relabelled.
- Confirmed correction: every video retry derives from one immutable raw remote
  timestamp through a signed, overflow-checked mapping. Session and clock-epoch
  validation rejects stale corrections, while audio and video use independent
  clock state. This fixes a cumulative retry defect in source; it does not prove
  that the defect caused the reporter's frozen frame.
- Diagnostic change: an active mirror session emits one fixed, numeric,
  content-free `AEROMIRROR_VIDEO_HEALTH` summary every two seconds. Session and
  geometry generations correlate VCL/config/action ingress, appsrc input/flow,
  sink and Direct3D 11 Present progress, timestamp outcomes, pipeline states,
  monotonic ages, and per-interval deltas. The legacy managed geometry line is
  unchanged.
- Scope boundary: the classifier is observational. It logs no pixels, payloads,
  artwork, titles, paths, or URLs and performs no automatic pause/resume, reset,
  restart, reconnect, crop, pixel analysis, or other recovery action.
- Native evidence: two clean builds and the extracted prepared-source rebuild
  reproduce SHA-256
  `5A6C8AEBC381F6090AD87CBB622A370B1BA0F29923B387C72C2AE07D78605F36`;
  reviewed libuxplay patch SHA-256 is
  `4B2AAF2C8B48BD3B993940011678DD25919C16788E1B061D733469463D4217EE`.
- Physical status: no 0.12.14 device run has occurred. The 0.12.13 evidence
  remains the last physical result: one H.265 frame appeared, then the last
  picture froze while the process, RTSP/control session, and mirror parser
  stayed alive; iPhone Stop still ended the PC session immediately. Therefore
  freeze correction, root cause, and real media recovery remain pending.
- Audit status: this is the first focused defect/diagnostic slice from the
  broader native-core audit, not completion of that audit. A separate P1 gap in
  natural mirror-worker exit/join ordering is the first follow-up; it is not
  fixed here. Photos inner-content/crop, Camera orientation, physical idle
  discovery, BLE in-process refresh, AWDL/peer-to-peer AirPlay, and AirDrop also
  remain separate.
- Its former immediate next step was the focused 0.12.14 physical plan. That
  plan is superseded by 0.12.15; the frozen 0.12.14 artifacts are not
  relabelled, tagged, or published, and no 0.12.14 `BUILD_REPORT.md` exists.

## Prior frozen internal candidate — 0.12.13

- Status: verified local pretag persistent-discovery candidate. Two
  clean official native builds, an extracted prepared-source rebuild, loader,
  four real redirected-pipe cases, exact patch/provenance/process-lifetime
  audits, a fresh managed x64 build, the complete resilience suite, frozen-
  source independent review, native-source, exact thin-payload, packaged-shell,
  Setup/lifecycle, architecture/version/default, all 59 source-Markdown local
  links, strict-UTF-8/no-added-BOM, diff, and release-input fingerprint gates
  passed with no blocker in the persistent-discovery scope. The later full
  native media audit found the separate P1 items recorded below. The focused
  post-evidence payload/
  Setup rebuild also passes. Installed update, physical-device, exact-tag,
  GitHub Release, and public re-download gates remain pending.
- Local app/Setup source version is `0.12.13`; Windows PE/file source version is
  `0.12.13.0`. Setup's comparison version and exactly five release-script
  defaults target 0.12.13. Public `v0.12.9` remains the immutable normal latest
  review release; 0.12.10–0.12.12 remain untagged local history.
- Normal automatic idle, eligible Windows-unlock, and persistent native
  discovery-health maintenance now prefer a request-correlated in-process
  refresh of the paired `_raop._tcp` and `_airplay._tcp` records. Current-
  generation callbacks must complete for both before ready. Success preserves
  the operating-system PID and both listener ports.
- DNS-SD identity/TXT data now has service lifetime, each partial pair rolls
  back before retry, Bonjour callbacks run on the owner GLib context, and
  retries use bounded 1, 2, 5, 10, and 30 second delays. Active connection/PIN,
  audio/video clients, or mirroring defer the operation without listener
  teardown.
- A narrow version-1 redirected-stdin/framed-stdout protocol carries exact
  request, generation, PID, RAOP port, and AirPlay port. The managed shell
  publishes pending state before writing, revalidates process identity, and
  rejects stale, malformed, wrong-PID, wrong-port, or phase-regressing output.
  Unsupported, rejected, timed-out, or repeated failed refreshes retain the
  existing bounded full-process fallback.
- **Restart discovery** remains a full process restart after the latest
  physical-network check so it refreshes both DNS-SD and the separate BLE
  helper. A real physical IPv4 change also remains a full restart because BLE
  receives its advertised address at startup. BLE output is now complete-line
  stderr framing; unexpected start/exit is reported once, while intentional
  maintenance stop is not falsely reported as failure.
- Receiver names canonicalize to at most 50 UTF-8 bytes: complete managed text
  elements, no C0/DEL controls, replacement for unpaired UTF-16, trimmed input,
  and `AeroMirror` fallback. Interactive save informs and persists the exact
  iPhone-visible value; legacy long stored names migrate silently. Native code
  independently enforces a complete-code-point boundary so the 12-character
  device ID plus `@` and name remain within the Bonjour 63-byte label. AirPlay,
  RAOP, and `/info` share the canonical name; diagnostics log lengths only.
- Native evidence: both official builds, extracted prepared-source build, and
  staged executable reproduce SHA-256
  `AD59F33907980122551458E5B97CE600D6AB8DBFF923B7BEE5EB30A26F521698`.
  The real pipe cases include same-PID/same-port refresh plus ASCII, Cyrillic,
  and fallback naming. Runtime/dependency, reverse-apply, protected unchanged
  audio source, local-path/debug, and complete source-diff checks pass.
- Initial package evidence: the final staged runtime contains that exact core;
  dependency inspection covers 199 binaries and 148 staged DLLs. Prepared
  corresponding source has exactly 143 archive entries/139 files, and its
  content, provenance, patch, and extracted rebuild checks pass. Its ZIP
  container hash is intentionally not recorded because `Compress-Archive`
  timestamps make that container non-deterministic while validated content is
  unchanged. The thin review payload has exactly 13 entries, and the complete
  resilience suite passes against its exact packaged shell.
- Initial Setup evidence: Setup builds from that payload; embedded payload and
  native provenance equal the reviewed inputs; `/verify-runtime`,
  `/verify-shortcut-selection`, and `/verify-update-lifecycle` each exit 0.
  Shell, Setup, and core are x64; `0.12.13.0`, Setup comparison 0.12.13,
  exactly five 0.12.13 script defaults, local links, strict UTF-8/no-added-BOM,
  diff, and release-input fingerprint checks pass. Volatile shell/payload/Setup
  hashes and sizes remain in the gate handoff rather than their own inputs.
- Carried-forward behavior: the untagged 0.12.12 bounded ten-minute then
  20-minute/shared-unlock schedule remains, but a capable healthy core now
  services those automatic stages in place. The untagged 0.12.11 Photos outer-
  window behavior and 0.12.10 geometry/test isolation are unchanged.
- Scope boundary: local Bonjour callback readiness does not continuously
  attest iPhone visibility or force an iOS browse-cache refresh. This patch
  adds no in-process BLE reconfiguration, AWDL/peer-to-peer AirPlay, AirDrop,
  Photos content rectangle/crop, Camera-orientation fix, full native-core
  audit, physical acceptance, installed update, tag, or public asset. There is
  no `docs/releases/0.12.13/BUILD_REPORT.md`.
- Immediate next step: install the unchanged candidate for the physical 30–40
  minute idle, manual-recovery, and physical-IPv4 test matrix. Do not infer
  remote iPhone visibility from the completed local registration gates.
- Physical reporter evidence on 2026-08-13 found an unresolved media-liveness
  defect in the installed 0.12.13 candidate: the last rendered frame froze a
  few seconds after mirroring began, while the native process, RTSP/control
  session, and mirror parser remained responsive until the reporter stopped
  mirroring on the iPhone. The log proves a first H.265 appsrc/sink/D3D11
  Present, then 3840x2160 codec/geometry packets, but has no continuous VCL,
  push, decoded-sink, or Present counters. Therefore 0.12.13 must not be
  published yet; the next candidate must first identify and correct the exact
  video-pipeline boundary. This observation is not a Bonjour/discovery failure.

## Prior untagged 0.12.12 candidate history

- Status: superseded verified local pretag discovery candidate. Its final local automated
  gate passes: fresh managed x64 build, complete receiver resilience suite and
  repeat against the exact packaged shell, independent source/evidence review
  with no P0/P1/P2 finding, unchanged-native source/provenance, exact thin
  payload, Setup and lifecycle verification, version/default/link/UTF-8/diff,
  and release-input fingerprint stability. Installed update, physical-device,
  exact-tag, GitHub Release, and public re-download gates remain pending.
- Local app/Setup source version is `0.12.12`; Windows PE/file source version is
  `0.12.12.0`. Setup's internal comparison version and exactly five release-
  script defaults target 0.12.12. Public `v0.12.9` remains the immutable normal
  updater-visible latest review release.
- Reporter-machine 0.12.11 evidence: the shell started at 12:09:14. Core PID
  19780 advertised port 61272 and reached DNS-SD/BLE startup readiness by
  12:09:20.771. The existing first idle renewal ran at 12:19:16; replacement
  PID 39968 advertised port 52197 and reached startup readiness by
  12:19:20.053. No further app event, inbound AirPlay probe, readiness failure,
  sleep, or network change appears before the user found the receiver absent
  and manually refreshed discovery at 12:42:35. Windows records one
  `SessionUnlock` at 12:14, before the first renewal; the next input event was
  `InputHid` at 12:40, not an unlock.
- Manual refresh started PID 36292 on port 53867. Local startup readiness
  completed 4.776 seconds later and an iPhone connection reached Windows at
  12:43:05, an upper bound of 30.717 seconds after the manual action. These
  facts show that a full managed restart restored a usable advertisement; they
  do not identify whether DNS-SD, BLE, Bonjour browse state, or iOS caching was
  the stale boundary. Existing readiness markers describe startup only.
- Mitigation: an idle epoch keeps the existing first renewal after ten minutes
  and now schedules a second timed renewal 20 minutes after that restart. In
  the captured episode the added stage would have been due near 12:39, about
  58 seconds before the recorded 12:40 `InputHid` event.
- Safety boundary: the timed second stage and the existing post-renewal
  `SessionUnlock` fallback share one strict two-renewal allowance. Whichever
  consumes the second allowance prevents a third restart. Active mirroring or
  current client grace preserves the due timer and allowance for a later idle
  pass. High-level client activity, mirroring start, manual discovery refresh,
  and the existing eligible-epoch boundaries reset or re-arm the sequence.
- Verification: deterministic tests cover 10/20-minute mapping, both allowance
  transitions, not-yet-due preservation, anti-churn postponement, active/client-
  grace deferral, timed-versus-unlock mutual exclusion, session activity, core
  restart, manual refresh, and physical-network reset boundaries. Automated
  checks do not prove continuous iPhone visibility during a real idle period.
- Package evidence: prepared native source retains the reviewed unchanged core
  and provenance and contains 143 archive entries/139 files. The thin review
  payload contains exactly 13 entries. Setup's embedded payload/provenance is
  equal to its reviewed input; `/verify-runtime`,
  `/verify-shortcut-selection`, and `/verify-update-lifecycle` each exit 0.
  Shell, Setup, and core architecture, PE/file version, Setup comparison
  version, exactly five script defaults, all 29 local links across 57 Markdown
  files, strict UTF-8, diff, and release-input fingerprints pass. Exact
  container sizes/hashes are retained only in the final gate handoff.
- Carried-forward behavior: the untagged 0.12.11 candidate's automatic exact-
  signature Photos outer-window fitting, retired Photos-specific switch,
  provisional-placement protection, geometry ordering/refit rules, and test
  isolation remain in the source.
- Scope boundary: no native source, DNS-SD/BLE ownership, BLE helper,
  capability, dependency, runtime, patch, or provenance input changed. This
  patch adds no acknowledged in-place re-publication, stable-port contract,
  root-cause claim, or continuous-visibility guarantee.
- Artifact status: the verified local 0.12.12 shell, prepared native source,
  exact 13-entry review payload, and Setup exist only as pretag candidate
  artifacts. There is no tag, GitHub Release, public asset, or
  `docs/releases/0.12.12/BUILD_REPORT.md`.
- Its former immediate next step was a 30–40 minute physical idle test. That
  gate is superseded by the 0.12.13 plan; its frozen 0.12.12 artifacts must not
  be relabeled, and it remains untagged and unpublished.

## Prior untagged 0.12.11 candidate history

- Status: superseded local pretag candidate. Fresh and packaged-shell managed
  builds, the complete resilience suite, independent source/evidence review,
  unchanged-native provenance, exact thin payload, Setup, all three Setup
  verification modes, version/default/link/UTF-8/diff, and release-input
  fingerprint gates passed. Physical Photos, installed update, exact tag,
  GitHub Release, and public re-download remained pending.
- Version 0.12.11 removed the temporary Photos-specific setting and made only
  the exact correlated `3840x2160`, auxiliary `0x0` media signature a
  provisional automatic outer-window target. A later phone-shaped frame takes
  over, and provisional placement cannot become trusted or persistable.
- Its verified local artifacts remain 0.12.11 evidence and must not be
  relabelled. The implementation is carried into 0.12.12. There is no 0.12.11
  tag, GitHub Release, public asset, or `BUILD_REPORT.md`.

## Prior untagged 0.12.10 candidate history

- Status: superseded local pretag candidate. The managed x64 source build, complete
  receiver resilience suite, independent source review, unchanged-native
  reuse/provenance, prepared native source, initial exact 13-entry review
  payload, Setup, runtime loader, shortcut, and update-lifecycle gates pass.
  The focused package/Setup rebuild after the evidence-document update also
  passes. Exact-tag, GitHub Release, public re-download, installed update, and
  all physical Windows/iPhone gates remain pending.
- Local app/Setup version is `0.12.10`; Windows PE/file version is
  `0.12.10.0`. Setup's internal comparison version and exactly five release-
  script defaults target 0.12.10. Settings remain at schema 12 and
  `FollowPhotosMediaCanvas` remains default-false; no migration was added.
- Geometry ordering: every correlated native geometry/size event advances a
  monotonic sequence for the lifetime of the current core. An identical
  pending candidate retains the original 350 ms deadline while adopting the
  newer sequence, preventing indefinite debounce starvation. A duplicate of
  the current stable candidate does not reopen the debounce; a change between
  device-frame and media-canvas classification is still distinct. A new mirror
  session clears its candidate/baseline state without rewinding the core-
  lifetime sequence; a full core reset clears it.
- Renderer fitting: the applied target records both its class
  (`DeviceFrame`/`MediaCanvas`) and exact aspect. A fresh event refits when the
  class or exact aspect changes, even without a portrait/landscape flip. A
  scaled marker with the same class and aspect is consumed without repeated
  movement. A live Photos-option change forces re-evaluation of the current
  stable frame. A class/aspect mismatch survives a supervision tick blocked by
  an active resize/mouse gesture or a failed fit and is retried later, while
  provisional media-canvas fits remain non-persistable.
- Test isolation: reflection tests set one process-lifetime storage override
  to a GUID-named direct child of the system temp directory before persistent
  paths or logging are used. All settings, trust, key, and log paths resolve
  there; a second different override is rejected. The asynchronous log queue
  is drained deterministically before assertions and exact-root cleanup. A
  failed run retains the exact GUID root and emits its path as a warning; only
  a successful drained run deletes it. The production
  `%LOCALAPPDATA%\AirPlayReceiverMvp\receiver.log` is untouched by the suite.
- Automated evidence: the full resilience suite covers non-sliding duplicate
  debounce, session/core reset invariants, device-frame/media-canvas and exact-
  aspect transitions, retry after a blocked setting-change pass, scaled-same-
  aspect suppression, Photos-toggle behavior, provisional-placement
  protection, one-shot storage-root validation, log markers after drain,
  failure-root retention/warning, and successful cleanup. The production log
  remained byte-identical across the executable test gate. Physical behavior
  is not inferred from these checks.
- Native/discovery scope: no native code, capability, patch, runtime, or
  dependency change is part of 0.12.10. Same-process, same-port DNS-SD/BLE
  refresh after HTTP reset, safe registration ownership, a `refreshDiscovery`
  command, and acknowledged ready markers remain `DESIGN/NEXT`, not
  implemented behavior.
- Local artifact status: the focused post-evidence 0.12.10 review payload and
  Setup pass automated pretag checks. Their exact hashes are retained in the
  final gate handoff, but they are not public downloads. There is no 0.12.10
  tag, GitHub Release, public asset, or `BUILD_REPORT.md`. Public `v0.12.9`
  remains the immutable normal updater-visible latest review Release with all
  evidence below unchanged.
- It was not tagged or published. Its implementation is carried into 0.12.11,
  while its frozen local artifacts remain 0.12.10 evidence and must not be
  relabelled. Its pending physical gates are superseded by the 0.12.11 plan.

## Prior public release — 0.12.9

- Status: published normal updater-visible public review Release. The
  implementation, final managed build, complete resilience suite, independent
  source review, unchanged-native reuse/provenance, thin-package, Setup,
  exact-tag, four-asset, checksum, API-digest, canonical/legacy latest-route,
  and fresh public re-download gates pass. Independent review reports no
  P0/P1/P2 finding. Physical Windows/iPhone, the installed update from public
  0.12.7, and actual discovery, Photos, and reconnect acceptance remain
  pending.
- Annotated tag object:
  `10deba1d48482da3500cf0bd7c796c87c7fce736`; commit:
  `b807d5dece26e972c58a3a2f7e5585dc8075672e`; tree:
  `a2f49d66039c79bdc72907a9cefe6833d4e0257d`. The tag/Release was
  created at `2026-08-11T19:14:44Z`; GitHub Release `368804215` was published
  at `2026-08-11T19:25:27Z`:
  https://github.com/pyram1da/aeromirror/releases/tag/v0.12.9
- Channel: normal latest, `draft=false`, `prerelease=false`. GitHub reports
  API `immutable=false`; the tag and four assets are immutable by AeroMirror
  project policy. Exact public evidence is recorded in
  `docs/releases/0.12.9/BUILD_REPORT.md`.
- Public/app/Setup version: `0.12.9`; Windows PE/file version: `0.12.9.0`.
  Setup's internal comparison version and all five release-script defaults
  target 0.12.9. Source-version, settings-schema-12/default-false, and version-
  default checks pass.
- Discovery scope: retain the existing first ten-minute idle renewal. Only
  after it has completed, a later Windows SessionUnlock may request at most one
  final receiver restart and discovery re-registration after a ten-minute
  cooldown. The core, local sockets, at least one DNS-SD/BLE marker, cached
  physical IPv4, inactive mirroring/client grace, and restart/network guards
  must be ready. Temporary socket/discovery/address readiness or competing
  maintenance defers evaluation. A stopped core, active mirroring/client
  grace, or an otherwise ineligible idle epoch cancels the pending unlock
  request. Unlock events themselves do not re-arm the allowance. New client or
  mirroring activity, an explicit manual discovery refresh, or an actual
  physical-network signature change begins a new eligible discovery epoch.
- The unlock refresh is a bounded mitigation for one reported missing-after-
  idle receiver. It is not a proven root-cause correction, adds no
  acknowledged native discovery IPC or in-place DNS-SD/BLE re-publication,
  cannot force iOS browse-cache invalidation, and provides no stable-port
  contract across process replacement.
- Photos scope: settings schema 12 adds a default-off Advanced A/B option for
  allowing only the exact ambiguous `3840x2160`, source `3840x2160`,
  auxiliary `0x0`, encoded `3840x2160` Photos/media canvas to shape the outer
  renderer window temporarily. The provisional landscape does not become the
  trusted device baseline and does not overwrite a valid saved placement.
- The Photos option changes no native capability, feature bit, negotiation,
  decoder, pixel, crop, or zoom. Inner media can remain small inside the
  iPhone-provided canvas, and Photos/video stability still requires physical
  regression testing.
- The untagged 0.12.8 current-PID/session/recovery-epoch Direct3D 11 Present-
  proof handoff is carried forward unchanged in product behavior. The
  underlying long-gap frozen-video path remains unresolved.
- One Windows 10 first-install report appeared to work only after a full OS
  reboot. Setup extracts the portable app runtime but installs no system-wide
  .NET/VC++ redistributable, driver, or framework prerequisite, so reboot is
  not treated as normal. A stopped/stale system-wide Bonjour service lifecycle
  is the strongest current hypothesis but is unproven; no
  machine-wide Bonjour mutation is authorized. A clean Windows 10 VM with
  pre-reboot Setup/receiver logs and service state is the immediate evidence
  target.
- Managed/release evidence: the final x64 shell build and full resilience suite
  pass; shell and Setup are x64 `0.12.9.0`; the exact 13-entry review payload,
  Setup build, `/verify-runtime`, shortcut/update lifecycle, and embedded
  payload/provenance checks pass. Local links, strict UTF-8, and diff checks
  pass.
- Native reuse evidence: core SHA-256 is
  `eb8162577689eed354c4382acfe099665a6d9e14eed466cb4da6ca6e087448d6`.
  Provenance, both patch reverse-apply checks, extracted prepared-source
  rebuild, exact 143 archive entries/139 files, runtime inspection of 199
  binaries/148 copied DLLs, and the loader test pass.
- The legacy C# compiler does not produce a byte-deterministic shell across
  independent builds. Its independent build confirms semantics and
  `0.12.9.0`; the exact packaged shell hash is evidence from the final focused
  package gate, not from cross-build byte equality.
- Immediate next step: run long-idle/unlock, Photos off/on, reconnect, the
  installed update from public 0.12.7, and clean Windows 10 first-install
  physical plans. Do not infer physical acceptance from the completed public
  asset verification, and never replace the published 0.12.9 tag or assets.

## Prior untagged 0.12.8 candidate history

0.12.8 completed its automated, native, package, Setup, and pretag gates but
not physical or public acceptance, and it was never tagged or published. Its
implementation is carried into 0.12.9. The version/default statements in this
historical section describe the former 0.12.8 candidate, not the current
working-tree defaults.

- Status: source implementation and independent final code review are complete
  with no remaining P0/P1/P2 finding. Managed build/resilience, official native
  reproducibility, prepared-source rebuild, runtime/provenance/dependency,
  loader, thin-package, and Setup pretag gates pass. The focused final package
  review and Setup rebuild after the evidence-doc update also pass. Physical,
  exact-tag, install-from-public, and public verification were not completed;
  the candidate was superseded by 0.12.9.
- Public/app/Setup version: `0.12.8`; Windows PE/file version: `0.12.8.0`.
  Setup's internal comparison version and all five release-script defaults
  target 0.12.8.
- Scope: correct the 0.12.7 continuity false handoff. The captured longer-gap
  run proves that HTTP feedback recovered while the old external renderer HWND
  remained visible. The shell then closed the placeholder without evidence of
  a fresh displayed frame, and video remained frozen.
- Candidate same-session gap contract: HTTP feedback recovery may change the
  continuity view to
  **Connection restored / Waiting for image**, but only a current native PID,
  current managed mirror-session generation, and current recovery-epoch
  `AEROMIRROR_VIDEO_PRESENT_READY epoch=E gap_seconds=N
  proof=d3d11-present pts_delta_ms=D` marker may authorize the fade for that
  path. Appsrc push/PTS, sink telemetry, window
  visibility, or a cached HWND are diagnostic evidence only. Manual reselection
  may arm a new epoch with `AEROMIRROR_VIDEO_PRESENT_ARMED
  reason=mirror-start epoch=E`, but only while that reason is expected.
  Feedback proof must repeat the exact positive stored gap; accepted
  mirror-start proof must use `gap_seconds=0`. Either accepted challenge gets a
  fresh three-second proof wait, and mirror-start alone cannot close continuity.
- Balanced Direct3D 11 is the primary proof path. Direct3D 12, advanced sinks,
  and Interactive `-vsync no` keep waiting/reconnect guidance; the Interactive
  path deliberately skips synchronized PTS/Present proof, and none may fall
  back to cached HWND state.
- If current presentation proof does not arrive during the bounded three-second
  wait, the view remains visible and changes to explicit iPhone Screen
  Mirroring reconnect guidance. The candidate does not auto-reset the core,
  hot-replace a half-open video socket, or rebase the media clock.
- This is not yet a fix for the underlying long-gap frozen-video path. The
  small photo/video inside Photos' encoded `3840x2160` canvas is also unchanged;
  the mirror-only feature-bit experiment remains as published in 0.12.7. A
  retained HEVC 4K-versus-Full-HD Photos A/B remains pending. No exact display-
  capability marker landed; any future marker is negotiation evidence, not a
  sizing fix.
- Updated provenance records expect native core SHA-256
  `eb8162577689eed354c4382acfe099665a6d9e14eed466cb4da6ca6e087448d6`
  and libuxplay patch SHA-256
  `c5be47ee96be25609677103cf85b3d98b07e2752a980d0d6d9fb975d187ad05e`.
  The official native build and extracted prepared-source rebuild reproduce
  that exact core hash. Both native patches reverse-apply. Native source
  generation produces 143 archive entries/139 files, and the loader test
  passes; the runtime/provenance/dependency audit covers 199 inspected binaries
  and 148 copied DLLs.
- Managed build and receiver resilience pass. The thin package review has the
  exact 13 entries; Setup builds, `verify-runtime` exits 0, and embedded
  payload/provenance matches. Shortcut and update-lifecycle checks also exit 0;
  shell, Setup, and core are x64 `0.12.8.0`. Version/default,
  documentation-link, and diff gates pass. Independent final review reports no
  P0/P1/P2 finding.
- A focused final package review and Setup rebuild after the evidence-doc update
  pass again: exact 13 entries, embedded payload/provenance, `verify-runtime`,
  shortcut/update lifecycle, version, link, and diff checks all pass. Exact
  payload/Setup byte sizes and SHA-256 values remain in the historical
  candidate handoff. Physical iPhone/Windows 10/Windows 11, exact-tag,
  install-from-public, GitHub Release, checksums, API, and public re-download
  verification were not completed. No 0.12.8 asset is public, so no
  post-publication 0.12.8 `BUILD_REPORT.md` exists.

## Prior public release — 0.12.7

- Version: `v0.12.7`
- Annotated tag object: `6154c7f3c3384dcd039b4e1e0c2feceb46b84fad`
- Tag commit: `dd343a44b0c9b6904815cd78e54a841e9f5ef6be`
- Release URL: https://github.com/pyram1da/aeromirror/releases/tag/v0.12.7
- GitHub Release ID: `368571434`
- Published: `2026-08-11T12:57:13Z`
- Channel: normal, non-draft, non-prerelease GitHub Release
- Updater status: historical public review Release; superseded on
  `releases/latest` by `v0.12.9`
- Supported target: Windows 10 version 1809+ x64 and Windows 11 x64
- Installer: unsigned per-user network Setup; SmartScreen may warn
- Public assets: Setup, AeroMirror source, prepared native corresponding
  source, and `SHA256SUMS.txt`
- Offline portable package: engineering-only and not published

AeroMirror project policy continues to treat the published `v0.12.7` tag and
its four assets as immutable, although GitHub reports API `immutable=false`.
It is historical release evidence and was not modified for 0.12.9. Exact
evidence is recorded in
`docs/releases/0.12.7/BUILD_REPORT.md`.

Project policy also treats the published 0.12.6, 0.12.5, 0.12.4, 0.12.3,
0.12.2, 0.12.1, 0.12.0, and 0.11 releases as immutable history. Their
verification remains under `docs/releases/` or the historical 0.11 report
paths.

## What 0.12.7 changes

- Status: published normal updater-visible public review Release.
- Public app/Setup version: `0.12.7`; Windows PE/file version:
  `0.12.7.0`. The five release-script defaults and Setup's internal comparison
  version target 0.12.7.
- The affected physical 0.12.6 log shows that the managed shell and native core
  processes remained alive while the current AirPlay connection was removed.
  It records `Disconnecting on software request`, but not the debug request
  type, and the native server has more than one software-disconnect site.
  Source review found that 0.12.6 newly forced a full disconnect from the typed
  AirPlay `TEARDOWN` handler; the transition timing is consistent with that
  plausible regression but does not prove it was the logged call site. One
  captured transition also reported a `wasapi2` wrong-format error immediately
  after the software disconnect; that ordering does not prove the audio error
  initiated teardown.
- The 0.12.7 native correction removes only that unconditional server-side
  disconnect request. It retains upstream's `Connection: close` response
  header, lets the client determine whether and when the socket closes, and
  adds a compact typed-`TEARDOWN` diagnostic marker so the next physical run
  can confirm the request type directly.
- Default Windows audio now requests
  `wasapi2sink continue-on-error=true`. The pinned redistributed GStreamer
  1.28.1 runtime supports that property for its documented device-open, I/O,
  and removal failures. Mute behavior is unchanged, and advanced UxPlay
  arguments remain later on the command line for an explicit override. This is
  not generic isolation for every GStreamer bus error.
- The headless wrapper now preserves external `-vs` and `-fs` arguments. This
  prevents hidden wrapper settings from replacing the shell's requested D3D11
  sink or fullscreen policy before UxPlay parses them.
- The isolated managed 0.12.7 build and resilience suite pass. Two clean
  native builds reproduce SHA-256
  `11b65324c83f23503f2d555d0064d1348c884407bf7f9b1c34d27b5d1c05fb9b`.
  Native patch/current-source/protected-audio hashes, x64 PE/Qt import and
  path/debug checks, exact 143-file prepared native-source content and
  provenance, 199-binary dependency inspection, 148-DLL collection, and the
  distinct redistributed GStreamer 1.28.1 versus build-toolchain 1.28.5
  contracts pass. The exact public-runtime loader test and reverse-apply of
  both patches also pass. A rebuild from the extracted prepared native source
  reproduces the same core.
- The exact 13-entry review payload, shell and Setup `0.12.7.0` PE/file
  versions, Setup's internal comparison version, all five release-script
  defaults, Setup embedded-payload and `/verify-runtime` verification,
  shortcut/update lifecycle self-checks, native-source content/provenance
  checks, local links, and `git diff --check` pass. The final pre-tag payload
  and Setup were regenerated after the evidence update, and their
  embedded-payload, lifecycle, version, link, and diff checks passed again.
- Annotated tag `v0.12.7` resolves to commit
  `dd343a44b0c9b6904815cd78e54a841e9f5ef6be`. Exact-tag packaging, the normal
  latest channel, exactly four public assets, the three-entry checksum file,
  all API digests, and fresh public re-download size/SHA-256 verification pass.
  Canonical and configured legacy latest routes return the same `v0.12.7`
  Release ID `368571434`, and the legacy-route Setup hash matches. The actual
  installed update from public 0.12.6 and full physical Windows/iPhone
  acceptance remain pending.
- A public-build Windows 11/iPhone smoke on the reporter's system passes the
  urgent involuntary Photos/video session-drop target: direct Photos launch
  and a normal gallery/video session work without the prior drop, and the user
  described that corrected path as ideal. This does not accept the full
  physical matrix. One first direct-Photos connection tap failed before the
  second succeeded, inner photo/video content remains small, and an
  reporter-estimated wall-clock interruption of about 15 seconds cleared the
  placeholder after reconnect but left video frozen; its exact log interval
  records an 11-second feedback gap, and closing AeroMirror briefly exposed the
  latest frame. A reporter-estimated wall-clock Wi-Fi interruption of about ten
  seconds recovered automatically; its exact log interval records a five-second
  feedback gap.

The urgent physical Windows 11/iPhone session-drop target has a scoped PASS,
but the complete repeated sequence, Windows 10, installed-update, and
interruption matrix remain priority gates in
`docs/releases/0.12.7/TEST_PLAN.md`. This release does not crop or enlarge a
small image already encoded inside the Photos canvas, repair delayed discovery,
or make reconnection reliable.

## What 0.12.6 changes

- Status: published normal updater-visible public review Release.
- Public app/Setup version: `0.12.6`; Windows PE/file version:
  `0.12.6.0`. The five release-script defaults and Setup's internal comparison
  version target 0.12.6.
- New profiles default to an explicitly pinned Direct3D 11 decoder and sink.
  Settings schema 11 migrates only the legacy automatic renderer choice to
  Direct3D 11, preserves an explicit Direct3D 12 opt-in, and normalizes an
  unknown renderer to Direct3D 11. The Advanced UI recommends Direct3D 11 and
  no longer offers automatic GStreamer selection.
- The continuity view is inserted immediately above the external renderer
  without activation. Fatal native cleanup changes its guidance to a manual
  Screen Mirroring reconnect instruction; it does not claim that discovery or
  reconnect completed automatically.
- The released native core emits explicit HTTP ready/failed markers for initial
  bind and fatal reset. The shell accepts them only from the current PID and
  preserves same-process recovery only after a matching reset on the original
  AirPlay port; failure or mismatch exits into full-process recovery. AirPlay
  `TEARDOWN` explicitly requests disconnect.
- AirPlay photo, slideshow, and preload feature bits are cleared and a
  mirror-only capability marker is logged. This is an isolated negotiation
  experiment. Its effect on direct-in-Photos startup and the inner encoded
  canvas remains a pending physical A/B gate.
- The current managed build, settings-migration/renderer arguments coverage,
  combined resilience suite, shell and Setup `0.12.6.0` x64 PE checks, exact
  13-entry review payload, Setup embedded-payload SHA-256 comparison,
  shortcut/update lifecycle self-checks, version/link audits, and
  `git diff --check` pass. The native core rebuild is reproducible at
  SHA-256 `9f1fb168c882b1531400d2edbb4abd1277803c1971a20e9d5c4d7eff3e8498fc`;
  patch/provenance, dependency, loader, reverse-apply, archive-content, and
  prepared native-source checks pass.
  Exact-tag packaging, the normal latest channel, all four GitHub API digests,
  and public re-download byte-size/SHA-256 verification also pass. Canonical
  and legacy `releases/latest` API routes return the same `v0.12.6` Release ID.
  Installed-update and physical Windows/iPhone gates remain pending.

Direct3D 11 is the default in this public review Release, not a physically
accepted Photos fix. The small photo and black bars may still
be encoded inside iOS's `3840x2160` presentation canvas; this release does
not crop, zoom, or reconstruct those pixels. It also does not claim to make a
stale iOS browse result, receiver discovery, or automatic reconnect reliable.

## What 0.12.5 changes

- Status: published normal updater-visible review Release.
- Public/app/Setup version: `0.12.5`; Windows PE/file version: `0.12.5.0`.
- The exact recorded Photos geometry
  `3840x2160 aux=0x0 encoded=3840x2160` is now classified as an ambiguous
  presentation canvas instead of becoming the device-orientation baseline.
  A later `998x2160 aux=1421x0` phone frame can establish portrait in the same
  session, while the observed real-landscape signature and unrelated 16:9
  streams remain eligible.
- An unresolved automatic/provisional fit cannot replace a valid saved
  placement. A trustworthy device frame or explicit user move, resize, or
  manual fit makes the current placement persistable.
- The native three-second feedback warning schedules a four-second local
  continuity deadline for a capable active session. Early recovery cancels it;
  acknowledged recovery changes the view to connection-restored/waiting-for-
  image and queues handoff. Fatal reconnect handoff still waits for a real
  positioned renderer.
- The pinned UxPlay core, native patches, source provenance, and third-party
  runtime are unchanged from 0.12.4. The pre-tag gates confirm that the staged
  native core is byte-identical to 0.12.4 and that the prepared native-source
  package contains 139 files with all 12 provenance hashes validated.
- The managed x64 build, receiver resilience suite, 13-entry thin review
  payload, network Setup build and shortcut/update lifecycle self-checks,
  prepared native-source build, shell/Setup `0.12.5.0` PE audit, Setup embedded-
  payload SHA-256 comparison, source/default/document/link checks, and
  `git diff --check` pass for the tagged source.
- The final payload and Setup were regenerated after the evidence update; the
  embedded payload hash, lifecycle checks, and version audits passed again.
  Exact-tag packaging, the normal latest channel, four API digests, and public
  re-download size/SHA-256 verification also pass.
- GitHub's canonical repository is now `pyram1da/aeromirror`. The checked-in
  updater slug remains `Nadejny/aeromirror` in this immutable release; its old
  API and Setup URLs followed GitHub redirects and successfully reached the
  canonical 0.12.5 Release. The actual installed update remains pending.

This release does not crop or zoom the small photo and black bars that Photos
may already encode inside its `3840x2160` canvas. It also does not claim to fix
delayed iOS browse-cache visibility when no request reaches Windows. Installed
update and physical Windows/iPhone tests remain clearly pending; the release
must not be called physically accepted or 1.0.

## What 0.12.4 changes

- UxPlay's feedback-loss bound returns from six seconds to the upstream
  15-second default. After completed native socket cleanup, the shell preserves
  the recovered core PID and AirPlay port instead of immediately replacing the
  process and publishing a new port.
- The patched core announces feedback-health capability and emits a compact
  recovered marker. AeroMirror can show continuity after a five-second gap and
  dismiss it when the same session recovers; legacy cores cannot enter this
  pre-fatal path.
- Saved renderer placement is applied from the early Windows show event.
  Continuity remains until the real renderer exists and is positioned, then
  fades away. Safe capture uses only unobscured renderer client pixels.
- Unchanged renderer title/taskbar/topmost policy is cached instead of being
  written on every supervision tick. Proportion restoration is queued after
  interactive resize completion.
- The former Minimal latency profile is labelled Interactive and now applies
  only `-vsync no`; it no longer forces `-al 0.05`. Explicit Direct3D 11/12
  choices pin matching decoder families and sinks, with codec matching at
  pipeline creation.
- Diagnostics add feedback-gap totals, native capability state, the full raw
  AirPlay geometry header (including the previously ignored auxiliary pair),
  and actual selected decoder/sink. The raw auxiliary dimensions are not
  interpreted as crop, PAR, or rotation metadata.
- The settings Back control is larger.

The upstream revisions and third-party runtime remain pinned. The reviewed
native patch, rebuilt core, modified-source hashes, build inputs,
`UPSTREAM.lock`, source provenance, and prepared corresponding source validate
together.

## Release verification

Passed against the exact source published as `v0.12.9`:

1. managed x64 shell build, complete receiver resilience suite, and
   independent source review with no P0/P1/P2 finding;
2. reused native core SHA-256
   `eb8162577689eed354c4382acfe099665a6d9e14eed466cb4da6ca6e087448d6`,
   exact 143 archive entries/139 files in prepared corresponding source,
   extracted rebuild, provenance/reverse-apply/dependency/loader checks, and
   the runtime 1.28.1 versus build-toolchain 1.28.5 contract;
3. exact 13-entry thin review payload, Setup build, embedded-payload SHA-256,
   and shortcut/update lifecycle verification;
4. shell/Setup `0.12.9.0` PE, internal Setup version, five script defaults,
   settings-schema-12/default-false, asset-name, documentation-version,
   local-link, strict-UTF-8, changed-file, and `git diff --check` audits;
5. clean exact-tag release packaging from annotated tag object
   `10deba1d48482da3500cf0bd7c796c87c7fce736`, resolving to commit
   `b807d5dece26e972c58a3a2f7e5585dc8075672e` and tree
   `a2f49d66039c79bdc72907a9cefe6833d4e0257d`;
6. normal latest GitHub channel with Release ID `368804215`, `draft=false`,
   `prerelease=false`, and exactly four expected assets;
7. all public re-download byte sizes and SHA-256 values match final local
   release files, and all four GitHub API digest fields match;
8. canonical and configured legacy latest API, HTML, and Setup routes resolve
   to the same `v0.12.9` Release ID, tag, and Setup bytes; the GitHub Release
   body matches the exact tagged release notes.

No physical Windows/iPhone result is claimed by these gates. Exact asset
evidence is in `docs/releases/0.12.9/BUILD_REPORT.md`.

## Pending physical verification and known limitations

- the installed updater path from public 0.12.7 to public 0.12.9, including
  version detection, settings/trust-state preservation, runtime-cache reuse,
  Setup launch, shortcut/autostart preservation, and rollback;
- the installed updater path from public 0.12.6 to public 0.12.7, including
  version detection, settings/trust-state preservation, runtime-cache reuse,
  Setup launch and rollback; the public-build smoke does not prove this updater
  path;
- the installed updater path from public 0.12.5 to public 0.12.6, including
  legacy automatic-to-D3D11 migration, explicit D3D12 preservation, settings,
  trust state, shortcuts, autostart, runtime-cache reuse, digest verification,
  Setup launch, and rollback;
- Windows 11 x64 and Windows 10 1809+ x64 with an iPhone: 3–4 second,
  5–8 second, and longer-than-15-second Wi-Fi interruptions; native in-place
  recovery; fatal recovery; normal disconnect; immediate and repeated
  reconnect; delayed Wi-Fi join; idle discovery; and VPN-over-Private-LAN;
- public 0.12.7 already recovered automatically from one reporter-estimated
  wall-clock Wi-Fi interruption of about ten seconds (five-second feedback gap
  in the exact log interval), but reconnect after one reporter-estimated
  wall-clock interruption of about 15 seconds (11-second exact-log feedback
  gap) cleared the placeholder while video stayed frozen; closing AeroMirror
  briefly exposed the latest frame. This observed longer-handoff failure must
  be reproduced and corrected;
- saved placement at first show, handoff fade and renewed-loss cancellation,
  mixed-DPI/multi-monitor restore, taskbar/topmost settings, manual resize,
  safe snapshot, privacy fallback, and no focus theft or Z-order flicker;
- Balanced versus Interactive plus Direct3D 11 and Direct3D 12
  frame-pacing, audio drift, CPU/GPU, feedback-gap, and decoder/sink evidence;
- direct-in-Photos startup where the ambiguous 4K canvas arrives before a
  phone-shaped frame; portrait/landscape rotation; unresolved placement
  persistence; fullscreen media; actual inner photo size; and the schema-12
  default-off outer-window A/B without provisional-landscape persistence;
- Photos may still place a small image and black bars inside a `3840x2160`
  encoded canvas. Raw geometry diagnostics do not provide a validated content
  rectangle, so this release does not crop or zoom those pixels;
- the missing-after-long-idle receiver report: first ten-minute renewal, later
  SessionUnlock, one guarded final refresh, repeated-unlock limit, iPhone browse
  and first-tap evidence, and whether manual discovery restart remains needed;
- the Windows 10 first-install report that became usable only after reboot.
  AeroMirror extracts a portable app runtime but installs no system-wide
  .NET/VC++ redistributable, driver, or framework prerequisite and does not
  normally require reboot; retain clean-VM pre/post-reboot Bonjour
  service/process, Setup/receiver log, pending-reboot, firewall, network, and
  iPhone evidence before considering any Setup message or machine-wide service
  action;
- an external GStreamer window cannot yet provide a Mac-style hover-only frame,
  true borderless surface, or live aspect lock while dragging. Those require a
  native embedded renderer plus versioned IPC;
- continuity does not make iOS browse-cache refresh instantaneous, and a dark
  fallback remains necessary when safe renderer capture is unavailable;
- the mirror-focused feature advertisement remains a physical experiment; its
  effect on direct-in-Photos startup has not been accepted;
- genuine AirDrop interoperability remains separate Bluetooth/AWDL, identity,
  and encrypted-transfer research. A staged AeroDrop companion/share-extension
  path is a separate future product decision, not part of 0.12.9;
- localization is not included. D-006 remains the planned resource-based
  system-language and manual override design.

## Public 0.12.9 physical follow-up

1. Preserve the published tag and four assets as immutable project history.
   Keep exact public hashes and route verification in
   `docs/releases/0.12.9/BUILD_REPORT.md`; any correction uses a later patch.
2. Leave the receiver idle through its first renewal, then lock/unlock after
   cooldown and retain every guard, refresh, iPhone browse, first-tap, and
   manual-workaround result. Repeat controls for active mirroring, client grace,
   unavailable physical network, competing maintenance, repeated unlock, an
   explicit manual discovery refresh, and an actual physical-network epoch
   change. Unlock alone must not re-arm the allowance.
3. Run the schema-12 Photos A/B off/on with the same media. Measure outer client
   bounds and inner visible content separately, confirm no native restart or
   session drop, and prove provisional landscape does not overwrite a valid
   saved placement.
4. Reproduce first install on a clean Windows 10 VM without rebooting or
   mutating Bonjour until Setup/receiver logs and service/process state are
   retained. Compare receiver Stop/Start and only then a full reboot.
5. Physically repeat short and longer Wi-Fi interruptions on D3D11 with logs
   and a screen recording, using Balanced first and Interactive as a separate
   row. Confirm either a current presentation proof followed by real video or a
   persistent reconnect hint; do not claim that the underlying longer-gap video
   recovery is fixed from overlay behavior alone.
6. Run the actual installed update from public 0.12.7 to public 0.12.9 and
   retain settings, receiver identity/trust, shortcuts, autostart, runtime
   cache, Setup launch, and rollback evidence. Public tag, asset, checksum,
   API-digest, route, and re-download verification already pass and must not be
   confused with installed-update acceptance.

## Where information belongs

- mandatory patch documentation: `docs/DOCUMENTATION_POLICY.md`;
- current handoff and immediate next step: this file;
- durable technical/product decisions: `docs/DECISIONS.md`;
- implementation backlog and acceptance targets: `docs/TODO.md`;
- component boundaries: `docs/ARCHITECTURE.md`;
- release/update/signing rules: `docs/RELEASE_AND_SIGNING.md`;
- user-visible release history: `CHANGELOG.md`;
- versioned release evidence and acceptance: `docs/releases/<version>/`;
- troubleshooting and log collection: `docs/TROUBLESHOOTING.md`.

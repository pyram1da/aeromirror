# AeroMirror 0.12.30 — local build report

Date: 2026-09-06. Uncommitted local candidate on top of `01f4d60`, preserving
the earlier .23–.29 work. No installation, source push, tag, GitHub Release,
settings/key reset or live Bonjour change was performed. The installed .28
shell and frozen .28/.29 Setup/payload hashes were checked unchanged; the .29
corresponding-source archive also retains its original hash.

This report freezes the pre-publication local build. Before the authorized
exact-tag rebuild, its Setup, payload and native-source ZIP were copied and
hash-verified under `artifacts/local-candidates/0.12.30/`. The extracted local
shell/core remain at `artifacts/tests/payload-0.12.30/`. Public rebuilt artifact
hashes are separate evidence in BUILD_REPORT.md and must not overwrite this table.

## Scope

D-018: Caption Close requests termination of the displayed session, not
minimization or a receiver restart. The HTTP owner selects an immutable stream
ID, removes only its connection, drains media workers through existing teardown
and reports a correlated result. The reusable Qt host hides only after admission
and rejects late SHOW for the dismissed ID. Listener/DNS-SD/BLE lifecycle remains
separate. The continuity warning reaches the same handler through exact-ID stdin
and an in-process HWND message, guarded by the original process and generation.

The shell accepts dismissal/completion only for the current process and stream.
A completed Close clears managed activity and that session's loss watchdog,
including wake/EOF cleanup paths that omit the legacy worker-stop log line.
Programmatic continuity-window closure does not stop a stream. Gallery
negotiation, rendering scale and the accepted .28 child-placement fix are kept.

## Passed gates and their limits

- Shell x64 0.12.30.0 build and final packaged-shell ReceiverResilience:
  exact started/closed markers, both Close sources, old process/stream rejection,
  original warning identity, completion without the legacy stop line, duplicate
  completion and replacement-session protection.
- NativeSessionClose: production HTTP owner with a loopback-only socket adapter
  and test cleanup callbacks/barriers. Exact target removal, another connection
  retained, same-socket replacement protected, bounded mailbox, result after
  cleanup, accept after drain, 50 Close/reconnect cycles with the same listening
  descriptor/port, stop cancellation and no ID reuse after listener restart.
  This does not exercise actual iPhone RTP/audio traffic or remote UI state.
- NativeHostContracts, NativeCoreContracts source checks and the existing
  positive AES-128 CTR NIST split/reset check. NativeWorkerLifecycle executes
  eight lifecycle scenarios against the production helper. NativeVideoSurface
  builds the exact production header against Qt 6.10.1 with hidden owned HWNDs.
- Final packaged RendererShowBoundary leaves the GStreamer child geometry
  untouched while restoring the outer host. No phone screenshots or live-window
  manipulations are used by these tests.
- Final packaged UpdateTransport: 15 bounded transport/lifetime scenarios.
  Final packaged AutomaticUpdate and BonjourFirewall pass. ControlWorkQueue
  compiles the production source and checks UI delivery/handle ownership and
  250 disposal races. No live Bonjour recovery or installer download is tested.
- Two clean native compilations and one extracted corresponding-source build
  without Git metadata produce the same core SHA-256 below. The bootstrap
  compile exposed the new hash; the subsequent pinned clean build and independent
  no-Git rebuild both passed the hash gate (57/57 build steps).
- Corresponding source: 149 ZIP entries, 145 files; both patches, all 50 modified
  source hashes, upstream commits and build inputs validated. The separate .27
  prepared source was not modified.
- Engineering runtime: 200 binaries inspected, 148 DLLs copied; isolated
  self-test passes from the Unicode workspace with runtime search paths restricted
  to that bundle. Static dependency validation was performed by the staging pass.
  Deployment warned about missing translation catalogs and dxcompiler/dxil;
  these warnings do not constitute physical GPU/codec acceptance.
- Review package: exactly 13 entries; native core and shell hashes agree with
  the extracted payload. Setup verifies embedded inputs and all four non-installing
  checks: `/verify-runtime`, `/verify-shortcut-selection`,
  `/verify-update-lifecycle`, `/verify-bonjour-recovery`; all exit 0.
- `git diff --check` passes.

## Frozen local artifacts

| File | Bytes | SHA-256 |
|---|---:|---|
| AeroMirror-Setup-0.12.30.exe | 1571840 | 3FF3AC31C86B5C3027C0746657CE0AF7AAE5D65A64C6BB836346DDB3E37C4E28 |
| AeroMirror-review-payload-x64-0.12.30.zip | 1264717 | 5FF27E80996876560A0A135997856DB7C36222C9A9EAB455F4AD45B098B7BDF4 |
| AeroMirror-native-source-0.12.30.zip | 918558 | AC891BCB0E051ABD91927F8CC668257C02C86776B151F6A2CD31C679A02894AF |
| AeroMirror.exe | 837632 | C9708B3AFB552324B25E89D53477B57E497CAD3C2D8AE030278FFC125083DD1D |
| uxplay-windows.exe | 1237739 | AA33FB22E5466910ECC29303DE6559BD47A02D1783D004C3169D45C7F6C436C0 |

Wrapper patch SHA-256:
`A19BF66C99CA76BD85BED5B83F2774A78945D183C66CCF1E39810F81B8533D06`.
Libuxplay patch SHA-256:
`B78F75123B7C3E9A90E07A3A8DD102D33AF9A436C53108C195308498715DC652`.
The network Setup retains pinned GStreamer 1.28.1; it does not distribute the
complete 1.28.5 engineering runtime. No offline portable asset was published.

## Remaining acceptance

Install only by the user's choice. Test normal Close, immediate reconnect,
lock-then-close on the viewer and continuity warning, and an old warning beside
a replacement session. Confirm the iPhone actually leaves Screen Mirroring and
the same receiver PID/ports/discovery remain available. Full H.265/H.264,
mixed-window/game protection and real Bonjour stop/recovery rows remain pending.

The user's earlier .28 success report is retained but is not a completed .30
physical matrix. Native results and loopback tests do not prove visible pixels,
iOS disconnection or long-idle discovery. Broad module decomposition remains
staged; this is not a complete independent audit of inherited dependencies.

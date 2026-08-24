# AeroMirror for Windows

A native-style Windows tray application that makes an iPhone screen available
on a Windows 10/11 PC through the open-source UxPlay receiver.

**Set it up once and forget it:** AeroMirror starts quietly with Windows,
waits in the tray, and is ready from the iPhone's Screen Mirroring menu.
There is no phone app, account, subscription, advertising, or telemetry.

This is an independent project. It is not affiliated with or endorsed by
Apple. AirPlay, iPhone, and Apple are trademarks of Apple Inc.

## What works

- starts the receiver automatically and stays in the system tray;
- opens to a compact connection-status page; normal and advanced settings
  are separate pages inside the same application window;
- follows the Windows light/dark app theme by default and allows a manual
  light or dark override;
- starts with Windows and starts hidden in the tray by default;
- lets the user choose whether the main-window close button hides to the tray
  or exits the application;
- starts, stops, and restarts the UxPlay core;
- changes the receiver name shown under **Control Center → Screen Mirroring**;
  names are normalized to one Bonjour-safe value of at most 50 UTF-8 bytes,
  and an interactive save explains and persists the effective value if it had
  to remove controls, repair invalid text, trim, or shorten the input;
- uses no PIN on a trusted/private network by default;
- can establish PIN trust once and keeps the receiver key and trusted-client
  register under the user's local application data, independently of the
  current Wi-Fi network and application install folder;
- detects the active physical Wi-Fi/Ethernet profile while ignoring VPN and
  virtual adapters, and reports how many overlay profiles were excluded;
- pauses unprotected reception on a Windows Public physical network and asks
  the user to enable a visible four-digit PIN;
- canonicalizes persisted protection state before use: only no-PIN mode or
  PIN mode with exactly four ASCII digits is accepted, so an obsolete,
  unknown, or malformed stored value becomes unprotected and the existing
  Public/Unknown physical-network rule fails closed;
- offers simple 720p/30, 1080p/30, 1080p/60, and HEVC 4K/60 quality
  presets in the normal settings;
- offers Windows Mobile Hotspot only as an optional advanced action while a
  Public physical network is active;
- defaults to a pinned Direct3D 11 decoder and video sink for stability, while
  retaining Direct3D 12 as an experimental opt-in; the 0.12.7 headless wrapper
  preserves shell-provided sink/fullscreen arguments instead of replacing them
  with hidden wrapper preferences;
- can use the default Windows audio output through
  `wasapi2sink continue-on-error=true`, or mute receiver audio; the resilience
  property covers the sink's documented Windows endpoint failures and is not a
  claim that every native media error is nonfatal;
- keeps latency and audio controls in the normal settings, with Balanced
  latency selected by default; renderer selection and raw UxPlay arguments
  remain under Advanced settings;
- renames the stream window, gives a newly found renderer a provisional fit,
  captures an early phone-shaped size before the video-size debounce, orders
  correlated geometry/size events with a monotonic core-lifetime sequence, and
  lets repeated identical candidates retain the original 350 ms deadline so
  marker traffic cannot starve a stable decision; a duplicate stable value
  does not reopen the debounce, while a target-class change remains distinct;
  ignores later media-canvas ratios that do not match the learned device
  frame, and
  treats only the complete observed Photos `3840x2160 aux=0x0` signature as an
  ambiguous canvas when it arrives first; a later phone-shaped frame can still
  establish portrait, while unresolved automatic fits cannot overwrite a valid
  saved placement; the window refits when the selected device-frame/media-
  canvas class or exact aspect changes, including same-orientation changes,
  while a scaled marker with the same class/aspect is consumed without another
  move; the window also adapts on real portrait/landscape changes, and
  automatic fitting restores the learned proportions after a manual resize
  unless the user turns it off; for the exact correlated Photos/media
  signature, the 0.12.19 candidate keeps the trusted phone shape (or a
  conservative portrait fallback when Photos arrives first) but contains the
  complete frame at normal scale instead of using the unverified 0.12.18 cover
  transform; the former schema-12 Photos A/B key and the 0.12.17 incremental
  zoom controls are retired; property-backed fullscreen suspends every shell
  resize/save path, has a shell-owned no-activate enter/exit control, and uses
  bounded event-driven foreground Escape;
  the last normal position, size, and DPI are restored on the next session and
  clamped into an available monitor; saved bounds are applied from the early
  window-show event, unchanged native-window policy is cached, and the window
  can stay on top and remains on the taskbar by default;
- debounces Windows network events, keeps a healthy receiver running after a
  normal disconnect, and in 0.12.16 keeps idle discovery maintenance active:
  the first eligible re-registration runs after ten minutes and later attempts
  recur every 20 minutes while the receiver remains idle; a guarded Windows
  session unlock can request another refresh after cooldown, and active
  mirroring/client grace preserve due work; the capable core first prepared in
  0.12.13 services normal automatic
  idle, unlock, and
  discovery-health maintenance by refreshing the paired RAOP/AirPlay DNS-SD
  generation in the same process and on the same listener ports; active
  clients defer that operation; only the first two automatic command failures
  retain the legacy full-process fallback, while later failures keep the
  listener alive and rearm the recurring schedule;
  **Restart discovery** remains a deliberate full DNS-SD-and-BLE process
  restart, and a real physical IPv4 change also restarts because the separate
  BLE helper receives its advertised address at startup; an incoming high-
  level AirPlay request starts a fresh ten-minute epoch so maintenance cannot interrupt the
  next handshake, and a stale end marker from the previous session preserves
  the newer request/PIN grace instead of triggering deferred maintenance;
  after completed lost-client cleanup, the recovered
  native process and AirPlay port are preserved instead of immediately being
  replaced, while an ordinary clean disconnect still leaves the receiver
  running; after a capable native three-second warning, the shell can show
  continuity at a deterministic four-second local deadline, cancel it on
  earlier recovery, and show connection-restored/waiting-for-image while a
  current recovery is evaluated; the 0.12.8 proof gate carried into 0.12.9
  requires a matching
  current-PID/session/recovery-epoch Direct3D 11 present proof before fade and
  otherwise changes the waiting state to explicit Screen Mirroring reconnect
  guidance; Direct3D 12 and other advanced sinks retain that hint until they
  provide an equivalent reviewed proof; the view stays immediately above the
  renderer without taking focus; confirmed loss keeps a
  softened in-memory view of unobscured renderer client pixels, or a dark
  fallback otherwise, until a proven handoff or the user closes it;
- checks a configured public GitHub Release channel only when requested,
  accepts only exact three-part release tags such as `v0.12.5`,
  requires the exact versioned asset name
  `AeroMirror-Setup-<MAJOR.MINOR.PATCH>.exe`, displays curated release notes,
  and verifies the setup SHA-256 before launching an update;
- provides a basic diagnostic report, a local log, and a **Report a problem**
  action that prepares a separately redacted log snapshot and opens a
  pre-filled GitHub Issue; the user reviews and attaches the file manually;
- captures UxPlay stdout/stderr in the rotating log while masking PINs,
  passwords, MAC addresses, user-profile paths, and labelled cryptographic
  material; the current review line records feedback-gap totals, the full raw AirPlay
  geometry header, and the selected GStreamer decoder/sink without treating
  the raw auxiliary geometry pair as crop, PAR, or rotation metadata; the
  0.12.6 release also logs explicit native HTTP reset readiness/failure and
  a mirror-only capability marker; the 0.12.7 release adds typed-`TEARDOWN`
  connection-ownership and external-argument-pass-through markers; the 0.12.8
  candidate separates recovery epochs, video push/PTS/sink diagnostics, and
  the D3D11 presentation proof that alone can authorize continuity handoff;
  the 0.12.9 release also records guarded idle/unlock discovery maintenance
  and provisional Photos/media canvas fits; the 0.12.13 candidate adds
  request/generation/PID/port-correlated discovery results, paired DNS-SD
  generation health, BLE helper lifecycle, and receiver-name byte counts
  without logging the receiver name or mirrored content; the 0.12.14
  diagnostic candidate added a two-second numeric media-health summary covering
  mirror ingress, appsrc, sink, Present, pipeline state, and timestamp outcomes;
  the 0.12.15 candidate also records a validated-frame implicit-resume action
  without logging media content; the 0.12.16 shell records the unbounded
  renewal number while retaining content-free request/PID/port results;
- keeps streaming local to the LAN; the shell has no account, analytics, or
  cloud component.

The actual AirPlay handshake, decryption, H.264/H.265 decoding, audio, mDNS,
and player window come from UxPlay/uxplay-windows.

## System requirements

- Windows 10 version 1809 or newer, x64; or Windows 11, x64;
- the iPhone and PC on the same local network;
- local-network access allowed in Windows Firewall;
- HEVC-capable decoding for the 4K/60 preset.

The pinned redistributed runtime uses Qt 6.10.1 and GStreamer 1.28.1. The
separate reproducible native build/staging prefix uses GStreamer 1.28.5; it is
not the runtime downloaded by the public network installer. Qt 6.10.1
officially supports Windows 10 1809 x64 and newer. Windows 10 is outside Microsoft's normal consumer support lifecycle,
but remains an explicit application target. ARM64 and 32-bit packages are not
included.

## Unpublished 0.12.19 review candidate

The local candidate keeps every pixel of the encoded Photos frame, adds a
shell-owned fullscreen/exit control for both framed and actual fullscreen
states, and replaces timer polling with bounded event-driven Escape. It also
diagnoses one exact missing Private Bonjour mDNS firewall rule and offers only
an explicit, confirmed, UAC-gated narrow repair. It does not alter the Bonjour
service or remove that external rule during uninstall. Automated and pre-tag
package gates pass; physical Photos, fullscreen, DPI, firewall, and long-idle
iPhone rows remain PENDING. Version 0.12.18 remains the latest public release
until publication.

See the [0.12.19 release notes](docs/releases/0.12.19/RELEASE_NOTES.md) and
[test plan](docs/releases/0.12.19/TEST_PLAN.md).

## Latest public 0.12.18 review release

The source targets 0.12.18/`0.12.18.0`. Annotated tag `v0.12.18` and normal
GitHub Release `373984443` are the current updater-visible review release for
real-device gallery/fullscreen testing. All prior tags and assets remain
immutable.

The tray now has one direct **Полный экран (Esc — выйти)** action. While the
native D3D11 window actually covers its monitor without a frame, the shell
does not refit, resize, restore, remember, or persist it. This prevents a
Photos geometry change from turning fullscreen into an unmanageable borderless
window. Alt+Enter remains native behavior, and foreground Esc requests the same
native fullscreen toggle.

The three incremental photo-zoom controls are removed. For only the exact
recorded Photos 3840×2160 transport canvas, AeroMirror keeps a learned portrait
phone shape—or a conservative 900×1950 target when the session starts directly
in Photos—and applies one centered uniform fill. Fullscreen uses 100% scale;
the portrait fill returns after exit. A trusted landscape target stays
unscaled. This rule reads geometry only: it does not inspect pixels, infer a
general content rectangle, rotate the stream, or rewrite media.

Managed/native contract tests, two reproducible native builds, staged runtime
inspection, the loader test, the final 147-entry corresponding-source archive
plus extracted no-Git rebuild, exact tagged package, x64 Setup, API digests,
canonical/legacy latest routes, and fresh public re-download equality pass.
Installed update and physical Photos/Camera/rotation remain pending. See the
[0.12.18 release notes](docs/releases/0.12.18/RELEASE_NOTES.md) and
[test plan](docs/releases/0.12.18/TEST_PLAN.md); exact public identities are in
the [build report](docs/releases/0.12.18/BUILD_REPORT.md).

## Previous public 0.12.17 review release

Public `v0.12.17` remains immutable history with native
fullscreen and explicit 100–250% Photos zoom. Its exact evidence remains in the
[0.12.17 build report](docs/releases/0.12.17/BUILD_REPORT.md),
[release notes](docs/releases/0.12.17/RELEASE_NOTES.md), and
[test plan](docs/releases/0.12.17/TEST_PLAN.md).

## Prior public 0.12.16 review release

The published `v0.12.16` source targets 0.12.16/`0.12.16.0`. Its narrow change is
the long-idle receiver policy: AeroMirror no longer disables automatic DNS-SD
re-registration after two attempts. It waits ten minutes once, then refreshes
the paired RAOP/AirPlay registrations every 20 minutes while idle. Each normal
refresh stays inside the current core process and preserves both listener
ports. Mirroring and active-client grace defer the operation.

Only the first two automatic attempts in an idle epoch may use the historical
full-process fallback when the native refresh is unavailable, times out, or
fails. Later failures leave the receiver listening and try again on the next
20-minute deadline. **Restart discovery** remains the explicit strong action
that restarts DNS-SD and the separate BLE helper, and a physical IPv4 change
still requires that full restart.

For an already installed copy, an update started inside AeroMirror, a newer
Setup, or a same-version Setup reinstall proceeds without presenting the three
shortcut/launch choices again. The installer preserves whichever Start menu
and desktop shortcuts currently exist and starts AeroMirror after replacement.
A clean first install still presents the normal choices; installing an older
version over a newer one still requires explicit confirmation.

The managed Release build and complete resilience suite pass, including the
ten-minute first deadline, indefinite 20-minute recurrence, a saturating
counter, Windows-unlock recurrence, anti-churn, readiness and active-session
guards, and the first-two-attempt fallback boundary. Native source and runtime
remain the frozen 0.12.15 build. The managed update policy and installer
self-check pass. A fresh exact 13-entry review package, packaged-shell
resilience, x64 Setup with byte-exact embedded inputs and all three
non-installing self-checks, and corresponding-source build also pass after the
unattended-update change. A local Bonjour-ready result still does not prove
that an iPhone continuously lists the row; physical two-hour idle, lock/unlock,
sleep/wake, repeated browse checks, and a real installed update remain pending.
The normal updater-visible public review Release, exact four-asset set, API
digests, and fresh public re-downloads pass. See the
[0.12.16 build report](docs/releases/0.12.16/BUILD_REPORT.md),
[release notes](docs/releases/0.12.16/RELEASE_NOTES.md), and
[test plan](docs/releases/0.12.16/TEST_PLAN.md).

## Frozen 0.12.15 native-core candidate

The frozen 0.12.15 source targeted 0.12.15/`0.12.15.0`. It hardens the
supported default native receiver path from socket accept and HTTP/RTSP SETUP
through pairing, mirror/RTP/NTP parsing, crypto, worker shutdown, and GStreamer
renderer ownership. Mirror, HTTP, audio RTP, and NTP now share explicit
start/exit/stop/join semantics; accepted streams restore blocking mode and use
Windows-correct timeouts; protocol sizes, peer identity, allocation, and
partial startup are checked before state is published.

The media-specific recovery is deliberately narrow. After a type-0 video
access unit has been received completely, decrypted, and NAL-validated, its
arrival is authoritative evidence that the sender is active. If the renderer
still appears suspended, the core records
`AEROMIRROR_VIDEO_IMPLICIT_RESUME reason=valid-type0`, requests a nonblocking
resume, and delivers that same access unit. It does not use the experimental
leaky/max appsrc properties or intentionally discard the recovery frame.

Renderer selection, timestamp work, bus callbacks, reset, and destruction now
use lock-protected retained GStreamer references. A bus is mapped to its actual
video or audio renderer, and final video destruction waits for callbacks that
already acquired that renderer. These are source-level lifetime and recovery
corrections; they do not prove that the reported physical freeze is fixed.

A fresh complete native build, the exact production NIST AES-CTR happy-path
harness, eight production worker-lifecycle cases, source-bound core contracts,
and independent frozen-source review pass with no P0/P1 finding in the default
mirroring path. Two clean compatible builds reproduce core SHA-256
`38C6A63CE3CA40D3D1E23E5ECB5E0D152F9978986C4384A780C5767EAE0650A4`;
patch/provenance materialization passes with libuxplay patch SHA-256
`E8233FFD59BFC49181D32BBD64A6C94A338FD31939B28A18C7FC7A3B5F14195D`
and 37 libuxplay/41 total patched-source hashes. The source workflow creates a
validated 147-entry, 826,213-byte archive with SHA-256
`DA95EC58A17C37DA53948F770DABEAF29FAD75405CDF69F005F84ACF56362EB7`.
Its no-Git extracted tree validates all hashes and a clean 57/57 rebuild
reproduces the same core. Staged-runtime inspection (199 binaries, 148 DLLs,
44 features mapped to 27 plug-ins), manual `--loader-test`, the fresh managed
build, complete receiver resilience, and same-PID/same-port discovery-pipe gate
pass. The initial package/Setup gate now passes:
the thin ZIP has exactly 13 entries; the current and packaged shell are byte-
identical and pass resilience; Setup is x64 `0.12.15.0`, embeds the payload
  byte-for-byte, and passes all three self-checks. The focused final rebuild
  against the frozen embedded documentation also passes, including packaged-
  shell resilience, exact embedded payload/provenance matching, and all three
  Setup self-checks. Installed update and physical/public gates remain pending.
  There is no 0.12.15 tag, public
asset, Release, public installer, or
`BUILD_REPORT.md`. See the
[0.12.15 release notes](docs/releases/0.12.15/RELEASE_NOTES.md) and
[test plan](docs/releases/0.12.15/TEST_PLAN.md).

The last retained physical result is still the installed 0.12.13 run: one
H.265 picture appeared and then froze while the native process and control
session stayed responsive, and iPhone Stop ended the PC session immediately.
The 0.12.14 health diagnostics remain in 0.12.15, but neither a health class
nor an implicit-resume marker alone proves visible motion or root cause.

Photos inner-content detection/crop and Camera rotation remain unresolved.
Terminal join-failure parent lifetime, broader audio/HLS synchronization,
remaining startup assertions, optional PIN/SRP depth, and tolerant dual
teardown consolidation remain explicit P2 follow-up. Local discovery readiness
still cannot force iOS browse-cache invalidation, and BLE in-process refresh,
AWDL, and AirDrop remain separate. Internal 0.12.10–0.12.15 candidates are not
renumbered or published. The 0.12.17 review release adds presentation controls;
all earlier public assets remain immutable history.

## Installer: recommended

For normal use, open the
[latest AeroMirror release](https://github.com/pyram1da/aeromirror/releases/latest)
and download:

```text
AeroMirror-Setup-0.12.18.exe
```

`v0.12.18` is the normal updater-visible review Release for physical Photos
and fullscreen testing. Its managed/native contracts, reproducible source,
13-entry review package, Setup lifecycle, public API digests, and fresh
re-downloads pass. It keeps 0.12.16 recurring same-process discovery
maintenance, replaces 0.12.17 manual Photos zoom with automatic portrait fill,
and makes fullscreen state/exit safe. Installed-update and physical
Photos/Camera/rotation acceptance remain pending; earlier published assets are
not replaced.

Scope and pending physical acceptance are in the
[0.12.18 release notes](docs/releases/0.12.18/RELEASE_NOTES.md) and
[test plan](docs/releases/0.12.18/TEST_PLAN.md). Exact tag, public assets,
digests, and re-download evidence are in the
[build report](docs/releases/0.12.18/BUILD_REPORT.md). The historical
[0.12.8 release notes](docs/releases/0.12.8/RELEASE_NOTES.md) and
[test plan](docs/releases/0.12.8/TEST_PLAN.md) remain available; 0.12.8 was
never tagged or published. Published 0.12.7 remains immutable history.

The canonical repository is now `pyram1da/aeromirror`. AeroMirror 0.12.18 still
contains the former `Nadejny/aeromirror` updater slug; GitHub redirects its
`releases/latest` API to the canonical repository. Canonical and legacy API,
HTML/latest, and Setup routes resolve to the same verified `v0.12.18`
Release.

The installer:

- is a **network review installer**: it downloads the unchanged pinned
  `uxplay-windows` runtime directly from the upstream GitHub Release, verifies
  SHA-256, and fails closed if the download or checksum is wrong;
- installs for the current Windows user without an administrator prompt;
- places the application under
  `%LOCALAPPDATA%\Programs\AirPlayReceiverMvp` (the legacy internal path is
  retained so v0.7/v0.8 upgrade in place);
- adds AeroMirror to Windows **Installed apps**;
- asks about Start menu, desktop, and post-install launch only on a clean first
  install;
- closes the setup window before launching the receiver when installation
  finishes;
- updates or reinstalls an existing copy without reopening that option form,
  preserves the current shortcut choices, relaunches AeroMirror, and rolls back
  application files plus installer metadata if replacement fails;
- refuses to downgrade a newer installed version automatically and retains the
  explicit downgrade confirmation;
- keeps the exact pinned upstream runtime in a content-addressed local cache
  after SHA-256 verification, so a reinstall or later update using the same
  runtime does not download the 100+ MB archive again;
- includes an uninstaller while preserving user settings by default.

An internet connection is therefore required during installation. The pinned
third-party asset, source location, and checksum are recorded in
[UPSTREAM.lock](UPSTREAM.lock) and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). AeroMirror's Release does
not mirror or silently fall back to another runtime.

AeroMirror Setup extracts the pinned portable Qt/GStreamer application
runtime, but installs no system-wide .NET/VC++ redistributable, driver, or
framework prerequisite; a full Windows reboot is not an expected normal
completion step. Bonjour is a separate machine-wide discovery service and may
prompt for elevation when it is absent. One Windows 10 first-install report
worked only after reboot, but the cause was not retained; a stopped or stale
Bonjour lifecycle is only a hypothesis. The 0.12.9 release does not mutate
that service. Diagnose a repeat on a clean Windows 10 VM before rebooting, as
described in
[troubleshooting](docs/TROUBLESHOOTING.md).

The installer is currently unsigned, so Windows SmartScreen may display an
unknown-publisher warning. Code signing is required before a broad public
release. The GitHub-provided digest and HTTPS protect against corruption and
an accidental mismatch; until Authenticode publisher verification is added,
they are not a substitute for a signed release if the repository account
itself were compromised.

## Portable build: local testing only in 0.11

The offline portable package is intentionally **not attached** to the 0.11
review Release. It contains the full Qt/GStreamer/FFmpeg/MSYS2 DLL closure,
whose per-file source and license inventory is still being completed. Use the
network installer for review distribution.

Maintainers can still build the portable ZIP locally for engineering tests.
Windows does not register that variant as an installed application, and
deleting or cleaning its folder deletes the program.

1. Extract the whole ZIP to a normal folder. Do not run it from inside the ZIP.
2. Start `AeroMirror.exe`.
3. Allow network access if Windows Firewall asks.
4. If Bonjour is missing, the bundled core may ask for administrator
   permission to install its mDNS service.
5. Put the iPhone and PC on the same local network.
6. On iPhone, open **Control Center → Screen Mirroring** and select the PC name.
7. Use the tray icon to change settings or stop the receiver.

On a Windows **Private** physical network, a fresh installation accepts a
connection without a PIN. PIN protection remains available there as an
optional extra layer. On a Windows **Public** network, the shell pauses an
unprotected receiver until you enable PIN protection. The PIN is generated
and shown in the settings window, so the iPhone never asks for an invisible
code.

VPN, tunnel, Hyper-V, and other virtual profiles do not determine whether the
LAN is trusted. The UI shows the exact physical profile name and Windows
category. If Windows itself marks the physical Wi-Fi/Ethernet as Public while
a VPN is active, AeroMirror remains fail-closed: disconnect the VPN and repeat
the check, change the physical Windows profile to Private only when it really
is a trusted network, or use PIN protection. A personal hotspot is never
enabled automatically.

Settings and logs are stored under:

```text
%LOCALAPPDATA%\AirPlayReceiverMvp
```

Supported settings are normalized when loaded and before saving. AeroMirror
writes a same-directory temporary settings file and atomically replaces the
previous `settings.ini`, so an interrupted save does not publish a partially
written new configuration. Receiver keys and trusted-client data are separate
files and are not replaced by a normal settings save.

Receiver diagnostics are written to `receiver.log`; installer and update
failures are written to the separate rotating `setup.log`.

The **Report a problem** link creates a temporary, additionally redacted
snapshot and opens GitHub. Browsers do not allow AeroMirror to attach a local
file silently, so Explorer selects the snapshot and the user drags it into the
Issue after reviewing it. Nothing is uploaded automatically.

For a reproducible bug report, follow
[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md). Never publish
`settings.ini`, `receiver-key.pem`, or `trusted-clients.txt`.

The patched core receives arguments directly from the shell. AeroMirror does
not duplicate the PIN in uxplay-windows' legacy `arguments.txt` file.

## Build the shell locally

No downloaded SDK is required on a standard Windows 10/11 installation. The
build script uses the C# compiler included with .NET Framework:

```powershell
.\build.ps1
```

If Windows marks a locally reviewed script as downloaded, inspect it first and
unblock that one file only:

```powershell
Unblock-File .\build.ps1
```

The result is:

```text
artifacts\Release\AeroMirror.exe
```

Create the current thin review candidate payload and build the per-user network
installer from that exact ZIP with:

```powershell
.\package-review.ps1 `
  -Version 0.12.18 `
  -HeadlessRuntimePath .\artifacts\headless-runtime

.\build-installer.ps1 `
  -Version 0.12.18 `
  -PortableZip .\artifacts\AeroMirror-review-payload-x64-0.12.18.zip
```

The result is:

```text
artifacts\installer\AeroMirror-Setup-0.12.18.exe
```

Public release names use three-part semantic versions such as `0.12.18`.
Windows executable metadata internally requires four numeric fields and may
show `0.12.18.0` in a file-property dialog; the AeroMirror UI and a future
GitHub Release would intentionally show only `0.12.18`.

For local offline engineering tests, create the full portable package with
both explicit inputs:

```powershell
.\package.ps1 `
  -Version 0.12.18 `
  -UxPlayPortablePath .\artifacts\headless-runtime `
  -HeadlessCorePath .\artifacts\headless-runtime\uxplay-windows.exe
```

`package.ps1` now rejects a runtime without the reviewed headless build
manifest, requires the patched executable explicitly, verifies its hash after
staging, and writes a versioned local ZIP. Do not attach that offline ZIP to
the current review Release. The network installer instead downloads the
unchanged pinned upstream asset at install time and verifies the locked
SHA-256.

### Rebuild the reviewed native core

`AeroMirror-native-source-0.12.18.zip` is a prepared corresponding-source
archive: the `uxplay-windows` and `libuxplay` patches are already applied, so
do not apply them a second time. After providing the pinned Qt 6.10.1 and
MSYS2 toolchains listed in
`AeroMirror-build-inputs\BUILD_INFO.md`, run from the extracted archive:

```powershell
# Use a short extraction path: the MinGW/CMake object tree can exceed the
# Windows filename limit under a deeply nested Downloads/workspace folder.
$source = Resolve-Path .\AeroMirror-native-source-0.12.18\uxplay-windows
& "$source\AeroMirror-build-inputs\build-compatible-core.ps1" `
  -UpstreamRoot $source `
  -Qt610Prefix C:\path\to\Qt-6.10.1 `
  -MsysRoot C:\path\to\msys64
```

The script validates both reviewed patch files, every pinned modified source
and build input, the distinct public GStreamer 1.28.1 and build-time GStreamer
1.28.5 contracts, the bundled
Bonjour header and `dnssd.def` against
`source-provenance.json`. It copies the verified header into the prepared
Bonjour SDK layout, generates the x64 `dnssd.lib` import library with MSYS2
`dlltool`, and rejects a resulting executable whose SHA-256 differs from the
reviewed core hash. It fails early with a clear instruction if the extraction
path is too long for the MinGW/CMake object layout. Git metadata is not
required in the prepared archive; when building from Git checkouts, the same
script additionally verifies both pinned commits.

## Source layout

```text
AGENTS.md                   stable entry point for coding agents
src/
  Properties/
    AssemblyInfo.cs          managed assembly metadata
  Application/
    AppVersion.cs            public display version from assembly metadata
    Program.cs               process startup and single-instance behavior
  Configuration/
    AppSettings.cs           settings migration, validation, and atomic save
  Receiver/
    ReceiverContext.cs       tray application context and shared state
    ReceiverContext.Core.cs  native lifecycle and discovery/recovery policy
    ReceiverContext.HttpReset.cs native HTTP reset marker state
    ReceiverContext.Rendering.cs renderer-window sizing and Win32 policy
    ReceiverContext.LostConnection.cs fatal-loss placeholder lifecycle
    ReceiverContext.Diagnostics.cs logging and problem-report workflow
  UI/
    SettingsForm.cs          active home/settings/updates window
    DiagnosticsForm.cs       diagnostic text viewer
    LostConnectionForm.cs    softened reconnect placeholder window
    ThemeHelper.cs           light/dark control styling
    AppIcon.cs               embedded application icon loader
    Controls/
      NamedValue.cs
      WheelSafeComboBox.cs
  Updates/
    UpdateInfo.cs
    UpdateService.cs         strict release parsing, download, and verification
  Network/
    NetworkProfileInfo.cs
    NetworkSafety.cs         physical-profile trust and adapter filtering
  Interop/
    NativeMethods.cs         Win32 process and renderer-window interop
assets/
  logo.png                  transparent application logo
  AirPlayReceiver.ico       multi-resolution executable and tray icon
build.ps1                 builds the Windows shell
build-installer.ps1       embeds the thin review payload in the per-user setup
release.ps1               builds/copies versioned release assets and checksums
build-native-source.ps1   packages exact corresponding native source
app.manifest              Windows 10/11, DPI, and asInvoker declarations
package.ps1               combines the shell with the official core bundle
package-review.ps1        makes the thin, pinned network-installer payload
update-repository.txt     public OWNER/REPOSITORY used for release checks
download-core.ps1         fetches and verifies the pinned upstream core
UPSTREAM.lock             exact upstream release, commit, and SHA-256
CHANGELOG.md              user-facing release notes
LICENSE                   license for this project
THIRD_PARTY_NOTICES.md    component and license inventory
native-core/
  README.md                native core pins and build notes
  build-compatible-core.ps1 builds against the pinned Qt 6.10.1 SDK
  dnssd.def                import definition for the bundled mDNS library
  gstreamer-features.txt   exact plug-ins staged for this build
  build-headless-runtime.ps1
  source-provenance.json   reviewed commits, patches, sources, and core hash
  uxplay-windows-headless.patch
  libuxplay-aeromirror.patch
installer/
  AirPlayReceiverSetup.cs  per-user installer and uninstaller
docs/
  ARCHITECTURE.md            integration boundary and next steps
  PROJECT_STATE.md           current release, blockers, and immediate handoff
  DECISIONS.md               durable product and architecture choices
  DOCUMENTATION_POLICY.md    mandatory documentation for every patch
  RELEASE_AND_SIGNING.md     GitHub updates, Store, and signing plan
  TROUBLESHOOTING.md         log collection and first-run reproduction
  TODO.md                    product and protocol roadmap
  releases/
    0.12.18/
      RELEASE_NOTES.md       automatic Photos layout review-release summary
      TEST_PLAN.md           fullscreen-state and gallery acceptance gates
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.17/
      RELEASE_NOTES.md       Photos presentation review-release summary
      TEST_PLAN.md           fullscreen, zoom, rotation, and package gates
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.16/
      RELEASE_NOTES.md       persistent idle-discovery review summary
      TEST_PLAN.md           recurring visibility and fallback gates
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.15/
      RELEASE_NOTES.md       native-core hardening candidate summary
      TEST_PLAN.md           lifecycle, parser, renderer, and physical gates
    0.12.14/
      RELEASE_NOTES.md       media-liveness diagnostic candidate summary
      TEST_PLAN.md           timestamp, health, and physical freeze gates
    0.12.13/
      RELEASE_NOTES.md       persistent discovery candidate summary
      TEST_PLAN.md           same-PID discovery and fallback gates
    0.12.12/
      RELEASE_NOTES.md       bounded idle-discovery candidate summary
      TEST_PLAN.md           timed stages and physical idle gates
    0.12.11/
      RELEASE_NOTES.md       automatic Photos-fitting candidate summary
      TEST_PLAN.md           settings, geometry, package, and physical gates
    0.12.10/
      RELEASE_NOTES.md       prior local geometry/test-isolation summary
      TEST_PLAN.md           prior geometry, logs, and physical gates
    0.12.9/
      RELEASE_NOTES.md       public discovery and Photos review summary
      TEST_PLAN.md           discovery, Photos, install, and reconnect gates
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.8/
      RELEASE_NOTES.md       evidence-gated reconnect candidate summary
      TEST_PLAN.md           recovery-epoch and present-proof acceptance
    0.12.7/
      RELEASE_NOTES.md       media-session continuity hotfix summary
      TEST_PLAN.md           Photos/video session-continuity acceptance
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.6/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           renderer, Photos, and reconnect acceptance
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.5/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           Photos-first and recovery acceptance matrix
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.4/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           recovery, frame-pacing, and renderer acceptance
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.3/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           loss, placement, and Photos acceptance matrix
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.2/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           reconnect, orientation, and fitting acceptance matrix
      BUILD_REPORT.md        published tag, assets, hashes, and test status
    0.12.0/
      RELEASE_NOTES.md       curated GitHub Release text
      TEST_PLAN.md           candidate acceptance and physical test matrix
      BUILD_REPORT.md        published tag, assets, hashes, and test status
  BUILD_REPORT*.md           immutable 0.11 release verification history
  TEST_PLAN_0.11.*.md        immutable 0.11 acceptance history
```

The managed files under `src/` still compile into one `AeroMirror.exe`.
Splitting them by responsibility is a maintenance boundary, not a new process,
plugin model, public API, settings location, or native IPC protocol. The
current `SettingsForm` intentionally remains one file in this conservative
pass; deeper UI extraction should be reviewed separately from receiver fixes.

## MVP limitations

- The portable package is x64-only.
- The native receiver is a minimally patched build of `uxplay-windows` 2.0.
  Its `--headless` mode removes the upstream tray, leaving one application
  icon. The remaining Qt UI code is still linked into the core and can be
  split into a smaller dedicated process later.
- The stream is rendered in the GStreamer window, not embedded inside the
  settings window.
- Portrait/landscape rotation is carried by the AirPlay stream and supported
  by UxPlay. The renderer should follow the iPhone automatically; iPhone
  Rotation Lock naturally prevents source rotation. This still requires
  device-by-device testing.
- Renderer-window detection is heuristic. AeroMirror gives a newly opened
  renderer a provisional fit and can retain an early phone-shaped raw marker
  before a later stable media-canvas marker wins the debounce. The 0.12.11
  candidate retains monotonic core-lifetime ordering from 0.12.10;
  identical pending candidates keep the original 350 ms deadline, so they
  cannot starve the decision. Its exact fit state follows a change in target
  class or exact aspect even when portrait/landscape class stays the same, and
  suppresses another move for a scaled marker with the same class/aspect.
  Later ratios within a small tolerance can reshape the client area for real
  portrait/landscape rotation. Only the complete correlated Photos signature
  with primary, source, and encoded `3840x2160` plus auxiliary `0x0`
  automatically becomes a provisional outer-window landscape target; it does
  not replace the trusted device baseline, and a later `998x2160` device frame
  returns the window to portrait. Other media-only sessions remain ambiguous
  until an authoritative frame or session.
  Automatic fitting restores the learned aspect after a completed manual
  resize by default, respects an explicit opt-out, and keeps **Restore window
  proportions** as a manual fallback that also uses the learned device frame
  rather than a later non-matching media canvas. Normal renderer bounds and
  their DPI are saved across sessions; stale/off-screen bounds are moved into
  an available Windows work area.
- A Photos `3840x2160` stream can contain the photo and black bars inside the
  encoded canvas itself. AeroMirror can keep the outer phone orientation, but
  it does not yet have native content-rectangle metadata or safe pixel analysis
  with which to crop or zoom that inner canvas. Photos may therefore still look
  very small even when the outer renderer proportions are correct. The 0.12.11
  candidate applies the exact recorded Photos/media outer-window fit
  automatically instead of exposing the former default-off A/B. It changes no
  pixels and cannot enlarge inner content. That automatic provisional
  landscape is not trusted or persistable; an explicit user move or resize
  remains a separate user-owned placement action.
- After an abnormal Wi-Fi/client loss, AeroMirror preserves UxPlay's recovered
  process and listening port after completed in-process cleanup. The first
  stale row tapped on iOS may still fail before any request reaches Windows,
  and iOS may take time to refresh its browse cache. In 0.12.13, automatic
  idle/unlock/native-health maintenance prefers a current-request paired DNS-
  SD refresh in that same process and on the same RAOP/AirPlay ports. Bonjour
  callbacks prove only local registration for the new generation; they do not
  continuously attest iPhone visibility or force a phone to discard a cached
  row. The unchanged BLE helper is not refreshed in place, so manual discovery
  and a real physical IPv4 change still use full-process recovery. The older
  0.12.16 keeps the first ten-minute deadline and then repeats same-process
  re-registration every 20 minutes while idle. Only the first two automatic
  attempts may fall back to a full process restart; later failures retain the
  listening core and retry. Physical long-idle behavior is still pending.
- The 0.12.15 candidate retains the 0.12.14 checked video-PTS mapping and
  passive two-second health summaries, adds explicit worker/parser/renderer
  ownership, and may request a nonblocking implicit resume only after a fully
  validated video access unit. It has not yet repeated the physical frozen-
  last-frame run; do not treat `class=healthy`, an implicit-resume marker, or
  any individual counter as visible-motion proof or root-cause confirmation.
- In the first public 0.12.7 physical smoke, a reporter-estimated wall-clock
  Wi-Fi interruption of about ten seconds recovered automatically; the exact
  log interval records a five-second feedback gap. After a reporter-estimated
  wall-clock interruption of about 15 seconds, whose exact log interval records
  an 11-second feedback gap, reconnect cleared the continuity placeholder but
  video remained frozen; closing AeroMirror briefly exposed the latest frame.
  That longer reconnect/handoff path remains unresolved.
- The untagged 0.12.8 correction, carried into the 0.12.9 release, addresses
  only the misleading handoff decision for that path. Feedback recovery,
  appsrc push/PTS, sink observation, and
  a visible cached renderer cannot close continuity; a matching current-PID,
  current-session, current-epoch D3D11 presentation proof is required. If it
  never arrives, AeroMirror keeps the view and shows a reconnect hint. This
  includes Direct3D 12 and other advanced sinks; Interactive `-vsync no`
  deliberately skips this synchronized proof and retains the hint. The
  public review release does not yet repair the underlying
  long-gap video freeze. Selecting the
  receiver again may arm a new proof epoch, but mirror-start alone keeps the
  continuity view visible until matching D3D11 present proof arrives.
- The 0.12.6 release accepts same-process HTTP recovery only after an
  explicit current-PID marker confirms the original AirPlay port; bind failure
  or mismatch exits for full-process recovery. This still does not prove
  DNS-SD/BLE re-publication or force iOS browse-cache refresh.
- The 0.12.7 release removes an immediate server-forced control-socket
  removal from the typed AirPlay `TEARDOWN` handler while retaining upstream's
  typed-stream behavior and `Connection: close` response header. The affected
  0.12.6 log proves a software-request disconnect, not which disconnect call
  site ran; the new marker makes that request type directly testable.
- Normal audio in 0.12.7 explicitly selects the redistributed GStreamer
  1.28.1 runtime's `wasapi2sink continue-on-error=true` behavior for documented
  endpoint open, I/O, and removal failures. Other native audio/decoder/video
  bus errors are not claimed to be isolated from the session.
- AeroMirror continues the 0.12.6 mirror-only experiment of omitting
  unimplemented AirPlay photo, slideshow, and photo-preload advertisement bits;
  0.12.7 inherits this behavior. Physical direct-in-Photos behavior remains
  pending, and this is not a crop/zoom fix.
- The continuity placeholder keeps only a softened renderer-client screenshot
  in process memory, writes no mirrored frame to disk, and uses a dark fallback
  whenever another visible window overlaps the renderer. With the patched
  feedback-health marker, a native three-second warning schedules it for a
  four-second local deadline. Recovery before that deadline cancels the view.
  In the 0.12.8 proof gate carried into 0.12.9, feedback after the view appears
  changes it to
  waiting, but only a correlated D3D11 present proof may begin the 180 ms fade;
  absent proof becomes explicit reconnect guidance. It does not make iOS
  discovery, native video recovery, or reconnection instantaneous.
- Arbitrary window proportions cannot both fill the window and preserve the
  whole phone image: the alternatives would be black bars, stretching, or
  cropping. This MVP preserves the whole image and changes the window shape.
- The stream window is an external GStreamer HWND. A Mac-style hover-only
  frame, true borderless viewer, and live aspect lock during an edge drag need
  an embedded native rendering surface and versioned IPC; the shell does not
  cross-process-subclass that foreign window.
- "Always on top" or automatic fitting may need to be toggled again with an
  unusual GStreamer sink.
- The shell combines listening sockets with explicit DNS-SD/BLE health markers
  and a legacy Bonjour fallback before reporting startup readiness. The
  0.12.13 core additionally provides a narrow version-1 redirected-stdin and
  framed-stdout discovery-maintenance protocol. It is intentionally not a
  general named-pipe status/RPC contract and does not prove that an iPhone is
  browsing or that an AirPlay session is active.
- The executables are not yet code-signed, so Windows SmartScreen may warn
  about an unknown publisher.
- GitHub update checking in 0.12.7 uses the former `Nadejny/aeromirror` slug;
  GitHub redirects it to canonical `pyram1da/aeromirror`, and both latest API
  routes returned the same public `v0.12.7` Release ID.
- Bonjour/mDNS and Windows Firewall remain external system dependencies. The
  0.12.19 candidate can explicitly add only the exact Private/UDP
  5353/LocalSubnet rule for the validated Bonjour executable after confirmation
  and UAC; it does not change the Bonjour service or clean the rule on uninstall.
  Allow the receiver only on intended network categories. Some managed or guest
  Wi-Fi networks block device discovery.
  AeroMirror extracts a portable app runtime but installs no system-wide
  .NET/VC++ redistributable, driver, or framework prerequisite and should not
  normally require a full Windows reboot. Bonjour is machine-wide, so an
  uninstall/reinstall on the same PC cannot reproduce a truly clean first
  install; one unproven Windows 10 reboot report remains scheduled for a clean
  VM test, without automatic Bonjour service mutation.
- Public-network detection follows active physical Windows network profiles.
  A wrongly classified physical profile can still produce a conservative
  warning; fix the category in Windows or enable PIN.
- DRM-protected playback is not supported.
- No AirDrop, AeroDrop companion, clipboard sync, remote input, recording UI,
  multi-device UI, virtual camera, OBS integration, or `Win+K` integration.
  AirDrop is separate from AirPlay and requires its own Bluetooth/AWDL,
  identity, trust, and encrypted-transfer implementation.
- Phone notifications, SMS, and call handling are not included. The app's
  notification option only covers failures and unsafe network status; a normal
  Windows autostart is silent.
- PIN registration behavior is provided by UxPlay and can vary with iOS
  versions and stored pairing records.

## Quality presets

- **HD 720p / 30 FPS** requests the lowest workload for a weak network or PC.
- **Full HD 1080p / 30 FPS** keeps Full HD while halving the maximum frame rate.
- **Full HD 1080p / 60 FPS** is the default.
- **4K / 60 FPS** enables HEVC and provides the highest requested quality.
  It worked well during testing on the target iPhone/PC, but still requires
  compatible HEVC decoding and substantially increases network, decoder, and
  GPU load.

These are receiver capabilities advertised to the iPhone, not a guarantee.
The source device can send a lower resolution or frame rate. UxPlay accepts a
120 FPS request, but iPhone mirroring is not guaranteed to provide it, so the
main UI does not advertise a misleading 120 FPS preset.

Changing quality, FPS, receiver name, PIN mode, renderer, latency, or raw
UxPlay arguments restarts the native receiver because these capabilities are
advertised when the AirPlay service/session starts. Stop Screen Mirroring on
the iPhone and connect again to guarantee a new quality negotiation. UI-only
settings such as notifications, close-button behavior, general window fitting,
and always-on-top save without restarting an otherwise running receiver. The
retired Photos-specific key is ignored and omitted on save.

The last saved quality preset is retained. The Save button compares the
controls with the saved settings, so changing a preset and returning to the
original preset disables Save again.

## Latency profiles

- **Balanced** keeps UxPlay's native timestamp synchronization and buffering.
- **Interactive** disables timestamp scheduling with `-vsync no` without the
  former 50 ms audio-buffer request. Motion may feel more immediate, but
  audio/video synchronization can be less exact.
- **Stable** reports a 350 ms audio buffer and adds visible delay.

The receiver now defaults to an explicitly pinned Direct3D 11 decoder family
and video sink. Existing profiles that still used automatic GStreamer
selection migrate to Direct3D 11; an explicit Direct3D 12 choice remains
available as an experimental comparison. Advanced UxPlay arguments remain
after the managed choice and can override it for diagnostics. UxPlay selects
the codec-matched decoder at pipeline creation. In the 0.12.7 release, the
headless wrapper also preserves external `-vs`/`-fs` arguments rather than
replacing them with persisted Qt preferences. Physical Direct3D 11 versus
Direct3D 12 Photos and resolution-change testing is still pending.

AirPlay itself and the iPhone encoder still add latency. Best results require
the PC on Ethernet, the iPhone on strong 5/6 GHz Wi-Fi, no VPN in the local
path, and no guest-network/client isolation. Public internet speed is not the
AirPlay media path: local packet loss, interference, Wi-Fi scheduling, decode,
and frame pacing matter. AeroMirror's optional Bluetooth beacon helps
discovery only; it does not carry or combine bandwidth with the Wi-Fi video.

## License

The AeroMirror-authored shell, installer, and build scripts are
GPL-3.0-or-later. The patched UxPlay core and every downloaded runtime
component remain under their respective upstream licenses. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

The current license inventory is an engineering review, not legal advice.

## Sharing a build

For the public 0.12.18 review Release, share the GitHub Release page or its
network Setup—not a loose `AeroMirror.exe`. Project policy keeps these assets
together and immutable:

- `AeroMirror-Setup-0.12.18.exe`;
- `AeroMirror-source-0.12.18.zip`;
- `AeroMirror-native-source-0.12.18.zip`;
- `SHA256SUMS.txt`.

The native source archive contains the exact prepared `uxplay-windows` and
`libuxplay` trees, both AeroMirror patches separately and already applied,
`source-provenance.json`, the actual Bonjour interface header, `dnssd.def`,
and the hash-validating build recipe that generates `dnssd.lib`. The offline
portable/full runtime remains unpublished until its complete runtime SBOM and
corresponding source set are ready.

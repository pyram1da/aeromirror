# Project decisions

This file records durable choices and their rationale. Update it when a
decision changes; do not use it as a task list.

## D-001 — Keep the Windows shell and native receiver in separate processes

**Status:** accepted

`AeroMirror.exe` owns the Windows UI, settings, network safety, updates,
diagnostics, and process supervision. The native UxPlay-based executable owns
AirPlay protocol handling, decode, audio, and rendering.

The boundary provides crash isolation and allows the native core to restart
without losing the tray application. Combining them would require a major
native UI or interop rewrite and would not inherently reduce media latency.
A future change should introduce versioned local IPC before reconsidering the
process boundary.

## D-002 — Use the physical Windows network for routing and diagnosis, not pairing policy

**Status:** accepted

Every previously unknown device now uses the per-device PIN trust contract in
D-016, including on a Windows Private network. Public/Unknown therefore never
selects an unprotected mode, and Private never silently disables pairing.

The physical Wi-Fi/Ethernet profile still supplies the preferred IPv4 address,
network name, and visible Windows category. VPN, tunnel, Hyper-V, and other
virtual adapters do not replace that physical route or redefine its category.
This preserves useful diagnosis without asking a normal user to choose an
access-control mode from Windows' sometimes confusing network labels.

## D-003 — Use physical IPv4 for receiver startup and discovery

**Status:** accepted

The core waits for a preferred, non-APIPA, non-SkipAsSource IPv4 on the active
physical adapter. DNS-SD remains the primary LAN discovery path; the optional
BLE beacon advertises the same physical address instead of selecting a route
through a VPN.

Readiness requires listening sockets and successful publication of the paired
RAOP/AirPlay DNS-SD records. BLE is supplemental discovery and must not
substitute for failed or unavailable DNS-SD publication.

## D-004 — Use normal GitHub Releases for the review update channel

**Status:** accepted

Installed clients read GitHub `releases/latest`, so a testable review build
is published as a normal Release and labelled as a review candidate in its
title/body. GitHub's Pre-release flag is not used because clients would not
see it.

Updates download the complete small Setup instead of applying line-level or
binary deltas. The large pinned runtime is stored in a verified
content-addressed cache and reused when unchanged.

Never replace an asset under an already published version. Post-release fixes
receive a new patch version.

## D-005 — Keep public release communication in English

**Status:** accepted

`CHANGELOG.md`, GitHub Release bodies, release plans, test plans, and new
maintainer documentation are written in English. The existing Russian UI is
not partially translated through scattered literals.

## D-006 — Localize from resources with a system default and manual override

**Status:** planned for 0.12

At first launch, AeroMirror should follow the Windows display language. Users
can override it through a `System / English / Russian` setting. `System`
continues following Windows after later OS language changes.

All user-visible strings should move to typed resource sets with English as
the invariant fallback. Settings store a stable culture choice rather than a
translated display label. Changing language should update the shell without
changing receiver identity, pairing, or network state.

## D-007 — Separate documentation scaffolding from source reorganization

**Status:** accepted

The 0.11.1 stability tag keeps the existing source layout. Agent guidance,
current state, and decisions are added as post-release documentation. Any
directory moves, project splits, or namespace changes begin in an explicit
0.12 development change with a migration map and build verification.

## D-008 — Do not call the project 1.0 before physical acceptance

**Status:** accepted

Automated tests cannot prove AirPlay interoperability. A 1.0 designation
requires the current manual test plan to pass on at least one physical
Windows 10 PC and one physical Windows 11 PC, including delayed Wi-Fi, VPN,
sequential sessions, and connection loss.

## D-009 — Keep one managed assembly and organize stateful classes with partial source files

**Status:** accepted for 0.12

The managed shell remains one .NET Framework `AeroMirror.exe` assembly. Source
is grouped by responsibility, and the stateful `ReceiverContext` is divided
across partial-class files for core lifecycle, rendering, and diagnostics while
retaining one private state owner.

This keeps the 0.12 move mechanical: it does not introduce another managed
process, plugin model, public API, serialization boundary, dependency-injection
container, or new runtime requirement. Existing namespace, mutex/event names,
settings and log paths, autostart and update identities, receiver key and trust
state, native process contract, and installed core path remain unchanged.

The active `SettingsForm` stays in one file during this pass because splitting
its tightly coupled WinForms construction and navigation at the same time would
add review risk without changing product behavior. Further service extraction
or UI decomposition requires a separate design, tests, and migration plan.

## D-010 — Keep file transfer separate from the AirPlay receiver boundary

**Status:** accepted

Appearing as an AirPlay/Apple TV receiver does not make AeroMirror an AirDrop
target. Genuine AirDrop interoperability requires a separate Bluetooth/AWDL,
identity, trust, and encrypted-transfer implementation with independent
hardware, driver, license, privacy, and security review. It must not be added
to the UxPlay receiver process or its firewall surface merely because AirPlay
discovery already works.

The lower-risk staged alternative is a separately named **AeroDrop** product
path using an iOS Share Extension or companion and an authenticated local
transfer. It still requires explicit acceptance, safe destination and filename
rules, transfer limits, quarantine policy, and physical-device tests. This
decision defines the architecture boundary; it does not commit 0.12.4 or 1.0
to either transfer implementation.

## D-011 — Keep unpublished candidate numbers as internal history

**Status:** accepted

A version number identifies the exact source and artifact candidate that was
built and tested, even when that candidate is never published. Superseded
internal candidates are recorded in the changelog and versioned plans but do
not receive reconstructed tags, draft Releases, prereleases, or relabelled
assets. The next public Release may therefore skip one or more patch numbers.

Do not rename a later candidate to fill a public numbering gap. Doing so would
collide with existing local artifacts, invalidate provenance and test evidence,
and make numeric update comparisons wrong for machines already running an
intermediate build. A corrective change after a frozen candidate always gets a
newer version; a published tag and its assets remain immutable under D-004.

The local 0.12.21 candidate is one such unpublished number. Its manual
**Start Bonjour**/`sc.exe` design was superseded by 0.12.22 before publication;
no `v0.12.21` tag, draft, prerelease, normal Release, or reconstructed asset set
may be created.

## D-012 — Keep idle AirPlay DNS-SD maintenance recurring in place

**Status:** accepted

The normal receiver is a long-lived tray service, so automatic discovery
maintenance must not silently expire merely because the machine has been idle
for more than two scheduled checks. A fresh idle epoch waits ten minutes once;
subsequent eligible maintenance recurs every 20 minutes for the lifetime of the
running receiver. The preferred operation is the request-correlated native
refresh of the paired RAOP/AirPlay DNS-SD generation in the same process and on
the same ports.

Real AirPlay/PIN/client activity and active mirroring take priority and defer
maintenance. To avoid turning a local registration problem into recurring
process churn, only automatic renewals one and two may use the historical full-
process fallback. Later failure leaves the listener alive and schedules the
next same-process attempt. The normal UI exposes no discovery-restart or
Bonjour-repair button. A real physical IPv4 change remains an internal full
DNS-SD-and-BLE restart because the helper address cannot change in place.

A known stopped Bonjour service is an unavailable external prerequisite, not a
failed receiver process. Scheduled renewal must not consume its generation or
use the process fallback while the service is stopped. When the validated
service returns to `Running`, the shell starts one bounded recovery epoch. The
recovery latch may submit at most two same-process DNS-SD attempts for one
service-return event and must never become a recurring three-second command
loop. A correlated native ready acknowledgement is required before restoring
ready.

This policy treats low-frequency re-registration as inexpensive receiver
upkeep; it does not claim that Bonjour callbacks attest remote iPhone browse
state, invalidate an iOS cache, implement AWDL, or refresh the separate BLE
helper in place. Those require physical evidence or separate architecture.

## D-013 — Make installed updates and reinstalls unattended

**Status:** accepted

The explicit **Download and install** action is the application's update
confirmation. After the exact Setup asset is downloaded and digest-verified,
AeroMirror launches it directly instead of asking the same question again.
Setup must not ask again for Start menu, desktop, or post-install launch
choices. A manually opened newer Setup and a same-version reinstall follow the
same unattended path when an installed copy is detected. Setup preserves the
shortcut state already chosen by the user and relaunches AeroMirror after
successful replacement.

A clean first install remains interactive because no prior shortcut preference
exists. A newer installed version is excluded from replacement and an older
Setup aborts instead of offering or performing a downgrade. This choice changes
presentation only; it does not weaken download-digest checks, the
backup/rollback transaction, per-user identity, or settings persistence.

Setup 0.12.22 and later serialize mutation for one Windows user with a
SID-derived global mutex. Interactive UI does not hold it while idle; the
worker acquires it before mutation. Automatic and interactive routes re-read
the authoritative primary executable version under that mutex, and failure
recovery retains it through the bounded replacement-shell launch check. A
present but unreadable/invalid primary executable enters repair rather than
falling back to stale registry or legacy-executable metadata. Already-published
older Setup binaries cannot participate in this new mutex, so concurrently
running a pre-0.12.22 Setup with a current transaction remains unsupported.

## D-014 — Do not crop a Photos transport canvas without a trusted content rectangle

**Status:** accepted

An observed AirPlay geometry signature may identify a presentation canvas and
help choose the outer renderer-window orientation. It does not identify the
photo rectangle inside that canvas. AeroMirror therefore keeps presentation
scale neutral and contains the complete frame unless a future versioned native
contract supplies trustworthy content bounds. Letterboxing is preferable to
silently losing real image pixels.

The rejected local 0.12.23 experiment widened the outer viewer to 4:5 to make
the letterboxed photo larger. That compensation is not an accepted product
behavior: the PC window must not be reshaped to disguise a source-negotiation or
frame-geometry error. The experiment and its geometry tests were removed.

Fullscreen and the visible video window have one native owner. The GStreamer
surface is embedded into that viewer without a crop rectangle and keeps
aspect-ratio containment. Caption maximize, Escape, Alt+Enter, and the shell's
tray action all use one idempotent setter. Fullscreen is borderless and contains
no floating shell controls. The shell
must not create a second top-level overlay or keyboard hook and must use the
native acknowledged state instead of inferring the next toggle from delayed
window geometry.

Caption Close is a minimize-equivalent while a renderer generation is active.
It must not clear native requested visibility: doing so would let a repeated
codec-selection SHOW immediately undo the user's dismissal. A minimized HWND
remains discoverable by the shell even when the taskbar setting gives it
`WS_EX_TOOLWINDOW`; the explicit tray restore action uses
`ShowWindow(SW_RESTORE)` to return it. Only renderer stop/destroy clears
requested visibility and hides the host, so the next session can SHOW it once
without retaining stale state.

This Caption Close paragraph describes the immutable 0.12.20 through 0.12.22
implementation. D-018 records the accepted future behavior and the native
session boundary required before it can change safely.

## D-015 — Configure exact Bonjour resilience in Setup and keep automatic runtime monitoring read-only

**Status:** accepted; the narrow explicit-start exception is defined by D-022

Bonjour is shared machine-wide Apple software. The per-user application runtime
may assess its exact service, executable, status, recovery configuration, and
firewall rule. Its startup and automatic monitoring never elevate, start or
reconfigure the service, or edit the firewall. The main page and tray expose no
permanent Bonjour/discovery repair action. A blocking state may be shown with
diagnostic or reinstall guidance. Version 0.12.26 adds only the user-initiated,
contextual start operation in D-022; service policy and firewall ownership stay
here.

After the application install/update transaction commits, Setup may request
Windows administrator approval for one bounded best-effort configuration pass.
The elevated branch accepts only the exact Apple service identity and a
canonical `mDNSResponder.exe` beneath the protected Program Files Bonjour
directory. It rejects reparse points, untrusted ownership/write access, and a
NULL DACL. It uses direct Service Control Manager APIs to select Automatic
start, start the service when needed, and configure restart actions after 5,
30, and 120 seconds plus the non-crash failure flag. It does not run a shell
command, `sc.exe`, or the per-user AeroMirror executable as administrator.

The same branch uses Windows Firewall policy APIs to converge one enabled
inbound Allow rule for the exact executable: Private profile, UDP local port
5353, remote `LocalSubnet`, and no edge traversal. Public, TCP, arbitrary
address/port, and broad application rules are prohibited. Missing or unsafe
Bonjour, declined elevation, timeout, or helper failure leaves the successful
per-user installation intact and is reported as best-effort system status.

The exact recovery policy and firewall rule intentionally remain after a
normal AeroMirror uninstall. Removing shared machine state would require a new
administrator prompt during per-user removal and could disrupt another Bonjour
consumer. A later install/update is idempotent and revalidates the same narrow
state. Separate administrator maintenance may remove it if the Apple Bonjour
installation itself is retired.

The rule proves only a local Windows prerequisite. It cannot be described as
continuous iPhone visibility, successful DNS-SD browsing, BLE/AWDL support, or
physical interoperability without separate device evidence.

## D-016 — Use one-time per-device PIN trust

**Status:** accepted

The old user-selected fixed/no-PIN modes are retired. Every unknown iPhone
receives a fresh cryptographically generated four-digit session PIN, while an
already trusted device reconnects without another prompt. The receiver key and
trusted-client register remain per-user and survive in-place updates. Settings
provides one explicit action to revoke the complete trusted-device register.

The native boundary emits a structured, process/request-scoped pairing event.
The shell displays the PIN in a high-contrast fullscreen overlay on the active
display for at most one minute and delivers it only through redirected stdin to
that exact request. Escape cancels the request. The PIN must not enter process
arguments, settings, ordinary logs, AeroMirror diagnostic exports, or the
trusted-client file. The native core necessarily holds the value transiently
for SRP and must clear its request buffers after success, cancellation, timeout,
or failure. This does not claim that an external full process-memory dump could
never capture a live in-flight secret. Legacy fixed secrets and advanced
pairing/identity overrides are stripped during migration and cannot weaken this
contract.

Pairing cancellation is authoritative, not cosmetic. Timeout, Escape,
connection destruction, malformed SETUP, a stale request, or mismatch with the
signature-verified client key must make native admission fail. Machine-readable
`AEROMIRROR_*` output uses a dedicated emitter. Ordinary native and HLS output
must flatten control bytes and neutralize marker tokens before stdout, while the
shell accepts only exact anchored marker grammars. Client name, model, device
identifier, public key, and PIN are not ordinary log fields.

## D-017 — Keep automatic updates opt-in and apply them only at a later safe start

**Status:** accepted

Automatic updates default to off. Enabling the setting authorizes AeroMirror to
check the fixed public GitHub repository, download only the exact installer for
an exact newer three-part release, enforce HTTPS redirect and size limits,
verify SHA-256, and stage the result for the current Windows user. Finding or
staging a release must not stop or restart an active mirroring session or the
current receiver. Download and verified staging may finish in the background;
only installation is deferred to a later application start.

The staged manifest is protected with Windows DPAPI and includes the exact
version, installer name, digest, timestamp, and bounded launch-attempt state.
Only a later safe AeroMirror start, before receiver/UI startup, may revalidate
and launch the existing unattended Setup transaction. Invalid, expired, stale,
or exhausted staging fails open to normal receiver startup. Disabling the
setting removes known staged update files. Manual **Check for updates** remains
available and keeps its explicit download/install confirmation.

The local 0.12.29 follow-up bounds metadata to 1 MiB and one 30-second transfer
deadline, rather than relying on default or individual-read timeouts. Real
owner shutdown cancels network work, not only its eventual UI callback.
Disabling automatic updates cancels that worker while leaving manual updates
independent; later re-enabling gets an uncanceled operation. Hiding the settings
form to tray is not owner shutdown. Canceled files cannot be handed to Setup.
The receiver, fixed channel, digest validation and later-start install policy
are unchanged. Metadata uses the confirmed permanent repository ID
`1324108899`, avoiding the legacy slug's redirect. Exact Setup paths from the
canonical `pyram1da/aeromirror` and historical `Nadejny/aeromirror` names are
allowlisted with the same mandatory digest check. A future unreviewed owner
name is not automatically trusted. The repository-root configuration marker
stays `Nadejny/aeromirror` for compatibility.

## D-018 — Make renderer Caption Close end only the active mirroring session

**Status:** accepted; local 0.12.30 implementation, physical acceptance pending

Caption Close should mean that the user is finished with the current
transmission. The native command must close only the current AirPlay/RAOP
session generation, drain its renderer/audio/RTP workers, and return a
request-correlated result while leaving the receiver process, HTTP listener,
advertised ports, DNS-SD registration, and BLE helper ready for the next
connection. The iPhone must physically leave Screen Mirroring rather than see a
window that merely minimized on the PC.

Killing and restarting the whole core is not an acceptable implementation: it
would churn advertised ports and discovery, create false loss/restart UI, and
could race a newer connection. Until the session-only command and physical iOS
evidence exist, published 0.12.22 retains its minimize-equivalent behavior and
an explicit tray restore path.

Local .30 binds a new stream ID only for a successful mirror SETUP, carries
it with native SHOW, and queues an exact-ID close to the HTTP owner. Completion
follows connection destruction and media reset; the shell rejects an old
session's dismissal. Fifty loopback close/reconnect cycles and same-socket
replacement checks pass. They do not establish iOS UI behavior or complete the
physical acceptance gate.

The lost-connection warning's user Close has the same session-only meaning.
Its process/managed/native identity is captured when that warning is prepared;
it never retargets a newer session at click time. If the old process has already
ended, the warning stays dismissible without starting a replacement process.
Programmatic warning closure remains presentation-only.

## D-019 — Isolate display negotiation before changing gallery presentation

**Status:** accepted for the 0.12.24 diagnostic candidate

When Photos switches from a portrait device frame to a landscape presentation
canvas, AeroMirror must test the AirPlay negotiation boundary before adding a
window, crop, zoom, or pixel-analysis workaround. The local 0.12.24 candidate
therefore changes one variable for the `4k60` preset: it requests
`998x2160@60` instead of `3840x2160@60`. H.265, frame rate, model, feature bits,
rotation policy, renderer, sink scale, and outer-window behavior remain fixed.
The device-specific size is a causal probe, not an accepted public default or a
claim that Photos is fixed.

Diagnostics for this experiment may log only the receiver-configured `/info`
display tuple advertised to the sender, the existing sender-side geometry
generation, and an independent sink-local timeline. That sink timeline records
every actual CAPS event with `caps_seq`, one first-buffer caps snapshot only
when no event was observed, and read-only crop metadata on the first buffer
after CAPS, on metadata changes, and every 120 buffers. Absence of CAPS is
evidence, but no one-to-one mapping to a sender generation may be inferred.
Diagnostics must not record plist bodies, client identifiers, mirrored pixels,
or sampled content, and the probe must not mutate caps, crop, scale, render
rectangles, or viewer geometry. Only repeatable physical iPhone evidence can
determine the next product change.

## D-020 — Redraw a newly shown embedded video surface through GstVideoOverlay

**Status:** physically insufficient in 0.12.25; retained as a guarded fallback and superseded by D-023

A fresh normal viewer may not use fullscreen, a synthetic resize, crop, zoom,
or periodic repaint loop to repair a black embedded D3D11 surface. The retained
physical log already proves transport, decode, sink buffers, and the D3D11
pre-Present callback were advancing while the window remained visibly black;
fullscreen repaired only the downstream HWND/swap-chain exposure boundary.

Qt remains the only owner allowed to assess its actual child HWND, while the
native renderer lifecycle owns the generation token. After Qt processes SHOW,
a GUI-owner callback must verify the exact generation, inherited visibility,
and nonzero client area, then acknowledge READY to libuxplay. The other
required condition is the first Present callback from the selected D3D11 sink,
because an earlier expose can precede swap-chain creation and be a no-op. That
callback may publish atomic state and use `PostMessage`, but it must not call
expose or wait on Qt while holding the D3D11 device lock. Its host Present proof
must remain installed when the independent failure-recovery pad probe is
attached.

A later Qt event-loop turn must revalidate the same generation. Libuxplay then
retains only the selected host sink under `renderer_lock`, releases the lock
before calling `gst_video_overlay_expose()`, and releases the sink reference
afterward. Show and WindowStateChange may coalesce a re-expose only for an
already acknowledged surface; this is event-driven recovery, not a periodic
repaint loop. WinIdChange uses bounded handle retry, suppresses SHOW while all
current overlay sinks are rebound, and starts a fresh native generation only
after rebind finishes. HIDE invalidates older READY, Present, and expose work.
GUI-callable surface APIs must not write through a logger that may already have
been destroyed during shutdown.

The resulting markers may report generation, client size, selected codec, and
whether expose was requested. They may not claim successful pixel presentation.
Only repeated untouched physical fresh starts accept the fix. If they still
open black, the next isolated experiment is host-visible-before-PLAYING with
selected-codec-only binding; presentation-geometry changes remain out of scope.

## D-021 — Raise once over ordinary windows but defer to fullscreen foreground content

**Status:** accepted for the 0.12.25 local review candidate

The renderer may raise its normal window once when a new mirror connection
starts, but it must not take keyboard focus or become always-on-top. Before
SHOW, Qt samples the current foreground HWND. If an external visible window's
client bounds cover its monitor, AeroMirror treats it as fullscreen, shows
without activation, and places the viewer behind that window. Otherwise the
viewer is raised within the ordinary non-topmost window band, still without
activation.

This rule intentionally uses fullscreen monitor coverage rather than process
names or a game database: it also avoids interrupting fullscreen video and does
not collect titles or executable names. An initial automatic AeroMirror
fullscreen request is deferred while external fullscreen content owns the
monitor. A later user-selected fullscreen command remains separate and
authoritative.

The 0.12.27 follow-up permits a bounded synchronous TOPMOST/NOTOPMOST
transaction only when an ordinary nonactivating raise succeeded but a visible
ordinary window is still above the viewer. Fullscreen foreground protection
is checked again immediately before promotion. Demotion follows immediately,
including after failed promotion; no persistent topmost, activation request,
or retry timer is allowed. Actual ordinary-window order is checked before
reporting success. A failed demotion hides the viewer rather than leaving it
above a game. The reported mixed-window arrangement remains a physical gate.

## D-022 — Offer one explicit protected Bonjour start only in the stopped-state card

**Status:** accepted for the 0.12.26 local review candidate

Windows service recovery is intentionally finite. Retained September 3 Windows
events show Apple Bonjour crashing four times and exhausting its configured
5/30/120-second restart sequence; the September 4 receiver log still reports
`Stopped`, and Windows reports service exit code 1067.
Restarting UxPlay cannot repair that machine-wide prerequisite. AeroMirror may
therefore show **Start Bonjour** only inside its existing network error card
when the exact validated Apple service is stopped. It remains absent from the
main healthy state and tray.

The action runs only after a user click and may produce at most one Windows
administrator confirmation. The runtime must first validate the canonical
Apple service identity, the service object's trusted owner/configuration DACL,
and every component of the protected `mDNSResponder.exe` path. It must likewise
validate the complete path chain to `%SystemRoot%\System32\sc.exe`, then invoke
only that executable with `runas` and the exact allowlisted service name
`Bonjour Service` or `mDNSResponder`. The command remains unreachable
unless the fresh service state is exactly `Stopped`; `Running`, `StartPending`,
`ContinuePending`, `StopPending`, `PausePending`, `Paused`, unknown, missing,
and unsafe states fail closed. No other executable, subcommand, service name, or
argument is accepted. Startup, timers, receiver restart, and canceled or failed
actions must never prompt or retry automatically. A bounded timeout does not
release the single-flight latch while that exact elevated process is still live;
only confirmed process exit permits another explicit request in the same
AeroMirror process lifetime. The latch is not persisted across application
restarts.

Command completion is diagnostic rather than authoritative: Windows recovery
may win the race and make `sc start` return nonzero. AeroMirror revalidates the
exact identity and requires the actual `Running` state before reporting
success. It then accelerates the existing same-process DNS-SD recovery without
resetting that epoch's two-request budget, changing the core PID or ports,
editing service configuration, or touching the firewall. Unsafe or missing
Bonjour still requires trusted installation or Setup; this exception does not
weaken D-015's configuration boundary.

## D-023 — Bind the selected host sink before mirror playback

**Status:** accepted for the 0.12.26 local review candidate; physical acceptance pending

The 0.12.25 post-Present expose rendezvous did not fix the untouched normal
viewer on the test computer: fullscreen still caused the first visible frame.
That result rejects redraw timing as the sole cause. A fresh mirror session must
instead make the application-owned child surface valid before the selected
D3D11 pipeline can preroll or present.

Fresh mirror codec pipelines start in `READY`. The media callback claims an
immutable renderer generation, selects exactly one H.264 or H.265 renderer,
requests SHOW, waits for the bounded Qt-owned acknowledgement of the real child
HWND, binds the selected `GstVideoOverlay` sink, commits the binding for that
exact generation, and only then changes the selected pipeline to `PLAYING`. The
other codec pipelines are quiesced after selection.

If the real child HWND changes, Qt supplies and registers the replacement, and
the selected pipeline must move through `NULL` before that handle is applied to
the sink. It then returns to `READY`, receives a fresh SHOW/READY
acknowledgement, rebinds and commits the selected sink for the still-current
generation and HWND, and only then resumes
`PLAYING`. The post-Present expose path from D-020 remains a redraw aid, but it
can execute only when the lifecycle, READY, bound, and Present generations all
match the current session; it no longer owns first-surface creation. No resize,
fullscreen, crop, scale, pixel inspection, or render rectangle participates in
recovery.

Start, stop, destroy, media callbacks, and bus callbacks share an explicit
session boundary. Start, stop, and destroy advance the generation under the
renderer state owner; media callbacks carry their claimed generation in
thread-local state; bus watches retain an immutable generation context. Stale
callbacks can finish only against retained old objects and cannot publish a
renderer, change new pipeline state, or push into the next session. Stop clears
published state and flushes the selected bus before taking its pipeline to
`NULL`; destroy waits for bus and operation references before freeing renderer
objects. Automated contracts and reproducible builds validate those invariants,
but only repeated untouched iPhone starts can accept visible first-frame
behavior.

## D-024 — Exclude the external video HWND from Qt raster painting

**Status:** implemented for local 0.12.27; insufficient alone to correct black video

The 0.12.26 selected-sink-before-PLAYING lifecycle still produced a black
normal viewer on the user's PC. Qt 6.10.1 source explicitly requires a QWidget
paintEngine override returning null when DirectX owns a native child surface;
setting PaintOnScreen on a base QWidget is ignored on Windows. The previous
video child also auto-filled a black background.

Use a dedicated widget with a null paint engine, PaintOnScreen,
NoSystemBackground, OpaquePaintEvent and auto-fill disabled. Qt retains HWND,
layout and event ownership, while GstVideoOverlay alone owns the pixels.
Keep the current binding/lifetime safeguards and gallery negotiation. Do not
substitute a resize, fullscreen transition, crop, scaling or periodic repaint.

The hidden real-Windows Qt harness checks the production widget's contract,
not decoded video or physical visibility. Only the untouched iPhone fresh-start
test can accept the black-screen correction.

Source: [Qt 6.10.1 QWidget implementation](https://raw.githubusercontent.com/qt/qtbase/v6.10.1/src/widgets/kernel/qwidget.cpp),
`setAttribute(WA_PaintOnScreen)` and `QWidget::paintEngine()`.

## D-025 — Desktop placement belongs only to the top-level renderer

**Status:** implemented in local 0.12.28; user reports the black-screen correction works; full physical matrix pending

The shell must not infer placement ownership from a renderer title or PID alone.
Windows SHOW events include the GStreamer child named `Direct3D11 renderer`.
Applying saved desktop coordinates to that child uses the wrong coordinate
system and can place all decoded output outside the Qt parent's clipping area.
The September 6 live hierarchy and an executable test on installed .27
confirmed this mechanism.

Validate the root ancestor and absence of WS_CHILD at selection and every
desktop-placement/aspect-fit mutation boundary. Keep Qt/GStreamer child layout
with its native owners. An owned top-level window is still eligible. Never
repair this defect by resizing/fullscreening the outer window or by periodically
repositioning the video child. Existing native/GStreamer ownership remains intact.

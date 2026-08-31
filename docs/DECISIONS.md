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

## D-015 — Configure exact Bonjour resilience in Setup and keep runtime read-only

**Status:** accepted

Bonjour is shared machine-wide Apple software. The per-user application runtime
may assess its exact service, executable, status, recovery configuration, and
firewall rule, but it never elevates, starts or reconfigures the service, or
edits the firewall. The main page and tray expose no Bonjour/discovery repair
action. A blocking state may be shown with diagnostic or reinstall guidance.

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

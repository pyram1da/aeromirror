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

## D-002 — Derive trust from the physical Windows network

**Status:** accepted

A Windows Private physical Wi-Fi/Ethernet network may receive without a PIN,
while PIN protection remains optional. A Public or unknown physical network
fails closed without a PIN. VPN, tunnel, Hyper-V, and other virtual adapters
do not override the physical network category.

This is conservative by design: a wrongly classified network should block an
unprotected receiver rather than expose it.

## D-003 — Use physical IPv4 for receiver startup and discovery

**Status:** accepted

The core waits for a preferred, non-APIPA, non-SkipAsSource IPv4 on the active
physical adapter. DNS-SD remains the primary LAN discovery path; the optional
BLE beacon advertises the same physical address instead of selecting a route
through a VPN.

Readiness requires listening sockets and at least one viable discovery signal.
Explicit failure of both DNS-SD and BLE must not produce a false ready state.

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
next same-process attempt. Manual **Restart discovery** and a physical IPv4
change remain explicit full DNS-SD-and-BLE restarts.

This policy treats low-frequency re-registration as inexpensive receiver
upkeep; it does not claim that Bonjour callbacks attest remote iPhone browse
state, invalidate an iOS cache, implement AWDL, or refresh the separate BLE
helper in place. Those require physical evidence or separate architecture.

## D-013 — Make installed updates and reinstalls unattended

**Status:** accepted

The application asks for update confirmation before launching the verified
Setup asset. After that confirmation, Setup must not ask again for Start menu,
desktop, or post-install launch choices. A manually opened newer Setup and a
same-version reinstall follow the same unattended path when an installed copy
is detected. Setup preserves the shortcut state already chosen by the user and
relaunches AeroMirror after successful replacement.

A clean first install remains interactive because no prior shortcut preference
exists. A newer installed version is excluded from the unattended path so a
downgrade continues to require an explicit warning and confirmation. This
choice changes presentation only; it does not weaken download-digest checks,
the backup/rollback transaction, per-user identity, or settings persistence.

## D-014 — Do not crop a Photos transport canvas without a trusted content rectangle

**Status:** accepted

An observed AirPlay geometry signature may identify a presentation canvas and
help choose the outer renderer-window orientation. It does not identify the
photo rectangle inside that canvas. AeroMirror therefore keeps presentation
scale neutral and contains the complete frame unless a future versioned native
contract supplies trustworthy content bounds. Letterboxing is preferable to
silently losing real image pixels.

Fullscreen remains a native renderer operation. The shell may add a
non-activating visible control and an event-driven foreground Escape path, but
it must not subclass the foreign window, consume the key, or infer fullscreen
solely from a managed toggle flag.

## D-015 — Keep Bonjour firewall repair exact, explicit, and separate from service ownership

**Status:** accepted

Bonjour is an external machine-wide service. AeroMirror may diagnose whether
its exact `mDNSResponder.exe` lacks a narrowly scoped inbound Windows Firewall
rule, but ordinary startup must remain read-only. Repair requires explicit
user confirmation and Windows administrator approval and is limited to the
exact executable, Private profile, UDP local port 5353, remote `LocalSubnet`,
and no edge traversal. Public, TCP, arbitrary-address, broad application, and
automatic Bonjour-service changes are prohibited.

Version 0.12.19 does not wire removal of this external rule into uninstall and
must not promise automatic cleanup.

The rule proves only a local Windows prerequisite. It cannot be described as
continuous iPhone visibility, successful DNS-SD browsing, BLE/AWDL support, or
physical interoperability without separate device evidence.

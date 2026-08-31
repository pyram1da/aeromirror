# AeroMirror 0.12.22 — automatic discovery recovery and first-device trust

## Summary

0.12.22 removes manual Bonjour repair from normal use, adds a large one-time
pairing code for each new iPhone, keeps fullscreen inside the native viewer,
and adds optional verified updates that apply only at a later safe start.

This is a public review release. Clean native reproducibility, the extracted
no-Git source rebuild, runtime, exact package, and Setup gates are complete;
exact-tag and public-asset verification remain publication steps. The installed
Windows/iPhone matrix remains pending until its results are recorded
in the [test plan](https://github.com/Nadejny/aeromirror/blob/v0.12.22/docs/releases/0.12.22/TEST_PLAN.md).

## Should I update?

- Yes, if AeroMirror disappeared after Windows startup or an update because the
  Apple Bonjour service had stopped.
- Yes, if the old fixed/no-PIN settings or the separate fullscreen control were
  confusing or unreliable.
- Yes, if you want to opt into verified background update staging without
  interrupting the current receiver.
- Optional if 0.12.20 is working reliably and you prefer to wait for the
  remaining physical Windows/iPhone review evidence.

## What changed

- Changed: the main page and tray no longer expose Bonjour, firewall, or
  discovery-restart controls. AeroMirror remains a background receiver and the
  running application only observes discovery health.
- Added: after the per-user application transaction commits, Setup can request
  administrator approval for one best-effort, exact Apple Bonjour
  configuration. It validates the service and protected Program Files binary,
  selects Automatic start, configures bounded Windows recovery after 5, 30, and
  120 seconds, starts the service when needed, and ensures one inbound Allow
  rule limited to Private, UDP 5353, `LocalSubnet`, with edge traversal off.
- Hardened: unsafe service identity, path, ownership, ACL, reparse, or firewall
  state is rejected. Declining or failing this system step does not roll back
  the installed application. The per-user AeroMirror executable is never run
  as administrator.
- Fixed: DNS-SD error `-65563` now marks Apple Bonjour as unavailable instead
  of driving native retry or receiver-process churn. BLE cannot create a false
  ready state. After Windows returns the validated service to `Running`,
  AeroMirror performs one bounded same-process/same-port publication recovery
  and waits for an acknowledged DNS-SD ready result.
- Added: every previously unknown iPhone receives a fresh cryptographic
  four-digit PIN in a high-contrast fullscreen overlay on the active PC display.
  The request expires after one minute and Escape cancels it. A successfully
  trusted device reconnects without another prompt until trust is reset in
  Settings.
- Hardened: the session PIN is sent only to the exact current native request
  through redirected stdin. It is not stored in Settings, added to the process
  command line, or intentionally written to logs or diagnostic exports. Legacy
  fixed PIN/password and pairing-path overrides are removed during migration;
  transient native request buffers are cleared after the SRP request settles.
- Hardened: timeout, Escape, disconnect, malformed setup, a stale request, or a
  verified client-key mismatch now rejects native SETUP admission. Pairing can
  no longer continue after the matching on-screen request has been cancelled.
- Hardened: cancellation and Settings trust reset first record a durable pending
  state. If native exit cannot be confirmed, replacement startup remains
  blocked; after confirmed exit, AeroMirror empties the trust store before the
  marker is removed or another core can start.
- Hardened: genuine `AEROMIRROR_*` control lines now use a dedicated native
  output path. Ordinary client and HLS metadata is flattened and neutralized,
  raw client identifiers are omitted from logs, and the shell accepts only
  exact control-line formats.
- Fixed: inherited HLS/gallery parsing now accepts valid mixed-case HTTP header
  names, bounds language metadata, master-URI lines, and condensed chunks,
  preserves zero-match playlists, caps adjusted output at 32 MiB, and rejects
  malformed fields or allocation failures without bringing down the receiver.
- Fixed: caption maximize and Alt+Enter enter the renderer's clean borderless
  fullscreen; Escape exits and restores the remembered framed geometry. The
  delayed shell-owned floating button and keyboard hook remain removed.
- Added: automatic updates are optional and disabled by default. When enabled,
  AeroMirror uses only the fixed public repository and exact versioned Setup,
  validates bounded HTTPS redirects and file size, verifies SHA-256, and
  protects staged metadata for the current Windows user. Background download
  and staging do not stop or restart an active session; Setup may run only at a
  later safe AeroMirror start.
- Hardened: 0.12.22-and-newer Setup transactions are serialized for the current
  Windows user. The installed primary executable is re-read under the same lock,
  stale registry or legacy metadata cannot force a downgrade, and recovery does
  not launch from an application tree another current Setup is replacing.
- Changed: the README now leads with a direct download path and concise FAQ for
  local networking, Bonjour, first-device trust, privacy, supported Windows
  versions, fullscreen, updates, remote control, and AirDrop.

## Known limitations

- Physical first/second-device pairing, trust persistence/revocation, stopped-
  Bonjour recovery, long-idle iPhone visibility, fullscreen keys, update
  handoff, and the Windows 10/11 matrix remain pending until recorded in the
  versioned test plan. Automated checks alone do not prove iPhone visibility.
- Apple Bonjour remains a separate machine-wide prerequisite. A missing,
  replaced, or unsafe installation is reported and left untouched rather than
  replaced with AeroMirror's bundled per-user responder.
- The exact Bonjour recovery policy and narrow firewall rule intentionally
  remain after normal AeroMirror uninstall because they belong to shared Apple
  system software. Removing Apple Bonjour itself is separate administrator
  maintenance.
- A locally running service, exact firewall rule, and DNS-SD ready callback do
  not prove that an iPhone currently received the multicast advertisement.
- Portrait Photos presentation still uses a conservative non-cropping boundary.
  Without trustworthy content bounds from AirPlay, AeroMirror may show
  letterboxing rather than discard real image pixels.
- The Setup executable is not Authenticode-signed, so Windows SmartScreen may
  show an unknown-publisher warning.
- Do not deliberately run a pre-0.12.22 Setup at the same time as a current
  Setup. Published historical installers are immutable and cannot participate
  in the transaction guard introduced by this release.
- This release does not add iPhone remote control, AirDrop/AWDL file transfer,
  ARM64/32-bit Windows support, or a portable public package.

## Durable decisions

The design boundaries are recorded in
[D-002, D-003, and D-011 through D-017](https://github.com/Nadejny/aeromirror/blob/v0.12.22/docs/DECISIONS.md).

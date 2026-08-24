# AeroMirror 0.12.20 — native fullscreen ownership and quieter setup

## Summary

0.12.20 replaces the delayed floating fullscreen control with one native
Windows viewer, makes Escape deterministic, removes duplicate application
confirmations, and keeps Photos presentation explicitly non-cropping.

## Should I update?

- Yes, if the 0.12.19 fullscreen button lagged behind the window, disappeared,
  or Escape did not restore the normal frame.
- Yes, if the verified update flow or Bonjour repair felt like the application
  asked the same question twice.
- This is an updater-visible review release. The physical Windows/iPhone matrix
  in `TEST_PLAN.md` remains pending and is not claimed as accepted.

## What changed

- Fixed: the video surface and its Windows frame now have one native owner.
  The standard caption fullscreen action no longer follows the viewer as a
  delayed second window.
- Fixed: caption fullscreen, Escape, Alt+Enter, and the tray action all reach
  one idempotent setter. The native acknowledgement includes requested and
  actual state, result, generation, and input source.
- Changed: Caption Close minimizes the active viewer instead of stopping the
  stream. It remains recoverable through its taskbar entry or the explicit
  **Show stream window** tray action; the latter also works when the optional
  taskbar entry is disabled.
- Fixed: entering fullscreen directly from that minimized state preserves the
  latest normal window position, and a delayed fullscreen request after session
  stop cannot reopen an empty black viewer.
- Fixed: the viewer explicitly enables aspect-ratio containment when the sink
  supports it and never requests a crop/render rectangle. The exact Photos
  canvas remains at neutral 100% scale inside the portrait outer window.
- Changed: selecting **Download and install** is the one application-level
  update confirmation. Existing unsaved settings are resolved before download
  and update-page navigation is locked during handoff. After exact-name and
  SHA-256 verification, Setup starts directly and retains the existing
  unattended update/reinstall behavior.
- Changed: a missing narrow Bonjour firewall rule is shown on the main network
  card. Clicking the action goes directly to one Windows UAC prompt; success
  refreshes discovery without a success dialog.
- Hardened: an absent Bonjour installation is reported without native English
  dialogs and without registering a bundled per-user executable as a
  machine-wide service.
- Hardened: Bonjour/firewall status expires after two minutes and is reassessed
  on receiver start, restart, and manual discovery refresh. The card now says
  that Bonjour is unavailable or incorrectly installed instead of assuming it
  is definitely absent.
- Fixed: native runtime paths containing Cyrillic characters no longer pass
  through a lossy byte conversion. The Windows-wide environment path is used
  for GStreamer, its scanner, GIO, fonts, and PATH. The staged runtime passes a
  full self-test; Setup verifies loader compatibility with its pinned upstream
  runtime before committing installation.

## Known limitations

- AirPlay still does not expose a trustworthy rectangle for a portrait photo
  inside the observed `3840x2160` Photos transport canvas. Preserving both the
  portrait outer window and every source pixel can require letterboxing; this
  build does not guess from dark pixels or enlarge by cropping.
- A clean first install remains interactive because no prior shortcut choices
  exist. Windows UAC and unsigned-publisher/SmartScreen prompts are operating-
  system trust boundaries and are not suppressed by this UX change.
- Local Bonjour readiness or a firewall rule does not prove that an iPhone can
  currently see the receiver. Physical idle, unlock, sleep, and reconnect
  checks remain separate.
- Caption Close/minimize and recovery with both taskbar policies still require
  physical Windows acceptance; automated state contracts do not prove focus or
  taskbar behavior.

The durable ownership and confirmation boundaries are recorded in D-013,
D-014, and D-015 in [the architecture decisions](../../DECISIONS.md).

## Verification boundary

Managed, native, corresponding-source, package, and non-installing Setup
checks pass locally. They cannot prove window behavior or iPhone visibility.
See `TEST_PLAN.md` for the physical rows that remain pending.

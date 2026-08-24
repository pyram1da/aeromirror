# AeroMirror 0.12.19 — non-cropping gallery and accessible fullscreen

## Summary

0.12.19 is a review release that keeps the complete Photos frame visible,
adds a visible fullscreen control to the renderer window, makes Escape handling
event-driven, and diagnoses a Windows Bonjour firewall condition that can hide
an otherwise healthy receiver on a Private network.

## Should I update?

- Yes, if Photos was cropped, Escape did not leave fullscreen, or fullscreen
  was available only through the tray.
- Yes, if AeroMirror diagnostics specifically reports that the exact narrow
  Private Bonjour firewall rule is missing. This is a prerequisite repair, not
  a promise that every intermittent iPhone-visibility case has the same cause.
- This remains a review build until the physical Windows/iPhone matrix below
  is completed.

## What changed

- Fixed: the exact `3840x2160` Photos transport signature no longer authorizes
  a 3.85x cover transform. The complete frame is contained without discarding
  pixels; the outer window can still follow the trusted portrait phone shape.
- Added: a compact shell-owned, no-activate fullscreen control follows the
  native renderer without subclassing or injecting code. It is available in
  both the normal framed state and actual fullscreen, and is hidden when the
  renderer is missing, invisible, minimized, or an unrelated app owns normal
  foreground focus.
- Fixed: the bounded keyboard hook is installed only while the real renderer
  is fullscreen; Escape capture additionally requires its PID/root to own
  foreground, and the key is never swallowed. Native Alt+Enter and the tray
  action remain available.
- Fixed: if a real fullscreen-to-nonfullscreen transition leaves the renderer
  borderless, the visible control stays in exit mode so the user can finish the
  transition. The shell never sends a speculative automatic second toggle,
  because a toggle is not idempotent and could re-enter fullscreen.
- Added: diagnostics distinguish a running local Bonjour registration from a
  missing Private-network inbound mDNS rule. Repair is explicit, UAC-gated,
  limited to UDP 5353 from the local subnet for the exact Bonjour executable,
  and never opens Public/TCP/Any traffic.
- Changed: renderer geometry constants now live in one typed policy class as
  product invariants instead of being duplicated across partial files.
- Changed: the updater relies on its exact versioned Setup asset contract and
  removes a redundant filename architecture filter. Receiver readiness is now
  state-backed instead of inferred from Russian display text.
- Cleaned: unused imports and one unreachable network-start branch were
  removed without changing persisted settings, protocol behavior, or the
  single-shell/single-tray architecture.

The non-cropping and explicit-firewall boundaries are recorded as D-014 and
D-015 in [the architecture decisions](../../DECISIONS.md).

## Known limitations

- AirPlay currently exposes the observed Photos transport canvas but not a
  trustworthy rectangle for the photo inside it. 0.12.19 therefore favors a
  complete, possibly letterboxed image over speculative cropping.
- The fullscreen button belongs to the managed shell and follows a foreign
  GStreamer window. Physical DPI, multi-monitor, minimize/restore, and
  topmost-state checks remain required.
- A firewall repair requires administrator approval and is offered only after
  the exact missing-rule condition is detected. AeroMirror does not modify the
  Bonjour service or broad Windows Firewall policy. Version 0.12.19 does not
  connect removal of that external rule to uninstall.

## Verification boundary

Automated build, policy, lifecycle, packaging, and installer checks do not
prove visible iPhone behavior. See `TEST_PLAN.md` for the physical rows that
remain pending at publication time.

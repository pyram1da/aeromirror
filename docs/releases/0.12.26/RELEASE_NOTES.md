# AeroMirror 0.12.26 — deterministic first surface and explicit Bonjour recovery

Superseded local candidate: the September 4 physical run still opened black
and failed the reported mixed-window placement. Bonjour physical recovery is
still pending. Do not publish this version as a verified black-screen fix.

## Summary

0.12.26 is a local review candidate for two observed failures: a new normal
viewer could remain black until fullscreen changed its surface, and a crashed
Apple Bonjour service could remain stopped after Windows exhausted its automatic
restart actions with no practical recovery inside AeroMirror.

The video path now completes READY, real-HWND SHOW/READY, selected-sink
bind/commit, and PLAYING in one generation-safe native session. Real child-HWND
replacement uses a serialized NULL -> new HWND -> READY -> SHOW/READY ->
bind/commit -> PLAYING transition. A stopped, safely validated Bonjour service
now exposes one contextual **Запустить Bonjour** action; it is never invoked
automatically.

This candidate is not published. No `v0.12.26` tag or GitHub Release exists;
public 0.12.22 remains the updater-visible latest release.

## Should I update?

Not yet as a public update. The local Setup is intended for the reported PC to
verify that a fresh normal H.265/H.264 connection is immediately visible and
that explicit stopped-Bonjour recovery returns the receiver to the iPhone list.
Installed users should remain on public 0.12.22 until those physical checks pass
and publication is separately authorized.

## What changed

- Fixed: fresh mirror pipelines wait in READY while Qt shows and validates the
  real nonzero child HWND. Libuxplay then binds and commits only the selected
  codec sink for that exact generation, and only afterward enters PLAYING.
- Fixed: after Qt supplies a replacement real child HWND, the selected pipeline
  moves to NULL before the sink accepts that handle, returns to READY, obtains a
  fresh SHOW/READY acknowledgement, rebinds and commits the selected sink, and
  only then resumes PLAYING.
- Hardened: each native video session owns a generation and bounded operation
  references. Start, render, bus, pause/resume, HLS, stop, and destroy paths
  reject stale work; teardown invalidates the old generation before a
  replacement renderer can be published. Post-Present expose is accepted only
  when the current lifecycle, READY, bound, and Present generations all match.
- Preserved: the viewer is not resized or fullscreened as a workaround. The
  change does not add crop, scale, render rectangles, pixel inspection, a
  decoder/sink-policy change, or a periodic redraw loop. The accepted portrait
  gallery negotiation remains unchanged.
- Added: the main network-status card shows **Запустить Bonjour** only while the
  exact Apple Bonjour service has passed identity/path validation and is
  `Stopped`. One click starts at most one bounded system operation and may cause
  at most one Windows administrator prompt.
- Hardened: AeroMirror never raises the Bonjour UAC prompt from startup, a
  timer, or monitoring. Concurrent clicks are coalesced. Cancellation, timeout,
  identity change, or start failure leaves the prerequisite visible. The service
  object's owner/configuration ACL and the full protected Apple and Windows
  `sc.exe` path chains are validated first. A timed-out elevated process retains
  the one-flight latch until its exit is confirmed, preventing overlap within
  that AeroMirror process lifetime.
- Fixed: after the validated service reaches `Running`, AeroMirror continues
  the existing bounded DNS-SD recovery in the same receiver process and on the
  same listener ports. It still requires a correlated DNS-SD ready
  acknowledgement; BLE alone cannot report ready.

## Evidence status

- The physical 0.12.25 result is a retained failure: the untouched normal
  viewer was black, fullscreen made the current stream visible, and normal view
  then remained visible. This justifies the stricter lifecycle order without
  changing presentation geometry.
- The frozen local source passes the managed build and all focused managed and
  native suites. Two independent clean builds and the extracted no-Git source
  rebuild reproduce core SHA-256
  `FAA8A1575EAC7C26BA41DF09A81EB08E03DE05A621FA3C504289EA8E98DAB84A`.
  The isolated runtime, exact review payload, x64 Setup, embedded-input
  equality, and all four non-installing Setup checks pass.
- Fresh normal-viewer and stopped-Bonjour Windows/iPhone results remain pending.
  Logs and source checks cannot prove visible pixels or remote iPhone browse
  visibility.

## Known limitations

- Apple Bonjour remains separate shared machine software. AeroMirror starts
  only an exact safely validated existing service; it does not install or
  replace Bonjour from this action.
- Starting a service and receiving a local DNS-SD ready callback do not prove
  that an iPhone has received the multicast advertisement. The physical test
  must confirm the receiver appears and accepts a connection.
- Windows UAC is unavoidable for the explicit service-start action because the
  normal AeroMirror process stays unelevated. Cancelling it intentionally leaves
  the service stopped and does not trigger another prompt.
- Physical proof of the fresh-viewer correction, minimize/restore, foreground
  placement, Windows 10/11, and both H.264/H.265 paths is still pending.
- Caption Close still has its separately documented behavior; this patch does
  not add a native session-disconnect command.

## Publication boundary

Do not push, tag, publish, replace public assets, or make this candidate
updater-visible until the exact local Setup passes its automated and physical
acceptance plan and the user explicitly authorizes publication.

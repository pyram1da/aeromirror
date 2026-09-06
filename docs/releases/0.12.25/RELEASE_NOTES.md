# AeroMirror 0.12.25 — video-surface lifecycle candidate

## Summary

0.12.25 is a local review candidate for the viewer that could remain black on
a fresh connection even though decoding and Direct3D 11 presentation callbacks
were already advancing. Entering fullscreen made the same stream visible and
the image then remained visible after returning to the normal window.

The retained 0.12.24 physical log locates this defect after AirPlay transport,
codec selection, decoded sink buffers, and the D3D11 pre-Present callback. The
remaining boundary is the application-owned child HWND and its embedded
GStreamer video surface.

This candidate is not published. No `v0.12.25` tag or GitHub Release exists;
public 0.12.22 remains the updater-visible latest release.

## Should I update?

Not yet. This local candidate is intended for the reported PC to verify fresh
H.265/H.264 starts, restore behavior, and connection window placement.
Installed users remain on public 0.12.22 until physical acceptance and explicit
publication approval.

## What changed

- The native renderer lifecycle now owns each SHOW generation. Qt acknowledges
  READY only after it validates the actual child HWND, inherited visibility,
  and nonzero client area.
- The first Present from the selected D3D11 sink posts that same generation
  without calling expose under its device lock. A later Qt turn rejects stale
  work, retains only the selected host sink, releases the renderer lock, calls
  the standard `gst_video_overlay_expose()` redraw operation, and releases the
  reference.
- Show and WindowStateChange may coalesce a re-expose only for an already
  acknowledged surface. WinIdChange retries for a bounded interval, rebinds all
  current overlay sinks, and keeps SHOW deferred until rebind completes. HIDE
  makes older READY, Present, expose, and rebind work inert.
- The host Present proof remains active when the independent failure-recovery
  pad probe is installed. GUI-callable surface APIs do not write through a
  receiver logger that may already be destroyed during shutdown.
- On connection, the viewer raises once over an ordinary foreground window
  without taking keyboard focus. When an external foreground window covers a
  complete monitor, the viewer stays behind it and an initial automatic
  fullscreen request is deferred. No always-on-top style is used.
- The log records `AEROMIRROR_VIDEO_HOST_SHOW` and
  `AEROMIRROR_VIDEO_HOST_EXPOSE` with generation, client size, selected codec,
  and request outcome. `result=requested` means the redraw API was invoked; it
  is not a claim that pixels were physically visible.

The patch does not resize or fullscreen the viewer, set a render rectangle,
crop mirrored content, change scale, inspect pixels, restart the pipeline, or
block a GStreamer thread on the Qt GUI thread. It does not identify a game by
process name or inspect the foreground window title.

## Evidence status

- All final automated gates pass. Two clean native builds and the extracted
  no-Git corresponding-source rebuild reproduce core SHA-256
  `F4824A375AFCD5593D1AA2E58547703E38F211380ACF71095CB8A08929ADB0E9`.
- Runtime static verification passes from an ASCII path; runtime execution and
  self-test pass from a Unicode path. The managed build, NativeHost, NativeCore,
  NativeWorker, ReceiverResilience, Bonjour/firewall, AutomaticUpdate, exact
  review payload, and Setup gates also pass.
- Corresponding-source packaging now materializes patches with an isolated
  temporary Git index and object directory. The real repository object store is
  read only as an alternate, so the build does not write temporary objects into
  it.
- A fresh physical iPhone connection without moving, resizing, or
  fullscreening the viewer remains the acceptance boundary. Automated Present
  counters or an expose request alone cannot prove the black screen is fixed.
- Minimize/restore and ordinary-window/external-fullscreen physical checks also
  remain pending. Source inspection cannot prove visible redraw, focus
  preservation, or monitor stacking on the affected machine.

## Preserved behavior

- The accepted one-device portrait gallery negotiation from 0.12.24 remains
  unchanged: `4k60` still requests `998x2160@60`.
- Explicit fullscreen and one-press Escape, saved normal placement, per-device
  trust, discovery recovery, automatic-update policy, and runtime versions are
  unchanged. Only an initial automatic fullscreen request is deferred while an
  external fullscreen application owns the monitor.
- Caption Close remains a separate minimize-equivalent behavior pending its
  own session-stop design and physical validation.

## Known limitations

- Physical proof of the black-start fix is still pending on the affected PC.
- Physical proof of same-session restore and no-focus foreground behavior is
  also pending.
- Full-monitor coverage is deliberately used as the privacy-safe proxy for a
  fullscreen game or video. AeroMirror does not maintain a game database.
- The existing caption-Close/session-stop behavior is outside this patch.

## Publication boundary

Do not push, tag, publish, replace public assets, or make this updater-visible
until the exact local Setup passes the physical fresh-start matrix and the user
explicitly authorizes publication.

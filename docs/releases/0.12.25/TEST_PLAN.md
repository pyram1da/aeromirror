# AeroMirror 0.12.25 — video-surface lifecycle test plan

## Purpose

Verify that a fresh normal viewer displays the first mirrored session without
requiring a move, resize, maximize, fullscreen transition, or tray action; that
the same acknowledged surface redraws after restore; and that a connection is
raised without focus over ordinary windows but does not cover external
fullscreen content. This is a native child-HWND/D3D11 surface and window-stack
test, not a media-geometry or gallery-layout test.

## Current evidence status

| Gate | Status | Required evidence |
|---|---|---|
| Root-cause boundary | PASS | Retained 0.12.24 log has decoded sink buffers and advancing D3D11 pre-Present callbacks for about 13 seconds before fullscreen makes the image visible |
| Host-show/expose contracts | PASS | Native-owned generation; visible nonzero READY plus first selected D3D11 Present; async GUI expose; acknowledged-only restore re-expose; bounded WinId rebind; HIDE invalidation; logger-lifetime safety |
| Failure-recovery probe coexistence | PASS | Host Present proof remains installed when the independent failure-recovery pad probe is attached |
| Connection foreground contracts | PASS | One no-focus ordinary-window raise; external-fullscreen placement behind; initial automatic-fullscreen deferral; no process/title inspection or topmost style |
| Reproducible native build | PASS | Two clean Qt 6.10.1/GStreamer 1.28.5 builds reproduce core SHA-256 `F4824A375AFCD5593D1AA2E58547703E38F211380ACF71095CB8A08929ADB0E9` |
| Corresponding source and runtime | PASS | Extracted no-Git source rebuild reproduces the same core; pinned runtime static verification passes from ASCII and execution/self-test passes from a Unicode path; provenance passes |
| Managed and native regression suites | PASS | Managed build, ReceiverResilience, NativeHost, NativeCore, NativeWorker, Bonjour/firewall, and AutomaticUpdate checks pass |
| Review payload and Setup | PASS | Exact `0.12.25` review payload, x64 Setup, embedded equality, and non-installing Setup checks pass |
| Physical fresh normal viewer | PENDING | Image is visible without any viewer intervention on repeated H.265 and H.264 connections |
| Physical restore | PENDING | An acknowledged active stream remains visible after minimize/restore without resize, fullscreen, or reconnect |
| Physical foreground/fullscreen | PENDING | Ordinary raise does not take focus; external fullscreen is not covered and initial automatic fullscreen is deferred |
| Tag and publication | NOT AUTHORIZED | No push, tag, asset replacement, or GitHub Release |

## Verified local artifacts

- `AeroMirror-native-source-0.12.25.zip`: 896,580 bytes, SHA-256
  `15D0E850DAB85C42BDAB8FA413290E893506D22174F5C4CC423733B31A94948F`.
- `AeroMirror-review-payload-x64-0.12.25.zip`: 1,242,766 bytes, SHA-256
  `DA7753AB77977CD4B52C5A7835F74E792B15CD70823A23815793DEEB998133F1`.
- `AeroMirror-Setup-0.12.25.exe`: 1,549,312 bytes, SHA-256
  `C4BD7D3C11514D2146754205D61961920287EABD1516CBC54CB903FF95F0B2F3`.
- Setup and shell file versions are `0.12.25.0`; the embedded native core is
  `F4824A375AFCD5593D1AA2E58547703E38F211380ACF71095CB8A08929ADB0E9`.

These are local candidate artifacts only. They are not published assets and do
not create or imply a GitHub Release.

## Automated acceptance

1. Verify managed and Setup PE/file version `0.12.25.0` and three-part product
   version `0.12.25`.
2. Verify the native renderer lifecycle creates and owns every SHOW generation;
   Qt must consume the supplied token rather than minting an independent one.
   HIDE must invalidate older READY, Present, expose, and rebind work.
3. Verify the GUI-owner readiness callback checks the actual child HWND with
   `IsWindowVisible()` and `GetClientRect()` before acknowledging READY for the
   exact native generation. Retry must be bounded and coalesced.
4. Verify libuxplay arms readiness without calling expose until the first
   Present from the selected D3D11 sink. The Present callback must only publish
   atomic state and post the exact generation; it must not call expose or wait
   for Qt while holding the device lock.
5. Verify the later GUI turn rejects stale generations. Libuxplay must accept
   only the registered handle, nonzero client area, active visibility, matching
   native generation, first-Present state, and one pending expose. It must retain
   only the selected host sink under `renderer_lock`, release the lock, request
   `gst_video_overlay_expose()`, and then unref the sink.
6. Verify Show and WindowStateChange coalesce a re-expose only after READY was
   acknowledged for the current surface. A hidden, stale, unacknowledged, or
   already pending generation must not start another request or periodic loop.
7. Verify WinIdChange schedules bounded handle acquisition and cannot recurse
   into overlapping rebinds. Native code must suppress SHOW, replace both host
   handles, bind every current overlay sink outside the renderer lock, clear the
   old rendezvous, and start the next SHOW generation only after rebind ends.
8. Verify the selected D3D11 host Present proof remains installed when the
   independent failure-recovery sink-pad probe is attached. One diagnostic hook
   must not replace the other.
9. Verify every GUI-callable host-surface API is safe after logger teardown and
   does not call a logger whose lifetime may already have ended.
10. Verify ordinary foreground content receives one non-topmost raise with
   `SWP_NOACTIVATE`, while an external window whose client bounds cover its full
   monitor causes no-activation placement behind that window and defers an
   initial automatic fullscreen request. Neither path may take keyboard focus,
   set `HWND_TOPMOST`, inspect a title/process name, or persist topmost state.
11. Prove this path contains no synchronous cross-thread `SendMessage`, outer
   window resize, synthetic fullscreen, render rectangle, crop, scale, pixel
   inspection, pipeline reset, or decoder change.
12. Rebuild the exact pinned native source twice, verify identical output,
   update all patch/source/executable hashes, rebuild the prepared no-Git
   corresponding source, and rerun loader/runtime contracts. PASS: all three
   builds reproduce
   `F4824A375AFCD5593D1AA2E58547703E38F211380ACF71095CB8A08929ADB0E9`;
   runtime static verification passes from an ASCII path and execution/self-test
   passes from a Unicode path.
13. Run the complete managed and native regression matrix before producing the
   exact review payload and local Setup. PASS: managed build, ReceiverResilience,
   NativeHost, NativeCore, NativeWorker, Bonjour/firewall, AutomaticUpdate,
   review payload, and Setup gates pass.
14. Verify corresponding-source patch materialization uses an isolated temporary
   Git index and object directory, with the real repository object store exposed
   only through alternates. PASS: the build does not write temporary objects to
   the real object store.

## Physical acceptance — fresh normal viewer

For every row, start from a stopped mirror session. Do not move, resize,
maximize, minimize, fullscreen, or restore the viewer before recording the
initial result.

1. Install the exact local 0.12.25 Setup and record its SHA-256, Windows build,
   GPU, iPhone/iOS version, quality preset, and display DPI.
2. Select `4k60`, connect from the portrait iPhone home screen, and verify the
   image is visible immediately in the normal framed viewer.
3. Stop mirroring from iPhone, wait for the viewer to hide, and repeat the same
   fresh H.265 connection five times.
4. Repeat at least twice with a non-H.265 quality preset so the H.264 sink path
   is exercised independently.
5. In one successful session, enter fullscreen and exit once with Escape;
   verify the existing image remains visible and the saved normal placement is
   restored. This is regression coverage, not a recovery step.
6. Open the same portrait gallery photo used for 0.12.24 and confirm the
   accepted portrait result remains unchanged.
7. Preserve the log. Each fresh normal SHOW should carry one native generation,
   an accepted READY record, a selected-codec
   `AEROMIRROR_VIDEO_HOST_EXPOSE result=requested ... trigger=present-ready`,
   and one `AEROMIRROR_VIDEO_HOST_FOREGROUND` for the same connection. These
   markers support lifecycle ordering but do not replace the visible
   observation.
8. With Chrome or another ordinary desktop window foreground, start a new
   mirror and verify the viewer comes in front once without moving keyboard
   focus and without becoming always-on-top afterward.
9. With a game or video occupying the complete monitor and owning foreground,
   start a new mirror and verify AeroMirror neither covers it nor steals focus.
   Enable any supported automatic initial-fullscreen state for this row and
   verify that transition is deferred. After leaving the external fullscreen,
   confirm the viewer exists in the normal window stack, can be selected
   normally, and an explicit user fullscreen command still works.
10. During an acknowledged visible stream, minimize and restore the viewer.
    Verify video is visible again without resizing, fullscreening, reconnecting,
    or waiting for a periodic refresh. Retain the event-driven re-expose marker.
11. If the test environment permits a real DPI/monitor transition that recreates
    the child HWND, move the normal viewer across that boundary and retain the
    bounded rebind plus fresh-generation sequence. Treat absence of handle
    recreation as NOT EXERCISED, not PASS.

## Failure branch

If any fresh or restored normal viewer remains black, do not use fullscreen to
mark the row as passed. Preserve that session's native generation, READY, SHOW,
Present-ready-triggered EXPOSE, re-expose/rebind if applicable, CAPS, sink,
Present, window, codec, and timing lines. If foreground handling is wrong, also
retain its privacy-safe FOREGROUND marker and state whether the prior app
covered the monitor, whether focus moved, and whether automatic fullscreen ran.
The next isolated video experiment is to show the host before PLAYING and bind
only the selected codec sink to the child HWND; do not add window resizing,
render rectangles, crop, zoom, or periodic refresh loops.

## Publication boundary

This plan authorizes only a local build and physical test. It does not authorize
source push, a tag, GitHub Release, public asset replacement, or updater-visible
publication.

# AeroMirror 0.12.28 — audit verification

Baseline 0.12.27 physically FAIL for black initial video. On September 6 the
user reported that local .28 works. This accepts the reported normal-viewer
black-screen correction on that PC, without inferring session counts or codecs.
The enumerated physical matrix below remains PENDING unless separately recorded.

## Executable regressions

1. `RendererShowBoundary.Tests.ps1 -AssemblyPath <installed-0.12.27-copy>
   -ExpectBug`: invoke its production SHOW handler against an owned test-only
   hierarchy. Verify the child is displaced by saved desktop coordinates.
2. Run the same harness against the new shell without `-ExpectBug`: the child
   stays at identical coordinates and size; direct outer-bounds/aspect-fit
   mutation calls reject it; the outer host still restores saved bounds;
   an owned top-level HWND remains valid. Never construct a ReceiverContext,
   load user settings, show a form, or touch the installed receiver in this test.
3. `ControlWorkQueue.Tests.ps1`: successful UI-thread delivery, missing handle,
   handle recreation, queued/late disposal, exactly-once abandoned-download
   cleanup, 250 dispose/post races, and valid/invalid/destroyed HWND policy calls.
4. Build and run ReceiverResilience, AutomaticUpdate, BonjourFirewall,
   NativeHostContracts, NativeCoreContracts, NativeWorkerLifecycle and
   NativeVideoSurface checks. Core/source hashes must remain identical to .27.
5. Build the review payload and Setup; pass its embedded payload, runtime,
   helper-verification and transaction non-installing checks. Verify packaged
   shell identity is 0.12.28.0 and packaged core hash is unchanged.

## Diagnostic evidence

The September 6 13:00 snapshot on the untouched black viewer recorded host
client 547x1184; Qt child screen bounds 1335,149–1882,1333; GStreamer child
screen bounds 2662,267–3225,1490 and client 563x1223. Its relative origin
(1327,118) and size were exactly the saved outer desktop bounds. Collect only
the target process's window classes/geometry/visibility, not phone pixels.

`NativePresentationProbe.ps1` is a diagnostic, not an acceptance test. It uses
a synthetic magenta appsrc with the exact production surface header and an
explicit runtime path. It samples only points owned by its own test window.
The default never enters fullscreen; `-ExerciseFullscreen` is opt-in. The
GStreamer 1.28.1 normal-window synthetic point was magenta, which narrowed the
investigation but did not validate the actual AirPlay session/decoder path.

## Physical acceptance

- Physical: five untouched H.265 and two H.264 starts; normal/fullscreen
  transition, minimize/restore, disconnect/reconnect, gallery size, mixed-window
  Z-order and fullscreen-foreground protection. Do not use a resize or
  fullscreen action before judging the initial image.
- No automatic installation, Bonjour stop/restart or publication.

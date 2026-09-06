# AeroMirror 0.12.24 — portrait negotiation diagnostic plan

## Purpose

This plan tests whether the iPhone's Photos `3840x2160` presentation canvas is
caused by the display geometry AeroMirror advertises during AirPlay `/info`
negotiation. The PC viewer must not be resized, fullscreened, zoomed, or used as
a workaround during the decisive sequence.

## Current evidence status

| Gate | Status | Required evidence |
|---|---|---|
| Rejected 0.12.23 behavior removed | PASS | No 4:5 gallery target, height-preserving media transition, or related test contract remains |
| Managed version and argument contract | PASS | Exact `0.12.24.0`; `4k60` uses H.265, `998x2160@60`, and 60 fps without the old display request |
| Native diagnostic contracts | PASS | One privacy-safe display marker; independent sender generation and sink-local CAPS/CropMeta timelines; no mutating media operation or claimed 1:1 mapping |
| Native rebuild and provenance | PASS | Two clean builds and the extracted 149-entry no-Git source rebuild reproduce `82B579693B60E9A1865E15BE314592838A0D3918DD1AFDF02565873213CE9397` |
| Managed and native regression suites | PASS | Receiver resilience, native core, worker lifecycle, native host, Bonjour/firewall, and automatic update pass |
| Review payload and Setup | PASS | Exact versioned inputs, embedded equality, x64 Setup, and all four non-installing self-checks pass |
| Physical iPhone causal result | PASS (ONE DEVICE) | User accepted the portrait gallery result; log independently records portrait `/info`, sender, and sink geometry. Fullscreen first exposed the separately black initial D3D11 surface |
| General gallery compatibility | NOT CLAIMED | Requires repeatable fresh starts and evidence across iPhone/iOS/GPU combinations |
| Tag and publication | NOT AUTHORIZED | No push, tag, asset replacement, or GitHub Release |

The exact 0.12.24 artifact is accepted only for this physical diagnostic. Any
old 0.12.23 Setup or payload is superseded and must not be used for this test.

Exact local artifacts:

| Artifact path | Bytes | SHA-256 |
|---|---:|---|
| `artifacts\installer\AeroMirror-Setup-0.12.24.exe` | 1,540,608 | `901CCB0EBC8D0213B03C7E9A032D3CE8CEDCE6B0EFDB66A180CBCD4D27DEBD9A` |
| `artifacts\AeroMirror-review-payload-x64-0.12.24.zip` | 1,233,943 | `1750DE01FEE7BFF1D7EC3FF3F0521F2E450A3A4B39069D032E1DBD0C62E7C202` |
| `artifacts\release\0.12.24\AeroMirror-native-source-0.12.24.zip` | 885,683 | `2C9EE82E2E463FF754097B50402AB72244D3BBF55E1AB5A67F8F2DA98A61F487` |
| `artifacts\headless-runtime\uxplay-windows.exe` | 1,201,783 | `82B579693B60E9A1865E15BE314592838A0D3918DD1AFDF02565873213CE9397` |

## Automated acceptance

1. Verify managed and Setup PE/file version `0.12.24.0` and three-part product
   version `0.12.24`.
2. Prove the `4k60` argument vector includes H.265, `-s 998x2160@60`, and
   `-fps 60`, and no longer includes `-s 3840x2160@60`. Other presets and all
   renderer/window arguments must remain unchanged.
3. Prove `/info` emits exactly one `AEROMIRROR_DISPLAY_INFO` marker for the
   receiver-configured response advertised to the iPhone. Its fields may
   include model, feature masks, logical and
   pixel size, physical-size placeholder, rotation policy, refresh period,
   maximum fps, overscan, and display feature mask. It must not contain client
   identifiers, keys, plist bodies, or arbitrary request text.
4. Prove a non-JPEG sink installs one separate observational pad probe. It must
   emit `AEROMIRROR_VIDEO_SINK_CAPS` for every actual CAPS event with sink-local
   `caps_seq`, width, height, PAR, PAR presence, and `origin=event`. If the first
   buffer arrives before an observed CAPS event, it may emit exactly one
   `origin=snapshot` current-caps fallback. It must read and report CropMeta on
   the first buffer after CAPS, when its state or rectangle changes, and every
   120 buffers, with local `caps_seq`, `buffer_seq`, reason, and PTS validity.
   Prove no sender geometry epoch or dimensions are copied into this sink state.
5. Prove diagnostics add no capsfilter, videoscale, videocrop, pixel mapping or
   extraction, render rectangle, crop request, viewer resize, or window-policy
   call. Probe cleanup must occur before sink and pipeline unref.
6. Rebuild the exact pinned native source twice, verify identical output, update
   every required provenance hash, rebuild from the prepared corresponding
   source archive, and rerun native core and worker-lifecycle contracts.
7. Run the complete managed build and regression matrix. Only after every gate
   passes, create the exact review payload and local Setup and record their
   sizes and SHA-256 values here.

## Physical test A — decisive no-window-intervention sequence

1. Record the exact Setup SHA-256, native-core SHA-256, Windows version,
   iPhone/iOS version, GPU, selected decoder/sink, quality preset, display DPI,
   and phone orientation-lock state.
2. Select `4k60`, start a fresh receiver session, and connect from iPhone Screen
   Mirroring while the phone is on its portrait home screen.
3. Do not move, resize, maximize, minimize, or fullscreen the AeroMirror viewer.
   Do not invoke manual fit, tray restore, zoom, or renderer changes.
4. Wait for the home screen to move normally, then open the same portrait photo
   that reproduced the small-image symptom. Leave it open for at least ten
   seconds, then return to the home screen without touching the PC window.
5. Export the current AeroMirror log and retain these independent timelines in
   wall-clock/log order:
   - the receiver-advertised `/info` display tuple;
   - sender-side home, Photos, and return-home primary/source/auxiliary/encoded
     geometry generations;
   - every actual sink CAPS event and any first-buffer snapshot fallback, with
     `caps_seq`, dimensions, PAR, and origin;
   - sink CropMeta observations with `caps_seq`, `buffer_seq`, reason, PTS
     validity, and present/none state.
   Absence of a sink CAPS event during a sender transition is evidence and must
   be recorded. Do not assign any sender generation to a sink `caps_seq` as a
   one-to-one mapping.
6. Record what is actually visible, including all four photo edges and black
   bars, but do not infer transmitted geometry from the PC window shape.

## Result branches

### A — Photos becomes portrait

If Photos reports `998x2160 aux=0x0` and the independent sink timeline also
shows portrait CAPS during the observed Photos interval, record that the result
supports the display request controlling the former wide canvas on this phone.
Do not describe the two records as a protocol-correlated pair.
Repeat the entire fresh-session sequence five times before considering a
general negotiation design. Do not publish the hard-coded phone size.

### B — Photos remains `3840x2160`

If Photos still reports the wide canvas, record whether `/info` actually
advertised `998x2160@60`. If it did, the request is ignored or insufficient. Keep
window and renderer behavior unchanged and isolate the next protocol variable;
do not restore the 4:5 workaround.

### C — display and sink geometry diverge

If `/info` advertises the portrait display while the sender timeline and the
independent sink CAPS/CropMeta timeline differ, or no sink CAPS event occurs,
use the ordering only to narrow parser, decoder, converter, or sink
investigation. The diagnostics expose no shared correlation identifier and are
evidence only; they must not become an automatic crop or resize without a
separate reviewed design.

## Regression observations

After the decisive run, separately verify fullscreen and one-press Escape,
ordinary portrait/landscape home-screen rotation, reconnect, idle discovery,
and another quality preset. These rows detect collateral regression but do not
replace the causal Photos sequence.

Record the known black-initial-normal-viewer and Caption Close behavior
separately. Do not use fullscreen to refresh a black viewer during the decisive
sequence; if the image is not visible, fail that run and preserve its log.

## Publication boundary

Do not create a tag or GitHub Release from this candidate. A successful result
on one phone authorizes analysis of the next implementation, not publication of
the device-specific `998x2160@60` request. Public 0.12.22 and its assets remain
immutable.

# AeroMirror 0.12.24 — portrait display-negotiation diagnostic

## Summary

0.12.24 is a local diagnostic candidate for the case where an iPhone begins
with a portrait `998x2160` mirror but Photos changes the transmitted canvas to
`3840x2160`. It does not resize the PC window to make the resulting photo look
larger. Instead, it changes one AirPlay negotiation input and records the native
receiver-advertised display tuple, sender header geometry, and independent sink
negotiation timeline.

This is not a public release. The reported iPhone now shows the portrait gallery
photo correctly with this negotiation request, but that one-device result is
not a cross-device compatibility claim. No `v0.12.24` tag or GitHub Release
exists, and public 0.12.22 remains the updater-visible latest release.

## Should I update?

- No public update is available.
- The automated build, provenance, runtime, packaging, and non-installing Setup
  gates pass. Use the exact local Setup only for the controlled physical test
  in the [test plan](TEST_PLAN.md).
- Do not distribute this device-specific display request as a general quality
  improvement until repeatable iPhone evidence establishes its effect.

## What changed

- For the `4k60` preset only, AeroMirror requests a portrait
  `998x2160@60` display instead of `3840x2160@60`.
- H.265, 60 fps, receiver model, feature masks, rotation policy, renderer
  selection, sink scale, and viewer geometry remain unchanged. This keeps the
  physical comparison to one causal variable.
- Native `/info` handling emits one privacy-safe `AEROMIRROR_DISPLAY_INFO`
  marker describing the receiver-configured display tuple advertised to the
  iPhone.
- Sender header geometry keeps its existing sender-side generation. A separate
  observational sink-pad probe records every actual CAPS event with sink-local
  `caps_seq`; if the first buffer arrives before any observed CAPS event, it
  records one current-caps snapshot as a fallback.
- The sink reads `GstVideoCropMeta` on the first buffer after CAPS, whenever its
  presence or rectangle changes, and every 120 buffers. Absence of a CAPS event
  is evidence, but no one-to-one sender-generation-to-sink-sequence mapping is
  claimed.
- The diagnostics do not record plist bodies, client identifiers, mirrored
  pixels, or sampled image content and do not insert crop, scale, capsfilter, or
  render-rectangle behavior.
- The rejected 0.12.23 4:5 outer-window experiment and its tests are removed.

## Automated evidence

- Two independent clean builds and the extracted no-Git corresponding-source
  rebuild reproduce native core SHA-256
  `82B579693B60E9A1865E15BE314592838A0D3918DD1AFDF02565873213CE9397`.
- Native contracts/lifecycle, the managed build and focused regression suites,
  isolated runtime verification, exact review packaging, x64 Setup, embedded
  equality, and all four non-installing Setup self-checks pass.
- The subsequent physical run on the reported iPhone accepted the visible
  portrait gallery result, while the log independently recorded portrait
  `/info`, sender, and sink geometry. Fullscreen first exposed the separately
  black initial D3D11 surface, so a strict untouched-window fresh start remains
  a regression row for the surface-lifecycle fix.

## How to interpret the test

- If Photos changes to `998x2160 aux=0x0` and the independent sink timeline also
  shows portrait CAPS during the observed Photos interval, that supports
  display negotiation as the cause of the earlier wide canvas on this phone;
  it is not a protocol-level one-to-one match.
- If Photos still changes to `3840x2160`, the portrait request is ignored or is
  insufficient; another protocol variable must be isolated.
- If the display response and sender geometry are portrait while the independent
  sink timeline records different CAPS, or records no CAPS event, the separate
  observations narrow the next boundary without proving where one sender event
  became one sink event.

None of these observations alone proves compatibility across other iPhone,
iOS, GPU, renderer, or quality-preset combinations.

## Known separate issues

- The initial normal viewer can appear black until fullscreen refreshes the
  native D3D11 surface. That surface-lifecycle issue is not changed here.
- Caption Close remains a minimize-equivalent until a session-only native
  disconnect command is implemented and physically verified.
- Gallery-content rotation and trustworthy inner-photo bounds remain deferred.

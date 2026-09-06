# AeroMirror 0.12.23 — rejected outer-window experiment

## Disposition

0.12.23 was an unpublished local experiment that widened the normal viewer to
4:5 when the known Photos `3840x2160 aux=0x0` canvas appeared. Although the
complete frame remained contained, the approach changed the PC window to make
the inner photo look larger instead of finding the error in AirPlay negotiation
or native frame geometry.

The user rejected that behavior. The 4:5 implementation and its geometry tests
were removed before the next candidate. It is not a product fix, release
baseline, or supported test version.

## Artifact boundary

- Do not install, distribute, tag, or publish any previously generated 0.12.23
  Setup, payload, or package.
- Earlier local hashes and automated results describe superseded code and are
  intentionally not retained as accepted release evidence.
- No `v0.12.23` tag or GitHub Release exists.
- Public 0.12.22 remains immutable and updater-visible latest.

Investigation continues in the isolated
[0.12.24 portrait negotiation diagnostic](../0.12.24/RELEASE_NOTES.md), which
keeps the viewer behavior unchanged.

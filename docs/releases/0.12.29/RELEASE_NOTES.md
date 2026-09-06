# AeroMirror 0.12.29 — bounded update work

Local follow-up to the user-reported working 0.12.28 video correction.
This is an unpublished local candidate, not a public Release.

## Should I update?

Optional, if you want the update-check robustness follow-up. The working .28
baseline is preserved; there is no new video, gallery or Bonjour fix here.

## Changes

- Resolve release metadata through the confirmed permanent GitHub repository
  ID and accept exact Setup paths for its canonical/historical names. This
  fixes rejection of the canonical installer after the repository owner rename.
- Bound release metadata to 1 MiB and one 30-second whole-transfer deadline.
- Cancel obsolete metadata/download work when its actual owner closes or
  automatic updates are disabled, preserving the verified installer handoff.
- Added executable slow-response, cancellation and ownership-race checks.
- Dispose HTTP error responses and retain exact installer/hash validation.

See [D-017](../../DECISIONS.md#d-017--keep-automatic-updates-opt-in-and-apply-them-only-at-a-later-safe-start)
for the unchanged opt-in/later-start policy and its owner-cancellation boundary.

## Known limitations

Caption Close still minimizes rather than disconnecting the iPhone; the
session-only native boundary required by D-018 is separate work. The full
physical codec, mixed-window and Bonjour matrices remain pending. No automatic
installation or GitHub publication is included.

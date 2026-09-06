# AeroMirror 0.12.27 — video-surface ownership and initial window placement

Local review candidate. Not published or authorized for publication.

## Summary

The 0.12.26 normal viewer still opened black on the reported PC. This follow-up
removes Qt background painting from the video child window and verifies the
initial window order instead of trusting a successful Windows API return alone.

## Should I update?

Use only the exact local candidate for the requested physical retest. Do not
describe this as a verified black-screen fix until those tests pass.

## Changes

- A dedicated video widget leaves drawing to the external video sink. It
  overrides the Qt paint engine and disables Qt background/backing-store work.
- A new connection checks whether ordinary windows still cover its viewer.
  One immediate promotion/demotion is permitted only after a repeated check
  for fullscreen foreground content. It requests no keyboard focus and leaves
  no persistent topmost style.
- Gallery negotiation, saved normal bounds, codec policy and frame scaling
  remain unchanged; no resize/fullscreen workaround is introduced.
- A Windows/Qt regression harness checks the exact production surface contract
  using hidden native windows, without a receiver connection or service change.

See [D-021 and D-024](../../DECISIONS.md) for the presentation boundaries.

## Known limitations

Physical first-frame visibility and the mixed-window arrangement must be
retested. Automated tests and Present/READY markers do not prove visible pixels.
Stopped-Bonjour recovery remains pending on a real service stop. Existing
unsigned review-release and runtime redistribution limitations are unchanged.

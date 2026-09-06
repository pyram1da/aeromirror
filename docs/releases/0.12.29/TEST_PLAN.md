# AeroMirror 0.12.29 — verification plan

Status: automated/build/packaged-shell checks PASS on September 6. Physical
.29 UI/update rows remain PENDING. Preserve the frozen .28 artifacts and the
user's working installation; never exercise tests against live phone content
or Bonjour. Exact hashes and scope are in LOCAL_BUILD_REPORT.md.

## Managed behavior

- Test the production metadata reader with an isolated loopback HTTP server:
  valid release data, a bounded body, delayed headers, a stalled body, and a
  steadily progressing slow body. The total transfer budget must not reset
  after each read. Test cancellation before and during I/O and after success.
- Check exact fixed production endpoint, no automatic redirect, retained API
  headers, body limit, response disposal and a subsequent successful request.
- Pass canonical and historical exact Setup paths through the real digest
  verifier; reject other owners/projects before any download. The optional
  `UpdateTransport.Tests.ps1 -CheckGitHub` performs one anonymous metadata GET
  through the candidate's permanent-ID endpoint and parses the real release.
  It never downloads an installer or launches the receiver/Setup.
- Exercise owner close/cancel/complete races and renewed work after cancellation.
  Hiding a settings window to tray is not disposal and must retain valid work.
- Preserve exact installer name, release URL, HTTPS redirect, size, SHA-256 and
  staging checks. Canceled downloads must not launch or leave pending files.
- Build and run ReceiverResilience, AutomaticUpdate, ControlWorkQueue and the
  RendererShowBoundary test against the candidate. Do not alter .28 child-HWND
  ownership or native gallery behavior.

## Packaging and handoff

- If producing Setup, validate the exact 13-entry payload, version 0.12.29.0,
  unchanged native core and all four non-installing Setup gates. Retain complete
  corresponding native source and record local hashes, not a public release.
- Run git diff --check; update mandatory and conditional documents with actual
  results before handoff. No installation or publication without authorization.

## Physical follow-up

Completed: 15 isolated transport/lifetime cases (including 200 cancellation
races), optional live GitHub metadata GET parsing public 0.12.22, canonical and
historical exact-asset/digest tests, ReceiverResilience, RendererShowBoundary,
ControlWorkQueue (250 disposal races), BonjourFirewall and NativeHostContracts.
The final extracted .29 shell passes transport/live, update, renderer,
resilience and Bonjour checks. Setup passes all four non-installing gates;
the 13-entry payload and complete corresponding native source are validated.

The user reported .28 works; do not reinterpret that as five H.265/two H.264
starts or Bonjour recovery. Manual check/retry and opt-in disable/exit should
be tested by the user on the new candidate without interrupting mirroring.

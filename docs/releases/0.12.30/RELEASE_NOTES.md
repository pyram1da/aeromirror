# AeroMirror 0.12.30 — session Close and viewer fixes (review)

Review release for Windows 10/11 x64, including the fixes developed since
0.12.22. Automated build, lifecycle and package checks pass; the remaining
physical Windows/iPhone checks are still pending.

## Should I update?

Update for the black normal-viewer correction, portrait Photos sizing, safer
update handling, or to test closing the current transmission from the PC.
Keep a working installer available while testing this review release.

Older builds affected by the GitHub repository rename may need a manual
download of `AeroMirror-Setup-0.12.30.exe` from this release. Running Setup
updates the existing installation and preserves settings and device trust.

## Changes

- Caption Close requests termination of the displayed mirroring session,
  rather than minimizing its window. The receiver remains available.
- Each successful mirror SETUP has an immutable ID. A delayed Close cannot
  select a later stream, even on the same control socket.
- The connection owner drains that session's media workers and returns an
  explicit result. Listener, DNS-SD and BLE restart are not part of Close.
- The shell only suppresses lost-connection UI for a matching native session.
- The lost-connection warning's Close uses the same native session handler.
  Its captured process/generation/stream prevents an old warning from closing
  a newer phone connection. Programmatic warning closure remains separate.
- A matching completed Close clears the shell's active-session and loss-
  recovery state without depending on a legacy worker log line.
- Preserve the accepted portrait gallery and top-level-only window-placement
  fix; no resize/fullscreen workaround is introduced.
- Includes portrait AirPlay display negotiation for Photos, correcting the
  reported small-photo presentation without reshaping the viewer as compensation.
- Fixes the confirmed black-viewer cause: saved desktop coordinates are no
  longer applied to the inner video window. The native surface lifecycle also
  binds the selected video sink before playback.
- Raises a newly connected viewer over ordinary windows without keeping it
  always-on-top; fullscreen foreground content remains protected.
- Offers a contextual **Запустить Bonjour** action for a safely validated,
  stopped Apple Bonjour service. Only a user click may request administrator
  confirmation; startup does not automatically raise a prompt.
- Bounds and cancels abandoned update work, cleans up late downloads safely,
  and accepts verified installer URLs after the repository-owner rename.

## Known limitations

Physical iPhone disconnection, reconnect, lock-then-close and the full codec/
window/Bonjour matrix remain pending. Fifty loopback Close/reconnect cycles are
not proof that iOS leaves Screen Mirroring. Independent network-failure recovery
is not disabled by this change. This is not a fully accepted stable release.

See the [physical test plan](https://github.com/pyram1da/aeromirror/blob/v0.12.30/docs/releases/0.12.30/TEST_PLAN.md)
and [D-018](https://github.com/pyram1da/aeromirror/blob/v0.12.30/docs/DECISIONS.md).

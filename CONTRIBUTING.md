# Contributing to AeroMirror

For a black initial viewer, keep it untouched while collecting target-process
window metadata with `tests/RendererWindowSnapshot.ps1`. Record the exact shell
version/core PID and compare outer, Qt-surface and GStreamer-child coordinates;
do not collect phone pixels or unrelated application titles. The .28 executable
regression is `tests/RendererShowBoundary.Tests.ps1`. Physical output remains a
separate acceptance check after this harness passes.

Thanks for testing AeroMirror. Review builds are expected to have rough edges;
a precise report is more useful than a long description without reproduction
details.

## Before reporting a problem

1. Confirm the AeroMirror version in **Updates**.
2. Confirm the iPhone and PC are on the same local network.
3. Search existing GitHub issues for the same symptom.
4. Reproduce the problem once more and note the exact local time.

Do not repeatedly restart or reinstall before collecting the first failure:
the first-run state is often important.

## A useful bug report

Open one GitHub issue per problem and include:

- AeroMirror version and whether it was a fresh install or an update;
- Windows edition/build and Windows display scaling;
- iPhone model and iOS version;
- connection type (Wi-Fi or Ethernet), Windows network profile
  (Private/Public), and whether a VPN, hotspot, or virtual adapter was active;
- GPU and selected renderer, quality, latency, and audio settings;
- exact steps, expected result, actual result, and failure time;
- whether stopping the receiver, starting it again, or restarting the whole
  app changed the result;
- whether an AirPlay session was connecting, active, or disconnecting;
- for a receiver missing after long idle, include the last successful session,
  Windows lock/unlock times, each automatic recovery decision, each correlated
  discovery request/generation with PID and ports, each iPhone browse/tap time,
  and the first log before taking any recovery action. If diagnostics explicitly
  require a controlled app restart, also retain the replacement PID and fresh
  DNS-SD/BLE startup after that restart;
- for a Wi-Fi interruption or frozen reconnect, whether the continuity view
  showed connection lost, waiting for image, the Screen Mirroring reconnect
  hint, or faded before the picture actually resumed; retain the log from the
  first feedback warning through any manual reselection and final image/hint;
- for a 0.12.14 frozen-last-frame report, keep the complete
  `AEROMIRROR_VIDEO_HEALTH` sequence from mirror start through iPhone Stop and
  note the visible freeze time. Do not trim the report to one classifier line:
  interval deltas and ages across several records are required to locate the
  stalled stage;
- for a 0.12.15 pause or frozen-last-frame report, additionally keep every
  `AEROMIRROR_VIDEO_IMPLICIT_RESUME reason=valid-type0` line and note whether
  visible motion actually resumed. The marker proves that one decrypted and
  validated access unit triggered a resume request; it does not prove decode,
  presentation, or a physically fixed freeze by itself;
- for a missing-after-idle report, retain the numbered automatic recovery
  lines, every correlated discovery request/result, lock/unlock and sleep/wake
  times, PID and ports, and the time of each iPhone Screen Mirroring browse.
  Note whether Windows service recovery restored visibility without restarting
  AeroMirror; a local ready marker is not remote visibility proof;
- for layout issues, the phone orientation and the app/media being displayed;
  for Photos also retain the ordered raw/encoded geometry, whether a phone-
  shaped frame preceded or followed the exact media signature, and measure the
  outer renderer separately from the visible inner photo/video;
- for the local 0.12.24 Photos negotiation probe, do not move, resize, or
  fullscreen the PC viewer during the decisive Home -> Photos -> Home run.
  Retain the receiver-advertised `AEROMIRROR_DISPLAY_INFO`, the sender header
  geometry generations, and the independent sink-local
  `AEROMIRROR_VIDEO_SINK_CAPS`/`AEROMIRROR_VIDEO_SINK_CROP` timeline with
  `caps_seq`, `buffer_seq`, reason, and PTS validity. A missing CAPS event is
  evidence too. Do not pair a sender generation with a sink sequence as though
  the protocol supplied a one-to-one identifier; separately describe what was
  visible. These content-free records narrow boundaries but do not establish a
  fix;
- for a local 0.12.25 black-initial-viewer report, start mirroring into the
  remembered normal window and do not move, resize, or fullscreen it. Retain
  the first `AEROMIRROR_VIDEO_HOST_SHOW` and selected-codec
  `AEROMIRROR_VIDEO_HOST_EXPOSE` records together with the normal sink/Present
  timeline, and state whether the first visible frame appeared without any
  window action. SHOW records the native generation and a READY check for the
  visible nonzero child HWND; EXPOSE with `trigger=first-present` proves only
  that the generation-safe post-Present redraw was requested. Neither marker
  proves physically visible pixels. For restore or handle-recreation reports,
  retain the same-generation re-expose or fresh-generation rebind sequence.
  For a foreground report, also retain `AEROMIRROR_VIDEO_HOST_FOREGROUND` and
  say only whether the prior app was ordinary or fullscreen, whether keyboard
  focus moved, and whether initial automatic fullscreen was deferred; do not
  include the other application's title;
- for the local 0.12.26 first-surface test, begin in the remembered normal
  window and do not move, resize, maximize, or fullscreen it. Retain the
  renderer-session generation, selected-codec SHOW/READY/bind-before-PLAYING
  sequence, first push/sink/Present evidence, and whether pixels appeared
  without any PC window action. For rapid stop/start or reconnect, also retain
  stale-generation rejection and bounded renderer-reference diagnostics. The
  ordering markers and reproducible binary hash prove code paths, not visible
  pixels;
- for local 0.12.27, repeat the untouched normal-window test after the Qt
  external-surface correction. For window order, include an arrangement with
  several ordinary application windows and report whether the viewer starts
  above all of them, preserves typing focus, can be covered afterward, and
  stays behind fullscreen content. `FOREGROUND result=raised` now follows an
  actual order check; `fallback=1` can denote one immediate promotion/demotion,
  not an always-on-top mode. Keep application titles and personal pixels out
  of shared diagnostics;
- if a Windows 10 first install works only after reboot, retain `setup.log`,
  `receiver.log`, Bonjour service/process state, pending-reboot state, and
  iPhone visibility before and after reboot. AeroMirror does not normally
  install a framework that requires reboot, and reinstalling on the same PC is
  not a clean Bonjour reproduction.
- if a receiver name is changed, state whether AeroMirror displayed a
  normalization notice and report only its input/effective UTF-8 byte counts,
  not the private name itself. Version 0.12.13 and later limit the effective name to 50
  UTF-8 bytes so `device-ID@name` remains a valid Bonjour label.

Screenshots or a short screen recording are welcome when they do not expose
private messages, photos, account names, or other personal information.

## Logs and privacy

The current local log is:

```text
%LOCALAPPDATA%\AirPlayReceiverMvp\receiver.log
```

Open it in a text editor and share only the short section around the failure
time. Before attaching it publicly, remove:

- PIN values and command-line fragments containing `-pin`;
- Windows user names and personal folder paths;
- computer, receiver, Wi-Fi, and network-adapter names;
- IP addresses or other identifiers you do not want to publish.

Never upload `receiver-key.pem`, `trusted-clients.txt`, settings containing a
PIN, memory dumps you have not reviewed, or mirrored photo/video content.

The reflection-based resilience suite must never be pointed at this production
directory. It creates one GUID-named child of the system temporary directory,
sets that storage root once before `AppSettings` or logging is initialized,
waits for a successful logger drain, and removes only that exact root after a
successful run. A test that cannot establish this isolation must fail before
it writes persistent state.

## Crash reports

For a crash, report separately whether:

- the AeroMirror settings/tray application disappeared;
- only the mirrored-video window disappeared;
- the receiver returned automatically;
- Windows displayed an error dialog.

Include the exact crash time and the last 50–100 relevant redacted log lines.
If a future build creates a diagnostic package or dump, review its contents
before opting to attach it.

## Pull requests

- Discuss substantial protocol, security, installer, or UI changes in an
  issue first.
- Keep each pull request focused on one problem.
- Preserve Windows 10 1809 x64 compatibility unless the change is explicitly
  approved otherwise.
- Add or update tests and documentation for changed behavior.
- Do not add proprietary Apple/vendor code, keys, certificates, or material
  with an incompatible license.
- By contributing, you agree that your contribution is provided under the
  repository's GPL-3.0-or-later license.

For future ideas and known protocol constraints, see
[`docs/TODO.md`](docs/TODO.md).

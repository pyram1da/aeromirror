# Contributing to AeroMirror

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

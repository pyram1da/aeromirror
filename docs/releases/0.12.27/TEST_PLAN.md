# AeroMirror 0.12.27 — external surface and initial window-order test plan

## Scope and baseline

0.12.26 is physically FAIL for the untouched normal viewer and the reported
mixed-window order. Preserve its artifacts. This candidate changes only Qt
surface painting ownership and the connection-time placement transaction;
gallery geometry and native selected-sink lifecycle remain the baseline.

## Automated gates

- Exact source/patch/executable provenance, clean native rebuild reproducibility
  and a complete corresponding-source archive.
- Hidden Windows Qt 6.10.1 executable: the base widget rejects PaintOnScreen;
  the exact production widget retains it, has a distinct child HWND, no Qt
  paint engine, no system background and no auto-fill, including after events.
- NativeHost source contracts: no crop, resize/fullscreen recovery, activation,
  or persistent topmost; verify actual Z-order, immediate demotion and repeated
  fullscreen guard before fallback. Keep the existing lifecycle contracts.
- Managed build, ReceiverResilience, NativeHost, NativeCore and NativeWorker
  regressions; unchanged Bonjour/update gates if the full package is rebuilt.
- Runtime verification, exact 0.12.27 shell/Setup versions and review payload,
  all non-installing Setup checks. No automatic install or service restart.
- `git diff --check`.

## Physical acceptance (all PENDING)

Record Windows build/GPU/display scaling, iPhone/iOS, codec/preset, exact Setup
and core hashes, test timestamps and redacted markers. Retain no PIN, key,
trusted-client material or personal mirrored pixels.

1. Five fresh H.265 and two H.264 connections: untouched normal viewer shows
   the first frame. Do not move, resize, maximize, fullscreen or use tray actions
   before judging the row. One black start fails the primary gate.
2. Repeat with the Explorer Installer folder and AeroMirror in front of the
   Instagram/Google windows. The new viewer appears above all ordinary windows;
   typing focus stays in the original application. Clicking another window
   can cover the viewer afterward (no retained always-on-top).
3. Connect while an external game/video fills its monitor. It remains above
   the viewer and automatic AeroMirror fullscreen is deferred. Repeat while
   foreground content changes near connection; failure must not force focus.
4. Minimize/restore, cover/uncover, disconnect/reconnect, Escape and Alt+Enter
   during a healthy stream: no new black frame or abandoned topmost style.
5. Open a portrait Photos image and zoom on the phone in a normal viewer.
   Preserve the previously accepted size and content; do not reshape the window.
6. Bonjour: carry forward the pending 0.12.26 physical row. Do not deliberately
   stop it on the daily-use PC as part of this rendering test.

Readiness/Present/expose and `FOREGROUND result=raised` are diagnostic evidence,
not substitutes for these observations. Failure or an unrun row stays explicit.

## Execution record

Build and automated gates: PASS on 2026-09-06.

- Managed build; ReceiverResilience; BonjourFirewall; AutomaticUpdate;
  NativeHostContracts; NativeCoreContracts (including production AES NIST
  positive-path check); NativeWorkerLifecycle (eight scenarios); and
  NativeVideoSurface on hidden Windows HWNDs, pinned Qt 6.10.1: all passed.
- Two clean final-source builds and the extracted no-Git corresponding-source
  build each produced core
  `B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F`.
- Runtime dependency closure: 200 binaries inspected. Isolated runtime
  self-test passed from the Unicode workspace path. Static dependency checking
  ran in the builder's ASCII staging path; no development PATH was used by
  the isolated self-test.
- 0.12.27 Setup: `/verify-runtime`, `/verify-shortcut-selection`,
  `/verify-update-lifecycle`, `/verify-bonjour-recovery` passed without
  installing or changing the live receiver. Embedded payload and source
  provenance were compared with the reviewed build inputs.
- `git diff --check`: PASS. Existing unrelated dirty changes were preserved.

Physical gates: PENDING. Publication: NOT AUTHORIZED; no tag/release action.

## Final local artifacts

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `AeroMirror-Setup-0.12.27.exe` | 1,561,088 | `66DCA2D74F5A16C37299132114AFB9022D576360AED654F5782CF5901D4D857F` |
| `AeroMirror-review-payload-x64-0.12.27.zip` | 1,254,749 | `3A62F21566DFECF927FE751DF844767EE33FD18D2B811737F5C47DDAD9055704` |
| `AeroMirror-native-source-0.12.27.zip` | 911,598 | `9C1B576D0A67C4BADE3D250F7B10E437DF87562CCFE35B3A09ACA22E8E904C44` |
| packaged `AeroMirror.exe` | 827,904 | `3202D966F2033325707E8C18C14BB279C5FCA2F50FE0BF78CA3A7957D0248BAF` |
| packaged `core/uxplay-windows.exe` | 1,231,896 | `B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F` |

Shell and Setup are `0.12.27.0`. The payload has 13 files, and native source
has 149 entries / 145 files. Final Setup embedded payload/provenance equality
was rechecked after packaging the final contributor guidance. No tag, source
push, public asset, installation, receiver restart or Bonjour restart occurred.

The inspected retained receiver log covered September 5 startup, not the exact
September 4 failing connection. It did not supply a new physical mirroring
test. The Qt contract defect is source- and executable-test-backed; attributing
every prior black frame to it remains an inference until physical retesting.

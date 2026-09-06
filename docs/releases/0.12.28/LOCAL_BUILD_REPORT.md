# AeroMirror 0.12.28 — local build report

Date: 2026-09-06. Local, uncommitted candidate on top of `01f4d60` with the
pre-existing .23–.27 work preserved. No source push, tag, GitHub Release,
installation, settings reset, or Bonjour service mutation was performed.

## Confirmed baseline and regression

Installed .27 remained physically black. A read-only live hierarchy at 13:00
identified the GStreamer child at a parent-relative (1327,118), exactly the
saved outer desktop origin, outside the Qt parent's client clipping area.
The same production SHOW handler in a copy of installed .27 displaces a real
hidden test child. The .28 packaged handler keeps it unchanged, rejects direct
outer-bound/aspect-fit mutation of the child, and still restores the real
outer window. This is evidence of the corrected defect, not physical .28
iPhone acceptance. See AUDIT.md and TEST_PLAN.md.

## Passed gates

- `build.ps1`: x64 shell, file version 0.12.28.0.
- ReceiverResilience and AutomaticUpdate on both build output and the exact
  extracted review payload shell.
- RendererShowBoundary: installed .27 reproduction and corrected build/packaged
  shell; child unchanged, root restored, child mutations rejected, owned root
  accepted, no visible forms or receiver construction.
- ControlWorkQueue: normal UI delivery, absent/recreated handles, late results,
  queued disposal, exactly-once cleanup, 250 disposal races, hidden-window policy
  acknowledgement and invalid/destroyed-window failures.
- BonjourFirewall: read-only service state, explicit stopped-service action,
  no automatic UAC and exact firewall contracts.
- NativeHostContracts.
- NativeCoreContracts against the prepared production source; source checks
  and the positive AES-128 CTR NIST split/reset executable. No malformed protocol
  input or exploit reproduction was used.
- NativeWorkerLifecycle: eight production-helper scenarios.
- NativeVideoSurface: exact production header, Qt 6.10.1 Windows plugin,
  hidden HWNDs. This does not prove phone pixels.
- Review packaging: exact 13 entries, shell 0.12.28.0 and unchanged native core.
- `build-native-source.ps1`: complete prepared .28 corresponding source, both
  patches materialized/verified with pinned provenance. Native bytes did not
  change; no new native binary/reproducibility claim is needed for this patch.
- Setup verification: `/verify-runtime`, `/verify-shortcut-selection`,
  `/verify-update-lifecycle`, `/verify-bonjour-recovery`; all exit 0.
- `git diff --check`.

## Artifacts

| File | Bytes | SHA-256 |
|---|---:|---|
| AeroMirror-Setup-0.12.28.exe | 1563648 | 47163C557DC12B5D77CDE990AE5E0654EFD15583CA37627E4B2A70777FA6482B |
| AeroMirror-review-payload-x64-0.12.28.zip | 1256875 | ACA703A1F8185354602C450885490609A6ABE5B77635757A71626AECDA53EB93 |
| AeroMirror-native-source-0.12.28.zip | 911598 | 8D9F9290EA5448DE022F8782AD547AF394E1C205F0896B894D1A604B3F818676 |
| AeroMirror.exe | 830464 | 141FE8D59FD1C64DB1A6002D15E0F47DDCEF28C7991D5E010A4F89216771397E |

Unchanged core SHA-256:
`B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F`.
The network installer retains the pinned upstream GStreamer 1.28.1 runtime;
the engineering stage uses 1.28.5. The synthetic probe was repeated with the
explicit 1.28.1 runtime path matching the installed DLL hash; only its normal
synthetic point was used to narrow the diagnosis, not to accept AirPlay.

## Pending

Physical follow-up on September 6: the user tested the local candidate and
reported that everything works. This is user evidence for the reported
black-screen correction, not an agent installation or an enumerated test run.
The frozen artifact hashes above are unchanged.

Five untouched physical H.265 and two H.264 fresh starts, reconnection,
minimize/restore, gallery, mixed-window Z-order and fullscreen-foreground
protection. Bonjour's stopped-service physical test remains independent and
pending. Broader line-by-line receiver/installer decomposition and the
close-to-disconnect product change remain TODO work. This is not a completed
whole-third-party security audit or a public release report.

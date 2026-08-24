# Build report — AeroMirror 0.12.19

Release `v0.12.19` was built from commit
`997e29324ec092ad46ae83a00fa6d08525c1b863` and tree
`f68eb9733adc4694d2cb2595bc3c8a660eba34b3`. Its annotated tag object is
`12be5da57585436cc512fb06a51795beaf854820`; the tag was created at
`2026-08-24T14:53:32+03:00`. GitHub Release `375664260` was published at
`2026-08-24T12:04:02Z`:

https://github.com/pyram1da/aeromirror/releases/tag/v0.12.19

The tag and all four public assets are immutable release history. Any
correction must use a later patch version; do not move this tag or replace an
asset under 0.12.19.

## Release channel

- Canonical repository: `pyram1da/aeromirror`
- GitHub state: normal Release
- Draft: `false`
- Pre-release: `false`
- Latest updater-visible Release: `v0.12.19`
- Distribution label: public gallery/fullscreen and Bonjour-prerequisite
  review release
- Public asset count: exactly four
- Offline portable package: not published

Canonical `pyram1da/aeromirror` and configured legacy
`Nadejny/aeromirror` latest API routes both resolve to Release `375664260`
and tag `v0.12.19`. This proves updater-facing publication identity, not
installed behavior or iPhone visibility.

## Exact-tag verification

The clean annotated tag passed:

- managed x64 Release build and the complete receiver resilience suite,
  including non-cropping Photos policy, short Escape/release/rearm state,
  foreground/Z-order rules for the shell-owned control, asynchronous Bonjour
  assessment/repair, Windows argument quoting, updater selection, and typed
  readiness;
- focused Bonjour firewall contracts for strict service `ImagePath`, exact
  Private/UDP 5353/LocalSubnet/no-edge matching, narrow UAC command
  construction, and unsafe-argument rejection without host firewall mutation;
- exact production worker-lifecycle executable checks across eight scenarios;
- source-bound parser, crypto, transport, SETUP, and renderer contracts plus
  the production AES-128-CTR split/reset known-answer test;
- unchanged native core SHA-256
  `C217386CBC916F8889A9C03774390FE7EC7D8C7EE0B6F64358215CACEEB35118`;
- final native corresponding source with 147 archive entries and pinned input,
  patch, and provenance validation;
- exact 13-entry review payload, packaged-shell resilience, x64
  `0.12.19.0` Setup, byte-exact embedded shell/core/provenance, and all three
  non-installing Setup self-checks;
- clean exact-tag `release.ps1` packaging plus documentation link, strict
  UTF-8, version-surface, and whitespace checks.

The standalone live discovery-pipe harness was not run because the installed
receiver owned its machine-wide BLE status file. The delivered native core is
byte-identical to 0.12.18; long-idle iPhone visibility remains a physical row,
not an automated PASS.

The principal automated entry points were:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ReceiverResilience.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\BonjourFirewall.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeWorkerLifecycle.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeCoreContracts.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 `
  -Version 0.12.19 -SourceRef v0.12.19 `
  -RuntimePath .\artifacts\headless-runtime `
  -UpstreamRoot ..\upstream-uxplay-windows
```

These gates prove deterministic source behavior, packaging, installer logic,
corresponding-source completeness, and provenance integrity. They do not
replace a physical Windows/iPhone, focus/DPI, UAC, or real installed-update
test.

## Published assets

Every public asset was downloaded again after publication. Its byte size and
SHA-256 matched the final local release file, and GitHub's API digest matched
the same value.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `AeroMirror-Setup-0.12.19.exe` | 1,436,160 | `E9D3263B7AAFA5EA63E8CCCD102398877398CB93DDED27E62943F89301142C4B` |
| `AeroMirror-source-0.12.19.zip` | 2,242,979 | `FBCB803424888119A42A0329873466A83D543FF6A4C43C7A4DE518EA2D4C5368` |
| `AeroMirror-native-source-0.12.19.zip` | 828,196 | `C9C02493712627022D84928ED5B836D65DBA4AEAF177516C60C9DFD7D2F1DB63` |
| `SHA256SUMS.txt` | 297 | `026C91DA37E70E6DCDAC1A1065523752AC07ABB5781DEBF7227184566BF90BE6` |

`SHA256SUMS.txt` contains exactly the three non-checksum public assets. The
Release contains no unexpected fifth asset. Setup and the packaged shell are
x64 and report file version `0.12.19.0`.

The non-public exact-tag review payload has 13 entries, is 1,203,273 bytes, and
has SHA-256
`3A0C7EF6B6DAD5CD52F6245AE4B2916A90CFBDB1BC8AF51EFC0BB018F498914F`.

## Acceptance status

Accepted:

- exact annotated-tag source and clean-tag packaging;
- normal latest-channel visibility with `draft=false` and
  `prerelease=false`;
- canonical and legacy latest routes resolving to the same Release;
- exact four-asset set, checksum entries, GitHub API digests, and fresh public
  re-download byte sizes and SHA-256 values;
- managed, firewall-contract, native-contract, package, Setup,
  corresponding-source, documentation, provenance, and whitespace gates.

Pending:

- real installed update and same-version reinstall, including settings,
  receiver identity, shortcut choices, autostart, relaunch, and rollback;
- physical portrait and landscape Photos containment;
- titlebar control, short Escape, tray/Alt+Enter, stale-borderless recovery,
  focus, topmost, DPI, and multi-monitor behavior;
- explicit UAC firewall repair and verification that only the exact
  Private/UDP 5353/LocalSubnet rule is added;
- long-idle, lock/unlock, sleep/wake, network reconnect, and repeated iPhone
  browse visibility.

The publication run did not install or replace the local AeroMirror and did not
modify Windows Firewall. No physical acceptance is inferred from source,
package, API, or public-download checks.

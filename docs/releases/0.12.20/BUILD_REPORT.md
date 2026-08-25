# Build report — AeroMirror 0.12.20

Release `v0.12.20` was built from commit
`288b8976d413861ab77bf1721e20f047e0480952` and tree
`643be86b7ec95174fa9dbbf460fc7fd4b322951c`. Its annotated tag object is
`0e891a4903b381a339daff5a2ec3533f83e512e2`; the tag was created at
`2026-08-25T00:42:22+03:00`. GitHub Release `376224221` was published at
`2026-08-25T07:51:17Z`:

https://github.com/pyram1da/aeromirror/releases/tag/v0.12.20

The tag and all four public assets are immutable release history. Any
correction must use a later patch version; do not move this tag or replace an
asset under 0.12.20.

## Release channel

- Canonical repository: `pyram1da/aeromirror`
- Configured legacy repository: `Nadejny/aeromirror`
- GitHub state: normal Release
- Draft: `false`
- Pre-release: `false`
- Latest updater-visible Release: `v0.12.20`
- Distribution label: public native-viewer/setup review release
- Public asset count: exactly four
- Offline portable package: not published

Canonical and configured legacy latest API routes both resolve to Release
`376224221` and tag `v0.12.20`. This proves updater-facing publication
identity, not installed behavior or iPhone visibility.

## Exact-tag verification

The clean annotated tag passed:

- managed x64 Release build, focused Bonjour firewall contracts, and the
  complete receiver resilience suite;
- native host contracts for one framed viewer, one child video surface,
  idempotent fullscreen acknowledgement, Escape/caption/Alt+Enter ownership,
  aspect-ratio containment, lifecycle-hidden state, and Unicode runtime paths;
- source-bound parser, crypto, transport, SETUP, renderer, and production
  AES-128-CTR tests;
- all eight production worker-lifecycle executable scenarios for mirror,
  audio RTP, NTP, and HTTP;
- clean native builds and an extracted no-Git corresponding-source rebuild,
  all completing 57/57 targets and reproducing the 1,178,345-byte core with
  SHA-256
  `4336B9DBFCDE87123EC4796FE43FAA4F1952E27224932B3DD5E8FEAFBAD41832`;
- exact 13-entry review payload, packaged-shell Bonjour/resilience checks,
  x64 `0.12.20.0` Setup, byte-exact embedded payload, and Setup
  `/verify-runtime`, `/verify-shortcut-selection`, and
  `/verify-update-lifecycle` exit 0;
- source ZIP byte equality with a second `git archive v0.12.20`;
- 148-entry native corresponding source with the full pinned file/hash map,
  reviewed overlays, tagged inputs, matching provenance, and no `.git` entry;
- clean exact-tag `release.ps1` packaging, strict UTF-8, local documentation
  links, version/default surfaces, and `git diff --check`.

The principal automated entry points were:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\BonjourFirewall.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ReceiverResilience.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeHostContracts.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeCoreContracts.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeWorkerLifecycle.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 `
  -Version 0.12.20 -SourceRef v0.12.20 `
  -RuntimePath <reviewed-headless-runtime> `
  -UpstreamRoot <clean-pinned-native-materialization>
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
| `AeroMirror-Setup-0.12.20.exe` | 1,431,040 | `A4071C3B875484A154EFBE9EE11CB23CB1EE2C21A0840060A8ADDB97F923379D` |
| `AeroMirror-source-0.12.20.zip` | 2,269,686 | `0584430357F01A8B8AF5AF96B88F27B8CFE4EA6CEED15C40648609099B302E45` |
| `AeroMirror-native-source-0.12.20.zip` | 838,219 | `CF14918D83D609FAC3FF56D921684A509138A7ABEEA1441C691933730C9A613B` |
| `SHA256SUMS.txt` | 297 | `2A0F0BCE9980D9FA7017FBFB2D40D8C29753FDDC93E45A30DEE05D41B7CA2D4B` |

`SHA256SUMS.txt` contains exactly the three non-checksum public assets. The
Release contains no unexpected fifth asset. Setup and the packaged shell are
x64 and report file version `0.12.20.0`.

The non-public exact-tag review payload has 13 entries, is 1,198,476 bytes, and
has SHA-256
`373843BAFC83A0F7696832E11D7C61B8808E92489D6B1D209993DB3E547E0CAC`.
The packaged shell is 775,680 bytes with SHA-256
`6E9D13F8BF1D1E8C49B7CC2E0A6BD35A545694FE272C8B0F428B3617F34527DD`;
the embedded provenance file has SHA-256
`C210B373720AD950164F9FA429030641830E72FDDA4AC5E444DA31969F96160B`.

## Acceptance status

Accepted:

- exact annotated-tag source and clean-tag packaging;
- normal latest-channel visibility with `draft=false` and
  `prerelease=false`;
- canonical and legacy latest routes resolving to the same Release;
- exact four-asset set, checksum entries, GitHub API digests, and fresh public
  re-download byte sizes and SHA-256 values;
- managed, Bonjour, native-host/core/lifecycle, package, Setup,
  corresponding-source, documentation, provenance, and whitespace gates.

Pending:

- real installed update and same-version reinstall, including settings,
  receiver identity, shortcut choices, autostart, relaunch, and rollback;
- physical portrait and landscape Photos containment, including the reported
  horizontal inner Photos region inside a portrait viewer;
- caption fullscreen, one-press Escape, tray/Alt+Enter, Caption Close/minimize,
  focus, topmost, DPI, multi-monitor, and both taskbar policies;
- reproduction of the post-freeze report where locking the phone while AirPlay
  remains active and then closing the viewer allows later connection events to
  show that window again;
- explicit UAC firewall repair and verification that only the exact
  Private/UDP 5353/LocalSubnet rule is added;
- long-idle, lock/unlock, sleep/wake, network reconnect, and repeated iPhone
  browse visibility.

The publication run did not install or replace the local AeroMirror and did not
modify Windows Firewall. No physical acceptance is inferred from source,
package, API, or public-download checks.

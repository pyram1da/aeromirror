# Build report — AeroMirror 0.12.22

Release `v0.12.22` was built from commit
`a23f774098ab6b31954de6ad653cfc4d61289e3e` and tree
`4820bb32901c7bc9b6f763ab25cf7b5d7b7ad2e9`. Its annotated tag object is
`898194de03de6090a17906b26cca60c29e7392c5`; the tag was created at
`2026-08-31T15:11:10+03:00`. GitHub Release `379732527` was published at
`2026-08-31T12:19:11Z`:

https://github.com/pyram1da/aeromirror/releases/tag/v0.12.22

The tag and all four public assets are immutable release history. Any
correction must use a later patch version; do not move this tag or replace an
asset under 0.12.22.

## Release channel

- Canonical repository: `pyram1da/aeromirror`
- Configured legacy updater repository: `Nadejny/aeromirror`
- GitHub state: normal Release
- Draft: `false`
- Pre-release: `false`
- Latest updater-visible Release: `v0.12.22`
- Distribution label: public discovery/trust/fullscreen review release
- Public asset count: exactly four
- Offline portable package: not published

The anonymous canonical and configured legacy `releases/latest` API routes
both resolve to Release `379732527` and tag `v0.12.22`. The repository also has
the reviewed description, twelve discovery topics, and canonical latest-release
homepage. This proves updater-facing publication identity and repository
metadata, not installed behavior or iPhone visibility.

## Exact-tag verification

The clean annotated tag passed:

- the managed x64 Release build and complete receiver resilience, Bonjour,
  automatic-update, native-host, native-core, and worker-lifecycle suites;
- two clean native builds and an extracted no-Git corresponding-source rebuild,
  all completing 57/57 targets and reproducing core SHA-256
  `E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6`;
- runtime staging over 200 PE binaries, 148 dependency DLLs, 44 requested
  GStreamer features resolved to 27 plug-ins, and isolated self-tests from both
  ASCII and Cyrillic paths;
- the 149-entry prepared native corresponding-source archive, including its
  pinned patch, modified/protected-source, provenance, and build-input hashes;
- the exact review payload, x64 `0.12.22.0` Setup, byte-exact embedded inputs,
  package review, and Setup `/verify-runtime`, `/verify-shortcut-selection`,
  `/verify-update-lifecycle`, and `/verify-bonjour-recovery` exit 0;
- clean exact-tag release packaging, version/default surfaces, documentation
  links, and `git diff --check`.

The principal automated entry points were:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\ReceiverResilience.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\BonjourFirewall.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\AutomaticUpdate.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeHostContracts.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeCoreContracts.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NativeWorkerLifecycle.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\package-review.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 `
  -Version 0.12.22 -SourceRef v0.12.22 `
  -RuntimePath .\artifacts\headless-runtime `
  -UpstreamRoot <clean-pinned-native-materialization>
```

These gates prove deterministic source behavior, packaging, installer logic,
corresponding-source completeness, and provenance integrity. They do not
replace a physical Windows/iPhone, UAC, fullscreen, Photos, idle-discovery, or
installed-update test.

## Published assets

Every public asset was downloaded again without authentication after
publication. Its byte size and SHA-256 matched the final local release file,
and GitHub's API digest matched the same value.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `AeroMirror-Setup-0.12.22.exe` | 1,534,976 | `9A0D7B3E6A5598A2FB6996D235CA95BA6A5F93394BF1BABEDCC07CCE35A6D782` |
| `AeroMirror-source-0.12.22.zip` | 2,403,237 | `B9EE2A992ED2DF8D0A33C8F8723793CB6B75C335C0DA74167E34C406E17DF0FC` |
| `AeroMirror-native-source-0.12.22.zip` | 881,519 | `1CD812EED704F9D5D8E983F841DCD0569F6DFF0CD32A17A383D96489A0620DE7` |
| `SHA256SUMS.txt` | 297 | `BB76A6884F4135A5252DDD9A136ADB4C423A72A4E263F39E33A379C6433F6D5E` |

`SHA256SUMS.txt` contains exactly the three non-checksum public assets and each
entry matches the freshly downloaded bytes. The Release contains no unexpected
fifth asset. Setup is x64 and reports file version `0.12.22.0`.

## Acceptance status

Accepted:

- exact annotated-tag source and clean-tag packaging;
- normal latest-channel visibility with `draft=false` and
  `prerelease=false`;
- canonical and configured legacy latest routes resolving to the same Release;
- exact four-asset set, checksum entries, GitHub API digests, and fresh
  unauthenticated re-download byte sizes and SHA-256 values;
- managed, resilience, Bonjour, automatic-update, native-host/core/lifecycle,
  package, Setup, corresponding-source, documentation, provenance, and
  whitespace gates;
- reviewed GitHub description, topic set, and canonical homepage.

Pending:

- clean install, same-version reinstall, and the real update from public
  0.12.20, including settings, receiver identity, trusted-device state,
  shortcuts, autostart, relaunch, staging handoff, and rollback;
- first, repeated, and second-iPhone trust, cancellation, persistence, and
  Settings revocation;
- administrator approval and decline for the exact Bonjour service/firewall
  branch, stopped-service recovery, and uninstall persistence;
- one-hour and longer idle visibility, Windows sign-in, lock/unlock,
  sleep/wake, network reconnect, and repeated iPhone browse checks;
- Windows 10/11, caption fullscreen, one-press Escape, Alt+Enter, focus, DPI,
  multi-monitor restore, and portrait/landscape Photos containment;
- the user-dismissed viewer remaining hidden after the phone locks and the
  active AirPlay connection later reports loss.

The publication run did not install or replace local AeroMirror, execute Setup,
start or reconfigure Bonjour, or modify Windows Firewall. No physical
acceptance is inferred from source, package, API, or public-download checks.

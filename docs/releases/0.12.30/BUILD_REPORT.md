# Build report — AeroMirror 0.12.30

Date: 2026-09-06. The user explicitly authorized normal-channel review
publication after the local candidate handoff, with physical acceptance pending.
This report records the clean-tag public build, not the earlier frozen local
artifacts in [LOCAL_BUILD_REPORT.md](LOCAL_BUILD_REPORT.md).

## Immutable release identity

- Source commit: `de0545edacd4ad0515e4835f870c7bd73e3e42f5`.
- Source tree: `93cbf4c8dbcff158ecbe9530b8ea87add4cd6a4b`.
- Annotated tag: `v0.12.30`.
- Tag object: `21b4b948da487fed35e0ec51c9d7bd38b13e85fa`.
- Canonical repository: `pyram1da/aeromirror`, permanent ID `1324108899`.
- Historical compatibility marker: `Nadejny/aeromirror`, unchanged.
- GitHub Release: `383636375`, published `2026-09-06T15:50:53Z`.
- Channel: normal latest review Release; `draft=false`, `prerelease=false`.
- Public asset count: exactly four. No offline portable/runtime package.

[Published release](https://github.com/pyram1da/aeromirror/releases/tag/v0.12.30).

Source and the annotated tag were pushed before uploading a draft. Its four
uploaded asset names, sizes, API digests and curated body were verified before
publishing it as normal latest. The Release body matches the tagged release
notes. Source ZIP Git metadata identifies the exact source commit above.

This report is a separate post-publication documentation change on `main`.
It does not move the release tag or alter the tagged source archive. Published
tags and assets are immutable; any product correction needs a later patch.

## Clean-tag build and verification

The clean annotated tag at HEAD was packaged with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 `
  -Version 0.12.30 -SourceRef v0.12.30 `
  -RuntimePath .\artifacts\headless-runtime `
  -UpstreamRoot <pinned-prepared-native-source>
```

`release.ps1` rebuilt the shell, review payload and Setup and exported the
tagged shell source and prepared native corresponding source. No skip-build
option, dirty-tree exception or offline portable asset was used.

Passed in this publication run:

- The managed x64 build, exact 13-entry review payload, embedded-input
  verification and Setup `/verify-runtime`, `/verify-shortcut-selection`,
  `/verify-update-lifecycle`, `/verify-bonjour-recovery`; all non-installing
  Setup checks exited 0.
- `ReceiverResilience.Tests.ps1`, `RendererShowBoundary.Tests.ps1`,
  `UpdateTransport.Tests.ps1`, `AutomaticUpdate.Tests.ps1` and
  `BonjourFirewall.Tests.ps1` against the exact rebuilt public packaged shell.
  The child-window test leaves the inner video geometry unchanged while
  restoring the outer host; it does not manipulate a live phone viewer.
- `ControlWorkQueue.Tests.ps1`: production-source UI ownership and 250 disposal
  races. `NativeHostContracts.Tests.ps1` and managed Close identity/completion
  checks also passed.
- `NativeSessionClose.Tests.ps1`: production HTTP owner with loopback sockets
  and test media-cleanup callbacks/barriers; 50 Close/reconnect cycles, unchanged
  listener/port, exact target selection, peer retention, same-socket replacement,
  bounded mailbox, cleanup barrier, stop cancellation and no session-ID reuse.
  This is not actual iPhone RTP/audio or remote Screen Mirroring UI testing.
- `NativeCoreContracts.Tests.ps1`: source contracts and its existing positive
  AES-128 CTR NIST split/reset checks only. `NativeWorkerLifecycle.Tests.ps1`
  passed eight production-helper lifecycle scenarios.
- The newly packaged public native-source ZIP was extracted without Git
  metadata and rebuilt using its own included build inputs: 57/57 targets and
  the same pinned core hash. Both patches, upstream commits and 50 modified
  source hashes remain consistent with source provenance.
- After publication, `UpdateTransport.Tests.ps1 -CheckGitHub` against the public
  packaged shell passed 15 isolated scenarios plus the real anonymous metadata
  GET; it parsed public version `0.12.30`. It did not download or run Setup.
- Clean worktree packaging and `git diff --check`.

The unchanged native inputs also retain the local evidence: two native builds,
the earlier local ZIP's no-Git rebuild, exact production Qt surface checks,
runtime staging over 200 binaries / 148 DLLs, and the isolated Unicode-path
runtime self-test. See LOCAL_BUILD_REPORT.md for scope and deployment warnings
about translation catalogs and dxcompiler/dxil. The network Setup retains its
pinned GStreamer 1.28.1 runtime; the full 1.28.5 engineering stage is not public.

Local publication logs are kept under `artifacts/tests/public30-*`; anonymous
download evidence is under `artifacts/tests/public-download-0.12.30/`.
They are ignored build/test outputs, not additional public assets.

## Published assets

Anonymous verification completed at `2026-09-06T15:53:33Z`. Each downloaded
file matched both the final local release file and GitHub's `sha256:` digest.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `AeroMirror-Setup-0.12.30.exe` | 1,572,352 | `637229F49704539BCEA905AF879C17B54182C69641CA210B102A15B363992512` |
| `AeroMirror-source-0.12.30.zip` | 2,575,735 | `72C8AD28E7101D7E1A925F04FD3221D91156C1E93CC1277D822215D40C46FA82` |
| `AeroMirror-native-source-0.12.30.zip` | 918,558 | `993BEE922756EBF29A15DA801975FED167502F633D4DDA74D0BB0D406EBE371D` |
| `SHA256SUMS.txt` | 297 | `05CB6F167049D7437F6831A377932DD704DE673A30AC378EC1FD7122B61421D0` |

`SHA256SUMS.txt` lists exactly the three non-checksum assets, without duplicates,
and each entry matches the public downloaded bytes. No fifth asset is present.

Additional build identities, not separately uploaded assets:

| Build input/output | Bytes | SHA-256 |
|---|---:|---|
| Public packaged `AeroMirror.exe` | 837,632 | `94EE6AB75CB8E3B187239F86EF29E8E22D467DC8F36A0F81A15E6FA316E35BE6` |
| Review payload ZIP | 1,265,155 | `D6F343466E69C3DFF85BC6171C598435A52FFC554F6D799E4F6BDA019059C27F` |
| Pinned/rebuilt `uxplay-windows.exe` | 1,237,739 | `AA33FB22E5466910ECC29303DE6559BD47A02D1783D004C3169D45C7F6C436C0` |

The pre-publication local .30 Setup/payload/native ZIP were copied and verified
under `artifacts/local-candidates/0.12.30/` before the exact-tag rebuild. Their
older hashes are deliberately retained in LOCAL_BUILD_REPORT.md. The public
rebuild includes the publication documentation freeze; it is not a replacement
of an already published artifact.

## Anonymous public routes

All checked without an Authorization header:

- Canonical and historical `repos/<owner>/aeromirror/releases/latest` APIs and
  `repositories/1324108899/releases/latest` returned the same Release
  `383636375`, tag `v0.12.30`, non-draft/non-prerelease flags and exact assets.
- Canonical and historical GitHub `/releases/latest` HTML routes ended at the
  canonical `v0.12.30` Release page with HTTP 200.
- All four canonical asset downloads matched the table above. The historical
  `Nadejny/aeromirror` direct Setup download matched the same Setup bytes.
- The public Release body matched the tagged curated notes.

Older installed updaters may still reject canonical-owner asset URLs because
their compiled allowlist predates the repository rename. The latest channel
cannot repair that old code; a manual Setup download may be required. The .30
metadata/URL correction and successful live parse do not prove an installed
upgrade or staging handoff on every older version.

## Physical acceptance and preserved state

The user's earlier .28 success report remains a user-reported black-viewer
correction, not completion of the .30 matrix. Pending under
[TEST_PLAN.md](TEST_PLAN.md): actual iPhone disconnect/reconnect, viewer and
warning lock-then-close, replacement-session protection, untouched H.265/H.264
starts, mixed-window/game focus, gallery/fullscreen/minimize-restore,
Bonjour/UAC recovery, long-idle visibility and installed-update workflows.

No physical .30 acceptance is inferred from loopback results, lifecycle markers,
package gates, successful metadata or public downloads. This remains a review
release, not an accepted stable/1.0 milestone. The publication run did not install
the application, restart the installed receiver, change Bonjour/firewall, or
reset settings, receiver keys, trusted devices or logs. Frozen local baselines
and previous public releases remain untouched.

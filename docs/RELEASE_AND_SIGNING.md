# Release, update, and signing plan

## Supported Windows versions

The x64 build targets:

- Windows 10 version 1809 or newer;
- Windows 11.

Qt 6.10 officially supports Windows 10 1809 x64 and newer. The application
manifest declares Windows 10/11 compatibility, per-monitor DPI awareness, and
`asInvoker` execution. Windows 10 itself is outside Microsoft's normal
consumer support lifecycle, but it remains an explicit application target.

ARM64 and 32-bit Windows are not supported by this package.

## Upgrade behavior

All installed versions use the same per-user location and uninstall registry
identity:

```text
%LOCALAPPDATA%\Programs\AirPlayReceiverMvp
HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\AirPlayReceiverMvp
```

Running a newer setup performs an in-place update rather than creating a
second installed application. A same-version Setup performs a reinstall. The
current setup:

1. detects the installed version and selects a clean install, unattended
   update/reinstall, or explicit downgrade-confirmation path;
2. stops the existing shell and receiver processes;
3. moves the previous application directory to a temporary backup;
4. installs and registers the new files;
5. removes the backup only after success;
6. restores the previous directory if installation fails.

The **Download and install** click is the application's one confirmation. After
the exact asset name and SHA-256 digest are verified, AeroMirror launches Setup
with `/update` directly instead of asking a second Yes/No. Setup reads the
existing Start menu and desktop shortcuts before replacing files and runs
without an option form. Opening a newer Setup over an installed copy or
reinstalling the same version uses the same unattended path. Only the shortcuts
that existed are recreated, legacy names are migrated to the current
AeroMirror name, and the installed shell is launched after success. A clean
first install retains the interactive options. An older Setup never silently
downgrades a newer install.

User settings, logs, the persistent receiver key, and trusted-iPhone register
are stored separately under `%LOCALAPPDATA%\AirPlayReceiverMvp` and survive
updates and normal uninstall.

## GitHub update channel

Suggested repository description:

> Native-style AirPlay receiver for Windows 10/11, powered by UxPlay. Tray
> mode, PIN trust, quality presets, audio and safe updates.

The public repository slug is stored in `update-repository.txt`:

```text
Nadejny/aeromirror
```

The application uses GitHub's public `releases/latest` API. It does not need a
GitHub account or access token for a public repository. It displays the
release name and curated release body before the user decides whether to
update.

For a working automatic update, every GitHub Release must include:

- a semantic tag such as `v0.12.7`;
- a setup asset named exactly
  `AeroMirror-Setup-<MAJOR.MINOR.PATCH>.exe` for that release version;
- GitHub's SHA-256 asset digest;
- a short user-facing release body.

The updater accepts only an exact three-part numeric tag with an optional
leading `v`, for example `0.12.7` or `v0.12.7`. It rejects two-part,
four-part, suffixed, or otherwise malformed values. Do not rely on a tag such
as `v0.12.7-beta` being normalized into the public update channel.

The current application checks GitHub's `releases/latest` endpoint. A release
that should be found by installed AeroMirror clients must therefore be
published as a normal, non-draft GitHub Release. GitHub drafts and releases
marked **Pre-release** are not returned by this endpoint. If review builds
later need a separate prerelease channel, the application update protocol must
be changed before relying on GitHub's Pre-release flag.

GitHub's **Code** tab and **Releases** are separate views. The Code tab shows
commits from the selected branch (normally `main`), while a Release points to
a tag and stores its own downloadable assets. Publishing or replacing a
Release asset does not update `main`; push the reviewed release commit to
`main` before creating the matching tag and Release.

Each candidate must pass its versioned automated release gates and native
corresponding-source validation before publication, and publication still
requires explicit user authorization. A normal Release may be labelled as a
review candidate so installed clients can participate in physical testing,
but it must not be described as accepted until its versioned physical plan
passes. The 0.12.20 native-viewer/setup review build is the immutable normal
latest Release. Annotated tag `v0.12.20`, GitHub Release `376224221`, the exact
four-asset set, checksums, API digests, canonical/legacy latest routes, and
fresh public re-download equality pass; exact evidence is in
[`releases/0.12.20/BUILD_REPORT.md`](releases/0.12.20/BUILD_REPORT.md). Installed
update and the physical matrix remain governed by
[`releases/0.12.20/TEST_PLAN.md`](releases/0.12.20/TEST_PLAN.md). Publication
does not claim physical acceptance, and no tag or public asset may be moved or
replaced. Public `v0.12.19` remains immutable as the previous review release.
The published 0.12.17 Photos-presentation Release is superseded and remains a
review build until its physical matrix passes; its status is tracked in
[`releases/0.12.17/TEST_PLAN.md`](releases/0.12.17/TEST_PLAN.md). It must not
alter or replace any `v0.12.16` asset. The user explicitly authorized its
normal updater-visible review publication before the physical matrix; the
Release states that limitation and does not claim acceptance. Annotated tag
`v0.12.17`, Release `373934492`, the exact four-asset set, API
digests, canonical/legacy latest routes, and fresh public re-download equality
pass; exact evidence is in
[`releases/0.12.17/BUILD_REPORT.md`](releases/0.12.17/BUILD_REPORT.md). The
frozen local 0.12.13 candidate remains failed internal history and
must not be relabelled or silently replaced. The public 0.12.16 persistent
idle-discovery review Release uses
[`releases/0.12.16/TEST_PLAN.md`](releases/0.12.16/TEST_PLAN.md). Its managed
build and deterministic resilience contracts pass: the first renewal remains
ten minutes, later renewals recur every 20 minutes, active clients defer, and
only automatic renewals one and two may use the legacy process-restart
fallback. It reuses the frozen 0.12.15 native source, runtime, patch, and
provenance without modification. The exact 13-entry review payload, packaged-
shell resilience, versioned corresponding source, x64 `0.12.16.0` Setup,
byte-exact embedded inputs, all three Setup self-checks, and unattended
update/reinstall policy pass after the final documentation freeze. Installed
update and physical long-idle/iPhone visibility remain pending. The annotated
`v0.12.16` tag, prior normal GitHub Release, exact four public assets, API
digests, canonical/legacy latest routes, and fresh re-download byte checks pass;
exact evidence is in
[`releases/0.12.16/BUILD_REPORT.md`](releases/0.12.16/BUILD_REPORT.md).

The frozen 0.12.15 native-core candidate retains its complete native,
reproducibility, staged runtime, managed, discovery-pipe, exact package, and
Setup evidence under
[`releases/0.12.15/TEST_PLAN.md`](releases/0.12.15/TEST_PLAN.md), but is not
relabelled or published after the 0.12.16 correction. The untagged
0.12.10–0.12.15 candidates remain local history. Public `v0.12.19` is the
immutable normal latest review Release; `v0.12.18`, `v0.12.17`, `v0.12.16`,
`v0.12.9`, and `v0.12.7` remain immutable historical evidence. Historical
0.11 plans remain part of the evidence required before labelling the project
1.0.

The application downloads only after explicit confirmation, verifies the
asset against GitHub's SHA-256 digest, launches the setup, and then closes.
It does not accept a similarly named executable or fall back to the first
`.exe` asset: the filename must exactly match the three-part version parsed
from the Release tag.

Recommended assets:

```text
AeroMirror-Setup-<MAJOR.MINOR.PATCH>.exe
AeroMirror-source-<MAJOR.MINOR.PATCH>.zip
AeroMirror-native-source-<MAJOR.MINOR.PATCH>.zip
SHA256SUMS.txt
```

The current 0.12 review line uses a network installer. It downloads the unchanged,
pinned `uxplay-windows` runtime directly from the upstream GitHub Release and
checks SHA-256 before extracting it. Do not attach the offline portable/full
runtime until its complete per-file SBOM and corresponding-source set are
published.

The native source asset is a prepared corresponding-source tree. Both
AeroMirror patches are included separately and already applied. Its
`source-provenance.json` records the reviewed commits, patch hashes, modified
source hashes, build-input hashes, and expected core hash. The included build
script validates those values, generates the x64 `dnssd.lib` import library
from the verified `dnssd.def`, and does not require Git metadata in the
extracted archive.

## Release-note template

```markdown
## Summary

One short sentence describing the user-visible outcome.

## Should I update?

- Yes, if you experienced …
- Optional, if the new feature is not relevant and the installed version works.

## What changed

- Added: …
- Fixed: …
- Changed: …

## Known limitations

- …
```

Do not use raw commit lists as the primary description. Generated GitHub
notes can be appended below the curated section for maintainers.

## Signing options

### Recommended public path: Microsoft Store MSIX

Microsoft Store registration for individual developers is currently free.
The Store signs submitted MSIX packages with a Microsoft certificate and
provides Store-managed updates without SmartScreen download warnings.

The current custom EXE setup is not an MSIX package. Store distribution
therefore requires a separate packaging pass and clean migration rules
between unpackaged and Store installations.

References:

- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account

### Recommended GitHub path: SignPath Foundation

SignPath Foundation offers free signing for qualifying open-source projects.
The project must already be publicly released, use an approved open-source
license, contain no proprietary project components, be maintained and
documented, use MFA, and publish a code-signing policy.

Reference: https://signpath.org/terms.html

### Microsoft Artifact Signing

Artifact Signing is Microsoft's managed non-Store signing service. Current
Public Trust eligibility is limited to organizations in the USA, Canada, EU,
and UK, and to individual developers in the USA and Canada. It is therefore
not currently a direct option for an individual developer in Russia.

### OV/EV certificate

A traditional CA-issued OV certificate remains an alternative for direct
downloads, but it is paid and SmartScreen reputation still builds over time.
EV no longer provides an immediate SmartScreen bypass, so buying EV only for
that purpose is not justified.

## Recommended sequence

1. Populate the existing `Nadejny/aeromirror` repository and publish an
   unsigned beta with checksums and full GPL corresponding source.
2. Configure and test the GitHub update channel.
3. Add a code-signing policy and apply to SignPath Foundation.
4. Sign every executable and installer through the approved build pipeline.
5. Prepare an MSIX package and Microsoft Store listing as the main
   consumer-distribution channel.

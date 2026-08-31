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
   update/reinstall, or a refusal to replace a newer installed version;
2. stops the existing shell and receiver processes;
3. moves the previous application directory to a temporary backup;
4. installs and registers the new files;
5. removes the backup only after success;
6. restores the previous directory if installation fails.

For a manual update, **Download and install** is the application's one
confirmation. After the exact asset name and SHA-256 digest are verified,
AeroMirror launches Setup with `/update` directly instead of asking a second
Yes/No. Setup reads the existing Start menu and desktop shortcuts before
replacing files and runs without an option form. Opening a newer Setup over an
installed copy or reinstalling the same version uses the same unattended path.
Only the shortcuts that existed are recreated, legacy names are migrated to the
current AeroMirror name, and the installed shell is launched after success. A
clean first install retains the interactive options. An older Setup never
silently downgrades a newer install.

Setup 0.12.22 and later serialize install, update, and uninstall mutation for
the same Windows user. The transaction lock is acquired only when work begins,
the installed primary executable version is re-read under that lock, and
failure recovery keeps the lock through the bounded shell-launch confirmation.
Do not deliberately run an immutable pre-0.12.22 Setup concurrently with a
current Setup: historical binaries cannot know or join the new mutex. The
normal updater starts one exact Setup and does not create this unsupported
cross-version concurrency.

Automatic updates are opt-in and disabled by default. When enabled, AeroMirror
may check, download, and safely stage a verified newer Setup in the background,
including while mirroring is active, but that work must not stop or restart the
receiver.
The per-user staged manifest is protected with Windows DPAPI. A later safe
application start, before receiver/UI startup, revalidates the exact version,
name, path, age, regular-file status, and SHA-256 before launching the same
unattended `/update` transaction. Invalid staging fails open to normal receiver
startup; launch attempts and retry delay are bounded. Disabling automatic
updates removes known staged files.

User settings, logs, the persistent receiver key, and trusted-iPhone register
are stored separately under `%LOCALAPPDATA%\AirPlayReceiverMvp` and survive
updates and normal uninstall.

After the per-user transaction commits, Setup may request Windows administrator
approval for a separate best-effort Apple Bonjour service/firewall
configuration. Decline, timeout, or failure cannot roll back the application.
Because Bonjour is shared system software, the exact service-recovery policy
and narrow firewall rule intentionally remain after AeroMirror uninstall.

## GitHub update channel

Suggested repository description:

> Open-source AirPlay receiver for Windows 10/11. Mirror an iPhone screen and
> audio over Wi-Fi with tray mode, one-time PIN trust, a movable viewer, and
> verified updates.

Suggested topics:

```text
airplay airplay-receiver screen-mirroring iphone-mirroring iphone ios windows
windows-10 windows-11 uxplay bonjour mdns
```

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

The updater accepts only an exact `v`-prefixed three-part numeric tag, for
example `v0.12.7`. It rejects an unprefixed, two-part, four-part, suffixed, or
otherwise malformed value. Do not rely on a tag such
as `v0.12.7-beta` being normalized into the public update channel.

For a candidate version `X.Y.Z`, the accepted initial download URL is exactly
`https://github.com/Nadejny/aeromirror/releases/download/vX.Y.Z/AeroMirror-Setup-X.Y.Z.exe`.
The updater rejects user information, query/fragment text, a non-default port,
HTTP, another repository, or a differently named executable. Redirects are
followed manually with a bounded count and must remain HTTPS on the reviewed
GitHub release-asset host set. The response body is size-limited, written to a
new per-user staging file, flushed, and SHA-256 verified before it can become a
pending update. Manual and automatic modes share this exact downloader and do
not fall back to the first `.exe` asset.

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
passes. The local 0.12.22 candidate is authorized for publication only after
its exact clean-tag, native corresponding-source, package, Setup, and public-
asset gates pass. It has not yet been tagged or published; physical Windows/
iPhone rows remain pending and must be stated as such in the Release. The
0.12.21 candidate was never published and is superseded; never create a
`v0.12.21` tag or reconstructed asset set. Until 0.12.22 publication completes,
the 0.12.20 native-viewer/setup review build is the immutable normal
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
0.12.10–0.12.15 candidates remain local history. Public `v0.12.20` is the
immutable normal latest review Release until the pending 0.12.22 publication;
`v0.12.19`, `v0.12.18`, `v0.12.17`, `v0.12.16`, `v0.12.9`, and `v0.12.7`
remain immutable historical evidence. Historical
0.11 plans remain part of the evidence required before labelling the project
1.0.

Manual mode downloads only after explicit confirmation, verifies the asset
against GitHub's SHA-256 digest, launches Setup, and then closes. Opt-in
automatic mode may download and stage the same verified asset in the background
but may launch it only at a later safe application start. Neither mode accepts
a similarly named executable or falls back to the first `.exe` asset: the
filename and initial release URL must exactly match the three-part version
parsed from the Release tag.

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

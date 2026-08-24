# Security

## Supported versions

Only the latest published AeroMirror release receives security fixes.

Public `v0.12.18` is currently the latest published normal-channel review
release. The local 0.12.19 candidate does not expand the public support
statement until its immutable tag, exact assets, and public download
verification complete. Physical gallery/fullscreen and iPhone discovery rows
are reported separately and are not implied by publication.

## Reporting a vulnerability

Do not publish receiver keys, trusted-client records, settings files, or a log
that has not been reviewed for personal data.

For an ordinary crash or discovery failure, use the GitHub bug-report template
and follow `docs/TROUBLESHOOTING.md`.

For a vulnerability that could expose another user's device, pairing material,
or local files, first try GitHub's private
[Report a vulnerability](https://github.com/Nadejny/aeromirror/security/advisories/new)
route. Availability of that form depends on the repository's GitHub security
settings. If GitHub reports that private vulnerability reporting is
unavailable, do not put sensitive details in a public issue; contact the
repository owner through a private contact method listed on the owner's GitHub
profile.

Include the AeroMirror version, Windows version, affected network profile, and
the smallest reproducible description. Do not attach an active receiver key or
a real PIN.

Do not submit malformed or hostile protocol material to a public receiver or
public issue as a reproduction. Describe the affected boundary privately and
coordinate any executable reproducer with the maintainer and relevant upstream
project before sharing it.

## Scope

AeroMirror is a local-network receiver built on UxPlay. Reports about UxPlay,
GStreamer, Qt, Bonjour/mDNS, or bundled codec libraries may need coordinated
disclosure to their upstream maintainers as well.

Ordinary startup observes but does not mutate the machine-wide Bonjour service
or Windows Firewall. The 0.12.19 explicit repair action is offered only when
the exact Bonjour executable lacks the narrow Private/UDP 5353/LocalSubnet
inbound rule; it requires user confirmation and Windows UAC. Treat any
automatic mutation, Public/TCP/Any-address widening, executable-path
substitution, or deletion of unrelated firewall rules as a security defect.

The connection-loss continuity view may copy unobscured renderer client pixels
from the Windows desktop into process memory. It rejects capture when another
visible higher window overlaps the renderer and never intentionally writes the
bitmap to settings, logs, diagnostics, or temporary files. Treat capture of an
unrelated window, persistence of a mirrored frame, or inclusion of frame pixels
in a diagnostic package as a privacy vulnerability and use the private report
path above.

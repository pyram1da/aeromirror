# Third-party notices

This MVP combines a Windows launcher/settings shell with a patched
build of `leapbtw/uxplay-windows`. The patch adds a headless mode, direct
argument passing, stable native `argv` storage, and a non-streaming loader
compatibility check. AeroMirror 0.11 also adds a diagnostic video-size marker
used by the shell to adapt the stream window when the iPhone orientation
changes. The reviewed 0.12.4 libuxplay patch additionally logs the complete raw
AirPlay geometry header, selected GStreamer decoder/video sink, and compact
feedback-health capability/recovery markers. The raw auxiliary geometry pair
is diagnostic only and is not represented as a validated crop, pixel-aspect
ratio, or rotation signal. The AeroMirror 0.12.6 patch extension adds explicit
native HTTP listener lifecycle markers, checked same-port listener reset, and
honest removal of unsupported photo presentation feature declarations. The
0.12.7 hotfix restores client-managed typed RTSP `TEARDOWN` handling while
retaining upstream's `Connection: close` response header; it removes only the
additional 0.12.6 server-side disconnect flag. It also makes the headless
wrapper preserve externally supplied renderer arguments and does not modify
libuxplay's native audio renderer.

The untagged 0.12.8 patch extension adds recovery-scoped video-path telemetry
and a correlated Direct3D 11 presentation marker. That marker is used only to
prevent the managed continuity view from closing on control recovery or a
stale renderer window before a current frame reaches the swap chain. It does
not add crop, zoom, discovery repair, or a generic media-error bypass. The
managed-only AeroMirror 0.12.9 shell changes reuse that reviewed native patch
and core.

The AeroMirror 0.12.13 patch extension adds a bounded same-process DNS-SD
refresh protocol. It keeps the RAOP and AirPlay registrations coherent,
processes their asynchronous Bonjour callbacks on the native owning thread,
and preserves the listener PID and ports while repairing discovery. The
Bluetooth beacon helper, upstream revisions, dependencies, and their licenses
are unchanged; a physical IPv4 change still follows the full-restart path so
the separate helper receives the new address.

The 0.12.14 diagnostic patch extends the modified libuxplay
source set with `lib/raop.h` callback metadata, passive two-second numeric video
health summaries, and a signed epoch-protected timestamp mapper. The summaries
contain counters, ages, pipeline enums, and session/geometry generations only;
they contain no pixels, payloads, artwork, titles, file paths, or network URLs,
and they perform no automatic recovery. The immutable video-PTS retry change is
covered by source and arithmetic regressions, but no claim is made here that it
resolves the separately observed physical frozen-frame symptom.

The 0.12.15 native-core audit extends the same AeroMirror-authored patch across
libuxplay's worker lifecycle, sockets, HTTP/RTSP and mirror parsers,
setup/pairing, RTP/NTP, crypto, buffering, and audio/video renderer integration.
It adds the `worker_lifecycle` and `mirror_payload_parser` source pairs, pins
fixed protocol and payload limits, and makes partial construction and teardown
fail safely. The audited audio renderer is now an intentionally modified and
hashed source, including caps and GStreamer object-lifetime corrections. A
sender that has left video suspended may be resumed by the next complete,
decrypted, validated video access unit before that unit is rendered. These are
local changes under the existing GPL-covered corresponding-source contract;
the pinned upstream revisions, redistributed runtime, and third-party license
scope are unchanged. Physical confirmation of the frozen-frame repair remains
pending.

The internal 0.12.18 extension adds no dependency or protocol capability. It
adds a narrow wrapper command grammar and libuxplay GLib-owner dispatch for the
existing Direct3D 11 sink's documented fullscreen and scale properties. Scale
is equal on both axes and bounded to 100–500%. The managed shell may request a
deterministic centered portrait fill for the exact known Photos transport
canvas; no mirrored pixels are inspected, no media bytes are rewritten, and no
general rotation heuristic is introduced. The pinned upstream revisions,
redistributed runtime, and license scope remain unchanged.

The local 0.12.20 extension replaces the sink-owned fullscreen path with one
Qt-owned top-level viewer and one child video surface embedded through
`GstVideoOverlay`. Caption maximize, Escape, Alt+Enter, lifecycle events, and
the exact shell state setter share one native GUI-thread fullscreen owner. The
selected sink explicitly contains the full frame when supported; AeroMirror
does not request a crop/render rectangle and resets legacy scale to 100%.
Headless startup reports an absent Bonjour prerequisite with a stable marker
and exit code 20 instead of opening the upstream install dialog or registering
the bundled responder as a system service. The new private
`aeromirror_host_protocol.h` is included in corresponding source. These are
local GPL-covered changes; upstream revisions, redistributed runtime,
dependencies, and third-party license scope remain unchanged.

The AeroMirror 0.11 network review installer does **not** mirror the full
third-party runtime. During installation it downloads this unchanged upstream
asset directly from GitHub and verifies it before extraction:

- Asset:
  `https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip`
- SHA-256:
  `9D3A51C15FC9DB857351195E7EB7BBB21700D9AE25D936A54BCF8536B62CCA18`
- Exact upstream source:
  `https://github.com/leapbtw/uxplay-windows/tree/8cf3424b438424bc99a89155bd29a789f48a43c0`

Each published AeroMirror review release pairs Setup with that version's exact
AeroMirror and patched native corresponding-source archives. The current local
0.12.20 candidate would use `AeroMirror-source-0.12.20.zip` and
`AeroMirror-native-source-0.12.20.zip`; neither is public yet.

All previously published assets remain immutable. Every later release must use
its own versioned filenames rather than replace an earlier asset. The native
archive is a prepared source tree with both AeroMirror patches included
separately and already applied.
Its `source-provenance.json` records the reviewed patch, modified-source,
Bonjour-header, `dnssd.def`, and resulting-core hashes. The included build
script validates those inputs and generates the x64 `dnssd.lib` import library
from `dnssd.def`; no prebuilt import library is supplied as corresponding
source.

## UxPlay

- Project: https://github.com/FDH2/UxPlay
- Integrated release: UxPlay 1.73.6, commit
  `21eef8df25d91e12635c36d8176ad192725baca2`
- License: GNU GPL v3; parts under LGPL 2.1+, MIT, and other compatible
  licenses as documented by the project.
- Copyright: FDH2/UxPlay contributors and the original RPiPlay authors.

## uxplay-windows

- Project: https://github.com/leapbtw/uxplay-windows
- Source baseline: commit
  `8cf3424b438424bc99a89155bd29a789f48a43c0`
- Linked libuxplay commit:
  `437f37514257d9cb513ac7fbdee743b4da85852e`
- Runtime seed: release `2.0.0.1736`, x64 portable ZIP; the Bluetooth beacon
  and mDNS binaries are downloaded unchanged from this release.
- License: GNU GPL v3.
- The project combines UxPlay, Qt, GStreamer, mDNSResponder, and a Bluetooth
  beacon. Its own `LICENSE.rtf` is preserved in `core/`.

## GStreamer and plug-ins

- Project: https://gstreamer.freedesktop.org/
- Redistributed runtime version: 1.28.1, from the unchanged pinned
  `uxplay-windows` 2.0.0.1736 archive.
- Pinned GStreamer core SHA-256:
  `F2ED35F5089521F9C050530AB74B56C297CC48A190E6CDB80D5E370400ADFFA0`.
- Pinned wasapi2 plug-in SHA-256:
  `EACD2DC97902D575298E65C4167F26C5809D82B26EF60B4E134F08DC08F35619`.
  This GStreamer 1.28.1 plug-in contains the `continue-on-error` property
  selected by AeroMirror's default Windows audio argument.
- Engineering native build/staging input: GStreamer 1.28.5. This separate
  prefix is not redistributed by the public network installer.
- Predominant license: GNU LGPL 2.1+.
- Individual plug-ins and codec libraries have their own licenses. Anyone
  redistributing the binary bundle must audit the exact staged DLL set and
  provide the corresponding notices and source-code offers where required.

## Qt

- Project: https://www.qt.io/
- Runtime version: Qt 6.10.1, dynamically linked.
- Open-source Qt modules are generally offered under LGPL/GPL terms; exact
  obligations depend on the modules and distribution model.

## Apple mDNSResponder

- Project: https://github.com/apple-oss-distributions/mDNSResponder
- Licenses: Apache License 2.0 and/or BSD-style terms, depending on file.

## Important licensing consequence

The AeroMirror-authored shell and installer are GPL-3.0-or-later. The receiver
core and third-party runtime retain their own upstream license grants; this
notice does not attempt to relicense them. A proprietary closed-source product
cannot simply ship this core as an internal component without satisfying its
GPL source and redistribution requirements. Obtain legal advice before
commercial release.

This notice is an engineering summary, not legal advice.

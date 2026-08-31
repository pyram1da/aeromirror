# AeroMirror native build information

This file records the native executable prepared for the AeroMirror 0.12.22
release candidate. It keeps the pinned upstream/runtime inputs and the reviewed
stream-geometry, feedback, discovery, selected-pipeline, worker-lifecycle,
parser, setup/pairing, RTP/NTP, crypto, buffering, and renderer hardening from
the 0.11.1–0.12.15 line. Version 0.12.13 added bounded request-correlated
same-PID/same-port DNS-SD refresh; 0.12.15 expanded the native safety audit;
and 0.12.18 historically added sink fullscreen/scale commands used by the
managed Photos cover/fill path.

Version 0.12.20 replaces that fullscreen/Photos ownership model. One Qt
top-level viewer owns one child native video surface, and the selected sink is
embedded into that surface. Caption maximize, Escape, Alt+Enter, and the exact
shell setter share one native GUI-thread state transition and acknowledgement.
The sink contains the complete frame with no crop rectangle, and missing
Bonjour in headless mode is a stable exit-code-20 prerequisite rather than an
interactive install path. The source bundle contains the complete pinned
upstream trees with both reviewed patches separately and already applied.

Version 0.12.21 makes Bonjour error `-65563` terminal for the current DNS-SD
generation instead of retrying forever. The native core emits one failed,
prerequisite-unavailable, and degraded sequence, releases any partial RAOP/
AirPlay registration pair, cancels the retry source, and keeps the listener,
BLE helper, and receiver process alive. A later explicit discovery refresh
clears the latch and starts one new same-process registration generation.

Version 0.12.22 replaces fixed or command-line PIN protection with durable
per-device trust. For an unknown client, native code requests one four-digit
session PIN from the shell through a request-correlated stdin exchange and
uses it only for that connection's SRP state. PIN buffers are cleared after
use, trusted public keys are stored in the existing per-user register, and a
late cancel or packet from an older connection cannot affect a newer request.
The Qt viewer also removes caption/control styles while fullscreen and restores
the exact saved styles and geometry on Escape or lifecycle exit.

## Exact inputs

- `leapbtw/uxplay-windows`:
  `8cf3424b438424bc99a89155bd29a789f48a43c0`
- `leapbtw/libuxplay`:
  `437f37514257d9cb513ac7fbdee743b4da85852e`
- AeroMirror patches: `uxplay-windows-headless.patch` and
  `libuxplay-aeromirror.patch`
- Patched wrapper files: `src/airplayworker.cpp`, `src/main.cpp`,
  `src/mainwindow.cpp`, and `src/mainwindow.h`
- Patched libuxplay files: `aeromirror_host_protocol.h`,
  `aeromirror_log_protocol.h`, `lib/airplay_video.c`, `lib/crypto.c`,
  `lib/crypto.h`, `lib/dnssd.c`,
  `lib/dnssd.h`, `lib/fairplay_playfair.c`, `lib/http_handlers.h`,
  `lib/http_request.c`, `lib/http_request.h`, `lib/http_response.c`,
  `lib/http_response.h`, `lib/httpd.c`, `lib/logger.c`, `lib/logger.h`,
  `lib/mirror_buffer.c`,
  `lib/mirror_buffer.h`, `lib/mirror_payload_parser.c`,
  `lib/mirror_payload_parser.h`, `lib/netutils.c`, `lib/netutils.h`,
  `lib/pairing.c`, `lib/pairing.h`, `lib/raop.c`, `lib/raop.h`,
  `lib/raop_buffer.c`, `lib/raop_handlers.h`, `lib/raop_ntp.c`,
  `lib/raop_ntp.h`, `lib/raop_rtp.c`, `lib/raop_rtp.h`,
  `lib/raop_rtp_mirror.c`, `lib/raop_rtp_mirror.h`, `lib/utils.c`,
  `lib/worker_lifecycle.c`, `lib/worker_lifecycle.h`,
  `renderers/audio_renderer.c`, `renderers/video_renderer.c`,
  `renderers/video_renderer.h`, `uxplay.cpp`, and `uxplay_api.h`
- Architecture: x64, MSYS2 UCRT64
- Compiler recorded in the binary:
  `gcc.exe (Rev6, Built by MSYS2 project) 16.1.0`
- Qt: 6.10.1, built from the official MSYS2 package
  `mingw-w64-ucrt-x86_64-qt6-base-6.10.1-1-any.pkg.tar.zst`
- Qt package URL:
  `https://repo.msys2.org/mingw/ucrt64/mingw-w64-ucrt-x86_64-qt6-base-6.10.1-1-any.pkg.tar.zst`
- Qt package SHA-256:
  `1F7E95DFA1968910460087E8235C274BA5E14365E0F79EDC0C7672D951544D65`
- Qt package verified signing key:
  `5F944B027F7FE2091985AA2EFA11531AA0AA7F57`
- Redistributed upstream GStreamer runtime: 1.28.1. The pinned
  `libgstreamer-1.0-0.dll` SHA-256 is
  `F2ED35F5089521F9C050530AB74B56C297CC48A190E6CDB80D5E370400ADFFA0`;
  the pinned `lib/gstreamer-1.0/libgstwasapi2.dll` SHA-256 is
  `EACD2DC97902D575298E65C4167F26C5809D82B26EF60B4E134F08DC08F35619`
  and that plug-in contains the `continue-on-error` property used by the
  managed Windows audio selection.
- Engineering build/staging GStreamer input: 1.28.5. It is not the
  redistributed-runtime version recorded above.
- AeroMirror 0.12.22 compatible executable SHA-256 reproduced by two
  independent clean
  builds:
  `E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6`
- Materialized wrapper patch SHA-256:
  `EBC949F1943F1CF9AF9F299CCEE29A817647F3547788BAC0F525E4A76729FF81`
- Materialized libuxplay patch SHA-256:
  `B127564D17E8A752D1C16B7D52B13B89BAB88992D358C0A9935C0544F75B997E`
- Provenance pins 44 libuxplay sources and 49 patched sources in total. The
  0.12.22 corresponding-source archive contains 149 entries (145 files) and retains the
  complete prepared source and build inputs. Its extracted no-Git tree passes
  every pinned hash and completes a clean 57/57 rebuild to the reviewed core.
- Reproducible PE timestamp (`SOURCE_DATE_EPOCH`): `1786008050`
- Local checkout paths are remapped to `/src/uxplay-windows`, and debug
  sections are stripped from the released executable.

The `uxplay-windows` tree contains its exact `ucrt_x64_dependencies.txt`,
Python requirements, CMake files, Bluetooth beacon recipe, Bonjour build
script, packaging scripts, and verification scripts.

The AeroMirror patches add the headless launcher integration,
`--loader-test`, stable video-size and codec-header geometry markers, a
feedback-health capability and one-shot recovery markers, a one-shot selected
GStreamer decoder/videosink marker, and stable DNS-SD readiness markers.

The 0.12.22 pairing protocol emits only request identifiers and state names in
stdout diagnostics. PIN digits are never placed in process arguments or native
logs. The shell answers an exact live request through inherited stdin; native
code binds the secret, cancellation, SRP progress, and trust persistence to
the same connection and wipes transient PIN storage when that request ends.
Timeout, Escape, disconnect, malformed or stale SETUP, and mismatch with the
signature-verified client key return a negative admission result. Genuine
`AEROMIRROR_*` lines use a dedicated protocol emitter; ordinary native,
client-metadata, and HLS-language output flattens control bytes and neutralizes
marker tokens, and raw client identifiers are not logged.

In 0.12.20, `RendererHostWindow` is the only top-level video viewer and its
native child widget is the only GStreamer video surface. The sink is bound to
that child through `GstVideoOverlay` before playback, sink-owned fullscreen
toggle handling is disabled, and `force-aspect-ratio=TRUE` is set when
available. AeroMirror supplies no `gst_video_overlay_set_render_rectangle`,
crop, or hidden zoom request; legacy uniform scale state is reset to `1.0`.
This contains the complete Photos transport canvas and may letterbox when a
portrait outer window contains the observed `3840x2160` frame.

The wrapper accepts only
`AEROMIRROR_COMMAND video-fullscreen-set state=0|1` for the shell setter.
Caption maximize, Escape, Alt+Enter, lifecycle transitions, and that IPC path
share the Qt GUI-thread setter. It emits
`AEROMIRROR_VIDEO_FULLSCREEN` with requested and actual state, an
`applied|noop|unavailable` result, a monotonic generation, and the initiating
source. The new `aeromirror_host_protocol.h` is the private in-process contract
for renderer show, hide, and fullscreen-set messages; it is not a second
cross-process control surface. If Bonjour is missing in headless mode, the
wrapper emits `AEROMIRROR_BONJOUR_MISSING action=install-required`, exits 20,
and never opens the legacy install dialog or registers the bundled responder
as a service.

If Bonjour becomes unavailable while the process is already running,
`DNSServiceProcessResult` error `-65563` is not treated as an ordinary
transient registration failure. The current generation emits exactly one
`AEROMIRROR_DNSSD_PREREQUISITE_UNAVAILABLE` marker together with its failed
and degraded result, cancels automatic native retry, and leaves the TCP
listener and process intact. The explicit refresh command is the only native
operation that clears this generation latch.

Caption Close first leaves fullscreen and then calls `showMinimized()` while
ignoring destruction. It deliberately retains the active generation's
requested-visibility state. A minimized HWND remains `WS_VISIBLE`, including
under the shell's `WS_EX_TOOLWINDOW` taskbar policy, so managed tray recovery
can still identify it. `video_renderer_stop()`/`destroy()` exclusively perform
the 1-to-0 visibility compare-and-swap and HIDE; the next session can therefore
perform exactly one fresh SHOW.

The native HTTP listener reports initial/reset readiness with its actual port,
checks same-port reset binding, exits for full shell recovery when a reset
cannot restore the advertised port, and logs typed RTSP `TEARDOWN` as
client-managed instead of forcing the whole connection closed from the server.
The upstream `Connection: close` response header remains unchanged; the
hotfix removes only AeroMirror 0.12.6's additional server-side disconnect flag.
The 0.12.14 diagnostic candidate also emits one fixed, numeric,
content-free media-health summary every two seconds during an active mirror
session. It separates mirror VCL/config/action ingress, appsrc flow, sink and
Direct3D 11 Present progress, timestamp retries, monotonic ages, and pipeline
state under session/geometry generations. The classifier is observational: it
does not reset, resume, reconnect, crop, map pixels, or otherwise alter the
pipeline. Video timestamp retries now derive each candidate from the same
immutable remote timestamp through a signed, clock-epoch-protected offset;
audio retains an independent checked mapping. These source and arithmetic
properties are regression-tested, while the original physical frozen-frame
symptom remains unverified until a new-device log is captured.
The 0.12.15 audit then makes worker and socket lifetime states explicit,
validates bounded HTTP/RTSP and mirror media structures before use, makes
SETUP and crypto publication transactional, and hardens RTP/NTP endpoint and
packet validation. Video/audio renderer callbacks take synchronized retained
GStreamer references across teardown. A complete decrypted and validated
type-0 video access unit now performs one implicit resume when the sender has
left the stream suspended without the usual resume option, before that same
access unit is delivered. This narrow repair remains physically unverified.
Unsupported photo, slideshow, and photo-preload feature bits are no
longer advertised by this screen-mirroring-focused receiver. The
shell uses the backward-compatible video-size marker to adapt the renderer
window when the iPhone changes orientation. `--beacon-ipv4` binds BLE
discovery to the physical Wi-Fi/Ethernet IPv4 selected by the shell instead of
letting a VPN default route choose the advertised address. The launcher also
keeps beacon diagnostics separate from the stdout command protocol and
keeps receiver arguments alive for the full native startup call. Headless or
external `--uxplay` launches now return before the wrapper removes or replaces
`-vs`/`-fs`. The source packaging script compares canonical full-index patch
hunks with line endings normalized; wrapper result-object IDs are ignored only
because equivalent LF/CRLF checkouts can produce different blob IDs. Every
packaged modified and protected source, including
`libuxplay/renderers/audio_renderer.c`, is independently pinned by raw SHA-256
in provenance, while both reviewed patch files retain their exact pinned
SHA-256.

DNS-SD identity and TXT storage now belong to the full `dnssd_t` lifetime,
while each RAOP/AirPlay service-ref pair is rolled back and refreshed
idempotently as one generation. Registration callbacks and
`DNSServiceProcessResult` remain pumped on the owning GLib thread. The bounded
stdin protocol reports request, generation, PID, and both unchanged ports;
active clients defer refresh without listener teardown. The separate BLE
helper and its pinned binary are unchanged, so a real physical IPv4 change
still requires the shell's full-process restart path.

The one stored DNS-SD receiver name is capped to 50 complete UTF-8 bytes so a
six-byte-MAC RAOP instance (`MAC@name`) is no longer than Bonjour's 63-byte
service-label boundary. The same canonical name is used for AirPlay, RAOP, and
`/info`; blank input falls back to `AeroMirror`. Its protocol diagnostic logs
only byte lengths and truncation state, never the original name.

The resulting x64 PE imports `qt_version_tag_6_10`, does not import
`qt_version_tag_6_11`, and its `--loader-test` passed with the unchanged
GStreamer 1.28.1 runtime from pinned `uxplay-windows` release `2.0.0.1736`.
The same staged runtime passes `--self-test` with separate fresh registries
through both ASCII and Cyrillic application paths after all nine runtime path
variables moved to the wide Windows environment API.
The build gate checks the exact runtime archive and both DLL hashes above,
their embedded 1.28.1 version, and the wasapi2 property before staging. It
also verifies that the separate build prefix contains GStreamer 1.28.5. The
executable contains no
`.debug_*` sections or local checkout path.

For the 0.12.22 release candidate, two independently materialized clean source
trees each complete 57/57 targets and reproduce SHA-256
`E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6`.
The engineering runtime stage inspects 200 binaries and copies 148 DLLs; all
44 requested GStreamer features resolve to 27 plug-ins and its isolated
`--self-test` passes. The same core also passes `--loader-test` against the
unchanged pinned GStreamer 1.28.1/Qt 6.10.1 upstream runtime. These automated
results do not establish installed recovery or iPhone visibility. The
149-entry corresponding-source archive's extracted no-Git build reproduces
the same core hash. Physical 0.12.22 pairing, multi-monitor fullscreen,
installed recovery, and iPhone visibility checks remain pending until the
versioned test plan is completed.

The runtime builder accepts a separate Qt 6.10.1 prefix when the selected
MSYS2 tree contains another Qt version. Its dependency-closure pass uses an
ASCII temporary copy to accommodate MSYS2 `objdump` on Unicode workspace paths,
then publishes only the validated 200-binary result.

## Rebuild from this source bundle

The source files in this archive are already checked out at the pinned
upstream revisions and both AeroMirror patches are already applied. Do not run
`git checkout` or apply either patch again. Extract the exact Qt package listed
above to an isolated prefix. Extract the source archive under a short path such
as `C:\src\aeromirror`; deeply nested Downloads/workspace paths can exceed the
MinGW/CMake object-file limit. From an x64 Windows PowerShell prompt with
MSYS2 installed, open the bundled `uxplay-windows` directory and run:

```powershell
.\AeroMirror-build-inputs\build-compatible-core.ps1 `
    -UpstreamRoot . `
    -Qt610Prefix C:\inputs\qt610\ucrt64 `
    -MsysRoot C:\msys64
```

## Rebuild from separate Git clones

When starting from Git repositories instead of this prepared source bundle,
clone `uxplay-windows` with its `libuxplay` submodule, select the pinned
revisions, copy the patch from this bundle, and apply it once:

```powershell
git clone --recurse-submodules https://github.com/leapbtw/uxplay-windows.git
Set-Location .\uxplay-windows
git checkout 8cf3424b438424bc99a89155bd29a789f48a43c0
git -C .\libuxplay checkout 437f37514257d9cb513ac7fbdee743b4da85852e
git apply C:\path\to\uxplay-windows-headless.patch
git -C .\libuxplay apply C:\path\to\libuxplay-aeromirror.patch
C:\path\to\build-compatible-core.ps1 `
    -UpstreamRoot . `
    -Qt610Prefix C:\inputs\qt610\ucrt64 `
    -MsysRoot C:\msys64
```

The compatible-core script configures CMake with:

```text
-G Ninja
-DCMAKE_BUILD_TYPE=Release
-DCMAKE_EXPORT_COMPILE_COMMANDS=ON
-DNO_MARCH_NATIVE=ON
-DCMAKE_PREFIX_PATH=<isolated Qt 6.10.1 prefix>
-DQt6_DIR=<isolated Qt 6.10.1 prefix>\lib\cmake\Qt6
-UDNSSD_INCLUDE_DIR
```

The actual `dns_sd.h` used for the interface and AeroMirror's `dnssd.def` are
included in the source bundle. The Bluetooth beacon is reused unchanged from
the pinned upstream runtime.

The full offline runtime is not published by the current AeroMirror review
line. The installer downloads the unchanged, pinned upstream runtime asset
directly from the upstream GitHub release and verifies its SHA-256 before
installing it.

# Native receiver core and viewer host

The .30 candidate adds session-only Caption Close. A successful mirror SETUP
owns a unique 64-bit ID, carried unchanged by the mirror worker and native SHOW.
Qt submits that ID to a bounded HTTP-owner mailbox; only its connection is
removed. Completion is correlated to the request, after connection cleanup and
serialized pipeline reset. No listener or DNS-SD/BLE restart is requested.
The continuity warning sends an exact-ID stdin intent. The command reader posts
that ID to the private host HWND, where the same GUI handler checks it against
the displayed session and assigns the request ID. It does not capture a Qt
object across the detached command thread's lifetime.
Same-socket replacement and 50 loopback close/reconnect cycles pass; physical
iPhone disconnection and reconnect remain pending. The prepared source builder
resolves Git's real object store, including linked worktrees, while retaining
its isolated packaging index/objects. See the .30 release test plan.

The MVP patches `leapbtw/uxplay-windows` so its linked UxPlay engine can run
with `--headless`. In this mode the upstream Qt tray icon is not created; the
single visible tray belongs to `AeroMirror.exe`. "Headless" therefore means
that the native wrapper has no competing control UI or tray. In 0.12.20 it
still owns the one visible video viewer required for reliable window and input
handling.

Upstream pins:

- `leapbtw/uxplay-windows`: `8cf3424b438424bc99a89155bd29a789f48a43c0`
- `leapbtw/libuxplay`: `437f37514257d9cb513ac7fbdee743b4da85852e`

`dnssd.def` is generated from the export table of the bundled x64 `dnssd.dll`
and is used to create a MinGW import library for local builds.

The source changes are recorded in `uxplay-windows-headless.patch` and
`libuxplay-aeromirror.patch`. The latter adds stable video-size and DNS-SD
readiness log markers used by the Windows shell for window adaptation and
discovery diagnostics, plus raw AirPlay geometry, selected GStreamer pipeline,
and feedback-health capability/recovered markers used only for diagnostics and
bounded continuity. It also exposes process-scoped HTTP listener lifecycle
markers, rejects a failed or changed-port in-process reset, logs typed
`TEARDOWN` as client-managed instead of forcing the whole RTSP connection
closed. The upstream `Connection: close` response header remains; only the
additional AeroMirror 0.12.6 server-side disconnect flag is removed. The patch
also stops advertising unimplemented photo presentation features. Its
auxiliary geometry pair is not claimed as crop, PAR, or rotation
metadata. The launcher accepts `--beacon-ipv4 <numeric IPv4>`
before `--uxplay`, passes it to the Windows BLE helper, and forwards helper
diagnostics to stderr with an `AEROMIRROR_BLE` prefix so command-result
markers remain line-framed in stdout alongside ordinary UxPlay output. The
wrapper buffers complete helper lines and reports an unexpected start failure
or exit exactly once; an intentional helper stop is not reported as failure.

The 0.12.13 native slice adds request-correlated same-PID/same-port DNS-SD
refresh. RAOP and AirPlay registrations are treated as one generation, their
real callbacks are pumped on the GLib owner thread for the lifetime of the
service refs, and partial failures roll back both refs before bounded retry.
The headless wrapper reads bounded commands from its inherited redirected
stdin pipe and preserves an admitted refresh across transient internal GLib
loop resets. The unchanged BLE helper remains a separate legacy/diagnostic
path; physical IPv4 changes still use a full receiver restart so its advertised
address cannot become stale.

The 0.12.14 diagnostic candidate adds a passive,
content-free `AEROMIRROR_VIDEO_HEALTH` summary at a two-second cadence while a
mirror session is active. Its numeric counters distinguish mirror ingress,
codec configuration, appsrc flow, decoder/sink progress, and Direct3D 11
presentation proof; session and geometry generations keep adjacent reports
correlated. It records pause/resume options and timestamp retry outcomes but
does not inspect pixels, media payloads, paths, titles, or artwork, and it does
not trigger pause, resume, pipeline reset, crop, or reconnect actions. The same
slice remaps every video retry from one immutable remote timestamp using a
signed, epoch-protected clock offset, and preserves a separate checked audio
clock mapping. This makes the historical cumulative future-PTS retry defect a
code-level fix, but it is not evidence that the reported physical frozen-frame
case is fixed; that remains blocked on a device test with the new health lines.

The 0.12.15 native-core audit candidate expands the reviewed patch across the
receiver's worker, socket, parser, setup, pairing, RTP/NTP, crypto, buffering,
and renderer boundaries. New `worker_lifecycle` helpers make start rollback,
natural exit, one-owner join, self-stop deferral, and terminal join failure
explicit for HTTP, mirror, RTP, and NTP workers. New `mirror_payload_parser`
helpers validate packet sizes and H.264/H.265 NAL spans before conversion.
Accepted sockets use explicit blocking and timeout semantics, source endpoints
are pinned where the protocol has already established them, and incremental
HTTP/RTSP plus mirror payload processing now has fixed size and field limits.
SETUP, pairing, crypto, and stream construction reject incomplete state and
publish ownership only after successful startup.

The same audit pins the audio renderer as a patched source. It corrects audio
caps, clock/reference ownership, invalid-buffer handling, pending volume, and
bus-to-pipeline mapping. Video renderer access now uses synchronized retained
references across callbacks and teardown. If the sender marks video suspended
but then supplies a complete decrypted and validated type-0 access unit without
the usual resume option, that same unit performs one explicit implicit-resume
transition before delivery. This is a narrow protocol-state repair, not a claim
that the physical frozen-frame symptom is resolved; that still requires a
0.12.15 iPhone test.

The historical 0.12.18 extension added exact fullscreen and uniform-scale
commands to the existing headless pipe. That release let the managed shell
drive sink fullscreen and a Photos cover/fill transform. It is retained here
as release history, not as the current ownership model.

The 0.12.20 extension replaces that product path with one Qt-owned top-level
`RendererHostWindow` and one child native video surface. The selected
GStreamer sink is embedded into the child HWND through `GstVideoOverlay`; sink
fullscreen toggling is disabled, `force-aspect-ratio` is enabled when the sink
supports it, and no render/crop rectangle is supplied. Legacy scale state is
returned to uniform `1.0`, so the complete AirPlay transport frame is
contained rather than enlarged by cropping. A portrait outer window can
therefore show substantial letterboxing when Photos sends its observed
`3840×2160` canvas; without trusted inner-photo bounds, this is the safe
non-cropping behavior.

The native frame is also the sole fullscreen owner. The standard caption
maximize action, Escape, Alt+Enter, and the shell command
`AEROMIRROR_COMMAND video-fullscreen-set state=<0|1>` all reach one
idempotent Qt GUI-thread setter. Each accepted request reports the exact
acknowledgement
`AEROMIRROR_VIDEO_FULLSCREEN requested=<0|1> actual=<0|1>
result=<applied|noop|unavailable> generation=<uint64>
source=<ipc|caption|escape|alt-enter|lifecycle|initial>`. The shell consumes
that acknowledged state instead of inferring a toggle from delayed window
geometry. The private `aeromirror_host_protocol.h` defines only the in-process
viewer show, hide, session-close and fullscreen-set messages shared by the renderer thread
and Qt host.

Through .29, Caption Close is a minimize-equivalent, not the renderer HIDE
transition. It exits fullscreen, acknowledges normal state, and keeps the
active generation's requested-visibility flag set, so a repeated codec callback
cannot reopen the window against the user's action. Win32 keeps the minimized
HWND visible to enumeration even when the shell applies `WS_EX_TOOLWINDOW`;
managed tray restore/fullscreen can therefore target it. Renderer stop or
destroy alone changes requested visibility from 1 to 0 and posts HIDE, allowing
the following session to own the next SHOW.

When Bonjour is absent, the headless wrapper emits
`AEROMIRROR_BONJOUR_MISSING action=install-required` and exits with code 20.
It does not open an installer prompt or register a bundled per-user executable
as a machine-wide service. The original interactive upstream path remains
separate from AeroMirror's headless launch.

The 0.12.21 extension handles a different case: Bonjour existed when the
receiver started but later became unavailable. DNS-SD error `-65563` is
terminal for that registration generation. The core emits one failed marker,
one `AEROMIRROR_DNSSD_PREREQUISITE_UNAVAILABLE` marker, and one degraded
marker, releases the paired service references, and cancels automatic native
retry while leaving TCP, BLE, and the receiver process alive. An explicit
same-process discovery refresh clears the latch and begins a new generation;
normal native timers do not silently restart it.

The 0.12.22 extension makes first-use pairing request-correlated and
per-device. Native code asks the shell for a fresh four-digit PIN only for an
unknown client, accepts the answer only for the exact live request and
connection, and retains the resulting trusted public key in the existing
per-user register. PIN digits travel only over inherited stdin, are not logged
or placed in process arguments, and transient native buffers are cleared when
the SRP request ends. Cancellation, timeout, and late packets from an older
connection cannot clear or complete a newer pairing attempt.

The locally prepared 0.12.24 diagnostic extension traces the sender/core
geometry boundary without changing presentation. The `/info` handler emits a
single privacy-safe display tuple containing only model/configuration values,
feature flags, refresh limits, and overscan state. Sender header geometry keeps
its authoritative sender-side generation. Independently, a sink-pad probe logs
every actual CAPS event with local `caps_seq`, uses one first-buffer current-
caps snapshot only if no event was observed, and reads `GstVideoCropMeta` on
the first buffer after CAPS, on changes, and every 120 buffers. Missing CAPS is
evidence, but there is no claimed one-to-one mapping to a sender generation.
The probe does not map pixels, inspect payload content, introduce
caps/crop/scale elements, set a render rectangle, or resize the viewer. Two
independent clean builds reproduce its diagnostic executable SHA-256
`82B579693B60E9A1865E15BE314592838A0D3918DD1AFDF02565873213CE9397`;
physical iPhone evidence is still required before it can be treated as a fix.

The local 0.12.25 extension addresses the separately observed black initial
normal viewer at the child-HWND/D3D11 exposure boundary. The native lifecycle
owns the SHOW generation. Qt acknowledges READY only after the child is visible
with a nonzero client area; the first Present from the selected D3D11 sink then
posts that same generation using only atomic state and a nonblocking message.
A later GUI turn revalidates it; libuxplay retains only the selected host sink,
releases the renderer lock, requests the standard
`gst_video_overlay_expose()` redraw, and releases the reference.

Show and WindowStateChange coalesce a re-expose only for an already
acknowledged surface. WinIdChange uses bounded retry; libuxplay suppresses SHOW
while it rebinds every current overlay sink and starts a fresh generation only
afterward. HIDE invalidates older work. The host Present proof remains attached
when the independent failure-recovery pad probe is installed, and GUI-callable
surface APIs do not depend on the receiver logger surviving shutdown. Normal
connection SHOW raises once over ordinary windows without taking focus, but
stays behind external fullscreen content and defers initial automatic
fullscreen; it never sets topmost. The path does not block a GStreamer thread
on the GUI, call expose under the D3D11 device lock, resize or fullscreen the
viewer as recovery, set a render rectangle, crop, scale, inspect pixels, or
reset the pipeline. Only repeated untouched physical fresh starts, restore, and
foreground/fullscreen tests can accept the visible behavior.

Two independent clean builds and the extracted no-Git corresponding-source
rebuild reproduce core SHA-256
`F4824A375AFCD5593D1AA2E58547703E38F211380ACF71095CB8A08929ADB0E9`.
The pinned runtime passes static verification from an ASCII path and execution/
self-test from a Unicode path. Corresponding-source packaging uses an isolated
temporary Git index and object directory and exposes the checkout's real objects
only as alternates, so it does not write build-only objects into the repository.

Physical testing showed that the 0.12.25 redraw rendezvous was insufficient:
the untouched normal viewer could still remain black until fullscreen. The
0.12.26 extension therefore starts fresh mirror pipelines in `READY`, lets the
media callback claim its immutable renderer generation and select H.264 or
H.265, waits for Qt to validate the visible real child HWND, binds and commits
only that selected `GstVideoOverlay` sink for the exact generation, and only
then moves its pipeline to `PLAYING`. The other codec pipelines are quiesced
after the selected pipeline becomes ready.

For a real child-HWND replacement supplied by Qt, the selected pipeline moves to
`NULL` before the sink accepts the new handle, returns to `READY`, completes a
fresh SHOW/READY acknowledgement, rebinds and commits the selected sink, and
only then resumes
`PLAYING`. The first-Present/expose path remains a guarded redraw aid after
correct binding and may execute only when the lifecycle, READY, bound, and
Present generations all match. There is still no recovery resize, fullscreen,
crop, scale, render rectangle, or pixel inspection.

Start, stop, and destroy advance the renderer generation under one state owner.
Media callbacks carry their claimed generation in thread-local state and bus
watches carry immutable generation contexts. Old callbacks can drain against
retained objects but cannot publish a renderer, change a later pipeline's
state, or push a frame across a stop/start boundary; destroy waits for bus and
operation references before freeing each renderer. Two independent clean
builds and the extracted no-Git corresponding-source rebuild reproduce core
SHA-256
`FAA8A1575EAC7C26BA41DF09A81EB08E03DE05A621FA3C504289EA8E98DAB84A`.
The staged 200-binary runtime passes isolated bundle verification. Physical
untouched first-frame testing remains the acceptance gate.

The same boundary gives genuine `AEROMIRROR_*` lines a dedicated protocol
emitter. Ordinary UxPlay, libuxplay, client-metadata, and HLS-language output
flattens control bytes and neutralizes marker tokens before stdout, while raw
client identifiers are omitted. Native registration returns a boolean
admission result so cancellation, disconnect, malformed or stale SETUP, and a
verified-key mismatch fail closed.

The same extension makes fullscreen a genuinely borderless Qt/Win32 state:
caption, resize, minimize, maximize, and system-menu styles are removed on
entry, while Escape and lifecycle exit restore the exact saved styles and
normal geometry. The ordinary viewer remains framed, movable, and resizable.

The receiver's shared AirPlay identity is canonicalized to at most 50 UTF-8
bytes at a complete character boundary. This keeps the six-byte-MAC RAOP label
(`MAC@name`) within Bonjour's 63-byte service-label limit while AirPlay, RAOP,
and `/info` expose the same name. A blank name falls back to `AeroMirror`.
The `AEROMIRROR_SERVICE_NAME` diagnostic reports only input/registered byte
lengths and truncation state, not the original name.

The wrapper now returns before its legacy settings UI can remove or replace
externally supplied `-vs` and `-fs` arguments in headless/`--uxplay` mode. The
0.12.15 libuxplay patch intentionally includes the audited
`renderers/audio_renderer.c`; every modified or added source is individually
hashed in `source-provenance.json`.

## Compatible runtime and build inputs

The local 0.12.27 follow-up replaces the base video QWidget with a dedicated
external-surface widget. Qt owns its HWND/events/layout but must not erase or
backing-store-paint its pixels; paintEngine returns null and PaintOnScreen is
retained on Windows. The production widget contract is tested with hidden
Windows HWNDs and pinned Qt 6.10.1. This is not physical first-frame acceptance.
The 0.12.26 normal-viewer and mixed-window placement reports remain FAIL.

SHOW now verifies ordinary-window order. Only when the ordinary raise leaves
another ordinary window above the viewer may one synchronous topmost/demotion
transaction run, guarded again against external fullscreen foreground content.
Demotion is unconditional, focus is not requested, and no topmost state remains.
Current core SHA-256:
`B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F`.
All runtime/dependency and gallery-negotiation inputs remain unchanged.

The headless executable must be built against Qt 6.10.1 so it can load with
the unchanged runtime from pinned `uxplay-windows` release `2.0.0.1736`.
The exact official MSYS2 package is:

- file: `mingw-w64-ucrt-x86_64-qt6-base-6.10.1-1-any.pkg.tar.zst`
- URL:
  `https://repo.msys2.org/mingw/ucrt64/mingw-w64-ucrt-x86_64-qt6-base-6.10.1-1-any.pkg.tar.zst`
- SHA-256:
  `1F7E95DFA1968910460087E8235C274BA5E14365E0F79EDC0C7672D951544D65`
- verified signature key:
  `5F944B027F7FE2091985AA2EFA11531AA0AA7F57`

Extract the package into an isolated directory and pass its `ucrt64`
directory as `Qt610Prefix`. Do not install, update, or downgrade packages in
the normal MSYS2 prefix for this build.

The public network installer reuses the unchanged runtime archive from
`uxplay-windows` release `2.0.0.1736`. That archive contains GStreamer 1.28.1,
including `libgstwasapi2.dll` with its `continue-on-error` property. The exact
archive and two DLL hashes are pinned in `UPSTREAM.lock` and
`source-provenance.json`. GStreamer 1.28.5 is the separate engineering
build/staging prefix; it must not be described as the redistributed runtime.

## Local build outline

1. Install MSYS2 UCRT64 with the packages listed by upstream
   `ucrt_x64_dependencies.txt`, except that the Qt build input is the isolated
   package above.
2. Check out the pinned `uxplay-windows` and `libuxplay` commits.
3. Apply `uxplay-windows-headless.patch`, then apply
   `libuxplay-aeromirror.patch` inside the `libuxplay` submodule.
4. Provide the Bonjour SDK header and import library. `dnssd.def` can generate
   the x64 MinGW import library from the redistributed `dnssd.dll`:

   ```powershell
   dlltool -d dnssd.def -D dnssd.dll -l dnssd.lib
   ```

5. Run the isolated compatible-core build without bootstrap or package
   updates:

   ```powershell
   .\build-compatible-core.ps1 `
       -UpstreamRoot C:\src\uxplay-windows `
       -Qt610Prefix C:\inputs\qt610\ucrt64 `
       -MsysRoot C:\msys64
   ```

   The script verifies Qt 6.10.1 and the build-prefix GStreamer 1.28.5,
   configures a clean
   `out\headless-x64-qt610` directory, builds with Ninja, and rejects anything
   except an x64 PE importing `qt_version_tag_6_10` and not
   `qt_version_tag_6_11`. It also pins `SOURCE_DATE_EPOCH=1786008050` so the
   PE timestamp and resulting executable hash are reproducible.
6. Run `build-headless-runtime.ps1` only for a local engineering runtime. Pass
   both the extracted upstream runtime and its original pinned archive. The
   script verifies the archive SHA-256, the embedded GStreamer 1.28.1 core and
   wasapi2 DLL hashes/version/property, then deploys Qt and GStreamer 1.28.5
   from the selected MSYS2 prefix. Its manifest records both contracts and
   their distinct purpose. The public installer
   instead downloads the unchanged pinned runtime from release
   `2.0.0.1736`, verifies it, and runs `--loader-test` before installation.

   ```powershell
    .\build-headless-runtime.ps1 `
        -UpstreamRoot C:\src\uxplay-windows `
        -OriginalRuntime C:\inputs\uxplay-windows-2.0.0.1736 `
        -OriginalRuntimeArchive C:\inputs\uxplay-windows.zip `
        -HeadlessExecutable C:\src\uxplay-windows\out\headless-x64-qt610\uxplay-windows.exe `
        -MsysRoot C:\msys64 `
        -QtPrefix C:\inputs\qt610\ucrt64
    ```

   `-QtPrefix` is optional when the required Qt 6.10.1 deployment tools already
   live under the selected MSYS2 prefix. Dependency inspection runs against an
   ASCII temporary copy because the MSYS2 `objdump` build cannot reliably open
   every Unicode Windows path; only the validated result is moved back to the
   normal artifact directory.
7. Run upstream `scripts/verify-bundle.ps1` against the staged runtime.

The runtime builder deliberately stages the hardware H.264/H.265 decoders for
both D3D11 and D3D12 because the latency profiles select them explicitly.
The resulting core's `--loader-test` has also passed against the unchanged
runtime from release `2.0.0.1736`.

Runtime paths are installed into the process environment with the wide Windows
API. A fresh-registry `--self-test` against the same staged bytes passes from
both ASCII and Cyrillic application paths. Setup uses `--loader-test` against
the separately pinned upstream runtime before committing installation; the
broader self-test remains a staged-bundle gate.

For the 0.12.22 release candidate, two independently materialized clean source
trees complete 57/57 targets and reproduce core SHA-256
`E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6`.
Staged inspection covers 200 binaries and copies 148 DLLs; all 44 requested GStreamer
features resolve to 27 plug-ins, and the isolated runtime `--self-test` passes.
The same core passes `--loader-test` against the unchanged pinned GStreamer
1.28.1/Qt 6.10.1 upstream runtime. Provenance pins 49 patched sources in total,
including `libuxplay/aeromirror_host_protocol.h`. The 149-entry corresponding-
source archive's extracted no-Git tree validates every pinned input and
completes 57/57 to the same core hash. These automated results do not
establish installed recovery, first-device trust, fullscreen restoration, or
iPhone visibility; those physical 0.12.22 checks remain pending.

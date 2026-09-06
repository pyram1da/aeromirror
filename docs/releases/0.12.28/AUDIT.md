# AeroMirror 0.12.28 — audit work record

Status: confirmed-defect corrections and local build gates completed.
No publication or installation authorized by this work.

## Scope

Audit all first-party C# source and installer entry points, native wrapper and
patched libuxplay integration, test coverage and release/build boundaries.
Third-party source is reviewed at the relevant integration boundary, not
claimed to have received a complete independent security audit.
Preserve the dirty 0.12.23–0.12.27 work and the accepted gallery behavior.

## Physical baseline

The user reports that 0.12.27 still opens black. The installed shell is
0.12.27.0 and installed core hash matches the exact local candidate
`B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F`.
The September 6 log records initial SHOW/READY at 12:13:17, H.265
998x2160 CAPS, sink buffers and Present callbacks before fullscreen at
12:13:52. These markers did not establish visible output. Treat the normal
first-frame row as FAIL; Qt surface flags alone were insufficient.

## Work sequence

1. Verify actual window/sink ownership and first-frame presentation, including
   internal GStreamer child windows and the interaction with shell placement.
2. Inspect shell lifecycle, callback concurrency, persistence, updates, network
   checks, forms and resource disposal. Record concrete findings separately
   from maintainability concerns and unproven hypotheses.
3. Refactor confirmed defects behind narrow behavior tests; avoid cosmetic
   whole-tree rewrites or unverified geometry workarounds.
4. Rebuild and package only after the new regression checks pass. Preserve
   explicit physical acceptance gaps.

## Confirmed findings and corrections

### A28-1 — SHOW callback moved the sink child outside its parent (high)

At 13:00 on the user's untouched black session, the outer host was at
1327,118 with outer size 563x1223 and client size 547x1184. Its Qt surface was
at screen 1335,149 with the same client size. The visible `GSTD3D11` child was
instead at screen 2662,267 with size 563x1223: relative to its Qt parent, it
had received the outer host's desktop origin and outer size. Frames continued
through sink/Present; the child lay beyond its parent's clipping area.

`OnRendererWindowShowEvent` checked the process and title, but
`EVENT_OBJECT_SHOW` also includes the sink child named `Direct3D11 renderer`.
The title predicate accepted it. `TryApplySavedStreamWindowPlacement` then
sent desktop coordinates to a parent-relative `SetWindowPos` call.

Correction: top-level root + no-WS_CHILD validation in the SHOW hook, cached
and enumerated lookup, saved placement, direct outer-bounds setter and aspect
fit. Ownership is checked where mutation occurs, not only at the caller.
No native pipeline or gallery change is required.

The same executable harness calls the exact private production handler against
real hidden HWNDs without constructing a receiver or loading its settings:
installed .27 displaces the child; .28 leaves it unchanged and still restores
the outer host. Owned top-level windows remain accepted. On September 6 the
user then reported that local .28 works. Record that black-screen follow-up
separately from the still-unrecorded full physical codec/window matrix.

### A28-2 — late manual-update callbacks outlived the settings form (medium)

The worker checked IsDisposed, then called BeginInvoke. A disposal between the
check and enqueue could throw; the catch handler repeated the same unsafe
enqueue. Download ownership was a path field written/read by worker and UI,
including FormClosed, without a consistent handoff.

Correction: ControlWorkQueue claims/cancels pending work once, catches enqueue
lifetime races, cancels on handle destruction/disposal, and runs callbacks on
the owner thread. The selected release is captured before download. Unclaimed
downloads are cleaned on cancellation; the UI launch routine owns an accepted
path and transfers it only after Process.Start succeeds. The shared pending
path field and duplicate UI-enqueue error path are removed.

Behavior checks cover normal delivery, missing/recreated handles, disposal
before/after enqueue, late download results and 250 concurrent closure races.

### A28-3 — window policy was acknowledged after failed API calls (medium)

The shell ignored taskbar-style/frame and Z-order call results but marked
rendererPolicyApplied=true, preventing a retry for unchanged settings.
The style helper now verifies relevant extended-style bits and frame refresh;
the policy is acknowledged only after both operations succeed. Failures remain
pending. Hidden real-HWND checks cover valid, idempotent and destroyed targets.
This is not proof of the separate physical mixed-window Z-order requirement.

## Coverage and remaining refactoring

This pass traces window/event ownership and native selected-sink startup in
detail, and reviews cross-module callback/lifecycle, update/download ownership,
settings persistence, network/service boundaries, UI disposal, and build/
installer contracts. The inventory was 31 existing application C# files plus
the installer; one focused ControlWorkQueue source was added. Existing dirty work from
earlier candidates is preserved.

- Detailed correction paths: ReceiverContext.Rendering, NativeMethods,
  SettingsForm, native RendererHostWindow, video_renderer integration and the
  official GStreamer Win32 sink implementation.
- Additional reviewed boundaries: Program/AppVersion; AppSettings normalization
  and atomic persistence; ReceiverContext lifecycle, HTTP reset, pairing,
  lost-connection, diagnostics, automatic updates and Bonjour integration;
  network assessment/recovery; UpdateService and AutomaticUpdateService;
  UI controls/theme/overlay ownership; installer and package validation paths.
- Native/GStreamer review is an integration review, not a complete independent
  audit of every inherited third-party source file. Full post-split, line-by-line
  decomposition of the large receiver and installer remains staged TODO work.
- The existing caption-close action still minimizes the native viewer and
  suppresses loss UI; it does not implement a remote AirPlay disconnect.
  Treat the earlier requested close-to-disconnect behavior as unresolved product
  work, not as fixed by this presentation patch.
- Release metadata Check still uses WebClient defaults. Add explicit metadata
  size/time budgets in a separately tested follow-up; the installer body already
  has explicit size/time/redirect limits. No bypass of hash checks was found in
  the reviewed handoff path.
- Repeated broad using-directive blocks and large partial-class state owners
  remain maintainability debt. Avoid mixing their mechanical cleanup with the
  accepted gallery/native state machine or calling it a behavioral fix.

No sweeping repository moves, new native patches, service changes, user-profile
resets, installation or GitHub mutations were performed. The executable test
results and final artifact hashes are recorded in LOCAL_BUILD_REPORT.md.

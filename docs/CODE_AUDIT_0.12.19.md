# AeroMirror code audit for 0.12.19

Date: 2026-08-24

This is a maintainability and correctness audit of the authoritative Windows
shell, installer, build/release scripts, native integration inputs, and tests.
It is not a proposal to rewrite the application. The preferred sequence is to
fix observed user-facing failures, add behavior-level tests, and then extract
one independently testable responsibility at a time.

The baseline contains 23 tracked C# files under `src/`. The current 0.12.19
worktree also contains five new focused source files:
`Network/BonjourFirewallService.cs`,
`Receiver/ReceiverContext.BonjourFirewall.cs`,
`Receiver/ReceiverContext.RendererControls.cs`,
`Receiver/RendererPresentationPolicy.cs`, and
`UI/RendererFullscreenButtonForm.cs`. They are included below. The installer
is audited separately because it is compiled as a different assembly.

## Executive result

No P0 issue was found. Six P1 issues were found during the two static review
passes and corrected before the final release gate:

1. Timer-sampled Escape was replaced by a bounded low-level keyboard event hook
   installed only during actual fullscreen. Capture additionally requires the
   renderer PID/root to own foreground, and the hook never consumes the key.
2. Windows command-line arguments now use the standard backslash/quote
   algorithm, with round trips through `CommandLineToArgvW`. Raw
   `AdvancedArguments` remains an explicit expert override.
3. Unknown network state now has its own settings guidance instead of falling
   through to private-network copy.
4. The first stale-borderless design could send a non-idempotent automatic
   fullscreen toggle and accidentally re-enter fullscreen. It now keeps the
   explicit exit control available and sends no speculative toggle.
5. The first overlay design could remain topmost over an unrelated foreground
   application, while a later no-Z-order variant could fall behind the renderer.
   Visibility now always requires renderer/overlay foreground; guarded refresh
   restores the overlay above the renderer, using the non-topmost band for a
   normal framed window and topmost only for the configured/fullscreen exit
   state.
6. The first explicit Bonjour repair path could wait for UAC and `netsh` on
   the WinForms thread. Assessment and repair now run on worker threads and
   marshal only their cached result through the existing UI supervision pass.

The worktree also forces Photos/media presentation to a non-cropping 100% scale
when no trusted content rectangle exists and uses `IsReceiverReady` rather
than parsing localized status text. Static inspection and automated contracts
cannot certify physical iPhone discovery, real key/focus behavior, DPI
placement, or visible pixels; those acceptance rows remain separate.

## What was safe to clean in 0.12.19

The following independent cleanup was completed without changing product
behavior:

- `UpdateService.Check` now selects the one exact
  `AeroMirror-Setup-<version>.exe` asset directly. The redundant
  `IsCompatibleInstallerName` second filter was removed, and a focused
  resilience assertion protects the exact-name contract.
- Unused copied `using` directives were removed only from small files in
  `Application`, `Updates`, `UI/Controls/NamedValue.cs`,
  `Network/NetworkProfileInfo.cs`, and `Properties/AssemblyInfo.cs`.
- The isolated cleanup passed the managed Release build,
  `ReceiverResilience.Tests.ps1`, and `git diff --check` at the time it was
  made. The combined 0.12.19 exact-tag and publication gates subsequently
  passed; physical acceptance remains separate.

Do not broaden this cleanup into a field move or a partial-class split while
the gallery/fullscreen and discovery fixes are being stabilized.

## File-by-file C# inventory

### Application and configuration

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `src/Application/AppVersion.cs` | Reads the managed assembly version and exposes display/update forms. | Cohesive. Keep this facade, but later make the release version originate from one build input instead of editing assembly, installer, and script defaults independently. |
| `src/Application/Program.cs` | WinForms entry point, single-instance mutex, and startup wiring. | Cohesive enough. The small-file import cleanup is complete; no split is justified. |
| `src/Configuration/AppSettings.cs` | Defaults, mutable setting fields, load/save escaping, migration, normalization, receiver identity files, and placement values. | Oversized and change-prone: adding one setting requires coordinated edits in defaults, copy, load, save, normalization, and often migration. Keep current invariants for 0.12.19; later separate serialization/migrations from an immutable settings snapshot and test malformed/old files directly. |
| `src/Properties/AssemblyInfo.cs` | Product metadata and assembly/file version. | Cohesive. Version is a release/build invariant, not a runtime preference; remove duplication through the release pipeline rather than `settings.ini`. |

### Interop and network

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `src/Interop/NativeMethods.cs` | Win32 structs, constants, P/Invokes, and small safe wrappers. | Centralizing interop is reasonable. The new hook/ancestor/DPI declarations are narrowly consumed by renderer controls. `GetClassName`, `ShowWindow`, and `SetForegroundWindow` have no production call sites and are candidates for a later mechanical deletion after the complete test run. |
| `src/Network/NetworkProfileInfo.cs` | Mutable result bag for physical profile detection. | `IsKnown`, `IsPublic`, `Category`, and the stored `Signature` can contradict one another. Replace later with an immutable typed category/result and derive booleans/signature; do not change the fail-closed policy in a cleanup patch. |
| `src/Network/NetworkSafety.cs` | Launches PowerShell, selects the physical adapter/profile, parses JSON, and returns the network result. | Process adapter and pure parsing/selection are coupled. Extract a pure parser with cases for malformed JSON, unknown category, no IPv4, multiple physical adapters, and overlay profiles. PowerShell timeout and physical-only selection are safety invariants, not user settings. |
| `src/Network/BonjourFirewallService.cs` | New 0.12.19 assessment and explicit UAC repair for a narrowly scoped private-subnet Bonjour mDNS rule. | The matcher is deliberately pure and the one mutation is explicit, which is the right boundary. An unused future uninstall/delete path was removed. Keep the exact executable, UDP 5353, Private profile, LocalSubnet, and no-edge contract as product/security invariants. Focused tests pass; real non-admin/UAC/firewall behavior remains physical acceptance. Never auto-mutate the firewall on ordinary startup. |

### Receiver coordinator and partials

The eight `ReceiverContext` files currently total roughly 6,700 lines. The root
partial owns about 150 fields across process lifecycle, discovery, network,
renderer geometry, presentation, lost-connection UI, logging, and update
state. The partial split improves navigation but does not provide ownership:
all partials can mutate all fields. This is a P2 architecture risk, not a reason
for a release-cycle rewrite.

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `src/Receiver/ReceiverContext.cs` | Application coordinator, tray/menu construction, timers, high-level state, and shared fields/properties. | Keep as the composition root. Replace the growing field set gradually with owned components and typed read-only snapshots. The 250 ms monitor should not be used to emulate edge-triggered keyboard input. |
| `src/Receiver/ReceiverContext.Core.cs` | UxPlay/beacon process lifecycle, startup policy, marker observation, native command channel, discovery maintenance, argument construction, and diagnostics text. | Largest hotspot. `ObserveCoreOutput` mixes parsing with side effects; extract a pure typed marker parser first. Remove the obsolete `PairingMode == "password"` argument branch only after migration tests prove persisted legacy values normalize to `none`. Repeated nullable initialization of `coreCommandSync` exists to support reflection-created test objects; production synchronization should eventually live in an always-initialized command-channel object. |
| `src/Receiver/ReceiverContext.BonjourFirewall.cs` | New 0.12.19 background firewall assessment, tray warning/action, explicit confirmation/UAC repair flow, diagnostics, and discovery refresh after repair. | Correctly keeps mutation behind an explicit user action and runs assessment/UAC waiting off the UI thread. Keep UI projection here while the feature stabilizes; later expose a typed assessment snapshot if the coordinator is decomposed. Exercise cancellation, unavailable policy, form ownership, and repeated-assessment paths before physical acceptance. |
| `src/Receiver/ReceiverContext.Diagnostics.cs` | Support bundle/report flow, autostart, shutdown, logging/redaction, and shared argument quoting. | Responsibilities are only loosely related. Standard Windows argv quoting and round-trip tests are complete. Repository/product identity is also hard-coded here while update configuration exists elsewhere; centralize it later as build metadata. Log queue/batch/file limits should be named internal policy constants, not user preferences. |
| `src/Receiver/ReceiverContext.HttpReset.cs` | Local HTTP reset endpoint, strict request parsing, nonce/auth checks, and response handling. | This is the most cohesive partial and already uses a static compiled parser. Keep it independent; security tokens, loopback binding, request limits, and status codes are protocol/security invariants. |
| `src/Receiver/ReceiverContext.LostConnection.cs` | Lost-stream detection, overlay state, renderer handoff, reconnect timing, and queued UI actions. | Multiple integer pending flags and cancellation tokens encode mutually exclusive transitions informally. Characterize transitions first, then replace them with one typed mailbox/state machine. Do not change this during gallery/fullscreen fixes. |
| `src/Receiver/ReceiverContext.Rendering.cs` | Renderer HWND discovery, chrome/fullscreen detection, saved placement, automatic fit, orientation/media-canvas policy, and presentation commands. | Current 0.12.19 non-cropping policy is the correct safe default: a transport canvas signature is not a content rectangle. Input/control effects moved to their focused partial. Split geometry further only after pure decisions have direct tests; keep Win32 effects in the adapter. |
| `src/Receiver/ReceiverContext.RendererControls.cs` | Event-driven foreground Escape hook, shell-owned fullscreen control coordination, and stale-borderless detection. | The hook is bounded to actual fullscreen and always continues the keyboard chain. The stale path intentionally offers a visible manual action instead of a non-idempotent automatic toggle. Physical focus, repeated transition, DPI, and multi-monitor behaviors remain acceptance evidence. |
| `src/Receiver/RendererPresentationPolicy.cs` | New pure 0.12.19 geometry constants and Photos/device-frame classifiers. | Good extraction. These values describe renderer/protocol behavior, so named code constants are preferable to user configuration. Add table-driven geometry tests, including the rule that a Photos canvas may influence outer orientation but never authorize pixel cropping. |

### UI

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `src/UI/AppIcon.cs` | Loads and exposes the process-lifetime icon/image resources. | Small and cohesive. Document caller ownership if new callers begin disposing returned objects; no 0.12.19 change needed. |
| `src/UI/Controls/NamedValue.cs` | Display/value pair for combo-box choices. | Small and cohesive; import cleanup complete. |
| `src/UI/Controls/NetworkHelpGlyph.cs` | Accessible, theme-aware painted network help glyph. | Small and cohesive. Paint dimensions/colors are UI invariants and can stay local unless a shared palette is introduced. |
| `src/UI/Controls/WheelSafeComboBox.cs` | Prevents accidental selection changes while forwarding wheel scroll and draws a custom dropdown glyph. | Cohesive custom control. Add UI/message tests only if behavior regresses; no split is useful. |
| `src/UI/DiagnosticsForm.cs` | Read-only diagnostics viewer/copy UI. | Small and cohesive. |
| `src/UI/LostConnectionForm.cs` | Non-activating reconnect overlay, snapshot rendering, fade/handoff, and resource disposal. | Reasonably cohesive and explicitly disposes fonts, bitmap, and timer. Name timing constants later if behavior is tuned; they are UI policy, not settings. |
| `src/UI/RendererFullscreenButtonForm.cs` | Small non-activating tool-window button, accessible text, deterministic DPI geometry, and resource disposal. | Cohesive shell-owned adapter for a foreign renderer. It is hidden outside renderer foreground instead of staying above unrelated applications. Keep it free of renderer lifecycle/state ownership. |
| `src/UI/SettingsForm.cs` | Entire multi-page settings UI, construction/layout, status projection, update/install flow, save validation, theme, and timers. | At about 1,500 lines with about 100 fields and a very large constructor, this is the second managed hotspot. Direct `IsReceiverReady` fixes localized-text coupling, and unknown network now has explicit guidance. Later extract page builders and a typed `ReceiverUiSnapshot`; avoid a big designer rewrite. Ensure `ToolTip` and other manually created disposable resources have explicit ownership. |
| `src/UI/ThemeHelper.cs` | Recursive light/dark coloring and control-specific palette rules. | Useful central helper, but status/card colors are still duplicated in `SettingsForm`. A small internal `ThemePalette` can remove duplication after release; colors are product design constants, not user settings. |

### Updates

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `src/Updates/UpdateInfo.cs` | DTO for parsed release/update state. | Small and cohesive; import cleanup complete. Prefer immutable construction if update parsing is later extracted. |
| `src/Updates/UpdateService.cs` | Reads repository configuration, calls GitHub, parses latest release, selects the exact installer, downloads, and verifies SHA-256. | Exact asset matching and fail-closed digest verification are correct product/security invariants. Redundant architecture-name filtering is removed. Later extract pure release-JSON parsing for offline tests and name TLS 1.2 instead of leaving numeric `3072`; neither belongs in user settings. |

### Installer assembly

| File | Current responsibility | Audit decision |
| --- | --- | --- |
| `installer/AirPlayReceiverSetup.cs` | Setup/uninstall entry points, setup form, paths/shortcut choices, installation transaction, runtime verification/download, process control, registry, shortcuts, and pinned network client. | At about 2,200 lines it is large, but its conceptual classes already provide safe split points. After 0.12.19, move existing classes to separate files without changing behavior and make `build-installer.ps1` compile all installer sources. Keep pinned URL/hash, allowed PE architecture, install paths, rollback, and certificate/TLS checks as supply-chain invariants. |

## Build, packaging, native inputs, and test inventory

| File | Audit result |
| --- | --- |
| `app.manifest` | Static Windows compatibility, DPI, execution-level, and architecture contract. Its `1.0.0.0` identity is not evidence that product versioning is broken; do not bind it to release version without a deployment requirement. |
| `build.ps1` | Simple managed compiler driver. `Configuration` currently mostly selects the output folder while optimization is always enabled. Later either validate the supported configuration semantics or make one release build explicit; build into a temporary output before replacing the last good executable. |
| `run-dev.ps1` | Minimal development launcher; no cleanup needed. |
| `build-installer.ps1` | Compiles setup/uninstaller and validates runtime/provenance. It already cleans its validation directory in `finally`. Source discovery should precede any installer class split; source-text literal gates should gradually become behavior tests. |
| `download-core.ps1` | Legacy/offline runtime extraction with a child-path safety check. Still referenced by the portable path, so it is not dead. |
| `package.ps1` | Portable/offline package path with provenance and PE/hash checks. Its GUID staging directory is removed only on success; wrap staging in `try/finally` so failed runs do not accumulate multi-gigabyte folders. |
| `package-review.ps1` | Public/review payload path with pinned runtime verification. It has the same success-only staging cleanup. Its generated delivery metadata repeats values also represented by provenance/build validation; derive or validate one canonical delivery record later. |
| `release.ps1` | Release orchestration and artifact validation. Keep tag/version/provenance checks fail closed. Version default duplication is maintenance debt, not runtime configuration. |
| `build-native-source.ps1` | Creates corresponding-source material with explicit file/provenance validation and good temporary cleanup. Large explicit lists are intentional auditability; change them only with source-offer tests. |
| `native-core/build-compatible-core.ps1` | Applies pinned patches and builds the compatible native core. Keep it independently usable for corresponding source. Common helpers may be copied from a small native-local module, but it must not depend on an unavailable root workspace module. |
| `native-core/build-headless-runtime.ps1` | Builds/stages the headless runtime and manifests. Fixed native/provenance checks are supply-chain invariants. A failed run may leave one partial stage; cleanup can be tightened independently. |
| `native-core/source-provenance.json` | Canonical pinned upstream commits/assets/hashes and patch identity. Treat as immutable release evidence, not user configuration. |
| `native-core/dnssd.def` | Native export contract for DNS-SD linkage. Keep declarative and validate exports. |
| `native-core/gstreamer-features.txt` | Runtime feature allowlist. Keep declarative and validate the packaged registry/bundle against it. |
| `native-core/uxplay-windows-headless.patch` | First-party integration patch for headless/window-command behavior over pinned upstream. Review as protocol code; do not restyle it mechanically. |
| `native-core/libuxplay-aeromirror.patch` | First-party AirPlay/worker/discovery integration patch over pinned upstream. It is large because it carries corresponding-source modifications; contract and harness tests are more valuable than formatting cleanup. |
| `tests/ReceiverResilience.Tests.ps1` | Broad managed contract suite, now about 4,000 lines. It relies heavily on private reflection and source substrings, so refactors can fail despite preserved behavior and physical bugs can pass. Split by domain and replace source-shape assertions with tests of pure policies/parsers before moving production classes. |
| `tests/NativeCoreContracts.Tests.ps1` | Useful pinned-patch and marker contract checks, but source-shape heavy. Add compiled parser/protocol tests for behavioral confidence. |
| `tests/NativeDiscoveryPipe.Tests.ps1` | Valuable real discovery-pipe integration coverage. Extend with long-idle/restart/unlock scenarios where CI support permits; physical Bonjour visibility remains a separate acceptance check. |
| `tests/NativeWorkerLifecycle.Tests.ps1` | Builds/runs the lifecycle harness and protects worker shutdown/restart behavior. Retain as a release gate for native lifecycle changes. |
| `tests/NativeWorkerLifecycleHarness.c` | Functional C harness for worker lifecycle and failure transitions. Cohesive test support; no production cleanup needed. |
| `tests/NativeCryptoHappyPathHarness.c` | Focused crypto happy-path harness. Keep it narrow and pair security changes with negative-path contract checks. |
| `tests/BonjourFirewall.Tests.ps1` | New 0.12.19 deterministic tests for strict path/rule matching and command construction. Keep host firewall mutation out of automated tests; perform a separate manual UAC/firewall acceptance check. |

Several root and native scripts repeat `Assert-ChildPath`, `Get-PeMachine`,
`Get-Sha256Lower`, and hash-map comparison helpers. A small root-local build
module could reduce divergence, but the corresponding-source/native package
must remain independently buildable. Do not centralize these helpers across a
distribution boundary merely to remove duplicate lines.

## Constants: configuration versus invariants

Moving every visible constant into configuration would make the product harder
to reason about and would expose unsafe combinations.

Keep these as **user policy/settings**, with normalization and migration:

- receiver display name;
- PIN mode and fixed PIN;
- quality/renderer/latency choices;
- auto-fit, placement, startup, notifications, theme, and tray behavior;
- explicitly documented advanced native arguments.

Keep these as **named code/build invariants**, not `settings.ini` options:

- protocol markers, mDNS port and firewall scope;
- exact update asset name, HTTPS and SHA-256 requirements;
- native runtime architecture, pinned URLs/commits/hashes, feature allowlist;
- receiver-name UTF-8 limit and PIN syntax;
- renderer normal scale, aspect tolerances, and the rule forbidding crop
  without a trusted content rectangle;
- process/pipe/request bounds, security timeouts, log queue/file safety caps;
- product/repository identity and release version source.

Values may still be centralized and named even when they are not configurable.

## Staged, independently testable plan

### Stage 0: 0.12.19 correctness and release gate

Completed in source and automated contracts:

1. Standard Windows argv quoting plus `CommandLineToArgvW` round trips.
2. Bounded event-driven Escape plus the no-activate renderer control.
3. Explicit unknown-network guidance.
4. Non-cropping Photos presentation and typed receiver readiness.
5. Read-only background firewall assessment and explicit background repair.

The complete managed/native/package/installer/publication gates pass.
Discovery after cold boot, long idle, network change, session unlock, and
manual restart; real Escape/focus; visible photo edges; and the non-admin
UAC/firewall path remain physical acceptance. Ordinary startup must remain
non-mutating.

### Stage 1: mechanical hygiene after release

- Remove remaining unused imports one file at a time.
- Remove confirmed unused P/Invokes and the normalized-away password branch.
- Add `try/finally` staging cleanup to both packaging scripts.
- Name local policy constants without turning them into settings.
- Split the 4,000-line managed test script by domain while preserving one
  aggregate entry point.

Each item is independently reviewable and should produce no behavior diff.

### Stage 2: pure decisions before state movement

- Extract `CoreMarkerParser` returning typed events.
- Extract the network JSON parser/selector.
- Extract `UxPlayArgumentBuilder` with tested Windows quoting.
- Extract pure update-release JSON parsing.
- Expand table-driven `RendererPresentationPolicy` tests.

These slices reduce reflection and source-text testing without moving process
or UI ownership yet.

### Stage 3: explicit state ownership

Introduce one component at a time behind the existing `ReceiverContext`
composition root: core lifecycle/command channel, discovery maintenance,
renderer window/presentation, lost-connection transitions, and network trust.
Expose immutable snapshots/events to the UI. Preserve thread affinity and add
transition tests before deleting old fields.

### Stage 4: UI, settings, and installer structure

- Separate settings serialization/migration from an immutable settings value.
- Break `SettingsForm` construction into page builders and consume one typed UI
  snapshot; centralize palette/resource ownership.
- Split existing installer classes into files without changing the transaction.
- Make tests locate source recursively before any file move.

### Stage 5: build and release metadata

- Establish one release-version input and one validated runtime-delivery record.
- Share root packaging helpers in a root-local module while keeping the native
  corresponding-source path standalone.
- Build/package into temporary locations and replace final artifacts only after
  validation.

No stage requires committing to a wholesale rewrite. A stage is complete only
when its behavior-level tests pass and, for renderer/discovery changes, the
relevant physical-device check also passes.

## Missing behavior-level coverage

The highest-value gaps are:

- short Escape press, fullscreen entry/exit acknowledgement, restored window
  chrome, and keyboard focus;
- portrait and landscape Photos frames verified by pixels/content bounds, not
  only marker numbers or source strings;
- Windows argv round trips for spaces, quotes, backslash-plus-quote, and a
  trailing backslash;
- malformed/unknown/multi-adapter network JSON and matching UI guidance;
- cold boot and multi-hour Bonjour visibility across unlock/network change;
- updater release JSON with wrong architecture/look-alike asset names, missing
  digest, and exact expected asset;
- failed package run proving that its unique staging directory is removed;
- installer update/reinstall defaults and cancellation/rollback paths.

Automated PASS and physical-device acceptance must be reported separately.

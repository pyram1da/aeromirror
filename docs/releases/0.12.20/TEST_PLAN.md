# AeroMirror 0.12.20 — native viewer, Photos, and setup acceptance

## Purpose

This plan verifies the corrective candidate after physical 0.12.19 reports of
a lagging/orphaned fullscreen overlay, intermittent Escape, excess
confirmations, and a portrait Photos presentation described as cropped. It
keeps automated evidence separate from real Windows/iPhone acceptance.

## Current evidence status

| Gate | Status | Required evidence |
|---|---|---|
| Managed x64 shell build | PASS | Final source builds as `0.12.20.0` |
| Focused Bonjour contracts | PASS | Exact rule scope, direct one-UAC action, expiring/restart-refreshed state, accurate unavailable diagnosis, no success modal |
| Complete receiver resilience | PASS | Updated native-viewer, dynamic acknowledgement ordering, Photos-contain, updater handoff, and lifecycle contracts |
| Native core build/contracts | PASS | Exact setter grammar, GUI-thread ownership, one HWND, Escape/caption/Alt+Enter, missing-Bonjour exit |
| Native provenance/corresponding source | PASS | Materialized patches, two identical clean builds, 148-entry prepared source and extracted no-Git rebuild |
| Runtime Unicode path | PASS | Same bytes, separate fresh registries, ASCII and Cyrillic application-path `--self-test` exit 0 |
| Review payload and Setup | PASS | Exact 13-entry payload, embedded equality, x64 Setup, loader/shortcut/update self-checks |
| Physical Photos containment | PENDING | Portrait/landscape images retain all visible edges; letterboxing allowed |
| Physical native fullscreen | PENDING | Caption, tray, Alt+Enter, one-press Escape, restore, DPI/multi-monitor |
| Installed update/reinstall | PENDING | One app decision, no shortcut form, preserved shortcuts/settings, relaunch |
| Bonjour UAC flow | PENDING | Visible card, one UAC, exact rule, decline/failure/success, no bundled service install |
| Long-idle discovery | PENDING | Repeated first-open iPhone browse checks across idle/unlock/sleep/network change |
| Tag and publication | PASS | Immutable annotated tag, exact four assets, latest routes, API digests, and fresh public downloads verified |

The reproducible native executable is 1,178,345 bytes with SHA-256
`4336B9DBFCDE87123EC4796FE43FAA4F1952E27224932B3DD5E8FEAFBAD41832`.
The public x64 Setup is 1,431,040 bytes with SHA-256
`A4071C3B875484A154EFBE9EE11CB23CB1EE2C21A0840060A8ADDB97F923379D`
and file version `0.12.20.0`. Publication evidence is recorded in
[`BUILD_REPORT.md`](BUILD_REPORT.md); these release gates do not change the
physical PENDING rows above.

## Automated acceptance

1. Build the managed shell and confirm x64 PE/file version `0.12.20.0`.
2. Run the complete resilience and focused firewall suites. They must prove:
   - deleted managed overlay and keyboard-hook types are not compiled or
     referenced;
   - the shell sends only exact `video-fullscreen-set state=0|1`, accepts only
     the exact native acknowledgement grammar, and chooses the next tray state
     from the last acknowledgement;
   - Photos never selects a scale above 100% and does not promote the exact
     media canvas to trusted device orientation;
   - the first update button remains the decision and no second post-download
     application confirmation exists; unsaved settings are resolved first and
     update-page navigation remains locked through installer handoff;
   - the main network card distinguishes a missing exact firewall rule from
     unavailable Bonjour, describes that state without a false missing-service
     diagnosis, expires its cache, and reassesses on start/restart/refresh;
3. Build and test the native core. Verify:
   - parser rejection for missing/extra/invalid state values and multiline
     input;
   - the state matrix `0+0=noop`, `0+1=applied`, `1+1=noop`, `1+0=applied`;
   - caption, Escape, Alt+Enter, and IPC share one GUI-thread setter;
   - one top-level viewer owns one child video surface, with no sink-created
     second window, crop rectangle, or non-GUI Qt mutation;
   - aspect-ratio containment is explicit and reset/stop leaves no orphan HWND;
   - Caption Close exits fullscreen and minimizes without clearing the active
     visibility generation, minimized HWND lookup does not reject `IsIconic`,
     fullscreen from minimized retains `normalGeometry()`, stop/destroy alone
     owns the `visible=1 -> 0` HIDE transition, and a delayed fullscreen request
     cannot show the lifecycle-hidden host;
   - missing Bonjour in headless mode emits one stable marker and exits with
     code 20 without MessageBox or bundled service registration.
   - all nine runtime path variables use the wide Windows environment API and
     a rejected value fails closed; run `--self-test` with a separate fresh
     registry through both ASCII and Cyrillic application paths.
4. Materialize both native patches, update provenance, perform two compatible
   builds where required, prepare corresponding source, and rebuild it from an
   extracted no-Git tree.
5. Build the review payload and Setup. Run runtime, shortcut-selection, and
   update-lifecycle self-checks. Runtime verification must execute
   `--loader-test`; the broader `--self-test` remains a staged-bundle gate.
   Repeat managed resilience against
   the packaged shell and verify embedded shell/core/provenance byte equality.
6. Run version/default, UTF-8, local-link, expected-file, and
   `git diff --check` gates.

## Physical test A — Photos containment

1. Record Setup hash, Windows/iPhone versions, GPU, renderer, display DPI,
   phone orientation lock, and the exact portrait/landscape images.
2. Start on the portrait home screen, open the portrait image, and compare all
   four edges with the iPhone. The native viewer may letterbox, but must not
   crop, stretch, or repeatedly resize.
3. Repeat with a landscape image, thumbnails, Camera, video, direct-in-Photos
   startup, and ten rapid portrait/landscape transitions.
4. Repeat in and out of fullscreen. Normal placement must remain movable,
   resizable, and restorable after every media transition.

## Physical test B — native fullscreen

1. Enter separately from the caption control, tray, and Alt+Enter. There must
   be no floating second control and no delayed movement while dragging.
2. Exit each route with one normal Escape press, including a short press with
   focus on video. Repeat 20 times and retain the native acknowledgement lines.
3. With **Show stream in taskbar** enabled, verify minimize and Caption Close
   both keep playback alive and restore from the taskbar. With it disabled,
   Caption Close must remain recoverable through **Show stream window** in the
   tray. Stop the session and verify the host hides; the next session must show
   it exactly once.
4. Verify drag, resize, always-on-top, and session stop/start. No borderless
   normal state or orphan viewer may remain.
5. Repeat on available monitors at 100%, 125%, 150%, and 200% DPI. Fullscreen
   must use the current monitor and restore the original normal rectangle once.

## Physical test C — update and Bonjour confirmations

1. From an installed older version, choose **Download and install** once.
   Confirm the digest-verified Setup launches without a second AeroMirror
   Yes/No or shortcut/launch form. Record any Windows UAC/SmartScreen prompt
   separately from application prompts.
   Repeat once with an unsaved setting: save, discard, and cancel must each
   complete before download starts, and Back must remain unavailable while the
   verified installer is downloading or launching.
2. Verify existing Start-menu/desktop shortcut absence or presence, settings,
   identity, trusted client, autostart, and relaunch. Repeat a same-version
   reinstall and a controlled rollback failure.
3. With the exact Bonjour rule absent, verify the main card is visible. Decline
   UAC once: no rule/service state may change. Approve once: exactly one
   enabled inbound Allow rule must target the validated executable, Private,
   UDP 5353, remote LocalSubnet, and no edge traversal; no success modal or
   duplicate rule may appear.
4. On a disposable machine with Bonjour absent, verify the card reports the
   prerequisite and the core produces no interactive native install/dialog
   path. AeroMirror must not register its bundled responder as a service.

## Physical test D — discovery retention

1. After successful startup, check the iPhone list immediately and then after
   10, 30, 50, and at least 70 minutes without mirroring.
2. Retain same-PID/same-port renewal generations and each first-open iPhone
   browse result. Repeat after lock/unlock, sleep/wake, and physical-network
   reconnect.
3. Use **Restart discovery** only after the failed observation is recorded; it
   remains a deliberate full DNS-SD-and-BLE restart and is not evidence that
   the automatic path worked.

## Failure boundary

Do not publish acceptance claims if any image edge is lost, the fullscreen
control is detached from the native frame, Escape requires a second press,
normal chrome is not restored, Setup repeats application choices, Bonjour
repair broadens scope or installs a bundled service, or local readiness is
presented as proof of iPhone visibility.

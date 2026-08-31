# AeroMirror 0.12.22 — release acceptance plan

## Scope and status

This plan accepts the 0.12.22 review release changes for automatic Apple
Bonjour recovery, per-device first-connection trust, native fullscreen,
optional staged updates, installer behavior, and public presentation. It keeps
automated evidence separate from physical Windows/iPhone evidence.

Public `v0.12.22` is the verified normal updater-visible latest Release. Public
`v0.12.20` remains immutable as the previous review release. The unpublished
0.12.21 candidate is superseded and must not be tagged, published, or used as
an asset source.

| Gate | Current status | Acceptance evidence |
|---|---|---|
| Managed x64 build | PASS | Exact 0.12.22 PE/version and build transcript |
| Receiver/Bonjour/automatic-update contracts | PASS | Complete focused PowerShell transcripts |
| Pairing/fullscreen/native contracts | PASS | Complete managed/native transcripts plus loader and isolated runtime self-tests |
| Clean native reproducibility and corresponding source | PASS | Two clean builds plus extracted no-Git source rebuild reproduce `E4601B1BDAE661AF63A3F92C9FDA01CA66E54B6E2C5A36EDF802BAF0338CE6F6` |
| Exact review package and Setup | PASS | Exact entry set, embedded equality, x64 version, and all four non-installing Setup self-checks from the frozen core |
| Exact tag and four public assets | PASS | `BUILD_REPORT.md`: tag commit, normal Release/API state, exact names/sizes/digests, both latest routes, fresh unauthenticated re-download equality |
| Physical Windows 10/11 and iPhone matrix | PENDING | Screenshots/video plus redacted logs and before/after machine state |

## Test environments

Retain evidence for:

1. the maintainer Windows 11 x64 machine used for clean build/package gates;
2. a controlled Windows 11 x64 install/update/recovery machine;
3. a clean Windows 10 1809-or-newer x64 VM or PC;
4. at least one current physical iPhone/iOS combination, plus a second iPhone
   or a deliberately reset trust register for unknown-device behavior;
5. a Private physical Wi-Fi/Ethernet LAN with the iPhone on the same local
   network; record any VPN/virtual adapters separately;
6. one multi-monitor or mixed-DPI Windows arrangement for overlay/fullscreen
   placement; and
7. the exact public 0.12.20 installation for update-source testing.

For every physical row record Windows build, AeroMirror/Setup version, iPhone
model and iOS version, connection type, physical network name/category, display
scale, test time/time zone, shell/core PIDs, AirPlay ports, Apple Bonjour service
identity/path/version/status, and whether automatic updates were enabled.
Never retain the active PIN, receiver key, trusted-client contents, Wi-Fi
password, Apple ID, or unredacted user-profile paths.

## Automated source and build gates

Run from a clean reviewed source tree:

1. Verify `update-repository.txt` contains exactly `Nadejny/aeromirror`, every
   public/default version is 0.12.22/`0.12.22.0`, and no 0.12.21 artifact is
   selected for packaging or publication.
2. Run `build.ps1`, `tests/ReceiverResilience.Tests.ps1`,
   `tests/BonjourFirewall.Tests.ps1`, `tests/AutomaticUpdate.Tests.ps1`, and the
   applicable setup/update/native contract suites. Retain complete transcripts.
3. Pairing contracts must prove:
   - cryptographic generation always yields four ASCII digits with observed
     diversity;
   - one structured native request maps to one current PID/request overlay;
   - the 60-second timeout and Escape cancellation are bounded;
   - a stale PID/request cannot receive a secret or dismiss the current overlay;
   - pairing secrets are absent from process arguments, persisted settings,
     ordinary/redacted logs, and diagnostic export paths;
   - timeout, Escape, disconnect, malformed SETUP, stale completion, and a
     verified client-key mismatch all return a negative native admission result;
   - genuine control markers use the dedicated emitter, while exact marker
     text plus CR/LF/C0 bytes in ordinary client or HLS metadata cannot create a
     shell-accepted control line and raw client identifiers are not logged;
   - master-playlist URI discovery and both condensed-playlist passes remain
     line/chunk bounded, checked output cannot exceed 32 MiB or `INT_MAX`, and
     malformed or allocation-failure paths cannot terminate the receiver;
   - legacy fixed PIN/password, key, and register overrides are removed; and
   - trust revocation is atomic, records a durable pending reset before native
     shutdown, blocks replacement after unconfirmed exit, and clears the trust
     store before marker removal or restart after confirmed exit.
4. Fullscreen contracts must prove one native GUI-thread state setter owns
   caption maximize, Alt+Enter, Escape, lifecycle exit, and exact shell request;
   stale/unavailable acknowledgements cannot overwrite newer state; no managed
   overlay, global keyboard hook, speculative toggle, or delayed floating button
   remains.
5. Bonjour runtime contracts must prove there is no main/tray repair or
   discovery-restart action; service/firewall assessment is read-only; `-65563`
   removes ready; BLE cannot replace DNS-SD; a service-return event permits at
   most two same-process refresh submissions; and a correlated ready result is
   required.
6. Setup verification must prove the elevated branch runs before user logging
   or UI, accepts only the exact Apple service and protected canonical Program
   Files path, rejects reparse/untrusted owner or write/NULL-DACL state, uses
   direct SCM and Firewall COM APIs, and does not spawn `sc.exe`, `netsh.exe`,
   a shell, or elevated AeroMirror.
7. Setup must configure Automatic start, start the service when needed, set
   restart actions to 5/30/120 seconds plus the non-crash failure flag, and
   converge exactly one enabled inbound Allow rule for the exact executable,
   Private, UDP 5353, `LocalSubnet`, no edge traversal. The helper and parent
   waits must remain bounded. Failure/decline must occur after the application
   commit and cannot invoke rollback.
8. Automatic-update tests must prove default-off behavior; fixed repository and
   exact `vX.Y.Z` initial URL; strict three-part version and exact asset name;
   HTTPS-only bounded allowlisted redirects; no userinfo, fragment, or
   non-default port on any hop; no query on the exact initial GitHub URL while
   allowing provider-signed queries on allowlisted CDN hops; 64 MiB response
   bound; new-file/regular-file staging;
   SHA-256 verification; current-user DPAPI manifest; age, path, version, digest,
   attempt and retry validation; stale download cleanup; disabling cleanup; and
   fail-open normal startup for invalid staging. Setup tests must also prove one
   SID-derived transaction mutex, under-lock primary-executable version
   revalidation, invalid-primary repair behavior, bounded recovery while the
   same mutex remains held, and restoration of the automatic attempt budget
   only for a genuine busy-transaction handoff.
9. Apply both canonical native patches to their pinned upstream commits. Run two
   independent clean native builds. Stage and test the runtime, then create the
   corresponding-source archive, extract it without Git metadata, validate all
   provenance inputs, and rebuild. All three core hashes must match the frozen
   expected 0.12.22 hash. The final engineering runtime must inspect 200 PE
   binaries, copy 148 dependency DLLs, resolve 44 requested features to 27
   GStreamer plug-ins, and pass isolated self-tests from ASCII and Cyrillic
   paths.
10. Build the exact review payload and Setup. Verify the allowed payload entry
    set, packaged-shell resilience, native provenance, x64 PE/version,
    byte-for-byte embedded input equality, runtime/loader self-checks,
    shortcut/update lifecycle checks, Bonjour recovery self-check, and
    `git diff --check`.

Any secret in output, broad firewall/service mutation, unbounded wait, version
mismatch, non-reproducible native hash, unexpected package entry, or failed
self-check blocks tagging and publication.

## Fresh install and Bonjour machine policy

Run these rows on a controlled machine or VM; do not deliberately corrupt
Bonjour on a daily-use PC.

1. With an exact healthy Apple Bonjour installation, run a clean 0.12.22 Setup.
   The ordinary shortcut/launch options appear once. Approve the separate
   administrator request. The per-user application commits, then the exact
   service is Automatic and Running with 5/30/120-second recovery plus the
   non-crash flag, and exactly one narrow firewall rule exists.
2. Re-run the same Setup. It is an unattended reinstall, preserves existing
   shortcut choices, creates no duplicate rule, converges the same service
   policy, preserves settings/identity/trust, and relaunches AeroMirror.
3. Repeat from a clean snapshot and decline administrator approval. The
   application remains correctly installed and starts unelevated; Setup records
   best-effort machine configuration as declined/failed without rollback.
4. Test a missing service and, separately, an identity/path/owner/ACL/reparse
   mismatch using a controlled fixture. Setup leaves the unsafe state untouched,
   the headless receiver reports the prerequisite, and no broad fallback rule or
   bundled system service appears.
5. Uninstall AeroMirror. Per-user application files/registration are removed as
   designed, while the exact Apple Bonjour recovery policy and narrow firewall
   rule remain. Reinstall and confirm idempotent convergence without duplicates.

Failure conditions include application rollback after the post-commit helper,
an elevated per-user application, a Public/TCP/Any rule, another executable or
service being touched, duplicate exact rules, or automatic removal of shared
Bonjour policy during normal uninstall.

## Runtime discovery and long-idle recovery

1. Start AeroMirror normally and verify the main page and tray have no Bonjour,
   firewall, or discovery-restart button. Healthy startup reaches ready only
   after sockets and paired DNS-SD publication; BLE alone is insufficient.
2. On a controlled machine, stop or fault the exact Apple Bonjour service while
   AeroMirror is idle. The shell/core remain alive, ready is removed, `-65563`
   becomes one unavailable-prerequisite state, and native retry/process churn
   stops. The UI may show a read-only notice without an elevation prompt.
3. Allow Windows service recovery to return Bonjour to `Running`. AeroMirror
   detects it without opening Settings, submits no more than two correlated
   in-process discovery requests, reaches `AEROMIRROR_DNSSD_READY`, preserves
   core PID and AirPlay ports, and appears in the iPhone Screen Mirroring list.
4. Repeat after an in-place 0.12.20-to-0.12.22 update and after Windows sign-in.
   Check iPhone visibility at 1, 20, and 60 minutes and after the recurring idle
   schedule. Record local DNS-SD acknowledgement separately from the physical
   iPhone browse result.
5. Connect and mirror after recovery; verify audio/video, clean disconnect, and
   the next idle session. A physical IPv4 change may use the documented internal
   full restart; an ordinary service return must not.

## First-device trust

1. Use a clean per-user identity/trust state and select AeroMirror from an
   unknown iPhone. The active PC display receives one borderless high-contrast
   overlay with a clearly readable four-digit code. No Settings navigation is
   needed and no network-category choice is requested.
2. Enter the correct code. The overlay closes, mirroring starts, and the device
   reconnects after disconnect, application restart, Windows restart, and an
   in-place reinstall/update without another code.
3. Cancel a fresh request with Escape, let another expire for 60 seconds, and
   try an incorrect code. Each request ends cleanly without trusting the device,
   leaking digits, crashing, or interrupting the long-lived receiver.
4. Pair a second previously unknown iPhone. It receives its own fresh code; the
   first device remains trusted. Confirm overlay placement on the active display
   with multi-monitor and mixed-DPI layouts.
5. Use **Reset trust** in Settings and accept its confirmation. Every prior
   device requires a new code on its next connection; receiver identity and
   unrelated settings remain intact.
6. Inspect process command lines, ordinary and redacted logs, diagnostic report,
   settings migration output, and retained artifacts. No active or legacy PIN,
   password, private key, or trusted-client content may appear.

## Viewer, fullscreen, and Photos regression

1. Start portrait home-screen mirroring and confirm the normal renderer is
   framed, movable, resizable, and restored at its remembered placement.
2. Use caption maximize. The renderer becomes a clean monitor-sized borderless
   view with no caption, Close, Minimize, or floating shell control. One normal
   Escape returns to the exact usable framed placement. Repeat at least 20 times.
3. Repeat entry/exit with Alt+Enter and the tray fullscreen action. Mix entry
   from normal/minimized state, window movement, both taskbar policies, display
   changes, and 100/125/150/200% scale. No lagging button, stuck borderless
   window, missing Escape, speculative double toggle, or hidden empty host is
   accepted.
4. Open a portrait photo in Photos, zoom on the phone, rotate the phone, enter
   and leave fullscreen, return home, and repeat with Camera/video. Record outer
   client bounds and visible inner-media bounds separately. AeroMirror must not
   crop real pixels by inventing a content rectangle; letterboxing is an
   accepted current limitation.
5. Lock the iPhone while AirPlay remains selected and close/minimize the viewer.
   Record whether loss/recovery reopens a user-dismissed window. Any repeated
   unwanted reappearance remains a release blocker if 0.12.22 claims to fix it;
   otherwise retain it explicitly as an unchanged follow-up observation.

## Automatic and manual update acceptance

1. Start from a clean/default profile. Automatic updates are off; startup may
   clean stale known downloads but does not stage or launch a new release.
2. Enable automatic updates while no session is active. The exact newer normal
   GitHub Release is checked, its exact Setup is downloaded and verified, and a
   protected pending stage is created. The current receiver keeps running and
   Setup does not launch in that process lifetime.
3. Repeat the check while mirroring is active. Background work must not close or
   restart the session. Download and verified staging may complete while the
   session continues; Setup must not launch until a later AeroMirror start.
4. Exit normally and start AeroMirror again. Before the receiver/UI starts, the
   stage is revalidated and unattended Setup runs once. Shortcuts, settings,
   receiver identity, trusted devices, and runtime cache survive; the new shell
   starts after success.
5. Test wrong digest, oversized/truncated response, redirect outside the allowed
   HTTPS host set, stale/old/current-version stage, unsafe/reparse path, expired
   manifest, failed launch, and disabled setting. No invalid Setup launches;
   normal receiver startup continues; retry/cleanup remains bounded.
6. Test manual **Check for updates** with accept and cancel. Curated notes are
   shown, one application confirmation controls the exact verified download,
   and unattended Setup asks no second shortcut/launch question.
7. On a controlled disposable install, start two 0.12.22 Setup transactions for
   the same user. Only one may mutate the tree; the other waits/exits through the
   bounded busy path and never launches an executable from a mutable tree.
   Do not use a historical pre-0.12.22 Setup for this row: those immutable
   binaries predate the mutex and concurrent cross-version execution is an
   explicit limitation, not an accepted workflow.

## Publication and public verification

1. Freeze and commit the reviewed source to `main`; confirm a clean worktree.
2. Create annotated tag `v0.12.22` at that exact commit. Run the release pipeline
   from a clean exact-tag checkout, never from stale 0.12.21 artifacts.
3. Publish one normal, non-draft, non-prerelease GitHub Release with exactly:
   `AeroMirror-Setup-0.12.22.exe`, `AeroMirror-source-0.12.22.zip`,
   `AeroMirror-native-source-0.12.22.zip`, and `SHA256SUMS.txt`. Do not publish
   the portable/offline runtime.
4. Verify Release/API state, asset names/count/sizes/digests, canonical and
   legacy latest routes, and fresh public re-download byte equality. Apply the
   reviewed repository description and topics. These publication checks pass;
   installed-client update discovery remains part of the physical update row.
   Do not add a fabricated screenshot.
5. Add `BUILD_REPORT.md` with tag/commit, commands, public URL, asset hashes,
   re-download evidence, completed physical rows, pending rows, limitations,
   and immutable-asset statement; update `docs/PROJECT_STATE.md` after
   publication.

## Acceptance gate

Publication is blocked by any failed automated/native/package/Setup/tag/public-
asset gate. The authorized normal review Release may be published with physical
rows still marked PENDING, but its notes and build report must not claim Windows
or iPhone acceptance from automation alone. Never replace an already published
0.12.22 asset; any post-publication correction receives a newer patch version.

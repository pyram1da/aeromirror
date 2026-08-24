# AeroMirror 0.12.19 — gallery, fullscreen, and discovery acceptance

## Purpose

This plan verifies the corrective review build after physical reports against
0.12.18 on another PC and a separate discovery investigation on a PC that was
still running 0.12.17. Those observations must not be mixed: gallery and
fullscreen behavior is valid product feedback, while the local missing-row
episode is not evidence of a 0.12.19 regression. A separate read-only check
reported that the exact narrow Private Bonjour firewall rule was absent; that
is a prerequisite finding, not proof of the missing-row cause.

## Current evidence status

| Gate | Status | Required evidence |
|---|---|---|
| Managed x64 shell build | PASS | Final source builds as `0.12.19.0` |
| Receiver resilience suite | PASS | Complete suite plus new non-crop, Escape/rearm, overlay Z-order, async firewall, updater, and readiness contracts |
| Source/version/default audit | PASS | Shell/Setup `0.12.19.0`, Setup comparison and exactly five script defaults `0.12.19` |
| Native reuse/provenance | PASS | Native contracts and eight-scenario worker lifecycle pass; core remains SHA-256 `c217386cbc916f8889a9c03774390fe7ec7d8c7ee0b6f64358215caceeb35118`; corresponding-source build passes |
| Review payload and Setup | PASS | Final pre-tag payload, embedded equality, x64 Setup, and all non-installing self-checks pass; exact-tag release will rerun them |
| Physical Photos contain | PENDING | Complete portrait and landscape photos remain visible without cover-crop |
| Physical fullscreen control | PENDING | Button, tray, Alt+Enter, Escape, exit, and repeated Photos transitions |
| Physical titlebar lifecycle | PENDING | DPI, multi-monitor, minimize/restore, close, taskbar, topmost, and no orphan control |
| Private Bonjour repair | PENDING | Missing-rule detection, explicit UAC repair, LocalSubnet-only rule, no broad/duplicate rule, and iPhone rediscovery |
| Long-idle discovery | PENDING | Repeated iPhone browse checks while same-PID renewal continues |
| Tag and publication | PENDING | Immutable annotated tag, exact four assets, checksums/API digests, latest routes, and fresh downloads |

## Automated acceptance

1. Build the managed shell and confirm PE/file version `0.12.19.0`.
2. Run the complete receiver resilience suite. It must prove that:
   - the Photos marker can affect the outer target but never selects a scale
     above 100%;
   - fullscreen Escape is event-driven, limited to an actual foreground
     renderer, and does not consume the key;
   - the shell-owned control has one owner, follows renderer bounds and DPI,
     is visible in both framed and actual fullscreen states as appropriate, and
     is hidden for a missing/invisible/minimized renderer or any unrelated
     foreground application;
   - Bonjour repair accepts only a validated service executable and emits the
     exact Private/UDP/5353/LocalSubnet rule specification;
   - updater selection requires the one exact versioned Setup asset;
   - UI readiness is independent of localized display text.
3. Verify the unchanged native executable, runtime manifest, patches, and
   source provenance against the committed 0.12.18 baseline.
4. Build the exact review payload and Setup, run runtime, shortcut-selection,
   and update-lifecycle self-checks, then repeat packaged-shell resilience.
5. Run UTF-8, local-link, version-surface, and `git diff --check` gates.

## Physical test A — Photos without crop

1. Record the exact Setup hash, Windows/iPhone versions, display DPI, phone
   orientation lock, and the photo used.
2. From the home screen in portrait, open Photos and the same portrait image
   used in the report. Confirm every edge of the image that is visible on the
   iPhone remains visible on Windows; letterboxing is allowed, cropping is not.
3. Repeat with a landscape image, thumbnails, Camera, a video, and rapid
   portrait/landscape transitions.
4. Exit and re-enter Photos five times. The outer window must not enter a
   resize loop, save an ambiguous canvas as trusted orientation, or lose its
   movable/resizable framed state.

## Physical test B — fullscreen and Escape

1. Enter fullscreen from the titlebar control, tray, and Alt+Enter. Confirm the
   control is visible beside normal caption controls while framed and remains
   available as the inset exit control while fullscreen.
2. Exit each path with one normal Escape press. Repeat with a short press and
   with focus on the renderer content.
3. Enter fullscreen, move between Photos and the home screen, then exit. The
   normal caption, move, resize, minimize, close, placement, and scale must all
   be restored on the first attempt.
4. Repeat on two monitors and at 100%, 125%, 150%, and 200% DPI where
   available. The managed button must neither overlap Windows caption buttons
   nor remain above unrelated applications in either topmost state. It must
   disappear for a missing, invisible, or minimized renderer. Also force the
   real-fullscreen to stale-borderless transition and confirm that the exit
   control stays available without an automatic second toggle; use the control
   once and confirm the normal frame returns.

## Physical test C — Private-network Bonjour firewall

1. On a disposable or fully recorded machine, retain the physical profile,
   default inbound policy, Bonjour service image path, and existing firewall
   rules before changing anything.
2. Confirm the diagnostic distinguishes a missing exact Private rule from a
   healthy rule. Decline UAC once and verify no rule or service state changes.
3. Approve repair. Verify exactly one enabled inbound Allow rule exists for
   the validated Bonjour executable, UDP local port 5353, Private profile, and
   remote LocalSubnet. Public, TCP, Any-address, and unrelated rules must be
   unchanged.
4. Refresh discovery and test first-open iPhone visibility, two hours idle,
   lock/unlock, sleep/wake, network reconnect, and one successful mirror.
5. Run the assessment again and verify it reports the configured rule without
   adding a duplicate. Do not claim uninstall cleanup: 0.12.19 has no wired
   removal path for this external firewall rule.

## Failure boundary

Do not publish acceptance claims if any image edge is lost, Escape needs a
second attempt, the button becomes orphaned or steals focus, normal window
chrome is not restored, repair broadens firewall scope, or local Bonjour
readiness is presented as proof that an iPhone can see the receiver.

The candidate may be published as an explicitly labelled review release after
all automated, package, and source prepublication gates pass. Publication is
not verified or complete until the public-asset/API/download gates pass and the
post-release build report records them. Physical rows may remain PENDING but
must be stated as such in the Release body.

# AeroMirror 0.12.29 — local build report

Date: 2026-09-06. Uncommitted local candidate on top of `01f4d60`, preserving
the earlier .23–.28 work. No installation, source push, tag, GitHub Release,
settings/key reset or Bonjour mutation was performed. This report freezes the
final local artifacts after the real GitHub preflight correction in AUDIT.md.

## Scope and accepted baseline

The user reports the .28 normal-viewer correction works. Installed .28.0 and
its original SHA-256 were checked read-only; the stored .28 Setup/payload also
retain their original hashes. That is not an enumerated physical codec/window
matrix. This patch changes managed update transport/lifetime and repository
identity handling only. The native core and accepted gallery remain unchanged.

## Passed gates

- Shell build: x64 0.12.29.0; no added runtime dependency.
- UpdateTransport: 15 executable cases on the build output and final extracted
  payload shell. Includes UTF-8/BOM/release parsing, exact headers, bounded
  declared/unknown-length body, delayed headers, stalled/progressing body,
  pre-/in-flight cancellation, fresh work after opt-out, 200 owner-cancel /
  worker-complete races, HTTP failure/retry and canceled-file cleanup.
- The opt-in live test performs only an anonymous metadata GET through the
  production permanent-ID endpoint; final packaged .29 parses public 0.12.22.
  It downloads no installer and never constructs a receiver or launches Setup.
  An initial sandbox socket restriction required network access approval; the
  live legacy endpoint then exposed the canonical-URL defect fixed before this
  final build. The rejected preflight payload was not installed or published.
- AutomaticUpdate: full existing staging/hash/transaction contracts plus exact
  canonical and historical Setup URLs; other owner/project paths are rejected.
  Passes on build and final packaged shell.
- ReceiverResilience, RendererShowBoundary and BonjourFirewall on build and
  final packaged shell. Hidden child geometry remains unchanged; no phone
  screenshots, visible test forms or live Bonjour recovery actions.
- ControlWorkQueue: UI result ownership, handle recreation, window-policy
  acknowledgement, late results and 250 disposal races.
- NativeHostContracts. No native source/binary changed; the prior native/Qt
  executable and reproducibility evidence is retained rather than claimed as
  a new native rebuild in this managed-only patch.
- Review package: exact 13 entries, matching shell/core versions and hashes.
- build-native-source: complete prepared .29 source with both existing patches
  and pinned source provenance validated.
- build-installer: embedded input/runtime verification plus /verify-runtime,
  /verify-shortcut-selection, /verify-update-lifecycle and
  /verify-bonjour-recovery; all exit 0, no installation.
- git diff --check.

## Frozen local artifacts

| File | Bytes | SHA-256 |
|---|---:|---|
| AeroMirror-Setup-0.12.29.exe | 1566720 | D93C13C308063F79B40AC8918F50A70B34E4B84129917A033D677A74884D68BF |
| AeroMirror-review-payload-x64-0.12.29.zip | 1260441 | 4A9AB66FF74203212AC53A02CC7636C8E3A42A6A6809889DC38EA89D752DA058 |
| AeroMirror-native-source-0.12.29.zip | 911598 | 64EF569EEEE91F046CDAF0D9B042BC5AD4A339BDE04E0F9BBDD20174E01F98E1 |
| AeroMirror.exe | 835072 | D55D63FC7FDF3E48C1196C7E2B3F5FEF60D1E396A73D46DFA6E9663D13EBB63E |

Unchanged core SHA-256:
`B3EC9500B3E5D8D69A4AD5A7FFA385891446FC1B573F97A1B04BC327806AF36F`.
Network Setup still uses the pinned upstream 1.28.1 runtime, not the complete
1.28.5 engineering stage. No provenance record was relabeled or changed.

## Remaining work

Manual .29 settings close/hide/retry and automatic opt-out/exit behavior on the
user's machine; full H.265/H.264, mixed-window and Bonjour physical rows.
Caption Close still minimizes: its session-specific native disconnect contract
and stale-request tests are the next product task. Full large-module
decomposition remains staged. This is not a public release or a complete
independent security audit of all inherited dependencies.

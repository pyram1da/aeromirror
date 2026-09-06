# AeroMirror 0.12.29 — focused audit continuation

Date: 2026-09-06. The user reports the .28 black-screen correction works.
Preserve that frozen baseline; this pass changes managed update work only.
It is not completion of the whole receiver/installer decomposition or an
independent audit of every third-party source file.

## A29-1 — metadata transport had no explicit whole-transfer or body budget

The previous Check used WebClient.DownloadString with default transport
settings and buffered the entire response before release parsing. The installer
path already enforced separate redirect, size, timeout and hash rules; these
did not constrain the metadata request.

ReleaseMetadataClient now owns metadata I/O: 1 MiB body, no automatic redirect
or decompression, one 30-second linked cancellation deadline across headers
and body, and strict UTF-8 decoding. Request.Abort releases blocked I/O. The
fixed repository, API headers, version parser and installer validation remain.
Local-server executable tests include stalled headers/body and a progressing
body, not only a source assertion that a timeout property exists.

[Microsoft's HttpWebRequest timeout documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.timeout?view=netframework-4.8.1)
distinguishes response acquisition from stream read/write timeouts and notes
DNS timing limits. A whole-transfer abort is therefore used in addition to
those request settings; this is not a hard real-time guarantee on a starved OS.

## A29-2 — abandoned callbacks were safe, but network work continued

The .28 ControlWorkQueue protects delivery and file ownership, not cancellation
of the network operation producing a result. Manual and automatic work now
have separate UpdateWorkScope owners. Actual form close/disposal and application
quit cancel manual I/O; opt-out and quit cancel automatic I/O. Hiding a form
to tray leaves its lifetime intact. Workers dispose their sources only after
completion; cancellation and that disposal are serialized. Existing epoch and
result-queue checks still protect staging and eventual UI handoff.

[Microsoft documents CancellationTokenSource.Dispose as the exception to its
thread-safe operations](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource?view=netframework-4.8.1).
The scope therefore retains active sources through cancellation instead of
disposing a source directly from a form-close callback. Test coverage includes
200 concurrent cancel/complete races and repeated opt-out/re-enable semantics.

## A29-3 — HTTP failure responses need explicit disposal

GetResponse can throw WebException with a response before the normal response
using block is entered. Metadata and installer response acquisition now close
that exceptional response. A local HTTP failure remains a retryable failure;
no automatic retry loop, installer launch, receiver restart or service mutation
is introduced.

## A29-4 — canonical repository assets were rejected after an owner rename

The live metadata check exposed the legacy slug's HTTP 301 to
`/repositories/1324108899/releases/latest`. The public
[repository API](https://api.github.com/repositories/1324108899) confirms
`pyram1da/aeromirror`; its latest release uses that canonical owner in the
Setup URL. The old metadata client followed redirects, but the installer
candidate validator still accepted only the historical Nadejny path. Thus
finding a release was not evidence that its installer could be accepted.

Metadata now goes directly to the confirmed permanent repository ID with
redirects disabled. Exact canonical and historical Setup paths share the same
version/name/HTTPS/size/digest validation. Arbitrary owners and project names
remain rejected. Existing local repository configuration is preserved.
Offline canonical/historical handoffs and the anonymous live metadata check
pass; no Setup was downloaded by that live check. The initial .29 preflight
package was rebuilt before handoff after this finding, not published or installed.

## Caption-close native investigation (not implemented)

The retained prepared native source and repository patch show:

- RendererHostWindow::closeEvent leaves fullscreen, reports minimized,
  calls showMinimized and ignores Close. No remote disconnect is sent.
- uxplay_api.h exposes global stop/discovery/video/PIN operations, not a
  request-correlated stop-current-session operation.
- raop_remove_known_connections delegates to the HTTP worker's removal mailbox.
  That mailbox stores a connection-type bitmask; the worker selects matching
  live connections when it consumes the mask. It carries no requested mirror
  session generation, so wiring a GUI Close directly to it could also select
  a replacement connection before the worker consumes the request.
- raop_stop_httpd instead stops the listener and does not satisfy D-018.

Next native work must carry an immutable session identity from caption Close,
select/drain only that session's control/media workers, acknowledge the exact
request, and leave the listener/ports/DNS-SD intact. Test stale Close versus a
new connection and obtain physical iPhone disconnect evidence. No native bytes
or published assets are changed during this managed pass.

## Coverage and limits

This follow-up adds two focused managed owners (34 application C# files total,
plus the existing installer), not another receiver process or dependency.
Build/packaging evidence belongs in LOCAL_BUILD_REPORT.md. Full physical codec,
Z-order and Bonjour rows remain independent; no new installation or publication
was performed by the agent.

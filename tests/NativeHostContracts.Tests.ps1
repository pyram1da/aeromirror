$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAILED: $Message"
    }
}

function Assert-Match(
    [string]$Text,
    [string]$Pattern,
    [string]$Message
) {
    $options = [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::Singleline
    Assert-True ([regex]::IsMatch($Text, $Pattern, $options)) $Message
}

function Assert-NoMatch(
    [string]$Text,
    [string]$Pattern,
    [string]$Message
) {
    $options = [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::Singleline
    Assert-True (-not [regex]::IsMatch($Text, $Pattern, $options)) $Message
}

function Assert-InOrder(
    [string]$Text,
    [string[]]$Fragments,
    [string]$Message
) {
    $offset = 0
    foreach ($fragment in $Fragments) {
        $index = $Text.IndexOf(
            $fragment, $offset, [StringComparison]::Ordinal)
        Assert-True ($index -ge 0) `
            "$Message (missing or out of order: $fragment)"
        $offset = $index + $fragment.Length
    }
}

function Get-AddedSource([string]$PatchText) {
    $lines = $PatchText -split "`n"
    $added = foreach ($line in $lines) {
        if ($line.StartsWith("+") -and -not $line.StartsWith("+++")) {
            $line.Substring(1).TrimEnd("`r")
        }
    }
    return $added -join "`n"
}

function Get-SourceSlice(
    [string]$Text,
    [string]$Start,
    [string]$End,
    [string]$Name
) {
    $startIndex = $Text.IndexOf($Start, [StringComparison]::Ordinal)
    Assert-True ($startIndex -ge 0) "$Name start marker exists"
    $endIndex = $Text.IndexOf(
        $End, $startIndex + $Start.Length, [StringComparison]::Ordinal)
    Assert-True ($endIndex -gt $startIndex) "$Name end marker follows start"
    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$wrapperPatchPath = Join-Path $projectRoot `
    "native-core\uxplay-windows-headless.patch"
$libPatchPath = Join-Path $projectRoot `
    "native-core\libuxplay-aeromirror.patch"
$sourceBuilderPath = Join-Path $projectRoot "build-native-source.ps1"
$managedRenderingPath = Join-Path $projectRoot `
    "src\Receiver\ReceiverContext.Rendering.cs"
$nativeMethodsPath = Join-Path $projectRoot "src\Interop\NativeMethods.cs"

Assert-True (Test-Path -LiteralPath $wrapperPatchPath -PathType Leaf) `
    "native wrapper patch exists"
Assert-True (Test-Path -LiteralPath $libPatchPath -PathType Leaf) `
    "libuxplay patch exists"
Assert-True (Test-Path -LiteralPath $sourceBuilderPath -PathType Leaf) `
    "native corresponding-source builder exists"
Assert-True (Test-Path -LiteralPath $managedRenderingPath -PathType Leaf) `
    "managed renderer supervision source exists"
Assert-True (Test-Path -LiteralPath $nativeMethodsPath -PathType Leaf) `
    "managed Win32 interop source exists"

$wrapperPatch = Get-Content -LiteralPath $wrapperPatchPath -Raw -Encoding UTF8
$libPatch = Get-Content -LiteralPath $libPatchPath -Raw -Encoding UTF8
$sourceBuilder = Get-Content -LiteralPath $sourceBuilderPath -Raw -Encoding UTF8
$managedRendering = Get-Content -LiteralPath $managedRenderingPath `
    -Raw -Encoding UTF8
$nativeMethods = Get-Content -LiteralPath $nativeMethodsPath -Raw -Encoding UTF8
$wrapperAdded = Get-AddedSource $wrapperPatch
$libAdded = Get-AddedSource $libPatch

# The viewer owns one normal framed top-level HWND and one native child HWND.
Assert-Match $wrapperAdded `
    'class\s+RendererHostWindow\s+final\s*:\s*public\s+QMainWindow' `
    "native Qt viewer host owns the top-level window"
Assert-InOrder $wrapperAdded @(
    'm_videoSurface->setAttribute(Qt::WA_NativeWindow)',
    '(void) m_videoSurface->winId()',
    'm_rendererHost = new RendererHostWindow()',
    'set_video_host_handles(',
    'm_rendererHost->renderHandle()',
    'm_rendererHost->controlHandle()'
) "Qt creates both native handles before registering them with libuxplay"
Assert-Match $wrapperAdded (
    'WM_SYSCOMMAND[\s\S]*SC_MAXIMIZE[\s\S]*' +
    'applyFullscreenState\(true,\s*"caption"\)') `
    "the standard maximize caption enters fullscreen"
Assert-Match $wrapperAdded (
    '(?:VK_ESCAPE|Qt::Key_Escape)[\s\S]*' +
    'applyFullscreenState\(false,\s*"escape"\)') `
    "Escape is an idempotent set-to-normal operation"
Assert-Match $wrapperAdded `
    'applyFullscreenState\(!isFullScreen\(\),\s*"alt-enter"\)' `
    "Alt+Enter toggles only from the native host actual state"
$fullscreenState = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::applyFullscreenState(' `
    'void RendererHostWindow::showRenderer()' `
    'renderer fullscreen state transition'
Assert-InOrder $fullscreenState @(
    'if (desired && !isVisible())',
    'emitFullscreenMarker(desired, before, "unavailable", source)',
    'return;',
    'if (before == desired)',
    'showFullScreen()'
) "a stale fullscreen request cannot reshow a lifecycle-hidden host"
Assert-Match $fullscreenState (
    'const QRect normalCandidate = \(isMinimized\(\)\s*\|\|\s*' +
    'isMaximized\(\)\)\s*' +
    '\? normalGeometry\(\) : geometry\(\);[\s\S]*' +
    'normalCandidate\.isValid\(\)[\s\S]*' +
    'm_normalGeometry = normalCandidate') `
    "fullscreen preserves the current restore geometry from minimized or maximized"
Assert-NoMatch $fullscreenState `
    'if\s*\(\s*!isMinimized\(\)\s*\)' `
    "minimized fullscreen entry does not retain stale restore geometry"

# The IPC grammar carries desired state, never a timing-sensitive toggle.
Assert-Match $wrapperAdded `
    '\^AEROMIRROR_COMMAND video-fullscreen-set state=\(\[01\]\)\$' `
    "wrapper accepts exactly fullscreen state 0 or 1"
Assert-Match $wrapperAdded `
    'request_video_fullscreen_set\(state\)' `
    "wrapper forwards the exact desired fullscreen state"
Assert-NoMatch $wrapperAdded (
    'AEROMIRROR_COMMAND video-fullscreen-toggle|' +
    'request_video_fullscreen_toggle') `
    "wrapper no longer exposes toggle IPC"
Assert-Match $libAdded (
    'int\s+request_video_fullscreen_set\(int state\)[\s\S]*' +
    'state != 0 && state != 1[\s\S]*' +
    'AEROMIRROR_PRESENTATION_FULLSCREEN,\s*\(unsigned int\) state') `
    "libuxplay validates and queues the exact desired state"
Assert-NoMatch $libAdded `
    'video_renderer_toggle_fullscreen|request_video_fullscreen_toggle' `
    "libuxplay no longer toggles sink state"

# Acks have one closed grammar. The Qt host is the sole applied/noop owner.
Assert-Match $wrapperAdded (
    '\\nAEROMIRROR_VIDEO_FULLSCREEN requested=%d actual=%d "\s*' +
    '"result=%s generation=%llu source=%s\\n') `
    "native host frames fullscreen acknowledgements after any unterminated progress line"
Assert-Match $wrapperAdded '"noop"' `
    "idempotent requests acknowledge noop"
Assert-Match $wrapperAdded '"applied"\s*:\s*"unavailable"' `
    "state transitions acknowledge applied or unavailable"
foreach ($source in @(
    '"ipc"',
    '"caption"',
    '"escape"',
    '"alt-enter"',
    '"lifecycle"',
    '"initial"'
)) {
    Assert-True ($wrapperAdded.Contains($source)) `
        "fullscreen acknowledgement source exists: $source"
}
Assert-Match $libAdded (
    'AEROMIRROR_VIDEO_FULLSCREEN requested=%u actual=0 "\s*' +
    '"result=unavailable generation=0 source=ipc') `
    "GLib emits only the exact unavailable acknowledgement when delivery fails"

# GstVideoOverlay binds before READY. The sink retains the whole frame with
# neutral scale and no render rectangle/crop override.
$hostBinding = Get-SourceSlice $libAdded `
    "static void aeromirror_bind_host_to_sink(" `
    "static void aeromirror_request_host_show()" `
    "GstVideoOverlay host binding"
Assert-InOrder $hostBinding @(
    'gst_video_overlay_set_window_handle(',
    'gst_video_overlay_handle_events(GST_VIDEO_OVERLAY(sink), FALSE)',
    '"fullscreen-toggle-mode", (guint) 0',
    '"fullscreen", FALSE',
    '"force-aspect-ratio", TRUE'
) "external D3D sink is embedded, host-controlled, and contain/aspect safe"
Assert-NoMatch $hostBinding (
    'gst_video_overlay_set_render_rectangle|render-rectangle|\bcrop\b|' +
    '"scale-[xy]"') `
    "host binding never crops or applies a non-neutral scale"
Assert-Match $libAdded (
    '"scale-x",\s*\(gfloat\) 1\.0[\s\S]*' +
    '"scale-y",\s*\(gfloat\) 1\.0') `
    "renderer lifecycle resets any legacy scale to neutral"
Assert-InOrder $libPatch @(
    'gst_video_overlay_set_window_handle(',
    'gst_element_set_state (renderer_type[i]->pipeline, GST_STATE_READY)'
) "video HWND is bound before a renderer pipeline reaches READY"

# Fullscreen availability is independent of the optional D3D11 Present proof,
# including the low-latency -vsync no path.
Assert-Match $libAdded `
    'GstElement \*aeromirror_host_sink;' `
    "renderer retains a dedicated host sink"
Assert-Match $libAdded (
    'video_renderer_set_fullscreen\(bool fullscreen\)[\s\S]*' +
    'aeromirror_snapshot_selected_host_sink\(\)[\s\S]*' +
    'AEROMIRROR_WM_RENDERER_FULLSCREEN_SET') `
    "fullscreen IPC uses the dedicated host sink, not Present proof state"
$videoStopPatch = Get-SourceSlice $libPatch `
    ' void video_renderer_stop() {' `
    ' void video_renderer_set_device_model' `
    'video renderer stop patch'
Assert-Match $videoStopPatch `
    '\+\s*aeromirror_request_host_hide\(\);' `
    "renderer stop hides the native host"
$videoDestroyPatch = Get-SourceSlice $libPatch `
    ' void video_renderer_destroy() {' `
    '@@ -789' `
    'video renderer destroy patch'
Assert-Match $videoDestroyPatch `
    '\+\s*aeromirror_request_host_hide\(\);' `
    "renderer destroy hides the native host"
Assert-Match $libAdded (
    'aeromirror_request_host_hide\(\)[\s\S]*' +
    'g_atomic_int_compare_and_exchange\([\s\S]*' +
    '&aeromirror_host_visible_requested, 1, 0\)') `
    "stop and destroy coalesce to one lifecycle hide message"
Assert-Match $wrapperAdded (
    'hideRenderer\(\)[\s\S]*' +
    'applyFullscreenState\(false,\s*"lifecycle"\)') `
    "lifecycle hide publishes the actual normal state before hiding"
$closeEvent = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::closeEvent(QCloseEvent *event)' `
    'MainWindow::MainWindow(' `
    'renderer host close event'
Assert-InOrder $closeEvent @(
    'applyFullscreenState(false, "lifecycle")',
    'AEROMIRROR_VIDEO_WINDOW state=minimized source=caption-close',
    'showMinimized()',
    'event->ignore()'
) "caption close is an explicit minimize-equivalent for the active session"
Assert-True ([regex]::Matches(
        $closeEvent,
        'AEROMIRROR_VIDEO_WINDOW state=minimized source=caption-close').Count -eq
        1) `
    "caption close emits one exact session-dismissal marker before minimizing"
Assert-Match $closeEvent `
    '"\\nAEROMIRROR_VIDEO_WINDOW state=minimized source=caption-close\\n"' `
    "caption close begins on a fresh line even after unterminated native progress output"
Assert-NoMatch $closeEvent `
    'hideRenderer\(\)|(?m)^\s*hide\(\);' `
    "caption close neither hides the HWND nor clears the active-session state"
Assert-Match $libAdded (
    'aeromirror_request_host_show\(\)[\s\S]*' +
    '&aeromirror_host_visible_requested, 0, 1') `
    "a new renderer generation owns the only hidden-to-shown CAS transition"

# Win32 keeps WS_VISIBLE on a minimized top-level window. The shell therefore
# continues to find the caption-minimized host even when taskbar policy changes
# it to WS_EX_TOOLWINDOW, and its tray fullscreen command calls showFullScreen.
$rendererLookup = Get-SourceSlice $managedRendering `
    'private bool TryGetRendererWindow(out IntPtr rendererWindow)' `
    'private void ApplyTopMost()' `
    'managed renderer lookup'
Assert-Match $rendererLookup `
    'NativeMethods\.IsWindowVisible\([^)]+\)' `
    "managed lookup retains visible minimized renderer HWNDs"
Assert-NoMatch $rendererLookup `
    'NativeMethods\.IsIconic\(|NativeMethods\.IsZoomed\(' `
    "managed lookup does not discard minimized or maximized renderer HWNDs"
$taskbarStyle = Get-SourceSlice $nativeMethods `
    'internal static void SetToolWindowStyle(' `
    '[DllImport("user32.dll")]' `
    'managed taskbar style policy'
Assert-InOrder $taskbarStyle @(
    'WS_EX_TOOLWINDOW',
    'WS_EX_APPWINDOW',
    'SetWindowPos('
) "hidden-taskbar policy changes only the HWND extended style"
Assert-NoMatch $taskbarStyle `
    'ShowWindow\(|CloseWindow\(|DestroyWindow\(' `
    "hidden-taskbar policy does not hide or destroy the renderer HWND"
$fullscreenToggle = Get-SourceSlice $managedRendering `
    'private void ToggleStreamWindowFullscreen(bool notifyIfMissing)' `
    'private void ApplyNonCroppingPresentationScale(' `
    'managed tray fullscreen path'
Assert-InOrder $fullscreenToggle @(
    'TryGetRendererWindow(out window)',
    'IsRendererFullscreenWindow(window)',
    'string command = "video-fullscreen-set state="',
    'TryWriteNativeVideoCommand('
) "tray fullscreen can target the caption-minimized renderer"
Assert-Match $wrapperAdded `
    'if \(desired\)[\s\S]*showFullScreen\(\);' `
    "fullscreen entry restores a minimized native viewer"
Assert-Match $wrapperAdded (
    'AEROMIRROR_FULLSCREEN_SOURCE_INITIAL[\s\S]*' +
    '\?\s*"initial"\s*:\s*"ipc"') `
    "initial fullscreen state is not mislabeled as IPC"

# Headless startup never prompts or executes a user-writable Bonjour helper.
Assert-Match $wrapperAdded (
    'if \(m_headless\)\s*\{\s*fprintf\(stderr,[\s\S]*' +
    'AEROMIRROR_BONJOUR_MISSING action=install-required\\n[\s\S]*' +
    'return false;') `
    "headless Bonjour absence emits one stable install-required marker"
Assert-Match $wrapperAdded (
    'const int exitCode = m_headless \? 20 : 0;[\s\S]*' +
    'QCoreApplication::exit\(exitCode\)') `
    "headless Bonjour absence exits deterministically with code 20"
Assert-InOrder $wrapperPatch @(
    'AEROMIRROR_BONJOUR_MISSING action=install-required',
    'int choice = QMessageBox::question('
) "interactive Bonjour prompts remain after the headless early return"
$headlessBonjour = Get-SourceSlice $wrapperPatch `
    "+    if (m_headless) {`n+        fprintf(stderr,`n" `
    '     int choice = QMessageBox::question(' `
    'headless Bonjour branch'
Assert-NoMatch $headlessBonjour `
    'QMessageBox|MdnsResponder::install|mDNSResponder' `
    "headless Bonjour branch neither prompts nor launches bundled installation"

# Runtime paths can contain Cyrillic characters through the Windows profile
# directory. Configure them through the wide Win32 environment API rather
# than passing UTF-8 bytes to qputenv's local-8-bit contract.
Assert-Match $wrapperAdded (
    'setRuntimeEnvironmentVariable[\s\S]*' +
    'SetEnvironmentVariableW\([\s\S]*' +
    'name\.utf16\(\)[\s\S]*value\.utf16\(\)') `
    "Windows runtime environment variables preserve Unicode paths"
foreach ($name in @(
    'GST_PLUGIN_PATH',
    'GST_PLUGIN_PATH_1_0',
    'GST_PLUGIN_SYSTEM_PATH',
    'GST_PLUGIN_SYSTEM_PATH_1_0',
    'GST_PLUGIN_SCANNER',
    'GST_PLUGIN_SCANNER_1_0',
    'GIO_EXTRA_MODULES',
    'FONTCONFIG_PATH',
    'PATH'
)) {
    Assert-True ($wrapperAdded.Contains(
            'QStringLiteral("' + $name + '")')) `
        "Unicode-safe runtime environment includes $name"
}
Assert-NoMatch $wrapperAdded (
    'qputenv\s*\(\s*"(?:GST_PLUGIN_(?:SYSTEM_)?PATH(?:_1_0)?|' +
    'GST_PLUGIN_SCANNER(?:_1_0)?|GIO_EXTRA_MODULES|FONTCONFIG_PATH|PATH)"') `
    "runtime paths never pass through lossy qputenv conversion"
Assert-Match $wrapperAdded (
    'if \(!runtimeEnvironmentReady\)[\s\S]*' +
    'AEROMIRROR_RUNTIME_ENVIRONMENT_FAILED action=exit\\n[\s\S]*' +
    'return 3;') `
    "runtime startup fails closed when a required environment value is rejected"

# The new shared host protocol is present in patch review and corresponding
# source packaging, rather than existing only in a local prepared tree.
Assert-Match $libPatch `
    'diff --git a/aeromirror_host_protocol\.h b/aeromirror_host_protocol\.h' `
    "shared native host protocol is materialized in the libuxplay patch"
$hostProtocolSourceMentions = [regex]::Matches(
    $sourceBuilder,
    '"(?:\?\? )?aeromirror_host_protocol\.h"',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant).Count
Assert-True ($hostProtocolSourceMentions -eq 4) `
    "corresponding-source builder reviews, diffs, and copies the host protocol"

Write-Host "Native host contracts passed."

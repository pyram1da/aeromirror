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
    'm_videoSurface = new RendererVideoSurface(this)',
    'm_controlHandle = static_cast<uintptr_t>(winId())',
    'm_renderHandle = static_cast<uintptr_t>(m_videoSurface->winId())',
    'm_rendererHost = new RendererHostWindow()',
    'set_video_host_handles(',
    'm_rendererHost->renderHandle()',
    'm_rendererHost->controlHandle()'
) "Qt creates both native handles before registering them with libuxplay"
Assert-Match $wrapperAdded (
    'class RendererVideoSurface final : public QWidget[\s\S]*' +
    'setAttribute\(Qt::WA_NativeWindow\)[\s\S]*' +
    'setAttribute\(Qt::WA_PaintOnScreen\)[\s\S]*' +
    'setAttribute\(Qt::WA_NoSystemBackground\)[\s\S]*' +
    'setAutoFillBackground\(false\)[\s\S]*' +
    'QPaintEngine \*paintEngine\(\) const override \{ return nullptr; \}') `
    "the video HWND excludes Qt backing-store painting on Windows"
Assert-NoMatch $wrapperAdded 'm_videoSurface->setAutoFillBackground\(true\)' `
    "Qt cannot auto-fill black over the external video surface"
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
    'void RendererHostWindow::showRenderer(uint64_t generation, uint64_t sessionId)' `
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

# GstVideoOverlay binds only the selected sink after the shell has acknowledged
# a visible, nonzero child HWND and before that pipeline may enter PLAYING. The
# sink retains the whole frame with neutral scale and no geometry workaround.
$hostBindingDefinition =
    "static bool aeromirror_bind_host_to_sink(`n" +
    '        GstElement *sink,'
$hostBindingCommitDefinition =
    'static bool aeromirror_commit_host_binding(gpointer generation) {'
$hostBinding = Get-SourceSlice $libAdded `
    $hostBindingDefinition `
    $hostBindingCommitDefinition `
    "GstVideoOverlay host binding"
Assert-InOrder $hostBinding @(
    'bool bound = false',
    'gpointer render_handle = g_atomic_pointer_get(',
    '&aeromirror_host_bound_generation, NULL',
    'g_mutex_lock(&aeromirror_host_overlay_lock)',
    'gst_video_overlay_set_window_handle(',
    'gst_video_overlay_handle_events(GST_VIDEO_OVERLAY(sink), FALSE)',
    '"fullscreen-toggle-mode", (guint) 0',
    '"fullscreen", FALSE',
    '"force-aspect-ratio", TRUE',
    'bound = g_atomic_pointer_get(&aeromirror_host_render_handle)',
    '&aeromirror_host_bound_render_handle, render_handle',
    'g_mutex_unlock(&aeromirror_host_overlay_lock)',
    'return bound'
) "the selected D3D sink is embedded under the overlay lock and remains contain/aspect safe"
Assert-NoMatch $hostBinding (
    'gst_video_overlay_set_render_rectangle|render-rectangle|\bcrop\b|' +
    '"scale-[xy]"|aeromirror_host_lifecycle_lock|renderer_lock|' +
    'renderer_state_lock') `
    "host binding neither nests lifecycle/renderer locks nor changes presentation geometry"
$hostBindingCommit = Get-SourceSlice $libAdded `
    $hostBindingCommitDefinition `
    'static gpointer aeromirror_request_host_show_locked() {' `
    'host binding generation commit'
Assert-InOrder $hostBindingCommit @(
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_lifecycle_generation',
    '&aeromirror_host_ready_generation',
    '&aeromirror_host_bound_render_handle',
    '&aeromirror_host_show_desired',
    '&aeromirror_host_visible_requested',
    '&aeromirror_host_bound_generation, generation',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)'
) "binding is committed only to the exact visible READY generation and HWND"
Assert-NoMatch $hostBindingCommit `
    'gst_|aeromirror_host_overlay_lock|renderer_lock|renderer_state_lock' `
    "generation commit performs no sink work and holds only the lifecycle lock"
Assert-Match $libAdded (
    '"scale-x",\s*\(gfloat\) 1\.0[\s\S]*' +
    '"scale-y",\s*\(gfloat\) 1\.0') `
    "renderer lifecycle resets any legacy scale to neutral"

# A normal SHOW carries the native lifecycle generation into Qt. Qt retries a
# transiently unavailable child, while the first selected-sink D3D11 Present
# publishes the same generation without waiting on a GUI lock. Only the later
# GUI turn requests VideoOverlay expose through a retained reference.
$hostShow = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::showRenderer(uint64_t generation, uint64_t sessionId)' `
    'void RendererHostWindow::queueRendererReady(' `
    'renderer host show acknowledgement'
Assert-InOrder $hostShow @(
    'if (!generation || (sessionId && sessionId == m_dismissedSessionId))',
    'm_showGeneration = generation;',
    'setAttribute(Qt::WA_ShowWithoutActivating, true)',
    'queueRendererReady(generation)'
) "Qt accepts the native SHOW generation and schedules readiness validation"
Assert-NoMatch $hostShow (
    'SendMessage|SWP_FRAMECHANGED|setGeometry\(|resize\(|' +
    'gst_video_overlay_set_render_rectangle|render-rectangle|\bcrop\b|' +
    'scale-[xy]') `
    "normal SHOW acknowledgement does not block, resize, crop, or scale"
$hostReadyGui = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::queueRendererReady(' `
    'void RendererHostWindow::queueRendererExpose(' `
    'renderer host ready retry'
Assert-InOrder $hostReadyGui @(
    'generation != m_showGeneration',
    'm_hostReadyAcknowledgedGeneration == generation',
    'm_readyRetryPendingGeneration == generation',
    'maximumAttempts = 60',
    'm_readyRetryPendingGeneration = generation',
    'QTimer::singleShot(delayMs, this, [this, generation, attempt]()',
    'm_readyRetryPendingGeneration = 0',
    '!IsIconic(host)',
    'IsWindowVisible(surface)',
    'GetClientRect(surface, &client)',
    'queueRendererReady(generation, attempt + 1)',
    'notify_video_host_shown(',
    'm_hostReadyAcknowledgedGeneration = generation',
    'AEROMIRROR_VIDEO_HOST_SHOW result=%s generation=%llu'
) "Qt de-duplicates retries and acknowledges only a drawable child for the current generation"
Assert-Match $wrapperAdded (
    'void RendererHostWindow::showRenderer\(uint64_t generation, uint64_t sessionId\)[\s\S]*' +
    'm_hostReadyAcknowledgedGeneration = 0;[\s\S]*' +
    'm_readyRetryPendingGeneration = 0;[\s\S]*' +
    'm_surfaceRefreshPendingGeneration = 0;') `
    "a new SHOW cannot inherit readiness or a pending retry from an older generation"
Assert-Match $wrapperAdded (
    'void RendererHostWindow::hideRenderer\(uint64_t generation\)[\s\S]*' +
    'm_showGeneration = generation;[\s\S]*' +
    'm_hostReadyAcknowledgedGeneration = 0;[\s\S]*' +
    'm_readyRetryPendingGeneration = 0;[\s\S]*' +
    'm_surfaceRefreshPendingGeneration = 0;') `
    "native HIDE generation invalidates queued GUI readiness and refresh work"

$hostLifecycleEvents = Get-SourceSlice $wrapperAdded `
    'bool RendererHostWindow::eventFilter(QObject *watched, QEvent *event)' `
    'void RendererHostWindow::closeEvent(QCloseEvent *event)' `
    'renderer host lifecycle events'
Assert-Match $hostLifecycleEvents (
    'QEvent::WinIdChange[\s\S]*scheduleHandleRebind\(\)') `
    "native HWND changes are deferred to the dedicated safe rebind path"
Assert-InOrder $hostLifecycleEvents @(
    'QEvent::Show',
    'QEvent::WindowStateChange',
    'm_showGeneration && isVisible() && !isMinimized()',
    'm_surfaceRefreshPendingGeneration != generation',
    'm_hostReadyAcknowledgedGeneration == generation',
    'm_surfaceRefreshPendingGeneration = generation',
    'QTimer::singleShot(25, this, [this, generation, reexpose]()',
    'm_surfaceRefreshPendingGeneration = 0',
    'generation != m_showGeneration',
    'queueRendererReady(generation)',
    'if (reexpose)',
    'request_video_host_surface_expose('
) "show and restore events coalesce one delayed READY and re-request EXPOSE only after prior readiness"
Assert-NoMatch $hostLifecycleEvents (
    'set_video_host_handles\(|SetWindowPos\(|HWND_TOP|HWND_BOTTOM|' +
    'SetForegroundWindow\(|activateWindow\(') `
    "Qt lifecycle events neither rebind synchronously nor repeat connection z-order changes"

$hostHandleRebind = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::scheduleHandleRebind(unsigned int attempt)' `
    'void RendererHostWindow::emitFullscreenMarker(' `
    'renderer host HWND rebind'
Assert-InOrder $hostHandleRebind @(
    'm_handleRebindScheduled = true',
    'const int delayMs = attempt == 0 ? 0 : 25',
    'QTimer::singleShot(delayMs, this, [this, attempt]()',
    'm_rebindingHandles = true',
    'const uintptr_t previousControl = m_controlHandle',
    'const uintptr_t previousRender = m_renderHandle',
    'static_cast<uintptr_t>(winId())',
    'static_cast<uintptr_t>(m_videoSurface->winId())',
    'IsWindow(reinterpret_cast<HWND>(currentControl))',
    'IsWindow(reinterpret_cast<HWND>(currentRender))',
    'm_controlHandle = currentControl',
    'm_renderHandle = currentRender',
    'm_showGeneration = 0',
    'm_hostReadyAcknowledgedGeneration = 0',
    'm_readyRetryPendingGeneration = 0',
    'm_surfaceRefreshPendingGeneration = 0',
    'set_video_host_handles(currentRender, currentControl)',
    'AEROMIRROR_VIDEO_HOST_REBIND result=requested',
    'm_rebindingHandles = false',
    'if (!valid && attempt < 60)',
    'scheduleHandleRebind(attempt + 1)'
) "Qt validates replacement HWNDs, publishes cached handles first, invalidates stale GUI generations, and retries a transiently invalid replacement"

$nativeHandleRebind = Get-SourceSlice $libAdded `
    'void video_renderer_set_host_handles(' `
    'bool video_renderer_notify_host_shown(' `
    'native HWND rebind'
Assert-InOrder $nativeHandleRebind @(
    'g_mutex_lock(&renderer_state_lock)',
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    'const gboolean show_desired = g_atomic_int_get(',
    '&aeromirror_host_show_desired) != 0',
    'g_atomic_int_set(&aeromirror_host_rebind_in_progress, 1)',
    '&aeromirror_host_show_deferred, show_desired ? 1 : 0',
    'g_atomic_int_set(&aeromirror_host_visible_requested, 0)',
    '&aeromirror_host_render_handle',
    '&aeromirror_host_control_handle',
    '&aeromirror_host_ready_generation, NULL',
    '&aeromirror_host_bound_generation, NULL',
    '&aeromirror_host_bound_render_handle, NULL',
    '&aeromirror_host_present_generation, NULL',
    '&aeromirror_host_expose_posted_generation, NULL',
    '&aeromirror_host_exposed_generation, NULL',
    'aeromirror_next_host_generation_locked()',
    'g_cond_broadcast(&aeromirror_host_ready_cond)',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'g_mutex_lock(&renderer_lock)',
    'gst_object_ref(renderer->pipeline)',
    'gst_object_ref(',
    'renderer->aeromirror_host_sink',
    'g_mutex_unlock(&renderer_lock)',
    'gst_element_get_state(',
    'pipeline, &current_state, &pending_state, 0)',
    '&aeromirror_renderer_playback_ready, 0',
    'pipeline, GST_STATE_NULL',
    'aeromirror_bind_host_to_sink(sink, "replacement-host")',
    'pipeline, GST_STATE_READY',
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    'const gboolean resume_after_rebind =',
    '&aeromirror_host_show_desired) != 0',
    'g_atomic_int_set(&aeromirror_host_rebind_in_progress, 0)',
    'g_atomic_int_set(&aeromirror_host_show_deferred, 0)',
    'if (resume_after_rebind)',
    'gpointer generation = aeromirror_request_host_show_locked()',
    'aeromirror_host_pending_rebind_generation = generation',
    'aeromirror_host_pending_rebind_session_generation =',
    'aeromirror_host_pending_rebind_renderer_id =',
    'aeromirror_host_pending_rebind_target = resume_state',
    'else if (!g_atomic_int_get(&aeromirror_host_show_desired)',
    'AEROMIRROR_WM_RENDERER_HIDE',
    '(uintptr_t) (guintptr) generation, 0',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'g_mutex_unlock(&renderer_state_lock)',
    'if (sink) gst_object_unref(sink)',
    'if (pipeline) gst_object_unref(pipeline)'
) "native rebind invalidates the old generation, releases the D3D11 window at NULL, binds the replacement HWND, returns to READY, and defers resume"
$nativeHandleInvalidate = Get-SourceSlice $nativeHandleRebind `
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)' `
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)' `
    'native HWND invalidation critical section'
Assert-NoMatch $nativeHandleInvalidate `
    'g_mutex_lock\(&renderer_lock\)|aeromirror_host_overlay_lock|gst_video_overlay_' `
    "native HWND invalidation releases the lifecycle lock before retaining or rebinding sinks"
$nativeSinkSnapshot = Get-SourceSlice $nativeHandleRebind `
    'g_mutex_lock(&renderer_lock)' `
    'g_mutex_unlock(&renderer_lock)' `
    'native HWND sink snapshot'
Assert-NoMatch $nativeSinkSnapshot (
    'aeromirror_host_lifecycle_lock|aeromirror_host_overlay_lock|' +
    'gst_video_overlay_|\bsinks\s*\[NCODECS\]') `
    "native rebind retains only the selected pipeline and sink before GStreamer work"
Assert-Match $nativeHandleRebind (
    'GST_STATE_NULL[\s\S]*' +
    'aeromirror_bind_host_to_sink\(sink, "replacement-host"\)[\s\S]*' +
    'GST_STATE_READY[\s\S]*' +
    'aeromirror_request_host_show_locked\(\)') `
    "replacement HWND binding occurs only after NULL and before READY and fresh SHOW"
Assert-NoMatch $nativeHandleRebind (
    'aeromirror_wait_for_(?:current_)?host_ready|g_cond_wait|Sleep|' +
    'SendMessage|gst_video_overlay_set_render_rectangle|render-rectangle|' +
    '\bcrop\b|scale-[xy]') `
    "GUI-thread rebind neither waits for READY nor modifies presentation geometry"
$nativeHandleReconcile = Get-SourceSlice $nativeHandleRebind `
    'const gboolean resume_after_rebind =' `
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)' `
    'native HWND visibility reconciliation'
Assert-NoMatch $nativeHandleReconcile `
    'renderer_lock|aeromirror_host_overlay_lock|gst_video_overlay_' `
    "replacement-HWND SHOW/HIDE reconciliation holds only the lifecycle lock"
$nativeRebindDefinition =
    "static void aeromirror_complete_pending_host_rebind(`n" +
    '        gpointer generation) {'
$nativeRebindResume = Get-SourceSlice $libAdded `
    $nativeRebindDefinition `
    'static void aeromirror_request_host_hide()' `
    'deferred native HWND resume'
Assert-InOrder $nativeRebindResume @(
    'g_mutex_lock(&renderer_state_lock)',
    'aeromirror_host_pending_rebind_generation != generation',
    'g_mutex_lock(&renderer_lock)',
    'gst_object_ref(renderer->pipeline)',
    'gst_object_ref(',
    'renderer->aeromirror_host_sink',
    'g_mutex_unlock(&renderer_lock)',
    'aeromirror_wait_for_host_ready(generation, 1)',
    'aeromirror_bind_host_to_sink(sink, "replacement-host")',
    'aeromirror_commit_host_binding(generation)',
    'gst_element_set_state(pipeline, target_state)',
    '&aeromirror_renderer_playback_ready, 1',
    'aeromirror_host_pending_rebind_generation = NULL',
    'g_mutex_unlock(&renderer_state_lock)'
) "only the exact READY replacement generation rebinds and resumes the selected session"
Assert-NoMatch $nativeRebindResume (
    'gst_element_get_state\s*\(|g_cond_wait|Sleep|SendMessage|' +
    'gst_video_overlay_set_render_rectangle|render-rectangle|\bcrop\b|' +
    'scale-[xy]') `
    "GUI READY completion does not block or modify presentation geometry"
$hostReady = Get-SourceSlice $libAdded `
    'bool video_renderer_notify_host_shown(' `
    'bool video_renderer_expose_host_surface(' `
    'visible host readiness acknowledgement'
Assert-InOrder $hostReady @(
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_lifecycle_generation',
    '&aeromirror_host_visible_requested',
    '&aeromirror_host_ready_generation, generation_token',
    '&aeromirror_host_bound_render_handle',
    '&aeromirror_host_bound_generation, generation_token',
    'g_cond_broadcast(&aeromirror_host_ready_cond)',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'aeromirror_complete_pending_host_rebind(generation_token)',
    'aeromirror_maybe_request_host_expose()'
) "generation-matched host acknowledgement publishes HWND readiness before deferred resume or expose"
Assert-NoMatch $hostReady `
    'gst_video_overlay_expose|g_mutex_lock\(&renderer_lock\)|logger_protocol' `
    "SHOW acknowledgement does not touch a not-yet-created D3D11 swap chain or log from the GUI-callable native path"
$hostExpose = Get-SourceSlice $libAdded `
    'bool video_renderer_expose_host_surface(' `
    'bool video_renderer_abandon_host_surface_expose(uint64_t generation)' `
    'selected sink expose implementation'
Assert-InOrder $hostExpose @(
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_visible_requested',
    '&aeromirror_host_ready_generation',
    '&aeromirror_host_bound_generation',
    '&aeromirror_host_present_generation',
    '&aeromirror_host_expose_posted_generation',
    'generation_token, NULL',
    '&aeromirror_host_exposed_generation, generation_token',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'g_mutex_lock(&renderer_lock)',
    'gst_object_ref(',
    'g_mutex_unlock(&renderer_lock)',
    'GST_IS_VIDEO_OVERLAY(sink)',
    'g_mutex_lock(&aeromirror_host_overlay_lock)',
    '&aeromirror_explicit_expose_generation, generation_token',
    'gst_video_overlay_expose(GST_VIDEO_OVERLAY(sink))',
    '&aeromirror_explicit_expose_generation,',
    'previous_expose_generation',
    'g_mutex_unlock(&aeromirror_host_overlay_lock)',
    'gst_object_unref(sink)'
) "visible host redraw retains the selected sink and serializes expose with HWND binding"
$hostExposeClaim = Get-SourceSlice $hostExpose `
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)' `
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)' `
    'selected sink expose claim'
Assert-NoMatch $hostExposeClaim `
    'renderer_lock|aeromirror_host_overlay_lock|gst_video_overlay_' `
    "generation claim releases the lifecycle lock before sink or VideoOverlay work"
Assert-NoMatch $hostExpose (
    'SendMessage|gst_video_overlay_set_render_rectangle|render-rectangle|' +
    '\bcrop\b|scale-[xy]|logger_protocol') `
    "selected-sink expose neither blocks the GUI nor changes presentation geometry"
$hostExposeRendererSnapshot = Get-SourceSlice $hostExpose `
    'g_mutex_lock(&renderer_lock)' `
    'g_mutex_unlock(&renderer_lock)' `
    'selected sink expose snapshot'
Assert-NoMatch $hostExposeRendererSnapshot `
    'aeromirror_host_lifecycle_lock|aeromirror_host_overlay_lock|gst_video_overlay_' `
    "selected sink snapshot takes a strong reference without nesting or calling VideoOverlay"
$hostExposeOverlay = Get-SourceSlice $hostExpose `
    'g_mutex_lock(&aeromirror_host_overlay_lock)' `
    'g_mutex_unlock(&aeromirror_host_overlay_lock)' `
    'selected sink overlay expose'
Assert-NoMatch $hostExposeOverlay `
    'aeromirror_host_lifecycle_lock|renderer_lock' `
    "VideoOverlay expose runs only under its dedicated lock"
Assert-InOrder $libAdded @(
    'static void aeromirror_recovery_present(',
    '&aeromirror_explicit_expose_generation',
    ': g_atomic_pointer_get(&aeromirror_host_bound_generation)',
    '&aeromirror_host_ready_generation',
    '&aeromirror_host_bound_render_handle',
    '&aeromirror_host_render_handle',
    '&aeromirror_host_present_generation',
    'aeromirror_maybe_request_host_expose()'
) "Present can publish readiness only for its explicit or committed HWND generation"
Assert-NoMatch (Get-SourceSlice $libAdded `
    'static void aeromirror_recovery_present(' `
    'void video_renderer_poll_recovery_present()' `
    'D3D11 Present callback') `
    'g_mutex_lock\(|gst_video_overlay_expose\s*\(\s*GST_VIDEO_OVERLAY|SendMessage' `
    "D3D11 Present never waits on GUI lifecycle work or exposes synchronously"
Assert-InOrder $libAdded @(
    'static void aeromirror_maybe_request_host_expose()',
    '&aeromirror_host_expose_posted_generation',
    'AEROMIRROR_WM_RENDERER_EXPOSE',
    '(uintptr_t) (guintptr) generation'
) "first selected-sink Present posts one generation-bound asynchronous expose"
Assert-Match $wrapperAdded (
    'void RendererHostWindow::queueRendererExpose\(' +
    '[\s\S]*generation != m_showGeneration[\s\S]*' +
    'QTimer::singleShot\(delayMs, this, \[this, generation, attempt\]\(\)') `
    "Qt rejects stale generations and defers expose beyond the Present callback"
Assert-Match $wrapperAdded (
    'queueRendererExpose\(generation, attempt \+ 1\)[\s\S]*' +
    'abandon_video_host_surface_expose\(') `
    "transiently non-drawable GUI state retries then generation-safely rearms"
Assert-Match $wrapperAdded (
    'const int exposed = expose_video_host_surface\([\s\S]*' +
    'AEROMIRROR_VIDEO_HOST_EXPOSE result=%s[\s\S]*' +
    'trigger=present-ready[\s\S]*' +
    'exposed \? "requested" : "rejected"') `
    "the GUI owner records present-triggered requested versus rejected expose without native cross-thread logging"
Assert-Match $wrapperAdded (
    'case AEROMIRROR_WM_RENDERER_EXPOSE:[\s\S]*' +
    'static_cast<uint64_t>\(nativeMessage->wParam\)') `
    "the generation token crosses the native message boundary"
Assert-Match $libAdded (
    'int\s+notify_video_host_shown\([\s\S]*' +
    'video_renderer_notify_host_shown\([\s\S]*' +
    'int\s+expose_video_host_surface\([\s\S]*' +
    'video_renderer_expose_host_surface\([\s\S]*' +
    'int\s+abandon_video_host_surface_expose\([\s\S]*' +
    'video_renderer_abandon_host_surface_expose\([\s\S]*' +
    'int\s+request_video_host_surface_expose\([\s\S]*' +
    'video_renderer_request_host_surface_expose\(') `
    "wrapper-to-renderer API separates readiness, expose, timeout rearm, and restore re-request"

$hostExposeAbandon = Get-SourceSlice $libAdded `
    'bool video_renderer_abandon_host_surface_expose(uint64_t generation)' `
    'bool video_renderer_request_host_surface_expose(uint64_t generation)' `
    'wait-free expose abandonment'
Assert-InOrder $hostExposeAbandon @(
    'gpointer generation_token',
    '&aeromirror_host_lifecycle_generation',
    'generation_token',
    'g_atomic_pointer_compare_and_exchange(',
    '&aeromirror_host_expose_posted_generation',
    'generation_token, NULL'
) "timeout rearm changes only the exact active generation with atomic compare/exchange"
Assert-NoMatch $hostExposeAbandon `
    'g_mutex_|gst_|SendMessage|PostMessage|aeromirror_maybe_request_host_expose' `
    "timeout rearm is wait-free and cannot touch the sink or GUI"

$hostExposeRequest = Get-SourceSlice $libAdded `
    'bool video_renderer_request_host_surface_expose(uint64_t generation)' `
    'static bool aeromirror_post_host_message(' `
    'restore expose request'
Assert-InOrder $hostExposeRequest @(
    'gpointer generation_token',
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_lifecycle_generation',
    'generation_token',
    '&aeromirror_host_visible_requested',
    '&aeromirror_host_exposed_generation, NULL',
    '&aeromirror_host_expose_posted_generation, NULL',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'aeromirror_maybe_request_host_expose()'
) "restore re-request clears only the current visible generation before asynchronously reposting"
Assert-NoMatch $hostExposeRequest `
    'gst_video_overlay_expose|g_mutex_lock\(&renderer_lock\)|SendMessage' `
    "restore re-request cannot expose synchronously or acquire the renderer lock"

Assert-InOrder $hostShow @(
    'isExternalFullscreenForeground(',
    'setAttribute(Qt::WA_ShowWithoutActivating, true)',
    'currentForeground = GetForegroundWindow()',
    'protectedForeground',
    'SWP_NOACTIVATE',
    'SetWindowPos(',
    'host, HWND_NOTOPMOST',
    'host, protectedForeground',
    'host, HWND_BOTTOM',
    'host, HWND_TOP',
    'AEROMIRROR_VIDEO_HOST_FOREGROUND result=%s'
) "ordinary windows are raised without focus theft while fullscreen apps stay above"
Assert-NoMatch $hostShow (
    'SetForegroundWindow|activateWindow\(|' +
    'Qt::WindowStaysOnTopHint') `
    "connection foreground behavior never steals focus or sets Qt always-on-top"
Assert-InOrder $hostShow @(
    'if (placementApplied && hasOrdinaryWindowAbove(host))',
    'const HWND latestForeground = GetForegroundWindow()',
    'isExternalFullscreenForeground(latestForeground, host)',
    'm_connectionForegroundDeferred = true',
    'host, HWND_BOTTOM',
    'const BOOL promoted = SetWindowPos(',
    'host, HWND_TOPMOST',
    'const BOOL demoted = SetWindowPos(',
    'host, HWND_NOTOPMOST',
    '!hasOrdinaryWindowAbove(host)',
    'ShowWindow(host, SW_HIDE)',
    'AEROMIRROR_VIDEO_HOST_FOREGROUND result=%s'
) "only a verified ordinary-window obstruction permits one immediate promotion/demotion"
Assert-Match $hostShow (
    'else if \(!deferActivation && nonTopmost\)[\s\S]*' +
    'SetWindowPos\(\s*host, HWND_TOP,') `
    "an ordinary connection raises once in the normal z-order band without activation"
Assert-Match $hostShow (
    'if \(deferActivation && nonTopmost\)[\s\S]*' +
    'host, HWND_BOTTOM') `
    "a fullscreen foreground forces the automatic viewer below the protected window"
Assert-InOrder $wrapperAdded @(
    'foreground == GetDesktopWindow()',
    'foreground == GetShellWindow()',
    'L"Progman"',
    'L"WorkerW"',
    'GetClientRect(foreground, &clientRect)',
    'ClientToScreen(foreground, &clientTopLeft)',
    'ClientToScreen(foreground, &clientBottomRight)'
) "desktop shell and invisible window borders cannot masquerade as fullscreen apps"
Assert-Match $wrapperAdded (
    'desired && initialRequest[\s\S]*' +
    'isExternalFullscreenForeground\(foreground, host\)[\s\S]*' +
    'emitFullscreenMarker\(desired, before, "deferred", source\)') `
    "automatic initial fullscreen never bypasses an existing fullscreen app"

$nativeShowLocked = Get-SourceSlice $libAdded `
    'static gpointer aeromirror_request_host_show_locked() {' `
    'static gpointer aeromirror_request_host_show() {' `
    'native host locked SHOW lifecycle'
Assert-InOrder $nativeShowLocked @(
    '&aeromirror_host_show_desired, 1',
    '&aeromirror_host_rebind_in_progress',
    '&aeromirror_host_show_deferred, 1',
    '&aeromirror_host_ready_generation, NULL',
    '&aeromirror_host_bound_generation, NULL',
    '&aeromirror_host_present_generation, NULL',
    '&aeromirror_host_expose_posted_generation, NULL',
    'aeromirror_next_host_generation_locked()',
    '&aeromirror_host_visible_requested, 1',
    'AEROMIRROR_WM_RENDERER_SHOW',
    '(uintptr_t) (guintptr) generation',
    '&aeromirror_host_visible_requested, 0',
    'g_cond_broadcast(&aeromirror_host_ready_cond)',
    'return NULL',
    'return generation'
) "locked SHOW returns the exact generation, wakes cancellation waiters, and keeps desired-visible intent after a failed post"
Assert-NoMatch $nativeShowLocked `
    'aeromirror_host_show_desired, 0' `
    "a failed SHOW post cannot discard the desired-visible state needed by rebind recovery"
$nativeShow = Get-SourceSlice $libAdded `
    'static gpointer aeromirror_request_host_show() {' `
    'static void aeromirror_request_host_hide() {' `
    'native host SHOW lock wrapper'
Assert-InOrder $nativeShow @(
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    'gpointer generation = aeromirror_request_host_show_locked()',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'return generation'
) "SHOW serializes the lifecycle transition and returns its generation"
$nativeHostReadyWait = Get-SourceSlice $libAdded `
    'static bool aeromirror_wait_for_host_ready(' `
    'static bool aeromirror_wait_for_current_host_ready(' `
    'bounded native host readiness wait'
Assert-InOrder $nativeHostReadyWait @(
    'const gint64 deadline = g_get_monotonic_time() + timeout_us',
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_lifecycle_generation',
    '&aeromirror_host_show_desired',
    '&aeromirror_host_visible_requested',
    '&aeromirror_host_ready_generation',
    'g_cond_wait_until(',
    '&aeromirror_host_ready_cond',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'return ready'
) "host startup waits only for the exact visible generation and has a finite deadline"
Assert-NoMatch $nativeHostReadyWait `
    'gst_|renderer_lock|renderer_state_lock|SendMessage|Sleep' `
    "host readiness wait cannot hold renderer/GStreamer work or block on GUI calls"
$nativeCurrentHostReadyWait = Get-SourceSlice $libAdded `
    'static bool aeromirror_wait_for_current_host_ready(' `
    $nativeRebindDefinition `
    'replacement-aware host readiness wait'
Assert-InOrder $nativeCurrentHostReadyWait @(
    'if (!generation || deadline_us <= 0) return false',
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_lifecycle_generation',
    '*generation = current_generation',
    '&aeromirror_host_ready_generation',
    'g_cond_wait_until(',
    'deadline_us',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'return ready'
) "startup follows replacement generations against the caller's one absolute deadline"
Assert-NoMatch $nativeCurrentHostReadyWait (
    'gst_|renderer_lock|renderer_state_lock|SendMessage|Sleep|' +
    'g_get_monotonic_time\(\)\s*\+\s*deadline_us') `
    "replacement-aware readiness waiting neither touches the renderer nor extends the deadline"
$nativeHide = Get-SourceSlice $libAdded `
    'static void aeromirror_request_host_hide()' `
    'static guint64 aeromirror_monotonic_us()' `
    'native host HIDE lifecycle'
Assert-InOrder $nativeHide @(
    'g_mutex_lock(&aeromirror_host_lifecycle_lock)',
    '&aeromirror_host_show_desired, 0',
    '&aeromirror_host_bound_generation, NULL',
    '&aeromirror_host_rebind_in_progress',
    '&aeromirror_host_show_deferred, 0',
    '&aeromirror_host_visible_requested, 0',
    'aeromirror_next_host_generation_locked()',
    '&aeromirror_host_ready_generation, NULL',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)',
    'return;',
    '&aeromirror_host_visible_requested, 0',
    'aeromirror_next_host_generation_locked()',
    'AEROMIRROR_WM_RENDERER_HIDE',
    '(uintptr_t) (guintptr) generation',
    'g_mutex_unlock(&aeromirror_host_lifecycle_lock)'
) "HIDE cancels desired visibility, invalidates in-flight work, and avoids posting to the old HWND during rebind"
Assert-Match $nativeHandleRebind (
    'resume_after_rebind =[\s\S]*' +
    'aeromirror_host_show_desired[\s\S]*' +
    'if \(resume_after_rebind\)[\s\S]*' +
    'aeromirror_request_host_show_locked\(\)[\s\S]*' +
    'else if \(!g_atomic_int_get\(&aeromirror_host_show_desired\)[\s\S]*' +
    'AEROMIRROR_WM_RENDERER_HIDE') `
    "replacement HWND reconciliation cannot lose a SHOW retry or a HIDE that arrived during rebind"

$codecSelection = Get-SourceSlice $libAdded `
    'const bool hosted_playback = aeromirror_host_is_configured();' `
    'GstClockTime base_time = gst_element_get_base_time(selected_appsrc);' `
    'generation-retryable host selection'
Assert-InOrder $codecSelection @(
    'gint64 host_ready_deadline = 0',
    'host_generation = aeromirror_request_host_show()',
    'host_ready_deadline = g_get_monotonic_time() +',
    '2000 * G_TIME_SPAN_MILLISECOND',
    'g_mutex_unlock(&renderer_state_lock)',
    'for (;;)',
    'const gint64 remaining_us = host_ready_deadline -',
    'aeromirror_wait_for_current_host_ready(',
    '&host_generation, host_ready_deadline)',
    'g_mutex_lock(&renderer_state_lock)',
    'selection_owned = aeromirror_renderer_session_is_active(',
    'renderer == renderer_used',
    'gpointer current_generation = g_atomic_pointer_get(',
    '&aeromirror_host_lifecycle_generation',
    'aeromirror_wait_for_host_ready(current_generation, 1)',
    'host_generation = current_generation',
    'aeromirror_bind_host_to_sink(',
    'aeromirror_commit_host_binding(host_generation)',
    'if (host_bound) break',
    'g_mutex_unlock(&renderer_state_lock)',
    'g_get_monotonic_time() >= host_ready_deadline',
    'if (host_bound)',
    'selected_pipeline, GST_STATE_PLAYING',
    'if (state_lock_held)',
    'g_mutex_unlock(&renderer_state_lock)'
) "initial selection retries a changed HWND generation until one absolute deadline and starts only under the state barrier"
Assert-NoMatch $codecSelection (
    'gst_video_overlay_set_render_rectangle|render-rectangle|\bcrop\b|' +
    'scale-[xy]|aeromirror_wait_for_current_host_ready\(' +
    '[\s\S]{0,160}(?:remaining_us|2000\s*\*)') `
    "selection neither changes geometry nor restarts the readiness timeout"

$rendererPublication = Get-SourceSlice $libAdded `
    'video_renderer_t *new_renderer =' `
    'gst_object_unref(videosink_element);' `
    'renderer publication'
Assert-Match $rendererPublication `
    'g_mutex_lock\(&renderer_lock\);\s*renderer_type\[i\] = new_renderer;\s*g_mutex_unlock\(&renderer_lock\);' `
    "new renderer pointers are published under renderer_lock"
$hostSinkAssignments = [regex]::Matches(
    $rendererPublication,
    'renderer_type\[i\]->aeromirror_host_sink\s*=\s*retained_host_sink;').Count
$hostSinkPublications = [regex]::Matches(
    $rendererPublication,
    'g_mutex_lock\(&renderer_lock\);\s*' +
    'renderer_type\[i\]->aeromirror_host_sink\s*=\s*' +
    'retained_host_sink;\s*g_mutex_unlock\(&renderer_lock\);').Count
Assert-True ($hostSinkAssignments -eq 2 -and
    $hostSinkPublications -eq $hostSinkAssignments) `
    "every retained host sink is published under renderer_lock"
Assert-Match $libAdded `
    'if \(!sync && !aeromirror_host_is_configured\(\)\)' `
    "low-latency host mode still attaches the D3D11 Present callback"

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
    '-static gboolean gstreamer_video_pipeline_bus_callback' `
    'video renderer destroy patch'
Assert-Match $videoDestroyPatch `
    '\+\s*aeromirror_request_host_hide\(\);' `
    "renderer destroy hides the native host"
Assert-Match $libAdded (
    'aeromirror_request_host_hide\(\)[\s\S]*' +
    '&aeromirror_host_visible_requested, 0[\s\S]*' +
    'AEROMIRROR_WM_RENDERER_HIDE') `
    "stop and destroy coalesce to one lifecycle hide message"
Assert-Match $wrapperAdded (
    'hideRenderer\(uint64_t generation\)[\s\S]*' +
    'applyFullscreenState\(false,\s*"lifecycle"\)') `
    "lifecycle hide publishes the actual normal state before hiding"
$closeEvent = Get-SourceSlice $wrapperAdded `
    'void RendererHostWindow::closeEvent(QCloseEvent *event)' `
    'MainWindow::MainWindow(' `
    'renderer host close event'
Assert-InOrder $closeEvent @(
    'event->ignore()',
    'closeSession(m_sessionId, "caption-close")',
    'if (!sessionId || sessionId != m_sessionId)',
    'request_mirroring_session_close(sessionId, requestId)',
    'm_dismissedSessionId = sessionId',
    'applyFullscreenState(false, "lifecycle")',
    'AEROMIRROR_VIDEO_WINDOW state=closed source=%s',
    'hide()'
) "caption close hides only after the session-only command is admitted"
Assert-Match $wrapperAdded (
    'case AEROMIRROR_WM_RENDERER_CLOSE_SESSION:[\s\S]*' +
    'closeSession\(static_cast<uint64_t>\(nativeMessage->wParam\),\s*"continuity-close"\)') `
    "continuity Close enters the same GUI-owned exact-session handler"
Assert-Match $wrapperAdded 'request_video_session_close\(session\)' `
    "the command reader posts an immutable stream ID instead of capturing a Qt object"
Assert-Match $libAdded (
    'video_renderer_request_session_close\(uint64_t session_id\)[\s\S]*' +
    'AEROMIRROR_WM_RENDERER_CLOSE_SESSION, \(uintptr_t\) session_id, 0') `
    "the internal HWND bridge carries the stream ID unchanged"
Assert-NoMatch $closeEvent 'showMinimized|stop_uxplay|raop_stop_httpd|CLOSESOCKET' `
    "Qt neither minimizes nor directly stops the receiver or its sockets"
Assert-Match $wrapperAdded `
    'sessionId && sessionId == m_dismissedSessionId' `
    "a late SHOW cannot resurrect the dismissed stream"
Assert-Match $libAdded `
    'connection->session_id == session_id[\s\S]*httpd_remove_connection' `
    "only the HTTP owner selects the immutable session ID for close"
Assert-Match $libAdded (
    'aeromirror_request_host_show\(\)[\s\S]*' +
    'aeromirror_next_host_generation_locked\(\)[\s\S]*' +
    '&aeromirror_host_visible_requested, 1') `
    "a new renderer generation owns each hidden-to-shown transition"

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
    'internal static bool SetToolWindowStyle(' `
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

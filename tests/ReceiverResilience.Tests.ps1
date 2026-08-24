param(
    [string]$AssemblyPath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "src"
$sourcePaths = @(
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter "*.cs" -File |
        Sort-Object -Property FullName |
        ForEach-Object { $_.FullName }
)
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot "artifacts\Release\AeroMirror.exe"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAILED: $Message"
    }
}

Assert-True ($sourcePaths.Count -gt 0) "receiver sources exist"
Assert-True (Test-Path -LiteralPath $AssemblyPath) "compiled AeroMirror assembly exists"

$source = [string]::Join(
    [Environment]::NewLine,
    @($sourcePaths | ForEach-Object {
        [IO.File]::ReadAllText($_)
    }))
$lostConnectionUiSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "UI\LostConnectionForm.cs"))
$settingsFormSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "UI\SettingsForm.cs"))
$accessNoteStart = $settingsFormSource.IndexOf(
    "private void UpdateAccessNote()", [StringComparison]::Ordinal)
$accessNoteEnd = $settingsFormSource.IndexOf(
    "private void UpdateStartupChild()", [Math]::Max(0, $accessNoteStart),
    [StringComparison]::Ordinal)
Assert-True ($accessNoteStart -ge 0 -and $accessNoteEnd -gt $accessNoteStart) `
    "the settings access-note policy has a deterministic source boundary"
$accessNoteSource = $settingsFormSource.Substring(
    $accessNoteStart, $accessNoteEnd - $accessNoteStart)
$unknownNetworkAccessNote = $accessNoteSource.IndexOf(
    "else if (!context.IsNetworkProfileKnown)",
    [StringComparison]::Ordinal)
$publicNetworkAccessNote = $accessNoteSource.IndexOf(
    "else if (context.IsPublicNetwork)",
    [StringComparison]::Ordinal)
$unknownNetworkProfileText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0J/RgNC+0YTQuNC70Ywg0YTQuNC30LjRh9C10YHQutC+0Lkg0YHQtdGC0Lgg0L/QvtC60LAg0L3QtSDQvtC/0YDQtdC00LXQu9GR0L0="))
$receiverWillNotStartText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0JHQtdC3IFBJTiDQv9GA0LjRkdC80L3QuNC6"))
$repeatNetworkCheckText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0J/QvtCy0YLQvtGA0LjRgtC1INC/0YDQvtCy0LXRgNC60YMg0YHQtdGC0Lg="))
Assert-True ($unknownNetworkAccessNote -ge 0 -and
    $publicNetworkAccessNote -gt $unknownNetworkAccessNote -and
    $accessNoteSource.Contains($unknownNetworkProfileText) -and
    $accessNoteSource.Contains($receiverWillNotStartText) -and
    $accessNoteSource.Contains($repeatNetworkCheckText)) `
    "an unknown physical network is not described as private and explains the fail-closed PIN policy"
$receiverCoreSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Receiver\ReceiverContext.Core.cs"))
$receiverContextSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Receiver\ReceiverContext.cs"))
$rendererControlsSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Receiver\ReceiverContext.RendererControls.cs"))
$rendererButtonSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "UI\RendererFullscreenButtonForm.cs"))
$bonjourFirewallContextSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Receiver\ReceiverContext.BonjourFirewall.cs"))
$bonjourAssessmentGetterStart = $bonjourFirewallContextSource.IndexOf(
    "private BonjourFirewallAssessment GetBonjourFirewallAssessment()",
    [StringComparison]::Ordinal)
$bonjourAssessmentGetterEnd = $bonjourFirewallContextSource.IndexOf(
    "internal string GetBonjourFirewallDiagnosticLine()",
    [Math]::Max(0, $bonjourAssessmentGetterStart),
    [StringComparison]::Ordinal)
$bonjourRepairStart = $bonjourFirewallContextSource.IndexOf(
    "public void RepairBonjourFirewall(IWin32Window owner)",
    [StringComparison]::Ordinal)
$bonjourRepairEnd = $bonjourFirewallContextSource.IndexOf(
    "private void HandleBonjourFirewallRepairResult()",
    [Math]::Max(0, $bonjourRepairStart),
    [StringComparison]::Ordinal)
Assert-True ($bonjourAssessmentGetterStart -ge 0 -and
    $bonjourAssessmentGetterEnd -gt $bonjourAssessmentGetterStart -and
    $bonjourRepairStart -ge 0 -and
    $bonjourRepairEnd -gt $bonjourRepairStart) `
    "Bonjour assessment and repair have deterministic source boundaries"
$bonjourAssessmentGetterSource = $bonjourFirewallContextSource.Substring(
    $bonjourAssessmentGetterStart,
    $bonjourAssessmentGetterEnd - $bonjourAssessmentGetterStart)
$bonjourRepairSource = $bonjourFirewallContextSource.Substring(
    $bonjourRepairStart, $bonjourRepairEnd - $bonjourRepairStart)
Assert-True (-not $bonjourAssessmentGetterSource.Contains(
        "AssessPrivateMdnsRule()") -and
    $bonjourAssessmentGetterSource.Contains(
        "BeginBonjourFirewallAssessment();") -and
    $bonjourRepairSource.Contains("ThreadPool.QueueUserWorkItem") -and
    $bonjourRepairSource.IndexOf(
        "ThreadPool.QueueUserWorkItem", [StringComparison]::Ordinal) -lt
        $bonjourRepairSource.IndexOf(
            "RepairPrivateMdnsRuleExplicitlyWithUac",
            [StringComparison]::Ordinal) -and
    $bonjourFirewallContextSource.Contains(
        "HandleBonjourFirewallRepairResult();") -and
    $bonjourFirewallContextSource.Contains(
        "ref bonjourFirewallRepairReady")) `
    "Bonjour assessment and explicit UAC repair never wait synchronously on the WinForms thread"
$quoteArgumentStart = $source.IndexOf(
    "private static string QuoteArgument(string value)",
    [StringComparison]::Ordinal)
$quoteArgumentEnd = $source.IndexOf(
    "internal static void Log(string message)",
    [Math]::Max(0, $quoteArgumentStart),
    [StringComparison]::Ordinal)
Assert-True ($quoteArgumentStart -ge 0 -and
    $quoteArgumentEnd -gt $quoteArgumentStart) `
    "Windows argument quoting has a focused implementation boundary"
$quoteArgumentSource = $source.Substring(
    $quoteArgumentStart, $quoteArgumentEnd - $quoteArgumentStart)
Assert-True ($quoteArgumentSource.Contains("if (value == null)") -and
    $quoteArgumentSource.Contains("new StringBuilder(value.Length + 2)") -and
    $quoteArgumentSource.Contains(
        "quoted.Append('\\', backslashCount * 2 + 1);") -and
    $quoteArgumentSource.Contains(
        "quoted.Append('\\', backslashCount * 2);") -and
    -not $quoteArgumentSource.Contains("value.Replace")) `
    "Windows argument quoting doubles slash runs before quotes and the closing quote"
$installerSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "installer\AirPlayReceiverSetup.cs"))
Assert-True (-not $settingsFormSource.Contains(
        "followPhotosMediaCanvas") -and
    -not $settingsFormSource.Contains(
        "FollowPhotosMediaCanvas")) `
    "Photos/media window fitting is automatic and has no user-facing A/B control"
$lostConnectionContextSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Receiver\ReceiverContext.LostConnection.cs"))
$handoffMethodStart = $lostConnectionContextSource.IndexOf(
    "private void CompleteLostConnectionRendererHandoff()")
$handoffMethodEnd = $lostConnectionContextSource.IndexOf(
    "private bool IsFeedbackRendererHandoffCurrent", $handoffMethodStart)
$handoffMethodSource = $lostConnectionContextSource.Substring(
    $handoffMethodStart, $handoffMethodEnd - $handoffMethodStart)
Assert-True ($handoffMethodSource.IndexOf(
        "placeholder.CancelRendererHandoff();") -ge 0 -and
    $handoffMethodSource.IndexOf(
        "placeholder.CancelRendererHandoff();") -lt
        $handoffMethodSource.IndexOf("placeholder.BeginRendererHandoff(") -and
    $handoffMethodSource.Contains(
        "ref lostConnectionRendererHandoffPending, 1, 0")) `
    "a fresh one-shot proof synchronously cancels an older fade and requeues if Begin still loses the race"
$nativePatchPath = Join-Path $projectRoot `
    "native-core\libuxplay-aeromirror.patch"
Assert-True (Test-Path -LiteralPath $nativePatchPath) `
    "pinned libuxplay patch exists"
$nativePatchSource = [IO.File]::ReadAllText($nativePatchPath)
$wrapperPatchPath = Join-Path $projectRoot `
    "native-core\uxplay-windows-headless.patch"
Assert-True (Test-Path -LiteralPath $wrapperPatchPath) `
    "pinned wrapper patch exists"
$wrapperPatchSource = [IO.File]::ReadAllText($wrapperPatchPath)
Assert-True ($nativePatchSource.Contains("DNSServiceProcessResult") -and
    $nativePatchSource.Contains("#define DNSSD_FLAGS_NO_AUTO_RENAME 0x8") -and
    [regex]::Matches(
        $nativePatchSource,
        'DNSSD_FLAGS_NO_AUTO_RENAME, 0').Count -eq 2 -and
    $nativePatchSource.Contains("aeromirror_discovery_capability_announced") -and
    -not $nativePatchSource.Contains("aeromirror_accepting_commands") -and
    $nativePatchSource.Contains("aeromirror_attach_discovery_request") -and
    $nativePatchSource.Contains("AEROMIRROR_DISCOVERY_REFRESH_DEFERRED") -and
    $nativePatchSource.Contains("pid=%lu raop_port=%u airplay_port=%u") -and
    $wrapperPatchSource.Contains("ReadFile(") -and
    $wrapperPatchSource.Contains("rawHeadless") -and
    $wrapperPatchSource.Contains("requestInterruption()")) `
    "reviewed native patches retain callback pumping, coherent identity, loop-reset command persistence, redirected pipe IPC, and graceful stop"
Assert-True ($wrapperPatchSource.Contains(
        'AEROMIRROR_COMMAND video-fullscreen-toggle') -and
    $wrapperPatchSource.Contains('request_video_fullscreen_toggle()') -and
    $wrapperPatchSource.Contains(
        'AEROMIRROR_COMMAND video-scale permille=') -and
    $wrapperPatchSource.Contains(
        'request_video_scale((unsigned int) scale)') -and
    $nativePatchSource.Contains(
        'static gboolean aeromirror_apply_presentation_command') -and
    $nativePatchSource.Contains(
        'static int aeromirror_queue_presentation_command') -and
    $nativePatchSource.Contains('g_idle_source_new()') -and
    $nativePatchSource.Contains(
        'g_source_attach(source, context)') -and
    $nativePatchSource.Contains(
        'video_renderer_toggle_fullscreen(&fullscreen_state)') -and
    $nativePatchSource.Contains(
        'video_renderer_set_scale(command->value)') -and
    $nativePatchSource.Contains(
        'AEROMIRROR_VIDEO_FULLSCREEN state=%d result=%s') -and
    $nativePatchSource.Contains(
        'AEROMIRROR_VIDEO_SCALE permille=%u result=%s')) `
    "fullscreen and Photos scale commands are parsed narrowly and marshalled to the native GLib owner"
Assert-True ($nativePatchSource.Contains(
        'bool video_renderer_toggle_fullscreen(bool *fullscreen)') -and
    $nativePatchSource.Contains(
        '"fullscreen-toggle-mode", (guint) 6') -and
    $nativePatchSource.Contains('"fullscreen", next') -and
    $nativePatchSource.Contains(
        'bool video_renderer_set_scale(unsigned int permille)') -and
    $nativePatchSource.Contains(
        'if (permille < 1000 || permille > 5000) return false;') -and
    $nativePatchSource.Contains('"scale-x", scale') -and
    $nativePatchSource.Contains('"scale-y", scale') -and
    $nativePatchSource.Contains('aeromirror_reset_present_scale();')) `
    "the selected D3D11 sink uses property-backed fullscreen and bounded uniform zoom that resets at renderer start"
Assert-True ($wrapperPatchSource.Contains(
        "QByteArray m_beaconOutputBuffer") -and
    $wrapperPatchSource.Contains(
        "consumeBluetoothBeaconOutput(false)") -and
    $wrapperPatchSource.Contains(
        "consumeBluetoothBeaconOutput(true)") -and
    $wrapperPatchSource.Contains(
        "maximumBufferedBytes = 64 * 1024") -and
    $wrapperPatchSource.Contains(
        "m_beaconOutputBuffer.indexOf('\r')") -and
    $wrapperPatchSource.Contains(
        "m_beaconOutputBuffer.indexOf('\n')") -and
    $wrapperPatchSource.Contains(
        'QByteArray framed("AEROMIRROR_BLE ")') -and
    $wrapperPatchSource.Contains(
        "fwrite(framed.constData(), 1,") -and
    $wrapperPatchSource.Contains(
        "&QProcess::errorOccurred") -and
    $wrapperPatchSource.Contains(
        "error != QProcess::FailedToStart") -and
    $wrapperPatchSource.Contains(
        "helper exited unexpectedly with status") -and
    $wrapperPatchSource.Contains(
        'status == QProcess::CrashExit') -and
    $wrapperPatchSource.Contains(
        "if (m_beacon->state() != QProcess::NotRunning) return;") -and
    $wrapperPatchSource.Contains(
        "stopBluetoothBeacon();") -and
    $wrapperPatchSource.Contains(
        "if (m_beaconStopping || m_beaconFailureReported) return;") -and
    $wrapperPatchSource.Contains(
        "static constexpr qsizetype maximumDetailBytes = 512") -and
    $wrapperPatchSource.Contains(
        'QByteArray("Advertising failed: ")') -and
    $wrapperPatchSource.Contains(
        "m_beaconStopping = true;")) `
    "BLE helper output is buffered into complete stderr lines, while unexpected start/crash failure is bounded and reported exactly once without misclassifying intentional stop"
Assert-True ($nativePatchSource.Contains(
        "static void aeromirror_protocol_marker") -and
    $nativePatchSource.Contains("line[0] = '\n';") -and
    $nativePatchSource.Contains("line[length++] = '\n';") -and
    $nativePatchSource.Contains(
        "fwrite(line, 1, length, stdout);") -and
    [regex]::Matches(
        $nativePatchSource,
        '(?m)^\+\s*aeromirror_protocol_marker\(').Count -ge 8 -and
    [regex]::Matches(
        $nativePatchSource,
        '(?m)^\+\s*LOGI\("AEROMIRROR_(?:DISCOVERY_REFRESH|DNSSD)').Count -eq 0) `
    "all discovery protocol markers use one leading-newline and trailing-newline stdout write so each result stays isolated from adjacent unterminated output"
$registrationPairStart = $nativePatchSource.IndexOf(
    "dnssd_register_services(dnssd_t *dnssd")
$registrationPairEnd = $nativePatchSource.IndexOf(
    "dnssd_process_results(dnssd_t *dnssd)", $registrationPairStart)
Assert-True ($registrationPairStart -ge 0 -and
    $registrationPairEnd -gt $registrationPairStart) `
    "the native paired-registration implementation is present in provenance"
$registrationPairSource = $nativePatchSource.Substring(
    $registrationPairStart,
    $registrationPairEnd - $registrationPairStart)
$prepareRaopIndex = $registrationPairSource.IndexOf(
    "dnssd_prepare_raop_txt(dnssd)")
$prepareAirPlayIndex = $registrationPairSource.IndexOf(
    "dnssd_prepare_airplay_txt(dnssd)")
$firstRegistrationIndex = $registrationPairSource.IndexOf(
    "dnssd_register_raop(dnssd, raop_port)")
Assert-True ($prepareRaopIndex -ge 0 -and
    $prepareAirPlayIndex -gt $prepareRaopIndex -and
    $firstRegistrationIndex -gt $prepareAirPlayIndex -and
    $nativePatchSource.Contains(
        "if (dnssd->airplay_record_initialized) return 0;") -and
    $nativePatchSource.Contains(
        "if (dnssd->raop_record_initialized) return 0;")) `
    "both lifetime TXT records are initialized before the first Bonjour registration attempt so degraded /info remains safe"
Assert-True ([regex]::Matches(
        $nativePatchSource,
        'static const char empty_txt').Count -eq 2 -and
    $nativePatchSource.Contains(
        "if (!dnssd || !dnssd->raop_record_initialized)") -and
    $nativePatchSource.Contains(
        "if (!dnssd || !dnssd->airplay_record_initialized)") -and
    [regex]::Matches(
        $nativePatchSource,
        '\*length = 0;').Count -ge 2) `
    "HTTP TXT getters fail safely before registration preparation and never call Bonjour TXT APIs on an uninitialized record"
Assert-True ($nativePatchSource.Contains(
        "#define DNSSD_SERVICE_LABEL_MAX_BYTES 63") -and
    $nativePatchSource.Contains(
        "DNSSD_SERVICE_LABEL_MAX_BYTES - (2 * hw_addr_len) - 1") -and
    $nativePatchSource.Contains(
        "((unsigned char) name[length] & 0xc0) == 0x80") -and
    $nativePatchSource.Contains(
        'canonical_name = DNSSD_FALLBACK_NAME') -and
    $nativePatchSource.Contains(
        "dnssd->name_len = canonical_name_len") -and
    $nativePatchSource.Contains(
        "AEROMIRROR_SERVICE_NAME input_bytes=%zu registered_bytes=%d") -and
    $nativePatchSource.Contains(
        "raop_label_bytes=%zu truncated=%d")) `
    "one UTF-8-safe canonical receiver label keeps RAOP, AirPlay, and /info within Bonjour's 63-byte NoAutoRename boundary"
$nativeArmStart = $nativePatchSource.IndexOf(
    "+void video_renderer_arm_recovery")
$nativeArmEnd = $nativePatchSource.IndexOf(
    "+void video_renderer_cancel_recovery", $nativeArmStart)
$nativeArmSource = $nativePatchSource.Substring(
    $nativeArmStart, $nativeArmEnd - $nativeArmStart)
$nativeFlushStart = $nativePatchSource.IndexOf(
    " void video_renderer_flush()")
$nativeFlushEnd = $nativePatchSource.IndexOf(
    " void video_renderer_hls_ready()", $nativeFlushStart)
$nativeFlushSource = $nativePatchSource.Substring(
    $nativeFlushStart, $nativeFlushEnd - $nativeFlushStart)
$nativeFeedbackTimerStart = $nativePatchSource.IndexOf(
    " static gboolean feedback_callback")
$nativeFeedbackTimerEnd = $nativePatchSource.IndexOf(
    "@@ -721", $nativeFeedbackTimerStart)
$nativeFeedbackTimerSource = $nativePatchSource.Substring(
    $nativeFeedbackTimerStart,
    $nativeFeedbackTimerEnd - $nativeFeedbackTimerStart)
Assert-True ($nativePatchSource.Contains(
        "#define AEROMIRROR_RECOVERY_PTS_SLOTS 64") -and
    $nativePatchSource.Contains(
        "aeromirror_recovery_pts[i].pts == sink_pts") -and
    -not $nativePatchSource.Contains(
        "aeromirror_recovery_pts[i].pts >= sink_pts")) `
    "native recovery uses a bounded ring of exact post-recovery PTS values"
Assert-True ($nativeArmSource.IndexOf(
        "g_atomic_int_set(&aeromirror_recovery_epoch, 0)") -lt
        $nativeArmSource.IndexOf(
            "aeromirror_reset_recovery_candidates_locked()") -and
    $nativeArmSource.IndexOf(
        "aeromirror_reset_recovery_candidates_locked()") -lt
        $nativeArmSource.LastIndexOf(
            "g_atomic_int_set(&aeromirror_recovery_epoch, (gint) epoch)")) `
    "a new native presentation challenge disarms the old epoch before reset and publishes the new epoch last"
Assert-True (-not $nativeFlushSource.Contains(
        "aeromirror_reset_recovery_candidates") -and
    -not $nativeFlushSource.Contains("video_renderer_cancel_recovery")) `
    "generic RAOP HTTP connection teardown cannot erase an in-flight video proof"
Assert-True ($nativeFeedbackTimerSource.IndexOf(
        "video_present_arm_mutex") -ge 0 -and
    $nativeFeedbackTimerSource.IndexOf(
        "video_present_arm_mutex") -lt
        $nativeFeedbackTimerSource.IndexOf(
            "video_renderer_poll_recovery_present")) `
    "native arm and Present polling share the same controller mutex"
Assert-True ($nativePatchSource.Contains(
        "static volatile gint aeromirror_active_present_proof_ready") -and
    $nativePatchSource.Contains(
        "sync && selected_present_proof_ready ? 1 : 0") -and
    $nativePatchSource.Contains("selected_present_proof_ready =") -and
    $nativePatchSource.Contains(
        "renderer_used->aeromirror_present_proof_ready") -and
    $nativePatchSource.Contains("if (!sync)") -and
    $nativePatchSource.Contains(
        "&aeromirror_active_present_proof_ready) == 1")) `
    "D3D11 presentation capability is atomic and unavailable when video sync is disabled"
Assert-True ([regex]::Matches(
        $nativePatchSource, 'g_signal_handler_disconnect\(').Count -ge 2 -and
    [regex]::Matches(
        $nativePatchSource, 'gst_pad_remove_probe\(').Count -ge 2) `
    "native renderer teardown explicitly detaches both Present signal and sink probe"
Assert-True ($nativePatchSource.Contains(
        "static std::atomic<unsigned int> open_connections(0)") -and
    $nativePatchSource.Contains("open_connections.fetch_add(1)") -and
    $nativePatchSource.Contains("open_connections.compare_exchange_weak")) `
    "feedback polling observes an atomic underflow-safe connection count"

function Get-NativePatchSlice(
        [string]$StartNeedle, [string]$EndNeedle, [string]$Description) {
    $start = $nativePatchSource.IndexOf($StartNeedle)
    $end = $nativePatchSource.IndexOf($EndNeedle, $start + 1)
    Assert-True ($start -ge 0 -and $end -gt $start) `
        "$Description is present in the pinned native patch"
    return $nativePatchSource.Substring($start, $end - $start)
}

function Map-TestPts([uint64]$Raw, [int64]$Offset) {
    [decimal]$value = [decimal]$Raw + [decimal]$Offset
    if ($value -lt 0 -or $value -gt [decimal][uint64]::MaxValue) {
        return $null
    }
    return [uint64]$value
}

$rawTestPts = [uint64]1000000
[int64]$testOffset = 100
$mappedTestPts = @((Map-TestPts $rawTestPts $testOffset))
foreach ($delta in @([int64]20, [int64]30)) {
    $testOffset += $delta
    $mappedTestPts += (Map-TestPts $rawTestPts $testOffset)
}
Assert-True ([string]::Join(',', $mappedTestPts) -eq
    '1000100,1000120,1000150') `
    "PTS retries always remap the immutable remote PTS"
Assert-True ($null -eq (Map-TestPts ([uint64]::MaxValue) 1)) `
    "positive PTS overflow is rejected"
Assert-True ($null -eq (Map-TestPts 0 -1)) `
    "negative PTS underflow is rejected"
Assert-True ((Map-TestPts ([uint64]::Parse('9223372036854775808')) `
        ([int64]::MinValue)) -eq 0) `
    "INT64_MIN magnitude is handled without signed overflow"

$testClock = @{ epoch = [uint64]7; offset = [int64]100 }
$testEpochSnapshot = [uint64]7
if ($testClock.epoch -eq $testEpochSnapshot) { $testClock.offset += 20 }
Assert-True ($testClock.offset -eq 120) `
    "a current clock epoch accepts a correction"
$testClock.epoch = 8
$testClock.offset = 0
if ($testClock.epoch -eq $testEpochSnapshot) { $testClock.offset += 30 }
Assert-True ($testClock.offset -eq 0) `
    "a stale callback cannot correct a reset clock epoch"
$testEpochSnapshot = 8
if ($testClock.epoch -eq $testEpochSnapshot) { $testClock.offset += 5 }
Assert-True ($testClock.offset -eq 5) `
    "the new clock epoch accepts its own correction"

$nativePtsHelperSource = Get-NativePatchSlice `
    "static bool aeromirror_add_signed_ns" `
    "static uint64_t aeromirror_age_ms" `
    "checked signed PTS helper"
Assert-True ($nativePtsHelperSource.Contains(
        "raw_remote_pts > UINT64_MAX - positive") -and
    $nativePtsHelperSource.Contains(
        "(uint64_t) (-(offset_ns + 1)) + 1") -and
    $nativePtsHelperSource.Contains("raw_remote_pts < magnitude")) `
    "the native PTS helper rejects overflow and handles INT64_MIN safely"

$nativeAudioProcessSource = Get-NativePatchSlice `
    'extern "C" void audio_process' `
    'extern "C" void video_process' `
    "native audio timestamp mapper"
Assert-True ($nativeAudioProcessSource.Contains(
        "aeromirror_audio_clock_mutex") -and
    $nativeAudioProcessSource.Contains("__int128 difference") -and
    $nativeAudioProcessSource.Contains("difference > INT64_MAX") -and
    $nativeAudioProcessSource.Contains("difference < INT64_MIN") -and
    $nativeAudioProcessSource.Contains(
        "aeromirror_add_signed_ns(data->ntp_time_remote, audio_offset") -and
    $nativeAudioProcessSource.Contains(
        "data->ntp_time_remote = mapped_audio_pts")) `
    "audio keeps an independent checked local-clock mapping"
$mappedAudioIndex = $nativeAudioProcessSource.IndexOf(
    "data->ntp_time_remote = mapped_audio_pts")
$audioDelayIndex = $nativeAudioProcessSource.IndexOf("switch (data->ct)")
$audioRenderIndex = $nativeAudioProcessSource.IndexOf(
    "audio_renderer_render_buffer", $audioDelayIndex)
if ($audioRenderIndex -lt 0) {
    # The compact patch hunk may omit an unchanged render call after the
    # modified delay switch. Bind the ordering assertion to the effective
    # source contract by requiring that the hunk continues through video_process.
    $audioRenderIndex = $nativeAudioProcessSource.Length
}
Assert-True ($mappedAudioIndex -ge 0 -and
    $audioDelayIndex -gt $mappedAudioIndex -and
    $audioRenderIndex -gt $audioDelayIndex) `
    "audio PTS is mapped before delay selection and rendering"

$nativeVideoProcessSource = Get-NativePatchSlice `
    'extern "C" void video_process' `
    'extern "C" void mirror_video_running' `
    "native video timestamp mapper"
Assert-True ([regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+\s*const uint64_t raw_remote_pts') -and
    [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+\s*uint64_t candidate_pts = 0;') -and
    $nativeVideoProcessSource.Contains(
        "aeromirror_add_signed_ns(raw_remote_pts, offset_snapshot") -and
    $nativeVideoProcessSource.Contains("&candidate_pts") -and
    $nativeVideoProcessSource.Contains(
        "generation_snapshot = callback_clock_epoch") -and
    $nativeVideoProcessSource.Contains(
        "aeromirror_video_clock.epoch == generation_snapshot") -and
    $nativeVideoProcessSource.Contains("count < 10") -and
    [regex]::Matches($nativeVideoProcessSource,
        'aeromirror_active_session_generation\.load\(\)').Count -ge 4) `
    "video retries use immutable raw PTS with bounded, session-checked clock epochs"
Assert-True (-not [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+\s*data->ntp_time_remote\s*=') -and
    -not [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+.*&\(data->ntp_time_remote\)') -and
    -not [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+\s*(?:LOGI|LOGE|LOGW|LOGD)\([^\r\n]*(?:PTS|timestamp|mismatch|invalid)') -and
    -not [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+.*adjust timestamps') -and
    -not [regex]::IsMatch($nativeVideoProcessSource,
        '(?m)^\+.*PTS_INVALID')) `
    "video rendering neither mutates remote PTS nor emits hot per-frame PTS logs"

$nativeMirrorLifecycleSource = Get-NativePatchSlice `
    'extern "C" void mirror_video_running' `
    'extern "C" void video_pause' `
    "mirror session lifecycle"
$videoClockResetIndex = $nativeMirrorLifecycleSource.IndexOf(
    "aeromirror_reset_video_clock(generation)")
$audioClockResetIndex = $nativeMirrorLifecycleSource.IndexOf(
    "aeromirror_reset_audio_clock(generation)")
$rendererHealthResetIndex = $nativeMirrorLifecycleSource.IndexOf(
    "video_renderer_reset_health()")
$sessionPublishIndex = $nativeMirrorLifecycleSource.IndexOf(
    "aeromirror_active_session_generation.store(generation)")
Assert-True ($videoClockResetIndex -ge 0 -and
    $audioClockResetIndex -gt $videoClockResetIndex -and
    $rendererHealthResetIndex -gt $audioClockResetIndex -and
    $sessionPublishIndex -gt $rendererHealthResetIndex) `
    "clock and renderer health state reset before a new session is published"

$nativeClassifierSource = Get-NativePatchSlice `
    "static const char *aeromirror_health_classification" `
    'extern "C" void mirror_media_diagnostic' `
    "passive media health classifier"
$nativeHealthSource = Get-NativePatchSlice `
    "static uint64_t previous_ingress_generation" `
    "if (poll_recovery_present)" `
    "periodic media health report"
$nativeFeedbackSource = Get-NativePatchSlice `
    "static gboolean feedback_callback" `
    "guint feedback_watch_id = g_timeout_add_seconds" `
    "independent feedback timer"
Assert-True ([regex]::Matches(
        $nativePatchSource,
        '(?m)^\+.*LOGI\("AEROMIRROR_VIDEO_HEALTH').Count -eq 1 -and
    $nativePatchSource.Contains(
        "#define AEROMIRROR_VIDEO_HEALTH_INTERVAL_US (2 * SECOND_IN_USECS)") -and
    $nativeFeedbackSource.Contains(
        "health_now_us - aeromirror_last_health_log_us.load() >=") -and
    $nativeFeedbackSource.IndexOf('LOGI("AEROMIRROR_VIDEO_HEALTH') -lt
        $nativeFeedbackSource.IndexOf(
            "aeromirror_last_health_log_us.store(health_now_us)")) `
    "one fixed media health summary is emitted by the independent two-second timer"
$healthLogStart = $nativeHealthSource.IndexOf('LOGI("AEROMIRROR_VIDEO_HEALTH')
$healthLogEnd = $nativeHealthSource.IndexOf(
    "previous_ingress_generation = ingress_generation", $healthLogStart)
Assert-True ($healthLogStart -ge 0 -and $healthLogEnd -gt $healthLogStart) `
    "the fixed media health log statement is present"
$nativeHealthLogSource = $nativeHealthSource.Substring(
    $healthLogStart, $healthLogEnd - $healthLogStart)
$healthFields = @(
    "session=", "geometry=", "vcl=", "vcl_bytes=", "type1=", "type5=",
    "invalid=", "config_pending=", "config_delivered=",
    "config_discarded=", "pause=", "resume=", "option=", "action=",
    "suspended=", "input=", "push_ok=", "push_error=", "sink=",
    "present=", "d_vcl=", "d_input=", "d_push_ok=", "d_push_error=",
    "d_sink=", "d_present=", "pts_retry=", "pts_correction=",
    "pts_invalid=", "ingress_age_ms=", "input_age_ms=", "push_age_ms=",
    "sink_age_ms=", "present_age_ms=", "flow=", "state=", "pending=",
    "proof=", "class=")
Assert-True (($healthFields | Where-Object {
        -not $nativeHealthLogSource.Contains($_) }).Count -eq 0 -and
    [regex]::Matches($nativeHealthLogSource, '%s').Count -eq 1 -and
    $nativeHealthSource.Contains("if (!session_generation ||") -and
    $nativeHealthSource.Contains(
        "aeromirror_active_session_generation.load() !=") -and
    $nativeHealthLogSource.Contains("session_generation,")) `
    "the health line is numeric, fixed-field, and correlated to one live session"
$healthClasses = @(
    "starting", "pipeline-reset", "client-paused", "no-vcl",
    "pre-appsrc", "appsrc-error", "unavailable", "decoder-stall",
    "present-stall", "healthy")
Assert-True (($healthClasses | Where-Object {
        -not $nativeClassifierSource.Contains(('"' + $_ + '"')) }).Count -eq 0) `
    "the diagnostic classifier exposes the complete fixed class allowlist"
$passiveForbidden = @(
    "video_pause(", "video_resume(", "gst_element_set_state", "reset_loop",
    "full_video_reset", "video_renderer_flush", "g_main_loop_quit",
    "gst_buffer_map", "GstVideoCropMeta")
Assert-True (($passiveForbidden | Where-Object {
        $nativeClassifierSource.Contains($_) -or $nativeHealthSource.Contains($_)
    }).Count -eq 0) `
    "health reporting and classification remain observational only"
$privacyForbidden = @(
    "artist", "album", "track_title", "coverart", "location=", "path=",
    "uri=", "url=", "payload=", "pixel", "crop", "%p")
Assert-True (($privacyForbidden | Where-Object {
        $nativeHealthSource.Contains($_) }).Count -eq 0) `
    "health reporting contains no media content or identifying paths"

$nativePresentSource = Get-NativePatchSlice `
    "static void aeromirror_recovery_present" `
    "void video_renderer_poll_recovery_present" `
    "D3D11 Present callback"
$presentForbidden = @(
    "g_mutex_", "logger_log", "LOG", "g_get_monotonic_time",
    "gst_element_", "g_object_", "malloc", "calloc", "realloc", "free")
Assert-True ($nativePresentSource.Contains(
        "g_atomic_int_inc(&aeromirror_health_present_count)") -and
    ($presentForbidden | Where-Object {
        $nativePresentSource.Contains($_) }).Count -eq 0) `
    "the Present callback publishes only atomic observations"
Assert-True ([regex]::Matches($nativePatchSource,
        '(?ms)^\+\s*gst_app_src_push_buffer[^\r\n]*\r?\n^\+\s*aeromirror_health_note_push\(flow_return\);').Count -eq 2) `
    "every added appsrc push result is unconditionally counted"

$nativeMirrorThreadSource = Get-NativePatchSlice `
    "diff --git a/lib/raop_rtp_mirror.c b/lib/raop_rtp_mirror.c" `
    "diff --git a/renderers/video_renderer.c b/renderers/video_renderer.c" `
    "mirror media packet loop"
$configMismatchIndex = $nativeMirrorThreadSource.IndexOf(
    "MIRROR_CONFIG_RESULT_MISMATCH_DISCARDED")
$configVideoProcessIndex = $nativeMirrorThreadSource.IndexOf(
    "callbacks.video_process", $configMismatchIndex)
$configDeliveredIndex = $nativeMirrorThreadSource.IndexOf(
    "MIRROR_CONFIG_RESULT_MATCH_DELIVERED", $configVideoProcessIndex)
Assert-True ($nativeMirrorThreadSource.Contains(
        "MIRROR_CONFIG_RESULT_PENDING") -and
    $configMismatchIndex -ge 0 -and
    $configVideoProcessIndex -gt $configMismatchIndex -and
    $configDeliveredIndex -gt $configVideoProcessIndex -and
    $nativePatchSource.Contains(
        "event->type1_packets > previous_type1 ||") -and
    $nativePatchSource.Contains(
        "event->action != MIRROR_PACKET_ACTION_NONE")) `
    "config and pause/resume evidence is durable and delivery is reported only after video processing"
$configPendingIndex = $nativeMirrorThreadSource.IndexOf(
    "MIRROR_CONFIG_RESULT_PENDING")
$configPendingWindowStart = [Math]::Max(0, $configPendingIndex - 180)
$configPendingWindowLength = [Math]::Min(
    280, $nativeMirrorThreadSource.Length - $configPendingWindowStart)
$configPendingWindow = $nativeMirrorThreadSource.Substring(
    $configPendingWindowStart, $configPendingWindowLength)
Assert-True ($configPendingIndex -ge 0 -and
    $configPendingWindow.Contains("MIRROR_PACKET_ACTION_NONE")) `
    "the type1 pending callback cannot double-count a pause or resume action"
$legacyGeometryFormat =
    "AEROMIRROR_VIDEO_GEOMETRY width0=%u height0=%u source=%ux%u aux=%ux%u encoded=%ux%u"
Assert-True ([regex]::Matches($nativePatchSource,
        [regex]::Escape($legacyGeometryFormat)).Count -eq 1 -and
    $nativePatchSource.Contains(
        "AEROMIRROR_VIDEO_DIAGNOSTIC_GEOMETRY geometry=%llu option=0x%02x action=%u suspended=%u")) `
    "the managed geometry contract remains byte-for-byte stable while diagnostics use a separate marker"
$upstreamLockPath = Join-Path $projectRoot "UPSTREAM.lock"
$nativeProvenancePath = Join-Path $projectRoot `
    "native-core\source-provenance.json"
$wrapperPatchPath = Join-Path $projectRoot `
    "native-core\uxplay-windows-headless.patch"
Assert-True (Test-Path -LiteralPath $upstreamLockPath) `
    "the pinned runtime version contract exists"
Assert-True (Test-Path -LiteralPath $nativeProvenancePath) `
    "the native source provenance contract exists"
Assert-True (Test-Path -LiteralPath $wrapperPatchPath) `
    "the pinned wrapper patch exists"
$upstreamLock = [IO.File]::ReadAllText($upstreamLockPath)
$nativeProvenance = Get-Content -LiteralPath $nativeProvenancePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$wrapperPatchSource = [IO.File]::ReadAllText($wrapperPatchPath)
$runtimeGStreamerVersionMatch = [regex]::Match(
    $upstreamLock, '(?m)^runtime\.gstreamer=([0-9]+\.[0-9]+\.[0-9]+)$')
$buildGStreamerVersionMatch = [regex]::Match(
    $upstreamLock, '(?m)^build\.gstreamer=([0-9]+\.[0-9]+\.[0-9]+)$')
Assert-True ($runtimeGStreamerVersionMatch.Success -and
    $buildGStreamerVersionMatch.Success -and
    $runtimeGStreamerVersionMatch.Groups[1].Value -eq
        [string]$nativeProvenance.runtimeGStreamerVersion -and
    $buildGStreamerVersionMatch.Groups[1].Value -eq
        [string]$nativeProvenance.buildGStreamerVersion -and
    [version]$nativeProvenance.runtimeGStreamerVersion -ge
        [version]"1.28.0" -and
    $nativeProvenance.runtimeGStreamerCoreSha256 -match '^[0-9a-f]{64}$' -and
    $nativeProvenance.runtimeWasapi2PluginSha256 -match '^[0-9a-f]{64}$' -and
    $nativeProvenance.runtimeWasapi2RequiredProperty -eq
        "continue-on-error") `
    "redistributed and build-time GStreamer contracts are distinct and pinned"
Assert-True (-not $source.Contains("GetOrCreateReceiverDeviceId")) `
    "an upgrade must not invent a replacement AirPlay device ID"
Assert-True ($source.Contains("GetSavedReceiverDeviceId")) `
    "a previously observed UxPlay device ID is reused"
Assert-True ($source.Contains("randomly-generated) MAC address")) `
    "the exact first-run UxPlay device ID is captured"
Assert-True (-not [regex]::IsMatch($source, 'WaitForExit\s*\(\s*\)')) `
    "core shutdown contains no unbounded WaitForExit call"
Assert-True ($source.Contains(
    "WorkingDirectory = Path.GetDirectoryName(installerPath)")) `
    "the updater launches Setup outside the installed application directory"
Assert-True ($settingsFormSource.Contains(
        'Arguments = "/update /delete-source"') -and
    $installerSource.Contains("ShouldRunAutomaticInstall(") -and
    $installerSource.Contains("RunAutomaticInstall(updateRequested);") -and
    $installerSource.Contains(
        "InstallerOperations.GetShortcutSelection(true)") -and
    $installerSource.Contains("SetupVersion.Build + 1") -and
    $installerSource.Contains(
        "AeroMirror relaunched after automatic install.")) `
    "application updates and installed-version reinstalls run without the option form, preserve shortcut choices, retain version-independent downgrade verification, and relaunch AeroMirror"
Assert-True ($source.Contains("discoveryRefreshAfterNetworkCheck")) `
    "manual discovery refresh survives an unavailable physical network"
$manualDiscoveryStart = $receiverContextSource.IndexOf(
    "bool discoveryRefreshPending =")
$manualDiscoveryEnd = $receiverContextSource.IndexOf(
    "if (startAfterNetworkCheck)", $manualDiscoveryStart)
Assert-True ($manualDiscoveryStart -ge 0 -and
    $manualDiscoveryEnd -gt $manualDiscoveryStart) `
    "manual discovery refresh has a focused post-network-check boundary"
$manualDiscoverySource = $receiverContextSource.Substring(
    $manualDiscoveryStart,
    $manualDiscoveryEnd - $manualDiscoveryStart)
Assert-True ($manualDiscoverySource.Contains(
        'ScheduleRestart(') -and
    $manualDiscoverySource.Contains(
        '"manual discovery refresh after network check"') -and
    -not $manualDiscoverySource.Contains(
        "TryRequestNativeDiscoveryRefresh(")) `
    "the explicit Restart Discovery command fully restarts DNS-SD and its separately bound BLE helper"
Assert-True ($source.Contains("physicalNetworkReady")) `
    "receiver startup requires a confirmed physical IPv4 address"
Assert-True ($source.Contains('$_.AddressState -eq ''Preferred''')) `
    "BLE binding excludes tentative or deprecated physical IPv4 addresses"
Assert-True ($source.Contains('-not $_.SkipAsSource')) `
    "BLE binding excludes physical IPv4 addresses marked SkipAsSource"
Assert-True ($source.Contains("AEROMIRROR_DNSSD_READY")) `
    "native DNS-SD readiness marker is observed"
Assert-True ($source.Contains("AEROMIRROR_DNSSD_DEGRADED")) `
    "native DNS-SD degradation marker is observed"
Assert-True ($source.Contains(
        "AEROMIRROR_DISCOVERY_REFRESH_CAPABILITY version=1") -and
    $source.Contains("RedirectStandardInput = true") -and
    $source.Contains(
        "AEROMIRROR_COMMAND refresh-discovery request=") -and
    $source.Contains("TryRequestNativeDiscoveryRefresh(") -and
    $source.Contains("coreDiscoveryRefreshPendingRequest") -and
    $source.Contains("coreDiscoveryRefreshPendingPid") -and
    $source.Contains("coreDiscoveryRefreshPendingPort") -and
    $source.Contains("ResetNativeDiscoveryRefreshForProcessLifecycle") -and
    $source.Contains("DetachCoreProcessForLifecycle") -and
    $source.Contains("object.ReferenceEquals(coreProcess, process)") -and
    $source.Contains("Native discovery refresh completed in PID")) `
    "capable cores use request-correlated same-PID and same-port discovery refresh IPC"
$nativeRefreshStart = $receiverCoreSource.IndexOf(
    "private bool TryRequestNativeDiscoveryRefresh(")
$nativeRefreshLock = $receiverCoreSource.IndexOf(
    "lock (coreCommandSync)", $nativeRefreshStart)
$nativeRefreshEnd = $receiverCoreSource.IndexOf(
    "private void ClearNativeDiscoveryRefreshRequest", $nativeRefreshStart)
Assert-True ($nativeRefreshStart -ge 0 -and
    $nativeRefreshLock -gt $nativeRefreshStart -and
    $nativeRefreshEnd -gt $nativeRefreshLock) `
    "native refresh request has a focused synchronized process boundary"
$nativeRefreshBeforeLock = $receiverCoreSource.Substring(
    $nativeRefreshStart, $nativeRefreshLock - $nativeRefreshStart)
$nativeRefreshUnderLock = $receiverCoreSource.Substring(
    $nativeRefreshLock, $nativeRefreshEnd - $nativeRefreshLock)
Assert-True (-not $nativeRefreshBeforeLock.Contains("process.Id") -and
    $nativeRefreshUnderLock.Contains("try { processId = process.Id; }") -and
    $nativeRefreshUnderLock.Contains(
        "object.ReferenceEquals(coreProcess, process)")) `
    "same-process refresh never dereferences a potentially disposed Process before synchronized identity validation"
Assert-True ([regex]::IsMatch(
        $receiverCoreSource,
        'ScheduleRestart\s*\(\s*"physical network changed"') -and
    [regex]::IsMatch(
        $receiverCoreSource,
        'ScheduleRestart\s*\(\s*"deferred physical network change"')) `
    "a real physical IPv4 change still restarts the core so the separate BLE helper receives the new address"
Assert-True ($source.Contains("AEROMIRROR_BLE")) `
    "native BLE marker is observed"
$stderrHandlerStart = $receiverCoreSource.IndexOf(
    "process.ErrorDataReceived += delegate")
$stderrHandlerEnd = $receiverCoreSource.IndexOf(
    "process.BeginOutputReadLine()", $stderrHandlerStart)
Assert-True ($stderrHandlerStart -ge 0 -and
    $stderrHandlerEnd -gt $stderrHandlerStart) `
    "native stderr handler has a focused implementation boundary"
$stderrHandlerSource = $receiverCoreSource.Substring(
    $stderrHandlerStart, $stderrHandlerEnd - $stderrHandlerStart)
Assert-True ($stderrHandlerSource.Contains(
        "ObserveCoreDiscoveryMarker(processId, e.Data);") -and
    $stderrHandlerSource.IndexOf(
        "ObserveCoreDiscoveryMarker(processId, e.Data);") -lt
        $stderrHandlerSource.IndexOf(
            'Log("core[" + processId + "]/stderr:')) `
    "PID-scoped BLE health markers are consumed from their real stderr channel before diagnostic logging"
Assert-True ($source.Contains("Discovery registration: DNS-SD=")) `
    "support diagnostics expose native discovery registration"
Assert-True ($source.Contains(
        "SystemEvents.SessionSwitch += OnSessionSwitch") -and
    $source.Contains(
        "SystemEvents.SessionSwitch -= OnSessionSwitch")) `
    "long-idle discovery maintenance observes and releases the Windows session-unlock event"
Assert-True ($source.Contains(
        "private const int IdleDiscoveryFirstRenewalMinutes = 10") -and
    $source.Contains(
        "private const int IdleDiscoveryRecurringRenewalMinutes = 20") -and
    $source.Contains(
        "private const int IdleDiscoveryLegacyRestartLimit = 2") -and
    $source.Contains('"session-unlock discovery refresh"')) `
    "long-idle discovery has one initial stage, a recurring stage, and a bounded legacy-restart allowance"
$automaticRenewalHandlerStart = $receiverCoreSource.IndexOf(
    "private void HandleAutomaticDiscoveryMaintenance")
$automaticRenewalHandlerEnd = $receiverCoreSource.IndexOf(
    "private static SessionUnlockDiscoveryAction", $automaticRenewalHandlerStart)
Assert-True ($automaticRenewalHandlerStart -ge 0 -and
    $automaticRenewalHandlerEnd -gt $automaticRenewalHandlerStart) `
    "automatic discovery maintenance has a focused implementation boundary"
$automaticRenewalHandlerSource = $receiverCoreSource.Substring(
    $automaticRenewalHandlerStart,
    $automaticRenewalHandlerEnd - $automaticRenewalHandlerStart)
Assert-True ($automaticRenewalHandlerSource.Contains(
        "EvaluateAutomaticDiscoveryRenewal(") -and
    $automaticRenewalHandlerSource.Contains(
        "ref clientActivityGraceDueTicks") -and
    $automaticRenewalHandlerSource.Contains(
        'TryRequestNativeDiscoveryRefresh(') -and
    $automaticRenewalHandlerSource.Contains(
        "nextCompletedRenewals <=") -and
    $automaticRenewalHandlerSource.Contains(
        "IdleDiscoveryLegacyRestartLimit") -and
    $automaticRenewalHandlerSource.Contains(
        "ArmIdleDiscoveryRenewalIfAvailable();") -and
    [regex]::Matches(
        $automaticRenewalHandlerSource,
        'ScheduleRestart\s*\(\s*"idle discovery renewal"').Count -eq 1) `
    "timed renewal prefers same-PID refresh, bounds legacy restart fallback, and rearms recurring maintenance"
$unlockHandlerStart = $source.IndexOf(
    "private void HandleSessionUnlockDiscoveryRefresh")
$unlockHandlerEnd = $source.IndexOf(
    "private void HandleLostConnectionRecovery", $unlockHandlerStart)
Assert-True ($unlockHandlerStart -ge 0 -and
    $unlockHandlerEnd -gt $unlockHandlerStart) `
    "session-unlock discovery maintenance has a focused implementation boundary"
$unlockHandlerSource = $source.Substring(
    $unlockHandlerStart, $unlockHandlerEnd - $unlockHandlerStart)
$unlockHandlerLockIndex = $unlockHandlerSource.IndexOf(
    "lock (postSessionMaintenanceSync)")
$unlockHandlerLockedDueReadIndex = $unlockHandlerSource.IndexOf(
    "ref sessionUnlockDiscoveryRefreshDueTicks", $unlockHandlerLockIndex)
$unlockHandlerEvaluateIndex = $unlockHandlerSource.IndexOf(
    "EvaluateSessionUnlockDiscoveryRefresh(")
Assert-True ($unlockHandlerLockIndex -ge 0 -and
    $unlockHandlerLockedDueReadIndex -gt $unlockHandlerLockIndex -and
    $unlockHandlerLockedDueReadIndex -lt $unlockHandlerEvaluateIndex -and
    $unlockHandlerSource.IndexOf(
        "now = DateTime.UtcNow;", $unlockHandlerLockIndex) -gt
        $unlockHandlerLockedDueReadIndex) `
    "unlock maintenance rechecks the newest settle deadline under its serialization lock"
$unlockEventStart = $receiverCoreSource.IndexOf(
    "private void OnSessionSwitch(")
Assert-True ($unlockEventStart -ge 0) `
    "the Windows session-unlock callback has a focused implementation boundary"
$unlockEventSource = $receiverCoreSource.Substring($unlockEventStart)
$unlockEventLockIndex = $unlockEventSource.IndexOf(
    "lock (postSessionMaintenanceSync)")
$unlockEventDueIndex = $unlockEventSource.IndexOf(
    "ref sessionUnlockDiscoveryRefreshDueTicks")
$unlockEventPendingIndex = $unlockEventSource.IndexOf(
    "ref sessionUnlockDiscoveryRefreshPending")
Assert-True ($unlockEventLockIndex -ge 0 -and
    $unlockEventDueIndex -gt $unlockEventLockIndex -and
    $unlockEventPendingIndex -gt $unlockEventDueIndex) `
    "a newer unlock publishes its settle deadline and pending state atomically with timer consumption"
$unlockRefreshGateIndex = $unlockHandlerSource.IndexOf(
    "if (action != SessionUnlockDiscoveryAction.Refresh)")
$unlockRenewalConsumeIndex = $unlockHandlerSource.LastIndexOf(
    "ref idleDiscoveryRenewalUsed")
$unlockScheduleIndex = $unlockHandlerSource.IndexOf(
    'ScheduleRestart(')
Assert-True ($unlockHandlerSource.Contains(
        "EvaluateSessionUnlockDiscoveryRefresh(") -and
    $unlockHandlerSource.Contains("ref coreSocketsReady") -and
    $unlockHandlerSource.Contains("ref coreDnsSdStatus") -and
    $unlockHandlerSource.Contains("ref coreBleStatus") -and
    $unlockHandlerSource.Contains(
        "dnsSdStatus == 1 || bleStatus == 1") -and
    $unlockHandlerSource.Contains("localDiscoveryReady") -and
    $unlockHandlerSource.Contains("physicalNetworkReady") -and
    $unlockHandlerSource.Contains("networkProfileKnown") -and
    $unlockHandlerSource.Contains(
        "FirstNumericIpv4(physicalNetworkAddresses)") -and
    $unlockRefreshGateIndex -ge 0 -and
    $unlockRefreshGateIndex -lt $unlockRenewalConsumeIndex -and
    $unlockRenewalConsumeIndex -lt $unlockScheduleIndex) `
    "only the deterministic Refresh action advances the recurring count and may schedule a bounded legacy restart"
Assert-True (-not $source.Contains("post-session discovery renewal")) `
    "a completed session does not force an unconditional core restart"
Assert-True ($source.Contains('parts.Add("-reset 15")')) `
    "UxPlay receives its upstream fifteen-second lost-client reset bound"
$systemResetIndex = $source.IndexOf('parts.Add("-reset 15")')
$advancedArgumentsIndex = $source.IndexOf(
    'parts.Add(settings.AdvancedArguments.Trim())')
Assert-True ($systemResetIndex -lt $advancedArgumentsIndex) `
    "advanced UxPlay arguments can override the system reset bound"
$managedAudioSinkIndex = $source.IndexOf(
    '"wasapi2sink continue-on-error=true"')
Assert-True ($managedAudioSinkIndex -ge 0 -and
    $managedAudioSinkIndex -lt $advancedArgumentsIndex) `
    "the resilient Windows audio sink is applied before advanced overrides"
Assert-True (-not $source.Contains('parts.Add("-nohold")')) `
    "the receiver does not allow a new client to preempt an active session"
Assert-True (-not $source.Contains('parts.Add("-p ') -and
    $source.Contains("AEROMIRROR_HTTP_READY") -and
    $source.Contains("AEROMIRROR_HTTP_FAILED")) `
    "the managed receiver verifies native HTTP lifecycle markers without pinning a speculative fixed port"
Assert-True ($nativePatchSource.Contains(
        'AEROMIRROR_HTTP_READY stage=initial port=%u') -and
    $nativePatchSource.Contains(
        'AEROMIRROR_HTTP_READY stage=reset port=%u') -and
    $nativePatchSource.Contains(
        'AEROMIRROR_HTTP_FAILED stage=reset expected_port=%u')) `
    "the pinned native patch checks initial/reset HTTP binding"
$teardownHunk = [regex]::Match(
    $nativePatchSource,
    '(?ms)^@@ -1265.*?(?=^diff --git |\z)')
Assert-True ($teardownHunk.Success -and
    $teardownHunk.Value.Contains(
        'AEROMIRROR_TEARDOWN audio=%d video=%d disconnect=client-managed') -and
    -not $teardownHunk.Value.Contains(
        'http_response_set_disconnect(response, 1);')) `
    "type-specific TEARDOWN does not force an immediate server-side disconnect"
Assert-True ($wrapperPatchSource.Contains(
        'if (m_headless || !m_argumentOverride.isEmpty())') -and
    $wrapperPatchSource.Contains(
        'AEROMIRROR_ARGUMENTS_PASSTHROUGH mode=external')) `
    "the headless wrapper preserves externally supplied renderer arguments"
Assert-True ($nativePatchSource.Contains(
        'AEROMIRROR_MIRROR_ONLY_FEATURES_READY') -and
    $nativePatchSource.Contains(
        'dnssd_set_airplay_features(dnssd,  1, 0)') -and
    $nativePatchSource.Contains(
        'dnssd_set_airplay_features(dnssd,  5, 0)') -and
    $nativePatchSource.Contains(
        'dnssd_set_airplay_features(dnssd, 13, 0)') -and
    $nativePatchSource.Contains(
        'plist_new_uint(0x25D)')) `
    "the native receiver clears only unsupported photo, slideshow, and photo-preload declarations"
Assert-True ($source.Contains('parts.Add("-vsync no")') -and
    -not $source.Contains('parts.Add("-al 0.05")')) `
    "the interactive profile disables timestamp scheduling without the old aggressive audio buffer"
Assert-True ($source.Contains('parts.Add("-vd d3d11h264dec")') -and
    $source.Contains('parts.Add("-vs d3d11videosink")') -and
    $source.Contains('parts.Add("-vd d3d12h264dec")') -and
    $source.Contains('parts.Add("-vs d3d12videosink")')) `
    "an explicit Direct3D choice pins both decoder and sink for a valid compatibility test"
$d3d11DecoderIndex = $source.IndexOf(
    'parts.Add("-vd d3d11h264dec")')
$d3d11SinkIndex = $source.IndexOf(
    'parts.Add("-vs d3d11videosink")')
$d3d12DecoderIndex = $source.IndexOf(
    'parts.Add("-vd d3d12h264dec")')
$d3d12SinkIndex = $source.IndexOf(
    'parts.Add("-vs d3d12videosink")')
Assert-True ($d3d11DecoderIndex -lt $advancedArgumentsIndex -and
    $d3d11SinkIndex -lt $advancedArgumentsIndex -and
    $d3d12DecoderIndex -lt $advancedArgumentsIndex -and
    $d3d12SinkIndex -lt $advancedArgumentsIndex) `
    "advanced UxPlay arguments can override the managed renderer compatibility choice"
$automaticRendererOptionCount = [regex]::Matches(
    $source, 'NamedValue\(\s*"[^"]*"\s*,\s*"auto"\s*\)').Count
$recommendedD3D11OptionCount = [regex]::Matches(
    $source,
    'NamedValue\(\s*"Direct3D 11[^"]*"\s*,\s*"d3d11"\s*\)').Count
Assert-True ($automaticRendererOptionCount -eq 0 -and
    $recommendedD3D11OptionCount -eq 1) `
    "the settings UI recommends the pinned Direct3D 11 pipeline instead of automatic D3D12 selection"
$sharedBudgetCallCount = [regex]::Matches(
    $source, 'ConsumeSharedAutomaticRecoveryBudget\s*\(').Count
Assert-True ($sharedBudgetCallCount -ge 3) `
    "readiness and native discovery both consume one shared recovery budget"
Assert-True (-not $source.Contains(
        'StopCoreInternal("readiness confirmation failed"')) `
    "unconfirmed readiness never synchronously stops a socket-ready core"
Assert-True ($source.Contains(
        "networkTitle.TextAlign = ContentAlignment.MiddleLeft")) `
    "network text is vertically centered beside its help glyph"
Assert-True ($source.Contains(
        "toolTips.SetToolTip(statusDot, receiverDetails)") -and
    $source.Contains("toolTips.SetToolTip(status, receiverDetails)")) `
    "receiver details are available from both the status dot and status text"
Assert-True ($source.Contains("status.Size = new Size(1, 24)") -and
    $source.Contains(
        "TextRenderer.MeasureText(status.Text, status.Font).Width")) `
    "receiver status tooltip target follows the rendered text instead of blank space"
Assert-True (-not $source.Contains("toolTips.SetToolTip(networkCard") -and
    -not $source.Contains("toolTips.SetToolTip(networkTitle")) `
    "network details are not attached to the whole network card"
$networkHelpTooltipCount = [regex]::Matches(
    $source, 'toolTips\.SetToolTip\(networkHelp, networkDetails\)').Count
Assert-True ($networkHelpTooltipCount -eq 1) `
    "network details are attached only to the question-mark control"
$privatePinGuidanceBreakCount = [regex]::Matches(
    $source, '\\r\\n" \+\s*"[^"]*PIN [^"]*"').Count
Assert-True ($privatePinGuidanceBreakCount -ge 2) `
    "private-network PIN guidance starts on a separate tooltip line"
Assert-True ($source.Contains("e.Graphics.DpiX / 96F") -and
    $source.Contains("SmoothingMode.AntiAlias") -and
    $source.Contains("format.Alignment = StringAlignment.Center") -and
    $source.Contains("format.LineAlignment = StringAlignment.Center")) `
    "the help circle and question glyph use DPI-aware anti-aliased centering"
Assert-True ($source.Contains("EVENT_SYSTEM_MOVESIZESTART") -and
    $source.Contains("EVENT_SYSTEM_MOVESIZEEND") -and
    $source.Contains("EVENT_OBJECT_SHOW") -and
    $source.Contains("SetWinEventHook") -and
    $source.Contains("UnhookWinEvent")) `
    "renderer placement and resize completion use bounded WinEvent hook lifecycles"
Assert-True ($source.Contains("processId != rendererMoveSizeHookPid") -and
    $source.Contains("windowProcessId != (uint)processId")) `
    "renderer move/size events are restricted to the active native core"
Assert-True ($source.Contains("NativeMethods.IsIconic(window)") -and
    $source.Contains("NativeMethods.IsZoomed(window)")) `
    "automatic aspect fitting does not fight minimized or maximized state"
Assert-True ($source.Contains("pendingManualFitDueTicks") -and
    $source.Contains("DateTime.UtcNow.Ticks") -and
    [regex]::Matches(
        $source, 'ApplyPendingManualRendererFit\s*\(').Count -eq 2) `
    "manual renderer fitting is queued for the next supervision pass"
Assert-True ($source.Contains("autoFit = MakeCheckBox(") -and
    $source.Contains("FitStreamWindow(true)")) `
    "automatic aspect fitting retains a settings control and manual tray fallback"
Assert-True ([regex]::Matches(
        $receiverContextSource,
        'ToggleStreamWindowFullscreen\s*\(\s*true\s*\)').Count -eq 1 -and
    $receiverContextSource.Contains('(Esc ') -and
    $source.Contains('"video-fullscreen-toggle"') -and
    $source.Contains('"video-scale permille=" + desired') -and
    $source.Contains("TryWriteNativeVideoCommand(") -and
    $receiverCoreSource.Contains("lock (coreCommandSync)") -and
    $source.Contains("ref appliedPresentationScalePermille, 0, 0") -and
    $source.Contains("ApplyNonCroppingPresentationScale(") -and
    $source.Contains("RendererPresentationPolicy.NormalScalePermille") -and
    -not $source.Contains("PresentationScaleMaximumPermille") -and
    -not $source.Contains("ResolveAutomaticPresentationScale(") -and
    $source.Contains("IsRendererFullscreenWindow(window)") -and
    $rendererControlsSource.Contains("NativeMethods.WH_KEYBOARD_LL") -and
    $rendererControlsSource.Contains("NativeMethods.CallNextHookEx(") -and
    $rendererControlsSource.Contains("UninstallRendererKeyboardHook()") -and
    $rendererControlsSource.Contains("ref rendererEscapeRequestState") -and
    -not $source.Contains("NativeMethods.IsEscapeKeyDown()") -and
    -not $source.Contains("AdjustPhotosZoom(") -and
    -not $source.Contains("ResetPhotosZoom(") -and
    -not $source.Contains("WM_SYSKEYDOWN") -and
    -not $source.Contains("PostMessage(")) `
    "fullscreen uses bounded event-driven exits and Photos keeps every source pixel without manual zoom controls"
Assert-True ($source.Contains("internal sealed class LostConnectionForm") -and
    $lostConnectionUiSource.Contains("titleLabel.Text =") -and
    $lostConnectionUiSource.Contains("detailLabel.Text =") -and
    $lostConnectionUiSource.Contains("closeButton.Text =")) `
    "fatal connection loss has a focused user-visible placeholder"
Assert-True ($source.Contains("CopyFromScreen(") -and
    -not $source.Contains("PrintWindow(")) `
    "the placeholder uses only a non-blocking desktop screen snapshot"
Assert-True ($source.Contains("IsRendererWindowUnoccluded(") -and
    $source.Contains("NativeMethods.GW_HWNDPREV") -and
    $source.Contains("Rectangle.Intersect(")) `
    "desktop capture is rejected when any visible higher z-order window overlaps the renderer"
Assert-True ($source.Contains("TryGetRendererClientScreenBounds(") -and
    $source.Contains("NativeMethods.ClientToScreen(")) `
    "the continuity frame captures renderer client pixels rather than duplicating native chrome"
Assert-True ($lostConnectionUiSource.Contains("source.Width / 12") -and
    $lostConnectionUiSource.Contains("source.Height / 12") -and
    $lostConnectionUiSource.Contains(
        "InterpolationMode.HighQualityBicubic")) `
    "the captured frame is softened once in memory before display"
Assert-True (-not [regex]::IsMatch(
        $lostConnectionUiSource, '\.(?:Save|SaveAdd)\s*\(')) `
    "the lost-frame placeholder never writes its snapshot to disk"
$lostArmStart = $source.IndexOf("private void ArmLostConnectionRecovery")
$lostArmEnd = $source.IndexOf(
    "private void ResetCoreSessionTracking", $lostArmStart)
Assert-True ($lostArmStart -ge 0 -and $lostArmEnd -gt $lostArmStart) `
    "fatal-loss arming has a focused implementation boundary"
$lostArmSource = $source.Substring(
    $lostArmStart, $lostArmEnd - $lostArmStart)
Assert-True ($lostArmSource.Contains("QueueLostConnectionPlaceholder()") -and
    -not $lostArmSource.Contains("CopyFromScreen(") -and
    -not $lostArmSource.Contains("new LostConnectionForm")) `
    "the native output callback only queues placeholder UI work"
$placeholderCloseCallCount = [regex]::Matches(
    $source, 'CloseLostConnectionPlaceholder\s*\(\s*\)').Count
Assert-True ($placeholderCloseCallCount -ge 5) `
    "manual stop, disabled receiver startup, app exit, and UI close consume the placeholder"
$sessionResetStart = $source.IndexOf(
    "private void ResetCoreSessionTracking")
$sessionResetEnd = $source.IndexOf(
    "private void ResetIdleDiscoveryRenewalSchedule", $sessionResetStart)
Assert-True ($sessionResetStart -ge 0 -and
    $sessionResetEnd -gt $sessionResetStart) `
    "core-session reset has a focused implementation boundary"
$sessionResetSource = $source.Substring(
    $sessionResetStart, $sessionResetEnd - $sessionResetStart)
Assert-True (-not $sessionResetSource.Contains(
        "LostConnectionPlaceholder")) `
    "a native core restart does not automatically dismiss the placeholder"
$mirroringStartStart = $source.IndexOf(
    "private bool HandleMirroringStartedMaintenance")
$mirroringStartEnd = $source.IndexOf(
    "private void ResolveCoreReadinessFromClientActivityLocked",
    $mirroringStartStart)
Assert-True ($mirroringStartStart -ge 0 -and
    $mirroringStartEnd -gt $mirroringStartStart) `
    "mirroring-start maintenance has a focused implementation boundary"
$mirroringStartSource = $source.Substring(
    $mirroringStartStart, $mirroringStartEnd - $mirroringStartStart)
Assert-True (-not $mirroringStartSource.Contains(
        "QueueLostConnectionPlaceholderClose")) `
    "a protocol start marker does not dismiss continuity before a renderer exists"
Assert-True ($source.Contains(
        "CompleteLostConnectionRendererHandoff();") -and
    $source.Contains("lostConnectionRendererHandoffPending") -and
    $source.Contains(
        "Mirroring renderer is visible and positioned; beginning") -and
    $source.Contains("Renderer handoff fade completed; closing")) `
    "continuity is dismissed only after supervision has positioned a visible renderer"
Assert-True ($source.Contains("ShowConnectionRecovered()") -and
    $source.Contains("ShowReconnectHint(settings.ReceiverName)") -and
    $source.Contains("ShowConnectionLost()") -and
    [regex]::Matches(
        $lostConnectionUiSource, 'titleLabel\.Text\s*=').Count -ge 2 -and
    [regex]::Matches(
        $lostConnectionUiSource, 'detailLabel\.Text\s*=').Count -ge 2) `
    "loss, manual reconnect guidance, and recovered waiting states remain distinct"
Assert-True ($lostConnectionUiSource.Contains(
        "IntPtr rendererWindow, Action completed") -and
    $lostConnectionUiSource.Contains("rendererHandoffTimer.Interval = 20") -and
    $lostConnectionUiSource.Contains("elapsedMilliseconds / 180.0") -and
    $lostConnectionUiSource.Contains("CancelRendererHandoff();") -and
    $source.Contains("lostConnectionPlaceholderShowPending, 0, 0") -and
    $lostConnectionUiSource.Contains("NativeMethods.SWP_NOACTIVATE") -and
    -not $lostConnectionUiSource.Contains("Thread.Sleep")) `
    "successful renderer handoff uses a short non-blocking opacity fade"
Assert-True ($lostConnectionUiSource.Contains(
        "protected override bool ShowWithoutActivation")) `
    "the continuity placeholder does not steal focus when it first appears"
Assert-True ($lostConnectionUiSource.Contains(
        "BringAboveRendererWithoutActivation(") -and
    $lostConnectionUiSource.Contains("NativeMethods.HWND_TOP") -and
    $lostConnectionUiSource.Contains(
        "NativeMethods.GetWindow(") -and
    $source.Contains(
        "placeholder.BringAboveRendererWithoutActivation(") -and
    -not $lostConnectionUiSource.Contains("Activate()") -and
    -not $lostConnectionUiSource.Contains("SetForegroundWindow")) `
    "continuity is raised above the foreign renderer without activation or a permanent topmost policy"
$bringContinuityStart = $lostConnectionUiSource.IndexOf(
    "internal bool BringAboveRendererWithoutActivation")
$bringContinuityEnd = $lostConnectionUiSource.IndexOf(
    "internal bool BeginRendererHandoff", $bringContinuityStart)
Assert-True ($bringContinuityStart -ge 0 -and
    $bringContinuityEnd -gt $bringContinuityStart) `
    "continuity z-order has a focused implementation boundary"
$bringContinuitySource = $lostConnectionUiSource.Substring(
    $bringContinuityStart,
    $bringContinuityEnd - $bringContinuityStart)
Assert-True ($bringContinuitySource.Contains(
        "IntPtr insertAfter = TopMost") -and
    $bringContinuitySource.Contains("NativeMethods.HWND_TOPMOST") -and
    $bringContinuitySource.Contains("NativeMethods.HWND_TOP") -and
    $bringContinuitySource.Contains("NativeMethods.SWP_NOACTIVATE") -and
    $bringContinuitySource.Contains("aboveRenderer == Handle")) `
    "always-on-top is preserved only when requested and ordinary continuity is inserted immediately above the renderer"
$handoffUiStart = $bringContinuityEnd
$handoffUiEnd = $lostConnectionUiSource.IndexOf(
    "internal void ShowConnectionRecovered", $handoffUiStart)
Assert-True ($handoffUiEnd -gt $handoffUiStart) `
    "continuity handoff has a focused UI implementation boundary"
$handoffUiSource = $lostConnectionUiSource.Substring(
    $handoffUiStart, $handoffUiEnd - $handoffUiStart)
Assert-True ($handoffUiSource.Contains(
        "BringAboveRendererWithoutActivation(rendererWindow)") -and
    -not $handoffUiSource.Contains("NativeMethods.HWND_TOPMOST") -and
    -not $handoffUiSource.Contains("SetWindowPos(")) `
    "renderer handoff reuses the same non-activating z-order policy"
$renewedLossStateStart = $lostConnectionUiSource.IndexOf(
    "internal void ShowConnectionLost")
$renewedLossStateEnd = $lostConnectionUiSource.IndexOf(
    "internal void CancelRendererHandoff", $renewedLossStateStart)
Assert-True ($renewedLossStateStart -ge 0 -and
    $renewedLossStateEnd -gt $renewedLossStateStart) `
    "renewed-loss presentation has a focused implementation boundary"
$renewedLossStateSource = $lostConnectionUiSource.Substring(
    $renewedLossStateStart,
    $renewedLossStateEnd - $renewedLossStateStart)
Assert-True ([regex]::Matches(
        $renewedLossStateSource,
        'CancelRendererHandoff\(\);').Count -eq 2) `
    "a renewed or fatal loss cancels an in-progress renderer handoff fade"
$mirroringEndStart = $source.IndexOf(
    "private void HandleMirroringEndedMaintenance")
$mirroringEndEnd = $source.IndexOf(
    "private void ObserveClientFeedbackHealth", $mirroringEndStart)
Assert-True ($mirroringEndStart -ge 0 -and
    $mirroringEndEnd -gt $mirroringEndStart) `
    "mirroring-end maintenance has a focused implementation boundary"
$mirroringEndSource = $source.Substring(
    $mirroringEndStart, $mirroringEndEnd - $mirroringEndStart)
Assert-True ($mirroringEndSource.Contains(
        "if (showReconnectHint)") -and
    $mirroringEndSource.Contains(
        "QueueLostConnectionReconnectHint();") -and
    $mirroringEndSource.Contains(
        "if (closeTransientFeedbackPlaceholder)")) `
    "only abnormal cleanup queues reconnect guidance while a clean stop still closes transient continuity"
$showCallbackStart = $source.IndexOf(
    "private void OnRendererWindowShowEvent")
$showCallbackEnd = $source.IndexOf(
    "private void OnRendererMoveSizeEvent", $showCallbackStart)
Assert-True ($showCallbackStart -ge 0 -and
    $showCallbackEnd -gt $showCallbackStart) `
    "renderer-show callback has a focused implementation boundary"
$showCallbackSource = $source.Substring(
    $showCallbackStart, $showCallbackEnd - $showCallbackStart)
Assert-True ($showCallbackSource.Contains(
        "TryApplySavedStreamWindowPlacement") -and
    -not $showCallbackSource.Contains("settings.Save") -and
    -not $showCallbackSource.Contains("FitRendererWindow") -and
    -not $showCallbackSource.Contains("Log(")) `
    "renderer show pre-positions from loaded settings without IO, activation, or aspect fitting"
$moveSizeCallbackStart = $source.IndexOf(
    "private void OnRendererMoveSizeEvent")
$moveSizeCallbackEnd = $source.IndexOf(
    "private static bool ShouldQueueManualRendererFit",
    $moveSizeCallbackStart)
Assert-True ($moveSizeCallbackStart -ge 0 -and
    $moveSizeCallbackEnd -gt $moveSizeCallbackStart) `
    "renderer move/size callback has a focused implementation boundary"
$moveSizeCallbackSource = $source.Substring(
    $moveSizeCallbackStart,
    $moveSizeCallbackEnd - $moveSizeCallbackStart)
Assert-True (-not $moveSizeCallbackSource.Contains("FitRendererWindow") -and
    -not $moveSizeCallbackSource.Contains("SetWindowPos") -and
    -not $moveSizeCallbackSource.Contains("settings.Save") -and
    -not $moveSizeCallbackSource.Contains("GetWindowRect")) `
    "the WinEvent callback only records and queues instead of resizing or writing settings"
$placementQueueIndex = $moveSizeCallbackSource.IndexOf(
    "QueueStreamWindowPlacementSave")
$fitDecisionIndex = $moveSizeCallbackSource.IndexOf(
    "ShouldQueueManualRendererFit")
Assert-True ($placementQueueIndex -ge 0 -and
    $fitDecisionIndex -gt $placementQueueIndex) `
    "move-only and automatic-fit-disabled completion still queues placement persistence"
$placementQueueCallCount = [regex]::Matches(
    $source, 'QueueStreamWindowPlacementSave\s*\(').Count
Assert-True ($placementQueueCallCount -ge 5 -and
    $source.Contains("SavePendingStreamWindowPlacement(window)") -and
    $source.Contains(
        "UpdateStreamWindowPlacementAfterAutomaticFit")) `
    "interactive and programmatic fits persist through the supervision timer"
Assert-True ($source.Contains(
        "MarkStreamWindowPlacementPersistable(window)") -and
    $source.Contains(
        "CanPersistStreamWindowPlacement(window)") -and
    $source.Contains(
        "if (!videoSize.IsEmpty)") -and
    $source.Contains(
        "ClearStreamWindowPlacementPersistence(window)")) `
    "only a trusted automatic fit or explicit user move can replace saved renderer placement"
$restorePlacementStart = $source.IndexOf(
    "private bool TryRestoreStreamWindowPlacement")
$restorePlacementEnd = $source.IndexOf(
    "private bool TryApplySavedStreamWindowPlacement",
    $restorePlacementStart)
Assert-True ($restorePlacementStart -ge 0 -and
    $restorePlacementEnd -gt $restorePlacementStart) `
    "saved renderer restoration has a focused implementation boundary"
$restorePlacementSource = $source.Substring(
    $restorePlacementStart,
    $restorePlacementEnd - $restorePlacementStart)
Assert-True (-not $restorePlacementSource.Contains(
        "QueueStreamWindowPlacementSave")) `
    "a provisional restored window is not persisted before device orientation is known"
$placementFlushCount = [regex]::Matches(
    $source, 'FlushStreamWindowPlacementBeforeCoreStop\s*\(\s*\)').Count
Assert-True ($placementFlushCount -ge 3) `
    "manual stop and asynchronous restart flush placement before detaching the core"
Assert-True ($source.Contains("settings.StreamWindowLeft = oldLeft") -and
    $source.Contains("settings.StreamWindowDpi = oldDpi") -and
    $source.Contains("streamWindowPlacementSaveFailures < 2")) `
    "a failed atomic placement save restores memory and receives a bounded retry"
$restoredAreaFitCount = [regex]::Matches(
    $source,
    'FitRendererWindow\(\s*window, automaticVideoSize,\s*restoredStreamWindowPlacementWindow == window\)').Count
Assert-True ($restoredAreaFitCount -eq 1 -and
    [regex]::IsMatch(
        $source,
        'firstExactFit\s*\?\s*restoredStreamWindowPlacementWindow == window\s*:\s*true')) `
    "initial and first exact-size fits preserve restored area while later aspect transitions preserve current area"

$assembly = [Reflection.Assembly]::LoadFrom(
    [IO.Path]::GetFullPath($AssemblyPath))
$settingsType = $assembly.GetType(
    "AirPlayReceiverMvp.AppSettings", $true)
$contextType = $assembly.GetType(
    "AirPlayReceiverMvp.ReceiverContext", $true)
$instanceFlags = [Reflection.BindingFlags]::Instance -bor `
    [Reflection.BindingFlags]::NonPublic -bor `
    [Reflection.BindingFlags]::Public
$staticFlags = [Reflection.BindingFlags]::Static -bor `
    [Reflection.BindingFlags]::NonPublic -bor `
    [Reflection.BindingFlags]::Public

$rendererButtonType = $assembly.GetType(
    "AirPlayReceiverMvp.RendererFullscreenButtonForm", $true)
$calculateRendererButtonBounds = $rendererButtonType.GetMethod(
    "CalculateBounds", $staticFlags)
$shouldShowRendererButton = $rendererButtonType.GetMethod(
    "ShouldShow", $staticFlags)
$shouldCaptureRendererEscape = $contextType.GetMethod(
    "ShouldCaptureRendererEscape", $staticFlags)
$resolveRendererEscapeKeyState = $contextType.GetMethod(
    "ResolveRendererEscapeKeyState", $staticFlags)
$isStaleBorderlessTransition = $contextType.GetMethod(
    "IsStaleBorderlessTransition", $staticFlags)
Assert-True ($null -ne $calculateRendererButtonBounds -and
    $null -ne $shouldShowRendererButton -and
    $null -ne $shouldCaptureRendererEscape -and
    $null -ne $resolveRendererEscapeKeyState -and
    $null -ne $isStaleBorderlessTransition) `
    "renderer fullscreen controls expose deterministic policy boundaries"

$rendererBounds = [Drawing.Rectangle]::new(100, 200, 800, 600)
$normalButtonBounds = [Drawing.Rectangle]$calculateRendererButtonBounds.Invoke(
    $null, [object[]]@($rendererBounds, $false, 96, 46))
$fullscreenButtonBounds = [Drawing.Rectangle]$calculateRendererButtonBounds.Invoke(
    $null, [object[]]@($rendererBounds, $true, 96, 46))
$highDpiButtonBounds = [Drawing.Rectangle]$calculateRendererButtonBounds.Invoke(
    $null, [object[]]@($rendererBounds, $true, 192, 92))
$narrowButtonBounds = [Drawing.Rectangle]$calculateRendererButtonBounds.Invoke(
    $null, [object[]]@(
        [Drawing.Rectangle]::new(0, 0, 120, 300), $false, 96, 46))
Assert-True ($normalButtonBounds.Width -eq 36 -and
    $normalButtonBounds.Height -eq 28 -and
    $normalButtonBounds.Right -le $rendererBounds.Right - (46 * 3) -and
    $fullscreenButtonBounds.Right -lt $rendererBounds.Right -and
    $fullscreenButtonBounds.Top -gt $rendererBounds.Top -and
    $highDpiButtonBounds.Width -eq 72 -and
    $highDpiButtonBounds.Height -eq 56 -and
    $narrowButtonBounds.IsEmpty) `
    "renderer fullscreen control is DPI-aware and never overlaps standard caption buttons"
Assert-True ([bool]$shouldShowRendererButton.Invoke(
        $null, [object[]]@($true, $false, $true)) -and
    -not [bool]$shouldShowRendererButton.Invoke(
        $null, [object[]]@($true, $false, $false)) -and
    -not [bool]$shouldShowRendererButton.Invoke(
        $null, [object[]]@($true, $true, $true)) -and
    -not [bool]$shouldShowRendererButton.Invoke(
        $null, [object[]]@($false, $false, $true))) `
    "renderer fullscreen control stays off minimized, missing, and unrelated foreground windows"
Assert-True ([bool]$shouldCaptureRendererEscape.Invoke(
        $null, [object[]]@($true, 77, [uint32]77, $true)) -and
    -not [bool]$shouldCaptureRendererEscape.Invoke(
        $null, [object[]]@($false, 77, [uint32]77, $true)) -and
    -not [bool]$shouldCaptureRendererEscape.Invoke(
        $null, [object[]]@($true, 77, [uint32]78, $true)) -and
    -not [bool]$shouldCaptureRendererEscape.Invoke(
        $null, [object[]]@($true, 77, [uint32]77, $false))) `
    "Escape capture requires actual fullscreen and the foreground renderer root"
Assert-True ([int]$resolveRendererEscapeKeyState.Invoke(
        $null, [object[]]@(0, $true)) -eq 1 -and
    [int]$resolveRendererEscapeKeyState.Invoke(
        $null, [object[]]@(1, $true)) -eq 1 -and
    [int]$resolveRendererEscapeKeyState.Invoke(
        $null, [object[]]@(1, $false)) -eq 3 -and
    [int]$resolveRendererEscapeKeyState.Invoke(
        $null, [object[]]@(2, $false)) -eq 0 -and
    [int]$resolveRendererEscapeKeyState.Invoke(
        $null, [object[]]@(3, $false)) -eq 3) `
    "short Escape is retained through key-up and a sent held press rearms on release"
Assert-True ([bool]$isStaleBorderlessTransition.Invoke(
        $null, [object[]]@($true, $false, $true)) -and
    -not [bool]$isStaleBorderlessTransition.Invoke(
        $null, [object[]]@($true, $false, $false)) -and
    -not [bool]$isStaleBorderlessTransition.Invoke(
        $null, [object[]]@($true, $true, $true))) `
    "only a fullscreen-to-borderless transition keeps the explicit exit recovery control"
Assert-True ($rendererButtonSource.Contains("WsExNoActivate") -and
    $rendererButtonSource.Contains("ShowWithoutActivation") -and
    $rendererButtonSource.Contains("standardWidth * 3") -and
    $rendererButtonSource.Contains(
        "NativeMethods.GW_HWNDPREV") -and
    $rendererButtonSource.Contains(
        "topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP") -and
    -not $rendererButtonSource.Contains("SetParent(") -and
    -not $rendererButtonSource.Contains("SetWindowLongPtr(") -and
    $rendererControlsSource.Contains("Interlocked.CompareExchange(") -and
    $rendererControlsSource.Contains("NativeMethods.WM_KEYUP") -and
    $rendererControlsSource.Contains(
        "settings.AlwaysOnTop || exitMode") -and
    $rendererControlsSource.Contains("rendererStaleBorderlessWindow") -and
    -not $rendererControlsSource.Contains(
        '"restore stale borderless renderer frame"') -and
    $rendererControlsSource.Contains("NativeMethods.CallNextHookEx(")) `
    "fullscreen control remains shell-owned and the keyboard hook always preserves the system chain"

$quoteArgument = $contextType.GetMethod("QuoteArgument", $staticFlags)
Assert-True ($null -ne $quoteArgument) `
    "managed process arguments use one focused Windows quoting helper"
$commandLineNativeType = Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class AeroMirrorCommandLineNative
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CommandLineToArgvW(
        string commandLine, out int argumentCount);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr memory);
}
"@ -PassThru

$quoteRoundTripCases = @(
    [pscustomobject]@{ Label = "null"; Value = $null },
    [pscustomobject]@{ Label = "empty"; Value = "" },
    [pscustomobject]@{ Label = "ordinary text"; Value = "AeroMirror" },
    [pscustomobject]@{ Label = "spaces"; Value = "Aero Mirror Receiver" },
    [pscustomobject]@{ Label = "quote"; Value = 'say "hello"' },
    [pscustomobject]@{
        Label = "backslashes before quote"
        Value = "before" + ("\" * 3) + '"after'
    },
    [pscustomobject]@{
        Label = "trailing backslashes"
        Value = "C:\Program Files\AeroMirror" + ("\" * 3)
    }
)
foreach ($case in $quoteRoundTripCases) {
    [object[]]$invokeArguments = [object[]]::new(1)
    $invokeArguments[0] = $case.Value
    $quotedArgument = [string]$quoteArgument.Invoke(
        $null, $invokeArguments)
    $commandLine = '"AeroMirrorTest.exe" ' + $quotedArgument
    $argumentCount = 0
    $argumentVector = $commandLineNativeType::CommandLineToArgvW(
        $commandLine, [ref]$argumentCount)
    Assert-True ($argumentVector -ne [IntPtr]::Zero) `
        "CommandLineToArgvW accepts the $($case.Label) quoted argument"
    try {
        Assert-True ($argumentCount -eq 2) `
            "$($case.Label) adds exactly one Windows argv argument"
        $roundTripValue = [Runtime.InteropServices.Marshal]::PtrToStringUni(
            [Runtime.InteropServices.Marshal]::ReadIntPtr(
                $argumentVector, [IntPtr]::Size))
        $expectedValue = if ($null -eq $case.Value) {
            ""
        }
        else {
            [string]$case.Value
        }
        Assert-True ($roundTripValue -ceq $expectedValue) `
            "$($case.Label) survives Windows argv quoting unchanged"
    }
    finally {
        $commandLineNativeType::LocalFree($argumentVector) | Out-Null
    }
}

$testStorageRoot = [IO.Path]::GetFullPath((Join-Path `
    ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString("N"))))
$testStorageRootInfo = [IO.DirectoryInfo]::new($testStorageRoot)
$normalizedTempRoot = [IO.Path]::GetFullPath(
    [IO.Path]::GetTempPath()).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
$testStorageRootId = [Guid]::Empty
$testStorageRootIsSafe =
    $null -ne $testStorageRootInfo.Parent -and
    $testStorageRootInfo.Parent.FullName.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) -ieq $normalizedTempRoot -and
    [Guid]::TryParseExact(
        $testStorageRootInfo.Name, "N", [ref]$testStorageRootId)
Assert-True $testStorageRootIsSafe `
    "the resilience suite owns one exact GUID child of the process temp root"

$setStorageRootForTests = $settingsType.GetMethod(
    "SetStorageRootForTests", $staticFlags)
$flushLog = $contextType.GetMethod("FlushLog", $staticFlags)
$writeLog = $contextType.GetMethod("Log", $staticFlags)
Assert-True ($null -ne $setStorageRootForTests -and
    $null -ne $flushLog -and
    $flushLog.ReturnType -eq [bool] -and
    $null -ne $writeLog) `
    "test storage isolation exposes a process-lifetime root and deterministic log drain"
$testCompleted = $false
$testStorageRootSetupStarted = $false
try {
    $testStorageRootSetupStarted = $true
    $setStorageRootForTests.Invoke(
        $null, [object[]]@($testStorageRoot)) | Out-Null
    $setStorageRootForTests.Invoke(
        $null, [object[]]@($testStorageRoot)) | Out-Null
    $alternateStorageRoot = [IO.Path]::GetFullPath((Join-Path `
        ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString("N"))))
    $secondStorageRootRejected = $false
    try {
        $setStorageRootForTests.Invoke(
            $null, [object[]]@($alternateStorageRoot)) | Out-Null
    }
    catch {
        $storageRootException = $_.Exception
        while ($null -ne $storageRootException) {
            if ($storageRootException -is [InvalidOperationException]) {
                $secondStorageRootRejected = $true
                break
            }
            $storageRootException = $storageRootException.InnerException
        }
    }
    Assert-True ($secondStorageRootRejected -and
        -not (Test-Path -LiteralPath $alternateStorageRoot)) `
        "the process accepts one idempotent test root and never creates a replacement"

    $expectedStoragePaths = [ordered]@{
        Folder = $testStorageRoot
        FilePath = Join-Path $testStorageRoot "settings.ini"
        LogPath = Join-Path $testStorageRoot "receiver.log"
        ReceiverKeyPath = Join-Path $testStorageRoot "receiver-key.pem"
        ReceiverDeviceIdPath = Join-Path $testStorageRoot "receiver-device-id.txt"
        TrustedClientsPath = Join-Path $testStorageRoot "trusted-clients.txt"
    }
    foreach ($entry in $expectedStoragePaths.GetEnumerator()) {
        $storageProperty = $settingsType.GetProperty($entry.Key, $staticFlags)
        Assert-True ($null -ne $storageProperty) `
            "AppSettings exposes the $($entry.Key) storage path"
        $actualStoragePath = [IO.Path]::GetFullPath(
            [string]$storageProperty.GetValue($null, $null))
        Assert-True ($actualStoragePath -ieq
            [IO.Path]::GetFullPath([string]$entry.Value)) `
            "$($entry.Key) stays inside the isolated GUID storage root"
    }

    $testLogMarker = "AEROMIRROR_TEST_LOG_ISOLATION root=" +
        $testStorageRootInfo.Name
    $context = [Runtime.Serialization.FormatterServices]::GetUninitializedObject(
        $contextType)
    $writeLog.Invoke(
        $null, [object[]]@($testLogMarker)) | Out-Null

$decideLostPlaceholder = $contextType.GetMethod(
    "DecideLostConnectionPlaceholderAction", $staticFlags)
Assert-True ($null -ne $decideLostPlaceholder) `
    "lost-frame placeholder exposes a deterministic state transition"
Assert-True ($decideLostPlaceholder.Invoke(
        $null, [object[]]@($true, $false, $false)).ToString() -eq "Show") `
    "a first fatal-loss request opens the placeholder"
Assert-True ($decideLostPlaceholder.Invoke(
        $null, [object[]]@($true, $false, $true)).ToString() -eq "None") `
    "a repeated request cannot duplicate an existing placeholder"
Assert-True ($decideLostPlaceholder.Invoke(
        $null, [object[]]@($true, $true, $false)).ToString() -eq "Close") `
    "a reconnect or shutdown close request wins over a stale show request"
$decideLostPresentation = $contextType.GetMethod(
    "DecideLostConnectionPresentationState", $staticFlags)
Assert-True ($null -ne $decideLostPresentation) `
    "lost-frame copy exposes deterministic presentation states"
Assert-True ($decideLostPresentation.Invoke(
        $null, [object[]]@($true, $false, $false)).ToString() -eq "Lost") `
    "a transient feedback gap starts with ordinary waiting guidance"
Assert-True ($decideLostPresentation.Invoke(
        $null, [object[]]@($true, $true, $false)).ToString() -eq
        "ReconnectHint") `
    "abnormal native cleanup replaces generic waiting with explicit iPhone reconnect guidance"
Assert-True ($decideLostPresentation.Invoke(
        $null, [object[]]@($false, $false, $true)).ToString() -eq
        "Recovered") `
    "only explicit recovery evidence selects the recovered waiting-for-image state"
Assert-True ($decideLostPresentation.Invoke(
        $null, [object[]]@($false, $false, $false)).ToString() -eq "None") `
    "an unrelated supervision tick does not rewrite continuity text"
$clampLostPlaceholderBounds = $contextType.GetMethod(
    "ClampLostConnectionPlaceholderBounds", $staticFlags)
Assert-True ($null -ne $clampLostPlaceholderBounds) `
    "lost-frame placement exposes a deterministic screen-clamp path"
$placeholderWorkArea = [Drawing.Rectangle]::new(0, 0, 1920, 1080)
$rememberedRendererBounds = [Drawing.Rectangle]::new(140, 90, 520, 920)
Assert-True ([Drawing.Rectangle]$clampLostPlaceholderBounds.Invoke(
        $null, [object[]]@(
            $rememberedRendererBounds, $placeholderWorkArea)) -eq
        $rememberedRendererBounds) `
    "an on-screen placeholder reuses the last renderer bounds"
$disconnectedMonitorBounds = [Drawing.Rectangle]::new(
    2400, 1200, 520, 920)
$clampedRendererBounds = [Drawing.Rectangle]$clampLostPlaceholderBounds.Invoke(
    $null, [object[]]@(
        $disconnectedMonitorBounds, $placeholderWorkArea))
Assert-True ($clampedRendererBounds -eq
        [Drawing.Rectangle]::new(1400, 160, 520, 920)) `
    "a remembered window from a disconnected monitor returns fully on-screen"

$shouldQueueManualFit = $contextType.GetMethod(
    "ShouldQueueManualRendererFit", $staticFlags)
Assert-True ($null -ne $shouldQueueManualFit) `
    "manual renderer resize classification is independently testable"
$startClientSize = [Drawing.Size]::new(460, 1000)
Assert-True (-not [bool]$shouldQueueManualFit.Invoke(
        $null, [object[]]@($true, $startClientSize,
            [Drawing.Size]::new(460, 1000)))) `
    "moving a renderer without changing its client size does not queue a fit"
Assert-True (-not [bool]$shouldQueueManualFit.Invoke(
        $null, [object[]]@($true, $startClientSize,
            [Drawing.Size]::new(464, 996)))) `
    "four-pixel window metric noise does not queue a fit"
Assert-True ([bool]$shouldQueueManualFit.Invoke(
        $null, [object[]]@($true, $startClientSize,
            [Drawing.Size]::new(465, 1000)))) `
    "a manual client resize larger than the tolerance queues a fit"
Assert-True (-not [bool]$shouldQueueManualFit.Invoke(
        $null, [object[]]@($false, $startClientSize,
            [Drawing.Size]::new(700, 1000)))) `
    "an explicit disabled automatic-fit setting remains authoritative"

$shouldApplyRendererPolicy = $contextType.GetMethod(
    "ShouldApplyRendererWindowPolicy", $staticFlags)
Assert-True ($null -ne $shouldApplyRendererPolicy) `
    "foreign renderer policy caching is independently testable"
$rendererOne = [IntPtr]::new(101)
$rendererTwo = [IntPtr]::new(202)
Assert-True ([bool]$shouldApplyRendererPolicy.Invoke(
        $null, [object[]]@(
            $rendererOne, [IntPtr]::Zero, $false,
            $false, $false, $true, $true))) `
    "a newly observed renderer receives window policy once"
Assert-True (-not [bool]$shouldApplyRendererPolicy.Invoke(
        $null, [object[]]@(
            $rendererOne, $rendererOne, $true,
            $false, $false, $true, $true))) `
    "an unchanged supervision tick does not mutate the foreign renderer"
Assert-True ([bool]$shouldApplyRendererPolicy.Invoke(
        $null, [object[]]@(
            $rendererOne, $rendererOne, $true,
            $true, $false, $true, $true))) `
    "an always-on-top settings change reapplies renderer policy"
Assert-True ([bool]$shouldApplyRendererPolicy.Invoke(
        $null, [object[]]@(
            $rendererOne, $rendererOne, $true,
            $false, $false, $false, $true))) `
    "a taskbar settings change reapplies renderer policy"
Assert-True ([bool]$shouldApplyRendererPolicy.Invoke(
        $null, [object[]]@(
            $rendererTwo, $rendererOne, $true,
            $false, $false, $true, $true))) `
    "a replacement renderer receives policy even when settings are unchanged"

$clampSavedStreamWindowBounds = $contextType.GetMethod(
    "ClampSavedStreamWindowBounds", $staticFlags)
Assert-True ($null -ne $clampSavedStreamWindowBounds) `
    "saved renderer bounds normalization is independently testable"
function Invoke-ClampSavedRendererBounds(
    [Drawing.Rectangle]$Saved,
    [Drawing.Rectangle]$Current,
    [Drawing.Rectangle[]]$WorkAreas,
    [int]$SavedDpi,
    [int]$TargetDpi) {
    $arguments = [Array]::CreateInstance([object], 5)
    $arguments.SetValue($Saved, 0)
    $arguments.SetValue($Current, 1)
    $arguments.SetValue($WorkAreas, 2)
    $arguments.SetValue($SavedDpi, 3)
    $arguments.SetValue($TargetDpi, 4)
    return [Drawing.Rectangle]$clampSavedStreamWindowBounds.Invoke(
        $null, $arguments)
}
$dualMonitorAreas = [Drawing.Rectangle[]]@(
    [Drawing.Rectangle]::new(0, 0, 1920, 1040),
    [Drawing.Rectangle]::new(1920, 0, 1920, 1040))
$secondaryPlacement = Invoke-ClampSavedRendererBounds `
    ([Drawing.Rectangle]::new(2100, 40, 600, 900)) `
    ([Drawing.Rectangle]::new(100, 100, 460, 1000)) `
    $dualMonitorAreas 96 96
Assert-True ($secondaryPlacement -eq
    [Drawing.Rectangle]::new(2100, 40, 600, 900)) `
    "a placement on an available secondary monitor is retained"
$primaryArea = [Drawing.Rectangle[]]@(
    [Drawing.Rectangle]::new(0, 0, 1920, 1040))
$disconnectedPlacement = Invoke-ClampSavedRendererBounds `
    ([Drawing.Rectangle]::new(2500, 200, 600, 800)) `
    ([Drawing.Rectangle]::new(100, 100, 460, 1000)) `
    $primaryArea 96 96
Assert-True ($primaryArea[0].Contains($disconnectedPlacement)) `
    "a placement from a disconnected monitor is clamped into the current work area"
$dpiScaledPlacement = Invoke-ClampSavedRendererBounds `
    ([Drawing.Rectangle]::new(100, 100, 400, 600)) `
    ([Drawing.Rectangle]::new(100, 100, 460, 1000)) `
    $primaryArea 96 144
Assert-True ($dpiScaledPlacement.Width -eq 600 -and
    $dpiScaledPlacement.Height -eq 900) `
    "saved renderer size follows a target monitor DPI change"
$minimumVisiblePlacement = Invoke-ClampSavedRendererBounds `
    ([Drawing.Rectangle]::new(100, 100, 100, 100)) `
    ([Drawing.Rectangle]::new(100, 100, 460, 1000)) `
    $primaryArea 144 96
Assert-True ($minimumVisiblePlacement.Width -ge 100 -and
    $minimumVisiblePlacement.Height -ge 100 -and
    $primaryArea[0].Contains($minimumVisiblePlacement)) `
    "DPI restoration keeps a sensible visible minimum including the title bar"
$oversizedPlacement = Invoke-ClampSavedRendererBounds `
    ([Drawing.Rectangle]::new(-100, -100, 4000, 3000)) `
    ([Drawing.Rectangle]::new(100, 100, 460, 1000)) `
    $primaryArea 96 96
Assert-True ($primaryArea[0].Contains($oversizedPlacement)) `
    "an oversized saved placement is uniformly constrained to the work area"

$networkHelpType = $assembly.GetType(
    "AirPlayReceiverMvp.NetworkHelpGlyph", $true)
Assert-True ($networkHelpType.BaseType.FullName -eq
    "System.Windows.Forms.Control") `
    "the network help glyph is a dedicated custom control"
$networkHelpPaint = $networkHelpType.GetMethod("OnPaint", $instanceFlags)
Assert-True ($null -ne $networkHelpPaint -and
    $networkHelpPaint.DeclaringType -eq $networkHelpType) `
    "the network help glyph owns its DPI-aware drawing path"
$networkHelpProbe = [Activator]::CreateInstance($networkHelpType, $true)
try {
    Assert-True ($networkHelpProbe.Width -eq 24 -and
        $networkHelpProbe.Height -eq 24) `
        "the help glyph has a compact square layout box"
    Assert-True ($networkHelpProbe.Text -eq "?") `
        "the help glyph exposes a question-mark text alternative"
    Assert-True ($networkHelpProbe.AccessibleRole.ToString() -eq
        "HelpBalloon") `
        "assistive technology receives an explicit help role"
}
finally {
    $networkHelpProbe.Dispose()
}

$normalizeSettings = $settingsType.GetMethod(
    "NormalizePersistedValues", $instanceFlags)
Assert-True ($null -ne $normalizeSettings) `
    "persisted settings normalization exists"
$migrateRendererDefault = $settingsType.GetMethod(
    "MigrateRendererStabilityDefault", $staticFlags)
Assert-True ($null -ne $migrateRendererDefault) `
    "the renderer stability migration is independently testable"
$migrateCurrentSettingsVersion = $settingsType.GetMethod(
    "MigrateCurrentSettingsVersion", $staticFlags)
Assert-True ($null -ne $migrateCurrentSettingsVersion) `
    "the current profile migration is independently testable"
$settingsPersistenceSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Configuration\AppSettings.cs"))
$settingsPersistenceSource = $settingsPersistenceSource.Replace(
    "namespace AirPlayReceiverMvp",
    "namespace AirPlayReceiverMvp.PersistenceRoundTripProbe")
$productionFilePathSource =
    'public static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }'
$isolatedFilePathSource =
    'public static string TestFilePath = "";' +
    [Environment]::NewLine +
    '        public static string FilePath { get { return TestFilePath; } }'
Assert-True ($settingsPersistenceSource.Contains(
        $productionFilePathSource)) `
    "the isolated settings round-trip replaces only the production file path"
$settingsPersistenceSource = $settingsPersistenceSource.Replace(
    $productionFilePathSource, $isolatedFilePathSource)
$settingsPersistenceTypes = Add-Type `
    -TypeDefinition $settingsPersistenceSource `
    -Language CSharp `
    -ReferencedAssemblies @(
        "System.dll",
        "System.Core.dll",
        "System.Drawing.dll",
        "System.ServiceProcess.dll",
        "System.Web.Extensions.dll",
        "System.Windows.Forms.dll") `
    -PassThru `
    -WarningAction SilentlyContinue
$settingsPersistenceType = $settingsPersistenceTypes |
    Where-Object { $_.Name -eq "AppSettings" } |
    Select-Object -First 1
Assert-True ($null -ne $settingsPersistenceType) `
    "the isolated profile uses the current AppSettings implementation"
$settingsPersistencePathField = $settingsPersistenceType.GetField(
    "TestFilePath", $staticFlags)
$settingsPersistenceSave = $settingsPersistenceType.GetMethod(
    "Save", $instanceFlags)
$settingsPersistenceLoad = $settingsPersistenceType.GetMethod(
    "Load", $staticFlags)
$settingsPersistenceRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "AeroMirror-settings-roundtrip-" + [Guid]::NewGuid().ToString("N"))
$settingsPersistencePath = Join-Path $settingsPersistenceRoot "settings.ini"
try {
    [IO.Directory]::CreateDirectory($settingsPersistenceRoot) | Out-Null
    $settingsPersistencePathField.SetValue(
        $null, $settingsPersistencePath)

    foreach ($legacyValue in @("true", "false", "not-a-boolean")) {
        [IO.File]::WriteAllText(
            $settingsPersistencePath,
            "SettingsVersion=12`r`n" +
            "ReceiverName=Retained-$legacyValue`r`n" +
            "AutoFitWindow=False`r`n" +
            "FollowPhotosMediaCanvas=$legacyValue`r`n",
            (New-Object Text.UTF8Encoding($false)))
        $loadedLegacyMediaCanvas = $settingsPersistenceLoad.Invoke(
            $null, [object[]]@())
        Assert-True ($loadedLegacyMediaCanvas.SettingsVersion -eq 12 -and
            $loadedLegacyMediaCanvas.ReceiverName -eq
                "Retained-$legacyValue" -and
            -not [bool]$loadedLegacyMediaCanvas.AutoFitWindow) `
            "schema-12 legacy Photos value '$legacyValue' is ignored without resetting unrelated settings"
        $settingsPersistenceSave.Invoke(
            $loadedLegacyMediaCanvas, [object[]]@()) | Out-Null
        $savedLegacyProfile = [IO.File]::ReadAllText(
            $settingsPersistencePath)
        Assert-True (-not $savedLegacyProfile.Contains(
                "FollowPhotosMediaCanvas=") -and
            $savedLegacyProfile.Contains(
                "ReceiverName=Retained-$legacyValue") -and
            $savedLegacyProfile.Contains("AutoFitWindow=False")) `
            "saving schema 12 removes the retired Photos key and preserves unrelated values"
    }

    [IO.File]::WriteAllText(
        $settingsPersistencePath,
        "SettingsVersion=11`r`n" +
        "ReceiverName=Retained-v11`r`n" +
        "AlwaysOnTop=True`r`n" +
        "FollowPhotosMediaCanvas=True`r`n",
        (New-Object Text.UTF8Encoding($false)))
    $loadedV11MediaCanvas = $settingsPersistenceLoad.Invoke(
        $null, [object[]]@())
    Assert-True ($loadedV11MediaCanvas.SettingsVersion -eq 12 -and
        $loadedV11MediaCanvas.ReceiverName -eq "Retained-v11" -and
        [bool]$loadedV11MediaCanvas.AlwaysOnTop) `
        "a schema-11 profile advances while its retired Photos key is ignored"
    $settingsPersistenceSave.Invoke(
        $loadedV11MediaCanvas, [object[]]@()) | Out-Null
    Assert-True (-not [IO.File]::ReadAllText(
            $settingsPersistencePath).Contains(
                "FollowPhotosMediaCanvas=")) `
        "saving a migrated schema-11 profile drops the retired Photos key"
}
finally {
    if ([IO.Directory]::Exists($settingsPersistenceRoot)) {
        [IO.Directory]::Delete($settingsPersistenceRoot, $true)
    }
}
$contextSettingsField = $contextType.GetField("settings", $instanceFlags)
$normalizeReceiverName = $settingsType.GetMethod(
    "NormalizeReceiverNameForDiscovery", $staticFlags)
$buildUxPlayArguments = $contextType.GetMethod(
    "BuildUxPlayArguments", $instanceFlags)
Assert-True ($null -ne $contextSettingsField -and
    $null -ne $normalizeReceiverName -and
    $null -ne $buildUxPlayArguments) `
    "receiver arguments can be verified from normalized settings"
function Normalize-ReceiverName([string]$Value) {
    return [string]$normalizeReceiverName.Invoke(
        $null, [object[]]@($Value))
}
$ascii50 = "A" * 50
$ascii51 = "A" * 51
$cyrillicGlyph = [string][char]0x0416
$cyrillicBoundary = $cyrillicGlyph * 26
$emojiBoundary = [char]::ConvertFromUtf32(0x1F600) * 13
Assert-True ((Normalize-ReceiverName $ascii50) -eq $ascii50 -and
    [Text.Encoding]::UTF8.GetByteCount(
        (Normalize-ReceiverName $ascii50)) -eq 50) `
    "an exact 50-byte ASCII receiver name is preserved"
Assert-True ((Normalize-ReceiverName $ascii51) -eq ("A" * 50)) `
    "a 51-byte ASCII receiver name is reduced to the Bonjour-safe boundary"
Assert-True ((Normalize-ReceiverName $cyrillicBoundary) -eq
        ($cyrillicGlyph * 25) -and
    [Text.Encoding]::UTF8.GetByteCount(
        (Normalize-ReceiverName $cyrillicBoundary)) -eq 50) `
    "Cyrillic receiver names are not split inside a UTF-8 code point"
Assert-True ((Normalize-ReceiverName $emojiBoundary) -eq
        ([char]::ConvertFromUtf32(0x1F600) * 12) -and
    [Text.Encoding]::UTF8.GetByteCount(
        (Normalize-ReceiverName $emojiBoundary)) -eq 48) `
    "emoji receiver names preserve complete surrogate pairs"
Assert-True ((Normalize-ReceiverName "   ") -eq "AeroMirror") `
    "a blank receiver name uses the same nonblank fallback as native DNS-SD"
$replacementCharacter = [string][char]0xfffd
$unpairedHigh = "A" + [char]0xd800 + "B"
$unpairedLow = "A" + [char]0xdc00 + "B"
Assert-True ((Normalize-ReceiverName $unpairedHigh) -eq
        ("A" + $replacementCharacter + "B") -and
    (Normalize-ReceiverName $unpairedLow) -eq
        ("A" + $replacementCharacter + "B")) `
    "unpaired UTF-16 surrogates are deterministically replaced before persistence and UTF-8 byte counting"
$controlName = "A" + [char]0x00 + [char]0x09 + [char]0x1f +
    "B" + [char]0x7f + "C"
Assert-True ((Normalize-ReceiverName $controlName) -eq "ABC") `
    "hand-edited C0 and DEL controls are removed before the receiver name reaches native argument parsing"
function Invoke-UxPlayArguments($Settings) {
    $contextSettingsField.SetValue($context, $Settings)
    return [string]$buildUxPlayArguments.Invoke($context, [object[]]@())
}
$resilientAudioArgument = '-as "wasapi2sink continue-on-error=true"'
$legacyAutomaticSettings = [Activator]::CreateInstance($settingsType, $true)
$legacyAutomaticSettings.SettingsVersion = 10
$legacyAutomaticSettings.Renderer = "auto"
$migrateRendererDefault.Invoke(
    $null, [object[]]@($legacyAutomaticSettings)) | Out-Null
Assert-True ($legacyAutomaticSettings.SettingsVersion -eq 11 -and
    $legacyAutomaticSettings.Renderer -eq "d3d11") `
    "a legacy automatic renderer profile migrates to pinned Direct3D 11"
$legacyDefaultAudioArguments = Invoke-UxPlayArguments `
    $legacyAutomaticSettings
Assert-True ($legacyDefaultAudioArguments.Contains(
        $resilientAudioArgument)) `
    "an existing default-audio profile receives resilient WASAPI2 output without a settings migration"
$explicitD3D12Settings = [Activator]::CreateInstance($settingsType, $true)
$explicitD3D12Settings.SettingsVersion = 10
$explicitD3D12Settings.Renderer = "d3d12"
$migrateRendererDefault.Invoke(
    $null, [object[]]@($explicitD3D12Settings)) | Out-Null
Assert-True ($explicitD3D12Settings.SettingsVersion -eq 11 -and
    $explicitD3D12Settings.Renderer -eq "d3d12") `
    "the stability migration preserves an explicit Direct3D 12 choice"
$legacyMediaCanvasSettings = [Activator]::CreateInstance(
    $settingsType, $true)
$legacyMediaCanvasSettings.SettingsVersion = 11
$legacyMediaCanvasSettings.ReceiverName = "Retained-migration"
$legacyMediaCanvasSettings.AutoFitWindow = $false
$migrateCurrentSettingsVersion.Invoke(
    $null, [object[]]@($legacyMediaCanvasSettings)) | Out-Null
Assert-True ($legacyMediaCanvasSettings.SettingsVersion -eq 12 -and
    $legacyMediaCanvasSettings.ReceiverName -eq "Retained-migration" -and
    -not [bool]$legacyMediaCanvasSettings.AutoFitWindow) `
    "current-version migration preserves unrelated profile values"
$currentMediaCanvasSettings = [Activator]::CreateInstance(
    $settingsType, $true)
$currentMediaCanvasSettings.ReceiverName = "Current-profile"
$migrateCurrentSettingsVersion.Invoke(
    $null, [object[]]@($currentMediaCanvasSettings)) | Out-Null
Assert-True ($currentMediaCanvasSettings.SettingsVersion -eq 12 -and
    $currentMediaCanvasSettings.ReceiverName -eq "Current-profile") `
    "a current-schema profile is left intact"
$settingsProbe = [Activator]::CreateInstance($settingsType, $true)
Assert-True ([int]$settingsProbe.SettingsVersion -eq 12 -and
    $settingsProbe.Renderer -eq "d3d11" -and
    $null -eq $settingsType.GetField(
        "FollowPhotosMediaCanvas", $instanceFlags)) `
    "new profiles keep stable D3D11 without a Photos fitting preference"
$defaultAudioArguments = Invoke-UxPlayArguments $settingsProbe
Assert-True ([regex]::Matches(
        $defaultAudioArguments,
        [regex]::Escape($resilientAudioArgument)).Count -eq 1) `
    "default audio emits exactly one resilient WASAPI2 sink argument"
$longNameSettings = [Activator]::CreateInstance($settingsType, $true)
$longNameSettings.ReceiverName = $cyrillicBoundary
$longNameArguments = Invoke-UxPlayArguments $longNameSettings
Assert-True ($longNameArguments.Contains(
        '-n "' + ($cyrillicGlyph * 25) + '"') -and
    -not $longNameArguments.Contains($cyrillicBoundary)) `
    "managed launch arguments use exactly the canonical name shown to iPhone"
$mutedSettings = [Activator]::CreateInstance($settingsType, $true)
$mutedSettings.AudioOutput = "mute"
$mutedArguments = Invoke-UxPlayArguments $mutedSettings
Assert-True (-not $mutedArguments.Contains($resilientAudioArgument) -and
    [regex]::IsMatch($mutedArguments, '(?:^|\s)-a(?:\s|$)')) `
    "mute disables audio without adding the managed WASAPI2 sink"
$advancedAudioSettings = [Activator]::CreateInstance($settingsType, $true)
$advancedAudioSettings.AdvancedArguments = '-as "fakesink sync=false"'
$advancedAudioArguments = Invoke-UxPlayArguments $advancedAudioSettings
Assert-True ($advancedAudioArguments.IndexOf(
        $resilientAudioArgument, [StringComparison]::Ordinal) -ge 0 -and
    $advancedAudioArguments.LastIndexOf(
        $advancedAudioSettings.AdvancedArguments,
        [StringComparison]::Ordinal) -gt
    $advancedAudioArguments.IndexOf(
        $resilientAudioArgument, [StringComparison]::Ordinal)) `
    "advanced arguments remain later and can override the managed audio sink"
Assert-True ([bool]$settingsProbe.AutoFitWindow) `
    "automatic renderer aspect fitting is enabled for a new settings profile"
$settingsProbe.AutoFitWindow = $false
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True (-not [bool]$settingsProbe.AutoFitWindow) `
    "settings normalization preserves an explicit automatic-fit opt-out"
$mediaCanvasArgumentsBefore = Invoke-UxPlayArguments $settingsProbe
$mediaCanvasSettingsCopy = $settingsProbe.Copy()
$mediaCanvasArgumentsAfter = Invoke-UxPlayArguments $mediaCanvasSettingsCopy
Assert-True ($mediaCanvasArgumentsAfter -eq $mediaCanvasArgumentsBefore) `
    "automatic Photos window fitting has no native receiver argument delta"
Invoke-UxPlayArguments $settingsProbe | Out-Null
$hasValidStreamWindowPlacement = $settingsType.GetMethod(
    "HasValidStreamWindowPlacement", $instanceFlags)
Assert-True ($null -ne $hasValidStreamWindowPlacement) `
    "persisted renderer placement validation exists"
$settingsProbe.StreamWindowLeft = -1200
$settingsProbe.StreamWindowTop = 80
$settingsProbe.StreamWindowWidth = 620
$settingsProbe.StreamWindowHeight = 920
$settingsProbe.StreamWindowDpi = 144
Assert-True ([bool]$hasValidStreamWindowPlacement.Invoke(
        $settingsProbe, [object[]]@())) `
    "a valid mixed-monitor renderer placement survives normalization"
$settingsCopy = $settingsType.GetMethod("Copy", $instanceFlags).Invoke(
    $settingsProbe, [object[]]@())
Assert-True ($settingsCopy.StreamWindowLeft -eq -1200 -and
    $settingsCopy.StreamWindowWidth -eq 620 -and
    $settingsCopy.StreamWindowDpi -eq 144) `
    "ordinary settings edits preserve the saved stream-window placement"
$settingsProbe.StreamWindowDpi = 0
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True ($settingsProbe.StreamWindowWidth -eq 0 -and
    $settingsProbe.StreamWindowHeight -eq 0) `
    "an incomplete persisted renderer placement is cleared as one unit"
$settingsProbe.PairingMode = "garbage"
$settingsProbe.FixedPin = "1234"
$settingsProbe.QualityPreset = "unknown-quality"
$settingsProbe.Renderer = "vulkan"
$settingsProbe.LatencyProfile = "turbo"
$settingsProbe.AudioOutput = "custom"
$settingsProbe.ThemeMode = "sepia"
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True ($settingsProbe.PairingMode -eq "none") `
    "an unknown pairing mode becomes unprotected so network policy fails closed"
Assert-True ($settingsProbe.FixedPin -eq "") `
    "an invalid pairing mode does not retain a misleading PIN"
Assert-True ($settingsProbe.QualityPreset -eq "1080p60") `
    "an unknown quality preset receives the stable default"
Assert-True ($settingsProbe.Renderer -eq "d3d11") `
    "an unknown renderer receives the pinned Direct3D 11 default"
Assert-True ($settingsProbe.LatencyProfile -eq "balanced") `
    "an unknown latency profile receives the balanced default"
Assert-True ($settingsProbe.AudioOutput -eq "default") `
    "an unknown audio output receives the system default"
Assert-True ((Invoke-UxPlayArguments $settingsProbe).Contains(
        $resilientAudioArgument)) `
    "normalized unknown audio output receives resilient WASAPI2 output"
Assert-True ($settingsProbe.ThemeMode -eq "system") `
    "an unknown theme follows Windows"

$settingsProbe.PairingMode = "password"
$settingsProbe.FixedPin = "1234"
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True ($settingsProbe.PairingMode -eq "none") `
    "the obsolete password mode migrates to the fail-closed unprotected state"
$settingsProbe.PairingMode = "pin"
$settingsProbe.FixedPin = -join @(
    [char]0xFF11, [char]0xFF12, [char]0xFF13, [char]0xFF14)
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True ($settingsProbe.PairingMode -eq "none") `
    "PIN protection requires four ASCII digits"
$settingsProbe.PairingMode = " PIN "
$settingsProbe.FixedPin = " 0427 "
$normalizeSettings.Invoke($settingsProbe, [object[]]@()) | Out-Null
Assert-True ($settingsProbe.PairingMode -eq "pin" -and
    $settingsProbe.FixedPin -eq "0427") `
    "a valid persisted PIN is canonicalized and preserved"

$atomicWriter = $settingsType.GetMethod(
    "WriteAllLinesAtomically", $staticFlags)
Assert-True ($null -ne $atomicWriter) "atomic settings writer exists"
$atomicRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "AeroMirror-settings-atomic-test-" + [Guid]::NewGuid().ToString("N"))
$atomicPath = Join-Path $atomicRoot "settings.ini"
try {
    [IO.Directory]::CreateDirectory($atomicRoot) | Out-Null
    [IO.File]::WriteAllText($atomicPath, "old=value")
    $atomicLines = [Array]::CreateInstance([string], 2)
    $atomicLines.SetValue("SettingsVersion=12", 0)
    $atomicLines.SetValue("PairingMode=none", 1)
    $atomicArguments = [Array]::CreateInstance([object], 2)
    $atomicArguments.SetValue([string]$atomicPath, 0)
    $atomicArguments.SetValue($atomicLines, 1)
    $atomicWriter.Invoke($null, $atomicArguments) | Out-Null
    $atomicText = [IO.File]::ReadAllText($atomicPath)
    Assert-True ($atomicText.Contains("SettingsVersion=12") -and
        $atomicText.Contains("PairingMode=none")) `
        "atomic settings replacement publishes the complete new file"
    Assert-True (([IO.Directory]::GetFiles(
        $atomicRoot, "*.tmp")).Count -eq 0) `
        "atomic settings replacement leaves no temporary file"
}
finally {
    if ([IO.Directory]::Exists($atomicRoot)) {
        [IO.Directory]::Delete($atomicRoot, $true)
    }
}

$updateType = $assembly.GetType(
    "AirPlayReceiverMvp.UpdateService", $true)
$obsoleteInstallerCompatibility = $updateType.GetMethod(
    "IsCompatibleInstallerName", $staticFlags)
$updateServiceSource = [IO.File]::ReadAllText(
    (Join-Path $sourceRoot "Updates\UpdateService.cs"))
Assert-True ($null -eq $obsoleteInstallerCompatibility -and
    $updateServiceSource.Contains(
        '"AeroMirror-Setup-" + latest.ToString(3) + ".exe"') -and
    -not $updateServiceSource.Contains("IsCompatibleInstallerName")) `
    "the updater selects the one exact versioned Setup asset without a redundant architecture-name filter"
$tryParseVersion = $updateType.GetMethod("TryParseVersion", $staticFlags)
Assert-True ($null -ne $tryParseVersion) `
    "release version parser exists"
function Invoke-VersionParse([string]$Value) {
    $arguments = [Array]::CreateInstance([object], 2)
    $arguments.SetValue([string]$Value, 0)
    $arguments.SetValue($null, 1)
    $parsed = [bool]$tryParseVersion.Invoke($null, $arguments)
    return [pscustomobject]@{
        Success = $parsed
        Version = $arguments[1]
    }
}
$threePartVersion = Invoke-VersionParse "v0.11.4"
Assert-True ($threePartVersion.Success -and
    $threePartVersion.Version.ToString() -eq "0.11.4") `
    "an exact three-part release version is accepted"
Assert-True (-not (Invoke-VersionParse "v0.11").Success) `
    "a two-part release version is rejected"
Assert-True (-not (Invoke-VersionParse "v0.11.4.1").Success) `
    "a four-part release version is rejected"
Assert-True (-not (Invoke-VersionParse "v0.11.4-beta").Success) `
    "a suffixed release version is rejected"

function Field([string]$Name) {
    $field = $contextType.GetField($Name, $instanceFlags)
    Assert-True ($null -ne $field) "field '$Name' exists"
    return $field
}

$activePid = Field "activeCorePid"
$mirrorActive = Field "mirrorSessionActive"
$recoveryPending = Field "lostConnectionRecoveryPending"
$recoveryPid = Field "lostConnectionRecoveryPid"
$recoveryDue = Field "lostConnectionRecoveryDueTicks"
$sessionEndedPending = Field "mirrorSessionEndedPending"
$sessionEndedDue = Field "mirrorSessionEndedDueTicks"
$settingsRestartDeferred = Field "settingsRestartDeferred"
$idleRenewalDue = Field "idleDiscoveryRenewalDueTicks"
$idleRenewalUsed = Field "idleDiscoveryRenewalUsed"
$sessionUnlockRefreshPending =
    Field "sessionUnlockDiscoveryRefreshPending"
$sessionUnlockRefreshDue = Field "sessionUnlockDiscoveryRefreshDueTicks"
$restartPending = Field "restartPending"
$coreReadyPending = Field "coreReadyPending"
$coreReadyChecks = Field "coreReadyChecks"
$coreReadinessAttempts = Field "coreReadinessRecoveryAttempts"
$coreReadinessPid = Field "coreReadinessPid"
$clientReadyPending = Field "coreClientActivityReadyPending"
$clientGraceDue = Field "clientActivityGraceDueTicks"
$physicalNetworkRestartDeferred = Field "physicalNetworkRestartDeferred"
$maintenanceSync = Field "postSessionMaintenanceSync"
$videoSizeSync = Field "videoSizeSync"
$streamWindowPlacementSync = Field "streamWindowPlacementSync"
$videoGeometryEventSequence = Field "videoGeometryEventSequence"
$pendingVideoSize = Field "pendingVideoSize"
$pendingVideoSizeDueUtc = Field "pendingVideoSizeDueUtc"
$pendingVideoSizeSequence = Field "pendingVideoSizeSequence"
$pendingVideoSizeIsAmbiguous =
    Field "pendingVideoSizeIsAmbiguousMediaCanvas"
$currentVideoSize = Field "currentVideoSize"
$currentVideoSizeSequence = Field "currentVideoSizeSequence"
$currentVideoSizeIsAmbiguous =
    Field "currentVideoSizeIsAmbiguousMediaCanvas"
$rawGeometryVideoSize = Field "rawGeometryVideoSize"
$rawGeometryVideoSizeGeneration = Field "rawGeometryVideoSizeGeneration"
$rawGeometryIsAmbiguous = Field "rawGeometryIsAmbiguousMediaCanvas"
$earlyDeviceFrameVideoSize = Field "earlyDeviceFrameVideoSize"
$deviceFrameVideoSize = Field "deviceFrameVideoSize"
$lastSuppressedVideoSize = Field "lastSuppressedVideoSize"
$persistablePlacementWindow =
    Field "persistableStreamWindowPlacementWindow"
$pendingPlacementWindow =
    Field "pendingStreamWindowPlacementWindow"
$startAfterNetwork = Field "startAfterNetworkCheck"
$refreshAfterNetwork = Field "discoveryRefreshAfterNetworkCheck"
$networkRefreshPending = Field "networkRefreshPending"
$networkRefreshDue = Field "networkRefreshDueTicks"
$socketsReady = Field "coreSocketsReady"
$httpMarkersReady = Field "coreHttpMarkersReady"
$httpPort = Field "coreHttpPort"
$httpResetStatus = Field "lostConnectionHttpResetStatus"
$httpResetPort = Field "lostConnectionHttpResetPort"
$dnsSdStatus = Field "coreDnsSdStatus"
$bleStatus = Field "coreBleStatus"
$discoveryRecoveryPending = Field "coreDiscoveryRecoveryPending"
$discoveryRecoveryAttempts = Field "coreDiscoveryRecoveryAttempts"
$discoveryRecoveryPid = Field "coreDiscoveryRecoveryPid"
$discoveryRecoveryDue = Field "coreDiscoveryRecoveryDueTicks"
$placeholderShowPending = Field "lostConnectionPlaceholderShowPending"
$placeholderClosePending = Field "lostConnectionPlaceholderClosePending"
$rendererHandoffPending = Field "lostConnectionRendererHandoffPending"
$lostStatePending = Field "lostConnectionLostStatePending"
$reconnectHintPending = Field "lostConnectionReconnectHintPending"
$recoveredStatePending = Field "lostConnectionRecoveredStatePending"
$feedbackEpisodeActive = Field "feedbackGapEpisodeActive"
$feedbackEpisodeCount = Field "feedbackGapEpisodeCount"
$feedbackLongest = Field "feedbackGapLongestSeconds"
$feedbackPlaceholderActive = Field "feedbackGapPlaceholderActive"
$feedbackPlaceholderDue = Field "feedbackGapPlaceholderDueTicks"
$feedbackMarkersReady = Field "feedbackHealthMarkersReady"
$feedbackPresentProofReady = Field "feedbackVideoPresentProofReady"
$feedbackPresentProofPid = Field "feedbackVideoPresentProofPid"
$feedbackVideoPending = Field "feedbackVideoRecoveryPending"
$feedbackVideoPid = Field "feedbackVideoRecoveryPid"
$feedbackVideoEpoch = Field "feedbackVideoRecoveryEpoch"
$feedbackVideoGapSeconds = Field "feedbackVideoRecoveryGapSeconds"
$feedbackVideoSession = Field "feedbackVideoRecoverySessionGeneration"
$feedbackMirrorStartArmExpected = Field "feedbackVideoMirrorStartArmExpected"
$feedbackVideoWaitDue = Field "feedbackVideoRecoveryWaitDueTicks"
$feedbackVideoCompleted = Field "feedbackVideoRecoveryCompletedCount"
$feedbackVideoHintCount = Field "feedbackVideoRecoveryHintCount"
$feedbackHandoffPending = Field "lostConnectionFeedbackHandoffPending"
$feedbackHandoffToken = Field "lostConnectionFeedbackHandoffToken"
$continuityToken = Field "lostConnectionContinuityToken"
$mirrorSessionGeneration = Field "mirrorSessionGeneration"
$coreProcess = Field "coreProcess"
$coreDiscoveryRefreshPendingRequest =
    Field "coreDiscoveryRefreshPendingRequest"
$coreDiscoveryRefreshPendingPid = Field "coreDiscoveryRefreshPendingPid"
$coreDiscoveryRefreshPendingPort = Field "coreDiscoveryRefreshPendingPort"
$coreDiscoveryRefreshDueTicks = Field "coreDiscoveryRefreshDueTicks"
$coreDiscoveryRefreshPhase = Field "coreDiscoveryRefreshPhase"
$coreDiscoveryRefreshFallbackPending =
    Field "coreDiscoveryRefreshFallbackPending"
$coreCommandSync = Field "coreCommandSync"
$fittedStreamWindow = Field "fittedStreamWindow"
$videoSizeWindow = Field "videoSizeWindow"
$rendererPolicyWindow = Field "rendererPolicyWindow"
$rendererPolicyApplied = Field "rendererPolicyApplied"
$rendererPolicyAlwaysOnTop = Field "rendererPolicyAlwaysOnTop"
$rendererPolicyShowInTaskbar = Field "rendererPolicyShowInTaskbar"
$exactVideoSizeFitSequence = Field "exactVideoSizeFitSequence"
$appliedVideoFitSize = Field "appliedVideoFitSize"
$appliedVideoFitTargetKind = Field "appliedVideoFitTargetKind"
$maintenanceSync.SetValue($context, (New-Object object))
$coreCommandSync.SetValue($context, (New-Object object))
$videoSizeSync.SetValue($context, (New-Object object))
$streamWindowPlacementSync.SetValue($context, (New-Object object))
$activePid.SetValue($context, 42)
$mirrorActive.SetValue($context, 1)
$markPlacementPersistable = $contextType.GetMethod(
    "MarkStreamWindowPlacementPersistable", $instanceFlags)
$canPersistPlacement = $contextType.GetMethod(
    "CanPersistStreamWindowPlacement", $instanceFlags)
$clearPlacementPersistence = $contextType.GetMethod(
    "ClearStreamWindowPlacementPersistence", $instanceFlags)
$updatePlacementAfterAutomaticFit = $contextType.GetMethod(
    "UpdateStreamWindowPlacementAfterAutomaticFit", $instanceFlags)
Assert-True ($null -ne $markPlacementPersistable -and
    $null -ne $canPersistPlacement -and
    $null -ne $clearPlacementPersistence -and
    $null -ne $updatePlacementAfterAutomaticFit) `
    "renderer placement persistence exposes a deterministic trust gate"
$placementWindow = [IntPtr]::new(501)
$otherPlacementWindow = [IntPtr]::new(502)
Assert-True (-not [bool]$canPersistPlacement.Invoke(
        $context, [object[]]@($placementWindow))) `
    "an unresolved provisional renderer cannot overwrite saved placement"
$markPlacementPersistable.Invoke(
    $context, [object[]]@($placementWindow)) | Out-Null
Assert-True ([bool]$canPersistPlacement.Invoke(
        $context, [object[]]@($placementWindow)) -and
    -not [bool]$canPersistPlacement.Invoke(
        $context, [object[]]@($otherPlacementWindow))) `
    "placement persistence is scoped to the explicitly trusted renderer"
$clearPlacementPersistence.Invoke(
    $context, [object[]]@($placementWindow)) | Out-Null
Assert-True (-not [bool]$canPersistPlacement.Invoke(
        $context, [object[]]@($placementWindow)) -and
    [IntPtr]$persistablePlacementWindow.GetValue($context) -eq
        [IntPtr]::Zero) `
    "renderer placement persistence is cleared after the session"
$sameDeviceAspect = $contextType.GetMethod(
    "HaveEquivalentDeviceFrameAspect", $staticFlags)
$likelyModernIPhoneFrame = $contextType.GetMethod(
    "IsLikelyModernIPhoneDeviceFrame", $staticFlags)
$knownAmbiguousMediaCanvas = $contextType.GetMethod(
    "IsKnownAmbiguousMediaCanvasGeometry", $staticFlags)
$resolveAutomaticVideo = $contextType.GetMethod(
    "ResolveAutomaticVideoSize", $instanceFlags)
$resolveManualFitVideo = $contextType.GetMethod(
    "ResolveManualFitVideoSize", $instanceFlags)
Assert-True ($null -ne $sameDeviceAspect -and
    $null -ne $likelyModernIPhoneFrame -and
    $null -ne $knownAmbiguousMediaCanvas -and
    $null -ne $resolveAutomaticVideo -and
    $null -ne $resolveManualFitVideo) `
    "renderer orientation and non-cropping Photos layout expose deterministic decisions"

$portraitFrame = [Drawing.Size]::new(998, 2160)
$landscapeFrame = [Drawing.Size]::new(3840, 1776)
$presentationCanvas = [Drawing.Size]::new(3840, 2160)
$provisionalPortrait = [Drawing.Size]::new(900, 1950)
$unknownCanvas = [Drawing.Size]::new(1200, 1000)
$sixteenByNinePortrait = [Drawing.Size]::new(1080, 1920)
$sixteenByNineLandscape = [Drawing.Size]::new(1920, 1080)
$rendererFitTargetKindType = $contextType.GetNestedType(
    "RendererFitTargetKind", [Reflection.BindingFlags]::NonPublic)
$shouldApplyRendererFitTarget = $contextType.GetMethod(
    "ShouldApplyRendererFitTarget", $staticFlags)
$haveExactRendererFitAspect = $contextType.GetMethod(
    "HaveExactRendererFitAspect", $staticFlags)
Assert-True ($null -ne $rendererFitTargetKindType -and
    $null -ne $shouldApplyRendererFitTarget -and
    $null -ne $haveExactRendererFitAspect) `
    "renderer fitting exposes deterministic target-class and exact-aspect decisions"
$noFitTarget = [Enum]::Parse($rendererFitTargetKindType, "None")
$deviceFrameFitTarget = [Enum]::Parse(
    $rendererFitTargetKindType, "DeviceFrame")
$mediaCanvasFitTarget = [Enum]::Parse(
    $rendererFitTargetKindType, "MediaCanvas")
$scaledLandscapeFrame = [Drawing.Size]::new(1920, 888)
$nearbyLandscapeFrame = [Drawing.Size]::new(1920, 900)
function Should-ApplyRendererFitTarget(
    [Drawing.Size]$AppliedSize,
    $AppliedKind,
    [Drawing.Size]$TargetSize,
    $TargetKind
) {
    return [bool]$shouldApplyRendererFitTarget.Invoke(
        $null,
        [object[]]@(
            $AppliedSize, $AppliedKind, $TargetSize, $TargetKind))
}
$blockedAutomaticMediaTarget = Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $presentationCanvas $mediaCanvasFitTarget
$sameSequenceRetryTarget = Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $presentationCanvas $mediaCanvasFitTarget
Assert-True ($blockedAutomaticMediaTarget -and $sameSequenceRetryTarget) `
    "a blocked automatic 3840x1776 device to 3840x2160 media transition remains eligible"
Assert-True (Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $presentationCanvas $mediaCanvasFitTarget) `
    "automatic Photos fitting distinguishes a same-orientation media target from a device frame"
Assert-True (Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $nearbyLandscapeFrame $deviceFrameFitTarget) `
    "an exact device-frame aspect change is fitted even when both targets are landscape"
Assert-True (-not (Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $scaledLandscapeFrame $deviceFrameFitTarget)) `
    "a scaled copy of the same exact aspect does not move the outer window"
Assert-True (Should-ApplyRendererFitTarget `
        $landscapeFrame $deviceFrameFitTarget `
        $scaledLandscapeFrame $mediaCanvasFitTarget) `
    "a target-class transition is fitted even when the exact aspect is unchanged"
Assert-True (-not (Should-ApplyRendererFitTarget `
        $presentationCanvas $mediaCanvasFitTarget `
        $presentationCanvas $mediaCanvasFitTarget)) `
    "a newer geometry sequence with the same target class and aspect is consumed without refitting"
Assert-True (-not (Should-ApplyRendererFitTarget `
        ([Drawing.Size]::Empty) $noFitTarget `
        ([Drawing.Size]::Empty) $noFitTarget)) `
    "an unresolved media canvas cannot force a fallback outer-window transition"
Assert-True ([bool]$haveExactRendererFitAspect.Invoke(
        $null, [object[]]@($landscapeFrame, $scaledLandscapeFrame)) -and
    -not [bool]$haveExactRendererFitAspect.Invoke(
        $null, [object[]]@($landscapeFrame, $presentationCanvas))) `
    "renderer fit equivalence distinguishes the device and Photos landscape aspects exactly"
$markPlacementPersistable.Invoke(
    $context, [object[]]@($placementWindow)) | Out-Null
$pendingPlacementWindow.SetValue($context, $placementWindow)
$updatePlacementAfterAutomaticFit.Invoke(
    $context, [object[]]@(
        $placementWindow, $presentationCanvas, $true)) | Out-Null
Assert-True ([IntPtr]$persistablePlacementWindow.GetValue($context) -eq
        [IntPtr]::Zero -and
    [IntPtr]$pendingPlacementWindow.GetValue($context) -eq
        [IntPtr]::Zero) `
    "an automatic Photos landscape fit cannot persist provisional placement"
$updatePlacementAfterAutomaticFit.Invoke(
    $context, [object[]]@(
        $placementWindow, $portraitFrame, $false)) | Out-Null
Assert-True ([IntPtr]$persistablePlacementWindow.GetValue($context) -eq
        $placementWindow -and
    [IntPtr]$pendingPlacementWindow.GetValue($context) -eq
        $placementWindow) `
    "a trusted device-frame fit remains eligible for normal placement persistence"
$updatePlacementAfterAutomaticFit.Invoke(
    $context, [object[]]@(
        $placementWindow, $presentationCanvas, $true)) | Out-Null
Assert-True ([bool]$sameDeviceAspect.Invoke(
        $null, [object[]]@($portraitFrame, $landscapeFrame))) `
    "portrait and physical landscape frames share the device aspect"
Assert-True (-not [bool]$sameDeviceAspect.Invoke(
        $null, [object[]]@($portraitFrame, $presentationCanvas))) `
    "the Photos presentation canvas does not impersonate device rotation"
Assert-True ([bool]$likelyModernIPhoneFrame.Invoke(
        $null, [object[]]@($portraitFrame)) -and
    [bool]$likelyModernIPhoneFrame.Invoke(
        $null, [object[]]@($landscapeFrame))) `
    "phone-shaped portrait and landscape markers qualify as early device frames"
Assert-True (-not [bool]$likelyModernIPhoneFrame.Invoke(
        $null, [object[]]@($presentationCanvas)) -and
    -not [bool]$likelyModernIPhoneFrame.Invoke(
        $null, [object[]]@($sixteenByNinePortrait))) `
    "a generic 16:9 canvas is never guessed to be the early iPhone baseline"
Assert-True ([bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3840, 2160, 3840, 2160, 0, 0, 3840, 2160))) `
    "the recorded direct-in-Photos 4K canvas signature is treated as ambiguous"
Assert-True (-not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3840, 1776, 3840, 1776, 0, 192, 3840, 1776)) -and
    -not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            1920, 1080, 1920, 1080, 0, 0, 1920, 1080))) `
    "real landscape and non-matching 16:9 streams are not rejected by the Photos signature"
Assert-True (-not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3840, 2160, 3840, 2160, 1, 0, 3840, 2160)) -and
    -not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3840, 2160, 3839, 2160, 0, 0, 3840, 2160)) -and
    -not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3840, 2160, 3840, 2160, 0, 0, 3840, 2159)) -and
    -not [bool]$knownAmbiguousMediaCanvas.Invoke(
        $null, [object[]]@(
            3839, 2160, 3840, 2160, 0, 0, 3840, 2160))) `
    "every correlated 4K geometry component must match the observed Photos signature exactly"

function Resolve-AutomaticVideoSize(
    [Drawing.Size]$VideoSize,
    [bool]$AmbiguousMediaCanvas = $false
) {
    $arguments = [object[]]@(
        $VideoSize, $AmbiguousMediaCanvas, $false, $false)
    $resolved = [Drawing.Size]$resolveAutomaticVideo.Invoke(
        $context, $arguments)
    return [pscustomobject]@{
        Size = $resolved
        OrientationAuthoritative = [bool]$arguments[2]
        SuppressionChanged = [bool]$arguments[3]
    }
}

$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$automaticPhotosArgumentsBefore = Invoke-UxPlayArguments $settingsProbe
$unlearnedCanvasResult = Resolve-AutomaticVideoSize `
    $presentationCanvas $true
Assert-True (-not $unlearnedCanvasResult.OrientationAuthoritative -and
    $unlearnedCanvasResult.Size -eq $provisionalPortrait -and
    $unlearnedCanvasResult.SuppressionChanged -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty) `
    "a direct-in-Photos canvas uses the portrait fallback without seeding the device baseline"
$directMediaPortrait = Resolve-AutomaticVideoSize $portraitFrame
Assert-True ($directMediaPortrait.OrientationAuthoritative -and
    $directMediaPortrait.Size -eq $portraitFrame) `
    "a later phone-shaped frame recovers a direct-in-Photos session to portrait"
Assert-True ((Invoke-UxPlayArguments $settingsProbe) -eq
        $automaticPhotosArgumentsBefore) `
    "automatic Photos target selection does not alter UxPlay arguments"
$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$unlearnedCanvasResult = Resolve-AutomaticVideoSize `
    $presentationCanvas $false
Assert-True ($unlearnedCanvasResult.OrientationAuthoritative -and
    $unlearnedCanvasResult.Size -eq $presentationCanvas -and
    -not $unlearnedCanvasResult.SuppressionChanged) `
    "a 4K landscape frame without the complete Photos signature remains valid"
$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$portraitResult = Resolve-AutomaticVideoSize $portraitFrame
Assert-True ($portraitResult.OrientationAuthoritative -and
    $portraitResult.Size -eq $portraitFrame -and
    -not $portraitResult.SuppressionChanged) `
    "the first exact frame establishes session orientation"
$photoResult = Resolve-AutomaticVideoSize $presentationCanvas $true
Assert-True (-not $photoResult.OrientationAuthoritative -and
    $photoResult.Size -eq $portraitFrame -and
    $photoResult.SuppressionChanged -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        $portraitFrame) `
    "998x2160 to the exact Photos canvas keeps the trusted portrait window target"
$repeatedPhotoResult = Resolve-AutomaticVideoSize $presentationCanvas $true
Assert-True (-not $repeatedPhotoResult.OrientationAuthoritative -and
    $repeatedPhotoResult.Size -eq $portraitFrame -and
    -not $repeatedPhotoResult.SuppressionChanged) `
    "a stable presentation canvas resolves to the same portrait target"
$manualPhotoFit = [Drawing.Size]$resolveManualFitVideo.Invoke(
    $context, [object[]]@($presentationCanvas, $true))
Assert-True ($manualPhotoFit -eq $portraitFrame) `
    "manual tray fitting uses the automatic Photos portrait target"
Assert-True ($null -eq $contextType.GetMethod(
        "ResolveAutomaticPresentationScale", $staticFlags)) `
    "Photos presentation cannot select a cover scale without a trusted content rectangle"
$portraitReturnResult = Resolve-AutomaticVideoSize $portraitFrame
Assert-True ($portraitReturnResult.OrientationAuthoritative -and
    $portraitReturnResult.Size -eq $portraitFrame) `
    "returning from Photos restores authoritative portrait input"

$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$landscapeBeforeMedia = Resolve-AutomaticVideoSize $landscapeFrame
$mediaAfterLandscape = Resolve-AutomaticVideoSize `
    $presentationCanvas $true
Assert-True ($landscapeBeforeMedia.OrientationAuthoritative -and
    $landscapeBeforeMedia.Size -eq $landscapeFrame -and
    $mediaAfterLandscape.Size -eq $landscapeFrame -and
    -not $mediaAfterLandscape.OrientationAuthoritative -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        $landscapeFrame) `
    "3840x1776 device landscape retains its shape for a Photos class transition"
$portraitAfterLandscapeMedia = Resolve-AutomaticVideoSize $portraitFrame
Assert-True ($portraitAfterLandscapeMedia.OrientationAuthoritative -and
    $portraitAfterLandscapeMedia.Size -eq $portraitFrame -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        $portraitFrame) `
    "a trusted portrait frame restores portrait after landscape device and media targets"

$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
Resolve-AutomaticVideoSize $portraitFrame | Out-Null
$landscapeResult = Resolve-AutomaticVideoSize $landscapeFrame
Assert-True ($landscapeResult.OrientationAuthoritative -and
    $landscapeResult.Size -eq $landscapeFrame) `
    "998x2160 to 3840x1776 accepts a real landscape rotation"
$physicalPortraitResult = Resolve-AutomaticVideoSize $portraitFrame
Assert-True ($physicalPortraitResult.OrientationAuthoritative -and
    $physicalPortraitResult.Size -eq $portraitFrame) `
    "the physical rotation sequence can return to portrait"
$unknownResult = Resolve-AutomaticVideoSize $unknownCanvas
Assert-True (-not $unknownResult.OrientationAuthoritative -and
    $unknownResult.Size -eq $portraitFrame) `
    "an unknown non-device ratio conservatively retains current orientation"

$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$sixteenByNinePortraitResult =
    Resolve-AutomaticVideoSize $sixteenByNinePortrait
$sixteenByNineLandscapeResult =
    Resolve-AutomaticVideoSize $sixteenByNineLandscape
Assert-True ($sixteenByNinePortraitResult.OrientationAuthoritative -and
    $sixteenByNinePortraitResult.Size -eq $sixteenByNinePortrait -and
    $sixteenByNineLandscapeResult.OrientationAuthoritative -and
    $sixteenByNineLandscapeResult.Size -eq $sixteenByNineLandscape) `
    "a physical 16:9 iPhone can rotate after its first exact frame seeds the baseline"

$currentVideoSize.SetValue($context, $presentationCanvas)
Resolve-AutomaticVideoSize $presentationCanvas $true | Out-Null
Assert-True ([Drawing.Size]$currentVideoSize.GetValue($context) -eq
    $presentationCanvas) `
    "orientation classification preserves the raw stream size for diagnostics and manual fitting"
$currentVideoSize.SetValue($context, [Drawing.Size]::Empty)
$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)

$observe = $contextType.GetMethod("ObserveCoreOutput", $instanceFlags)
Assert-True ($null -ne $observe) "core-output observer exists"
$observeVideoPresentation = $contextType.GetMethod(
    "ObserveRecoveredVideoPresentation", $instanceFlags)
$proofReadyMarker =
    "AEROMIRROR_VIDEO_PRESENT_PROOF_READY codec=h264 videosink=d3d11videosink"
$activePid.SetValue($context, 43)
$observeVideoPresentation.Invoke(
    $context, [object[]]@(43, $proofReadyMarker)) | Out-Null
$observeVideoPresentation.Invoke(
    $context, [object[]]@(42, $proofReadyMarker)) | Out-Null
Assert-True ([int]$feedbackPresentProofReady.GetValue($context) -eq 1 -and
    [int]$feedbackPresentProofPid.GetValue($context) -eq 43) `
    "late proof capability output from a detached core cannot overwrite the current core owner"
$feedbackPresentProofReady.SetValue($context, 0)
$feedbackPresentProofPid.SetValue($context, 0)
$activePid.SetValue($context, 42)
$observeSocketReady = $contextType.GetMethod(
    "ObserveCoreSocketReady", $instanceFlags)
$handleNativeDiscoveryTimeout = $contextType.GetMethod(
    "HandleNativeDiscoveryRefreshTimeout", $instanceFlags)
Assert-True ($null -ne $observeSocketReady) `
    "generic native socket readiness has a process-scoped observer"
Assert-True ($null -ne $handleNativeDiscoveryTimeout) `
    "native discovery timeout exposes a deterministic maintenance boundary"

$httpMarkersReady.SetValue($context, 0)
$httpPort.SetValue($context, 0)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$recoveryPending.SetValue($context, 0)
$recoveryPid.SetValue($context, 0)
$observe.Invoke(
    $context,
    [object[]]@(41,
        "AEROMIRROR_HTTP_READY stage=initial port=53999")) | Out-Null
Assert-True ([int]$httpMarkersReady.GetValue($context) -eq 0 -and
    [int]$httpPort.GetValue($context) -eq 0) `
    "an HTTP-ready marker from a stale native PID is ignored"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=initial port=53999")) | Out-Null
Assert-True ([int]$httpMarkersReady.GetValue($context) -eq 1 -and
    [int]$httpPort.GetValue($context) -eq 53999 -and
    [int]$socketsReady.GetValue($context) -eq 1) `
    "the initial marker establishes native capability and advertised AirPlay port"

# Same-process discovery command protocol: a native client can defer a
# correlated request for longer than the shell's terminal-result deadline.
# Only ACCEPTED starts that deadline; stale request/PID/port frames cannot
# mutate the current command state.
$coreDiscoveryRefreshPendingRequest.SetValue($context, [long]700)
$coreDiscoveryRefreshPendingPid.SetValue($context, 42)
$coreDiscoveryRefreshPendingPort.SetValue($context, 53999)
$coreDiscoveryRefreshFallbackPending.SetValue($context, 1)
$coreDiscoveryRefreshPhase.SetValue($context, 0)
$coreDiscoveryRefreshDueTicks.SetValue(
    $context, [DateTime]::UtcNow.AddSeconds(12).Ticks)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "audio progress`rAEROMIRROR_DISCOVERY_REFRESH_DEFERRED request=700 reason=client-active pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
Assert-True ([int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 0 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -gt 0) `
    "a carriage-return-prefixed embedded marker cannot mutate command correlation state; only the exact separately framed marker below is accepted"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_DEFERRED request=699 reason=client-active pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
Assert-True ([long]$coreDiscoveryRefreshPendingRequest.GetValue($context) -eq
        700 -and
    [int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 0 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -gt 0) `
    "a stale deferred request cannot suspend the current command deadline"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_DEFERRED request=700 reason=client-active pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
Assert-True ([long]$coreDiscoveryRefreshPendingRequest.GetValue($context) -eq
        700 -and
    [int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 1 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -eq 0 -and
    [int]$coreDiscoveryRefreshFallbackPending.GetValue($context) -eq 1) `
    "a correlated native deferral suspends the legacy timeout without losing its fallback"
$handleNativeDiscoveryTimeout.Invoke($context, @()) | Out-Null
Assert-True ([long]$coreDiscoveryRefreshPendingRequest.GetValue($context) -eq
        700 -and
    [int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 1 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -eq 0 -and
    -not [bool]$restartPending.GetValue($context)) `
    "a timeout recheck cannot claim a request after correlated deferral suspended its deadline"
$acceptedBefore = [DateTime]::UtcNow.Ticks
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_ACCEPTED request=700 next_generation=9 pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
$acceptedDue = [long]$coreDiscoveryRefreshDueTicks.GetValue($context)
Assert-True ([int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 2 -and
    $acceptedDue -ge $acceptedBefore + [TimeSpan]::FromSeconds(11).Ticks -and
    $acceptedDue -le [DateTime]::UtcNow.AddSeconds(13).Ticks) `
    "native acceptance starts a fresh bounded terminal-result deadline"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_DEFERRED request=700 reason=client-active pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
Assert-True ([int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 2 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -eq $acceptedDue) `
    "a late duplicate deferral cannot roll an accepted request back to an unbounded phase"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "prefix AEROMIRROR_DISCOVERY_REFRESH_READY request=700 generation=9 pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_READY request=700 generation=9 pid=43 raop_port=53999 airplay_port=53999")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_READY request=700 generation=9 pid=42 raop_port=53998 airplay_port=53999")) |
    Out-Null
Assert-True ([long]$coreDiscoveryRefreshPendingRequest.GetValue($context) -eq
        700 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -eq $acceptedDue) `
    "embedded, wrong-PID, and wrong-port terminal markers leave the bounded request pending"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_DISCOVERY_REFRESH_READY request=700 generation=9 pid=42 raop_port=53999 airplay_port=53999")) |
    Out-Null
Assert-True ([long]$coreDiscoveryRefreshPendingRequest.GetValue($context) -eq
        0 -and
    [int]$coreDiscoveryRefreshPendingPid.GetValue($context) -eq 0 -and
    [int]$coreDiscoveryRefreshPendingPort.GetValue($context) -eq 0 -and
    [long]$coreDiscoveryRefreshDueTicks.GetValue($context) -eq 0 -and
    [int]$coreDiscoveryRefreshPhase.GetValue($context) -eq 0 -and
    [int]$coreDiscoveryRefreshFallbackPending.GetValue($context) -eq 0 -and
    [int]$dnsSdStatus.GetValue($context) -eq 1) `
    "one correlated same-PID same-port READY atomically settles and clears its command"
$dnsSdStatus.SetValue($context, 0)

$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$observe.Invoke(
    $context,
    [object[]]@(41,
        "AEROMIRROR_HTTP_READY stage=reset port=53999")) | Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$socketsReady.GetValue($context) -eq 0) `
    "a reset marker from a stale native PID cannot satisfy recovery"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=reset port=54000")) | Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq -1 -and
    [int]$httpResetPort.GetValue($context) -eq 54000 -and
    [int]$socketsReady.GetValue($context) -eq 0) `
    "a reset marker for a different port is rejected"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=reset port=53999")) | Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 1 -and
    [int]$httpResetPort.GetValue($context) -eq 53999 -and
    [int]$socketsReady.GetValue($context) -eq 1) `
    "a matching reset marker explicitly confirms same-process recovery"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_FAILED stage=reset expected_port=53999 port=0 code=-1")) |
    Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq -1 -and
    [int]$socketsReady.GetValue($context) -eq 0) `
    "a native reset-bind failure clears readiness while full-process recovery begins"

$recoveryPending.SetValue($context, 0)
$recoveryPid.SetValue($context, 0)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 1)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_FAILED stage=reset expected_port=53999 port=0 code=-1")) |
    Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$socketsReady.GetValue($context) -eq 1) `
    "an out-of-sequence reset failure marker cannot overwrite healthy listener state"

$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$httpMarkersReady.SetValue($context, 0)
$httpPort.SetValue($context, 0)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$observeSocketReady.Invoke($context, [object[]]@(42)) | Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 2 -and
    [int]$socketsReady.GetValue($context) -eq 1) `
    "a legacy core can use only the bounded post-fatal generic listener fallback"
$recoveryPending.SetValue($context, 0)
$recoveryPid.SetValue($context, 0)
$recoveryDue.SetValue($context, [long]0)
$httpMarkersReady.SetValue($context, 0)
$httpPort.SetValue($context, 0)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$getStableVideoSize = $contextType.GetMethod(
    "GetStableVideoSize", $instanceFlags)
$decideVideoSizeCandidateAction = $contextType.GetMethod(
    "DecideVideoSizeCandidateAction", $staticFlags)
Assert-True ($null -ne $getStableVideoSize -and
    $null -ne $decideVideoSizeCandidateAction) `
    "video-size debounce exposes a deterministic stable-frame boundary"
function Decide-VideoSizeCandidate(
    [Drawing.Size]$CurrentSize,
    [bool]$CurrentIsMediaCanvas,
    [Drawing.Size]$PendingSize,
    [bool]$PendingIsMediaCanvas,
    [Drawing.Size]$ObservedSize,
    [bool]$ObservedIsMediaCanvas
) {
    return [string]$decideVideoSizeCandidateAction.Invoke(
        $null,
        [object[]]@(
            $CurrentSize, $CurrentIsMediaCanvas,
            $PendingSize, $PendingIsMediaCanvas,
            $ObservedSize, $ObservedIsMediaCanvas))
}
Assert-True ((Decide-VideoSizeCandidate `
        $portraitFrame $false ([Drawing.Size]::Empty) $false `
        $portraitFrame $false) -eq "None") `
    "an already-stable geometry is ignored without opening another debounce"
Assert-True ((Decide-VideoSizeCandidate `
        $portraitFrame $false $landscapeFrame $false `
        $portraitFrame $false) -eq "CancelPending") `
    "a return to the stable geometry cancels a superseded candidate"
Assert-True ((Decide-VideoSizeCandidate `
        $portraitFrame $false $landscapeFrame $false `
        $landscapeFrame $false) -eq "RetainPendingDeadline") `
    "an identical pending candidate retains its original debounce deadline"
Assert-True ((Decide-VideoSizeCandidate `
        $portraitFrame $false $presentationCanvas $false `
        $presentationCanvas $true) -eq "ArmPending") `
    "a media-canvas class change arms a distinct geometry candidate"

# Recorded Photos sequence: 998x2160 was followed by the app's 3840x2160
# presentation canvas about 130 ms later, before the 350 ms debounce elapsed.
$videoGeometryEventSequence.SetValue($context, [long]0)
$pendingVideoSize.SetValue($context, [Drawing.Size]::Empty)
$pendingVideoSizeDueUtc.SetValue($context, [DateTime]::MinValue)
$pendingVideoSizeSequence.SetValue($context, [long]0)
$pendingVideoSizeIsAmbiguous.SetValue($context, $false)
$currentVideoSize.SetValue($context, [Drawing.Size]::Empty)
$currentVideoSizeSequence.SetValue($context, [long]0)
$currentVideoSizeIsAmbiguous.SetValue($context, $false)
$rawGeometryVideoSize.SetValue($context, [Drawing.Size]::Empty)
$rawGeometryVideoSizeGeneration.SetValue($context, 0)
$rawGeometryIsAmbiguous.SetValue($context, $false)
$earlyDeviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=998 height0=2160 source=998x2160 aux=1421x0 encoded=998x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=998x2160 encoded=998x2160")) |
    Out-Null
$firstPortraitDue = [DateTime]$pendingVideoSizeDueUtc.GetValue($context)
$firstPortraitSequence = [long]$pendingVideoSizeSequence.GetValue($context)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=998 height0=2160 source=998x2160 aux=1421x0 encoded=998x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=998x2160 encoded=998x2160")) |
    Out-Null
Assert-True ([Drawing.Size]$earlyDeviceFrameVideoSize.GetValue($context) -eq
        $portraitFrame -and
    [DateTime]$pendingVideoSizeDueUtc.GetValue($context) -eq
        $firstPortraitDue -and
    [long]$pendingVideoSizeSequence.GetValue($context) -gt
        $firstPortraitSequence -and
    [long]$videoGeometryEventSequence.GetValue($context) -eq
        [long]$pendingVideoSizeSequence.GetValue($context)) `
    "a repeated phone candidate advances event sequence without postponing its original debounce"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=3840 height0=2160 source=3840x2160 aux=0x0 encoded=3840x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=3840x2160 encoded=3840x2160")) |
    Out-Null
Assert-True ([Drawing.Size]$earlyDeviceFrameVideoSize.GetValue($context) -eq
    $portraitFrame -and
    [Drawing.Size]$pendingVideoSize.GetValue($context) -eq
        $presentationCanvas -and
    [bool]$pendingVideoSizeIsAmbiguous.GetValue($context)) `
    "the later Photos canvas keeps both the early device frame and its ambiguous classification"
$recordedCanvasSequence =
    [long]$pendingVideoSizeSequence.GetValue($context)
$pendingVideoSizeDueUtc.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(-1))
$stableArguments = [object[]]@([long]0, $false)
$recordedStableCanvas = [Drawing.Size]$getStableVideoSize.Invoke(
    $context, $stableArguments)
$recordedPhotosResult = Resolve-AutomaticVideoSize `
    $recordedStableCanvas ([bool]$stableArguments[1])
Assert-True ($recordedStableCanvas -eq $presentationCanvas -and
    [long]$stableArguments[0] -eq $recordedCanvasSequence -and
    [bool]$stableArguments[1] -and
    -not $recordedPhotosResult.OrientationAuthoritative -and
    $recordedPhotosResult.Size -eq $portraitFrame -and
    $recordedPhotosResult.SuppressionChanged -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        $portraitFrame) `
    "the recorded portrait-to-Photos sequence commits its newest event while retaining the portrait target and baseline"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=3840 height0=2160 source=3840x2160 aux=0x0 encoded=3840x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=3840x2160 encoded=3840x2160")) |
    Out-Null
Assert-True ([Drawing.Size]$pendingVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    [DateTime]$pendingVideoSizeDueUtc.GetValue($context) -eq
        [DateTime]::MinValue -and
    [long]$currentVideoSizeSequence.GetValue($context) -eq
        $recordedCanvasSequence -and
    [long]$videoGeometryEventSequence.GetValue($context) -gt
        $recordedCanvasSequence) `
    "an identical stable candidate advances observation order without reopening debounce"

$pendingVideoSize.SetValue($context, [Drawing.Size]::Empty)
$pendingVideoSizeDueUtc.SetValue($context, [DateTime]::MinValue)
$pendingVideoSizeSequence.SetValue($context, [long]0)
$pendingVideoSizeIsAmbiguous.SetValue($context, $false)
$currentVideoSize.SetValue($context, [Drawing.Size]::Empty)
$currentVideoSizeSequence.SetValue($context, [long]0)
$currentVideoSizeIsAmbiguous.SetValue($context, $false)
$rawGeometryVideoSize.SetValue($context, [Drawing.Size]::Empty)
$rawGeometryVideoSizeGeneration.SetValue($context, 0)
$rawGeometryIsAmbiguous.SetValue($context, $false)
$earlyDeviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$deviceFrameVideoSize.SetValue($context, [Drawing.Size]::Empty)
$lastSuppressedVideoSize.SetValue($context, [Drawing.Size]::Empty)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=3840 height0=2160 source=3840x2160 aux=0x0 encoded=3840x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=3840x2160 encoded=3840x2160")) |
    Out-Null
Assert-True ([Drawing.Size]$earlyDeviceFrameVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [bool]$pendingVideoSizeIsAmbiguous.GetValue($context)) `
    "a first raw Photos canvas is classified without becoming an iPhone candidate"
$pendingVideoSizeDueUtc.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(-1))
$directCanvasArguments = [object[]]@([long]0, $false)
$directCanvas = [Drawing.Size]$getStableVideoSize.Invoke(
    $context, $directCanvasArguments)
$directCanvasResult = Resolve-AutomaticVideoSize `
    $directCanvas ([bool]$directCanvasArguments[1])
Assert-True ($directCanvas -eq $presentationCanvas -and
    [bool]$directCanvasArguments[1] -and
    $directCanvasResult.Size -eq $provisionalPortrait -and
    -not $directCanvasResult.OrientationAuthoritative -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty) `
    "the observed Photos-first canvas uses the portrait fallback without becoming a device baseline"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_GEOMETRY width0=998 height0=2160 source=998x2160 aux=1421x0 encoded=998x2160")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_SIZE source=998x2160 encoded=998x2160")) |
    Out-Null
$latePortraitResult = Resolve-AutomaticVideoSize `
    $directCanvas ([bool]$directCanvasArguments[1])
Assert-True (-not $latePortraitResult.OrientationAuthoritative -and
    $latePortraitResult.Size -eq $portraitFrame -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
        $portraitFrame) `
    "a later early phone marker repairs the trusted baseline while the stable Photos target remains provisional"
$pendingVideoSizeDueUtc.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(-1))
$latePortraitArguments = [object[]]@([long]0, $false)
$latePortraitStable = [Drawing.Size]$getStableVideoSize.Invoke(
    $context, $latePortraitArguments)
$latePortraitStableResult = Resolve-AutomaticVideoSize `
    $latePortraitStable ([bool]$latePortraitArguments[1])
Assert-True ($latePortraitStable -eq $portraitFrame -and
    -not [bool]$latePortraitArguments[1] -and
    $latePortraitStableResult.OrientationAuthoritative -and
    $latePortraitStableResult.Size -eq $portraitFrame) `
    "the completed Photos-first replay establishes portrait as the saved device baseline"
$pendingVideoSize.SetValue($context, [Drawing.Size]::Empty)
$pendingVideoSizeDueUtc.SetValue($context, [DateTime]::MinValue)
$pendingVideoSizeSequence.SetValue($context, [long]0)
$pendingVideoSizeIsAmbiguous.SetValue($context, $false)

$readiness = $contextType.GetMethod(
    "IsCoreReadinessConfirmed", $staticFlags)
Assert-True ($null -ne $readiness) "core-readiness predicate exists"
Assert-True (-not [bool]$readiness.Invoke(
        $null, [object[]]@($false, $true, 1, 1))) `
    "native markers never replace the ready-socket baseline"
Assert-True ([bool]$readiness.Invoke(
        $null, [object[]]@($true, $true, 0, 0))) `
    "legacy core readiness remains valid without native markers"
Assert-True ([bool]$readiness.Invoke(
        $null, [object[]]@($true, $false, 1, 0))) `
    "direct DNS-SD confirmation can replace a service-status lookup"
Assert-True ([bool]$readiness.Invoke(
        $null, [object[]]@($true, $false, -1, 1))) `
    "healthy BLE discovery can back up degraded DNS-SD"
Assert-True (-not [bool]$readiness.Invoke(
        $null, [object[]]@($true, $false, -1, -1))) `
    "sockets alone do not confirm readiness without Bonjour or a healthy marker"
Assert-True (-not [bool]$readiness.Invoke(
        $null, [object[]]@($true, $true, -1, -1))) `
    "a running Bonjour service cannot override explicit failure of both discovery paths"

$connectionMarker = $contextType.GetMethod(
    "IsIncomingAirPlayConnectionRequestMarker", $staticFlags)
$pinMarker = $contextType.GetMethod(
    "IsAirPlayPinEntryMarker", $staticFlags)
$deferDisruptive = $contextType.GetMethod(
    "ShouldDeferDisruptiveMaintenance", $staticFlags)
$getIdleRenewalDelay = $contextType.GetMethod(
    "GetIdleDiscoveryRenewalDelayMinutes", $staticFlags)
$evaluateAutomaticRenewal = $contextType.GetMethod(
    "EvaluateAutomaticDiscoveryRenewal", $staticFlags)
$evaluateUnlockRefresh = $contextType.GetMethod(
    "EvaluateSessionUnlockDiscoveryRefresh", $staticFlags)
$onSessionSwitch = $contextType.GetMethod(
    "OnSessionSwitch", $instanceFlags)
$calculateFeedbackPlaceholderDue = $contextType.GetMethod(
    "CalculateFeedbackGapPlaceholderDueTicks", $staticFlags)
$shouldShowFeedbackPlaceholder = $contextType.GetMethod(
    "ShouldShowFeedbackGapPlaceholder", $staticFlags)
$handleFeedbackPlaceholderTimer = $contextType.GetMethod(
    "HandleFeedbackGapPlaceholderTimer", $instanceFlags)
$parseFeedbackRecoverySeconds = $contextType.GetMethod(
    "ParseClientFeedbackRecoverySeconds", $staticFlags)
$parseFeedbackRecoveryEpoch = $contextType.GetMethod(
    "ParseClientFeedbackRecoveryEpoch", $staticFlags)
$tryParseVideoPresentReady = $contextType.GetMethod(
    "TryParseVideoPresentReady", $staticFlags)
$tryParseVideoPresentArmed = $contextType.GetMethod(
    "TryParseVideoPresentArmed", $staticFlags)
$consumeLostRecovery = $contextType.GetMethod(
    "ConsumeDueLostConnectionRecoveryLocked", $instanceFlags)
Assert-True ($null -ne $calculateFeedbackPlaceholderDue -and
    $null -ne $shouldShowFeedbackPlaceholder -and
    $null -ne $handleFeedbackPlaceholderTimer) `
    "feedback-gap continuity exposes deterministic deadline transitions"
Assert-True ($null -ne $consumeLostRecovery) `
    "lost-client recovery exposes a focused one-shot state transition"
Assert-True ($null -ne $getIdleRenewalDelay -and
    $null -ne $evaluateAutomaticRenewal) `
    "timed discovery renewal exposes deterministic initial and recurring transitions"
Assert-True ($null -ne $evaluateUnlockRefresh) `
    "session-unlock discovery maintenance exposes a deterministic action transition"
Assert-True ($null -ne $onSessionSwitch) `
    "Windows session changes have a focused discovery-maintenance observer"
$sessionUnlockRefreshPending.SetValue($context, 0)
$sessionUnlockRefreshDue.SetValue($context, [long]0)
$sessionLockArgs = [Activator]::CreateInstance(
    [Microsoft.Win32.SessionSwitchEventArgs],
    [object[]]@([Microsoft.Win32.SessionSwitchReason]::SessionLock))
$onSessionSwitch.Invoke(
    $context,
    [object[]]@($null, $sessionLockArgs)) |
    Out-Null
Assert-True ([int]$sessionUnlockRefreshPending.GetValue($context) -eq 0 -and
    [long]$sessionUnlockRefreshDue.GetValue($context) -eq 0) `
    "locking Windows does not churn the AirPlay advertisement"
$unlockObservedBefore = [DateTime]::UtcNow
$sessionUnlockArgs = [Activator]::CreateInstance(
    [Microsoft.Win32.SessionSwitchEventArgs],
    [object[]]@([Microsoft.Win32.SessionSwitchReason]::SessionUnlock))
$onSessionSwitch.Invoke(
    $context,
    [object[]]@($null, $sessionUnlockArgs)) |
    Out-Null
$queuedUnlockDue = [long]$sessionUnlockRefreshDue.GetValue($context)
Assert-True ([int]$sessionUnlockRefreshPending.GetValue($context) -eq 1 -and
    $queuedUnlockDue -ge $unlockObservedBefore.AddSeconds(1).Ticks -and
    $queuedUnlockDue -le [DateTime]::UtcNow.AddSeconds(3).Ticks) `
    "unlock queues a short network-settle delay without restarting on the event thread"
$sessionUnlockRefreshPending.SetValue($context, 0)
$sessionUnlockRefreshDue.SetValue($context, [long]0)
function Invoke-UnlockDiscoveryDecision(
    [int]$CompletedRenewals,
    [bool]$CoreRunning,
    [bool]$ReadinessCheckIdle,
    [bool]$LocalDiscoveryReady,
    [bool]$PhysicalNetworkReady,
    [bool]$RestartBusy,
    [bool]$MirrorActive,
    [long]$ClientGraceDueTicks,
    [DateTime]$LastRefreshUtc,
    [DateTime]$NowUtc) {
    $arguments = [object[]]@(
        $CompletedRenewals,
        $CoreRunning,
        $ReadinessCheckIdle,
        $LocalDiscoveryReady,
        $PhysicalNetworkReady,
        $RestartBusy,
        $MirrorActive,
        $ClientGraceDueTicks,
        $NowUtc.Ticks,
        $LastRefreshUtc,
        $NowUtc,
        [long]0,
        [int]0)
    $action = $evaluateUnlockRefresh.Invoke($null, $arguments)
    return [pscustomobject]@{
        Action = $action.ToString()
        NextDueTicks = [long]$arguments[11]
        NextCompletedRenewals = [int]$arguments[12]
    }
}
$firstIdleDelay = [int]$getIdleRenewalDelay.Invoke(
    $null, [object[]]@(0))
$firstRecurringIdleDelay = [int]$getIdleRenewalDelay.Invoke(
    $null, [object[]]@(1))
$laterRecurringIdleDelay = [int]$getIdleRenewalDelay.Invoke(
    $null, [object[]]@(2))
$longRunningIdleDelay = [int]$getIdleRenewalDelay.Invoke(
    $null, [object[]]@(1000000))
$invalidIdleDelay = [int]$getIdleRenewalDelay.Invoke(
    $null, [object[]]@(-1))
Assert-True ($firstIdleDelay -eq 10 -and
    $firstRecurringIdleDelay -eq 20 -and
    $laterRecurringIdleDelay -eq 20 -and
    $longRunningIdleDelay -eq 20 -and
    $invalidIdleDelay -eq 0) `
    "idle discovery waits ten minutes once and then keeps a twenty-minute recurring schedule"
function Invoke-AutomaticDiscoveryDecision(
    [int]$CompletedRenewals,
    [long]$DueTicks,
    [bool]$MirrorActive,
    [long]$ClientGraceDueTicks,
    [DateTime]$LastRefreshUtc,
    [DateTime]$NowUtc) {
    $arguments = [object[]]@(
        $CompletedRenewals,
        $DueTicks,
        $MirrorActive,
        $ClientGraceDueTicks,
        $NowUtc.Ticks,
        $LastRefreshUtc,
        $NowUtc,
        [long]0,
        [int]0)
    $action = $evaluateAutomaticRenewal.Invoke($null, $arguments)
    return [pscustomobject]@{
        Action = $action.ToString()
        NextDueTicks = [long]$arguments[7]
        NextCompletedRenewals = [int]$arguments[8]
    }
}
$automaticNow = [DateTime]::UtcNow
$automaticDue = $automaticNow.AddSeconds(-1).Ticks
$automaticFirstDecision = Invoke-AutomaticDiscoveryDecision `
    0 $automaticDue $false ([long]0) ([DateTime]::MinValue) $automaticNow
Assert-True ($automaticFirstDecision.Action -eq "Refresh" -and
    $automaticFirstDecision.NextDueTicks -eq 0 -and
    $automaticFirstDecision.NextCompletedRenewals -eq 1) `
    "the first due timer advances renewal count 0 to 1"
$automaticSecondDecision = Invoke-AutomaticDiscoveryDecision `
    1 $automaticDue $false ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($automaticSecondDecision.Action -eq "Refresh" -and
    $automaticSecondDecision.NextDueTicks -eq 0 -and
    $automaticSecondDecision.NextCompletedRenewals -eq 2) `
    "the first recurring timer advances renewal count 1 to 2"
$automaticThirdDecision = Invoke-AutomaticDiscoveryDecision `
    2 $automaticDue $false ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($automaticThirdDecision.Action -eq "Refresh" -and
    $automaticThirdDecision.NextDueTicks -eq 0 -and
    $automaticThirdDecision.NextCompletedRenewals -eq 3) `
    "recurring discovery remains active after the former two-renewal limit"
$automaticSaturatedDecision = Invoke-AutomaticDiscoveryDecision `
    ([int]::MaxValue) $automaticDue $false ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($automaticSaturatedDecision.Action -eq "Refresh" -and
    $automaticSaturatedDecision.NextDueTicks -eq 0 -and
    $automaticSaturatedDecision.NextCompletedRenewals -eq [int]::MaxValue) `
    "a process-lifetime renewal counter saturates without disabling maintenance"
$automaticFutureDue = $automaticNow.AddMinutes(1).Ticks
$automaticNotDueDecision = Invoke-AutomaticDiscoveryDecision `
    2 $automaticFutureDue $false ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($automaticNotDueDecision.Action -eq "None" -and
    $automaticNotDueDecision.NextDueTicks -eq $automaticFutureDue -and
    $automaticNotDueDecision.NextCompletedRenewals -eq 2) `
    "a not-yet-due recurring stage preserves its exact deadline and count"
$automaticRecentRefresh = $automaticNow.AddMinutes(-1)
$automaticAntiChurnDecision = Invoke-AutomaticDiscoveryDecision `
    2 $automaticDue $false ([long]0) `
    $automaticRecentRefresh $automaticNow
Assert-True ($automaticAntiChurnDecision.Action -eq "None" -and
    $automaticAntiChurnDecision.NextDueTicks -eq
        $automaticNow.AddMinutes(20).Ticks -and
    $automaticAntiChurnDecision.NextCompletedRenewals -eq 2) `
    "the two-minute anti-churn guard postpones recurring maintenance without advancing its count"
$automaticActiveDecision = Invoke-AutomaticDiscoveryDecision `
    3 $automaticDue $true ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
$automaticGraceDecision = Invoke-AutomaticDiscoveryDecision `
    3 $automaticDue $false $automaticNow.AddSeconds(30).Ticks `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($automaticActiveDecision.Action -eq "None" -and
    $automaticGraceDecision.Action -eq "None" -and
    $automaticActiveDecision.NextDueTicks -eq $automaticDue -and
    $automaticGraceDecision.NextDueTicks -eq $automaticDue -and
    $automaticActiveDecision.NextCompletedRenewals -eq 3 -and
    $automaticGraceDecision.NextCompletedRenewals -eq 3) `
    "active mirroring and client grace preserve recurring maintenance for a later idle pass"
$unlockNow = [DateTime]::UtcNow
$unlockIdleRefresh = $unlockNow.AddMinutes(-11)
$refreshDecision = Invoke-UnlockDiscoveryDecision `
    1 $true $true $true $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($refreshDecision.Action -eq "Refresh" -and
    $refreshDecision.NextDueTicks -eq 0 -and
    $refreshDecision.NextCompletedRenewals -eq 2) `
    "a healthy long-idle unlock advances count 1 to 2 and requests a guarded refresh"
$unlockAfterTimedSecondDecision = Invoke-UnlockDiscoveryDecision `
    $automaticSecondDecision.NextCompletedRenewals `
    $true $true $true $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($unlockAfterTimedSecondDecision.Action -eq "Refresh" -and
    $unlockAfterTimedSecondDecision.NextCompletedRenewals -eq 3) `
    "a later unlock can refresh discovery after recurring timed maintenance"
$timedAfterUnlockDecision = Invoke-AutomaticDiscoveryDecision `
    $refreshDecision.NextCompletedRenewals $automaticDue $false ([long]0) `
    $automaticNow.AddMinutes(-21) $automaticNow
Assert-True ($timedAfterUnlockDecision.Action -eq "Refresh" -and
    $timedAfterUnlockDecision.NextCompletedRenewals -eq 3) `
    "recurring timed maintenance continues after an unlock refresh"
$recurringUnlockDecision = Invoke-UnlockDiscoveryDecision `
    2 $true $true $true $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($recurringUnlockDecision.Action -eq "Refresh" -and
    $recurringUnlockDecision.NextDueTicks -eq 0 -and
    $recurringUnlockDecision.NextCompletedRenewals -eq 3) `
    "the old two-renewal boundary no longer disables a later guarded unlock refresh"
$firstPendingDecision = Invoke-UnlockDiscoveryDecision `
    0 $true $true $true $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($firstPendingDecision.Action -eq "None" -and
    $firstPendingDecision.NextCompletedRenewals -eq 0) `
    "unlock cannot replace the normal first idle renewal"
$cooldownRefresh = $unlockNow.AddMinutes(-9)
$cooldownDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $true $true $false $false ([long]0) `
    $cooldownRefresh $unlockNow
Assert-True ($cooldownDecision.Action -eq "RetryLater" -and
    $cooldownDecision.NextDueTicks -eq
        $cooldownRefresh.AddMinutes(10).Ticks -and
    $cooldownDecision.NextCompletedRenewals -eq 3) `
    "the cooldown preserves the recurring count and reschedules at its exact deadline"
$busyDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $true $true $true $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($busyDecision.Action -eq "RetryLater" -and
    $busyDecision.NextDueTicks -eq $unlockNow.AddSeconds(5).Ticks -and
    $busyDecision.NextCompletedRenewals -eq 3) `
    "busy restart or network work receives an exact five-second retry without advancing the recurring count"
$readinessDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $false $true $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
$localDiscoveryDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $false $true $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
$physicalNetworkDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $true $false $false $false ([long]0) `
    $unlockIdleRefresh $unlockNow
Assert-True ($readinessDecision.Action -eq "RetryLater" -and
    $localDiscoveryDecision.Action -eq "RetryLater" -and
    $physicalNetworkDecision.Action -eq "RetryLater" -and
    $readinessDecision.NextDueTicks -eq $unlockNow.AddSeconds(5).Ticks -and
    $localDiscoveryDecision.NextDueTicks -eq $unlockNow.AddSeconds(5).Ticks -and
    $physicalNetworkDecision.NextDueTicks -eq
        $unlockNow.AddSeconds(5).Ticks) `
    "unlock waits five seconds for an idle readiness check, local discovery marker, and cached physical IPv4"
$activeDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $true $true $false $true ([long]0) `
    $unlockIdleRefresh $unlockNow
$graceDecision = Invoke-UnlockDiscoveryDecision `
    3 $true $true $true $true $false $false `
    $unlockNow.AddSeconds(30).Ticks $unlockIdleRefresh $unlockNow
Assert-True ($activeDecision.Action -eq "None" -and
    $graceDecision.Action -eq "None" -and
    $activeDecision.NextCompletedRenewals -eq 3 -and
    $graceDecision.NextCompletedRenewals -eq 3) `
    "unlock never interrupts mirroring or AirPlay client grace"
Assert-True ([int]$parseFeedbackRecoverySeconds.Invoke(
        $null,
        [object[]]@(
            "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=15 epoch=71")) -eq
        15 -and
    [int]$parseFeedbackRecoveryEpoch.Invoke(
        $null,
        [object[]]@(
            "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=15 epoch=71")) -eq
        71) `
    "the structured feedback recovery marker retains exact gap and epoch"
$readyArguments = [object[]]@(
    "AEROMIRROR_VIDEO_PRESENT_READY epoch=71 gap_seconds=0 proof=d3d11-present pts_delta_ms=-120",
    0, 0, 0)
Assert-True ([bool]$tryParseVideoPresentReady.Invoke(
        $null, $readyArguments) -and
    [int]$readyArguments[1] -eq 71 -and
    [int]$readyArguments[2] -eq 0 -and
    [int]$readyArguments[3] -eq -120) `
    "mirror-start presentation proof allows an exact zero-gap marker"
$malformedReadyArguments = [object[]]@(
    "prefix AEROMIRROR_VIDEO_PRESENT_READY epoch=71 gap_seconds=0 proof=d3d11-present pts_delta_ms=0",
    0, 0, 0)
$wrongProofArguments = [object[]]@(
    "AEROMIRROR_VIDEO_PRESENT_READY epoch=71 gap_seconds=0 proof=sink-buffer pts_delta_ms=0",
    0, 0, 0)
$armedArguments = [object[]]@(
    "AEROMIRROR_VIDEO_PRESENT_ARMED reason=mirror-start epoch=72", 0)
$malformedArmedArguments = [object[]]@(
    "AEROMIRROR_VIDEO_PRESENT_ARMED reason=feedback epoch=72", 0)
Assert-True (-not [bool]$tryParseVideoPresentReady.Invoke(
        $null, $malformedReadyArguments) -and
    -not [bool]$tryParseVideoPresentReady.Invoke(
        $null, $wrongProofArguments) -and
    [bool]$tryParseVideoPresentArmed.Invoke($null, $armedArguments) -and
    -not [bool]$tryParseVideoPresentArmed.Invoke(
        $null, $malformedArmedArguments)) `
    "presentation markers are whole-line strict and only D3D11 Present can authorize a fade"
Assert-True ([bool]$connectionMarker.Invoke(
        $null, [object[]]@("connection request from iPhone (iPhone14,8)"))) `
    "the anchored post-auth AirPlay request marker is recognized"
Assert-True (-not [bool]$connectionMarker.Invoke(
        $null, [object[]]@("rejecting new connection request from iPhone"))) `
    "a rejected request is not treated as successful client activity"
Assert-True ([bool]$pinMarker.Invoke(
        $null, [object[]]@('*** CLIENT MUST NOW ENTER PIN = "1234" AS AIRPLAY PASSWORD'))) `
    "the exact pre-auth PIN progress prefix is recognized"
Assert-True (-not [bool]$pinMarker.Invoke(
        $null, [object[]]@("CLIENT MUST NOW ENTER PIN"))) `
    "a PIN-marker near miss is ignored"
$graceProbeNow = [DateTime]::UtcNow.Ticks
Assert-True ([bool]$deferDisruptive.Invoke(
        $null, [object[]]@($true, [long]0, $graceProbeNow))) `
    "an active stream defers disruptive maintenance without a renderer HWND"
Assert-True ([bool]$deferDisruptive.Invoke(
        $null, [object[]]@(
            $false, [DateTime]::UtcNow.AddSeconds(30).Ticks, $graceProbeNow))) `
    "client-activity grace defers disruptive maintenance before rendering"
Assert-True (-not [bool]$deferDisruptive.Invoke(
        $null, [object[]]@($false, [long]0, $graceProbeNow))) `
    "idle maintenance is allowed when no session or client grace exists"
$feedbackDeadlineBase = [DateTime]::UtcNow.Ticks
$feedbackDeadline = [long]$calculateFeedbackPlaceholderDue.Invoke(
    $null, [object[]]@(3, [long]$feedbackDeadlineBase))
Assert-True ($feedbackDeadline -eq
        $feedbackDeadlineBase + [TimeSpan]::FromSeconds(1).Ticks) `
    "the first three-second warning deterministically schedules continuity at four seconds"
Assert-True (-not [bool]$shouldShowFeedbackPlaceholder.Invoke(
        $null,
        [object[]]@(
            [long]$feedbackDeadline,
            [long]($feedbackDeadline - 1),
            $true, $true, $true, $false))) `
    "continuity remains hidden before the local four-second deadline"
Assert-True ([bool]$shouldShowFeedbackPlaceholder.Invoke(
        $null,
        [object[]]@(
            [long]$feedbackDeadline,
            [long]$feedbackDeadline,
            $true, $true, $true, $false))) `
    "continuity becomes eligible exactly at the local deadline"
Assert-True (-not [bool]$shouldShowFeedbackPlaceholder.Invoke(
        $null,
        [object[]]@(
            [long]$feedbackDeadline,
            [long]$feedbackDeadline,
            $true, $true, $true, $true))) `
    "a fatal recovery episode owns continuity instead of the feedback timer"

$consumeSharedBudget = $contextType.GetMethod(
    "ConsumeSharedAutomaticRecoveryBudget", $instanceFlags)
Assert-True ($null -ne $consumeSharedBudget) `
    "shared automatic recovery budget exists"

$coreReadyPending.SetValue($context, $true)
$coreReadyChecks.SetValue($context, 8)
$coreReadinessAttempts.SetValue($context, 0)
$coreReadinessPid.SetValue($context, 42)
$discoveryRecoveryPending.SetValue($context, 1)
$discoveryRecoveryAttempts.SetValue($context, 0)
$discoveryRecoveryPid.SetValue($context, 42)
$discoveryRecoveryDue.SetValue(
    $context, [DateTime]::UtcNow.AddSeconds(5).Ticks)
$readinessWon = [bool]$consumeSharedBudget.Invoke(
    $context, [object[]]@($true))
Assert-True $readinessWon `
    "readiness can consume an unused shared automatic recovery budget"
Assert-True ([int]$coreReadinessAttempts.GetValue($context) -eq 1) `
    "readiness recovery marks its own shared allowance consumed"
Assert-True ([int]$discoveryRecoveryAttempts.GetValue($context) -eq 1) `
    "readiness recovery also consumes the native-discovery allowance"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "readiness recovery cancels sibling native-discovery maintenance"
Assert-True (-not [bool]$coreReadyPending.GetValue($context)) `
    "readiness recovery resolves its completed readiness check"
$discoveryAfterReadiness = [bool]$consumeSharedBudget.Invoke(
    $context, [object[]]@($false))
Assert-True (-not $discoveryAfterReadiness) `
    "native discovery cannot trigger a second restart after readiness recovery"

$coreReadyPending.SetValue($context, $true)
$coreReadyChecks.SetValue($context, 8)
$coreReadinessAttempts.SetValue($context, 0)
$coreReadinessPid.SetValue($context, 42)
$discoveryRecoveryPending.SetValue($context, 1)
$discoveryRecoveryAttempts.SetValue($context, 0)
$discoveryRecoveryPid.SetValue($context, 42)
$discoveryRecoveryDue.SetValue(
    $context, [DateTime]::UtcNow.AddSeconds(5).Ticks)
$discoveryWon = [bool]$consumeSharedBudget.Invoke(
    $context, [object[]]@($false))
Assert-True $discoveryWon `
    "native discovery can consume an unused shared automatic recovery budget"
Assert-True ([int]$coreReadinessAttempts.GetValue($context) -eq 1) `
    "native-discovery recovery also consumes the readiness allowance"
Assert-True (-not [bool]$coreReadyPending.GetValue($context)) `
    "native-discovery recovery cancels sibling readiness maintenance"
Assert-True ([int]$coreReadinessPid.GetValue($context) -eq 0) `
    "native-discovery recovery clears the sibling readiness owner"
$readinessAfterDiscovery = [bool]$consumeSharedBudget.Invoke(
    $context, [object[]]@($true))
Assert-True (-not $readinessAfterDiscovery) `
    "readiness cannot trigger a second restart after native discovery recovery"

$coreReadyPending.SetValue($context, $false)
$coreReadyChecks.SetValue($context, 0)
$coreReadinessAttempts.SetValue($context, 0)
$coreReadinessPid.SetValue($context, 0)
$discoveryRecoveryPending.SetValue($context, 0)
$discoveryRecoveryAttempts.SetValue($context, 0)
$discoveryRecoveryPid.SetValue($context, 0)
$discoveryRecoveryDue.SetValue($context, [long]0)

$observe.Invoke(
    $context,
    [object[]]@(99, "AEROMIRROR_DNSSD_READY")) | Out-Null
Assert-True ([int]$dnsSdStatus.GetValue($context) -eq 0) `
    "discovery markers from a stale core PID are ignored"

$socketsReady.SetValue($context, 1)
$observe.Invoke(
    $context,
    [object[]]@(42, "UxPlay: AEROMIRROR_DNSSD_DEGRADED")) | Out-Null
Assert-True ([int]$dnsSdStatus.GetValue($context) -eq -1) `
    "degraded DNS-SD registration is recorded"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "one degraded discovery path does not trigger recovery"

$discoveryBefore = [DateTime]::UtcNow.Ticks
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_BLE [beacon] Failed to start: radio unavailable")) |
    Out-Null
$discoveryDue = [long]$discoveryRecoveryDue.GetValue($context)
Assert-True ([int]$bleStatus.GetValue($context) -eq -1) `
    "failed BLE advertising is recorded"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 1) `
    "recovery is armed only after both discovery paths fail"
Assert-True ([int]$discoveryRecoveryPid.GetValue($context) -eq 42) `
    "discovery recovery is tied to the active core PID"
Assert-True ($discoveryDue -ge `
        $discoveryBefore + [TimeSpan]::FromSeconds(4).Ticks) `
    "discovery recovery includes a grace period"
Assert-True ($discoveryDue -le [DateTime]::UtcNow.AddSeconds(6).Ticks) `
    "discovery recovery grace is bounded"
Assert-True ([int]$socketsReady.GetValue($context) -eq 1) `
    "discovery marker failures do not invalidate ready server sockets"

$discoveryRecoveryAttempts.SetValue($context, 1)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_BLE [beacon] Advertising started: 192.0.2.10:7000")) |
    Out-Null
Assert-True ([int]$bleStatus.GetValue($context) -eq 1) `
    "successful BLE advertising is recorded"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "a healthy discovery path cancels pending recovery"
Assert-True ([int]$discoveryRecoveryAttempts.GetValue($context) -eq 0) `
    "a healthy discovery path resets the bounded recovery allowance"

$bleStatus.SetValue($context, 0)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "[beacon] Advertising started: 192.0.2.10:7000")) | Out-Null
Assert-True ([int]$bleStatus.GetValue($context) -eq 1) `
    "a continuation line from a chunked BLE marker is recognized"

$dnsSdStatus.SetValue($context, -1)
$bleStatus.SetValue($context, 0)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_BLE [beacon] Advertising failed: access denied")) |
    Out-Null
Assert-True ([int]$bleStatus.GetValue($context) -eq -1) `
    "alternate BLE advertising-failed wording is recognized"
$observe.Invoke(
    $context,
    [object[]]@(42, "UxPlay: AEROMIRROR_DNSSD_READY")) | Out-Null
Assert-True ([int]$dnsSdStatus.GetValue($context) -eq 1) `
    "successful DNS-SD registration is recorded"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "DNS-SD success cancels recovery even when BLE is unavailable"

$recoveryPending.SetValue($context, 0)
$placeholderShowPending.SetValue($context, 0)
$placeholderClosePending.SetValue($context, 0)
$rendererHandoffPending.SetValue($context, 0)
$lostStatePending.SetValue($context, 0)
$recoveredStatePending.SetValue($context, 0)
$feedbackPlaceholderDue.SetValue($context, [long]0)
$restartPending.SetValue($context, $false)
$mirrorActive.SetValue($context, 1)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "*** ERROR:   5 seconds since last client feedback request (expected every two seconds); client may be offline")) |
    Out-Null
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 0) `
    "a legacy core without recovered markers cannot open a pre-fatal placeholder that it cannot dismiss"
Assert-True ([long]$feedbackPlaceholderDue.GetValue($context) -eq 0) `
    "a legacy core without recovered markers does not arm the local continuity timer"
$feedbackEpisodeActive.SetValue($context, 0)
$feedbackEpisodeCount.SetValue($context, 0)
$feedbackLongest.SetValue($context, 0)
$observe.Invoke(
    $context,
    [object[]]@(42, "AEROMIRROR_FEEDBACK_HEALTH_READY")) | Out-Null
Assert-True ([int]$feedbackMarkersReady.GetValue($context) -eq 1) `
    "the patched native feedback-health capability is detected"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "*** ERROR:   3 seconds since last client feedback request (expected every two seconds); client may be offline")) |
    Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "a client-feedback delay warning does not arm the lost-client watchdog"
Assert-True (-not [bool]$restartPending.GetValue($context)) `
    "a client-feedback delay warning does not schedule a core restart"
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 0) `
    "a client-feedback delay warning does not show a lost-frame placeholder"
Assert-True ([int]$feedbackEpisodeActive.GetValue($context) -eq 1 -and
    [int]$feedbackEpisodeCount.GetValue($context) -eq 1 -and
    [int]$feedbackLongest.GetValue($context) -eq 3) `
    "a short feedback gap is counted without disrupting the active stream"
$scheduledFeedbackDue = [long]$feedbackPlaceholderDue.GetValue($context)
Assert-True ($scheduledFeedbackDue -gt [DateTime]::UtcNow.Ticks -and
    $scheduledFeedbackDue -le [DateTime]::UtcNow.AddSeconds(2).Ticks) `
    "the first warning arms a bounded local deadline instead of waiting for another native warning"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=3")) |
    Out-Null
Assert-True ([long]$feedbackPlaceholderDue.GetValue($context) -eq 0 -and
    [int]$placeholderShowPending.GetValue($context) -eq 0 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0) `
    "feedback recovery before four seconds cancels the queued placeholder"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "*** ERROR:   3 seconds since last client feedback request (expected every two seconds); client may be offline")) |
    Out-Null
$feedbackPlaceholderDue.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(-1).Ticks)
$handleFeedbackPlaceholderTimer.Invoke($context, @()) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0 -and
    -not [bool]$restartPending.GetValue($context)) `
    "the local four-second deadline still does not arm destructive recovery"
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 1 -and
    [int]$feedbackEpisodeCount.GetValue($context) -eq 2 -and
    [int]$feedbackLongest.GetValue($context) -eq 3 -and
    [long]$feedbackPlaceholderDue.GetValue($context) -eq 0) `
    "the local deadline queues continuity without requiring another native warning"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_PROOF_READY codec=h264 videosink=d3d11videosink")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=4 epoch=11")) |
    Out-Null
Assert-True ([int]$feedbackEpisodeActive.GetValue($context) -eq 0 -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 1 -and
    [int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$placeholderClosePending.GetValue($context) -eq 0 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0 -and
    [int]$recoveredStatePending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoPending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoEpoch.GetValue($context) -eq 11 -and
    [int]$feedbackVideoGapSeconds.GetValue($context) -eq 4 -and
    [long]$feedbackPlaceholderDue.GetValue($context) -eq 0) `
    "control recovery keeps continuity visible while it waits for matching presented video"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_ARMED reason=mirror-start epoch=99")) |
    Out-Null
Assert-True ([int]$feedbackVideoEpoch.GetValue($context) -eq 11 -and
    [int]$feedbackVideoGapSeconds.GetValue($context) -eq 4 -and
    [int]$feedbackMirrorStartArmExpected.GetValue($context) -eq 0) `
    "an ordinary feedback recovery rejects an unexpected mirror-start challenge"

$handleFeedbackVideoWaitTimer = $contextType.GetMethod(
    "HandleFeedbackVideoRecoveryWaitTimer", $instanceFlags)
$feedbackVideoWaitDue.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(-1).Ticks)
$handleFeedbackVideoWaitTimer.Invoke($context, @()) | Out-Null
Assert-True ([int]$reconnectHintPending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoPending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoHintCount.GetValue($context) -eq 1 -and
    [int]$recoveryPending.GetValue($context) -eq 0 -and
    -not [bool]$restartPending.GetValue($context)) `
    "a three-second presentation timeout shows guidance without resetting sockets or the core"

$observe.Invoke(
    $context,
    [object[]]@(41,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=11 gap_seconds=4 proof=d3d11-present pts_delta_ms=0")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=11 gap_seconds=0 proof=d3d11-present pts_delta_ms=0")) |
    Out-Null
Assert-True ([int]$rendererHandoffPending.GetValue($context) -eq 0 -and
    [int]$feedbackHandoffPending.GetValue($context) -eq 0 -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 1) `
    "wrong-PID and wrong-gap Present markers cannot dismiss continuity"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=11 gap_seconds=4 proof=d3d11-present pts_delta_ms=-35")) |
    Out-Null
$fadeToken = [long]$feedbackHandoffToken.GetValue($context)
$fadeSession = [int]$feedbackVideoSession.GetValue($context)
$isFeedbackHandoffCurrent = $contextType.GetMethod(
    "IsFeedbackRendererHandoffCurrent", $instanceFlags)
Assert-True ($null -ne $isFeedbackHandoffCurrent -and
    [bool]$isFeedbackHandoffCurrent.Invoke(
        $context,
        [object[]]@($fadeToken, 42, $fadeSession, 11))) `
    "matching D3D11 Present queues a PID/session/epoch-correlated fade"
Assert-True ([int]$feedbackPlaceholderActive.GetValue($context) -eq 1 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 1 -and
    [int]$feedbackHandoffPending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoCompleted.GetValue($context) -eq 0) `
    "continuity stays active until the fade completion callback revalidates its token"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "*** ERROR:   3 seconds since last client feedback request (expected every two seconds); client may be offline")) |
    Out-Null
Assert-True ([long]$continuityToken.GetValue($context) -ne $fadeToken -and
    [int]$feedbackHandoffPending.GetValue($context) -eq 0 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0 -and
    -not [bool]$isFeedbackHandoffCurrent.Invoke(
        $context,
        [object[]]@($fadeToken, 42, $fadeSession, 11))) `
    "a new loss during the 180 ms fade invalidates the old completion token"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=3 epoch=12")) |
    Out-Null
$preReselectSession = [int]$mirrorSessionGeneration.GetValue($context)
$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror starting mirroring")) | Out-Null
$reselectSession = [int]$mirrorSessionGeneration.GetValue($context)
Assert-True ($reselectSession -eq $preReselectSession + 1 -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 1 -and
    [int]$feedbackVideoPending.GetValue($context) -eq 1 -and
    [int]$feedbackVideoEpoch.GetValue($context) -eq 0 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0) `
    "manual reselection keeps continuity visible and never trusts mirror-start or HWND alone"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=12 gap_seconds=3 proof=d3d11-present pts_delta_ms=0")) |
    Out-Null
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_ARMED reason=mirror-start epoch=13")) |
    Out-Null
Assert-True ([int]$feedbackVideoEpoch.GetValue($context) -eq 13 -and
    [int]$feedbackVideoGapSeconds.GetValue($context) -eq 0 -and
    [int]$feedbackVideoSession.GetValue($context) -eq $reselectSession -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0) `
    "the latest valid mirror-start challenge supersedes the stale feedback epoch"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=13 gap_seconds=3 proof=d3d11-present pts_delta_ms=0")) |
    Out-Null
Assert-True ([int]$rendererHandoffPending.GetValue($context) -eq 0) `
    "a mirror-start challenge rejects a forged positive-gap Present marker"
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_VIDEO_PRESENT_READY epoch=13 gap_seconds=0 proof=d3d11-present pts_delta_ms=12")) |
    Out-Null
Assert-True ([int]$rendererHandoffPending.GetValue($context) -eq 1 -and
    [int]$feedbackHandoffPending.GetValue($context) -eq 1 -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 1) `
    "manual reselection fades only after its exact new-session presentation proof"
$completeFeedbackHandoff = $contextType.GetMethod(
    "TryCompleteFeedbackRendererHandoff", $instanceFlags)
$completedToken = [long]$feedbackHandoffToken.GetValue($context)
$completedBefore = [int]$feedbackVideoCompleted.GetValue($context)
$rendererHandoffPending.SetValue($context, 0)
$completedOnce = [bool]$completeFeedbackHandoff.Invoke(
    $context,
    [object[]]@($completedToken, 42, $reselectSession, 13))
$completedTwice = [bool]$completeFeedbackHandoff.Invoke(
    $context,
    [object[]]@($completedToken, 42, $reselectSession, 13))
Assert-True ($completedOnce -and -not $completedTwice -and
    [int]$feedbackPlaceholderActive.GetValue($context) -eq 0 -and
    [int]$feedbackHandoffPending.GetValue($context) -eq 0 -and
    [int]$feedbackVideoPending.GetValue($context) -eq 0 -and
    [int]$feedbackVideoCompleted.GetValue($context) -eq
        $completedBefore + 1) `
    "fade completion revalidates and consumes its token exactly once"
$placeholderShowPending.SetValue($context, 0)

$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror->running is no longer true")) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "a clean mirror shutdown does not arm abnormal-loss recovery"
Assert-True (-not [bool]$restartPending.GetValue($context)) `
    "a clean mirror shutdown does not schedule a receiver restart"
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 0) `
    "a clean mirror shutdown does not show a lost-frame placeholder"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=initial port=53999")) | Out-Null
$mirrorActive.SetValue($context, 1)

$before = [DateTime]::UtcNow.Ticks
$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror error in recv: 10054")) | Out-Null
$armedDue = [long]$recoveryDue.GetValue($context)
Assert-True ([int]$recoveryPending.GetValue($context) -eq 1) `
    "fatal mirror recv error arms recovery"
Assert-True ([int]$socketsReady.GetValue($context) -eq 0 -and
    [int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$httpResetPort.GetValue($context) -eq 0) `
    "fatal loss clears listener readiness until the native reset is explicitly confirmed"
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0 -and
    [int]$lostStatePending.GetValue($context) -eq 1 -and
    [int]$recoveredStatePending.GetValue($context) -eq 0 -and
    [long]$feedbackPlaceholderDue.GetValue($context) -eq 0) `
    "fatal mirror recv error queues continuity and cancels any smooth handoff"
Assert-True ($armedDue -ge $before + [TimeSpan]::FromSeconds(2).Ticks) `
    "recovery grace is not shorter than two seconds"
Assert-True ($armedDue -le [DateTime]::UtcNow.AddSeconds(4).Ticks) `
    "recovery grace is bounded"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=6")) |
    Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 1 -and
    [int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 0) `
    "a late recovered marker cannot dismiss an already-fatal loss episode"

$observe.Invoke(
    $context,
    [object[]]@(42, "***ERROR lost connection with client")) | Out-Null
Assert-True ([long]$recoveryDue.GetValue($context) -eq $armedDue) `
    "repeated fatal markers do not postpone recovery indefinitely"

$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror->running is no longer true")) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 1) `
    "quick native cleanup preserves abnormal-loss recovery"
Assert-True ([int]$recoveryPid.GetValue($context) -eq 42) `
    "quick native cleanup preserves the abnormal-loss owner"
Assert-True ([long]$recoveryDue.GetValue($context) -eq $armedDue) `
    "quick native cleanup preserves the bounded recovery deadline"
Assert-True ([int]$mirrorActive.GetValue($context) -eq 0) `
    "abnormal mirror cleanup clears the active-session flag"
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 0) `
    "abnormal mirror cleanup does not use normal post-session maintenance"
Assert-True (-not [bool]$restartPending.GetValue($context)) `
    "abnormal mirror cleanup waits for its bounded recovery deadline"
$idleDue = [long]$idleRenewalDue.GetValue($context)
Assert-True ($idleDue -ge [DateTime]::UtcNow.AddMinutes(9).Ticks) `
    "abnormal mirror cleanup preserves the idle-discovery sequence"
Assert-True ($idleDue -le [DateTime]::UtcNow.AddMinutes(11).Ticks) `
    "the first idle-discovery stage remains bounded near ten minutes"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=reset port=53999")) | Out-Null
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 1 -and
    [int]$httpResetPort.GetValue($context) -eq 53999 -and
    [int]$socketsReady.GetValue($context) -eq 1) `
    "matching native reset readiness is retained through abnormal mirror cleanup"
$observe.Invoke(
    $context,
    [object[]]@(42, "connection request from reconnecting iPhone")) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "a reconnect request cancels abnormal-loss discovery renewal"
Assert-True ([int]$recoveryPid.GetValue($context) -eq 0) `
    "a reconnect request clears the abnormal-loss owner"
Assert-True ([long]$recoveryDue.GetValue($context) -eq 0) `
    "a reconnect request clears the abnormal-loss deadline"
Assert-True ([int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$httpResetPort.GetValue($context) -eq 0) `
    "a reconnect request clears one-shot HTTP reset evidence"
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$placeholderClosePending.GetValue($context) -eq 0) `
    "a reconnect handshake keeps the placeholder until mirroring really starts"

$clientGraceDue.SetValue($context, [long]0)
$mirrorActive.SetValue($context, 0)
$httpMarkersReady.SetValue($context, 1)
$httpPort.SetValue($context, 53999)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$missingConfirmationAction = $consumeLostRecovery.Invoke(
    $context, [object[]]@([DateTime]::UtcNow, $true, $false)).ToString()
Assert-True ($missingConfirmationAction -eq "RestartStalledSession") `
    "a marker-capable core cannot preserve same-process recovery without matching reset readiness"

$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$observe.Invoke(
    $context,
    [object[]]@(42,
        "AEROMIRROR_HTTP_READY stage=reset port=53999")) | Out-Null
$preserveAction = $consumeLostRecovery.Invoke(
    $context, [object[]]@([DateTime]::UtcNow, $true, $false)).ToString()
Assert-True ($preserveAction -eq "PreserveNativeRecovery") `
    "an ended abnormal session preserves only an explicitly confirmed same-port native reset"
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0 -and
    [int]$recoveryPid.GetValue($context) -eq 0 -and
    [long]$recoveryDue.GetValue($context) -eq 0 -and
    [int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$httpResetPort.GetValue($context) -eq 0) `
    "consuming discovery renewal clears all one-shot recovery state"
$secondRenewAction = $consumeLostRecovery.Invoke(
    $context, [object[]]@([DateTime]::UtcNow, $true, $false)).ToString()
Assert-True ($secondRenewAction -eq "None") `
    "the same abnormal loss cannot renew discovery twice"

$httpMarkersReady.SetValue($context, 0)
$httpPort.SetValue($context, 0)
$httpResetStatus.SetValue($context, 0)
$httpResetPort.SetValue($context, 0)
$socketsReady.SetValue($context, 0)
$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$observeSocketReady.Invoke($context, [object[]]@(42)) | Out-Null
$legacyPreserveAction = $consumeLostRecovery.Invoke(
    $context, [object[]]@([DateTime]::UtcNow, $true, $false)).ToString()
Assert-True ($legacyPreserveAction -eq "PreserveLegacyRecovery") `
    "a legacy core keeps a bounded generic listener fallback without claiming port identity"

$mirrorActive.SetValue($context, 1)
$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$stalledAction = $consumeLostRecovery.Invoke(
    $context, [object[]]@([DateTime]::UtcNow, $true, $false)).ToString()
Assert-True ($stalledAction -eq "RestartStalledSession") `
    "an active session at the deadline keeps stalled-session recovery"
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "stalled-session recovery is also consumed exactly once"
$mirrorActive.SetValue($context, 0)

$settingsRestartDeferred.SetValue($context, 1)
$sessionEndedPending.SetValue($context, 1)
$rawAcceptDue = [DateTime]::UtcNow.AddMilliseconds(100).Ticks
$rawAcceptIdleDue = [DateTime]::UtcNow.AddMilliseconds(100).Ticks
$sessionEndedDue.SetValue($context, $rawAcceptDue)
$idleRenewalDue.SetValue($context, $rawAcceptIdleDue)
$idleRenewalUsed.SetValue($context, 1)
$discoveryRecoveryPending.SetValue($context, 1)
$discoveryRecoveryAttempts.SetValue($context, 1)
$discoveryRecoveryPid.SetValue($context, 42)
$discoveryRecoveryDue.SetValue($context, $rawAcceptDue)
$coreReadyPending.SetValue($context, $true)
$coreReadyChecks.SetValue($context, 8)
$coreReadinessAttempts.SetValue($context, 1)
$coreReadinessPid.SetValue($context, 42)
$clientReadyPending.SetValue($context, 0)
$clientGraceDue.SetValue($context, [long]0)
$observe.Invoke(
    $context,
    [object[]]@(42, "Accepted IPv4 client on socket 12, port 7000")) |
    Out-Null
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 1) `
    "a low-level accepted socket is not treated as an AirPlay request"
Assert-True ([long]$sessionEndedDue.GetValue($context) -eq $rawAcceptDue) `
    "a low-level accepted socket does not alter deferred maintenance"
Assert-True ([long]$idleRenewalDue.GetValue($context) -eq $rawAcceptIdleDue) `
    "a low-level accepted socket does not postpone idle maintenance"
Assert-True ([int]$idleRenewalUsed.GetValue($context) -eq 1) `
    "a low-level accepted socket does not renew the idle sequence allowance"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 1) `
    "a low-level accepted socket does not cancel discovery recovery"
Assert-True ([bool]$coreReadyPending.GetValue($context)) `
    "a low-level accepted socket does not bypass readiness confirmation"
Assert-True ([long]$clientGraceDue.GetValue($context) -eq 0) `
    "a low-level accepted socket does not start client-activity grace"
Assert-True ([int]$settingsRestartDeferred.GetValue($context) -eq 1) `
    "a low-level accepted socket does not discard deferred settings"

$observe.Invoke(
    $context,
    [object[]]@(42,
        "rejecting new connection request from unsupported client")) |
    Out-Null
Assert-True ([long]$sessionEndedDue.GetValue($context) -eq $rawAcceptDue) `
    "a rejected request does not postpone deferred maintenance"
Assert-True ([bool]$coreReadyPending.GetValue($context)) `
    "a rejected request does not bypass readiness confirmation"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 1) `
    "a rejected request does not cancel discovery recovery"

$mirrorActive.SetValue($context, 1)
$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$pinBefore = [DateTime]::UtcNow
$observe.Invoke(
    $context,
    [object[]]@(42,
        '*** CLIENT MUST NOW ENTER PIN = "1234" AS AIRPLAY PASSWORD')) |
    Out-Null
Assert-True ([bool]$coreReadyPending.GetValue($context) -eq $false) `
    "PIN-entry progress resolves readiness without waiting for DNS-SD checks"
Assert-True ([int]$coreReadinessAttempts.GetValue($context) -eq 0) `
    "PIN-entry progress cancels readiness recovery"
Assert-True ([int]$coreReadinessPid.GetValue($context) -eq 0) `
    "PIN-entry progress clears the readiness recovery owner"
Assert-True ([int]$clientReadyPending.GetValue($context) -eq 1) `
    "PIN-entry progress queues the ready UI state for the monitor thread"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "PIN-entry progress cancels obsolete discovery recovery"
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "PIN-entry progress cancels the previous session's lost-client watchdog"
$pinGraceDue = [long]$clientGraceDue.GetValue($context)
Assert-True ($pinGraceDue -ge $pinBefore.AddSeconds(59).Ticks) `
    "PIN entry receives a usable authentication grace period"
Assert-True ($pinGraceDue -le [DateTime]::UtcNow.AddSeconds(61).Ticks) `
    "PIN authentication grace remains bounded"
Assert-True ([int]$mirrorActive.GetValue($context) -eq 1) `
    "PIN-entry progress does not manufacture a new mirroring start"
$observe.Invoke(
    $context,
    [object[]]@(42, "***ERROR lost connection with client")) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "a late old-session fatal marker cannot re-arm during PIN grace"
Assert-True (-not [bool]$restartPending.GetValue($context)) `
    "a late old-session fatal marker cannot schedule a handshake restart"

$mirrorActive.SetValue($context, 0)
$sessionEndedPending.SetValue($context, 1)
$sessionEndedDue.SetValue($context, [DateTime]::UtcNow.AddMilliseconds(100).Ticks)
$idleRenewalUsed.SetValue($context, 1)
$discoveryRecoveryPending.SetValue($context, 1)
$discoveryRecoveryAttempts.SetValue($context, 1)
$discoveryRecoveryPid.SetValue($context, 42)
$discoveryRecoveryDue.SetValue(
    $context, [DateTime]::UtcNow.AddMilliseconds(100).Ticks)
$coreReadyPending.SetValue($context, $true)
$coreReadyChecks.SetValue($context, 8)
$coreReadinessAttempts.SetValue($context, 1)
$coreReadinessPid.SetValue($context, 42)
$clientReadyPending.SetValue($context, 0)
$requestBefore = [DateTime]::UtcNow
$observe.Invoke(
    $context,
    [object[]]@(42, "connection request from iPhone (iPhone14,8)")) |
    Out-Null
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 1) `
    "an AirPlay request keeps deferred settings maintenance pending"
$postponedDue = [long]$sessionEndedDue.GetValue($context)
Assert-True ($postponedDue -ge $requestBefore.AddSeconds(29).Ticks) `
    "an AirPlay request grants deferred settings a new grace period"
Assert-True ($postponedDue -le [DateTime]::UtcNow.AddSeconds(31).Ticks) `
    "the deferred settings grace period remains bounded"
$requestIdleDue = [long]$idleRenewalDue.GetValue($context)
Assert-True ($requestIdleDue -ge $requestBefore.AddMinutes(9).Ticks) `
    "an AirPlay request moves idle maintenance away from the handshake"
Assert-True ($requestIdleDue -le [DateTime]::UtcNow.AddMinutes(11).Ticks) `
    "the re-armed idle sequence starts near ten minutes"
Assert-True ([int]$idleRenewalUsed.GetValue($context) -eq 0) `
    "an AirPlay request re-arms the bounded idle sequence"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "an AirPlay request cancels obsolete discovery recovery"
Assert-True ([int]$discoveryRecoveryPid.GetValue($context) -eq 0) `
    "an AirPlay request clears the discovery recovery owner"
Assert-True ([long]$discoveryRecoveryDue.GetValue($context) -eq 0) `
    "an AirPlay request clears the discovery recovery deadline"
Assert-True ([int]$discoveryRecoveryAttempts.GetValue($context) -eq 0) `
    "an AirPlay request restores the bounded discovery recovery allowance"
Assert-True (-not [bool]$coreReadyPending.GetValue($context)) `
    "an AirPlay request cancels pending readiness recovery"
Assert-True ([int]$coreReadyChecks.GetValue($context) -eq 0) `
    "an AirPlay request clears stale readiness checks"
Assert-True ([int]$coreReadinessAttempts.GetValue($context) -eq 0) `
    "an AirPlay request restores the readiness recovery allowance"
Assert-True ([int]$coreReadinessPid.GetValue($context) -eq 0) `
    "an AirPlay request clears the readiness recovery owner"
Assert-True ([int]$clientReadyPending.GetValue($context) -eq 1) `
    "an AirPlay request queues ready UI state on the monitor thread"
$requestGraceDue = [long]$clientGraceDue.GetValue($context)
Assert-True ($requestGraceDue -ge $requestBefore.AddSeconds(29).Ticks) `
    "post-auth client activity receives a connection grace period"
Assert-True ($requestGraceDue -le [DateTime]::UtcNow.AddSeconds(31).Ticks) `
    "post-auth client grace remains bounded"

$applySettingsRestart = $contextType.GetMethod(
    "ApplyOrDeferSettingsRestart", $instanceFlags)
$settingsRestartDeferred.SetValue($context, 0)
$sessionEndedPending.SetValue($context, 0)
$sessionEndedDue.SetValue($context, [long]0)
$applySettingsRestart.Invoke($context, [object[]]@()) | Out-Null
Assert-True ([int]$settingsRestartDeferred.GetValue($context) -eq 1) `
    "settings restart is deferred during client-activity grace"
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 1) `
    "deferred settings remain scheduled after handshake grace"
Assert-True ([long]$sessionEndedDue.GetValue($context) -ge $requestGraceDue) `
    "settings maintenance cannot interrupt the current handshake"

$recoveryPending.SetValue($context, 1)
$recoveryPid.SetValue($context, 42)
$recoveryDue.SetValue($context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$discoveryRecoveryPending.SetValue($context, 1)
$discoveryRecoveryAttempts.SetValue($context, 1)
$discoveryRecoveryPid.SetValue($context, 42)
$discoveryRecoveryDue.SetValue(
    $context, [DateTime]::UtcNow.AddSeconds(-1).Ticks)
$coreReadyPending.SetValue($context, $true)
$coreReadyChecks.SetValue($context, 8)
$coreReadinessAttempts.SetValue($context, 1)
$coreReadinessPid.SetValue($context, 42)
$clientReadyPending.SetValue($context, 0)
$physicalNetworkRestartDeferred.SetValue($context, 1)
$videoGeometryEventSequence.SetValue($context, [long]19)
$pendingVideoSize.SetValue($context, $presentationCanvas)
$pendingVideoSizeDueUtc.SetValue(
    $context, [DateTime]::UtcNow.AddSeconds(1))
$pendingVideoSizeSequence.SetValue($context, [long]18)
$pendingVideoSizeIsAmbiguous.SetValue($context, $true)
$currentVideoSize.SetValue($context, $portraitFrame)
$currentVideoSizeSequence.SetValue($context, [long]17)
$currentVideoSizeIsAmbiguous.SetValue($context, $true)
$rawGeometryVideoSize.SetValue($context, $presentationCanvas)
$rawGeometryVideoSizeGeneration.SetValue($context, 7)
$rawGeometryIsAmbiguous.SetValue($context, $true)
$earlyDeviceFrameVideoSize.SetValue($context, $portraitFrame)
$deviceFrameVideoSize.SetValue($context, $landscapeFrame)
$lastSuppressedVideoSize.SetValue($context, $presentationCanvas)
$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror starting mirroring")) | Out-Null
Assert-True ([int]$placeholderShowPending.GetValue($context) -eq 1 -and
    [int]$placeholderClosePending.GetValue($context) -eq 0 -and
    [int]$rendererHandoffPending.GetValue($context) -eq 1) `
    "a new mirroring start keeps continuity until the renderer actually exists"
Assert-True ([Drawing.Size]$earlyDeviceFrameVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [Drawing.Size]$lastSuppressedVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [long]$videoGeometryEventSequence.GetValue($context) -eq 19 -and
    [Drawing.Size]$pendingVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    [long]$pendingVideoSizeSequence.GetValue($context) -eq 0 -and
    [Drawing.Size]$currentVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    [long]$currentVideoSizeSequence.GetValue($context) -eq 0 -and
    -not [bool]$pendingVideoSizeIsAmbiguous.GetValue($context) -and
    -not [bool]$currentVideoSizeIsAmbiguous.GetValue($context) -and
    [Drawing.Size]$rawGeometryVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    -not [bool]$rawGeometryIsAmbiguous.GetValue($context)) `
    "a new mirroring session clears candidates and baselines without rewinding the core-lifetime geometry sequence"
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 0) `
    "actual mirroring start clears pending post-session maintenance"
Assert-True ([long]$sessionEndedDue.GetValue($context) -eq 0) `
    "actual mirroring start clears the post-session deadline"
Assert-True ([int]$settingsRestartDeferred.GetValue($context) -eq 1) `
    "actual mirroring start preserves the deferred settings change"
Assert-True ([int]$recoveryPending.GetValue($context) -eq 0) `
    "actual mirroring start atomically cancels the old lost-client watchdog"
Assert-True ([int]$recoveryPid.GetValue($context) -eq 0) `
    "actual mirroring start clears the old lost-client recovery owner"
Assert-True ([long]$recoveryDue.GetValue($context) -eq 0) `
    "actual mirroring start clears the old lost-client recovery deadline"
Assert-True ([int]$discoveryRecoveryPending.GetValue($context) -eq 0) `
    "actual mirroring start atomically cancels discovery recovery"
Assert-True ([int]$discoveryRecoveryPid.GetValue($context) -eq 0) `
    "actual mirroring start clears the discovery recovery owner"
Assert-True ([long]$discoveryRecoveryDue.GetValue($context) -eq 0) `
    "actual mirroring start clears the discovery recovery deadline"
Assert-True ([int]$discoveryRecoveryAttempts.GetValue($context) -eq 0) `
    "actual mirroring start restores the discovery recovery allowance"
Assert-True (-not [bool]$coreReadyPending.GetValue($context)) `
    "actual mirroring start cancels readiness recovery"
Assert-True ([int]$coreReadinessPid.GetValue($context) -eq 0) `
    "actual mirroring start clears the readiness owner"
Assert-True ([int]$clientReadyPending.GetValue($context) -eq 1) `
    "actual mirroring start queues the ready UI state"
Assert-True ([long]$clientGraceDue.GetValue($context) -eq 0) `
    "actual mirroring start replaces handshake grace with active-session state"
Assert-True ([int]$physicalNetworkRestartDeferred.GetValue($context) -eq 1) `
    "a deferred physical-network restart remains queued during mirroring"

$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror->running is no longer true")) |
    Out-Null
Assert-True ([int]$mirrorActive.GetValue($context) -eq 0) `
    "session end releases the active-stream maintenance guard"
Assert-True ([int]$physicalNetworkRestartDeferred.GetValue($context) -eq 1) `
    "session end leaves one physical-network restart for the monitor"
Assert-True (-not [bool]$deferDisruptive.Invoke(
        $null, [object[]]@($false, [long]0, [DateTime]::UtcNow.Ticks))) `
    "deferred network maintenance becomes eligible after session end"
$physicalNetworkRestartDeferred.SetValue($context, 0)
$sessionEndedPending.SetValue($context, 0)
$sessionEndedDue.SetValue($context, [long]0)
$settingsRestartDeferred.SetValue($context, 0)

$mirrorActive.SetValue($context, 1)
$settingsRestartDeferred.SetValue($context, 1)
$physicalNetworkRestartDeferred.SetValue($context, 1)
$clientGraceDue.SetValue($context, [long]0)
$staleEndRequestBefore = [DateTime]::UtcNow
$observe.Invoke(
    $context,
    [object[]]@(42, "connection request from reconnecting iPhone")) |
    Out-Null
$staleEndRequestGrace = [long]$clientGraceDue.GetValue($context)
Assert-True ($staleEndRequestGrace -ge
    $staleEndRequestBefore.AddSeconds(29).Ticks) `
    "a reconnect request establishes grace before old-session cleanup"
$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror->running is no longer true")) |
    Out-Null
Assert-True ([int]$mirrorActive.GetValue($context) -eq 0) `
    "the stale end marker can close the old active-session state"
Assert-True ([long]$clientGraceDue.GetValue($context) -ge
    $staleEndRequestGrace) `
    "a stale end marker preserves the newer reconnect grace"
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 1) `
    "deferred settings remain pending after stale old-session cleanup"
Assert-True ([long]$sessionEndedDue.GetValue($context) -ge
    $staleEndRequestGrace) `
    "deferred settings cannot interrupt the newer reconnect handshake"
Assert-True ([int]$physicalNetworkRestartDeferred.GetValue($context) -eq 1) `
    "deferred network maintenance remains guarded by reconnect grace"
Assert-True ([bool]$deferDisruptive.Invoke(
        $null,
        [object[]]@(
            $false,
            [long]$clientGraceDue.GetValue($context),
            [DateTime]::UtcNow.Ticks))) `
    "automatic maintenance remains blocked between stale end and new start"
$observe.Invoke(
    $context,
    [object[]]@(42, "raop_rtp_mirror starting mirroring")) | Out-Null
Assert-True ([int]$mirrorActive.GetValue($context) -eq 1) `
    "the reconnecting stream starts after stale old-session cleanup"
Assert-True ([int]$sessionEndedPending.GetValue($context) -eq 0) `
    "new mirroring cancels stale post-session maintenance"
Assert-True ([long]$clientGraceDue.GetValue($context) -eq 0) `
    "new mirroring replaces reconnect grace with active-session state"
Assert-True ([int]$settingsRestartDeferred.GetValue($context) -eq 1) `
    "new mirroring preserves the user's deferred settings change"
Assert-True ([int]$physicalNetworkRestartDeferred.GetValue($context) -eq 1) `
    "new mirroring keeps deferred network maintenance queued"
$physicalNetworkRestartDeferred.SetValue($context, 0)
$sessionEndedPending.SetValue($context, 0)
$sessionEndedDue.SetValue($context, [long]0)
$settingsRestartDeferred.SetValue($context, 0)

$mirrorActive.SetValue($context, 1)
$observe.Invoke(
    $context,
    [object[]]@(42, "***ERROR lost connection with client")) | Out-Null
Assert-True ([int]$recoveryPending.GetValue($context) -eq 1) `
    "lost-client marker arms recovery"

$networkChanged = $contextType.GetMethod(
    "OnNetworkAddressChanged", $instanceFlags)
Assert-True ($null -ne $networkChanged) "network-change debounce exists"
$networkRefreshPending.SetValue($context, 0)
$networkRefreshDue.SetValue($context, [long]0)
$networkChanged.Invoke(
    $context, [object[]]@($null, [EventArgs]::Empty)) | Out-Null
$firstNetworkDue = [long]$networkRefreshDue.GetValue($context)
Assert-True ([int]$networkRefreshPending.GetValue($context) -eq 1) `
    "network change schedules a profile refresh"
Start-Sleep -Milliseconds 25
$networkChanged.Invoke(
    $context, [object[]]@($null, [EventArgs]::Empty)) | Out-Null
$secondNetworkDue = [long]$networkRefreshDue.GetValue($context)
Assert-True ($secondNetworkDue -eq $firstNetworkDue) `
    "an event storm does not keep postponing profile detection"

$waitingProperty = $contextType.GetProperty(
    "IsWaitingForNetwork", $instanceFlags)
$startAfterNetwork.SetValue($context, $false)
$refreshAfterNetwork.SetValue($context, 1)
Assert-True ([bool]$waitingProperty.GetValue($context, $null)) `
    "manual discovery refresh exposes the waiting-for-network state"
$refreshAfterNetwork.SetValue($context, 0)
$startAfterNetwork.SetValue($context, $true)
Assert-True ([bool]$waitingProperty.GetValue($context, $null)) `
    "startup exposes the waiting-for-network state even when PIN is enabled"

$resetCoreSession = $contextType.GetMethod(
    "ResetCoreSessionTracking", $instanceFlags)
Assert-True ($null -ne $resetCoreSession) `
    "core shutdown exposes a complete session-state reset"
$videoGeometryEventSequence.SetValue($context, [long]23)
$pendingVideoSize.SetValue($context, $presentationCanvas)
$pendingVideoSizeSequence.SetValue($context, [long]22)
$pendingVideoSizeIsAmbiguous.SetValue($context, $true)
$currentVideoSize.SetValue($context, $portraitFrame)
$currentVideoSizeSequence.SetValue($context, [long]21)
$currentVideoSizeIsAmbiguous.SetValue($context, $true)
$rawGeometryVideoSize.SetValue($context, $presentationCanvas)
$rawGeometryVideoSizeGeneration.SetValue($context, 7)
$rawGeometryIsAmbiguous.SetValue($context, $true)
$earlyDeviceFrameVideoSize.SetValue($context, $portraitFrame)
$deviceFrameVideoSize.SetValue($context, $landscapeFrame)
$lastSuppressedVideoSize.SetValue($context, $presentationCanvas)
$exactVideoSizeFitSequence.SetValue($context, [long]20)
$appliedVideoFitSize.SetValue($context, $landscapeFrame)
$appliedVideoFitTargetKind.SetValue($context, $deviceFrameFitTarget)
$httpMarkersReady.SetValue($context, 1)
$httpPort.SetValue($context, 53999)
$httpResetStatus.SetValue($context, 1)
$httpResetPort.SetValue($context, 53999)
$resetCoreSession.Invoke($context, [object[]]@($false)) | Out-Null
Assert-True ([Drawing.Size]$earlyDeviceFrameVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [Drawing.Size]$deviceFrameVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [Drawing.Size]$lastSuppressedVideoSize.GetValue($context) -eq
    [Drawing.Size]::Empty -and
    [long]$videoGeometryEventSequence.GetValue($context) -eq 0 -and
    [long]$pendingVideoSizeSequence.GetValue($context) -eq 0 -and
    [long]$currentVideoSizeSequence.GetValue($context) -eq 0 -and
    -not [bool]$pendingVideoSizeIsAmbiguous.GetValue($context) -and
    -not [bool]$currentVideoSizeIsAmbiguous.GetValue($context) -and
    [Drawing.Size]$rawGeometryVideoSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    -not [bool]$rawGeometryIsAmbiguous.GetValue($context) -and
    [long]$exactVideoSizeFitSequence.GetValue($context) -eq -1 -and
    [Drawing.Size]$appliedVideoFitSize.GetValue($context) -eq
        [Drawing.Size]::Empty -and
    [string]$appliedVideoFitTargetKind.GetValue($context) -eq "None" -and
    [int]$httpMarkersReady.GetValue($context) -eq 0 -and
    [int]$httpPort.GetValue($context) -eq 0 -and
    [int]$httpResetStatus.GetValue($context) -eq 0 -and
    [int]$httpResetPort.GetValue($context) -eq 0) `
    "core reset clears geometry sequences, learned baselines, fit targets, and native HTTP lifecycle state"

$logDrainSucceeded = [bool]$flushLog.Invoke(
    $null, [object[]]@(5000))
Assert-True $logDrainSucceeded `
    "the isolated logger drains before the test inspects or removes its root"
$testLogPath = [string]$expectedStoragePaths.LogPath
Assert-True (Test-Path -LiteralPath $testLogPath -PathType Leaf) `
    "the resilience suite writes receiver.log only inside its GUID root"
$isolatedLog = [IO.File]::ReadAllText($testLogPath)
Assert-True ($isolatedLog.Contains($testLogMarker)) `
    "the isolated receiver log contains this run's unique marker"
Assert-True ($isolatedLog.Contains(
        "Rejected native reset HTTP-ready marker: port 54000 does not match advertised port 53999.")) `
    "fake core HTTP diagnostics are captured by the isolated receiver log"
$testCompleted = $true
}
finally {
    $cleanupDrainSucceeded = $false
    try {
        $cleanupDrainSucceeded = [bool]$flushLog.Invoke(
            $null, [object[]]@(5000))
    }
    catch {
        if ($testCompleted) {
            throw
        }
    }

    if ($testCompleted) {
        Assert-True $cleanupDrainSucceeded `
            "the isolated logger remains drained through safe cleanup"
        if (Test-Path -LiteralPath $testStorageRoot) {
            Remove-Item -LiteralPath $testStorageRoot -Recurse -Force
        }
    }
    elseif ($testStorageRootSetupStarted -and
        (Test-Path -LiteralPath $testStorageRoot)) {
        Write-Warning (
            "Receiver resilience failed; preserved GUID test root: " +
            $testStorageRoot)
    }
}

Assert-True (-not (Test-Path -LiteralPath $testStorageRoot)) `
    "successful resilience cleanup removes only its drained GUID root"
$testScriptSource = [IO.File]::ReadAllText($PSCommandPath)
$storageOwnershipMarker = $testScriptSource.IndexOf(
    '$testStorageRootSetupStarted = $false')
$storageOwnershipTry = $testScriptSource.IndexOf(
    'try {', $storageOwnershipMarker)
$firstStorageMutation = $testScriptSource.IndexOf(
    '$setStorageRootForTests.Invoke(', $storageOwnershipTry)
$successStorageCleanup = $testScriptSource.IndexOf(
    'Remove-Item -LiteralPath $testStorageRoot -Recurse -Force',
    $firstStorageMutation)
$failureStorageWarning = $testScriptSource.IndexOf(
    'Receiver resilience failed; preserved GUID test root:',
    $successStorageCleanup)
Assert-True ($storageOwnershipMarker -ge 0 -and
    $storageOwnershipTry -gt $storageOwnershipMarker -and
    $firstStorageMutation -gt $storageOwnershipTry) `
    "isolated test-root ownership begins before the first filesystem mutation"
Assert-True ($successStorageCleanup -gt $firstStorageMutation -and
    $failureStorageWarning -gt $successStorageCleanup) `
    "successful runs clean the exact root while failed runs announce its preserved path"
Write-Host "Receiver resilience checks passed."

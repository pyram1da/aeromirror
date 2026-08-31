param(
    [string]$LibUxPlayRoot = "",
    [string]$CompilerPath = "",
    [int]$TimeoutSeconds = 20
)

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
    Assert-True ([regex]::IsMatch(
        $Text, $Pattern,
        [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::Singleline)) $Message
}

function Assert-NoMatch(
    [string]$Text,
    [string]$Pattern,
    [string]$Message
) {
    Assert-True (-not [regex]::IsMatch(
        $Text, $Pattern,
        [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::Singleline)) $Message
}

function Assert-MatchCount(
    [string]$Text,
    [string]$Pattern,
    [int]$Expected,
    [string]$Message
) {
    $options = [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::Singleline
    $actual = [regex]::Matches($Text, $Pattern, $options).Count
    Assert-True ($actual -eq $Expected) `
        "$Message (expected $Expected, found $actual)"
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
if ([string]::IsNullOrWhiteSpace($LibUxPlayRoot)) {
    $LibUxPlayRoot = Join-Path (Split-Path -Parent $projectRoot) `
        "upstream-uxplay-windows\libuxplay"
}
if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    $CompilerPath = Join-Path (
        Split-Path -Parent (Split-Path -Parent (
            Split-Path -Parent (Split-Path -Parent $projectRoot)))) `
        "msys64\ucrt64\bin\gcc.exe"
}

$libRoot = (Resolve-Path -LiteralPath $LibUxPlayRoot).Path
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$cxxCompiler = Join-Path (Split-Path -Parent $compiler) "g++.exe"
$ucrtRoot = Split-Path -Parent (Split-Path -Parent $compiler)
$opensslInclude = Join-Path $ucrtRoot "include"
$cryptoImportLibrary = Join-Path $ucrtRoot "lib\libcrypto.dll.a"

$cryptoSource = Join-Path $libRoot "lib\crypto.c"
$cryptoHeader = Join-Path $libRoot "lib\crypto.h"
$pairingSource = Join-Path $libRoot "lib\pairing.c"
$pairingHeader = Join-Path $libRoot "lib\pairing.h"
$mirrorBufferSource = Join-Path $libRoot "lib\mirror_buffer.c"
$raopBufferSource = Join-Path $libRoot "lib\raop_buffer.c"
$raopSource = Join-Path $libRoot "lib\raop.c"
$handlersSource = Join-Path $libRoot "lib\raop_handlers.h"
$ntpSource = Join-Path $libRoot "lib\raop_ntp.c"
$ntpHeader = Join-Path $libRoot "lib\raop_ntp.h"
$rtpSource = Join-Path $libRoot "lib\raop_rtp.c"
$rtpHeader = Join-Path $libRoot "lib\raop_rtp.h"
$mirrorParserSource = Join-Path $libRoot "lib\mirror_payload_parser.c"
$mirrorParserHeader = Join-Path $libRoot "lib\mirror_payload_parser.h"
$mirrorSource = Join-Path $libRoot "lib\raop_rtp_mirror.c"
$raopHeader = Join-Path $libRoot "lib\raop.h"
$httpRequestSource = Join-Path $libRoot "lib\http_request.c"
$httpRequestHeader = Join-Path $libRoot "lib\http_request.h"
$llhttpApiSource = Join-Path $libRoot "lib\llhttp\api.c"
$llhttpHttpSource = Join-Path $libRoot "lib\llhttp\http.c"
$llhttpParserSource = Join-Path $libRoot "lib\llhttp\llhttp.c"
$httpHandlersSource = Join-Path $libRoot "lib\http_handlers.h"
$fcupRequestSource = Join-Path $libRoot "lib\fcup_request.h"
$httpdSource = Join-Path $libRoot "lib\httpd.c"
$airplayVideoSource = Join-Path $libRoot "lib\airplay_video.c"
$airplayVideoHeader = Join-Path $libRoot "lib\airplay_video.h"
$fairplaySource = Join-Path $libRoot "lib\fairplay_playfair.c"
$videoRendererSource = Join-Path $libRoot "renderers\video_renderer.c"
$audioRendererSource = Join-Path $libRoot "renderers\audio_renderer.c"
$uxplaySource = Join-Path $libRoot "uxplay.cpp"
$uxplayApiHeader = Join-Path $libRoot "uxplay_api.h"
$logProtocolHeader = Join-Path $libRoot "aeromirror_log_protocol.h"
$loggerSource = Join-Path $libRoot "lib\logger.c"
$loggerHeader = Join-Path $libRoot "lib\logger.h"
$wrapperRoot = Split-Path -Parent $libRoot
$wrapperWindowSource = Join-Path $wrapperRoot "src\mainwindow.cpp"
$wrapperWindowHeader = Join-Path $wrapperRoot "src\mainwindow.h"
$harnessSource = Join-Path $PSScriptRoot "NativeCryptoHappyPathHarness.c"

foreach ($path in @(
    $cryptoSource,
    $cryptoHeader,
    $pairingSource,
    $pairingHeader,
    $mirrorBufferSource,
    $raopBufferSource,
    $raopSource,
    $handlersSource,
    $ntpSource,
    $ntpHeader,
    $rtpSource,
    $rtpHeader,
    $mirrorParserSource,
    $mirrorParserHeader,
    $mirrorSource,
    $raopHeader,
    $httpRequestSource,
    $httpRequestHeader,
    $llhttpApiSource,
    $llhttpHttpSource,
    $llhttpParserSource,
    $httpHandlersSource,
    $fcupRequestSource,
    $httpdSource,
    $airplayVideoSource,
    $airplayVideoHeader,
    $fairplaySource,
    $videoRendererSource,
    $audioRendererSource,
    $uxplaySource,
    $uxplayApiHeader,
    $logProtocolHeader,
    $loggerSource,
    $loggerHeader,
    $wrapperWindowSource,
    $wrapperWindowHeader,
    $harnessSource,
    $cxxCompiler,
    $opensslInclude,
    $cryptoImportLibrary
)) {
    Assert-True (Test-Path -LiteralPath $path) `
        "required native core contract input exists: $path"
}

$cryptoText = Get-Content -LiteralPath $cryptoSource -Raw
$cryptoHeaderText = Get-Content -LiteralPath $cryptoHeader -Raw
$pairingText = Get-Content -LiteralPath $pairingSource -Raw
$pairingHeaderText = Get-Content -LiteralPath $pairingHeader -Raw
$mirrorBufferText = Get-Content -LiteralPath $mirrorBufferSource -Raw
$raopBufferText = Get-Content -LiteralPath $raopBufferSource -Raw
$raopText = Get-Content -LiteralPath $raopSource -Raw
$handlersText = Get-Content -LiteralPath $handlersSource -Raw
$ntpText = Get-Content -LiteralPath $ntpSource -Raw
$ntpHeaderText = Get-Content -LiteralPath $ntpHeader -Raw
$rtpText = Get-Content -LiteralPath $rtpSource -Raw
$rtpHeaderText = Get-Content -LiteralPath $rtpHeader -Raw
$mirrorParserText = Get-Content -LiteralPath $mirrorParserSource -Raw
$mirrorParserHeaderText = Get-Content -LiteralPath $mirrorParserHeader -Raw
$mirrorText = Get-Content -LiteralPath $mirrorSource -Raw
$raopHeaderText = Get-Content -LiteralPath $raopHeader -Raw
$httpRequestText = Get-Content -LiteralPath $httpRequestSource -Raw
$httpRequestHeaderText = Get-Content -LiteralPath $httpRequestHeader -Raw
$httpHandlersText = Get-Content -LiteralPath $httpHandlersSource -Raw
$fcupRequestText = Get-Content -LiteralPath $fcupRequestSource -Raw
$httpdText = Get-Content -LiteralPath $httpdSource -Raw
$airplayVideoText = Get-Content -LiteralPath $airplayVideoSource -Raw
$airplayVideoHeaderText = Get-Content -LiteralPath $airplayVideoHeader -Raw
$fairplayText = Get-Content -LiteralPath $fairplaySource -Raw
$videoRendererText = Get-Content -LiteralPath $videoRendererSource -Raw
$audioRendererText = Get-Content -LiteralPath $audioRendererSource -Raw
$uxplayText = Get-Content -LiteralPath $uxplaySource -Raw
$uxplayApiText = Get-Content -LiteralPath $uxplayApiHeader -Raw
$logProtocolText = Get-Content -LiteralPath $logProtocolHeader -Raw
$loggerText = Get-Content -LiteralPath $loggerSource -Raw
$loggerHeaderText = Get-Content -LiteralPath $loggerHeader -Raw
$wrapperWindowText = Get-Content -LiteralPath $wrapperWindowSource -Raw
$wrapperWindowHeaderText = Get-Content -LiteralPath $wrapperWindowHeader -Raw

# First-device trust is orchestrated by the shell. The native receiver emits
# only a request id, accepts the four digits over inherited stdin, and retains
# them only for the bounded SRP exchange.
Assert-True ($raopHeaderText.Contains(
        'bool  (*request_pairing_pin) (void *cls, char pin[5], uint64_t *request_id);') -and
    $raopHeaderText.Contains(
        'bool  (*pairing_pin_is_active) (void *cls, uint64_t request_id);') -and
    $raopHeaderText.Contains(
        'void  (*pairing_pin_failed) (void *cls, uint64_t request_id);') -and
    $raopHeaderText.Contains(
        'bool  (*register_client) (void *cls, const char *device_id, const char *pk_str, const char *name, uint64_t request_id);') -and
    $uxplayApiText.Contains('submit_pairing_pin(') -and
    $uxplayApiText.Contains('cancel_pairing_pin(')) `
    "native pairing exposes a bounded shell-to-SRP callback contract"
Assert-True ($handlersText.Contains(
        'raop->callbacks.request_pairing_pin') -and
    $handlersText.Contains('raop->callbacks.pairing_pin_failed') -and
    $handlersText.Contains('conn->pairing_pin_request_id') -and
    $handlersText.Contains('conn->pairing_pin') -and
    $handlersText.Contains(
        'raop_clear_transient_pin(pin, sizeof(pin))') -and
    $handlersText.Contains('Client Authentication Failure') -and
    $handlersText.Contains('Pairing PIN is ready for entry on the client') -and
    -not $handlersText.Contains(
        'CLIENT MUST NOW ENTER PIN = \"%s\"')) `
    "pair-pin-start blocks for the shell response without logging PIN digits"
$initialPairSetupSlice = Get-SourceSlice $handlersText `
    '/* this is the initial pair-setup-pin request */' `
    '} else if (PLIST_IS_DATA(req_pk_node)' `
    "initial pair-setup-pin request"
Assert-True ($initialPairSetupSlice.Contains(
        'pair-setup-pin: processing initial request') -and
    -not [regex]::IsMatch(
        $initialPairSetupSlice,
        'logger_log\s*\([^;]*(?:%s|device_id|,\s*user\s*\))')) `
    "initial PIN pairing never logs the untrusted SRP user or device identifier"

# Ordinary logs and authenticated shell-control markers share stdout, but not
# an emitter. Remote metadata is flattened to one line and every marker token
# is neutralized before an ordinary log can reach stdout.
$ordinaryLogSlice = Get-SourceSlice $uxplayText `
    'static void log(int level, const char* format, ...)' `
    'static void aeromirror_protocol_marker(' `
    "ordinary native log writer"
$protocolLogSlice = Get-SourceSlice $uxplayText `
    'static void aeromirror_protocol_marker(' `
    '#define LOGD' `
    "native protocol marker writer"
$logCallbackSlice = Get-SourceSlice $uxplayText `
    'extern "C" void log_callback' `
    'static int start_raop_server' `
    "native logger callback"
$clientRequestSlice = Get-SourceSlice $uxplayText `
    'extern "C" void report_client_request' `
    'extern "C" void audio_process' `
    "client request reporting"
Assert-True ($logProtocolText.Contains(
        '#define AEROMIRROR_PROTOCOL_PREFIX "AEROMIRROR_"') -and
    $logProtocolText.Contains('value < 0x20 || value == 0x7f') -and
    $logProtocolText.Contains("*cursor = ' '") -and
    $logProtocolText.Contains(
        'aeromirror_ascii_equal_ignore_case(') -and
    $logProtocolText.Contains(
        "candidate[prefix_length - 1] = '-'") -and
    $ordinaryLogSlice.Contains(
        'aeromirror_sanitize_ordinary_log(line);')) `
    "ordinary native logs flatten controls and neutralize every control-marker token"
Assert-InOrder $ordinaryLogSlice @(
    'vsnprintf(',
    'aeromirror_sanitize_ordinary_log(line);',
    "line[length++] = '\n';",
    'fwrite(line, 1, length, stdout)'
) "ordinary log sanitization occurs after formatting and before the sole line terminator"
$ordinaryOutputSlice = Get-SourceSlice $logProtocolText `
    'static inline int aeromirror_ordinary_output(' `
    '#endif' `
    "shared ordinary output writer"
Assert-InOrder $ordinaryOutputSlice @(
    'vsnprintf(',
    'aeromirror_sanitize_ordinary_log(line);',
    "line[length++] = '\n';",
    'fwrite(line, 1, length, stream)',
    'fflush(stream)'
) "direct ordinary output is flattened, marker-neutralized, and emitted as one bounded line"
Assert-True ($protocolLogSlice.Contains(
        'aeromirror_sanitize_control_bytes(line + 1);') -and
    $protocolLogSlice.Contains(
        'if (!aeromirror_is_protocol_marker(line + 1)) return;') -and
    $loggerHeaderText.Contains('#define LOGGER_PROTOCOL') -and
    $loggerHeaderText.Contains('void logger_protocol(') -and
    $loggerText.Contains(
        'logger->callback(logger->cls, LOGGER_PROTOCOL, buffer);') -and
    $logCallbackSlice.Contains('case LOGGER_PROTOCOL:') -and
    $logCallbackSlice.Contains(
        'aeromirror_protocol_marker("%s", msg);')) `
    "genuine machine markers use a dedicated validated logger callback path"
$markerEmitterText = [string]::Join("`n", @(
    $uxplayText,
    $handlersText,
    $mirrorText,
    $videoRendererText))
Assert-NoMatch $markerEmitterText `
    'LOG[DIWE]\s*\(\s*"AEROMIRROR_' `
    "no genuine control marker uses the ordinary executable logger"
Assert-NoMatch $markerEmitterText `
    'logger_log\s*\([^;]*?"AEROMIRROR_' `
    "no genuine control marker uses the ordinary library logger"
Assert-True ($clientRequestSlice.Contains(
        'LOGI("connection request from an AirPlay client")') -and
    $clientRequestSlice.Contains(
        'client connection denied because its device ID is not allowlisted') -and
    $clientRequestSlice.Contains(
        'attempt to connect by a blocked client: DENIED') -and
    -not $clientRequestSlice.Contains('connection request from %') -and
    -not $clientRequestSlice.Contains('clientID %') -and
    -not $clientRequestSlice.Contains('"-allow %s"')) `
    "client request diagnostics retain activity signals without logging remote identifiers"
$clientLogSurface = [string]::Join("`n", @(
    $raopText,
    $httpHandlersText,
    $fcupRequestText,
    $handlersText,
    $httpdText,
    $ntpText,
    $rtpText,
    $mirrorText,
    $raopBufferText,
    $uxplayText))
Assert-NoMatch $clientLogSurface `
    'logger_log\s*\([^;]*(?:ip_address|ipaddr|client_session_id|apple_session_id|dacp_id|active_remote_header|user_agent|rtpinfo|playback_uuid|fcup_response_url|stream_connection_id|packet_description)' `
    "ordinary native logs do not interpolate client identifiers"
Assert-NoMatch $clientLogSurface `
    'LOG[DIWE]\s*\([^;]*(?:password\.c_str|metadata_text\.c_str|url\.c_str)' `
    "executable diagnostics do not interpolate passwords, media metadata, or HLS URLs"
Assert-True (-not $raopText.Contains(
        'http_request_get_header_string(request') -and
    -not $httpHandlersText.Contains(
        'http_request_get_header_string(request') -and
    -not $fcupRequestText.Contains('utils_data_to_text(http_request') -and
    -not $httpdText.Contains('%.*s') -and
    -not $raopBufferText.Contains('utils_data_to_string') -and
    -not $ntpText.Contains('utils_data_to_string') -and
    -not $rtpText.Contains('utils_data_to_string') -and
    $uxplayText.Contains(
        'AirPlay audio metadata received (%d item(s))') -and
    $uxplayText.Contains(
        'Unhandled AirPlay audio metadata item received (%d bytes)') -and
    $uxplayText.Contains('on_video_play: start position %f')) `
    "request headers, packet bodies, media metadata, and playback URLs stay out of logs"
$hlsLanguageSlice = Get-SourceSlice $airplayVideoText `
    'char * select_master_playlist_language(' `
    'char *adjust_master_playlist (' `
    "HLS playlist language selection"
$hlsSetterSlice = Get-SourceSlice $airplayVideoText `
    'static bool replace_string(' `
    'const char *get_apple_session_id(' `
    "fallible HLS state setters"
Assert-InOrder $hlsSetterSlice @(
    'if (!target || !value || len == 0 || len > max_len',
    'if (!replacement)',
    'replacement[len] =',
    'bool set_apple_session_id(',
    'AIRPLAY_VIDEO_IDENTIFIER_BYTES',
    'bool set_playback_uuid(',
    'bool set_uri_prefix(',
    'AIRPLAY_VIDEO_URI_MAX_BYTES',
    'bool set_playback_location(',
    'bool set_language_selection(',
    'name_len > AIRPLAY_VIDEO_LANGUAGE_MAX_BYTES',
    'if (!name_copy || !code_copy)'
) "remote HLS state is copied through bounded fallible setters"
Assert-NoMatch $hlsSetterSlice '\b(?:assert|exit)\s*\(' `
    "HLS setters never terminate the receiver"
Assert-True ($airplayVideoHeaderText.Contains(
        '#define AIRPLAY_VIDEO_IDENTIFIER_BYTES 36U') -and
    $airplayVideoHeaderText.Contains(
        '#define AIRPLAY_VIDEO_URI_MAX_BYTES 4096U') -and
    $airplayVideoHeaderText.Contains(
        '#define AIRPLAY_VIDEO_LANGUAGE_MAX_BYTES 1024U')) `
    "HLS identifiers, URIs, and language selections have explicit caps"
$hlsLanguageParserSlice = Get-SourceSlice $airplayVideoText `
    'typedef struct language_s {' `
    'char * select_master_playlist_language(' `
    "HLS playlist language parser"
Assert-InOrder $hlsLanguageParserSlice @(
    "*slices = 0",
    "code_len >= sizeof(languages[i].code)",
    "parsed_count != count || !ptr",
    "copies <= 0 || count % copies != 0",
    "language_slices_free(languages, count)"
) "HLS language parser rejects malformed, oversized, and inconsistent remote playlists"
Assert-NoMatch $hlsLanguageParserSlice '\bassert\s*\(' `
    "HLS language parser does not depend on debug-only assertions for remote data"
Assert-True ($airplayVideoText.Contains(
        '#include "../aeromirror_log_protocol.h"') -and
    [regex]::Matches(
        $hlsLanguageSlice,
        'aeromirror_ordinary_output\s*\(').Count -eq 5 -and
    -not $hlsLanguageSlice.Contains(
        'printf("%2d %-5.5s') -and
    -not $hlsLanguageSlice.Contains(
        'printf("language choice:') -and
    -not $hlsLanguageSlice.Contains(
        'printf("using HLS-specified language choice:') -and
    -not $hlsLanguageSlice.Contains(
        'printf("using default language choice:')) `
    "HLS playlist-derived language code and name use the sanitized ordinary-output path"
$hlsUriTableSlice = Get-SourceSlice $airplayVideoText `
    'int create_media_uri_table(' `
    'static char *playlist_duplicate(' `
    "HLS media URI table parser"
Assert-InOrder $hlsUriTableSlice @(
    '*media_uri_table = NULL',
    '*num_uri = 0',
    '(size_t) datalen > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'strlen(master_playlist_data) != (size_t) datalen',
    'const size_t prefix_len = strlen(url_prefix)',
    'const char *playlist_end = master_playlist_data + (size_t) datalen',
    'if (count == 0)',
    'char **table = (char **) calloc(',
    'if (index != count || ptr != NULL)',
    'uri_table_error:',
    'free(table)'
) "HLS media URI table rejects inconsistent input and frees partial results"
Assert-MatchCount $hlsUriTableSlice `
    'playlist_find_bounded\s*\(\s*ptr \+ prefix_len, line_end, "m3u8"' `
    2 `
    "both HLS URI table passes bound the terminator after the full prefix"
Assert-MatchCount $hlsUriTableSlice `
    "memchr\s*\(\s*ptr, '\\n'," `
    2 `
    "both HLS URI table passes limit URI parsing to one playlist line"
Assert-NoMatch $hlsUriTableSlice '\b(?:assert|exit)\s*\(' `
    "HLS media URI table rejects remote data without assertions or process exit"
$hlsPlaylistCopySlice = Get-SourceSlice $airplayVideoText `
    'static char *playlist_duplicate(' `
    'char *adjust_master_playlist (' `
    "HLS playlist fallback copy"
Assert-InOrder $hlsPlaylistCopySlice @(
    'len > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'if (!copy)',
    'return NULL'
) "HLS fallback copies enforce the response cap and report allocation failure"
Assert-NoMatch $hlsPlaylistCopySlice '\bexit\s*\(' `
    "HLS fallback allocation failure cannot terminate the receiver"
Assert-True ($airplayVideoHeaderText.Contains(
        '#define AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES (32U * 1024U * 1024U)')) `
    "HLS input and response paths share an explicit 32 MiB playlist cap"
$hlsMediaParserSlice = Get-SourceSlice $airplayVideoText `
    'static bool playlist_line_has_prefix(' `
    'char * get_media_playlist(' `
    "HLS media playlist parser and commit"
Assert-InOrder $hlsMediaParserSlice @(
    'line_len >= prefix_len',
    'playlist_parse_nonnegative_int(',
    'const char *playlist_end = playlist + playlist_len',
    "memchr(line, '\n'",
    'line_len == 0',
    'playlist_line_has_prefix(line, line_len, "#EXTINF:")',
    'media_item_t parsed_item = {0}',
    'if (parse_media_playlist(&parsed_item) != 0)',
    'media_item->playlist = media_playlist'
) "media playlists are line-bounded and committed only after parsing succeeds"
Assert-NoMatch $hlsMediaParserSlice '\b(?:assert|exit)\s*\(' `
    "remote media playlist parsing never terminates the receiver"
$hlsCondensedStart = $airplayVideoText.IndexOf(
    'static bool playlist_size_add(', [StringComparison]::Ordinal)
Assert-True ($hlsCondensedStart -ge 0) `
    "HLS condensed playlist helpers exist"
$hlsCondensedSlice = $airplayVideoText.Substring($hlsCondensedStart)
Assert-InOrder $hlsCondensedSlice @(
    'static bool playlist_size_add(',
    'static const char *playlist_find_bounded(',
    'static bool playlist_find_quoted_attribute(',
    'static bool playlist_append_bytes(',
    'char *adjust_yt_condensed_playlist(',
    'header_start, header_end, "BASE-URI="',
    'nparams >= AIRPLAY_VIDEO_CONDENSED_MAX_PARAMS',
    'minimum_param_expansion',
    'const char *chunk_bound = next_chunk ? next_chunk : playlist_end',
    '!playlist_size_add(&new_len',
    'new_len > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'new_len > (size_t) INT_MAX',
    'char *new_playlist = (char *) malloc(new_len + 1)',
    'size_t remaining = new_len',
    '!playlist_append_bytes(',
    'remaining != 0',
    'condensed_fallback:',
    'return playlist_duplicate(media_playlist)'
) "condensed HLS playlists use bounded parsing, checked sizing, and exact writes"
Assert-True ($airplayVideoHeaderText.Contains(
        '#define AIRPLAY_VIDEO_CONDENSED_MAX_PARAMS 64U')) `
    "condensed HLS temporary arrays have an explicit small parameter cap"
Assert-MatchCount $hlsCondensedSlice `
    'playlist_find_bounded\s*\(\s*segment, chunk_bound, "#EXT"' `
    2 `
    "both condensed-playlist passes bound the chunk terminator"
Assert-True (-not $hlsCondensedSlice.Contains(
        'strstr(segment, "#EXT")') -and
    -not $hlsCondensedSlice.Contains(
        'chunk_end > chunk_bound')) `
    "condensed-playlist chunks cannot borrow the next chunk marker"
Assert-NoMatch $hlsCondensedSlice '\b(?:assert|exit)\s*\(' `
    "condensed HLS remote data falls back without assertions or process exit"
Assert-True ($httpHandlersText.Contains(
        '"playlistInsert item parameters received"') -and
    -not $httpHandlersText.Contains(
        'printf("playlistInsert parameter item list is:') -and
    -not $httpHandlersText.Contains(
        'plist_to_xml(req_params_item_node')) `
    "remote playlistInsert payloads are acknowledged without writing raw plist text to stdout"
$hlsPlaySlice = Get-SourceSlice $httpHandlersText `
    'http_handler_play(raop_conn_t *conn' `
    'http_handler_hls(raop_conn_t *conn' `
    "HLS play request handler"
$hlsActionSlice = Get-SourceSlice $httpHandlersText `
    'http_handler_action(raop_conn_t *conn' `
    'http_handler_play(raop_conn_t *conn' `
    "HLS action request handler"
Assert-InOrder $hlsActionSlice @(
    'if (uint_val > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'uint_val > (uint64_t) INT_MAX',
    'fcup_response_datalen = (int) uint_val',
    'char *selected_playlist =',
    'if (!selected_playlist)',
    'if (create_media_uri_table(',
    '!uri_list || num_uri <= 0',
    '"Master playlist contains no usable media URI"',
    'free(playlist)',
    'plist_mem_free(fcup_response_url)',
    'goto post_action_error',
    'if (ret == 1)',
    '} else if (ret < 0)',
    'if (fcup_request(',
    '!= 0)',
    'set_next_media_uri_id(airplay_video, ++uri_num)'
) "HLS action rejects unusable master playlists and releases request data"
Assert-NoMatch $hlsActionSlice '\bexit\s*\(' `
    "HLS action allocation and parsing failures cannot terminate the receiver"
Assert-InOrder $hlsPlaySlice @(
    'strlen(apple_session_id) != AIRPLAY_VIDEO_IDENTIFIER_BYTES',
    'if (!PLIST_IS_STRING(req_uuid_node))',
    'strlen(playback_uuid) != AIRPLAY_VIDEO_IDENTIFIER_BYTES',
    'plist_get_string_val(req_content_location_node, &playback_location)',
    "if (!playback_location || playback_location[0] == '\0' ||",
    'plist_get_string_val(req_client_proc_name_node, &client_proc_name)',
    'if (!client_proc_name)',
    'pending_video = airplay_video_init(',
    'set_playback_location(',
    'set_uri_prefix(',
    'raop->airplay_video[id] = pending_video',
    'if (fcup_request(',
    'if (count == MAX_AIRPLAY_VIDEO)'
) "HLS play fully validates and prepares state before atomically committing a slot"
Assert-True (-not $hlsPlaySlice.Contains(
        'if (!playback_location) {') -and
    -not [regex]::IsMatch($hlsPlaySlice, '\b(?:assert|exit)\s*\(')) `
    "HLS play allocation failure rejects the request instead of terminating the receiver"
$fcupSlice = $fcupRequestText
Assert-InOrder $fcupSlice @(
    '*datalen = 0',
    'if (!url || !client_session_id || !datalen)',
    'if (!req_root_node || !session_id_node || !type_node',
    'plist_to_xml(req_root_node, &plist_xml, &uint_val)',
    'if (!plist_xml || uint_val == 0 || uint_val > (uint32_t) INT_MAX',
    'strlen(plist_xml) != (size_t) uint_val',
    'if (!plist_xml || datalen <= 0)',
    'if (!request)',
    'if (!http_request || requestlen <= 0)',
    'while (send_len < requestlen)',
    'if (sent <= 0)'
) "FCUP request construction and partial sends propagate failure"
Assert-NoMatch $fcupSlice '\b(?:assert|exit)\s*\(' `
    "FCUP failure cannot terminate the receiver"
$scrubSlice = Get-SourceSlice $httpHandlersText `
    'http_handler_scrub(raop_conn_t *conn' `
    'http_handler_rate(raop_conn_t *conn' `
    "scrub query handler"
$rateSlice = Get-SourceSlice $httpHandlersText `
    'http_handler_rate(raop_conn_t *conn' `
    'http_handler_stop(raop_conn_t *conn' `
    "rate query handler"
foreach ($querySlice in @($scrubSlice, $rateSlice)) {
    Assert-InOrder $querySlice @(
        "url ? strchr(url, '?') : NULL",
        "const char *separator = strchr(data, '=')",
        "if (!separator || separator[1] == '\0')",
        'http_response_init(response, "HTTP/1.1", 400, "Bad Request")'
    ) "scrub and rate queries validate separators before pointer arithmetic"
}
$hlsHandlerStart = $httpHandlersText.IndexOf(
    'http_handler_hls(raop_conn_t *conn', [StringComparison]::Ordinal)
Assert-True ($hlsHandlerStart -ge 0) "HLS response handler exists"
$hlsHandlerSlice = $httpHandlersText.Substring($hlsHandlerStart)
Assert-InOrder $hlsHandlerSlice @(
    'if (!url)',
    'len > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'len > (size_t) INT_MAX',
    'char *data  = adjust_yt_condensed_playlist(media_playlist)',
    'if (!data)',
    'size_t data_len = strlen(data)',
    'data_len > AIRPLAY_VIDEO_PLAYLIST_MAX_BYTES',
    'data_len > (size_t) INT_MAX',
    '*response_datalen = (int) data_len'
) "HLS responses validate pointers and lengths before narrowing to int"
Assert-NoMatch $hlsHandlerSlice '\bexit\s*\(' `
    "HLS response allocation failures cannot terminate the receiver"
$hlsEosSlice = Get-SourceSlice $raopText `
    'void raop_destroy_airplay_video(' `
    'uint64_t get_local_time(' `
    "HLS end-of-stream lifecycle"
Assert-InOrder $hlsEosSlice @(
    'if (!raop || id < -1 || id >= MAX_AIRPLAY_VIDEO)',
    'void raop_handle_eos(',
    'if (id >= 0 && id < MAX_AIRPLAY_VIDEO && raop->airplay_video[id])',
    '"ignoring stale HLS end-of-stream notification"',
    'raop->current_video = -1',
    'if (raop->callbacks.video_reset)'
) "stale HLS EOS events reset safely without indexing an invalid slot"
Assert-NoMatch $hlsEosSlice '\b(?:assert|exit)\s*\(' `
    "HLS EOS races cannot terminate the receiver"
$verifiedClientKeySlice = Get-SourceSlice $pairingText `
    'pairing_session_get_verified_client_key(' `
    'pairing_session_make_nonce(' `
    "verified pair-verify client key"
Assert-True ($pairingHeaderText.Contains(
        'int pairing_session_get_verified_client_key(') -and
    $verifiedClientKeySlice.Contains(
        'session->status != STATUS_FINISHED') -and
    $verifiedClientKeySlice.Contains('session->ed_theirs') -and
    $verifiedClientKeySlice.Contains(
        'ed25519_key_get_raw(public_key, session->ed_theirs)')) `
    "SETUP can obtain a client key only after pair-verify signature completion"
Assert-True ($uxplayText.Contains(
        'AEROMIRROR_PAIRING_PIN_REQUIRED request=%llu timeout_seconds=60') -and
    $uxplayText.Contains(
        'AEROMIRROR_PAIRING_STATE request=%llu state=%s') -and
    $uxplayText.Contains('std::chrono::seconds(60)') -and
    $uxplayText.Contains('aeromirror_clear_pairing_pin(') -and
    $uxplayText.Contains('aeromirror_pairing_pin_condition.wait_for(') -and
    $uxplayText.Contains(
        'aeromirror_pairing_pin_waiting_request ||') -and
    $uxplayText.Contains(
        'aeromirror_pairing_pin_active_request) {') -and
    $uxplayText.Contains('extern "C" void pairing_pin_failed(') -and
    $uxplayText.Contains(
        'aeromirror_pairing_pin_active_request == request_id') -and
    $uxplayText.Contains('fflush(fp)') -and
    $uxplayText.Contains('fclose(fp)') -and
    $uxplayText.Contains(
        'invalid -pin value; expected at most four decimal digits') -and
    -not $uxplayText.Contains('invalid \"-pin %s\"')) `
    "native pairing has structured lifecycle markers, bounded waiting, secret clearing, and flushed trust persistence"
$registerClientSlice = Get-SourceSlice $uxplayText `
    'extern "C" bool register_client(' `
    'extern "C" bool check_register(' `
    "trusted-client registration"
Assert-True ($registerClientSlice.Contains(
        'completed pairing for a new AirPlay client') -and
    $registerClientSlice.Contains(
        'ignored stale AirPlay pairing registration') -and
    -not $registerClientSlice.Contains('LOGI("%s') -and
    -not $registerClientSlice.Contains('LOGD("%s')) `
    "trusted-client registration never logs device keys or pairing digits"
$trustedParserSlice = Get-SourceSlice $uxplayText `
    'static bool aeromirror_is_canonical_trusted_key(' `
    'extern "C" bool register_client(' `
    "trusted-client record parser"
$trustedLoadSlice = Get-SourceSlice $uxplayText `
    '/* read in public keys that were previously registered with pair-setup-pin */' `
    'if (pin_pw == 1 && keyfile == "0")' `
    "trusted-client register loading"
$checkRegisterSlice = Get-SourceSlice $uxplayText `
    'extern "C" bool check_register(' `
    '/* control  callbacks for video player' `
    "trusted-client lookup"
Assert-True ($uxplayText.Contains(
        'static std::mutex aeromirror_trust_store_mutex;') -and
    $trustedParserSlice.Contains('record.size() < 45') -and
    $trustedParserSlice.Contains("record[44] != ','") -and
    $trustedParserSlice.Contains('value[43] !=') -and
    $trustedParserSlice.Contains("ch >= 'A' && ch <= 'Z'") -and
    $trustedParserSlice.Contains("ch >= 'a' && ch <= 'z'") -and
    $trustedParserSlice.Contains("ch >= '0' && ch <= '9'") -and
    $trustedParserSlice.Contains("ch < 0x20 || ch == 0x7f") -and
    -not $trustedLoadSlice.Contains('line[44]') -and
    -not $trustedLoadSlice.Contains('line.c_str()')) `
    "trusted-client records validate length, canonical base64, delimiter, and controls before indexing"
Assert-True ($registerClientSlice.Contains(
        'std::unique_lock<std::mutex> pairing_guard(') -and
    $registerClientSlice.Contains(
        'std::lock_guard<std::mutex> trust_guard(') -and
    $registerClientSlice.Contains('fprintf(fp, "%s,\n", client_pk)') -and
    -not $registerClientSlice.Contains('device_id, client_name') -and
    $checkRegisterSlice.Contains(
        'std::lock_guard<std::mutex> trust_guard(aeromirror_trust_store_mutex)') -and
    $trustedLoadSlice.Contains(
        'std::lock_guard<std::mutex> trust_guard(') -and
    $trustedLoadSlice.Contains('registered_keys.clear()')) `
    "load, lookup, append, and in-memory publication share one trust-store mutex without client metadata persistence"
Assert-InOrder $registerClientSlice @(
    'if (!pairing_request_id)',
    'registered_keys.begin(), registered_keys.end(), pk',
    'std::unique_lock<std::mutex> pairing_guard(',
    'aeromirror_pairing_pin_active_request != pairing_request_id',
    'std::lock_guard<std::mutex> trust_guard(',
    'fprintf(fp, "%s,\n", client_pk)',
    'fflush(fp)',
    'fclose(fp)',
    'if (persisted) registered_keys.push_back(pk)',
    'AEROMIRROR_PAIRING_STATE request=%llu state=%s',
    'aeromirror_pairing_pin_active_request = 0',
    'pairing_guard.unlock()',
    'return true'
) "the exact pairing request stays active through serialized durable trust publication and its terminal marker"
Assert-True ($registerClientSlice.Contains(
        'ignored stale AirPlay pairing registration') -and
    $registerClientSlice.Contains('persisted ? "trusted" : "persist-failed"') -and
    -not $registerClientSlice.Contains('return persisted;')) `
    "stale registration fails closed while an exact completed PIN session survives a persist-failed terminal result"
Assert-Match $registerClientSlice `
    'if\s*\(!pairing_request_id\s*\|\|\s*aeromirror_pairing_pin_active_request\s*!=\s*pairing_request_id\)\s*\{[^}]*return false;' `
    "canceled and stale pairing requests return a negative admission result"
$pairingFailureStart = $handlersText.IndexOf(
    'raop_fail_pairing_request(raop_conn_t *conn)',
    [StringComparison]::Ordinal)
$pairingFailureEnd = $handlersText.IndexOf(
    'raop_handler_info(raop_conn_t *conn',
    $pairingFailureStart, [StringComparison]::Ordinal)
Assert-True ($pairingFailureStart -ge 0 -and
    $pairingFailureEnd -gt $pairingFailureStart) `
    "pairing request failure helper is present"
$pairingFailureSlice = $handlersText.Substring(
    $pairingFailureStart, $pairingFailureEnd - $pairingFailureStart)
$setupStart = $handlersText.IndexOf(
    'raop_handler_setup(raop_conn_t *conn',
    [StringComparison]::Ordinal)
$registrationDecision = $handlersText.IndexOf(
    'bool registration_required =',
    $setupStart, [StringComparison]::Ordinal)
Assert-True ($setupStart -ge 0 -and
    $registrationDecision -gt $setupStart) `
    "SETUP pairing admission decision is present"
$setupBeforeAdmission = $handlersText.Substring(
    $setupStart, $registrationDecision - $setupStart)
$setupEnd = $handlersText.IndexOf(
    'raop_handler_get_parameter(raop_conn_t *conn',
    $registrationDecision, [StringComparison]::Ordinal)
Assert-True ($setupEnd -gt $registrationDecision) `
    "SETUP handler end follows pairing admission decision"
$setupSlice = $handlersText.Substring(
    $setupStart, $setupEnd - $setupStart)
$connDestroySlice = Get-SourceSlice $raopText `
    'conn_destroy(void *ptr) {' `
    'raop_init(raop_callbacks_t *callbacks)' `
    "connection destruction"
Assert-True ($pairingFailureSlice.Contains(
        'uint64_t request_id = conn->pairing_pin_request_id;') -and
    $pairingFailureSlice.Contains('conn->pairing_pin_request_id = 0;') -and
    $pairingFailureSlice.Contains('conn->pairing_pin = 0;') -and
    $pairingFailureSlice.Contains(
        'raop->callbacks.pairing_pin_failed(') -and
    $connDestroySlice.Contains('raop_fail_pairing_request(conn);')) `
    "disconnect atomically detaches the connection request and terminalizes its exact native pairing request"
Assert-InOrder $pairingFailureSlice @(
    'uint64_t request_id = conn->pairing_pin_request_id;',
    'conn->pairing_pin_request_id = 0;',
    'conn->pairing_pin = 0;',
    'raop->callbacks.pairing_pin_failed('
) "connection-local pairing state is cleared before its terminal callback"
$preAdmissionReturns = [regex]::Matches(
    $setupBeforeAdmission, '\breturn;').Count
$preAdmissionCleanup = [regex]::Matches(
    $setupBeforeAdmission,
    'raop_fail_pairing_request\(conn\);').Count
Assert-True ($preAdmissionReturns -gt 0 -and
    $preAdmissionReturns -eq $preAdmissionCleanup) `
    "every SETUP return before authoritative registration terminalizes the connection pairing request"
Assert-True ($setupSlice.Contains(
        'conn->pairing_pin_request_id && !key_setup') -and
    $setupSlice.Contains('bool registration_required =') -and
    $setupSlice.Contains('bool registration_evaluated = false;') -and
    $setupSlice.Contains('bool registration_admitted = false;') -and
    $setupSlice.Contains(
        'registration_admitted = raop->callbacks.register_client(') -and
    $setupSlice.Contains(
        'pairing_session_get_verified_client_key(') -and
    $setupSlice.Contains(
        '!conn->pairing_pin_request_id ||') -and
    $setupSlice.Contains(
        '!strcmp(pairing_client_pk, verified_client_pk)') -and
    $setupSlice.Contains(
        'verified_client_pk, name,') -and
    $setupSlice.Contains(
        'admit_client = admit_client && registration_evaluated &&') -and
    $setupSlice.Contains('raop_fail_pairing_request(conn);') -and
    $setupSlice.Contains('470, "Client Authentication Failure"')) `
    "SETUP admits a trusted reconnect by its verified key, requires a new PIN key match, and otherwise fails closed"
Assert-InOrder $setupSlice @(
    'raop->callbacks.report_client_request(',
    'bool registration_required =',
    'registration_admitted = raop->callbacks.register_client(',
    'admit_client = admit_client && registration_evaluated &&',
    'if (admit_client)',
    'raop_fail_pairing_request(conn);',
    'response_datalen, 470,'
) "policy rejection and negative registration both terminalize and reject SETUP"
Assert-True ($wrapperWindowText.Contains(
        '^AEROMIRROR_SECRET pairing-pin request=([1-9][0-9]{0,19}) pin=([0-9]{4})$') -and
    $wrapperWindowText.Contains('submit_pairing_pin(request, pin.c_str())') -and
    $wrapperWindowText.Contains('SecureZeroMemory(chunk, count)') -and
    $wrapperWindowText.Contains('secureClearString(pending)') -and
    $wrapperWindowText.Contains('SecureZeroMemory(&pin[0], pin.size())') -and
    $wrapperWindowText.Contains('secureClearString(line)') -and
    -not $wrapperWindowText.Contains('PAIRING_SECRET_REJECTED pin=')) `
    "wrapper accepts the secret only from exact stdin grammar and clears command buffers without echoing it"

# Fullscreen is owned by the actual Qt renderer host. The normal framed window
# remains movable/resizable, while fullscreen strips all caption styles and
# foreground Escape restores the exact saved style and geometry.
$rendererHostSlice = Get-SourceSlice $wrapperWindowText `
    'RendererHostWindow::RendererHostWindow(' `
    'MainWindow::MainWindow(' `
    "renderer host fullscreen"
Assert-True ($rendererHostSlice.Contains('m_normalGeometry = normalCandidate') -and
    $rendererHostSlice.Contains('(isMinimized() || isMaximized())') -and
    $rendererHostSlice.Contains('WS_MINIMIZE') -and
    $rendererHostSlice.Contains('WS_MAXIMIZE') -and
    $rendererHostSlice.Contains('GetWindowLongPtrW(host, GWL_STYLE)') -and
    $rendererHostSlice.Contains('GetWindowLongPtrW(host, GWL_EXSTYLE)') -and
    $rendererHostSlice.Contains(
        'style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX |') -and
    $rendererHostSlice.Contains('SetWindowLongPtrW(') -and
    $rendererHostSlice.Contains('setGeometry(m_normalGeometry)') -and
    $rendererHostSlice.Contains('qApp->installEventFilter(this)') -and
    $rendererHostSlice.Contains('keyEvent->key() == Qt::Key_Escape') -and
    $rendererHostSlice.Contains('nativeMessage->wParam == VK_ESCAPE') -and
    -not $rendererHostSlice.Contains('Qt::Key_Space')) `
    "actual fullscreen is captionless and Escape restores remembered framed geometry without a Space toggle"
Assert-True ($wrapperWindowHeaderText.Contains(
        'QRect m_normalGeometry;') -and
    $wrapperWindowHeaderText.Contains('intptr_t m_normalWindowStyle = 0;') -and
    $wrapperWindowHeaderText.Contains('intptr_t m_normalWindowExStyle = 0;')) `
    "renderer host retains its normal geometry and native frame styles"

# A stopped machine-wide Bonjour service is a terminal prerequisite state for
# the current registration attempt, not a reason to churn the native core.
$dnssdFailureStart = $uxplayText.LastIndexOf(
    "static void aeromirror_handle_dnssd_failure(",
    [StringComparison]::Ordinal)
$dnssdFailureEnd = $uxplayText.IndexOf(
    "static gboolean aeromirror_dnssd_watch(",
    $dnssdFailureStart, [StringComparison]::Ordinal)
Assert-True ($dnssdFailureStart -ge 0 -and
    $dnssdFailureEnd -gt $dnssdFailureStart) `
    "Bonjour prerequisite failure implementation is present"
$dnssdFailureSlice = $uxplayText.Substring(
    $dnssdFailureStart, $dnssdFailureEnd - $dnssdFailureStart)
$dnssdUnavailableSlice = Get-SourceSlice $dnssdFailureSlice `
    "if (error == aeromirror_dnssd_service_not_running)" `
    "aeromirror_dnssd_prerequisite_unavailable = false;" `
    "Bonjour unavailable branch"
$dnssdRetryCancelSlice = Get-SourceSlice $uxplayText `
    "static void aeromirror_cancel_dnssd_retry() {" `
    "static void aeromirror_schedule_dnssd_retry() {" `
    "DNS-SD retry cancellation"
$dnssdRetryScheduleSlice = Get-SourceSlice $uxplayText `
    "static void aeromirror_schedule_dnssd_retry() {" `
    "static void aeromirror_handle_dnssd_failure(" `
    "DNS-SD retry scheduling"
$dnssdBeginSlice = Get-SourceSlice $uxplayText `
    "static int aeromirror_begin_dnssd_generation(uint64_t request_id) {" `
    "static gboolean aeromirror_refresh_discovery_on_owner(" `
    "DNS-SD generation start"
$dnssdStartupSlice = Get-SourceSlice $uxplayText `
    "static int register_dnssd() {" `
    "static void unregister_dnssd() {" `
    "DNS-SD startup registration"
Assert-MatchCount $dnssdFailureSlice `
    'aeromirror_emit_discovery_failed\s*\(' 1 `
    "one terminal failure marker is emitted per handled DNS-SD failure"
Assert-MatchCount $dnssdFailureSlice `
    'dnssd_unregister_services\s*\(' 1 `
    "the failure handler deallocates an active DNS-SD pair at most once"
Assert-MatchCount $dnssdUnavailableSlice `
    'AEROMIRROR_DNSSD_PREREQUISITE_UNAVAILABLE' 1 `
    "a stopped Bonjour attempt emits one exact prerequisite marker"
Assert-MatchCount $dnssdUnavailableSlice `
    'AEROMIRROR_DNSSD_DEGRADED' 1 `
    "a stopped Bonjour attempt emits one degraded marker"
Assert-NoMatch $dnssdUnavailableSlice `
    'aeromirror_schedule_dnssd_retry\s*\(' `
    "a stopped Bonjour attempt does not arm the native retry loop"
Assert-InOrder $dnssdRetryCancelSlice @(
    "g_source_remove(aeromirror_dnssd_retry_id)",
    "aeromirror_dnssd_retry_id = 0",
    "aeromirror_dnssd_retry_attempt = 0"
) "terminal prerequisite handling clears both retry source and attempt"
Assert-Match $dnssdRetryScheduleSlice `
    'aeromirror_dnssd_retry_id\s*\|\|\s*\r?\n\s*aeromirror_dnssd_prerequisite_unavailable' `
    "the retry scheduler stays latched off while Bonjour is unavailable"
Assert-InOrder $dnssdBeginSlice @(
    "if (request_id)",
    "aeromirror_cancel_dnssd_retry()",
    "aeromirror_dnssd_prerequisite_unavailable = false",
    "dnssd_register_services(",
    "aeromirror_handle_dnssd_failure(",
    "error, false)"
) "an explicit refresh clears the prerequisite latch and makes one registration attempt"
Assert-NoMatch $dnssdBeginSlice `
    'dnssd_unregister_services\s*\(|aeromirror_schedule_dnssd_retry\s*\(' `
    "synchronous registration failure cleanup is not duplicated by the generation owner"
Assert-NoMatch $dnssdStartupSlice 'AEROMIRROR_DNSSD_DEGRADED' `
    "startup does not duplicate the centralized degraded marker"
Assert-Match $dnssdStartupSlice `
    'if\s*\(dnssd_error\)\s*\{\s*return 0;' `
    "Bonjour prerequisite failure leaves the native core and its listeners alive"

# Crypto must remain a recoverable status API and a reusable streaming API.
Assert-Match $cryptoHeaderText `
    '\bint\s+aes_ctr_(?:reset|encrypt|decrypt|start_fresh_block)\s*\(' `
    "AES-CTR operations expose checked status results"
Assert-Match $cryptoHeaderText `
    '\bint\s+aes_cbc_(?:reset|encrypt|decrypt)\s*\(' `
    "AES-CBC operations expose checked status results"
Assert-Match $cryptoHeaderText `
    '\bint\s+sha_(?:update|final|reset)\s*\(' `
    "SHA operations expose checked status results"
Assert-NoMatch $cryptoText '\b(?:exit|abort|handle_error)\s*\(' `
    "production crypto never terminates the receiver process"

$encryptSlice = Get-SourceSlice $cryptoText `
    "static int aes_encrypt(" "static int aes_decrypt(" "aes_encrypt"
$decryptSlice = Get-SourceSlice $cryptoText `
    "static int aes_decrypt(" "static void aes_destroy(" "aes_decrypt"
$resetSlice = Get-SourceSlice $cryptoText `
    "static int aes_reset(" "// AES CTR" "aes_reset"
Assert-Match $encryptSlice 'EVP_EncryptUpdate\s*\(' `
    "AES encryption uses a reusable EVP update"
Assert-NoMatch $encryptSlice 'EVP_EncryptFinal' `
    "AES encryption does not finalize after every streaming chunk"
Assert-Match $encryptSlice 'out_len_e\s*==\s*in_len' `
    "AES encryption verifies the complete chunk was emitted"
Assert-Match $decryptSlice 'EVP_DecryptUpdate\s*\(' `
    "AES decryption uses a reusable EVP update"
Assert-NoMatch $decryptSlice 'EVP_DecryptFinal' `
    "AES decryption does not finalize after every streaming chunk"
Assert-Match $decryptSlice 'out_len_d\s*==\s*in_len' `
    "AES decryption verifies the complete chunk was emitted"
Assert-InOrder $resetSlice @(
    "EVP_CIPHER_CTX_reset",
    "EVP_CIPHER_CTX_set_padding",
    "ctx->block_offset = 0",
    "return 0"
) "AES reset restores EVP state before wrapper block bookkeeping"

# Catch accidental reintroduction of ignored crypto status calls.  This
# pattern only matches a complete bare C call statement.  Braces and earlier
# semicolons terminate the match, so checked multiline conditions are not
# mistaken for discarded calls merely because a continuation line starts with
# the function name.  Assignments and direct returns are deliberately accepted.
$checkedCryptoSources = @(
    $cryptoText,
    $pairingText,
    $mirrorBufferText,
    $raopBufferText,
    $handlersText
) -join "`n"
$bareCryptoStatusCall = '(?m)^\s*(?:' +
    'aes_(?:ctr|cbc)_(?:reset|encrypt|decrypt|start_fresh_block)|' +
    'sha_(?:update|final|reset)|md5_(?:update|final|reset)|' +
    'gcm_(?:encrypt|decrypt)|x25519_(?:key_get_raw|derive_secret)|' +
    'ed25519_(?:key_get_raw|sign|verify)|get_random_bytes|pk_to_base64' +
    ')\s*\([^;{}]*\)\s*;'
Assert-NoMatch $checkedCryptoSources $bareCryptoStatusCall `
    "security-relevant crypto status results are never discarded"

# NTP and audio worker starts use the same explicit tri-state contract.
Assert-Match $ntpHeaderText `
    ('RAOP_NTP_START_FAILED\s*=\s*-1\s*,\s*' +
     'RAOP_NTP_START_BUSY\s*=\s*0\s*,\s*' +
     'RAOP_NTP_START_OK\s*=\s*1') `
    "NTP start API defines failed, busy, and successful states"
Assert-Match $ntpHeaderText `
    '\bint\s+raop_ntp_start\s*\(' `
    "NTP start result is observable by SETUP"
Assert-Match $rtpHeaderText `
    ('RAOP_RTP_START_FAILED\s*=\s*-1\s*,\s*' +
     'RAOP_RTP_START_BUSY\s*=\s*0\s*,\s*' +
     'RAOP_RTP_START_OK\s*=\s*1') `
    "audio start API defines failed, busy, and successful states"
Assert-Match $rtpHeaderText `
    '\bint\s+raop_rtp_start_audio\s*\(' `
    "audio start result is observable by SETUP"

$ntpStart = Get-SourceSlice $ntpText `
    "raop_ntp_start(raop_ntp_t *raop_ntp" `
    "raop_ntp_wake_unlocked(" "raop_ntp_start"
Assert-InOrder $ntpStart @(
    "worker_lifecycle_can_start_locked",
    "return RAOP_NTP_START_BUSY",
    "raop_ntp_init_socket",
    "return RAOP_NTP_START_FAILED",
    "worker_lifecycle_start_thread_locked",
    "CLOSESOCKET(raop_ntp->tsock)",
    "return RAOP_NTP_START_FAILED",
    "*timing_lport = raop_ntp->timing_lport",
    "return RAOP_NTP_START_OK"
) "NTP publishes its bound port only after socket and thread success"

$audioStart = Get-SourceSlice $rtpText `
    "raop_rtp_start_audio(raop_rtp_t *raop_rtp" `
    "raop_rtp_set_volume(" "raop_rtp_start_audio"
Assert-InOrder $audioStart @(
    "worker_lifecycle_can_start_locked",
    "return RAOP_RTP_START_BUSY",
    "raop_rtp_init_sockets",
    "return RAOP_RTP_START_FAILED",
    "worker_lifecycle_start_thread_locked",
    "CLOSESOCKET(raop_rtp->csock)",
    "CLOSESOCKET(raop_rtp->dsock)",
    "return RAOP_RTP_START_FAILED",
    "*control_lport = raop_rtp->control_lport",
    "*data_lport = raop_rtp->data_lport",
    "return RAOP_RTP_START_OK"
) "audio publishes bound ports only after both sockets and its thread succeed"

# Every supplied SETUP mode must be valid, while a legitimate combined
# key/timing + streams request remains allowed.
$setupHandler = Get-SourceSlice $handlersText `
    "raop_handler_setup(raop_conn_t *conn" `
    "raop_handler_audiomode(raop_conn_t *conn" "raop_handler_setup"
Assert-InOrder $setupHandler @(
    'plist_dict_get_item(req_root_node, "ekey")',
    'plist_dict_get_item(req_root_node, "eiv")',
    'plist_dict_get_item(req_root_node, "streams")',
    "bool has_ekey",
    "bool has_eiv",
    "bool has_streams",
    "bool key_setup",
    "bool stream_setup",
    "(!key_setup && !stream_setup)",
    "raop_handler_setup_error",
    "plist_t res_root_node = plist_new_dict()"
) "SETUP validates supplied modes before response allocation or worker work"
Assert-Match $setupHandler `
    ('key_setup\s*=\s*PLIST_IS_DATA\(req_eiv_node\)\s*&&\s*' +
     'PLIST_IS_DATA\(req_ekey_node\)') `
    "SETUP accepts key mode only with both binary key fields"
Assert-Match $setupHandler `
    'stream_setup\s*=\s*PLIST_IS_ARRAY\(req_streams_node\)' `
    "SETUP accepts stream mode only with an array"
Assert-Match $setupHandler `
    '\(\(has_eiv\s*\|\|\s*has_ekey\)\s*&&\s*!key_setup\)' `
    "SETUP rejects partial or mistyped supplied key fields"
Assert-Match $setupHandler `
    '\(has_streams\s*&&\s*!stream_setup\)' `
    "SETUP rejects a mistyped supplied streams field"
Assert-NoMatch $setupHandler `
    '\bkey_setup\s*\^\s*stream_setup\b' `
    "SETUP does not impose an unsupported exclusive-or on combined mode"

Assert-InOrder $setupHandler @(
    "int ntp_start_result = raop_ntp_start",
    "if (ntp_start_result != RAOP_NTP_START_OK)",
    "raop_rtp_mirror_destroy(new_mirror)",
    "raop_rtp_destroy(new_rtp)",
    "raop_ntp_destroy(new_ntp)",
    "conn->raop_ntp = new_ntp",
    "conn->raop_rtp = new_rtp",
    "conn->raop_rtp_mirror = new_mirror",
    'plist_new_uint(timing_lport)'
) "first SETUP cleans failed timing ownership before publishing the session"
Assert-Match $setupHandler `
    'ntp_start_result\s*==\s*RAOP_NTP_START_BUSY\s*\?\s*409\s*:\s*500' `
    "first SETUP maps NTP busy and failure states explicitly"

Assert-InOrder $setupHandler @(
    "int audio_start_result = conn->raop_rtp",
    "if (audio_start_result != RAOP_RTP_START_OK)",
    "raop_handler_setup_error",
    'logger_log(raop->logger, LOGGER_DEBUG,',
    'plist_new_uint(dport)'
) "stream SETUP checks audio start before advertising its bound ports"
Assert-Match $setupHandler `
    'audio_start_result\s*==\s*RAOP_RTP_START_BUSY\s*\?\s*409\s*:\s*500' `
    "stream SETUP maps duplicate audio and internal start failure explicitly"

# Mirror payload parsing keeps remote lengths bounded and validates a complete
# frame before mutating length prefixes into Annex B start codes.
Assert-Match $mirrorParserHeaderText `
    'MIRROR_VIDEO_PAYLOAD_MAX\s+\(\(size_t\)\s*32U\s*\*\s*1024U\s*\*\s*1024U\)' `
    "mirroring video payloads retain the 32 MiB cap"
Assert-Match $mirrorParserHeaderText `
    'MIRROR_CONFIG_PAYLOAD_MAX\s+\(\(size_t\)\s*256U\s*\*\s*1024U\)' `
    "codec configuration payloads retain the 256 KiB cap"
Assert-Match $mirrorParserHeaderText `
    'MIRROR_REPORT_PLIST_MAX\s+\(\(size_t\)\s*1024U\s*\*\s*1024U\)' `
    "mirroring report plists retain the 1 MiB cap"
Assert-Match $mirrorParserHeaderText `
    'MIRROR_REPORT_TRAILER_SIZE\s+\(\(size_t\)\s*25000U\)' `
    "only the observed bounded report trailer remains allowed"
Assert-Match $mirrorParserHeaderText `
    'MIRROR_CONTROL_PAYLOAD_MAX\s+\(\(size_t\)\s*64U\s*\*\s*1024U\)' `
    "other mirroring control payloads retain the 64 KiB cap"

$cursorTake = Get-SourceSlice $mirrorParserText `
    "mirror_cursor_take(mirror_cursor_t *cursor" `
    "static uint16_t" "mirror_cursor_take"
Assert-InOrder $cursorTake @(
    "cursor->off > cursor->len",
    "len > cursor->len - cursor->off",
    "*span = cursor->data + cursor->off",
    "cursor->off += len"
) "mirror cursor validates subtraction bounds before exposing a span"

$sizeAdd = Get-SourceSlice $mirrorParserText `
    "mirror_size_add(size_t left" `
    "mirror_payload_is_h265(" "mirror_size_add"
Assert-InOrder $sizeAdd @(
    "!result",
    "right > SIZE_MAX - left",
    "*result = left + right"
) "mirroring size addition rejects overflow before calculating the sum"

$nalConvertStart = $mirrorParserText.IndexOf(
    "mirror_convert_nalus(", [StringComparison]::Ordinal)
Assert-True ($nalConvertStart -ge 0) "mirror NAL conversion exists"
$nalConvert = $mirrorParserText.Substring($nalConvertStart)
Assert-InOrder $nalConvert @(
    "Validate the complete frame before changing any length prefix",
    "remaining = payload_size - offset",
    "(size_t) wire_len > remaining",
    "if (count > (size_t) INT_MAX)",
    "offset = 0",
    "memcpy(payload + offset, nal_start_code"
) "NAL conversion completes a read-only validation pass before mutation"

$mirrorThread = Get-SourceSlice $mirrorText `
    "raop_rtp_mirror_thread(void *arg)" `
    "raop_rtp_mirror_init_socket(" "raop_rtp_mirror_thread"
Assert-InOrder $mirrorThread @(
    "uint32_t declared_payload_size = byteutils_get_int(packet, 0)",
    "mirror_payload_size_allowed(packet[4], payload_size)",
    "payload = malloc(payload_size)"
) "mirror transport rejects oversized declared payloads before allocation"
Assert-InOrder $mirrorThread @(
    "if (payload == NULL && ret == 0)",
    "raop_rtp_mirror_close_stream",
    "continue",
    "if (ret == 0)",
    "raop_rtp_mirror_mark_transport_failure",
    "break"
) "header EOF permits a new client while mid-payload EOF resets the session"
Assert-MatchCount $mirrorThread `
    'callbacks\.conn_reset\s*\(raop_rtp_mirror->callbacks\.cls,\s*1\)' `
    1 "mirror transport emits at most one connection reset at thread tail"

# HTTP request limits are applied incrementally and parse failures are removed
# before a request can reach the protocol callback.
Assert-Match $httpRequestHeaderText `
    'HTTP_REQUEST_MAX_URL_BYTES\s+4096U' `
    "request URL cap remains 4096 bytes"
Assert-Match $httpRequestHeaderText `
    'HTTP_REQUEST_MAX_HEADER_FIELDS\s+20U' `
    "request header field cap remains 20"
Assert-Match $httpRequestHeaderText `
    'HTTP_REQUEST_MAX_HEADER_NAME_BYTES\s+64U' `
    "request header name cap remains 64 bytes"
Assert-Match $httpRequestHeaderText `
    'HTTP_REQUEST_MAX_HEADER_VALUE_BYTES\s+1024U' `
    "request header value cap remains 1024 bytes"
Assert-Match $httpRequestHeaderText `
    'HTTP_REQUEST_MAX_BODY_BYTES\s+\(32U\s*\*\s*1024U\s*\*\s*1024U\)' `
    "request body cap remains 32 MiB"

$httpAppend = Get-SourceSlice $httpRequestText `
    "http_request_append(char **target" "on_url(llhttp_t *parser" `
    "http_request_append"
Assert-InOrder $httpAppend @(
    "*target_len > limit",
    "length > limit - *target_len",
    "realloc(*target, *target_len + length + 1U)"
) "HTTP fragmented fields check subtraction bounds before allocation"
$httpHeaderField = Get-SourceSlice $httpRequestText `
    "on_header_field(llhttp_t *parser" `
    "on_header_value(llhttp_t *parser" "on_header_field"
Assert-InOrder $httpHeaderField @(
    "HTTP_REQUEST_MAX_HEADER_FIELDS",
    "request->headers_size += 2",
    "realloc(request->headers"
) "HTTP header count is capped before the pointer array grows"
$httpBody = Get-SourceSlice $httpRequestText `
    "on_body(llhttp_t *parser" `
    "on_headers_complete(llhttp_t *parser" "on_body"
Assert-InOrder $httpBody @(
    "request->datalen > HTTP_REQUEST_MAX_BODY_BYTES",
    "length > HTTP_REQUEST_MAX_BODY_BYTES - request->datalen",
    "realloc(request->data, request->datalen + length)"
) "HTTP body growth checks subtraction bounds before allocation"
$httpHeaderLookup = Get-SourceSlice $httpRequestText `
    "http_request_get_header(http_request_t *request" `
    "http_request_header_get_size(http_request_t *request" `
    "HTTP header lookup"
Assert-Match $httpRequestText `
    'static\s+int\s+http_header_name_equals_ascii\s*\(' `
    "HTTP header lookup has a locale-independent ASCII comparator"
Assert-InOrder $httpHeaderLookup @(
    "http_header_name_equals_ascii(request->headers[i], name)",
    "return request->headers[i+1]"
) "HTTP field names are matched case-insensitively and exactly"
Assert-NoMatch $httpHeaderLookup `
    'strcmp\s*\(\s*request->headers\[i\]\s*,\s*name\s*\)' `
    "HTTP field names never regress to case-sensitive lookup"
$httpDispatch = Get-SourceSlice $httpdText `
    "Parse HTTP request from data read from connection" `
    "const char *data;" "httpd request parse and dispatch"
Assert-InOrder $httpDispatch @(
    "int parse_result = http_request_add_data",
    "parse_result != 0",
    "http_request_has_error(connection->request)",
    "httpd_remove_connection(httpd, connection, 0)",
    "continue",
    "http_request_is_complete(connection->request)",
    "httpd->callbacks.conn_request"
) "HTTP parser errors are dropped before protocol dispatch"

# FairPlay and audioMode handlers validate representation and ownership before
# fixed-offset reads, table indexing, key use, and logging.
$fairplaySetup = Get-SourceSlice $fairplayText `
    "fairplay_setup(fairplay_t *fp" `
    "fairplay_handshake(fairplay_t *fp" "fairplay_setup"
Assert-InOrder $fairplaySetup @(
    "!fp || !req || !res",
    "req[4] != 0x03",
    "req[14] >= 4",
    "int mode = req[14]",
    "reply_message[mode]"
) "FairPlay setup checks pointers, version, and mode before table indexing"
$fairplayHandshake = Get-SourceSlice $fairplayText `
    "fairplay_handshake(fairplay_t *fp" `
    "fairplay_decrypt(fairplay_t *fp" "fairplay_handshake"
Assert-InOrder $fairplayHandshake @(
    "!fp || !req || !res",
    "req[4] != 0x03",
    "memcpy(fp->keymsg, req, 164)",
    "fp->keymsglen = 164"
) "FairPlay handshake validates input before retaining fixed-size key data"
$fairplayDecrypt = Get-SourceSlice $fairplayText `
    "fairplay_decrypt(fairplay_t *fp" `
    "fairplay_destroy(fairplay_t *fp" "fairplay_decrypt"
Assert-InOrder $fairplayDecrypt @(
    "!fp || !input || !output",
    "fp->keymsglen != 164",
    "playfair_decrypt"
) "FairPlay decryption requires a completed 164-byte handshake"

$fpHandler = Get-SourceSlice $handlersText `
    "raop_handler_fpsetup(raop_conn_t *conn" `
    "raop_handler_options(raop_conn_t *conn" "raop_handler_fpsetup"
Assert-InOrder $fpHandler @(
    "!conn->fairplay || !data || (datalen != 16 && datalen != 164)",
    "if (datalen == 16)",
    "fairplay_setup",
    "free(*response_data)",
    "*response_data = NULL",
    "if (datalen == 164)",
    "fairplay_handshake",
    "free(*response_data)",
    "*response_data = NULL"
) "fp-setup enforces exact message sizes and cleans failed replies"

$audioModeHandler = Get-SourceSlice $handlersText `
    "raop_handler_audiomode(raop_conn_t *conn" `
    "raop_handler_feedback(raop_conn_t *conn" "raop_handler_audiomode"
Assert-InOrder $audioModeHandler @(
    "!data || data_len <= 0",
    "PLIST_IS_DICT(req_root_node)",
    "PLIST_IS_STRING(req_audiomode_node)",
    "plist_get_string_val(req_audiomode_node, &audiomode)",
    "if (!audiomode)",
    'Unhandled RTSP audioMode request',
    "plist_mem_free(audiomode)",
    "plist_free(req_root_node)"
) "audioMode checks plist types and frees extracted and root ownership"

Assert-InOrder $setupHandler @(
    "bool saw_audio = false",
    "bool saw_mirror = false",
    "Validate the complete request before starting any worker",
    "if (saw_mirror || !PLIST_IS_UINT(stream_id_node))",
    "if (saw_audio)",
    "plist_t res_streams_node = plist_new_array()",
    "raop_rtp_mirror_start",
    "raop_rtp_start_audio"
) "SETUP rejects duplicate streams during a complete validation pass"

# UDP state is accepted only from the negotiated peer, and endpoint pinning
# occurs only after packet length and type checks.
$rtpControl = Get-SourceSlice $rtpText `
    "if (FD_ISSET(raop_rtp->csock, &rfds))" `
    "rtp audio data packets" "RTP control receive"
Assert-InOrder $rtpControl @(
    "packetlen = recvfrom",
    "if (packetlen < 0)",
    "netutils_sockaddr_equal_ip(&saddr, &raop_rtp->remote_saddr)",
    "netutils_sockaddr_get_port(&saddr, &source_port)",
    "netutils_sockaddr_equal_endpoint(&saddr, &raop_rtp->control_saddr)",
    "packetlen < 2",
    "type_c = packet[1]",
    "type_c == 0x56 && packetlen >= 8",
    "memcpy(&raop_rtp->control_saddr, &saddr, saddrlen)",
    "process_control_packet = true"
) "RTP control state pins only a valid negotiated-peer packet"

$rtpData = Get-SourceSlice $rtpText `
    "if (FD_ISSET(raop_rtp->dsock, &rfds))" `
    "Natural exit deliberately leaves join debt" "RTP data receive"
Assert-InOrder $rtpData @(
    "packetlen = recvfrom",
    "if (packetlen < 0)",
    "netutils_sockaddr_equal_ip(&saddr, &raop_rtp->remote_saddr)",
    "packetlen < 2",
    "type_d = packet[1]",
    "type_d != 0x60 || packetlen < 12",
    "netutils_sockaddr_equal_endpoint(&saddr, &data_saddr)",
    "memcpy(&data_saddr, &saddr, saddrlen)",
    "got_remote_data_saddr = true"
) "RTP data endpoint pins only after peer, length, and type validation"

$ntpReceive = Get-SourceSlice $ntpText `
    "response_len = recvfrom" "// Sleep for 3 seconds" "NTP receive"
Assert-InOrder $ntpReceive @(
    "if (response_len < 0)",
    "response_len != 32",
    "netutils_sockaddr_equal_endpoint(&response_saddr",
    "response[0] != 0x80",
    "(response[1] & ~0x80) != 0x53",
    "memcmp(response + 8, request + 24, 8) != 0",
    "MUTEX_LOCK(raop_ntp->sync_params_mutex)",
    "raop_ntp->sync_offset = offset",
    "raop_ntp->client_time_received = true",
    "MUTEX_UNLOCK(raop_ntp->sync_params_mutex)"
) "NTP commits a synchronized sample only after exact peer and origin checks"
$videoOffsetSet = Get-SourceSlice $ntpText `
    "raop_ntp_set_video_arrival_offset(" `
    "raop_ntp_get_video_arrival_offset(" "NTP video offset setter"
Assert-InOrder $videoOffsetSet @(
    "MUTEX_LOCK(raop_ntp->sync_params_mutex)",
    "raop_ntp->video_arrival_offset = *offset",
    "MUTEX_UNLOCK(raop_ntp->sync_params_mutex)"
) "NTP video arrival offset writes use the synchronization mutex"
$videoOffsetGet = Get-SourceSlice $ntpText `
    "raop_ntp_get_video_arrival_offset(" `
    "raop_ntp_parse_remote(" "NTP video offset getter"
Assert-InOrder $videoOffsetGet @(
    "MUTEX_LOCK(raop_ntp->sync_params_mutex)",
    "uint64_t offset = raop_ntp->video_arrival_offset",
    "MUTEX_UNLOCK(raop_ntp->sync_params_mutex)",
    "return offset"
) "NTP video arrival offset reads use the synchronization mutex"

# Appsrc remains non-blocking and unbounded by deliberate policy: dropping an
# arbitrary inter-frame video buffer corrupts decode dependencies, while
# blocking here can stall the same reader that carries control transitions.
Assert-MatchCount $videoRendererText `
    'g_string_new\("appsrc name=video_source ! "\)' `
    1 "video pipeline has one canonical appsrc launch point"
Assert-MatchCount $audioRendererText `
    'g_string_new\("appsrc name=audio_source ! "\)' `
    1 "audio pipeline has one canonical appsrc launch point"
$rendererText = $videoRendererText + "`n" + $audioRendererText
Assert-NoMatch $rendererText `
    '\b(?:max-bytes|max-buffers|max-time|leaky-type|block)\s*=' `
    "renderer appsrc does not introduce unsafe drop or reader-block policy"

$videoSnapshot = Get-SourceSlice $videoRendererText `
    "aeromirror_snapshot_selected_renderer(" `
    "aeromirror_acquire_renderer_for_bus(" "video renderer snapshot"
Assert-InOrder $videoSnapshot @(
    "g_mutex_lock(&renderer_lock)",
    "gst_object_ref(renderer->appsrc)",
    "gst_object_ref(renderer->pipeline)",
    "g_mutex_unlock(&renderer_lock)"
) "video renderer snapshots strong appsrc and pipeline references under lock"
$videoBusAcquire = Get-SourceSlice $videoRendererText `
    "aeromirror_acquire_renderer_for_bus(" `
    "aeromirror_release_renderer_for_bus(" "video bus acquire"
Assert-InOrder $videoBusAcquire @(
    "g_mutex_lock(&renderer_lock)",
    "renderer_type[i]->bus == bus",
    "aeromirror_bus_callback_refs++",
    "gst_object_ref(selected->pipeline)",
    "gst_object_ref(selected->appsrc)",
    "g_mutex_unlock(&renderer_lock)"
) "video bus callback retains the exact bus owner and its GStreamer objects"
$videoBusRelease = Get-SourceSlice $videoRendererText `
    "aeromirror_release_renderer_for_bus(" `
    "static void aeromirror_health_reset(" "video bus release"
Assert-InOrder $videoBusRelease @(
    "g_mutex_lock(&renderer_lock)",
    "aeromirror_bus_callback_refs--",
    "g_cond_broadcast(&renderer_callback_cond)",
    "g_mutex_unlock(&renderer_lock)"
) "video bus callback releases retained renderer lifetime under lock"

$coverArtRender = Get-SourceSlice $videoRendererText `
    "video_renderer_display_jpeg(" `
    "video_renderer_render_buffer(" "cover-art render"
Assert-InOrder $coverArtRender @(
    'if (type_jpeg == -1 || !data || !data_len || *data_len <= 0)',
    'buffer = gst_buffer_new_allocate(',
    'if (!buffer)',
    'gsize written = gst_buffer_fill(',
    'if (written != (gsize) *data_len)',
    'gst_buffer_unref(buffer)',
    'gst_app_src_push_buffer(',
    'if (appsrc)',
    'gst_object_unref(appsrc)'
) "cover-art rendering validates allocations and exact buffer writes"
Assert-NoMatch $coverArtRender '\b(?:g_assert|assert|exit)\s*\(' `
    "cover-art allocation failure cannot terminate the receiver"

$hlsReady = Get-SourceSlice $videoRendererText `
    "video_renderer_hls_ready()" `
    "video_renderer_stop()" "HLS ready transition"
$hlsPlaybackInfo = Get-SourceSlice $videoRendererText `
    "video_get_playback_info(" `
    "video_renderer_set_start(" "HLS playback-info query"
$hlsSeek = Get-SourceSlice $videoRendererText `
    "video_renderer_seek(" `
    "video_renderer_listen(" "HLS seek"
foreach ($hlsPipelineUse in @($hlsReady, $hlsPlaybackInfo, $hlsSeek)) {
    Assert-InOrder $hlsPipelineUse @(
        'GstElement *pipeline = NULL',
        'aeromirror_snapshot_selected_renderer(',
        'gst_object_unref(pipeline)'
    ) "HLS control callbacks retain the selected pipeline while using it"
    Assert-NoMatch $hlsPipelineUse 'renderer->' `
        "HLS control callbacks do not dereference a renderer being replaced"
}
$chooseCodecSlice = Get-SourceSlice $videoRendererText `
    "video_renderer_choose_codec (" `
    "video_get_playback_info(" "video codec selection"
Assert-NoMatch $chooseCodecSlice '\bg_assert\s*\(' `
    "codec callbacks reject invalid HLS/cover-art state without aborting"

$videoRender = Get-SourceSlice $videoRendererText `
    "video_renderer_render_buffer(" `
    "video_renderer_flush(" "video render"
Assert-InOrder $videoRender @(
    "g_mutex_lock(&renderer_lock)",
    "gst_object_ref(renderer->appsrc)",
    "gst_object_ref(renderer->pipeline)",
    "base_time = gst_video_pipeline_base_time",
    "g_mutex_unlock(&renderer_lock)",
    "if (!appsrc || !pipeline)",
    "GstClockTime pts",
    "gst_app_src_push_buffer",
    "gst_object_unref(appsrc)",
    "gst_object_unref(pipeline)"
) "video render retains selected objects before reading clock state and PTS"
$videoResume = Get-SourceSlice $videoRendererText `
    "video_renderer_resume()" "video_renderer_start()" "video resume"
Assert-NoMatch $videoResume 'gst_element_get_state\s*\(' `
    "implicit resume never waits synchronously for a GStreamer state change"
Assert-InOrder $videoResume @(
    "aeromirror_snapshot_selected_renderer",
    "gst_element_set_state",
    "set_result == GST_STATE_CHANGE_FAILURE",
    "gst_object_unref(appsrc)",
    "gst_object_unref(pipeline)"
) "video resume checks immediate failure and releases its strong references"
$videoDestroyInstance = Get-SourceSlice $videoRendererText `
    "video_renderer_destroy_instance(" `
    "video_renderer_destroy()" "video renderer instance destroy"
Assert-InOrder $videoDestroyInstance @(
    "g_mutex_lock(&renderer_lock)",
    "while (renderer->aeromirror_bus_callback_refs > 0)",
    "g_cond_wait(&renderer_callback_cond, &renderer_lock)",
    "g_mutex_unlock(&renderer_lock)",
    "gst_object_unref (renderer->appsrc)",
    "gst_object_unref(renderer->pipeline)",
    "free (renderer)"
) "video destroy waits for mapped bus callbacks before releasing ownership"
$videoDestroy = Get-SourceSlice $videoRendererText `
    "video_renderer_destroy()" `
    "static void get_stream_status_name(" "video renderer destroy"
Assert-InOrder $videoDestroy @(
    "g_mutex_lock(&renderer_lock)",
    "renderer = NULL",
    "destroyed[i] = renderer_type[i]",
    "renderer_type[i] = NULL",
    "g_mutex_unlock(&renderer_lock)",
    "video_renderer_destroy_instance(destroyed[i])"
) "video destroy unpublishes renderer slots before releasing instances"
$chooseCodec = Get-SourceSlice $videoRendererText `
    "video_renderer_choose_codec (" `
    "video_renderer_set_start(" "video renderer codec selection"
Assert-NoMatch $chooseCodec '\bfree\s*\(' `
    "codec selection retains unused renderer structures"
Assert-NoMatch $chooseCodec 'renderer_type\s*\[[^\]]+\]\s*=\s*NULL' `
    "codec selection does not clear retained renderer slots"
Assert-InOrder $chooseCodec @(
    "gst_object_ref(renderer_type[i]->pipeline)",
    "g_mutex_unlock(&renderer_lock)",
    "gst_element_set_state(unused_pipelines[i], GST_STATE_NULL)",
    "gst_object_unref(unused_pipelines[i])"
) "codec selection stops unused pipelines through temporary strong references"

$audioStop = Get-SourceSlice $audioRendererText `
    "audio_renderer_stop()" "static void get_renderer_type(" "audio stop"
Assert-InOrder $audioStop @(
    "g_mutex_lock(&audio_renderer_lock)",
    "gst_object_ref(renderer->appsrc)",
    "gst_object_ref(renderer->pipeline)",
    "renderer = NULL",
    "g_mutex_unlock(&audio_renderer_lock)",
    "gst_app_src_end_of_stream",
    "gst_element_set_state",
    "gst_object_unref(appsrc)",
    "gst_object_unref(pipeline)"
) "audio stop snapshots strong references before unpublishing the renderer"
$audioDestroy = Get-SourceSlice $audioRendererText `
    "audio_renderer_destroy()" `
    "gstreamer_audio_pipeline_bus_callback(" "audio destroy"
Assert-InOrder $audioDestroy @(
    "audio_renderer_stop()",
    "g_mutex_lock(&audio_renderer_lock)",
    "destroyed[i] = renderer_type[i]",
    "renderer_type[i] = NULL",
    "g_mutex_unlock(&audio_renderer_lock)",
    "gst_object_unref(destroyed[i]->bus)",
    "gst_object_unref(destroyed[i]->pipeline)",
    "free(destroyed[i])"
) "audio destroy unpublishes slots under the same lock before releasing them"
$audioBus = Get-SourceSlice $audioRendererText `
    "gstreamer_audio_pipeline_bus_callback(" `
    "audio_renderer_listen(" "audio bus callback"
Assert-InOrder $audioBus @(
    "g_mutex_lock(&audio_renderer_lock)",
    "message_renderer->bus == bus",
    "gst_object_ref(message_renderer->appsrc)",
    "gst_object_ref(message_renderer->pipeline)",
    "g_mutex_unlock(&audio_renderer_lock)",
    "if (!message_pipeline)",
    "gst_object_unref(message_appsrc)",
    "gst_object_unref(message_pipeline)"
) "audio bus callback maps the exact bus and retains objects under lock"

# A valid video access unit resumes a paused pipeline even if the iPhone omits
# the usual codec-option resume signal.  The action is durable in diagnostics.
Assert-Match $raopHeaderText `
    'MIRROR_PACKET_ACTION_IMPLICIT_RESUME\s*=\s*3' `
    "mirroring diagnostics expose the implicit-resume action"
$type0Start = $mirrorThread.IndexOf("case  0x00:", [StringComparison]::Ordinal)
$type0End = $mirrorThread.IndexOf("case 0x01:", $type0Start,
                                  [StringComparison]::Ordinal)
Assert-True ($type0Start -ge 0 -and $type0End -gt $type0Start) `
    "mirror type-0 handler slice exists"
$type0Handler = $mirrorThread.Substring($type0Start, $type0End - $type0Start)
Assert-InOrder $type0Handler @(
    "mirror_buffer_decrypt",
    "mirror_convert_nalus",
    "if (video_stream_suspended)",
    "video_stream_suspended = false",
    "type0_action = MIRROR_PACKET_ACTION_IMPLICIT_RESUME",
    "AEROMIRROR_VIDEO_IMPLICIT_RESUME reason=valid-type0",
    "callbacks.video_resume",
    "packet[6], type0_action",
    "callbacks.video_process"
) "implicit resume occurs only after decrypt and complete NAL validation"
Assert-MatchCount $type0Handler `
    'packet\[6\],\s*type0_action' `
    1 "the type-0 diagnostic records the implicit action exactly once"
Assert-InOrder $uxplayText @(
    "static std::atomic<uint64_t> aeromirror_implicit_resume_actions(0)",
    "event->action == MIRROR_PACKET_ACTION_IMPLICIT_RESUME",
    "aeromirror_implicit_resume_actions.fetch_add(1)",
    "aeromirror_implicit_resume_actions.load()",
    'implicit_resume=%',
    "implicit_resume_actions"
) "implicit resume has a durable counter and health-log field"

Write-Host "Native core source contracts passed for parsers, crypto, transport, SETUP, and renderers."

# Compile the exact production crypto.c and execute a bounded, positive-only
# NIST vector.  No malformed protocol, parser, or network inputs are used.
$originalPath = $env:PATH
$compilerDirectory = Split-Path -Parent $compiler
$env:PATH = $compilerDirectory + [IO.Path]::PathSeparator + $originalPath
$compilerInfo = & $compiler --version
Assert-True ($LASTEXITCODE -eq 0 -and @($compilerInfo).Count -gt 0) `
    "native compiler starts"
$compilerBanner = [string]@($compilerInfo)[0]

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "aeromirror-native-core-contracts-" + [Guid]::NewGuid().ToString("N"))
$executable = Join-Path $temporaryRoot "native-crypto-happy-path.exe"
$trustedHarnessSource = Join-Path $temporaryRoot `
    "native-trusted-store-parser.cpp"
$trustedHarnessExecutable = Join-Path $temporaryRoot `
    "native-trusted-store-parser.exe"
$logIsolationHarnessSource = Join-Path $temporaryRoot `
    "native-log-isolation.cpp"
$logIsolationHarnessExecutable = Join-Path $temporaryRoot `
    "native-log-isolation.exe"
$httpHeaderHarnessSource = Join-Path $temporaryRoot `
    "native-http-header-lookup.c"
$httpHeaderHarnessExecutable = Join-Path $temporaryRoot `
    "native-http-header-lookup.exe"
$httpRequestObject = Join-Path $temporaryRoot "http-request.o"
$httpHeaderHarnessObject = Join-Path $temporaryRoot "http-header-harness.o"
$llhttpApiObject = Join-Path $temporaryRoot "llhttp-api.o"
$llhttpHttpObject = Join-Path $temporaryRoot "llhttp-http.o"
$llhttpParserObject = Join-Path $temporaryRoot "llhttp-parser.o"
$hlsLanguageHarnessSource = Join-Path $temporaryRoot `
    "native-hls-language-parser.c"
$hlsLanguageHarnessExecutable = Join-Path $temporaryRoot `
    "native-hls-language-parser.exe"

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $httpHeaderHarnessText = @'
#include <stdio.h>
#include <string.h>

#include "http_request.h"

int main(void) {
    const char *chunks[] = {
        "PO",
        "ST /pair-setup HT",
        "TP/1.1\r\ncon",
        "tent-type: application/pairing+tlv8\r\ncS",
        "eQ: 7\r\nx-aPpLe-SeSsIoN-Id: session\r\n",
        "Content-Type-X: decoy\r\nContent-Length: 0\r\n\r\n"
    };
    http_request_t *request = http_request_init();
    if (!request) return 10;
    for (size_t i = 0; i < sizeof(chunks) / sizeof(chunks[0]); i++) {
        if (http_request_add_data(
                request, chunks[i], (int) strlen(chunks[i])) != 0) {
            http_request_destroy(request);
            return 11;
        }
    }
    if (!http_request_is_complete(request) ||
        http_request_has_error(request)) {
        http_request_destroy(request);
        return 12;
    }

    const char *content_type =
        http_request_get_header(request, "Content-Type");
    const char *content_type_upper =
        http_request_get_header(request, "CONTENT-TYPE");
    const char *cseq = http_request_get_header(request, "CSeq");
    const char *session =
        http_request_get_header(request, "X-Apple-Session-ID");
    const char *decoy = http_request_get_header(request, "Content-Type-X");
    if (!content_type || !content_type_upper || !cseq || !session || !decoy ||
        strcmp(content_type, "application/pairing+tlv8") ||
        strcmp(content_type_upper, content_type) || strcmp(cseq, "7") ||
        strcmp(session, "session") ||
        http_request_get_header(request, "Content-Typ") != NULL ||
        strcmp(decoy, "decoy")) {
        http_request_destroy(request);
        return 13;
    }

    http_request_destroy(request);
    puts("Native HTTP header lookup checks passed");
    return 0;
}
'@
    [IO.File]::WriteAllText(
        $httpHeaderHarnessSource,
        $httpHeaderHarnessText,
        [Text.UTF8Encoding]::new($false))
    & $compiler @(
        "-std=c11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        ("-I" + (Join-Path $libRoot "lib")),
        ("-I" + (Join-Path $libRoot "lib\llhttp")),
        "-c",
        $httpRequestSource,
        "-o",
        $httpRequestObject
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "exact production HTTP request implementation compiles cleanly"
    & $compiler @(
        "-std=c11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        ("-I" + (Join-Path $libRoot "lib")),
        ("-I" + (Join-Path $libRoot "lib\llhttp")),
        "-c",
        $httpHeaderHarnessSource,
        "-o",
        $httpHeaderHarnessObject
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "mixed-case HTTP header harness compiles cleanly"
    foreach ($generatedParserUnit in @(
        @{ Source = $llhttpApiSource; Object = $llhttpApiObject },
        @{ Source = $llhttpHttpSource; Object = $llhttpHttpObject },
        @{ Source = $llhttpParserSource; Object = $llhttpParserObject }
    )) {
        & $compiler @(
            "-std=c11",
            "-O2",
            "-w",
            ("-I" + (Join-Path $libRoot "lib")),
            ("-I" + (Join-Path $libRoot "lib\llhttp")),
            "-c",
            $generatedParserUnit.Source,
            "-o",
            $generatedParserUnit.Object
        )
        Assert-True ($LASTEXITCODE -eq 0) `
            "vendored generated llhttp unit compiles for the HTTP lookup harness"
    }
    & $compiler @(
        $httpRequestObject,
        $httpHeaderHarnessObject,
        $llhttpApiObject,
        $llhttpHttpObject,
        $llhttpParserObject,
        "-o",
        $httpHeaderHarnessExecutable
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "production HTTP parser and mixed-case header harness link"
    $httpHeaderHarnessOutput = & $httpHeaderHarnessExecutable
    Assert-True ($LASTEXITCODE -eq 0 -and
        ([string]::Join("`n", @($httpHeaderHarnessOutput))).Contains(
            "Native HTTP header lookup checks passed")) `
        "fragmented lowercase and mixed-case HTTP field names resolve exactly"

    $hlsLanguageHarnessText = @'
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

'@ + $hlsLanguageParserSlice + @'

int main(void) {
    const char single[] =
        "#EXTM3U\n"
        "#EXT-X-MEDIA:URI=\"a\",DEFAULT=YES,NAME=\"English\","
        "LANGUAGE=\"en\",YT-EXT-AUDIO-CONTENT-ID=\"1\"\n"
        "#END\n";
    int slices = -1;
    int language_count = -1;
    language_t *parsed = master_playlist_process_language(
        single, &slices, &language_count);
    if (!parsed || slices != 3 || language_count != 1 ||
        strcmp(parsed[1].code, "en") ||
        strcmp(parsed[1].name, "English")) {
        language_slices_free(parsed, parsed ? slices - 2 : 0);
        return 10;
    }
    language_slices_free(parsed, slices - 2);

    const char repeated[] =
        "#EXTM3U\n"
        "#EXT-X-MEDIA:URI=\"a1\",DEFAULT=YES,NAME=\"English\",LANGUAGE=\"en\",YT-EXT-AUDIO-CONTENT-ID=\"1\"\n"
        "#EXT-X-MEDIA:URI=\"a2\",DEFAULT=NO,NAME=\"French\",LANGUAGE=\"fr\",YT-EXT-AUDIO-CONTENT-ID=\"2\"\n"
        "#EXT-X-MEDIA:URI=\"a3\",DEFAULT=YES,NAME=\"English\",LANGUAGE=\"en\",YT-EXT-AUDIO-CONTENT-ID=\"3\"\n"
        "#EXT-X-MEDIA:URI=\"a4\",DEFAULT=NO,NAME=\"French\",LANGUAGE=\"fr\",YT-EXT-AUDIO-CONTENT-ID=\"4\"\n"
        "#END\n";
    slices = -1;
    language_count = -1;
    parsed = master_playlist_process_language(
        repeated, &slices, &language_count);
    if (!parsed || slices != 6 || language_count != 2 ||
        strcmp(parsed[4].code, "fr")) {
        language_slices_free(parsed, parsed ? slices - 2 : 0);
        return 11;
    }
    language_slices_free(parsed, slices - 2);

    puts("Native HLS language parser checks passed");
    return 0;
}
'@
    [IO.File]::WriteAllText(
        $hlsLanguageHarnessSource,
        $hlsLanguageHarnessText,
        [Text.UTF8Encoding]::new($false))
    & $compiler @(
        "-std=c11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        $hlsLanguageHarnessSource,
        "-o",
        $hlsLanguageHarnessExecutable
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "exact production HLS language parser compiles cleanly"
    $hlsLanguageHarnessOutput = & $hlsLanguageHarnessExecutable
    Assert-True ($LASTEXITCODE -eq 0 -and
        ([string]::Join("`n", @($hlsLanguageHarnessOutput))).Contains(
            "Native HLS language parser checks passed")) `
        "HLS language parser accepts valid one-language and repeated-language playlists"

    $trustedHarnessText = @'
#include <cstdio>
#include <cstring>
#include <string>

'@ + $trustedParserSlice + @'

int main() {
    std::string key(43, 'A');
    key += "=";
    std::string parsed;
    if (!aeromirror_is_canonical_trusted_key(key.c_str())) return 10;
    if (!aeromirror_parse_trusted_client_record(key + ",", &parsed) ||
        parsed != key) return 11;
    if (!aeromirror_parse_trusted_client_record(
            key + ",legacy-device,legacy-name", &parsed) ||
        parsed != key) return 12;

    const std::string invalid[] = {
        "",
        "short",
        key,
        std::string(43, 'A') + "?,",
        std::string(42, 'A') + "==,",
        key + ";",
        key + ",name\rcontrol",
        key + ",name\n" + key + ","
    };
    for (const std::string &record : invalid) {
        parsed = "unchanged";
        if (aeromirror_parse_trusted_client_record(record, &parsed)) {
            return 20;
        }
    }
    if (aeromirror_parse_trusted_client_record(key + ",", nullptr)) {
        return 21;
    }
    std::puts("Native production trusted-store parser checks passed");
    return 0;
}
'@
    [IO.File]::WriteAllText(
        $trustedHarnessSource,
        $trustedHarnessText,
        [Text.UTF8Encoding]::new($false))
    & $cxxCompiler @(
        "-std=c++11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        $trustedHarnessSource,
        "-o",
        $trustedHarnessExecutable
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "exact production trusted-store parser compiles cleanly"
    $trustedHarnessOutput = & $trustedHarnessExecutable
    Assert-True ($LASTEXITCODE -eq 0 -and
        ([string]::Join("`n", @($trustedHarnessOutput))).Contains(
            "Native production trusted-store parser checks passed")) `
        "exact production trusted-store parser rejects short, malformed, and injected records"

    $logIsolationHarnessText = @'
#include <cstdio>
#include <cstring>

#include "aeromirror_log_protocol.h"

int main() {
    char ordinary[] =
        "AEROMIRROR_DNSSD_READY\r\n"
        "aErOmIrRoR_PAIRING_STATE request=7 state=trusted\t\x7f";
    aeromirror_sanitize_ordinary_log(ordinary);
    if (std::strchr(ordinary, '\r') || std::strchr(ordinary, '\n') ||
        std::strchr(ordinary, '\t') || std::strchr(ordinary, '\x7f')) {
        return 10;
    }
    if (std::strstr(ordinary, "AEROMIRROR_") ||
        std::strcmp(
            ordinary,
            "AEROMIRROR-DNSSD_READY  "
            "aErOmIrRoR-PAIRING_STATE request=7 state=trusted  ") != 0) {
        return 11;
    }

    char genuine[] =
        "AEROMIRROR_PAIRING_STATE request=7 state=trusted\r\n";
    aeromirror_sanitize_control_bytes(genuine);
    if (!aeromirror_is_protocol_marker(genuine) ||
        std::strchr(genuine, '\r') || std::strchr(genuine, '\n') ||
        std::strcmp(
            genuine,
            "AEROMIRROR_PAIRING_STATE request=7 state=trusted  ") != 0) {
        return 12;
    }
    if (aeromirror_is_protocol_marker(
            "ordinary AEROMIRROR_DNSSD_READY")) {
        return 13;
    }

    FILE *capture = std::tmpfile();
    if (!capture) return 14;
    if (!aeromirror_ordinary_output(
            capture, "peer=%s",
            "remote\r\naErOmIrRoR_DNSSD_READY\nnext")) {
        return 15;
    }
    std::rewind(capture);
    char captured[256] = {0};
    size_t captured_length =
        std::fread(captured, 1, sizeof(captured) - 1, capture);
    std::fclose(capture);
    if (captured_length == 0 ||
        std::strcmp(
            captured,
            "peer=remote  aErOmIrRoR-DNSSD_READY next\n") != 0 ||
        std::strstr(captured, "\nAEROMIRROR_") ||
        std::strstr(captured, "\naErOmIrRoR_")) {
        return 16;
    }

    std::puts("Native ordinary-log marker isolation checks passed");
    return 0;
}
'@
    [IO.File]::WriteAllText(
        $logIsolationHarnessSource,
        $logIsolationHarnessText,
        [Text.UTF8Encoding]::new($false))
    & $cxxCompiler @(
        "-std=c++11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        ("-I" + $libRoot),
        $logIsolationHarnessSource,
        "-o",
        $logIsolationHarnessExecutable
    )
    Assert-True ($LASTEXITCODE -eq 0) `
        "exact production ordinary-log sanitizer compiles cleanly"
    $logIsolationHarnessOutput = & $logIsolationHarnessExecutable
    Assert-True ($LASTEXITCODE -eq 0 -and
        ([string]::Join("`n", @($logIsolationHarnessOutput))).Contains(
            "Native ordinary-log marker isolation checks passed")) `
        "ordinary logs reject exact marker and CR/LF control-line injection while genuine markers remain valid"

    $arguments = @(
        "-std=c11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        ("-I" + (Join-Path $libRoot "lib")),
        ("-I" + $opensslInclude),
        $cryptoSource,
        $harnessSource,
        $cryptoImportLibrary,
        "-o",
        $executable
    )
    & $compiler @arguments
    Assert-True ($LASTEXITCODE -eq 0) `
        "exact production crypto.c and happy-path harness compile cleanly"
    Assert-True (Test-Path -LiteralPath $executable -PathType Leaf) `
        "native crypto happy-path executable is produced"

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $executable
    $start.WorkingDirectory = $temporaryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        Assert-True ($process.Start()) "native crypto happy-path harness starts"
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $completed = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $completed) {
            try { $process.Kill() } catch {}
            try { $process.WaitForExit(5000) | Out-Null } catch {}
            throw "FAILED: native crypto harness exceeded $TimeoutSeconds seconds"
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            Write-Host $stdout.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Host $stderr.TrimEnd()
        }
        Assert-True ($process.ExitCode -eq 0) `
            "native crypto happy-path harness exits successfully"
        Assert-True ($stdout.Contains(
            "Native production crypto happy-path checks passed")) `
            "native crypto harness emits its completion marker"
    }
    finally {
        $process.Dispose()
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
    $env:PATH = $originalPath
}

Write-Host (
    "Native core contracts passed against exact production crypto using " +
    $compilerBanner + ".")
exit 0

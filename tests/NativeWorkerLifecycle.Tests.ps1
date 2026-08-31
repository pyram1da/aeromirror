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

function Assert-MatchCount(
    [string]$Text,
    [string]$Pattern,
    [int]$Expected,
    [string]$Message
) {
    $actual = [regex]::Matches(
        $Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Multiline
    ).Count
    Assert-True ($actual -eq $Expected) (
        "$Message (expected $Expected, found $actual)")
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
        Assert-True ($index -ge 0) "$Message (missing or out of order: $fragment)"
        $offset = $index + $fragment.Length
    }
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
$productionSource = Join-Path $libRoot "lib\worker_lifecycle.c"
$productionHeader = Join-Path $libRoot "lib\worker_lifecycle.h"
$productionThreads = Join-Path $libRoot "lib\threads.h"
$productionCMake = Join-Path $libRoot "lib\CMakeLists.txt"
$mirrorSource = Join-Path $libRoot "lib\raop_rtp_mirror.c"
$rtpSource = Join-Path $libRoot "lib\raop_rtp.c"
$ntpSource = Join-Path $libRoot "lib\raop_ntp.c"
$httpSource = Join-Path $libRoot "lib\httpd.c"
$netutilsSource = Join-Path $libRoot "lib\netutils.c"
$netutilsHeader = Join-Path $libRoot "lib\netutils.h"
$harnessSource = Join-Path $PSScriptRoot "NativeWorkerLifecycleHarness.c"

foreach ($path in @(
    $productionSource,
    $productionHeader,
    $productionThreads,
    $productionCMake,
    $mirrorSource,
    $rtpSource,
    $ntpSource,
    $httpSource,
    $netutilsSource,
    $netutilsHeader,
    $harnessSource
)) {
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) `
        "required lifecycle harness input exists: $path"
}

$cmakeText = Get-Content -LiteralPath $productionCMake -Raw
Assert-True ($cmakeText -match '(?m)^\s*aux_source_directory\(\.\s+play_src\)\s*$') `
    "lib CMake discovers worker_lifecycle.c after every fresh configure"
Assert-True ($cmakeText -match '(?ms)add_library\(\s*airplay\s+STATIC\s+\$\{DIR_SRCS\}') `
    "the discovered lifecycle helper is compiled into production airplay"

$consumers = @(
    @{ Name = "mirror"; Path = $mirrorSource; Prefix = "raop_rtp_mirror" },
    @{ Name = "audio RTP"; Path = $rtpSource; Prefix = "raop_rtp" },
    @{ Name = "NTP"; Path = $ntpSource; Prefix = "raop_ntp" },
    @{ Name = "HTTP"; Path = $httpSource; Prefix = "httpd" }
)

foreach ($consumer in $consumers) {
    $text = Get-Content -LiteralPath $consumer.Path -Raw
    $prefix = [regex]::Escape($consumer.Prefix)
    Assert-MatchCount $text '#include\s+"worker_lifecycle\.h"' 1 `
        "$($consumer.Name) includes the shared lifecycle API exactly once"
    Assert-MatchCount $text '\bworker_lifecycle_t\s+lifecycle\s*;' 1 `
        "$($consumer.Name) owns exactly one shared lifecycle state"
    Assert-MatchCount $text 'worker_lifecycle_init\s*\(' 1 `
        "$($consumer.Name) initializes the shared lifecycle exactly once"
    Assert-MatchCount $text 'worker_lifecycle_start_thread_locked\s*\(' 1 `
        "$($consumer.Name) starts only through the shared helper"
    Assert-MatchCount $text 'if\s*\(\s*start_result\s*!=\s*1\s*\)' 1 `
        "$($consumer.Name) treats only helper result 1 as a successful start"
    Assert-MatchCount $text 'worker_lifecycle_mark_exited\s*\(' 1 `
        "$($consumer.Name) preserves join debt on every natural worker tail"
    Assert-MatchCount $text 'worker_lifecycle_stop\s*\(' 1 `
        "$($consumer.Name) stops only through the shared helper"
    Assert-MatchCount $text 'worker_lifecycle_is_joined\s*\(' 1 `
        "$($consumer.Name) refuses destruction while join debt remains"
    Assert-MatchCount $text 'worker_lifecycle_destroy\s*\(' 1 `
        "$($consumer.Name) destroys the shared lifecycle exactly once"
    Assert-MatchCount $text '\b(?:THREAD_CREATE|THREAD_JOIN)\s*\(' 0 `
        "$($consumer.Name) cannot bypass shared create/join ownership"
    Assert-MatchCount $text '\brun_mutex\b' 0 `
        "$($consumer.Name) has no legacy lifecycle mutex"
    Assert-InOrder $text @(
        "$($consumer.Prefix)_stop(",
        "worker_lifecycle_is_joined(",
        "worker_lifecycle_destroy("
    ) "$($consumer.Name) stop/join-check/destroy order is explicit"
}

$mirrorText = Get-Content -LiteralPath $mirrorSource -Raw
Assert-MatchCount $mirrorText 'netutils_set_blocking\s*\(\s*stream_fd\s*\)' 1 `
    "mirror explicitly restores blocking mode on the accepted stream"
Assert-InOrder $mirrorText @(
    "stream_fd = accept(",
    "netutils_set_blocking(stream_fd)",
    "raop_rtp_mirror->stream_fd = stream_fd",
    "DWORD recv_timeout = 5",
    "setsockopt(stream_fd, SOL_SOCKET, SO_RCVTIMEO"
) "mirror restores blocking mode before publishing and timing the stream"
Assert-MatchCount $mirrorText '\bDWORD\s+recv_timeout\s*=\s*5\s*;' 1 `
    "mirror uses the Windows millisecond timeout value type"
Assert-MatchCount $mirrorText 'SOCKET_ERRORNAME\(ETIMEDOUT\)' 2 `
    "mirror retries a fragmented header and payload after timed receive"
Assert-MatchCount $mirrorText 'raop_rtp_mirror_stop\s*\(' 2 `
    "mirror stop appears only as its definition and destroy call"
Assert-MatchCount $mirrorText 'raop_rtp_mirror_stop\s*\(\s*raop_rtp_mirror\s*\)\s*;' 1 `
    "mirror worker never invokes public stop on itself"

$httpText = Get-Content -LiteralPath $httpSource -Raw
Assert-MatchCount $httpText 'netutils_set_blocking\s*\(\s*fd\s*\)' 1 `
    "HTTP explicitly restores blocking mode on each accepted stream"
Assert-InOrder $httpText @(
    "fd = accept(",
    "netutils_set_blocking(fd)",
    "httpd_add_connection(httpd, fd",
    "DWORD io_timeout = io_timeout_msec",
    "setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO",
    "setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO"
) "HTTP restores blocking mode before publishing and timing a connection"
Assert-MatchCount $httpText 'SOCKET_ERRORNAME\(ETIMEDOUT\)' 3 `
    "HTTP retries prefix receive, normal receive, and response send timeouts"
Assert-True ($httpText -match '(?s)while\s*\(\s*readstart\s*<\s*8\s*\).*?' +
    'worker_lifecycle_should_run\s*\(') `
    "HTTP prefix collection checks lifecycle state"
Assert-True ($httpText -match '(?s)while\s*\(\s*written\s*<\s*datalen\s*\).*?' +
    'worker_lifecycle_should_run\s*\(') `
    "HTTP response send checks lifecycle state"
Assert-True ($httpText -match '(?s)received reversed HTTP response from client' +
    '.*?on socket %d \(%d bytes\).*?' +
    'connection->socket_fd\s*,\s*recv_datalen') `
    "reverse HTTP diagnostics retain content-free byte counts"
Assert-True ($httpText -notmatch '%\.\*s[\s\S]*?\(const char \*\)\s*buffer') `
    "reverse HTTP diagnostics never write client response bytes"

$netutilsText = Get-Content -LiteralPath $netutilsSource -Raw
$netutilsHeaderText = Get-Content -LiteralPath $netutilsHeader -Raw
Assert-MatchCount $netutilsHeaderText `
    'int\s+netutils_set_blocking\s*\(\s*int\s+socket_fd\s*\)\s*;' 1 `
    "blocking-mode restoration is part of the production netutils API"
Assert-True ($netutilsText -match '(?s)netutils_set_blocking\s*\(' +
    '.*?u_long\s+nonblocking\s*=\s*0\s*;' +
    '.*?IOCTLSOCKET\s*\(\s*socket_fd\s*,\s*FIONBIO') `
    "Windows accepted sockets are restored with FIONBIO zero"

Write-Host "Production bindings passed for mirror, audio RTP, NTP, and HTTP."

$originalPath = $env:PATH
$compilerDirectory = Split-Path -Parent $compiler
$env:PATH = $compilerDirectory + [IO.Path]::PathSeparator + $originalPath
$compilerInfo = & $compiler --version
Assert-True ($LASTEXITCODE -eq 0 -and
    @($compilerInfo).Count -gt 0) "pinned native compiler starts"
$compilerBanner = [string]@($compilerInfo)[0]
Assert-True ($compilerBanner -match
    '^gcc\.exe .* 16\.1\.0$') `
    "native harness uses the reviewed GCC 16.1.0 toolchain"

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "aeromirror-worker-lifecycle-" + [Guid]::NewGuid().ToString("N"))
$executable = Join-Path $temporaryRoot "worker-lifecycle-harness.exe"

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $arguments = @(
        "-std=c11",
        "-O2",
        "-Wall",
        "-Wextra",
        "-Werror",
        "-Wpedantic",
        "-pthread",
        ("-I" + (Join-Path $libRoot "lib")),
        $productionSource,
        $harnessSource,
        "-lws2_32",
        "-o",
        $executable
    )
    & $compiler @arguments
    Assert-True ($LASTEXITCODE -eq 0) `
        "production worker_lifecycle.c and executable harness compile cleanly"
    Assert-True (Test-Path -LiteralPath $executable -PathType Leaf) `
        "native lifecycle harness executable is produced"

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
        Assert-True ($process.Start()) "native lifecycle harness starts"
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $completed = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $completed) {
            try { $process.Kill() } catch {}
            try { $process.WaitForExit(5000) | Out-Null } catch {}
            $timeoutStdout = $stdoutTask.GetAwaiter().GetResult()
            $timeoutStderr = $stderrTask.GetAwaiter().GetResult()
            if (-not [string]::IsNullOrWhiteSpace($timeoutStdout)) {
                Write-Host $timeoutStdout.TrimEnd()
            }
            if (-not [string]::IsNullOrWhiteSpace($timeoutStderr)) {
                Write-Host $timeoutStderr.TrimEnd()
            }
            throw (
                "FAILED: native lifecycle harness exceeded " +
                "$TimeoutSeconds seconds")
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
            "native lifecycle harness exits successfully"
        Assert-True ($stdout.Contains(
            "Native worker lifecycle executable checks passed (8 scenarios).")) `
            "native lifecycle harness emits its completion marker"
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
    "Native worker lifecycle checks passed against exact production helper " +
    "$productionSource using $compilerBanner.")
exit 0

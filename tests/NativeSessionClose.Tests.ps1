param(
    [Parameter(Mandatory = $true)][string]$LibUxPlayRoot,
    [string]$CompilerPath = "C:\Users\ivang\Documents\Codex\msys64\ucrt64\bin\gcc.exe"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$lib = Join-Path (Resolve-Path -LiteralPath $LibUxPlayRoot).Path "lib"
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$compilerBin = Split-Path -Parent $compiler
$staging = Join-Path ([IO.Path]::GetTempPath()) "AeroMirror-session-close-tests"
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$harness = Join-Path $staging "NativeSessionCloseHarness.c"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "NativeSessionCloseHarness.c") -Destination $harness
$exe = Join-Path $staging "NativeSessionCloseHarness.exe"
$sources = @("worker_lifecycle.c", "http_request.c", "http_response.c",
    "logger.c", "utils.c", "compat.c", "llhttp\llhttp.c", "llhttp\api.c", "llhttp\http.c") |
    ForEach-Object { Join-Path $lib $_ }
$priorPath = $env:PATH
try {
    $env:PATH = "$compilerBin;$priorPath"
    & $compiler -std=gnu11 -O2 -ffunction-sections -fdata-sections -Wall -Wextra -Wno-unused-parameter `
        -I $lib -I (Join-Path $lib "llhttp") $harness @sources `
        "-Wl,--gc-sections" -lws2_32 -liphlpapi -lpthread -o $exe
    if ($LASTEXITCODE -ne 0) { throw "Native session-close harness did not compile." }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $exe
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw "Own session-close harness exceeded 30 seconds."
    }
    $process.WaitForExit()
    Write-Output $stdout.Result
    Write-Output $stderr.Result
    if ($process.ExitCode -ne 0) { throw "Native session-close harness failed: $($process.ExitCode)" }
}
finally { $env:PATH = $priorPath }

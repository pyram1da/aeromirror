param(
    [Parameter(Mandatory = $true)][string]$UpstreamRoot,
    [Parameter(Mandatory = $true)][string]$Qt610Prefix,
    [Parameter(Mandatory = $true)][string]$MsysRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$header = Join-Path (Resolve-Path -LiteralPath $UpstreamRoot).Path "src\mainwindow.h"
$qt = (Resolve-Path -LiteralPath $Qt610Prefix).Path
$compilerBin = Join-Path (Resolve-Path -LiteralPath $MsysRoot).Path "ucrt64\bin"
$provenance = Get-Content (Join-Path $projectRoot "native-core\source-provenance.json") -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $header -Algorithm SHA256).Hash -ne
    $provenance.patchedSources.'src/mainwindow.h') {
    throw "The harness must compile the exact reviewed production header."
}
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "aeromirror-video-surface-" + [Guid]::NewGuid().ToString("N"))
$originalPath = $env:PATH
$originalPluginPath = $env:QT_PLUGIN_PATH
try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Copy-Item -LiteralPath $header -Destination $temporaryRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "NativeVideoSurfaceHarness.cpp") -Destination $temporaryRoot
    $env:PATH = (Join-Path $qt "bin") + ";$compilerBin;$originalPath"
    $env:QT_PLUGIN_PATH = Join-Path $qt "share\qt6\plugins"
    $executable = Join-Path $temporaryRoot "video-surface-test.exe"
    $arguments = @("-std=c++17", "-O2", "-Wall", "-Wextra", "-Werror")
    foreach ($include in @("include\qt6", "include\qt6\QtCore", "include\qt6\QtGui", "include\qt6\QtWidgets")) {
        $arguments += @("-isystem", (Join-Path $qt $include))
    }
    $arguments += @((Join-Path $temporaryRoot "NativeVideoSurfaceHarness.cpp"),
        ("-L" + (Join-Path $qt "lib")), "-lQt6Widgets", "-lQt6Gui", "-lQt6Core", "-o", $executable)
    & (Join-Path $compilerBin "g++.exe") @arguments
    if ($LASTEXITCODE -ne 0) { throw "Native surface harness compilation failed." }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $executable
    $start.Arguments = "-platform windows"
    $start.WorkingDirectory = $temporaryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw "Native surface harness did not start." }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill()
            $process.WaitForExit()
            throw "Native surface harness timed out."
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        Write-Host $stdout.TrimEnd()
        if ($stderr) { Write-Host $stderr.TrimEnd() }
        if ($process.ExitCode -ne 0 -or $stdout -notmatch "Native video surface checks passed") {
            throw "Native surface contract failed on Windows."
        }
    }
    finally { $process.Dispose() }
}
finally {
    $env:PATH = $originalPath
    $env:QT_PLUGIN_PATH = $originalPluginPath
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $target = [IO.Path]::GetFullPath($temporaryRoot)
    if (-not $target.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -notmatch '^aeromirror-video-surface-[0-9a-f]{32}$') {
        throw "Unsafe temporary test cleanup path."
    }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}

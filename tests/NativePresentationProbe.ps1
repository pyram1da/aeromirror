param(
    [Parameter(Mandatory = $true)][string]$UpstreamRoot,
    [Parameter(Mandatory = $true)][string]$Qt610Prefix,
    [Parameter(Mandatory = $true)][string]$MsysRoot,
    [Parameter(Mandatory = $true)][string]$RuntimeRoot,
    [switch]$ExerciseFullscreen
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) 'aeromirror-presentation-audit-028'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $UpstreamRoot 'src\mainwindow.h') -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NativePresentationProbe.cpp') -Destination $testRoot
$ucrt = Join-Path $MsysRoot 'ucrt64'
$testExe = Join-Path $testRoot 'presentation-test.exe'
$includes = @('include\qt6', 'include\qt6\QtCore', 'include\qt6\QtGui', 'include\qt6\QtWidgets')
$arguments = @('-std=c++17', '-O2', '-Wall', '-Wextra', '-Werror')
foreach ($include in $includes) { $arguments += @('-isystem', (Join-Path $Qt610Prefix $include)) }
foreach ($include in @('include\gstreamer-1.0', 'include\glib-2.0', 'lib\glib-2.0\include')) {
    $arguments += @('-isystem', (Join-Path $ucrt $include))
}
$arguments += @((Join-Path $testRoot 'NativePresentationProbe.cpp'),
    ('-L' + (Join-Path $Qt610Prefix 'lib')), ('-L' + (Join-Path $ucrt 'lib')),
    '-lQt6Widgets', '-lQt6Gui', '-lQt6Core', '-lgstapp-1.0', '-lgstvideo-1.0',
    '-lgstreamer-1.0', '-lgobject-2.0', '-lglib-2.0', '-lgdi32', '-o', $testExe)
$savedPath = $env:PATH
try {
    $env:PATH = (Join-Path $ucrt 'bin') + ';' + $savedPath
    & (Join-Path $ucrt 'bin\g++.exe') @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Native presentation compilation failed.' }
    $runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $testExe
    if ($ExerciseFullscreen) { $start.Arguments = '--exercise-fullscreen' }
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['PATH'] = $runtime + ';' + (Join-Path $Qt610Prefix 'bin') + ';' + $env:WINDIR + '\System32'
    $start.EnvironmentVariables['QT_PLUGIN_PATH'] = $runtime
    $start.EnvironmentVariables['GST_PLUGIN_PATH_1_0'] = Join-Path $runtime 'lib\gstreamer-1.0'
    $start.EnvironmentVariables['GST_PLUGIN_SYSTEM_PATH_1_0'] = ''
    $start.EnvironmentVariables['GST_PLUGIN_SCANNER_1_0'] = Join-Path $runtime 'libexec\gstreamer-1.0\gst-plugin-scanner.exe'
    $start.EnvironmentVariables['GST_REGISTRY'] = Join-Path $testRoot 'registry.bin'
    $start.EnvironmentVariables['GST_DEBUG'] = 'd3d11*:3'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Presentation test did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) {
            $process.Kill()
            $process.WaitForExit()
            Write-Output $stdout.GetAwaiter().GetResult()
            Write-Output $stderr.GetAwaiter().GetResult()
            throw 'Presentation test timed out.'
        }
        Write-Output $stdout.GetAwaiter().GetResult()
        Write-Output $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Presentation test exit $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
}
finally { $env:PATH = $savedPath }

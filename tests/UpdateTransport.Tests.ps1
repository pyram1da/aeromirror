param([string]$AssemblyPath = '', [switch]$CheckGitHub)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot 'artifacts\Release\AeroMirror.exe'
}
$testRoot = Join-Path $projectRoot 'artifacts\tests\update-transport'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$testExe = Join-Path $testRoot 'UpdateTransport.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /warnaserror+ "/out:$testExe" `
    /reference:System.dll /reference:System.Core.dll `
    (Join-Path $PSScriptRoot 'UpdateTransportHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Update transport test compilation failed.' }
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$scratchName = [Guid]::NewGuid().ToString('N')
$scratch = Join-Path $temporaryRoot $scratchName
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $testArguments = @([IO.Path]::GetFullPath($AssemblyPath), $scratch)
    if ($CheckGitHub) { $testArguments += '--check-github' }
    & $testExe @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Update transport behavior checks failed.' }
}
finally {
    $owned = Get-Item -LiteralPath $scratch -ErrorAction SilentlyContinue
    if ($null -ne $owned) {
        if ($owned.Parent.FullName.TrimEnd('\') -ine $temporaryRoot -or
            $owned.Name -cne $scratchName -or
            ($owned.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Refusing cleanup outside the exact owned temporary test directory.'
        }
        Remove-Item -LiteralPath $owned.FullName -Recurse -Force
    }
}

# Wiring checks supplement (not replace) executable transport/lifetime tests.
$manual = Get-Content -LiteralPath (Join-Path $projectRoot 'src\UI\SettingsForm.cs') -Raw
$automatic = Get-Content -LiteralPath (Join-Path $projectRoot 'src\Receiver\ReceiverContext.Updates.cs') -Raw
$shutdown = Get-Content -LiteralPath (Join-Path $projectRoot 'src\Receiver\ReceiverContext.Diagnostics.cs') -Raw
foreach ($source in @($manual, $automatic)) {
    if (-not $source.Contains('UpdateService.Check(operation.Token)') -or
        -not $source.Contains('operation.Dispose();')) {
        throw 'Update work must pass the lifetime token and complete its ownership.'
    }
}
if (-not $manual.Contains('Disposed += delegate { StopUpdateWork(); };') -or
    -not $manual.Contains('UpdateService.DownloadAndVerify(selectedUpdate, operation.Token)') -or
    -not $automatic.Contains('automaticUpdateWork.CancelPending();') -or
    -not $automatic.Contains('automaticUpdateWork.Dispose();') -or
    -not $automatic.Contains('UpdateService.DownloadAndVerify(info, operation.Token)') -or
    -not $shutdown.Contains('form.StopUpdateWork();')) {
    throw 'Actual form/application close and opt-out must cancel their own update work.'
}
Write-Host 'PASS: manual, automatic, opt-out and shutdown cancellation wiring.'

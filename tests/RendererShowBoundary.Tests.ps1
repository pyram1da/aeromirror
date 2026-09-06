param([string]$AssemblyPath = '', [switch]$ExpectBug)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $projectRoot 'artifacts\Release\AeroMirror.exe' }
$testRoot = Join-Path $projectRoot 'artifacts\tests\renderer-show-boundary'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$testExe = Join-Path $testRoot 'RendererShowBoundary.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /warnaserror+ "/out:$testExe" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    (Join-Path $PSScriptRoot 'RendererShowBoundaryHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Renderer boundary test compilation failed.' }
if ($ExpectBug) { & $testExe $AssemblyPath --expect-bug }
else { & $testExe $AssemblyPath }
if ($LASTEXITCODE -ne 0) { throw 'Renderer show/placement boundary checks failed.' }

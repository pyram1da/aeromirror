$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot 'artifacts\tests\control-work-queue'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$testExe = Join-Path $testRoot 'ControlWorkQueue.Tests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /warnaserror+ "/out:$testExe" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll /reference:System.Security.dll /reference:System.ServiceProcess.dll `
    /reference:System.Web.Extensions.dll `
    (Join-Path $projectRoot 'src\Interop\NativeMethods.cs') `
    (Join-Path $projectRoot 'src\UI\ControlWorkQueue.cs') `
    (Join-Path $PSScriptRoot 'ControlWorkQueueHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'ControlWorkQueue test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'ControlWorkQueue behavior checks failed.' }

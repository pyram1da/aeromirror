param(
    [string]$PortableZip = "",
    [string]$Version = "0.12.20"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectRoot "installer\AirPlayReceiverSetup.cs"
$icon = Join-Path $projectRoot "assets\AirPlayReceiver.ico"
$manifest = Join-Path $projectRoot "app.manifest"
$provenancePath = Join-Path $projectRoot "native-core\source-provenance.json"
$outputFolder = Join-Path $projectRoot "artifacts\installer"
$output = Join-Path $outputFolder ("AeroMirror-Setup-" + $Version + ".exe")
$uninstaller = Join-Path $outputFolder "Uninstall.exe"
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function Get-PeMachine([string]$Path) {
    $stream = [IO.File]::Open(
        $Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $reader = [IO.BinaryReader]::new($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) {
            throw "Not a PE executable: $Path"
        }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadUInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Invalid PE signature: $Path"
        }
        return $reader.ReadUInt16()
    }
    finally {
        $stream.Dispose()
    }
}

function Get-Sha256Lower([string]$Path) {
    return (
        Get-FileHash -Algorithm SHA256 -LiteralPath $Path
    ).Hash.ToLowerInvariant()
}

function Assert-HashMapMatches(
    [object]$Actual,
    [object]$Expected,
    [string]$Description
) {
    if ($null -eq $Actual -or $null -eq $Expected) {
        throw "$Description is missing."
    }
    $expectedProperties = @($Expected.PSObject.Properties)
    $actualProperties = @($Actual.PSObject.Properties)
    if ($actualProperties.Count -ne $expectedProperties.Count) {
        throw "$Description does not match source-provenance.json."
    }
    foreach ($property in $expectedProperties) {
        $actualProperty = $Actual.PSObject.Properties[$property.Name]
        if ($null -eq $actualProperty -or
            ([string]$actualProperty.Value) -ne
                ([string]$property.Value)) {
            throw (
                "$Description does not match source-provenance.json at " +
                $property.Name + ".")
        }
    }
}

if (-not $PortableZip) {
    $PortableZip = Join-Path $projectRoot (
        "artifacts\AeroMirror-review-payload-x64-" + $Version + ".zip")
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use the numeric MAJOR.MINOR.PATCH format."
}
if (-not (Test-Path $compiler)) {
    $compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $compiler)) {
    throw "The built-in .NET Framework C# compiler was not found."
}
if (-not (Test-Path $PortableZip)) {
    throw "Portable payload was not found: $PortableZip"
}
if (-not (Test-Path $icon)) {
    throw "Application icon was not found: $icon"
}
if (-not (Test-Path $manifest)) {
    throw "Application manifest was not found: $manifest"
}
if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "Native source provenance was not found: $provenancePath"
}
$provenance = Get-Content -LiteralPath $provenancePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$provenanceHash = Get-Sha256Lower -Path $provenancePath
$versionParts = @($Version.Split('.') | ForEach-Object { [int]$_ })
$installerSource = Get-Content -LiteralPath $source -Raw -Encoding UTF8
$requiredVersionLiterals = @(
    ('[assembly: AssemblyVersion("' + $Version + '.0")]'),
    ('[assembly: AssemblyFileVersion("' + $Version + '.0")]'),
    ('SetupVersion = new Version(' + ($versionParts -join ', ') + ')'),
    '"AeroMirror-Setup/" + SetupForm.SetupVersion.ToString(3)',
    '"DisplayVersion", SetupForm.SetupVersion.ToString(3)'
)
foreach ($literal in $requiredVersionLiterals) {
    if (-not $installerSource.Contains($literal)) {
        throw (
            "Installer source version does not match requested release " +
            "$Version. Missing literal: $literal")
    }
}
if ([regex]::IsMatch($installerSource, 'WaitForExit\s*\(\s*\)')) {
    throw "Installer process shutdown must not contain an unbounded WaitForExit()."
}
if (-not $installerSource.Contains(
    "bool detached = EnsureCurrentDirectoryOutsideInstallTree(")) {
    throw "Installer must detach its working directory before update handling."
}

New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null
$validationRoot = Join-Path $outputFolder (
    "payload-check-" + [Guid]::NewGuid().ToString("N"))
try {
    $allowedEntries = @(
        "AeroMirror/AeroMirror.exe",
        "AeroMirror/CHANGELOG.md",
        "AeroMirror/CONTRIBUTING.md",
        "AeroMirror/LICENSE",
        "AeroMirror/README.md",
        "AeroMirror/SECURITY.md",
        "AeroMirror/THIRD_PARTY_NOTICES.md",
        "AeroMirror/update-repository.txt",
        "AeroMirror/core/uxplay-windows.exe",
        "AeroMirror/core/resources/build-manifest.json",
        "AeroMirror/core/resources/runtime-delivery.json",
        "AeroMirror/core/resources/source-provenance.json",
        "AeroMirror/docs/TROUBLESHOOTING.md"
    )
    $archive = [IO.Compression.ZipFile]::OpenRead(
        [IO.Path]::GetFullPath($PortableZip))
    try {
        $actualEntries = @(
            $archive.Entries | ForEach-Object {
                $_.FullName.Replace('\', '/').TrimEnd('/')
            }
        )
    }
    finally {
        $archive.Dispose()
    }
    $unexpected = @(Compare-Object `
        -ReferenceObject $allowedEntries `
        -DifferenceObject $actualEntries)
    if ($actualEntries.Count -ne $allowedEntries.Count -or
        $unexpected.Count -ne 0) {
        throw (
            "The installer accepts only the reviewed thin payload; " +
            "unexpected or missing archive entries were found.")
    }

    [IO.Compression.ZipFile]::ExtractToDirectory(
        [IO.Path]::GetFullPath($PortableZip), $validationRoot)
    $payloadShell = Join-Path $validationRoot "AeroMirror\AeroMirror.exe"
    $payloadCore = Join-Path $validationRoot "AeroMirror\core\uxplay-windows.exe"
    $payloadManifest = Join-Path $validationRoot (
        "AeroMirror\core\resources\build-manifest.json")
    $payloadDelivery = Join-Path $validationRoot (
        "AeroMirror\core\resources\runtime-delivery.json")
    $payloadProvenance = Join-Path $validationRoot (
        "AeroMirror\core\resources\source-provenance.json")
    if (-not (Test-Path -LiteralPath $payloadShell -PathType Leaf) -or
        -not (Test-Path -LiteralPath $payloadCore -PathType Leaf) -or
        -not (Test-Path -LiteralPath $payloadManifest -PathType Leaf) -or
        -not (Test-Path -LiteralPath $payloadDelivery -PathType Leaf) -or
        -not (Test-Path -LiteralPath $payloadProvenance -PathType Leaf)) {
        throw "Portable payload is incomplete or is not a reviewed headless package."
    }
    if ((Get-Sha256Lower -Path $payloadProvenance) -ne $provenanceHash) {
        throw "Payload provenance does not match committed source provenance."
    }
    $payloadVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
        $payloadShell).FileVersion
    if (-not $payloadVersion.StartsWith($Version + ".")) {
        throw "Portable shell version $payloadVersion does not match $Version."
    }
    $payloadBuild = Get-Content -LiteralPath $payloadManifest `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($payloadBuild.shellMode -ne "headless" -or
        $payloadBuild.architecture -ne "x64" -or
        $payloadBuild.qtBuildVersion -ne $provenance.qtBuildVersion -or
        $payloadBuild.runtimeGStreamerVersion -ne
            $provenance.runtimeGStreamerVersion -or
        $payloadBuild.buildGStreamerVersion -ne
            $provenance.buildGStreamerVersion -or
        $payloadBuild.runtimeGStreamerCorePath -ne
            $provenance.runtimeGStreamerCorePath -or
        $payloadBuild.runtimeGStreamerCoreSha256 -ne
            $provenance.runtimeGStreamerCoreSha256 -or
        $payloadBuild.runtimeWasapi2PluginPath -ne
            $provenance.runtimeWasapi2PluginPath -or
        $payloadBuild.runtimeWasapi2PluginSha256 -ne
            $provenance.runtimeWasapi2PluginSha256 -or
        $payloadBuild.runtimeWasapi2RequiredProperty -ne
            $provenance.runtimeWasapi2RequiredProperty -or
        $payloadBuild.pinnedRuntimeArchiveSha256 -ne
            $provenance.pinnedRuntimeArchiveSha256 -or
        $payloadBuild.pinnedRuntimeRelease -ne
            $provenance.pinnedRuntimeRelease -or
        $payloadBuild.coreRuntimeCompatibility -ne
            $provenance.coreRuntimeCompatibility -or
        $payloadBuild.provenanceSchemaVersion -ne
            $provenance.schemaVersion -or
        $payloadBuild.sourceProvenanceSha256 -ne $provenanceHash -or
        $payloadBuild.headlessExecutableSha256 -ne
            $provenance.headlessExecutableSha256 -or
        $payloadBuild.uxplayWindowsCommit -ne
            $provenance.uxplayWindowsCommit -or
        $payloadBuild.libuxplayCommit -ne
            $provenance.libuxplayCommit -or
        $payloadBuild.uxplayWindowsPatchSha256 -ne
            $provenance.uxplayWindowsPatchSha256 -or
        $payloadBuild.libuxplayPatchSha256 -ne
            $provenance.libuxplayPatchSha256) {
        throw "Portable payload does not match committed source provenance."
    }
    Assert-HashMapMatches -Actual $payloadBuild.patchedSources `
        -Expected $provenance.patchedSources `
        -Description "Patched source hashes"
    Assert-HashMapMatches -Actual $payloadBuild.protectedSources `
        -Expected $provenance.protectedSources `
        -Description "Protected source hashes"
    Assert-HashMapMatches -Actual $payloadBuild.buildInputs `
        -Expected $provenance.buildInputs `
        -Description "Native build-input hashes"
    if ((Get-PeMachine $payloadShell) -ne 0x8664 -or
        (Get-PeMachine $payloadCore) -ne 0x8664) {
        throw "Portable payload contains a non-x64 executable."
    }
    $payloadCoreHash = Get-Sha256Lower -Path $payloadCore
    if ($payloadCoreHash -ne $provenance.headlessExecutableSha256) {
        throw "Portable core hash does not match its reviewed build manifest."
    }
    $payloadDeliveryData = Get-Content -LiteralPath $payloadDelivery `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($payloadDeliveryData.deliveryMode -ne "upstream-download" -or
        $payloadDeliveryData.upstreamProject -ne
            "leapbtw/uxplay-windows" -or
        $payloadDeliveryData.upstreamRelease -ne "2.0.0.1736" -or
        $payloadDeliveryData.asset -ne "uxplay-windows.zip" -or
        $payloadDeliveryData.url -ne
            "https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip" -or
        $payloadDeliveryData.sha256 -ne
            "9d3a51c15fc9db857351195e7eb7bbb21700d9ae25d936a54bcf8536b62cca18" -or
        $payloadDeliveryData.source -ne (
            "https://github.com/leapbtw/uxplay-windows/tree/" +
            $provenance.uxplayWindowsCommit)) {
        throw "Portable payload does not contain a valid pinned runtime delivery manifest."
    }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ `
    /out:$uninstaller `
    /win32icon:$icon `
    /win32manifest:$manifest `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    "/resource:$provenancePath,AeroMirrorSourceProvenance" `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Uninstaller compilation failed with exit code $LASTEXITCODE."
}

& $compiler /nologo /target:winexe /platform:x64 /optimize+ `
    /out:$output `
    /win32icon:$icon `
    /win32manifest:$manifest `
    "/resource:$PortableZip,AirPlayReceiverPayload" `
    "/resource:$uninstaller,AirPlayReceiverUninstaller" `
    "/resource:$provenancePath,AeroMirrorSourceProvenance" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}
$builtVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    $output).FileVersion
if ($builtVersion -ne ($Version + ".0")) {
    throw "Built installer version $builtVersion does not match $Version."
}
$shortcutCheck = Start-Process `
    -FilePath $output `
    -ArgumentList "/verify-shortcut-selection" `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($shortcutCheck.ExitCode -ne 0) {
    throw (
        "Installer shortcut-selection verification failed with exit code " +
        $shortcutCheck.ExitCode + ".")
}
$updateLifecycleCheck = Start-Process `
    -FilePath $output `
    -ArgumentList "/verify-update-lifecycle" `
    -WorkingDirectory $projectRoot `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($updateLifecycleCheck.ExitCode -ne 0) {
    throw (
        "Installer update-lifecycle verification failed with exit code " +
        $updateLifecycleCheck.ExitCode + ".")
}

Write-Host "Built $output"

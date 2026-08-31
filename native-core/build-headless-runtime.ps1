param(
    [Parameter(Mandatory = $true)]
    [string]$UpstreamRoot,

    [Parameter(Mandatory = $true)]
    [string]$OriginalRuntime,

    [Parameter(Mandatory = $true)]
    [string]$OriginalRuntimeArchive,

    [Parameter(Mandatory = $true)]
    [string]$HeadlessExecutable,

    [string]$MsysRoot = "C:\msys64",

    [string]$QtPrefix = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$nativeRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $nativeRoot
$artifactRoot = Join-Path $projectRoot "artifacts"
$stage = Join-Path $artifactRoot "headless-runtime"
$prefix = Join-Path $MsysRoot "ucrt64"
$runtimeBin = Join-Path $prefix "bin"
$qtRoot = if ([string]::IsNullOrWhiteSpace($QtPrefix)) {
    $prefix
} else {
    (Resolve-Path -LiteralPath $QtPrefix).Path
}
$qtBin = Join-Path $qtRoot "bin"
$windeployqt = Join-Path $qtBin "windeployqt.exe"
if (-not (Test-Path -LiteralPath $windeployqt -PathType Leaf)) {
    $windeployqt = Join-Path $qtBin "windeployqt-qt6.exe"
}
$provenancePath = Join-Path $nativeRoot "source-provenance.json"
$uxplayPatch = Join-Path $nativeRoot "uxplay-windows-headless.patch"
$libuxplayPatch = Join-Path $nativeRoot "libuxplay-aeromirror.patch"
$dnssdDefinition = Join-Path $nativeRoot "dnssd.def"

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childFull = [System.IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe output path: $childFull is outside $parentFull"
    }
}

function Get-Sha256Lower([string]$Path) {
    return (
        Get-FileHash -Algorithm SHA256 -LiteralPath $Path
    ).Hash.ToLowerInvariant()
}

function Assert-FileHash(
    [string]$Path,
    [string]$Expected,
    [string]$Description
) {
    $actual = Get-Sha256Lower -Path $Path
    if ($actual -ne $Expected.ToLowerInvariant()) {
        throw (
            "$Description does not match source-provenance.json. " +
            "Expected $Expected, found $actual.")
    }
}

function Get-Sha256LowerFromBytes([byte[]]$Bytes) {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString(
            $sha256.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-BytesContainAsciiToken(
    [byte[]]$Bytes,
    [string]$Token,
    [string]$Description
) {
    $binaryText = [Text.Encoding]::ASCII.GetString($Bytes)
    if ($binaryText.IndexOf($Token, [StringComparison]::Ordinal) -lt 0) {
        throw "$Description does not contain required token '$Token'."
    }
}

function Assert-BinaryContainsAsciiToken(
    [string]$Path,
    [string]$Token,
    [string]$Description
) {
    Assert-BytesContainAsciiToken `
        -Bytes ([IO.File]::ReadAllBytes($Path)) `
        -Token $Token `
        -Description $Description
}

$upstream = (Resolve-Path -LiteralPath $UpstreamRoot).Path
$original = (Resolve-Path -LiteralPath $OriginalRuntime).Path
$originalArchive = (Resolve-Path -LiteralPath $OriginalRuntimeArchive).Path
$headless = (Resolve-Path -LiteralPath $HeadlessExecutable).Path
$libuxplay = Join-Path $upstream "libuxplay"
$bonjourHeader = Join-Path $upstream "Bonjour SDK\Include\dns_sd.h"
$buildGStreamerCore = Join-Path $runtimeBin "libgstreamer-1.0-0.dll"
$buildWasapi2Plugin = Join-Path $prefix "lib\gstreamer-1.0\libgstwasapi2.dll"

$required = @(
    $headless,
    $provenancePath,
    $uxplayPatch,
    $libuxplayPatch,
    $dnssdDefinition,
    $bonjourHeader,
    (Join-Path $original "uxplay-bluetooth-beacon.exe"),
    (Join-Path $original "Qt6Core.dll"),
    (Join-Path $original "dnssd.dll"),
    (Join-Path $original "mDNSResponder.exe"),
    (Join-Path $original "LICENSE.rtf"),
    $windeployqt,
    (Join-Path $runtimeBin "python.exe"),
    (Join-Path $runtimeBin "objdump.exe")
    $buildGStreamerCore,
    $buildWasapi2Plugin
)
$missing = $required | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missing) {
    throw "Missing build inputs:`n$($missing -join [Environment]::NewLine)"
}

$provenance = Get-Content -LiteralPath $provenancePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ($provenance.schemaVersion -ne 1 -or
    $provenance.uxplayWindowsCommit -notmatch '^[0-9a-f]{40}$' -or
    $provenance.libuxplayCommit -notmatch '^[0-9a-f]{40}$' -or
    $provenance.uxplayWindowsPatchSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.libuxplayPatchSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.headlessExecutableSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.runtimeGStreamerVersion -notmatch '^\d+\.\d+\.\d+$' -or
    $provenance.buildGStreamerVersion -notmatch '^\d+\.\d+\.\d+$' -or
    $provenance.runtimeGStreamerCoreSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.runtimeWasapi2PluginSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.pinnedRuntimeArchiveSha256 -notmatch '^[0-9a-f]{64}$' -or
    $provenance.runtimeWasapi2RequiredProperty -ne 'continue-on-error') {
    throw "source-provenance.json is missing required pinned values."
}
$provenanceHash = Get-Sha256Lower -Path $provenancePath

$runtimeGStreamerCore = Join-Path $original (
    ([string]$provenance.runtimeGStreamerCorePath).Replace('/', '\'))
$runtimeWasapi2Plugin = Join-Path $original (
    ([string]$provenance.runtimeWasapi2PluginPath).Replace('/', '\'))
Assert-ChildPath -Parent $original -Child $runtimeGStreamerCore
Assert-ChildPath -Parent $original -Child $runtimeWasapi2Plugin
$missingRuntimeFiles = @(
    $runtimeGStreamerCore,
    $runtimeWasapi2Plugin
) | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
if ($missingRuntimeFiles) {
    throw "Pinned runtime GStreamer files are missing:`n$($missingRuntimeFiles -join [Environment]::NewLine)"
}

Assert-FileHash -Path $originalArchive `
    -Expected $provenance.pinnedRuntimeArchiveSha256 `
    -Description "Pinned upstream runtime archive"
Assert-FileHash -Path $runtimeGStreamerCore `
    -Expected $provenance.runtimeGStreamerCoreSha256 `
    -Description "Pinned runtime GStreamer core"
Assert-FileHash -Path $runtimeWasapi2Plugin `
    -Expected $provenance.runtimeWasapi2PluginSha256 `
    -Description "Pinned runtime wasapi2 plug-in"
Assert-BinaryContainsAsciiToken -Path $runtimeGStreamerCore `
    -Token ([string]$provenance.runtimeGStreamerVersion) `
    -Description "Pinned runtime GStreamer core"
Assert-BinaryContainsAsciiToken -Path $runtimeWasapi2Plugin `
    -Token ([string]$provenance.runtimeGStreamerVersion) `
    -Description "Pinned runtime wasapi2 plug-in"
Assert-BinaryContainsAsciiToken -Path $runtimeWasapi2Plugin `
    -Token ([string]$provenance.runtimeWasapi2RequiredProperty) `
    -Description "Pinned runtime wasapi2 plug-in"

$archive = [IO.Compression.ZipFile]::OpenRead($originalArchive)
try {
    $expectedArchiveEntries = @{
        ([string]$provenance.runtimeGStreamerCorePath) = @{
            Hash = [string]$provenance.runtimeGStreamerCoreSha256
            Tokens = @([string]$provenance.runtimeGStreamerVersion)
        }
        ([string]$provenance.runtimeWasapi2PluginPath) = @{
            Hash = [string]$provenance.runtimeWasapi2PluginSha256
            Tokens = @(
                [string]$provenance.runtimeGStreamerVersion,
                [string]$provenance.runtimeWasapi2RequiredProperty)
        }
    }
    foreach ($expectedEntry in $expectedArchiveEntries.GetEnumerator()) {
        $matches = @($archive.Entries | Where-Object {
            $_.FullName -ceq $expectedEntry.Key
        })
        if ($matches.Count -ne 1) {
            throw "Pinned runtime archive must contain exactly one '$($expectedEntry.Key)' entry."
        }
        $entryStream = $matches[0].Open()
        $memory = New-Object IO.MemoryStream
        try {
            $entryStream.CopyTo($memory)
            $entryBytes = $memory.ToArray()
        }
        finally {
            $memory.Dispose()
            $entryStream.Dispose()
        }
        $entryHash = Get-Sha256LowerFromBytes -Bytes $entryBytes
        if ($entryHash -ne $expectedEntry.Value.Hash) {
            throw "Pinned runtime archive entry '$($expectedEntry.Key)' has SHA-256 $entryHash."
        }
        foreach ($token in $expectedEntry.Value.Tokens) {
            Assert-BytesContainAsciiToken -Bytes $entryBytes -Token $token `
                -Description ("Pinned runtime archive entry " + $expectedEntry.Key)
        }
    }
}
finally {
    $archive.Dispose()
}

Assert-BinaryContainsAsciiToken -Path $buildGStreamerCore `
    -Token ([string]$provenance.buildGStreamerVersion) `
    -Description "Engineering build GStreamer core"
Assert-BinaryContainsAsciiToken -Path $buildWasapi2Plugin `
    -Token ([string]$provenance.buildGStreamerVersion) `
    -Description "Engineering build wasapi2 plug-in"
Assert-BinaryContainsAsciiToken -Path $buildWasapi2Plugin `
    -Token ([string]$provenance.runtimeWasapi2RequiredProperty) `
    -Description "Engineering build wasapi2 plug-in"

Assert-FileHash -Path $uxplayPatch `
    -Expected $provenance.uxplayWindowsPatchSha256 `
    -Description "uxplay-windows patch"
Assert-FileHash -Path $libuxplayPatch `
    -Expected $provenance.libuxplayPatchSha256 `
    -Description "libuxplay patch"
Assert-FileHash -Path $headless `
    -Expected $provenance.headlessExecutableSha256 `
    -Description "Headless executable"
Assert-FileHash -Path $bonjourHeader `
    -Expected $provenance.buildInputs.'dns_sd.h' `
    -Description "Bonjour interface header"
Assert-FileHash -Path $dnssdDefinition `
    -Expected $provenance.buildInputs.'dnssd.def' `
    -Description "Bonjour import definition"

foreach ($sourceProperty in $provenance.patchedSources.PSObject.Properties) {
    $sourcePath = Join-Path $upstream (
        $sourceProperty.Name.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Pinned patched source is missing: $sourcePath"
    }
    Assert-FileHash -Path $sourcePath `
        -Expected ([string]$sourceProperty.Value) `
        -Description ("Patched source " + $sourceProperty.Name)
}
foreach ($sourceProperty in $provenance.protectedSources.PSObject.Properties) {
    $sourcePath = Join-Path $upstream (
        $sourceProperty.Name.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Pinned protected source is missing: $sourcePath"
    }
    Assert-FileHash -Path $sourcePath `
        -Expected ([string]$sourceProperty.Value) `
        -Description ("Protected unmodified source " + $sourceProperty.Name)
}

$upstreamHasGit = Test-Path -LiteralPath (Join-Path $upstream ".git")
$libuxplayHasGit = Test-Path -LiteralPath (Join-Path $libuxplay ".git")
if ($upstreamHasGit -ne $libuxplayHasGit) {
    throw "Prepared source must contain either both Git repositories or neither."
}
if ($upstreamHasGit) {
    $upstreamCommit = (
        & git -c ("safe.directory=" + $upstream) -C $upstream rev-parse HEAD
    ).Trim()
    if ($LASTEXITCODE -ne 0 -or
        $upstreamCommit -ne $provenance.uxplayWindowsCommit) {
        throw "uxplay-windows commit does not match source-provenance.json."
    }
    $libuxplayCommit = (
        & git -c ("safe.directory=" + $libuxplay) -C $libuxplay rev-parse HEAD
    ).Trim()
    if ($LASTEXITCODE -ne 0 -or
        $libuxplayCommit -ne $provenance.libuxplayCommit) {
        throw "libuxplay commit does not match source-provenance.json."
    }
}

$objdump = Join-Path $runtimeBin "objdump.exe"
$coreHeaders = (& $objdump -f $headless 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0 -or
    $coreHeaders -notmatch 'file format pei-x86-64' -or
    $coreHeaders -notmatch 'architecture:\s+i386:x86-64') {
    throw "The input headless core is not an x64 PE executable."
}
$coreImports = (& $objdump -p $headless 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the input headless core."
}
if ($coreImports -notmatch '\bqt_version_tag_6_10\b') {
    throw "The input headless core does not import qt_version_tag_6_10."
}
if ($coreImports -match '\bqt_version_tag_6_11\b') {
    throw "The input headless core imports incompatible qt_version_tag_6_11."
}
$runtimeQtVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    (Join-Path $original "Qt6Core.dll")).FileVersion
if ($runtimeQtVersion -notmatch (
        '^' + [Regex]::Escape($provenance.qtBuildVersion) + '(?:\.0)?$')) {
    throw (
        "The pinned runtime must contain Qt6Core " +
        "$($provenance.qtBuildVersion); found " +
        "'$runtimeQtVersion'.")
}

Assert-ChildPath -Parent $artifactRoot -Child $stage
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "resources") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage "lib\gstreamer-1.0") | Out-Null

Copy-Item -LiteralPath $headless -Destination (Join-Path $stage "uxplay-windows.exe")
Copy-Item -LiteralPath (Join-Path $original "uxplay-bluetooth-beacon.exe") -Destination $stage
Copy-Item -LiteralPath (Join-Path $original "dnssd.dll") -Destination $stage
Copy-Item -LiteralPath (Join-Path $original "mDNSResponder.exe") -Destination $stage
Copy-Item -LiteralPath (Join-Path $original "LICENSE.rtf") -Destination $stage
Copy-Item -LiteralPath (Join-Path $upstream "stuff\newicon.ico") `
    -Destination (Join-Path $stage "resources\icon.ico")
Copy-Item -LiteralPath (Join-Path $upstream "stuff\uxplay_arguments_list.txt") `
    -Destination (Join-Path $stage "resources\uxplay_arguments_list.txt")
Copy-Item -LiteralPath (Join-Path $nativeRoot "gstreamer-features.txt") `
    -Destination (Join-Path $stage "resources\gstreamer-features.txt")
Copy-Item -LiteralPath $provenancePath `
    -Destination (Join-Path $stage "resources\source-provenance.json")

$env:MSYSTEM = "UCRT64"
$env:PATH = "$qtBin;$runtimeBin;$(Join-Path $MsysRoot 'usr\bin');$env:PATH"

& $windeployqt `
    --release `
    --no-translations `
    --no-compiler-runtime `
    --dir $stage `
    (Join-Path $stage "uxplay-windows.exe")
if ($LASTEXITCODE -ne 0) {
    throw "windeployqt failed with exit code $LASTEXITCODE"
}

$pluginDir = Join-Path $prefix "lib\gstreamer-1.0"
$registry = Join-Path $artifactRoot "headless-build-registry.bin"
if (Test-Path -LiteralPath $registry) {
    Remove-Item -LiteralPath $registry -Force
}
$env:GST_PLUGIN_PATH = ""
$env:GST_PLUGIN_PATH_1_0 = ""
$env:GST_PLUGIN_SYSTEM_PATH = $pluginDir
$env:GST_PLUGIN_SYSTEM_PATH_1_0 = $pluginDir
$env:GST_REGISTRY_1_0 = $registry

& (Join-Path $runtimeBin "python.exe") `
    (Join-Path $upstream "scripts\resolve-gstreamer-plugins.py") `
    --features (Join-Path $nativeRoot "gstreamer-features.txt") `
    --plugin-dir $pluginDir `
    --destination (Join-Path $stage "lib\gstreamer-1.0") `
    --manifest (Join-Path $stage "resources\gstreamer-plugins.json")
if ($LASTEXITCODE -ne 0) {
    throw "GStreamer plugin resolution failed with exit code $LASTEXITCODE"
}

$stagedBuildGStreamerCore = Join-Path $stage "libgstreamer-1.0-0.dll"
$stagedBuildWasapi2Plugin = Join-Path $stage `
    "lib\gstreamer-1.0\libgstwasapi2.dll"
# The feature resolver copies plug-ins, while the later dependency collector
# supplies their DLL closure. Seed the GStreamer core now so its exact
# build-time version and hash can be recorded in the manifest before that
# collector inventories the complete stage.
Copy-Item -LiteralPath $buildGStreamerCore `
    -Destination $stagedBuildGStreamerCore -Force
Assert-BinaryContainsAsciiToken -Path $stagedBuildGStreamerCore `
    -Token ([string]$provenance.buildGStreamerVersion) `
    -Description "Staged engineering GStreamer core"
Assert-BinaryContainsAsciiToken -Path $stagedBuildWasapi2Plugin `
    -Token ([string]$provenance.buildGStreamerVersion) `
    -Description "Staged engineering wasapi2 plug-in"
Assert-BinaryContainsAsciiToken -Path $stagedBuildWasapi2Plugin `
    -Token ([string]$provenance.runtimeWasapi2RequiredProperty) `
    -Description "Staged engineering wasapi2 plug-in"

$scannerDestination = Join-Path $stage "libexec\gstreamer-1.0"
New-Item -ItemType Directory -Force -Path $scannerDestination | Out-Null
Copy-Item -LiteralPath (Join-Path $prefix "libexec\gstreamer-1.0\gst-plugin-scanner.exe") `
    -Destination $scannerDestination

$gioDestination = Join-Path $stage "lib\gio\modules"
New-Item -ItemType Directory -Force -Path $gioDestination | Out-Null
Get-ChildItem -LiteralPath (Join-Path $prefix "lib\gio\modules") -Filter "*.dll" -File |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $gioDestination
    }

Copy-Item -Path (Join-Path $prefix "etc\fonts") `
    -Destination (Join-Path $stage "etc") -Recurse -Force

$buildManifest = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    architecture = "x64"
    shellMode = "headless"
    qtBuildVersion = [string]$provenance.qtBuildVersion
    runtimeGStreamerVersion = [string]$provenance.runtimeGStreamerVersion
    buildGStreamerVersion = [string]$provenance.buildGStreamerVersion
    runtimeGStreamerCorePath = [string]$provenance.runtimeGStreamerCorePath
    runtimeGStreamerCoreSha256 = [string]$provenance.runtimeGStreamerCoreSha256
    runtimeWasapi2PluginPath = [string]$provenance.runtimeWasapi2PluginPath
    runtimeWasapi2PluginSha256 = [string]$provenance.runtimeWasapi2PluginSha256
    runtimeWasapi2RequiredProperty = [string]$provenance.runtimeWasapi2RequiredProperty
    pinnedRuntimeArchiveSha256 = [string]$provenance.pinnedRuntimeArchiveSha256
    pinnedRuntimeRelease = [string]$provenance.pinnedRuntimeRelease
    coreRuntimeCompatibility = [string]$provenance.coreRuntimeCompatibility
    qtImportedVersionTag = "qt_version_tag_6_10"
    qtRejectedVersionTag = "qt_version_tag_6_11"
    loaderTest = "required-by-installer"
    headlessExecutableSha256 = Get-Sha256Lower -Path (
        Join-Path $stage "uxplay-windows.exe")
    sourceProvenanceSha256 = $provenanceHash
    provenanceSchemaVersion = [int]$provenance.schemaVersion
    uxplayWindowsCommit = [string]$provenance.uxplayWindowsCommit
    libuxplayCommit = [string]$provenance.libuxplayCommit
    uxplayWindowsPatchSha256 = [string]$provenance.uxplayWindowsPatchSha256
    libuxplayPatchSha256 = [string]$provenance.libuxplayPatchSha256
    patchedSources = $provenance.patchedSources
    protectedSources = $provenance.protectedSources
    buildInputs = $provenance.buildInputs
    stagedBuildGStreamerCoreSha256 = Get-Sha256Lower -Path (
        $stagedBuildGStreamerCore)
    stagedBuildWasapi2PluginSha256 = Get-Sha256Lower -Path (
        $stagedBuildWasapi2Plugin)
    compiler = (& (Join-Path $runtimeBin "gcc.exe") --version |
        Select-Object -First 1)
    cmake = (& (Join-Path $runtimeBin "cmake.exe") --version |
        Select-Object -First 1)
    ninja = (& (Join-Path $runtimeBin "ninja.exe") --version |
        Select-Object -First 1)
}
$buildManifest | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath (Join-Path $stage "resources\build-manifest.json") -Encoding utf8

$aeroRuntimeTempRoot = [IO.Path]::GetTempPath()
$dependencyStage = Join-Path $aeroRuntimeTempRoot (
    "AeroMirror-runtime-dependencies-" + [Guid]::NewGuid().ToString("N"))
Assert-ChildPath -Parent $aeroRuntimeTempRoot -Child $dependencyStage
New-Item -ItemType Directory -Path $dependencyStage | Out-Null
try {
    # MSYS2 objdump cannot open some Unicode Windows paths. Run only its
    # recursive dependency pass against an ASCII temporary copy, then publish
    # the validated result back to the normal artifact directory.
    Get-ChildItem -LiteralPath $stage -Force |
        Copy-Item -Destination $dependencyStage -Recurse -Force
    & (Join-Path $upstream "scripts\collect-runtime-dependencies.ps1") `
        -StageDir $dependencyStage `
        -MsysRoot $MsysRoot `
        -EnvironmentName "ucrt64" `
        -ManifestPath (Join-Path $dependencyStage "resources\bundle-files.json")

    Assert-ChildPath -Parent $artifactRoot -Child $stage
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    Move-Item -LiteralPath $dependencyStage -Destination $stage
    $dependencyStage = $null
}
finally {
    if ($dependencyStage -and
        (Test-Path -LiteralPath $dependencyStage)) {
        Remove-Item -LiteralPath $dependencyStage -Recurse -Force
    }
}

Write-Host "Headless runtime staged at $stage"

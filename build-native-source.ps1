param(
    [Parameter(Mandatory = $true)]
    [string]$UpstreamRoot,

    [string]$Version = "0.12.20"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use the numeric MAJOR.MINOR.PATCH format."
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $projectRoot "artifacts"
$releaseRoot = Join-Path $artifactRoot ("release\" + $Version)
$upstream = (Resolve-Path -LiteralPath $UpstreamRoot).Path
$libuxplay = Join-Path $upstream "libuxplay"
$nativeRoot = Join-Path $projectRoot "native-core"
$provenancePath = Join-Path $nativeRoot "source-provenance.json"
$upstreamLockPath = Join-Path $projectRoot "UPSTREAM.lock"
$temporaryRoot = Join-Path $artifactRoot (
    "native-source-stage-" + [Guid]::NewGuid().ToString("N"))
$bundleName = "AeroMirror-native-source-" + $Version
$bundleRoot = Join-Path $temporaryRoot $bundleName
$sourceRoot = Join-Path $bundleRoot "uxplay-windows"
$inputsRoot = Join-Path $sourceRoot "AeroMirror-build-inputs"
$output = Join-Path $releaseRoot ($bundleName + ".zip")

function Invoke-Git {
    param(
        [string]$Repository,
        [string[]]$Arguments
    )
    & git -c ("safe.directory=" + $Repository) -C $Repository @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed in $Repository."
    }
}

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childFull = [IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith(
        $parentFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe source bundle path: $childFull is outside $parentFull"
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

function Read-UpstreamLock([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "UPSTREAM.lock is missing: $Path"
    }
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) {
            continue
        }
        $separator = $trimmed.IndexOf('=')
        if ($separator -le 0) {
            throw "UPSTREAM.lock contains an invalid line: $trimmed"
        }
        $key = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if ($values.ContainsKey($key) -or -not $value) {
            throw "UPSTREAM.lock contains a duplicate or empty key: $key"
        }
        $values[$key] = $value
    }
    return $values
}

if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "Native source provenance is missing: $provenancePath"
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
$expectedUpstream = [string]$provenance.uxplayWindowsCommit
$expectedLibuxplay = [string]$provenance.libuxplayCommit
$upstreamLock = Read-UpstreamLock -Path $upstreamLockPath
$requiredLockValues = @{
    "uxplay-windows.source.commit" = $expectedUpstream
    "libuxplay.commit" = $expectedLibuxplay
    "aeromirror.uxplay-windows.patch.sha256" =
        [string]$provenance.uxplayWindowsPatchSha256
    "aeromirror.libuxplay.patch.sha256" =
        [string]$provenance.libuxplayPatchSha256
    "aeromirror.headless-executable.sha256" =
        [string]$provenance.headlessExecutableSha256
    "uxplay-windows.asset.sha256" =
        [string]$provenance.pinnedRuntimeArchiveSha256
    "runtime.gstreamer" = [string]$provenance.runtimeGStreamerVersion
    "build.gstreamer" = [string]$provenance.buildGStreamerVersion
    "runtime.gstreamer.core.path" =
        [string]$provenance.runtimeGStreamerCorePath
    "runtime.gstreamer.core.sha256" =
        [string]$provenance.runtimeGStreamerCoreSha256
    "runtime.gstreamer.wasapi2.path" =
        [string]$provenance.runtimeWasapi2PluginPath
    "runtime.gstreamer.wasapi2.sha256" =
        [string]$provenance.runtimeWasapi2PluginSha256
    "runtime.gstreamer.wasapi2.required-property" =
        [string]$provenance.runtimeWasapi2RequiredProperty
}
foreach ($entry in $requiredLockValues.GetEnumerator()) {
    $actual = [string]$upstreamLock[$entry.Key]
    if (-not [string]::Equals(
            $actual, [string]$entry.Value,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw (
            "UPSTREAM.lock value for " + $entry.Key +
            " does not match source-provenance.json.")
    }
}

$upstreamCommit = (
    & git -c ("safe.directory=" + $upstream) -C $upstream rev-parse HEAD
).Trim()
$libuxplayCommit = (
    & git -c ("safe.directory=" + $libuxplay) -C $libuxplay rev-parse HEAD
).Trim()
if ($upstreamCommit -ne $expectedUpstream -or
    $libuxplayCommit -ne $expectedLibuxplay) {
    throw "Native source commits do not match UPSTREAM.lock."
}

$modified = @(
    & git -c ("safe.directory=" + $upstream) -C $upstream `
        status --short --untracked-files=no
)
$expectedModified = @(
    " m libuxplay",
    " M src/airplayworker.cpp",
    " M src/main.cpp",
    " M src/mainwindow.cpp",
    " M src/mainwindow.h"
)
$statusDifferences = @(Compare-Object $modified $expectedModified)
if ($statusDifferences.Count -ne 0) {
    throw "Upstream tree contains changes other than the reviewed headless patch."
}

$libModified = @(
    & git -c ("safe.directory=" + $libuxplay) -C $libuxplay `
        status --short --untracked-files=all
)
$expectedLibModified = @(
    "?? aeromirror_host_protocol.h",
    " M lib/crypto.c",
    " M lib/crypto.h",
    " M lib/dnssd.c",
    " M lib/dnssd.h",
    " M lib/fairplay_playfair.c",
    " M lib/http_handlers.h",
    " M lib/http_request.c",
    " M lib/http_request.h",
    " M lib/http_response.c",
    " M lib/http_response.h",
    " M lib/httpd.c",
    " M lib/mirror_buffer.c",
    " M lib/mirror_buffer.h",
    " M lib/netutils.c",
    " M lib/netutils.h",
    " M lib/pairing.c",
    " M lib/pairing.h",
    " M lib/raop.c",
    " M lib/raop.h",
    " M lib/raop_buffer.c",
    " M lib/raop_handlers.h",
    " M lib/raop_ntp.c",
    " M lib/raop_ntp.h",
    " M lib/raop_rtp.c",
    " M lib/raop_rtp.h",
    " M lib/raop_rtp_mirror.c",
    " M lib/raop_rtp_mirror.h",
    " M lib/utils.c",
    " M renderers/audio_renderer.c",
    " M renderers/video_renderer.c",
    " M renderers/video_renderer.h",
    " M uxplay.cpp",
    " M uxplay_api.h",
    "?? lib/mirror_payload_parser.c",
    "?? lib/mirror_payload_parser.h",
    "?? lib/worker_lifecycle.c",
    "?? lib/worker_lifecycle.h"
)
$libStatusDifferences = @(
    Compare-Object $libModified $expectedLibModified)
if ($libStatusDifferences.Count -ne 0) {
    throw "libuxplay contains changes other than the reviewed AeroMirror core patch."
}

$reviewedPatch = Join-Path (
    $nativeRoot) "uxplay-windows-headless.patch"
Assert-FileHash -Path $reviewedPatch `
    -Expected $provenance.uxplayWindowsPatchSha256 `
    -Description "Reviewed uxplay-windows patch"
$actualPatch = [IO.Path]::GetTempFileName()
try {
    & git -c ("safe.directory=" + $upstream) -C $upstream `
        diff --binary --no-ext-diff `
        ("--output=" + $actualPatch) -- `
        "src/airplayworker.cpp" `
        "src/main.cpp" `
        "src/mainwindow.cpp" `
        "src/mainwindow.h"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to generate the current native source diff."
    }
    $reviewedPatchHash = Get-Sha256Lower -Path $reviewedPatch
    $actualPatchHash = Get-Sha256Lower -Path $actualPatch
    if ($reviewedPatchHash -ne $actualPatchHash) {
        throw (
            "The modified native source does not exactly match " +
            "uxplay-windows-headless.patch.")
    }
}
finally {
    if (Test-Path -LiteralPath $actualPatch) {
        Remove-Item -LiteralPath $actualPatch -Force
    }
}

$reviewedLibPatch = Join-Path (
    $nativeRoot) "libuxplay-aeromirror.patch"
Assert-FileHash -Path $reviewedLibPatch `
    -Expected $provenance.libuxplayPatchSha256 `
    -Description "Reviewed libuxplay patch"
$actualLibPatch = [IO.Path]::GetTempFileName()
$actualLibIndex = Join-Path ([IO.Path]::GetTempPath()) (
    "aeromirror-libuxplay-index-" + [Guid]::NewGuid().ToString("N"))
$previousGitIndexFile = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $actualLibIndex
    & git -c ("safe.directory=" + $libuxplay) -C $libuxplay `
        read-tree HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to create the temporary libuxplay index."
    }
    & git -c ("safe.directory=" + $libuxplay) -C $libuxplay `
        add -N -- `
        "aeromirror_host_protocol.h" `
        "lib/mirror_payload_parser.c" `
        "lib/mirror_payload_parser.h" `
        "lib/worker_lifecycle.c" `
        "lib/worker_lifecycle.h"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to add new reviewed sources to the temporary index."
    }
    & git -c ("safe.directory=" + $libuxplay) -C $libuxplay `
        diff --binary --no-ext-diff `
        ("--output=" + $actualLibPatch) -- `
        "aeromirror_host_protocol.h" `
        "lib/crypto.c" `
        "lib/crypto.h" `
        "lib/dnssd.c" `
        "lib/dnssd.h" `
        "lib/fairplay_playfair.c" `
        "lib/http_handlers.h" `
        "lib/http_request.c" `
        "lib/http_request.h" `
        "lib/http_response.c" `
        "lib/http_response.h" `
        "lib/httpd.c" `
        "lib/mirror_buffer.c" `
        "lib/mirror_buffer.h" `
        "lib/mirror_payload_parser.c" `
        "lib/mirror_payload_parser.h" `
        "lib/netutils.c" `
        "lib/netutils.h" `
        "lib/pairing.c" `
        "lib/pairing.h" `
        "lib/raop.c" `
        "lib/raop.h" `
        "lib/raop_buffer.c" `
        "lib/raop_handlers.h" `
        "lib/raop_ntp.c" `
        "lib/raop_ntp.h" `
        "lib/raop_rtp.c" `
        "lib/raop_rtp.h" `
        "lib/raop_rtp_mirror.c" `
        "lib/raop_rtp_mirror.h" `
        "lib/utils.c" `
        "lib/worker_lifecycle.c" `
        "lib/worker_lifecycle.h" `
        "renderers/audio_renderer.c" `
        "renderers/video_renderer.c" `
        "renderers/video_renderer.h" `
        "uxplay.cpp" `
        "uxplay_api.h"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to generate the current libuxplay source diff."
    }
    $reviewedLibPatchHash = Get-Sha256Lower -Path $reviewedLibPatch
    $actualLibPatchHash = Get-Sha256Lower -Path $actualLibPatch
    if ($reviewedLibPatchHash -ne $actualLibPatchHash) {
        throw (
            "The modified libuxplay source does not exactly match " +
            "libuxplay-aeromirror.patch.")
    }
}
finally {
    $env:GIT_INDEX_FILE = $previousGitIndexFile
    if (Test-Path -LiteralPath $actualLibPatch) {
        Remove-Item -LiteralPath $actualLibPatch -Force
    }
    if (Test-Path -LiteralPath $actualLibIndex) {
        Remove-Item -LiteralPath $actualLibIndex -Force
    }
    $actualLibIndexLock = $actualLibIndex + ".lock"
    if (Test-Path -LiteralPath $actualLibIndexLock) {
        Remove-Item -LiteralPath $actualLibIndexLock -Force
    }
}

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

$bonjourHeader = Join-Path $upstream "Bonjour SDK\Include\dns_sd.h"
$dnssdDefinition = Join-Path $nativeRoot "dnssd.def"
Assert-FileHash -Path $bonjourHeader `
    -Expected $provenance.buildInputs.'dns_sd.h' `
    -Description "Bonjour interface header"
Assert-FileHash -Path $dnssdDefinition `
    -Expected $provenance.buildInputs.'dnssd.def' `
    -Description "Bonjour import definition"

Assert-ChildPath -Parent $artifactRoot -Child $temporaryRoot
New-Item -ItemType Directory -Force -Path $bundleRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

try {
    $upstreamArchive = Join-Path $temporaryRoot "uxplay-windows.zip"
    $libArchive = Join-Path $temporaryRoot "libuxplay.zip"
    Invoke-Git -Repository $upstream -Arguments @(
        "archive", "--format=zip", "-o", $upstreamArchive, "HEAD")
    [IO.Compression.ZipFile]::ExtractToDirectory(
        $upstreamArchive, $sourceRoot)
    Invoke-Git -Repository $libuxplay -Arguments @(
        "archive", "--format=zip", "-o", $libArchive, "HEAD")
    New-Item -ItemType Directory -Force -Path (
        Join-Path $sourceRoot "libuxplay") | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory(
        $libArchive, (Join-Path $sourceRoot "libuxplay"))

    foreach ($relative in @(
        "src\airplayworker.cpp",
        "src\main.cpp",
        "src\mainwindow.cpp",
        "src\mainwindow.h"
    )) {
        Copy-Item -LiteralPath (Join-Path $upstream $relative) `
            -Destination (Join-Path $sourceRoot $relative) -Force
    }
    foreach ($relative in @(
        "aeromirror_host_protocol.h",
        "lib\crypto.c",
        "lib\crypto.h",
        "lib\dnssd.c",
        "lib\dnssd.h",
        "lib\fairplay_playfair.c",
        "lib\http_handlers.h",
        "lib\http_request.c",
        "lib\http_request.h",
        "lib\http_response.c",
        "lib\http_response.h",
        "lib\httpd.c",
        "lib\mirror_buffer.c",
        "lib\mirror_buffer.h",
        "lib\mirror_payload_parser.c",
        "lib\mirror_payload_parser.h",
        "lib\netutils.c",
        "lib\netutils.h",
        "lib\pairing.c",
        "lib\pairing.h",
        "lib\raop.c",
        "lib\raop.h",
        "lib\raop_buffer.c",
        "lib\raop_handlers.h",
        "lib\raop_ntp.c",
        "lib\raop_ntp.h",
        "lib\raop_rtp.c",
        "lib\raop_rtp.h",
        "lib\raop_rtp_mirror.c",
        "lib\raop_rtp_mirror.h",
        "lib\utils.c",
        "lib\worker_lifecycle.c",
        "lib\worker_lifecycle.h",
        "renderers\audio_renderer.c",
        "renderers\video_renderer.c",
        "renderers\video_renderer.h",
        "uxplay.cpp",
        "uxplay_api.h"
    )) {
        Copy-Item -LiteralPath (Join-Path $libuxplay $relative) `
            -Destination (Join-Path $sourceRoot "libuxplay\$relative") `
            -Force
    }

    New-Item -ItemType Directory -Force -Path $inputsRoot | Out-Null
    foreach ($name in @(
        "uxplay-windows-headless.patch",
        "libuxplay-aeromirror.patch",
        "source-provenance.json",
        "build-compatible-core.ps1",
        "BUILD_INFO.md",
        "README.md",
        "dnssd.def",
        "build-headless-runtime.ps1",
        "gstreamer-features.txt"
    )) {
        Copy-Item -LiteralPath (Join-Path $nativeRoot $name) `
            -Destination $inputsRoot
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") `
        -Destination (Join-Path $inputsRoot "AEROMIRROR-LICENSE")
    Copy-Item -LiteralPath (
        $bonjourHeader) `
        -Destination $inputsRoot

    foreach ($sourceProperty in $provenance.patchedSources.PSObject.Properties) {
        $stagedSourcePath = Join-Path $sourceRoot (
            $sourceProperty.Name.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $stagedSourcePath -PathType Leaf)) {
            throw "Packaged patched source is missing: $stagedSourcePath"
        }
        Assert-FileHash -Path $stagedSourcePath `
            -Expected ([string]$sourceProperty.Value) `
            -Description ("Packaged patched source " + $sourceProperty.Name)
    }
    foreach ($sourceProperty in $provenance.protectedSources.PSObject.Properties) {
        $stagedSourcePath = Join-Path $sourceRoot (
            $sourceProperty.Name.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $stagedSourcePath -PathType Leaf)) {
            throw "Packaged protected source is missing: $stagedSourcePath"
        }
        Assert-FileHash -Path $stagedSourcePath `
            -Expected ([string]$sourceProperty.Value) `
            -Description ("Packaged protected source " + $sourceProperty.Name)
    }

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Force
    }
    Compress-Archive -LiteralPath $bundleRoot -DestinationPath $output `
        -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Native corresponding source is ready at $output"

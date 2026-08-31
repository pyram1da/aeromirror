param(
    [Parameter(Mandatory = $true)]
    [string]$HeadlessRuntimePath,

    [string]$Version = "0.12.22"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use the numeric MAJOR.MINOR.PATCH format."
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $projectRoot "artifacts"
$sourceExe = Join-Path $artifactRoot "Release\AeroMirror.exe"
$runtimeRoot = [IO.Path]::GetFullPath($HeadlessRuntimePath)
$headlessCore = Join-Path $runtimeRoot "uxplay-windows.exe"
$runtimeManifest = Join-Path $runtimeRoot "resources\build-manifest.json"
$runtimeProvenance = Join-Path $runtimeRoot "resources\source-provenance.json"
$provenancePath = Join-Path $projectRoot "native-core\source-provenance.json"
$stageRoot = Join-Path $artifactRoot (
    "review-stage-" + [Guid]::NewGuid().ToString("N"))
$stage = Join-Path $stageRoot "AeroMirror"
$core = Join-Path $stage "core"
$resources = Join-Path $core "resources"
$docs = Join-Path $stage "docs"
$zip = Join-Path $artifactRoot (
    "AeroMirror-review-payload-x64-" + $Version + ".zip")

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childFull = [IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith(
        $parentFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe package path: $childFull is outside $parentFull"
    }
}

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

& (Join-Path $projectRoot "build.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "Shell build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $headlessCore -PathType Leaf) -or
    -not (Test-Path -LiteralPath $runtimeManifest -PathType Leaf) -or
    -not (Test-Path -LiteralPath $runtimeProvenance -PathType Leaf) -or
    -not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "The reviewed headless core, manifest, and provenance are required."
}
$provenance = Get-Content -LiteralPath $provenancePath `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$provenanceHash = Get-Sha256Lower -Path $provenancePath
if ((Get-Sha256Lower -Path $runtimeProvenance) -ne $provenanceHash) {
    throw "Runtime source provenance does not match the committed release provenance."
}
$manifestData = Get-Content -LiteralPath $runtimeManifest `
    -Raw -Encoding UTF8 | ConvertFrom-Json
$coreHash = Get-Sha256Lower -Path $headlessCore
if ($manifestData.shellMode -ne "headless" -or
    $manifestData.architecture -ne "x64" -or
    $manifestData.qtBuildVersion -ne $provenance.qtBuildVersion -or
    $manifestData.runtimeGStreamerVersion -ne
        $provenance.runtimeGStreamerVersion -or
    $manifestData.buildGStreamerVersion -ne
        $provenance.buildGStreamerVersion -or
    $manifestData.runtimeGStreamerCorePath -ne
        $provenance.runtimeGStreamerCorePath -or
    $manifestData.runtimeGStreamerCoreSha256 -ne
        $provenance.runtimeGStreamerCoreSha256 -or
    $manifestData.runtimeWasapi2PluginPath -ne
        $provenance.runtimeWasapi2PluginPath -or
    $manifestData.runtimeWasapi2PluginSha256 -ne
        $provenance.runtimeWasapi2PluginSha256 -or
    $manifestData.runtimeWasapi2RequiredProperty -ne
        $provenance.runtimeWasapi2RequiredProperty -or
    $manifestData.pinnedRuntimeArchiveSha256 -ne
        $provenance.pinnedRuntimeArchiveSha256 -or
    $manifestData.pinnedRuntimeRelease -ne $provenance.pinnedRuntimeRelease -or
    $manifestData.coreRuntimeCompatibility -ne
        $provenance.coreRuntimeCompatibility -or
    $manifestData.provenanceSchemaVersion -ne $provenance.schemaVersion -or
    $manifestData.sourceProvenanceSha256 -ne $provenanceHash -or
    $manifestData.headlessExecutableSha256 -ne
        $provenance.headlessExecutableSha256 -or
    $coreHash -ne $provenance.headlessExecutableSha256 -or
    $manifestData.uxplayWindowsCommit -ne
        $provenance.uxplayWindowsCommit -or
    $manifestData.libuxplayCommit -ne $provenance.libuxplayCommit -or
    $manifestData.uxplayWindowsPatchSha256 -ne
        $provenance.uxplayWindowsPatchSha256 -or
    $manifestData.libuxplayPatchSha256 -ne
        $provenance.libuxplayPatchSha256 -or
    (Get-PeMachine $headlessCore) -ne 0x8664) {
    throw "The core does not match the committed source provenance."
}
Assert-HashMapMatches -Actual $manifestData.patchedSources `
    -Expected $provenance.patchedSources `
    -Description "Patched source hashes"
Assert-HashMapMatches -Actual $manifestData.protectedSources `
    -Expected $provenance.protectedSources `
    -Description "Protected source hashes"
Assert-HashMapMatches -Actual $manifestData.buildInputs `
    -Expected $provenance.buildInputs `
    -Description "Native build-input hashes"
if ((Get-PeMachine $sourceExe) -ne 0x8664) {
    throw "The AeroMirror shell is not an x64 PE executable."
}
$shellVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    $sourceExe).FileVersion
if (-not $shellVersion.StartsWith($Version + ".")) {
    throw "Shell version $shellVersion does not match requested release $Version."
}

Assert-ChildPath -Parent $artifactRoot -Child $stageRoot
New-Item -ItemType Directory -Force -Path $resources | Out-Null
New-Item -ItemType Directory -Force -Path $docs | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $stage
Copy-Item -LiteralPath $headlessCore -Destination $core
Copy-Item -LiteralPath $runtimeManifest -Destination $resources
Copy-Item -LiteralPath $runtimeProvenance -Destination $resources
foreach ($name in @(
    "README.md",
    "CHANGELOG.md",
    "LICENSE",
    "THIRD_PARTY_NOTICES.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "update-repository.txt"
)) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $projectRoot "docs\TROUBLESHOOTING.md") `
    -Destination $docs

$deliveryManifest = [ordered]@{
    deliveryMode = "upstream-download"
    upstreamProject = "leapbtw/uxplay-windows"
    upstreamRelease = "2.0.0.1736"
    asset = "uxplay-windows.zip"
    url = "https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip"
    sha256 = "9d3a51c15fc9db857351195e7eb7bbb21700d9ae25d936a54bcf8536b62cca18"
    source = "https://github.com/leapbtw/uxplay-windows/tree/8cf3424b438424bc99a89155bd29a789f48a43c0"
}
$deliveryManifest | ConvertTo-Json |
    Set-Content -LiteralPath (
        Join-Path $resources "runtime-delivery.json") -Encoding UTF8

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -LiteralPath $stage -DestinationPath $zip `
    -CompressionLevel Optimal
Remove-Item -LiteralPath $stageRoot -Recurse -Force
Write-Host "Packaged network review payload $zip"

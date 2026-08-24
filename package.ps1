param(
    [Parameter(Mandatory = $true)]
    [string]$UxPlayPortablePath,

    [Parameter(Mandatory = $true)]
    [string]$HeadlessCorePath,

    [string]$Version = "0.12.19"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceExe = Join-Path $projectRoot "artifacts\Release\AeroMirror.exe"
$provenancePath = Join-Path $projectRoot "native-core\source-provenance.json"
$stageRoot = Join-Path $projectRoot (
    "artifacts\package-stage-" + [Guid]::NewGuid().ToString("N"))
$stage = Join-Path $stageRoot "AeroMirror"
$core = Join-Path $stage "core"
$docs = Join-Path $stage "docs"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use the numeric MAJOR.MINOR.PATCH format."
}

function Assert-ChildPath([string]$Parent, [string]$Child) {
    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childFull = [System.IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe output path: $childFull is outside $parentFull"
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

function Assert-RuntimeBundle([string]$RuntimeRoot) {
    $bundleManifest = Join-Path $RuntimeRoot "resources\bundle-files.json"
    if (-not (Test-Path -LiteralPath $bundleManifest -PathType Leaf)) {
        throw "The runtime bundle file manifest is missing: $bundleManifest"
    }
    $parsedEntries = Get-Content -LiteralPath $bundleManifest `
        -Raw -Encoding UTF8 | ConvertFrom-Json
    $entries = @($parsedEntries)
    if ($entries.Count -eq 0) {
        throw "The runtime bundle file manifest is empty."
    }

    $expected = @{}
    foreach ($entry in $entries) {
        $relative = ([string]$entry.path).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($relative) -or
            [IO.Path]::IsPathRooted($relative) -or
            $relative -match '(^|/)\.\.(/|$)' -or
            $entry.sha256 -notmatch '^[0-9a-f]{64}$') {
            throw "The runtime bundle manifest contains an unsafe entry."
        }
        $key = $relative.ToLowerInvariant()
        if ($expected.ContainsKey($key)) {
            throw "Duplicate runtime bundle manifest entry: $relative"
        }
        $expected[$key] = $entry
        $file = Join-Path $RuntimeRoot $relative.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or
            (Get-Item -LiteralPath $file).Length -ne [long]$entry.bytes -or
            (Get-Sha256Lower -Path $file) -ne [string]$entry.sha256) {
            throw "Runtime file does not match bundle-files.json: $relative"
        }
    }

    $actualFiles = @(
        Get-ChildItem -LiteralPath $RuntimeRoot -Recurse -File |
            Where-Object {
                $_.FullName -ne [IO.Path]::GetFullPath($bundleManifest)
            }
    )
    foreach ($file in $actualFiles) {
        $relative = $file.FullName.Substring(
            [IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\').Length + 1
        ).Replace('\', '/').ToLowerInvariant()
        if (-not $expected.ContainsKey($relative)) {
            throw "Unmanifested runtime file would enter the portable ZIP: $relative"
        }
    }
    if ($actualFiles.Count -ne $expected.Count) {
        throw "Runtime file count does not match bundle-files.json."
    }
}

& (Join-Path $projectRoot "build.ps1")

if (-not (Test-Path (Join-Path $UxPlayPortablePath "uxplay-windows.exe"))) {
    throw "uxplay-windows.exe was not found in $UxPlayPortablePath"
}
$resolvedHeadlessCore = [System.IO.Path]::GetFullPath($HeadlessCorePath)
if (-not (Test-Path -LiteralPath $resolvedHeadlessCore -PathType Leaf)) {
    throw "Headless core executable was not found: $resolvedHeadlessCore"
}
$runtimeManifest = Join-Path $UxPlayPortablePath "resources\build-manifest.json"
$runtimeProvenance = Join-Path (
    $UxPlayPortablePath) "resources\source-provenance.json"
if (-not (Test-Path -LiteralPath $runtimeManifest -PathType Leaf) -or
    -not (Test-Path -LiteralPath $runtimeProvenance -PathType Leaf) -or
    -not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
    throw "The reviewed headless runtime manifest or provenance is missing."
}
$provenance = Get-Content -LiteralPath $provenancePath -Raw -Encoding UTF8 |
    ConvertFrom-Json
$provenanceHash = Get-Sha256Lower -Path $provenancePath
if ((Get-Sha256Lower -Path $runtimeProvenance) -ne $provenanceHash) {
    throw "Runtime source provenance does not match the committed release provenance."
}
$manifestData = Get-Content -LiteralPath $runtimeManifest -Raw -Encoding UTF8 |
    ConvertFrom-Json
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
    $manifestData.uxplayWindowsCommit -ne
        $provenance.uxplayWindowsCommit -or
    $manifestData.libuxplayCommit -ne $provenance.libuxplayCommit -or
    $manifestData.uxplayWindowsPatchSha256 -ne
        $provenance.uxplayWindowsPatchSha256 -or
    $manifestData.libuxplayPatchSha256 -ne
        $provenance.libuxplayPatchSha256) {
    throw "The runtime manifest does not match committed source provenance."
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
if ((Get-PeMachine $resolvedHeadlessCore) -ne 0x8664) {
    throw "The requested headless core is not an x64 PE executable."
}
$requestedCoreHash = Get-Sha256Lower -Path $resolvedHeadlessCore
if ($requestedCoreHash -ne $provenance.headlessExecutableSha256) {
    throw "The requested core hash does not match the reviewed runtime manifest."
}
Assert-RuntimeBundle -RuntimeRoot (
    [IO.Path]::GetFullPath($UxPlayPortablePath))

Assert-ChildPath -Parent $projectRoot -Child $stageRoot
New-Item -ItemType Directory -Force -Path $core | Out-Null
New-Item -ItemType Directory -Force -Path $docs | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $stage
Copy-Item -Path (Join-Path $UxPlayPortablePath "*") -Destination $core -Recurse -Force
Copy-Item -LiteralPath $resolvedHeadlessCore `
    -Destination (Join-Path $core "uxplay-windows.exe") -Force
$packagedCoreHash = Get-Sha256Lower -Path (
    Join-Path $core "uxplay-windows.exe")
if ($packagedCoreHash -ne $requestedCoreHash) {
    throw "The packaged core does not match the requested headless executable."
}
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "CHANGELOG.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD_PARTY_NOTICES.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "CONTRIBUTING.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "SECURITY.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "update-repository.txt") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "docs\TROUBLESHOOTING.md") `
    -Destination $docs
$shellVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    (Join-Path $stage "AeroMirror.exe")).FileVersion
if (-not $shellVersion.StartsWith($Version + ".")) {
    throw "Shell version $shellVersion does not match requested release $Version."
}

$zip = Join-Path $projectRoot (
    "artifacts\AeroMirror-portable-x64-" + $Version + ".zip")
Assert-ChildPath -Parent $projectRoot -Child $zip
if (Test-Path $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -LiteralPath $stageRoot -Recurse -Force
Write-Host "Packaged $zip"

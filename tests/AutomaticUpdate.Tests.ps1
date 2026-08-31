param(
    [string]$AssemblyPath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot "artifacts\Release\AeroMirror.exe"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "FAILED: $Message"
    }
}

function Get-MethodByParameterCount(
    [Type]$Type,
    [string]$Name,
    [int]$ParameterCount,
    [Reflection.BindingFlags]$Flags) {
    foreach ($method in $Type.GetMethods($Flags)) {
        if ($method.Name -eq $Name -and
            $method.GetParameters().Count -eq $ParameterCount) {
            return $method
        }
    }
    return $null
}

function Get-InnerException([Exception]$Exception) {
    $current = $Exception
    while ($null -ne $current.InnerException) {
        $current = $current.InnerException
    }
    return $current
}

Assert-True (Test-Path -LiteralPath $AssemblyPath) `
    "compiled AeroMirror assembly exists"

$programSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "src\Application\Program.cs"))
$automaticContextSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "src\Receiver\ReceiverContext.Updates.cs"))
$automaticServiceSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "src\Updates\AutomaticUpdateService.cs"))
$updateServiceSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "src\Updates\UpdateService.cs"))
$settingsFormSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "src\UI\SettingsForm.cs"))
$automaticUpdatesRussianText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0JDQstGC0L7QvNCw0YLQuNGH0LXRgdC60Lgg0YHQutCw0YfQuNCy0LDRgtGMINC/0YDQvtCy0LXRgNC10L3QvdGL0LUg0L7QsdC90L7QstC70LXQvdC40Y8="))
$nonInterruptingRussianText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0KLQtdC60YPRidCw0Y8g0YLRgNCw0L3RgdC70Y/RhtC40Y8g0Lgg0L3QtdGB0L7RhdGA0LDQvdGR0L3QvdGL0LUg0L3QsNGB0YLRgNC+0LnQutC4INC90LjQutC+0LPQtNCwINC90LUg0L/RgNC10YDRi9Cy0LDRjtGC0YHRjw=="))

$singleInstanceBoundary = $programSource.IndexOf(
    "if (!created)", [StringComparison]::Ordinal)
$startupUpdateBoundary = $programSource.IndexOf(
    "AutomaticUpdateService.TryLaunchPendingUpdate(",
    [StringComparison]::Ordinal)
$receiverConstructionBoundary = $programSource.IndexOf(
    "Application.Run(new ReceiverContext", [StringComparison]::Ordinal)
Assert-True ($singleInstanceBoundary -ge 0 -and
    $startupUpdateBoundary -gt $singleInstanceBoundary -and
    $receiverConstructionBoundary -gt $startupUpdateBoundary) `
    "a staged Setup is considered only after single-instance protection and before receiver/UI construction"
Assert-True ($automaticContextSource.Contains(
    "StageVerifiedInstaller(") -and
    -not $automaticContextSource.Contains("Process.Start(") -and
    $automaticContextSource.Contains(
        "The active ") -and
    $automaticContextSource.Contains(
        "receiver was not interrupted") -and
    $settingsFormSource.Contains(
        $automaticUpdatesRussianText) -and
    $settingsFormSource.Contains(
        $nonInterruptingRussianText)) `
    "background mode only stages a verified Setup and explains the non-interrupting opt-in in Russian"
Assert-True ([regex]::Matches(
        $automaticServiceSource, [regex]::Escape("Process.Start(")).Count -eq 1 -and
    $automaticServiceSource.Contains(
        'Arguments = "/automatic-update /delete-source"') -and
    -not $automaticServiceSource.Contains(
        'Arguments = "/update /delete-source"') -and
    $automaticServiceSource.Contains("MaximumLaunchAttempts = 3") -and
    $automaticServiceSource.Contains("MaximumStageAge") -and
    $automaticServiceSource.Contains("FailedLaunchRetryDelay")) `
    "only startup launch owns Setup handoff, with bounded cleanup and retry policy"

$installerSource = [IO.File]::ReadAllText(
    (Join-Path $projectRoot "installer\AirPlayReceiverSetup.cs"))
Assert-True ($installerSource.Contains(
        'HasArgument(args, "/automatic-update")') -and
    $installerSource.Contains(
        "updateRequested, automaticUpdateRequested") -and
    $installerSource.Contains(
        "GetPostInstallRelaunchArguments(") -and
    $installerSource.Contains(
        "GetPostInstallFailureRelaunchArguments(") -and
    $installerSource.Contains(
        "TryRelaunchInstalledShellAfterFailure(") -and
    $installerSource.Contains(
        "WaitForTransactionAndRelaunchAfterSetupGateFailure(") -and
    $installerSource.Contains(
        "no executable was ") -and
    $installerSource.Contains(
        "started from the mutable installation tree") -and
    $installerSource.Contains("if (updateRequested)") -and
    $installerSource.Contains("ResolveInstalledVersion(") -and
    $installerSource.Contains("ShouldAbortInstallAfterLock(") -and
    $installerSource.Contains("lockedInstalledVersion") -and
    $installerSource.Contains(
        "automaticInstallRequested = true;") -and
    $installerSource.Contains(
        "revalidated under the same mutex") -and
    $installerSource.Contains("GetInstallationMutexName(") -and
    $installerSource.Contains("installInProgress") -and
    $installerSource.Contains("OnFormClosing(") -and
    $installerSource.Contains("ComparePublicVersions(") -and
    -not $installerSource.Contains(
        "installedVersion.CompareTo(") -and
    $programSource.Contains('"--update-recovery"') -and
    $programSource.Contains('"--update-busy-recovery"') -and
    $programSource.Contains(
        "RestorePendingLaunchAttemptAfterSetupBusy(")) `
    "Setup serializes transactions, trusts the primary installed executable, and recovers the shell after failed update handoff"
Assert-True ($updateServiceSource.Contains(
        "MaximumDownloadRedirects = 5") -and
    $updateServiceSource.Contains("request.AllowAutoRedirect = false") -and
    $updateServiceSource.Contains(
        "redirect >= MaximumDownloadRedirects") -and
    $updateServiceSource.Contains("FileMode.CreateNew") -and
    $updateServiceSource.Contains("total > MaximumInstallerBytes")) `
    "the network downloader owns a bounded manual redirect loop, exclusive destination creation, and streamed size limit"

$assembly = [Reflection.Assembly]::LoadFrom(
    [IO.Path]::GetFullPath($AssemblyPath))
$staticFlags = [Reflection.BindingFlags]::Static -bor `
    [Reflection.BindingFlags]::NonPublic -bor `
    [Reflection.BindingFlags]::Public
$instanceFlags = [Reflection.BindingFlags]::Instance -bor `
    [Reflection.BindingFlags]::NonPublic -bor `
    [Reflection.BindingFlags]::Public

$settingsType = $assembly.GetType("AirPlayReceiverMvp.AppSettings", $true)
$updateInfoType = $assembly.GetType("AirPlayReceiverMvp.UpdateInfo", $true)
$updateServiceType = $assembly.GetType(
    "AirPlayReceiverMvp.UpdateService", $true)
$automaticServiceType = $assembly.GetType(
    "AirPlayReceiverMvp.AutomaticUpdateService", $true)

$testRoot = [IO.Path]::GetFullPath((Join-Path `
    ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString("N"))))
$testRootInfo = [IO.DirectoryInfo]::new($testRoot)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$safeRootId = [Guid]::Empty
$safeTestRoot = $null -ne $testRootInfo.Parent -and
    $testRootInfo.Parent.FullName.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) -ieq $tempRoot -and
    [Guid]::TryParseExact($testRootInfo.Name, "N", [ref]$safeRootId)
Assert-True $safeTestRoot `
    "the automatic-update suite owns one exact GUID child of process temp"

function New-UpdateInfo(
    [Version]$Version,
    [string]$Digest,
    [string]$Url = "") {
    $info = [Activator]::CreateInstance($updateInfoType, $true)
    $name = "AeroMirror-Setup-$($Version.ToString(3)).exe"
    if ([string]::IsNullOrWhiteSpace($Url)) {
        $Url = "https://github.com/Nadejny/aeromirror/releases/download/" +
            "v$($Version.ToString(3))/$name"
    }
    $updateInfoType.GetField("Version", $instanceFlags).SetValue(
        $info, $Version)
    $updateInfoType.GetField("VersionText", $instanceFlags).SetValue(
        $info, "v$($Version.ToString(3))")
    $updateInfoType.GetField("InstallerName", $instanceFlags).SetValue(
        $info, $name)
    $updateInfoType.GetField("InstallerUrl", $instanceFlags).SetValue(
        $info, $Url)
    $updateInfoType.GetField("InstallerSha256", $instanceFlags).SetValue(
        $info, $Digest)
    $updateInfoType.GetField("IsNewer", $instanceFlags).SetValue(
        $info, $true)
    return $info
}

$setStorageRoot = $settingsType.GetMethod(
    "SetStorageRootForTests", $staticFlags)
$loadSettings = $settingsType.GetMethod("Load", $staticFlags)
$saveSettings = $settingsType.GetMethod("Save", $instanceFlags)
$automaticUpdatesField = $settingsType.GetField(
    "AutomaticUpdates", $instanceFlags)
$parseLatest = $updateServiceType.GetMethod(
    "ParseLatestRelease", $staticFlags)
$downloadAndVerify = Get-MethodByParameterCount `
    $updateServiceType "DownloadAndVerify" 2 $staticFlags
$validateDownloadHop = Get-MethodByParameterCount `
    $updateServiceType "ValidateDownloadHop" 2 $staticFlags
$isAcceptableDownloadedInstallerFile = Get-MethodByParameterCount `
    $updateServiceType "IsAcceptableDownloadedInstallerFile" 2 $staticFlags
$stageInstaller = Get-MethodByParameterCount `
    $automaticServiceType "StageVerifiedInstaller" 3 $staticFlags
$readPending = Get-MethodByParameterCount `
    $automaticServiceType "TryReadUsablePendingUpdate" 5 $staticFlags
$acquirePending = Get-MethodByParameterCount `
    $automaticServiceType "TryAcquirePendingUpdateForLaunch" 4 $staticFlags
$restoreBusyAttempt = Get-MethodByParameterCount `
    $automaticServiceType "RestorePendingLaunchAttemptAfterSetupBusy" 3 `
    $staticFlags
$clearStaged = $automaticServiceType.GetMethod(
    "ClearStagedUpdate", $staticFlags)
$stagingFolderProperty = $automaticServiceType.GetProperty(
    "StagingFolder", $staticFlags)
Assert-True ($null -ne $setStorageRoot -and
    $null -ne $automaticUpdatesField -and
    $null -ne $parseLatest -and
    $null -ne $downloadAndVerify -and
    $null -ne $validateDownloadHop -and
    $null -ne $isAcceptableDownloadedInstallerFile -and
    $null -ne $stageInstaller -and
    $null -ne $readPending -and
    $null -ne $acquirePending -and
    $null -ne $restoreBusyAttempt -and
    $null -ne $clearStaged -and
    $null -ne $stagingFolderProperty) `
    "automatic-update settings, parser, verifier, staging and cleanup seams exist"

try {
    $setStorageRoot.Invoke($null, [object[]]@($testRoot)) | Out-Null

    $settings = $loadSettings.Invoke($null, [object[]]@())
    Assert-True (-not [bool]$automaticUpdatesField.GetValue($settings)) `
        "automatic updates default to off"
    $automaticUpdatesField.SetValue($settings, $true)
    $saveSettings.Invoke($settings, [object[]]@()) | Out-Null
    $reloadedSettings = $loadSettings.Invoke($null, [object[]]@())
    Assert-True ([bool]$automaticUpdatesField.GetValue($reloadedSettings)) `
        "the explicit automatic-update opt-in persists"

    $digestA = "A" * 64
    $json = @"
{
  "tag_name": "v9.8.7",
  "name": "AeroMirror 9.8.7",
  "body": "Verified update",
  "html_url": "https://github.com/Nadejny/aeromirror/releases/tag/v9.8.7",
  "assets": [
    {
      "name": "AeroMirror-Setup-9.8.7-x64.exe",
      "browser_download_url": "https://example.invalid/wrong.exe",
      "digest": "sha256:$digestA"
    },
    {
      "name": "AeroMirror-Setup-9.8.7.exe",
      "browser_download_url": "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe",
      "digest": "sha256:$digestA"
    }
  ]
}
"@
    $parsedInfo = $parseLatest.Invoke(
        $null, [object[]]@($json, [Version]"1.0.0"))
    Assert-True ($updateInfoType.GetField(
            "InstallerName", $instanceFlags).GetValue($parsedInfo) -ceq
            "AeroMirror-Setup-9.8.7.exe" -and
        $updateInfoType.GetField(
            "InstallerSha256", $instanceFlags).GetValue($parsedInfo) -eq
            $digestA -and
        [bool]$updateInfoType.GetField(
            "IsNewer", $instanceFlags).GetValue($parsedInfo)) `
        "offline latest-release parsing selects only the exact versioned Setup asset and digest"

    $unprefixedTagRejected = $false
    try {
        $parseLatest.Invoke(
            $null,
            [object[]]@(
                $json.Replace('"v9.8.7"', '"9.8.7"'),
                [Version]"1.0.0")) | Out-Null
    }
    catch {
        $unprefixedTagRejected = (Get-InnerException $_.Exception) -is
            [IO.InvalidDataException]
    }
    Assert-True $unprefixedTagRejected `
        "an unprefixed release tag is rejected before any installer URL can be trusted"

    $caseMismatchJson = $json.Replace(
        '"AeroMirror-Setup-9.8.7.exe"',
        '"aeromirror-setup-9.8.7.exe"')
    $caseMismatchInfo = $parseLatest.Invoke(
        $null, [object[]]@($caseMismatchJson, [Version]"1.0.0"))
    Assert-True ([string]::IsNullOrWhiteSpace(
        [string]$updateInfoType.GetField(
            "InstallerUrl", $instanceFlags).GetValue($caseMismatchInfo))) `
        "a differently cased asset is not accepted as the exact Setup contract"

    $allowedDownloadHops = @(
        @{ Url = "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe"; Initial = $true },
        @{ Url = "https://release-assets.githubusercontent.com/github-production-release-asset/file?token=signed"; Initial = $false },
        @{ Url = "https://objects.githubusercontent.com/github-production-release-asset/file?token=signed"; Initial = $false },
        @{ Url = "https://media.githubusercontent.com/file?token=signed"; Initial = $false },
        @{ Url = "https://github-production-release-asset-2e65be.s3.amazonaws.com/file?token=signed"; Initial = $false }
    )
    foreach ($hop in $allowedDownloadHops) {
        $validateDownloadHop.Invoke(
            $null,
            [object[]]@([Uri]$hop.Url, [bool]$hop.Initial)) | Out-Null
    }
    $rejectedDownloadHops = @(
        @{ Url = "http://github.com/file"; Initial = $false },
        @{ Url = "https://user@release-assets.githubusercontent.com/file"; Initial = $false },
        @{ Url = "https://release-assets.githubusercontent.com:444/file"; Initial = $false },
        @{ Url = "https://release-assets.githubusercontent.com/file#fragment"; Initial = $false },
        @{ Url = "https://example.invalid/file"; Initial = $false },
        @{ Url = "https://release-assets.githubusercontent.com.evil.invalid/file"; Initial = $false },
        @{ Url = "https://release-assets.githubusercontent.com/file"; Initial = $true }
    )
    foreach ($hop in $rejectedDownloadHops) {
        $rejected = $false
        try {
            $validateDownloadHop.Invoke(
                $null,
                [object[]]@([Uri]$hop.Url, [bool]$hop.Initial)) | Out-Null
        }
        catch {
            $rejected = (Get-InnerException $_.Exception) -is
                [IO.InvalidDataException]
        }
        Assert-True $rejected `
            "an unsafe or misplaced download hop is rejected: $($hop.Url)"
    }
    Assert-True (
        [bool]$isAcceptableDownloadedInstallerFile.Invoke(
            $null,
            [object[]]@([IO.FileAttributes]::Normal, [long](64MB))) -and
        -not [bool]$isAcceptableDownloadedInstallerFile.Invoke(
            $null,
            [object[]]@([IO.FileAttributes]::Normal, [long](64MB + 1))) -and
        -not [bool]$isAcceptableDownloadedInstallerFile.Invoke(
            $null,
            [object[]]@([IO.FileAttributes]::ReparsePoint, [long]1)) -and
        -not [bool]$isAcceptableDownloadedInstallerFile.Invoke(
            $null,
            [object[]]@([IO.FileAttributes]::Directory, [long]1))) `
        "downloaded Setup validation accepts only a regular non-reparse file no larger than 64 MiB"

    $payloadPath = Join-Path $testRoot "verified-payload.bin"
    [IO.File]::WriteAllBytes(
        $payloadPath,
        [Text.Encoding]::UTF8.GetBytes("offline deterministic Setup payload"))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $payloadDigest = [BitConverter]::ToString(
            $sha256.ComputeHash([IO.File]::ReadAllBytes($payloadPath))).Replace(
                "-", "")
    }
    finally {
        $sha256.Dispose()
    }
    $candidateVersion = [Version]"9.8.7"
    $candidate = New-UpdateInfo $candidateVersion $payloadDigest
    $downloadDestinations = [Collections.Generic.List[string]]::new()
    $offlineDownload = [Action[Uri, string]]{
        param([Uri]$Uri, [string]$Destination)
        $downloadDestinations.Add($Destination)
        [IO.File]::Copy($payloadPath, $Destination, $false)
    }
    $verifiedPath = [string]$downloadAndVerify.Invoke(
        $null, [object[]]@($candidate, $offlineDownload))
    try {
        Assert-True ([IO.File]::Exists($verifiedPath) -and
            [IO.File]::ReadAllBytes($verifiedPath).Length -eq
            [IO.File]::ReadAllBytes($payloadPath).Length) `
            "the injected offline downloader passes the same SHA-256 verification path"

        $now = [DateTime]::SpecifyKind(
            [DateTime]"2030-01-02T03:04:05", [DateTimeKind]::Utc)
        $stageInstaller.Invoke(
            $null, [object[]]@($candidate, $verifiedPath, $now)) | Out-Null
        $stagingFolder = [string]$stagingFolderProperty.GetValue($null, $null)
        $stagedPath = Join-Path $stagingFolder `
            "AeroMirror-Setup-9.8.7.exe"
        $manifestPath = Join-Path $stagingFolder "pending-update.dat"
        Assert-True ([IO.File]::Exists($stagedPath) -and
            [IO.File]::Exists($manifestPath) -and
            -not [Text.Encoding]::UTF8.GetString(
                [IO.File]::ReadAllBytes($manifestPath)).Contains("9.8.7")) `
            "staging copies verified bytes and protects persisted launch metadata"

        $busyAcquireArguments = [object[]]@(
            [Version]"1.0.0", $now, $null, $null)
        Assert-True ([bool]$acquirePending.Invoke(
                $null, $busyAcquireArguments)) `
            "a pending Setup handoff reserves one launch attempt"
        $busyRecoveryArguments = [object[]]@(
            [Version]"1.0.0", $now, $null)
        Assert-True ([bool]$restoreBusyAttempt.Invoke(
                $null, $busyRecoveryArguments)) `
            "a verified Setup-busy recovery restores the reserved attempt"
        $afterBusyArguments = [object[]]@(
            [Version]"1.0.0", $now, $null, $null)
        Assert-True ([bool]$acquirePending.Invoke(
                $null, $afterBusyArguments)) `
            "the pending update remains usable after a Setup-busy collision"
        $afterBusyPending = $afterBusyArguments[2]
        $afterBusyLaunchAttempts = $afterBusyPending.GetType().GetField(
            "LaunchAttempts", $instanceFlags)
        Assert-True ([int]$afterBusyLaunchAttempts.GetValue(
                $afterBusyPending) -eq 1) `
            "the Setup-busy collision consumes no launch-attempt budget"
        $busyRecoveryArguments = [object[]]@(
            [Version]"1.0.0", $now, $null)
        Assert-True ([bool]$restoreBusyAttempt.Invoke(
                $null, $busyRecoveryArguments)) `
            "the test restores its reserved attempt before the bounded-attempt matrix"

        for ($attempt = 1; $attempt -le 3; $attempt++) {
            $acquireArguments = [object[]]@(
                [Version]"1.0.0", $now, $null, $null)
            Assert-True ([bool]$acquirePending.Invoke(
                $null, $acquireArguments)) `
                "staged launch attempt $attempt is acquired within the bounded budget"
            $pendingObject = $acquireArguments[2]
            $launchAttemptsField = $pendingObject.GetType().GetField(
                "LaunchAttempts", $instanceFlags)
            Assert-True ([int]$launchAttemptsField.GetValue($pendingObject) -eq
                $attempt) `
                "launch attempt $attempt is persisted before Setup handoff"
        }

        $exhaustedArguments = [object[]]@(
            [Version]"1.0.0", $now, $true, $null, $null)
        Assert-True (-not [bool]$readPending.Invoke(
            $null, $exhaustedArguments) -and
            -not [IO.File]::Exists($stagedPath) -and
            -not [IO.File]::Exists($manifestPath) -and
            [IO.File]::Exists((Join-Path $stagingFolder "retry-update.dat"))) `
            "an exhausted staged launch is cleaned and enters bounded retry backoff"

        $retryBlocked = $false
        try {
            $stageInstaller.Invoke(
                $null, [object[]]@($candidate, $verifiedPath, $now)) | Out-Null
        }
        catch {
            $retryBlocked = (Get-InnerException $_.Exception) -is
                [InvalidOperationException]
        }
        Assert-True $retryBlocked `
            "the same failed candidate is not immediately restaged"

        $afterRetry = $now.AddHours(7)
        $stageInstaller.Invoke(
            $null,
            [object[]]@($candidate, $verifiedPath, $afterRetry)) | Out-Null
        [IO.File]::AppendAllText($stagedPath, "tampered")
        $tamperedArguments = [object[]]@(
            [Version]"1.0.0", $afterRetry, $true, $null, $null)
        Assert-True (-not [bool]$readPending.Invoke(
            $null, $tamperedArguments) -and
            -not [IO.File]::Exists($stagedPath)) `
            "a staged Setup modified after verification is rejected and cleaned"

        $staleTime = $afterRetry.AddDays(-15)
        $stageInstaller.Invoke(
            $null, [object[]]@($candidate, $verifiedPath, $staleTime)) |
            Out-Null
        $staleArguments = [object[]]@(
            [Version]"1.0.0", $afterRetry, $true, $null, $null)
        Assert-True (-not [bool]$readPending.Invoke(
            $null, $staleArguments) -and
            -not [IO.File]::Exists($stagedPath)) `
            "a staged Setup older than the retention window is cleaned for a later fresh retry"

        $stageInstaller.Invoke(
            $null, [object[]]@($candidate, $verifiedPath, $afterRetry)) |
            Out-Null
        $downgradeArguments = [object[]]@(
            [Version]"10.0.0", $afterRetry, $true, $null, $null)
        Assert-True (-not [bool]$readPending.Invoke(
            $null, $downgradeArguments) -and
            -not [IO.File]::Exists($stagedPath)) `
            "a staged version never bypasses the no-downgrade boundary"
    }
    finally {
        if ([IO.File]::Exists($verifiedPath)) {
            [IO.File]::Delete($verifiedPath)
        }
    }

    $badDigest = "0" * 64
    $badCandidate = New-UpdateInfo $candidateVersion $badDigest
    $badDestinations = [Collections.Generic.List[string]]::new()
    $badDownload = [Action[Uri, string]]{
        param([Uri]$Uri, [string]$Destination)
        $badDestinations.Add($Destination)
        [IO.File]::Copy($payloadPath, $Destination, $false)
    }
    $digestRejected = $false
    try {
        $downloadAndVerify.Invoke(
            $null, [object[]]@($badCandidate, $badDownload)) | Out-Null
    }
    catch {
        $digestRejected = (Get-InnerException $_.Exception) -is
            [IO.InvalidDataException]
    }
    Assert-True ($digestRejected -and $badDestinations.Count -eq 1 -and
        -not [IO.File]::Exists($badDestinations[0])) `
        "a digest mismatch deletes the temporary download"

    $wrongUrlCandidate = New-UpdateInfo `
        $candidateVersion $payloadDigest `
        "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/not-the-setup.exe"
    $downloadCalled = $false
    $mustNotDownload = [Action[Uri, string]]{
        param([Uri]$Uri, [string]$Destination)
        $downloadCalled = $true
    }
    $wrongNameRejected = $false
    try {
        $downloadAndVerify.Invoke(
            $null, [object[]]@($wrongUrlCandidate, $mustNotDownload)) |
            Out-Null
    }
    catch {
        $wrongNameRejected = (Get-InnerException $_.Exception) -is
            [InvalidOperationException]
    }
    Assert-True ($wrongNameRejected -and -not $downloadCalled) `
        "a URL without the exact versioned Setup filename is rejected before download"

    $unsafeCandidateUrls = @(
        "http://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe",
        "https://user@github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe",
        "https://github.com:444/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe",
        "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe?download=1",
        "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe#fragment"
    )
    foreach ($unsafeUrl in $unsafeCandidateUrls) {
        $unsafeCandidate = New-UpdateInfo `
            $candidateVersion $payloadDigest $unsafeUrl
        $unsafeDownloadCalled = $false
        $unsafeDownload = [Action[Uri, string]]{
            param([Uri]$Uri, [string]$Destination)
            $unsafeDownloadCalled = $true
        }
        $unsafeRejected = $false
        try {
            $downloadAndVerify.Invoke(
                $null, [object[]]@($unsafeCandidate, $unsafeDownload)) |
                Out-Null
        }
        catch {
            $unsafeRejected = (Get-InnerException $_.Exception) -is
                [InvalidOperationException]
        }
        Assert-True ($unsafeRejected -and -not $unsafeDownloadCalled) `
            "an unsafe initial Setup URL is rejected before download: $unsafeUrl"
    }

    $oversizeDestinations = [Collections.Generic.List[string]]::new()
    $oversizeDownload = [Action[Uri, string]]{
        param([Uri]$Uri, [string]$Destination)
        $oversizeDestinations.Add($Destination)
        $stream = [IO.File]::Open(
            $Destination,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None)
        try {
            $stream.SetLength(64MB + 1)
        }
        finally {
            $stream.Dispose()
        }
    }
    $oversizeRejected = $false
    try {
        $downloadAndVerify.Invoke(
            $null, [object[]]@($candidate, $oversizeDownload)) | Out-Null
    }
    catch {
        $oversizeRejected = (Get-InnerException $_.Exception) -is
            [IO.InvalidDataException]
    }
    Assert-True ($oversizeRejected -and
        $oversizeDestinations.Count -eq 1 -and
        -not [IO.File]::Exists($oversizeDestinations[0])) `
        "an injected 64 MiB plus one byte download is rejected and deleted"

    $clearStaged.Invoke($null, [object[]]@()) | Out-Null
    Assert-True (-not [IO.Directory]::GetFiles(
        (Join-Path $testRoot "automatic-update")).Where({
            $_ -match 'pending-update|retry-update|AeroMirror-Setup-'
        }).Count) `
        "disabling automatic updates can remove all known staged state"
}
finally {
    if ($safeTestRoot -and [IO.Directory]::Exists($testRoot)) {
        [IO.Directory]::Delete($testRoot, $true)
    }
}

Write-Host "Automatic-update checks passed."

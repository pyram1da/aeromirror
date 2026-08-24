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

function Get-InternalMethod(
    [Type]$Type,
    [string]$Name
) {
    $flags = [Reflection.BindingFlags]::Static -bor
        [Reflection.BindingFlags]::NonPublic
    $method = $Type.GetMethod($Name, $flags)
    Assert-True ($null -ne $method) "method $Name exists"
    return $method
}

Assert-True (Test-Path -LiteralPath $AssemblyPath) `
    "compiled AeroMirror assembly exists"

$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$serviceType = $assembly.GetType(
    "AirPlayReceiverMvp.BonjourFirewallService", $true)
$snapshotType = $assembly.GetType(
    "AirPlayReceiverMvp.FirewallRuleSnapshot", $true)
$contextType = $assembly.GetType(
    "AirPlayReceiverMvp.ReceiverContext", $true)
$publicInstanceFlags = [Reflection.BindingFlags]::Instance -bor
    [Reflection.BindingFlags]::Public
foreach ($propertyName in @(
    "IsBonjourFirewallRepairRequired",
    "IsBonjourUnavailable",
    "IsBonjourFirewallRepairRunning"
)) {
    $property = $contextType.GetProperty(
        $propertyName, $publicInstanceFlags)
    Assert-True ($null -ne $property) `
        "ReceiverContext exposes $propertyName"
    Assert-True ($property.PropertyType -eq [bool] -and
        $property.CanRead -and -not $property.CanWrite) `
        "$propertyName is a read-only Boolean UI state"
}

$sourceRoot = Join-Path $projectRoot "src"
$contextSource = [IO.File]::ReadAllText((Join-Path $sourceRoot `
    "Receiver\ReceiverContext.BonjourFirewall.cs"))
$receiverCoreSource = [IO.File]::ReadAllText((Join-Path $sourceRoot `
    "Receiver\ReceiverContext.Core.cs"))
$settingsSource = [IO.File]::ReadAllText((Join-Path $sourceRoot `
    "UI\SettingsForm.cs"))
$repairStart = $contextSource.IndexOf(
    "public void RepairBonjourFirewall(IWin32Window owner)",
    [StringComparison]::Ordinal)
$repairResultStart = $contextSource.IndexOf(
    "private void HandleBonjourFirewallRepairResult()",
    [Math]::Max(0, $repairStart),
    [StringComparison]::Ordinal)
Assert-True ($repairStart -ge 0 -and
    $repairResultStart -gt $repairStart) `
    "Bonjour repair flow has a deterministic source boundary"
$repairSource = $contextSource.Substring(
    $repairStart, $repairResultStart - $repairStart)
$repairResultSource = $contextSource.Substring($repairResultStart)
$repairSuccessDialogText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "0JTQvtGB0YLRg9C/IEJvbmpvdXIg0LjRgdC/0YDQsNCy0LvQtdC9"))
$automaticBonjourServiceText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "QWVyb01pcnJvciDQvdC1INGD0YHRgtCw0L3QsNCy0LvQuNCy0LDQtdGCINGB0LjRgdGC0LXQvNC90YPRjiDRgdC70YPQttCx0YMg0LDQstGC0L7QvNCw0YLQuNGH0LXRgdC60Lg="))
$bonjourUnavailableText = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "Qm9uam91ciDQvdC10LTQvtGB0YLRg9C/0LXQvSDQuNC70Lgg0YPRgdGC0LDQvdC+0LLQu9C10L0g0L3QtdC60L7RgNGA0LXQutGC0L3Qvi4="))
$falseBonjourDiagnosis = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String(
        "Qm9uam91ciDQvdC1INGD0YHRgtCw0L3QvtCy0LvQtdC9Lg=="))
Assert-True (-not $repairSource.Contains("MessageBoxButtons.YesNo") -and
    $repairSource.Contains("RepairPrivateMdnsRuleExplicitlyWithUac")) `
    "an explicit repair click reaches the narrow UAC operation without a second app confirmation"
Assert-True ($repairResultSource.Contains("RefreshDiscovery();") -and
    $repairResultSource.Contains("form.SyncStatus();") -and
    -not $repairResultSource.Contains($repairSuccessDialogText)) `
    "successful repair refreshes discovery and UI without a success message box"
Assert-True ($settingsSource.Contains(
        "context.RepairBonjourFirewall(this);") -and
    $settingsSource.Contains("context.IsBonjourUnavailable") -and
    $settingsSource.Contains(
        "bonjourFirewallRepair.Visible = false;") -and
    $settingsSource.Contains(
        $automaticBonjourServiceText)) `
    "the main network card distinguishes missing-rule repair from unavailable Bonjour"
Assert-True ($contextSource.Contains(
        "BonjourFirewallAssessmentLifetime") -and
    $contextSource.Contains(
        "bonjourFirewallAssessmentCompletedUtc") -and
    $contextSource.Contains(
        "DateTime.UtcNow - bonjourFirewallAssessmentCompletedUtc") -and
    $contextSource.Contains(
        "private void RefreshBonjourFirewallAssessment()") -and
    [regex]::Matches(
        $receiverCoreSource,
        'RefreshBonjourFirewallAssessment\s*\(').Count -eq 3) `
    "Bonjour assessment expires and Start, Restart, and Refresh Discovery explicitly reassess it"
Assert-True ($settingsSource.Contains($bonjourUnavailableText) -and
    -not $settingsSource.Contains($falseBonjourDiagnosis)) `
    "the unavailable card does not falsely claim that Bonjour is definitely absent"

$parseMethod = Get-InternalMethod $serviceType `
    "TryParseBonjourServiceImagePath"
$matchMethod = Get-InternalMethod $serviceType `
    "IsExpectedPrivateMdnsRule"
$addMethod = Get-InternalMethod $serviceType "BuildAddRuleArguments"

function Parse-BonjourPath([string]$Raw) {
    [object[]]$arguments = @($Raw, $null)
    $success = [bool]$parseMethod.Invoke($null, $arguments)
    return [pscustomobject]@{
        Success = $success
        Path = [string]$arguments[1]
    }
}

function New-RuleSnapshot(
    [string]$Name = "Equivalent third-party rule",
    [bool]$Enabled = $true,
    [int]$Direction = 1,
    [int]$Action = 1,
    [int]$Protocol = 17,
    [int]$Profiles = 2,
    [string]$ApplicationName = "C:\Program Files\Bonjour\mDNSResponder.exe",
    [string]$LocalPorts = "5353",
    [string]$RemoteAddresses = "LocalSubnet",
    [bool]$EdgeTraversal = $false
) {
    $snapshot = [Activator]::CreateInstance($snapshotType, $true)
    $values = @{
        Name = $Name
        Enabled = $Enabled
        Direction = $Direction
        Action = $Action
        Protocol = $Protocol
        Profiles = $Profiles
        ApplicationName = $ApplicationName
        LocalPorts = $LocalPorts
        RemoteAddresses = $RemoteAddresses
        EdgeTraversal = $EdgeTraversal
    }
    $flags = [Reflection.BindingFlags]::Instance -bor
        [Reflection.BindingFlags]::NonPublic
    foreach ($entry in $values.GetEnumerator()) {
        $field = $snapshotType.GetField($entry.Key, $flags)
        Assert-True ($null -ne $field) "snapshot field $($entry.Key) exists"
        $field.SetValue($snapshot, $entry.Value)
    }
    return $snapshot
}

function Test-Rule(
    [object]$Rule,
    [string]$ExpectedPath = "C:\Program Files\Bonjour\mDNSResponder.exe"
) {
    return [bool]$matchMethod.Invoke(
        $null,
        @($Rule, $ExpectedPath))
}

$validQuoted = Parse-BonjourPath `
    '"C:\Program Files\Bonjour\mDNSResponder.exe"'
Assert-True $validQuoted.Success "quoted absolute Bonjour ImagePath is accepted"
Assert-True ($validQuoted.Path -eq `
    "C:\Program Files\Bonjour\mDNSResponder.exe") `
    "quoted Bonjour ImagePath is normalized without quotes"

$validSimple = Parse-BonjourPath "C:\Bonjour\mDNSResponder.exe"
Assert-True $validSimple.Success `
    "unquoted Bonjour ImagePath without whitespace is accepted"

$invalidImagePaths = @(
    "",
    "C:\Program Files\Bonjour\mDNSResponder.exe",
    '"C:\Program Files\Bonjour\mDNSResponder.exe" -service',
    "%ProgramFiles%\Bonjour\mDNSResponder.exe",
    ".\mDNSResponder.exe",
    "\\server\share\mDNSResponder.exe",
    "C:\Bonjour\other.exe",
    "C:\Bonjour\mDNSResponder.exe:payload",
    "C:\Bonjour\mDNSResponder.exe`r`nprofile=public"
)
foreach ($invalid in $invalidImagePaths) {
    $parsed = Parse-BonjourPath $invalid
    Assert-True (-not $parsed.Success) `
        "unsafe ImagePath is rejected: $invalid"
}

$equivalent = New-RuleSnapshot
Assert-True (Test-Rule $equivalent) `
    "an exact enabled Private UDP 5353 LocalSubnet allow rule is accepted"
Assert-True (Test-Rule $equivalent `
    "c:\program files\bonjour\MDNSRESPONDER.EXE") `
    "exact executable comparison is case-insensitive"

$negativeRules = @(
    (New-RuleSnapshot -Enabled $false),
    (New-RuleSnapshot -Direction 2),
    (New-RuleSnapshot -Action 0),
    (New-RuleSnapshot -Protocol 6),
    (New-RuleSnapshot -Protocol 256),
    (New-RuleSnapshot -Profiles 4),
    (New-RuleSnapshot -Profiles 3),
    (New-RuleSnapshot -ApplicationName "C:\Other\mDNSResponder.exe"),
    (New-RuleSnapshot -LocalPorts "Any"),
    (New-RuleSnapshot -LocalPorts "5353,5354"),
    (New-RuleSnapshot -RemoteAddresses "Any"),
    (New-RuleSnapshot -RemoteAddresses "LocalSubnet,Internet"),
    (New-RuleSnapshot -EdgeTraversal $true)
)
foreach ($rule in $negativeRules) {
    Assert-True (-not (Test-Rule $rule)) `
        "a broadened or nonmatching firewall rule is rejected"
}

$addArguments = [string]$addMethod.Invoke(
    $null,
    @("C:\Program Files\Bonjour\mDNSResponder.exe"))
Assert-True ($addArguments -eq (
    'advfirewall firewall add rule ' +
    'name="AeroMirror Bonjour mDNS (Private)" ' +
    'dir=in action=allow enable=yes profile=private ' +
    'protocol=UDP localport=5353 remoteip=LocalSubnet ' +
    'edge=no program="C:\Program Files\Bonjour\mDNSResponder.exe"')) `
    "repair command is deterministic and narrowly scoped"
Assert-True (-not ($addArguments -match '(?i)profile=public')) `
    "repair never enables the Public profile"
Assert-True (-not ($addArguments -match '(?i)protocol=(tcp|any)')) `
    "repair never uses TCP or any protocol"
Assert-True (-not ($addArguments -match '(?i)(localport|remoteip)=any')) `
    "repair never uses any local port or remote address"

$unsafeInvocationRejected = $false
try {
    $addMethod.Invoke(
        $null,
        @('C:\Bonjour\mDNSResponder.exe" profile=public')) | Out-Null
}
catch {
    $current = $_.Exception
    while ($null -ne $current -and
        -not ($current -is [ArgumentException])) {
        $current = $current.InnerException
    }
    $unsafeInvocationRejected = $current -is [ArgumentException]
}
Assert-True $unsafeInvocationRejected `
    "command construction rejects quote-based argument injection"

Write-Host "Bonjour firewall tests passed: strict ImagePath, exact rule matcher, narrow UAC command, and main-card UX state."

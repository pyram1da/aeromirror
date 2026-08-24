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

Write-Host "Bonjour firewall tests passed: strict ImagePath, exact rule matcher, and narrow UAC command specification."

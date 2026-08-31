param([string]$AssemblyPath = "")

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot "artifacts\Release\AeroMirror.exe"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
}

function Get-InternalMethod([Type]$Type, [string]$Name) {
    $flags = [Reflection.BindingFlags]::Static -bor
        [Reflection.BindingFlags]::NonPublic
    $method = $Type.GetMethod($Name, $flags)
    Assert-True ($null -ne $method) "method $Name exists"
    return $method
}

function Decode-Utf8([string]$Base64) {
    return [Text.Encoding]::UTF8.GetString(
        [Convert]::FromBase64String($Base64))
}

Assert-True (Test-Path -LiteralPath $AssemblyPath) (
    "compiled AeroMirror assembly exists")
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$serviceType = $assembly.GetType(
    "AirPlayReceiverMvp.BonjourFirewallService", $true)
$snapshotType = $assembly.GetType(
    "AirPlayReceiverMvp.FirewallRuleSnapshot", $true)
$serviceRecoveryType = $assembly.GetType(
    "AirPlayReceiverMvp.BonjourServiceRecoveryService", $true)
$contextType = $assembly.GetType(
    "AirPlayReceiverMvp.ReceiverContext", $true)
$publicInstanceFlags = [Reflection.BindingFlags]::Instance -bor
    [Reflection.BindingFlags]::Public

foreach ($propertyName in @(
    "IsBonjourFirewallRepairRequired",
    "IsBonjourUnavailable",
    "IsBonjourServiceRecoveryRequired",
    "IsBonjourServiceStarting",
    "IsBonjourServiceStatusUnknown"
)) {
    $property = $contextType.GetProperty(
        $propertyName, $publicInstanceFlags)
    Assert-True ($null -ne $property) (
        "ReceiverContext exposes read-only state $propertyName")
    Assert-True ($property.PropertyType -eq [bool] -and
        $property.CanRead -and -not $property.CanWrite) (
        "$propertyName remains a read-only Boolean")
}
foreach ($removedProperty in @(
    "IsBonjourFirewallRepairRunning",
    "IsBonjourServiceRecoveryRunning"
)) {
    Assert-True ($null -eq $contextType.GetProperty(
        $removedProperty, $publicInstanceFlags)) (
        "$removedProperty is absent with the manual repair workflow")
}

$sourceRoot = Join-Path $projectRoot "src"
$contextSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Receiver\ReceiverContext.BonjourFirewall.cs")))
$receiverSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Receiver\ReceiverContext.cs")))
$receiverCoreSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Receiver\ReceiverContext.Core.cs")))
$settingsSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "UI\SettingsForm.cs")))
$programSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Application\Program.cs")))
$firewallServiceSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Network\BonjourFirewallService.cs")))
$serviceRecoverySource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Network\BonjourServiceRecoveryService.cs")))
$installerSource = [IO.File]::ReadAllText((Join-Path $projectRoot (
    "installer\AirPlayReceiverSetup.cs")))

Assert-True (-not $contextSource.Contains("RepairBonjour") -and
    -not $contextSource.Contains("RecoverBonjour") -and
    -not $contextSource.Contains("MessageBox.Show") -and
    -not $serviceRecoverySource.Contains("ProcessStartInfo") -and
    -not $serviceRecoverySource.Contains('Verb = "runas"') -and
    -not $firewallServiceSource.Contains("RunNetshElevated") -and
    -not $firewallServiceSource.Contains(
        "RepairPrivateMdnsRuleExplicitlyWithUac") -and
    -not $programSource.Contains("bonjour-machine")) (
    "ordinary app observes Bonjour without machine mutation or elevation")

Assert-True (-not $settingsSource.Contains("refreshDiscovery") -and
    -not $settingsSource.Contains("bonjourFirewallRepair") -and
    -not $receiverSource.Contains("bonjourFirewallItem") -and
    $settingsSource.Contains("bonjourServiceRecoveryRequired") -and
    $settingsSource.Contains("bonjourServiceStarting") -and
    $settingsSource.Contains("IsBonjourServiceStatusUnknown")) (
    "main window and tray have no manual discovery or Bonjour action")

$oldStoppedPromise = Decode-Utf8 (
    "V2luZG93cyDQstC+0YHRgdGC0LDQvdC+0LLQuNGCINGB0LvRg9C20LHRgw==")
$oldFirewallPromise = Decode-Utf8 (
    "0LLQvtGB0YHRgtCw0L3QvtCy0LjRgiDRg9C30LrQvtC1INC/0YDQsNCy0LjQu9C+INC+0LHQvdCw0YDRg9C20LXQvdC40Y8g0LDQstGC0L7QvNCw0YLQuNGH0LXRgdC60Lg=")
$oldReinstallPromise = Decode-Utf8 (
    "0J/QtdGA0LXRg9GB0YLQsNC90L7QstC60LAgQWVyb01pcnJvciDQstC+0YHRgdGC0LDQvdC+0LLQuNGC")
$oldDiscoveryPromise = Decode-Utf8 (
    "QWVyb01pcnJvciDQstC+0YHRgdGC0LDQvdC+0LLQuNGCINC+0LHQvdCw0YDRg9C20LXQvdC40LUg0LDQstGC0L7QvNCw0YLQuNGH0LXRgdC60Lg=")
$existingAppleOnly = Decode-Utf8 (
    "U2V0dXAg0L3QsNGB0YLRgNCw0LjQstCw0LXRgiDRgtC+0LvRjNC60L4g0YPQttC1INGD0YHRgtCw0L3QvtCy0LvQtdC90L3Rg9GOINC/0L7QtNC70LjQvdC90YPRjiDRgdC70YPQttCx0YMgQXBwbGUgQm9uam91cg==")
$trustedSource = Decode-Utf8 (
    "0JLQvtGB0YHRgtCw0L3QvtCy0LjRgtC1IEFwcGxlIEJvbmpvdXIg0LjQtyDQtNC+0LLQtdGA0LXQvdC90L7Qs9C+INC40YHRgtC+0YfQvdC40LrQsA==")
$rerunSetup = Decode-Utf8 (
    "0JXRgdC70Lgg0YHQu9GD0LbQsdCwINC90LUg0LLQtdGA0L3Rg9C70LDRgdGMLCDRgdC90L7QstCwINC30LDQv9GD0YHRgtC40YLQtSBTZXR1cA==")
$rerunSetupOrDiagnostics = Decode-Utf8 (
    "0JXRgdC70Lgg0YHQu9GD0LbQsdCwINC90LUg0LLQtdGA0L3Rg9C70LDRgdGMLCDRgdC90L7QstCwINC30LDQv9GD0YHRgtC40YLQtSBTZXR1cCDQuNC70Lgg0L7RgtC60YDQvtC50YLQtSDQtNC40LDQs9C90L7RgdGC0LjQutGD")
Assert-True (-not $contextSource.Contains($oldStoppedPromise) -and
    -not $contextSource.Contains($oldFirewallPromise) -and
    -not $settingsSource.Contains($oldReinstallPromise) -and
    -not $settingsSource.Contains($oldDiscoveryPromise) -and
    $settingsSource.Contains($existingAppleOnly) -and
    $settingsSource.Contains($trustedSource) -and
    $settingsSource.Contains($rerunSetup) -and
    $contextSource.Contains($rerunSetupOrDiagnostics)) (
    "Bonjour UI states the read-only boundary and conditional recovery steps")

Assert-True ($contextSource.Contains(
        "BonjourFirewallAssessmentLifetime") -and
    $contextSource.Contains("bonjourFirewallAssessmentGeneration") -and
    $contextSource.Contains("if (stale)") -and
    $contextSource.Contains(
        "DateTime.UtcNow - bonjourFirewallAssessmentCompletedUtc") -and
    $contextSource.Contains(
        "BonjourServiceRecoveryService.Assess();") -and
    $receiverCoreSource.Contains(
        "private void HandleBonjourServiceRecoveryMonitor()") -and
    $receiverCoreSource.Contains("now.AddSeconds(3).Ticks") -and
    $receiverCoreSource.Contains("attempt >= 2") -and
    $receiverCoreSource.Contains("TryRequestNativeDiscoveryRefresh(") -and
    $receiverCoreSource.Contains(
        '"Bonjour service recovered", false')) (
    "Bonjour is rechecked and uses at most two same-process refreshes")

Assert-True ($firewallServiceSource.Contains(
        "Registry.LocalMachine.OpenSubKey(") -and
    $firewallServiceSource.Contains(
        "RegistryValueOptions.DoNotExpandEnvironmentNames") -and
    $firewallServiceSource.Contains("File.Exists(parsed)") -and
    $firewallServiceSource.Contains(
        "Multiple Bonjour service identities were found.") -and
    $firewallServiceSource.Contains("FileSystemRights.WriteData") -and
    $firewallServiceSource.Contains("FileSystemRights.AppendData") -and
    $firewallServiceSource.Contains(
        "FileSystemRights.DeleteSubdirectoriesAndFiles") -and
    $firewallServiceSource.Contains("PropagationFlags.InheritOnly")) (
    "assessment validates exact HKLM identity and protected path")

$elevatedDispatch = $installerSource.IndexOf(
    "if (IsExactBonjourMachineConfigurationInvocation(args))",
    [StringComparison]::Ordinal)
$ordinarySetupStart = $installerSource.IndexOf(
    "string originalWorkingDirectory = Environment.CurrentDirectory;",
    [StringComparison]::Ordinal)
Assert-True ($elevatedDispatch -ge 0 -and
    $ordinarySetupStart -gt $elevatedDispatch -and
    $installerSource.Contains("args.Length == 1") -and
    $installerSource.Contains(
        "Program.BonjourMachineConfigurationArgument")) (
    "the exact elevated helper dispatch precedes log, UI, and user-path work")

$launchStart = $installerSource.IndexOf(
    "CreateBonjourMachineConfigurationStartInfo()",
    [StringComparison]::Ordinal)
$launchEnd = $installerSource.IndexOf(
    "internal static void ConfigureBonjourMachineElevated()",
    $launchStart,
    [StringComparison]::Ordinal)
Assert-True ($launchStart -ge 0 -and $launchEnd -gt $launchStart) (
    "bounded Bonjour helper-launch source slice exists")
$launchSource = $installerSource.Substring(
    $launchStart, $launchEnd - $launchStart)
Assert-True ($launchSource.Contains(
        "Assembly.GetExecutingAssembly().Location") -and
    $launchSource.Contains(
        "Arguments = Program.BonjourMachineConfigurationArgument") -and
    $launchSource.Contains('Verb = "runas"') -and
    $launchSource.Contains("UseShellExecute = true") -and
    -not $launchSource.Contains('FileName = "AeroMirror.exe"')) (
    "administrator launch is fixed to this Setup and its one private mode")

$elevatedCoreStart = $installerSource.IndexOf(
    "private static void ConfigureBonjourMachineElevatedCore()",
    [StringComparison]::Ordinal)
$elevatedCoreEnd = $installerSource.IndexOf(
    "private static void EnsureBonjourIdentityUnchanged(",
    $elevatedCoreStart,
    [StringComparison]::Ordinal)
Assert-True ($elevatedCoreStart -ge 0 -and
    $elevatedCoreEnd -gt $elevatedCoreStart) (
    "elevated Bonjour core source slice exists")
$elevatedCoreSource = $installerSource.Substring(
    $elevatedCoreStart, $elevatedCoreEnd - $elevatedCoreStart)
Assert-True ($elevatedCoreSource.Contains(
        "TryResolveBonjourServiceIdentity(") -and
    $elevatedCoreSource.Contains(
        "EnsureBonjourIdentityUnchanged(serviceName, executablePath);") -and
    $elevatedCoreSource.Contains(
        "ConfigureBonjourServicePolicy(serviceName);") -and
    $elevatedCoreSource.Contains(
        "ConfigureBonjourFirewallRule(executablePath);") -and
    $elevatedCoreSource.Contains("StartBonjourService(serviceName);") -and
    $elevatedCoreSource.Contains("WaitForBonjourRunning(serviceName)")) (
    "elevated core revalidates identity around the exact SCM and firewall work")

$servicePolicyStart = $installerSource.IndexOf(
    "private static void ConfigureBonjourServicePolicy(string serviceName)",
    [StringComparison]::Ordinal)
$servicePolicyEnd = $installerSource.IndexOf(
    "private static void StartBonjourService(string serviceName)",
    $servicePolicyStart,
    [StringComparison]::Ordinal)
Assert-True ($servicePolicyStart -ge 0 -and
    $servicePolicyEnd -gt $servicePolicyStart) (
    "Bonjour SCM policy source slice exists")
$servicePolicySource = $installerSource.Substring(
    $servicePolicyStart, $servicePolicyEnd - $servicePolicyStart)
Assert-True ($servicePolicySource.Contains("OpenSCManager(") -and
    $servicePolicySource.Contains("OpenService(") -and
    $servicePolicySource.Contains("ChangeServiceConfig(") -and
    $servicePolicySource.Contains(
        "ChangeServiceConfig2FailureActions(") -and
    $servicePolicySource.Contains(
        "ChangeServiceConfig2FailureFlag(") -and
    $servicePolicySource.Contains("CreateBonjourRestartActions()") -and
    $servicePolicySource.Contains("CreateBonjourFailureActionsFlag()")) (
    "the production service-policy path uses the verified direct SCM sequence")
Assert-True ($installerSource.Contains("private const int ScActionNone = 0;") -and
    $installerSource.Contains(
        "BonjourRestartDelaysMilliseconds.Length + 1") -and
    $installerSource.Contains("Type = ScActionNone") -and
    $installerSource.Contains("terminalAction.Type != ScActionNone") -and
    $installerSource.Contains("terminalAction.Delay != 0")) (
    "Bonjour recovery ends after three restarts with an explicit SC_ACTION_NONE")

$firewallPolicyStart = $installerSource.IndexOf(
    "private static void ConfigureBonjourFirewallRule(",
    [StringComparison]::Ordinal)
$firewallPolicyEnd = $installerSource.IndexOf(
    "private static int CountOwnedBonjourFirewallRules(",
    $firewallPolicyStart,
    [StringComparison]::Ordinal)
Assert-True ($firewallPolicyStart -ge 0 -and
    $firewallPolicyEnd -gt $firewallPolicyStart) (
    "Bonjour firewall policy source slice exists")
$firewallPolicySource = $installerSource.Substring(
    $firewallPolicyStart, $firewallPolicyEnd - $firewallPolicyStart)
foreach ($firewallContract in @(
    '"ApplicationName", executablePath',
    '"Protocol", 17',
    '"LocalPorts", "5353"',
    '"RemoteAddresses", "LocalSubnet"',
    '"Direction", 1',
    '"Enabled", true',
    '"Profiles", 2',
    '"Action", 1',
    '"EdgeTraversal", false'
)) {
    Assert-True ($firewallPolicySource.Contains($firewallContract)) (
        "firewall writer keeps exact contract: $firewallContract")
}

Assert-True ($installerSource.Contains(
        'DllImport("advapi32.dll"') -and
    $installerSource.Contains("OpenSCManager(") -and
    $installerSource.Contains("OpenService(") -and
    $installerSource.Contains("ChangeServiceConfig(") -and
    $installerSource.Contains(
        "ChangeServiceConfig2FailureActions(") -and
    $installerSource.Contains(
        "ChangeServiceConfig2FailureFlag(") -and
    $installerSource.Contains("StartService(") -and
    [Regex]::IsMatch(
        $installerSource,
        'Type\.GetTypeFromProgID\(\s*"HNetCfg\.FwPolicy2"') -and
    [Regex]::IsMatch(
        $installerSource,
        'Type\.GetTypeFromProgID\(\s*"HNetCfg\.FWRule"')) (
    "machine changes use direct SCM and Windows Firewall APIs")

Assert-True (-not [Regex]::IsMatch(
        $installerSource,
        '(?i)["''](?:sc|netsh|cmd|powershell)(?:\.exe)?["'']') -and
    ([Regex]::Matches(
        $installerSource,
        'Verb\s*=\s*"runas"').Count -eq 1) -and
    -not $elevatedCoreSource.Contains("Process.Start") -and
    -not $elevatedCoreSource.Contains("AeroMirror.exe")) (
    "Bonjour elevation cannot invoke sc, netsh, a command shell, or AeroMirror")

$installStart = $installerSource.IndexOf(
    "internal static string Install(bool startMenu, bool desktop)",
    [StringComparison]::Ordinal)
$installEnd = $installerSource.IndexOf(
    "private static void PreparePinnedRuntime(",
    $installStart,
    [StringComparison]::Ordinal)
Assert-True ($installStart -ge 0 -and $installEnd -gt $installStart) (
    "installer transaction source slice exists")
$installSource = $installerSource.Substring(
    $installStart, $installEnd - $installStart)
$rollbackEnd = $installSource.IndexOf(
    "Installation metadata rollback failed:",
    [StringComparison]::Ordinal)
$commitMarker = $installSource.IndexOf(
    "App installation is committed at this point.",
    [StringComparison]::Ordinal)
$machineCall = $installSource.IndexOf(
    "EnsureBonjourAutomaticRecovery();",
    [StringComparison]::Ordinal)
$installedReturn = $installSource.IndexOf(
    "return installedExecutable;",
    [StringComparison]::Ordinal)
Assert-True ($rollbackEnd -ge 0 -and
    $commitMarker -gt $rollbackEnd -and
    $machineCall -gt $commitMarker -and
    $installedReturn -gt $machineCall -and
    $installSource.Contains(
        "skipped after an unexpected error:")) (
    "best-effort machine work runs only after commit and cannot trigger rollback")

Assert-True ($installerSource.Contains(
        "BonjourElevatedSelfTimeoutMilliseconds") -and
    $installerSource.Contains("BonjourHelperTimeoutMilliseconds") -and
    $installerSource.Contains("BonjourServiceWaitMilliseconds") -and
    $installerSource.Contains(
        "process.WaitForExit(BonjourHelperTimeoutMilliseconds)") -and
    $installerSource.Contains(
        "BonjourElevatedSelfTimeoutMilliseconds,") -and
    $installerSource.Contains("Environment.FailFast(") -and
    $installerSource.Contains("TerminateProcessAndWait(process)") -and
    $installerSource.Contains(
        "CreateBonjourRestartActions()") -and
    $installerSource.Contains(
        "CreateBonjourFailureActionsFlag()") -and
    $installerSource.Contains(
        "VerifyBonjourSecurityDescriptorLogic();") -and
    $installerSource.Contains("FileAttributes.ReparsePoint") -and
    $installerSource.Contains("raw.DiscretionaryAcl != null") -and
    $installerSource.Contains("IsTrustedMachineWriter(owner)")) (
    "self-check covers bounded timeouts, recovery sequence, owner, ACL, and reparse policy")

$staticFlags = [Reflection.BindingFlags]::Static -bor
    [Reflection.BindingFlags]::NonPublic
$knownNamesField = $serviceType.GetField(
    "BonjourServiceNames", $staticFlags)
Assert-True ($null -ne $knownNamesField) (
    "known Bonjour service identities are explicit")
[string[]]$knownNames = $knownNamesField.GetValue($null)
Assert-True ($knownNames.Count -eq 2 -and
    $knownNames -contains "Bonjour Service" -and
    $knownNames -contains "mDNSResponder") (
    "only two known Bonjour service names are accepted")

$isKnownMethod = Get-InternalMethod $serviceRecoveryType "IsKnownServiceName"
foreach ($knownName in $knownNames) {
    Assert-True ([bool]$isKnownMethod.Invoke($null, @($knownName))) (
        "known service name is accepted: $knownName")
}
foreach ($unsafeName in @(
    "bonjour service",
    "Bonjour Service ",
    'Bonjour Service" failure reset= 0',
    "Other Service"
)) {
    Assert-True (-not [bool]$isKnownMethod.Invoke($null, @($unsafeName))) (
        "unsafe service name is rejected: $unsafeName")
}

$isExpectedBonjourPath = Get-InternalMethod $serviceType (
    "IsExpectedBonjourExecutablePath")
foreach ($expectedPath in @(
    "C:\Program Files\Bonjour\mDNSResponder.exe",
    "C:\Program Files (x86)\Bonjour\mDNSResponder.exe"
)) {
    Assert-True ([bool]$isExpectedBonjourPath.Invoke(
            $null,
            @($expectedPath, "C:\Program Files", "C:\Program Files (x86)"))) (
        "exact Program Files Bonjour path is accepted: $expectedPath")
}
foreach ($unexpectedPath in @(
    "C:\Program Files\Other\mDNSResponder.exe",
    "C:\Program Files\Bonjour\sub\mDNSResponder.exe",
    "C:\Program Files\Bonjour\mDNSResponder-copy.exe"
)) {
    Assert-True (-not [bool]$isExpectedBonjourPath.Invoke(
            $null,
            @($unexpectedPath, "C:\Program Files", "C:\Program Files (x86)"))) (
        "noncanonical protected path is rejected: $unexpectedPath")
}

$parseMethod = Get-InternalMethod $serviceType (
    "TryParseBonjourServiceImagePath")
$matchMethod = Get-InternalMethod $serviceType (
    "IsExpectedPrivateMdnsRule")

function Parse-BonjourPath([string]$Raw) {
    [object[]]$arguments = @($Raw, $null)
    $success = [bool]$parseMethod.Invoke($null, $arguments)
    return [pscustomobject]@{
        Success = $success
        Path = [string]$arguments[1]
    }
}

function New-RuleSnapshot(
    [bool]$Enabled = $true,
    [int]$Direction = 1,
    [int]$Action = 1,
    [int]$Protocol = 17,
    [int]$Profiles = 2,
    [string]$ApplicationName =
        "C:\Program Files\Bonjour\mDNSResponder.exe",
    [string]$LocalPorts = "5353",
    [string]$RemoteAddresses = "LocalSubnet",
    [bool]$EdgeTraversal = $false
) {
    $snapshot = [Activator]::CreateInstance($snapshotType, $true)
    $values = @{
        Name = "Equivalent third-party rule"
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
        $snapshotType.GetField(
            $entry.Key, $flags).SetValue($snapshot, $entry.Value)
    }
    return $snapshot
}

function Test-Rule([object]$Rule) {
    return [bool]$matchMethod.Invoke(
        $null,
        @($Rule, "C:\Program Files\Bonjour\mDNSResponder.exe"))
}

$valid = Parse-BonjourPath (
    '"C:\Program Files\Bonjour\mDNSResponder.exe"')
Assert-True ($valid.Success -and $valid.Path -eq
    "C:\Program Files\Bonjour\mDNSResponder.exe") (
    "quoted absolute Bonjour ImagePath is accepted")
foreach ($invalid in @(
    "",
    "C:\Program Files\Bonjour\mDNSResponder.exe",
    '"C:\Program Files\Bonjour\mDNSResponder.exe" -service',
    "%ProgramFiles%\Bonjour\mDNSResponder.exe",
    ".\mDNSResponder.exe",
    "\\server\share\mDNSResponder.exe",
    "C:\Bonjour\other.exe",
    "C:\Bonjour\mDNSResponder.exe:payload"
)) {
    Assert-True (-not (Parse-BonjourPath $invalid).Success) (
        "unsafe ImagePath is rejected: $invalid")
}

Assert-True (Test-Rule (New-RuleSnapshot)) (
    "exact Private UDP 5353 LocalSubnet rule is accepted")
foreach ($rule in @(
    (New-RuleSnapshot -Enabled $false),
    (New-RuleSnapshot -Direction 2),
    (New-RuleSnapshot -Action 0),
    (New-RuleSnapshot -Protocol 6),
    (New-RuleSnapshot -Profiles 4),
    (New-RuleSnapshot -Profiles 3),
    (New-RuleSnapshot -ApplicationName "C:\Other\mDNSResponder.exe"),
    (New-RuleSnapshot -LocalPorts "Any"),
    (New-RuleSnapshot -LocalPorts "5353,5354"),
    (New-RuleSnapshot -RemoteAddresses "Any"),
    (New-RuleSnapshot -RemoteAddresses "LocalSubnet,Internet"),
    (New-RuleSnapshot -EdgeTraversal $true)
)) {
    Assert-True (-not (Test-Rule $rule)) (
        "broadened or nonmatching firewall rule is rejected")
}

Write-Host (
    "Bonjour prerequisite tests passed: read-only app assessment, " +
    "background recovery monitor, no manual UI action, and exact firewall.")

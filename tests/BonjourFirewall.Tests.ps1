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
    "IsBonjourServiceStopping",
    "IsBonjourServicePausePending",
    "IsBonjourServicePaused",
    "IsBonjourServiceStatusUnknown",
    "IsBonjourServiceRecoveryRunning",
    "CanRequestBonjourServiceRecovery"
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
    "IsBonjourFirewallRepairRunning"
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
$diagnosticsSource = [IO.File]::ReadAllText((Join-Path $sourceRoot (
    "Receiver\ReceiverContext.Diagnostics.cs")))
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
$startBonjourText = Decode-Utf8 (
    "0JfQsNC/0YPRgdGC0LjRgtGMIEJvbmpvdXI=")
$oneAdministratorConfirmationText = Decode-Utf8 (
    "V2luZG93cyDQvtC00LjQvSDRgNCw0Lcg0L/QvtC/0YDQvtGB0LjRgiDQv9C+0LTRgtCy0LXRgNC20LTQtdC90LjQtSDQsNC00LzQuNC90LjRgdGC0YDQsNGC0L7RgNCw")
$noAutomaticRepeatText = Decode-Utf8 (
    "QWVyb01pcnJvciDQvdC1INC/0L7QstGC0L7RgNGP0LXRgiDRjdGC0L7RgiDQt9Cw0L/RgNC+0YEg0LDQstGC0L7QvNCw0YLQuNGH0LXRgdC60Lg=")

Assert-True ($contextSource.Contains(
        "public void RequestBonjourServiceRecovery()") -and
    $contextSource.Contains(
        "BonjourServiceRecoveryService.TryLaunchExplicitStart(") -and
    -not $contextSource.Contains("MessageBox.Show") -and
    $serviceRecoverySource.Contains("ProcessStartInfo") -and
    $serviceRecoverySource.Contains("Environment.SystemDirectory") -and
    $serviceRecoverySource.Contains(
        "Environment.SpecialFolder.Windows") -and
    $serviceRecoverySource.Contains('Path.Combine(systemDirectory, "sc.exe")') -and
    $serviceRecoverySource.Contains(
        "BonjourFirewallService.IsTrustedMachinePath(") -and
    $serviceRecoverySource.Contains('Arguments = "start \"" + serviceName + "\""') -and
    $serviceRecoverySource.Contains('Verb = "runas"') -and
    $serviceRecoverySource.Contains("UseShellExecute = true") -and
    -not $firewallServiceSource.Contains("RunNetshElevated") -and
    -not $firewallServiceSource.Contains(
        "RepairPrivateMdnsRuleExplicitlyWithUac") -and
    -not $programSource.Contains("bonjour-machine")) (
    "explicit recovery uses only protected sc.exe and one allowlisted service start")

$requestStart = $contextSource.IndexOf(
    "public void RequestBonjourServiceRecovery()",
    [StringComparison]::Ordinal)
$requestEnd = $contextSource.IndexOf(
    "private void CompleteBonjourExplicitRecovery(",
    $requestStart,
    [StringComparison]::Ordinal)
Assert-True ($requestStart -ge 0 -and $requestEnd -gt $requestStart) (
    "explicit Bonjour request has a focused source boundary")
$requestSource = $contextSource.Substring(
    $requestStart, $requestEnd - $requestStart)
Assert-True (-not $requestSource.Contains(
        "assessment.State == BonjourServiceState.StartPending") -and
    $requestSource.Contains("WaitForExplicitStart(") -and
    ([regex]::Matches(
        $requestSource,
        [regex]::Escape("TryLaunchExplicitStart("))).Count -eq 1 -and
    ([regex]::Matches(
        $serviceRecoverySource,
        [regex]::Escape("TryLaunchExplicitStart("))).Count -eq 1) (
    "Running and StartPending are confirmed by the common bounded wait instead of a premature success")

$requiredStart = $contextSource.IndexOf(
    "public bool IsBonjourServiceRecoveryRequired",
    [StringComparison]::Ordinal)
$requiredEnd = $contextSource.IndexOf(
    "public bool IsBonjourServiceStarting",
    $requiredStart,
    [StringComparison]::Ordinal)
Assert-True ($requiredStart -ge 0 -and $requiredEnd -gt $requiredStart) (
    "Bonjour recovery-required property has a focused source boundary")
$requiredSource = $contextSource.Substring(
    $requiredStart, $requiredEnd - $requiredStart)
Assert-True ($requiredSource.Contains(
        "assessment.State == BonjourServiceState.Stopped") -and
    -not $requiredSource.Contains("BonjourServiceState.StopPending")) (
    "the recovery button is eligible only for the exact Stopped state")

$waitStart = $serviceRecoverySource.IndexOf(
    "internal static bool WaitForExplicitStart(",
    [StringComparison]::Ordinal)
$waitEnd = $serviceRecoverySource.IndexOf(
    "private static bool TryCreateExplicitStartInfo(",
    $waitStart,
    [StringComparison]::Ordinal)
Assert-True ($waitStart -ge 0 -and $waitEnd -gt $waitStart) (
    "explicit Bonjour wait has a focused source boundary")
$waitSource = $serviceRecoverySource.Substring(
    $waitStart, $waitEnd - $waitStart)
$exitCodeIndex = $waitSource.IndexOf("process.ExitCode != 0")
$runningWaitIndex = $waitSource.IndexOf("service.WaitForStatus(")
Assert-True ($exitCodeIndex -ge 0 -and
    $runningWaitIndex -gt $exitCodeIndex -and
    $waitSource.Contains("commandDetail") -and
    $waitSource.Contains("out bool processExited") -and
    $waitSource.Contains("IsProcessExitConfirmed(process)") -and
    -not $waitSource.Contains("process.Kill()")) (
    "the bounded result retains whether the elevated command actually exited")

Assert-True (-not $settingsSource.Contains("refreshDiscovery") -and
    -not $settingsSource.Contains("bonjourFirewallRepair") -and
    -not $receiverSource.Contains("bonjourFirewallItem") -and
    $settingsSource.Contains($startBonjourText) -and
    $settingsSource.Contains("bonjourRecovery.Visible = showBonjourRecovery") -and
    $settingsSource.Contains("context.RequestBonjourServiceRecovery();") -and
    $settingsSource.Contains("bonjourServiceRecoveryRequired") -and
    $settingsSource.Contains("bonjourServiceStarting") -and
    $settingsSource.Contains("bonjourServiceStopping") -and
    $settingsSource.Contains("bonjourServicePausePending") -and
    $settingsSource.Contains("bonjourServicePaused") -and
    $settingsSource.Contains("IsBonjourServiceStatusUnknown")) (
    "the main window exposes recovery only inside the contextual Bonjour error card")

$showRecoveryStart = $settingsSource.IndexOf(
    "bool showBonjourRecovery =",
    [StringComparison]::Ordinal)
$showRecoveryEnd = $settingsSource.IndexOf(
    "networkCard.Height =",
    $showRecoveryStart,
    [StringComparison]::Ordinal)
Assert-True ($showRecoveryStart -ge 0 -and
    $showRecoveryEnd -gt $showRecoveryStart) (
    "Bonjour recovery visibility has a focused source boundary")
$showRecoverySource = $settingsSource.Substring(
    $showRecoveryStart, $showRecoveryEnd - $showRecoveryStart)
Assert-True ($showRecoverySource.Contains(
        "bonjourServiceRecoveryRequired") -and
    $showRecoverySource.Contains("bonjourRecoveryRunning") -and
    -not $showRecoverySource.Contains("bonjourServiceStopping") -and
    -not $showRecoverySource.Contains("bonjourServicePausePending") -and
    -not $showRecoverySource.Contains("bonjourServicePaused")) (
    "stop and pause transitions may show status but never the service-start button")

$completeStart = $contextSource.IndexOf(
    "private void CompleteBonjourExplicitRecovery(",
    [StringComparison]::Ordinal)
$completeEnd = $contextSource.IndexOf(
    "private void HandleBonjourExplicitRecoveryResult()",
    $completeStart,
    [StringComparison]::Ordinal)
Assert-True ($completeStart -ge 0 -and $completeEnd -gt $completeStart) (
    "explicit Bonjour completion has a focused source boundary")
$completeSource = $contextSource.Substring(
    $completeStart, $completeEnd - $completeStart)
Assert-True ($requestSource.Contains("Process process = null;") -and
    $requestSource.Contains("bool queued = ThreadPool.QueueUserWorkItem") -and
    $requestSource.Contains("if (!queued)") -and
    $requestSource.Contains("catch (Exception exception)") -and
    $requestSource.Contains(
        "WaitForBonjourExplicitRecoveryProcessExitAndRelease(") -and
    $requestSource.Contains(
        "WaitForExplicitStartProcessExit(process)") -and
    $serviceRecoverySource.Contains("while (!process.WaitForExit(1000))") -and
    -not $serviceRecoverySource.Contains("process.WaitForExit();") -and
    $requestSource.Contains(
        "the one-flight latch remains closed") -and
    $completeSource.Contains(
        "if (releaseFlight)") -and
    $completeSource.Contains(
        "ref bonjourExplicitRecoveryRunning, 0")) (
    "bounded background polls retain the live-command latch until confirmed exit")

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
    $settingsSource.Contains($startBonjourText) -and
    $settingsSource.Contains($oneAdministratorConfirmationText) -and
    $settingsSource.Contains($noAutomaticRepeatText) -and
    $contextSource.Contains("HandleBonjourExplicitRecoveryResult")) (
    "Bonjour UI explains the contextual explicit recovery and no-repeat boundary")

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

$monitorStart = $receiverCoreSource.IndexOf(
    "private void HandleBonjourServiceRecoveryMonitor()",
    [StringComparison]::Ordinal)
$monitorEnd = $receiverCoreSource.IndexOf(
    "private void MarkBonjourPrerequisiteUnavailable()",
    $monitorStart,
    [StringComparison]::Ordinal)
$monitorSource = $receiverCoreSource.Substring(
    $monitorStart, $monitorEnd - $monitorStart)
Assert-True (-not $monitorSource.Contains("TryLaunchExplicitStart") -and
    -not $monitorSource.Contains('Verb = "runas"') -and
    -not $receiverCoreSource.Substring(
        0, $receiverCoreSource.IndexOf(
            "private void HandleBonjourServiceRecoveryMonitor()",
            [StringComparison]::Ordinal)).Contains(
                "TryLaunchExplicitStart")) (
    "startup and timer monitoring never trigger an administrator prompt")

$resumeStart = $receiverCoreSource.IndexOf(
    "internal void ResumeDiscoveryAfterBonjourRecovery()",
    [StringComparison]::Ordinal)
$resumeEnd = $receiverCoreSource.IndexOf(
    "private void OnStartStop(",
    $resumeStart,
    [StringComparison]::Ordinal)
$resumeSource = $receiverCoreSource.Substring(
    $resumeStart, $resumeEnd - $resumeStart)
Assert-True (-not $resumeSource.Contains(
        "coreBonjourRecoveryAttempted, 0") -and
    $resumeSource.Contains(
        "coreBonjourServiceCheckDueTicks, 0") -and
    $resumeSource.Contains("HandleBonjourServiceRecoveryMonitor();")) (
    "an explicit start accelerates the current recovery epoch without renewing its two-attempt budget")

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
    $firewallServiceSource.Contains("PropagationFlags.InheritOnly") -and
    $firewallServiceSource.Contains(
        "TryValidateBonjourServiceObjectSecurity(") -and
    $firewallServiceSource.Contains(
        "QueryServiceObjectSecurity(") -and
    $firewallServiceSource.Contains(
        "HasUntrustedServiceControlAccess(")) (
    "assessment validates exact HKLM identity, service DACL, and protected path")

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

$isExplicitStartState = Get-InternalMethod $serviceRecoveryType (
    "IsExplicitStartState")
$serviceStatusType = $isExplicitStartState.GetParameters()[0].ParameterType
$stoppedServiceStatus = [Enum]::Parse($serviceStatusType, "Stopped")
foreach ($status in [Enum]::GetValues($serviceStatusType)) {
    $allowed = [bool]$isExplicitStartState.Invoke($null, @($status))
    Assert-True ($allowed -eq ($status -eq $stoppedServiceStatus)) (
        "only exact Stopped may launch sc.exe: $status")
}

$mapServiceState = Get-InternalMethod $serviceRecoveryType "MapServiceState"
$bonjourStateType = $mapServiceState.ReturnType
$expectedStateByServiceStatus = @{
    Running         = "Running"
    StartPending    = "StartPending"
    ContinuePending = "StartPending"
    StopPending     = "StopPending"
    PausePending    = "PausePending"
    Paused          = "Paused"
    Stopped         = "Stopped"
}
foreach ($status in [Enum]::GetValues($serviceStatusType)) {
    $mapped = $mapServiceState.Invoke($null, @($status)).ToString()
    Assert-True ($mapped -eq $expectedStateByServiceStatus[$status.ToString()]) (
        "service state maps without exposing recovery for $status")
}
$invalidServiceStatus = [Enum]::ToObject($serviceStatusType, 0)
$invalidMapped = $mapServiceState.Invoke(
    $null, @($invalidServiceStatus)).ToString()
Assert-True ($invalidMapped -eq "Unknown") (
    "an unknown service-controller value fails closed")
Assert-True ($diagnosticsSource.Contains(
        "case BonjourServiceState.PausePending:") -and
    $diagnosticsSource.Contains('return "PausePending";') -and
    $diagnosticsSource.Contains(
        "case BonjourServiceState.Paused:") -and
    $diagnosticsSource.Contains('return "Paused";')) (
    "diagnostics preserve distinct Bonjour pause states")

$createStartInfo = Get-InternalMethod $serviceRecoveryType (
    "TryCreateExplicitStartInfo")
[object[]]$startArguments = @("Bonjour Service", $null, $null)
Assert-True ([bool]$createStartInfo.Invoke($null, $startArguments)) (
    "the exact Bonjour service produces a recovery command")
$startInfo = [Diagnostics.ProcessStartInfo]$startArguments[1]
Assert-True ($null -ne $startInfo -and
    [IO.Path]::GetFullPath($startInfo.FileName) -eq
        [IO.Path]::Combine([Environment]::SystemDirectory, "sc.exe") -and
    $startInfo.Arguments -eq 'start "Bonjour Service"' -and
    $startInfo.Verb -eq "runas" -and
    $startInfo.UseShellExecute -and
    $startInfo.WindowStyle -eq [Diagnostics.ProcessWindowStyle]::Hidden -and
    -not $startInfo.ErrorDialog) (
    "recovery elevates only protected sc.exe with the exact start command")
[object[]]$unsafeStartArguments = @(
    'Bonjour Service" failure reset= 0', $null, $null)
Assert-True (-not [bool]$createStartInfo.Invoke(
        $null, $unsafeStartArguments) -and
    $null -eq $unsafeStartArguments[1]) (
    "an unsafe service name cannot produce an elevated command")

$hasUntrustedWriteAccessRules = Get-InternalMethod $serviceType (
    "HasUntrustedWriteAccessRules")
$localSystemSid = New-Object Security.Principal.SecurityIdentifier(
    [Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
$administratorsSid = New-Object Security.Principal.SecurityIdentifier(
    [Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
$usersSid = New-Object Security.Principal.SecurityIdentifier(
    [Security.Principal.WellKnownSidType]::BuiltinUsersSid, $null)
$trustedInstallerSid = New-Object Security.Principal.SecurityIdentifier(
    "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464")

$hasUntrustedServiceControlAccess = Get-InternalMethod $serviceType (
    "HasUntrustedServiceControlAccess")
function Test-UntrustedServiceControlAccess([string]$Sddl) {
    $descriptor = [Security.AccessControl.RawSecurityDescriptor]::new($Sddl)
    return [bool]$hasUntrustedServiceControlAccess.Invoke(
        $null, @($descriptor))
}

Assert-True (-not (Test-UntrustedServiceControlAccess (
        "O:SYD:(A;;GR;;;BU)(A;;GA;;;SY)"))) (
    "read-only untrusted service access and trusted mutation access are accepted")
Assert-True (Test-UntrustedServiceControlAccess "O:SY") (
    "a service security descriptor with a NULL DACL fails closed")
Assert-True (Test-UntrustedServiceControlAccess (
        "O:BUD:(A;;GA;;;SY)")) (
    "an untrusted service owner fails closed")

foreach ($dangerousServiceAccess in @(
    0x00000002,
    0x00010000,
    0x00040000,
    0x00080000,
    0x10000000,
    0x40000000
)) {
    $mask = "0x{0:X8}" -f $dangerousServiceAccess
    Assert-True (Test-UntrustedServiceControlAccess (
            "O:SYD:(A;;$mask;;;BU)")) (
        "an untrusted service mutation ACE fails closed: $mask")
}

Assert-True (Test-UntrustedServiceControlAccess (
        "O:SYD:(A;CIIO;0x00000002;;;BU)")) (
    "an inheritable untrusted service mutation ACE also fails closed")
Assert-True (Test-UntrustedServiceControlAccess (
        "O:SYD:(D;;GA;;;BU)(A;;0x00000002;;;BU)")) (
    "an untrusted allow is rejected even when another ACE denies access")

foreach ($trustedWriter in @(
    "SY",
    "BA",
    $trustedInstallerSid.Value
)) {
    Assert-True (-not (Test-UntrustedServiceControlAccess (
            "O:SYD:(A;;GA;;;$trustedWriter)"))) (
        "a machine-trusted service writer is accepted: $trustedWriter")
}

function Test-UntrustedWriteAccess(
    [Security.Principal.SecurityIdentifier]$Owner,
    [bool]$HasDacl,
    [Collections.IEnumerable]$Rules
) {
    [object[]]$arguments = New-Object object[] 3
    $arguments[0] = $Owner
    $arguments[1] = $HasDacl
    $arguments[2] = $Rules
    return [bool]$hasUntrustedWriteAccessRules.Invoke($null, $arguments)
}

$noRules = New-Object Collections.ArrayList
$trustedNoWrite = Test-UntrustedWriteAccess $localSystemSid $true $noRules
Assert-True (-not $trustedNoWrite) (
    "a protected LocalSystem-owned component with a DACL is accepted")
Assert-True (Test-UntrustedWriteAccess $localSystemSid $false $noRules) (
    "a NULL DACL fails closed")
Assert-True (Test-UntrustedWriteAccess $usersSid $true $noRules) (
    "an untrusted owner fails closed")

$untrustedWriteRules = New-Object Collections.ArrayList
[void]$untrustedWriteRules.Add(
    (New-Object Security.AccessControl.FileSystemAccessRule(
        $usersSid,
        [Security.AccessControl.FileSystemRights]::WriteData,
        [Security.AccessControl.AccessControlType]::Allow)))
$hasUntrustedWrite = Test-UntrustedWriteAccess `
    $localSystemSid $true $untrustedWriteRules
Assert-True $hasUntrustedWrite (
    "a broad untrusted write ACE fails closed")

foreach ($trustedWriter in @(
    $localSystemSid, $administratorsSid, $trustedInstallerSid)) {
    $trustedWriteRules = New-Object Collections.ArrayList
    [void]$trustedWriteRules.Add(
        (New-Object Security.AccessControl.FileSystemAccessRule(
            $trustedWriter,
            [Security.AccessControl.FileSystemRights]::WriteData,
            [Security.AccessControl.AccessControlType]::Allow)))
    $hasTrustedWriteOnly = Test-UntrustedWriteAccess `
        $localSystemSid $true $trustedWriteRules
    Assert-True (-not $hasTrustedWriteOnly) (
        "a machine-trusted writer ACE is accepted: $trustedWriter")
}

$inheritOnlyRules = New-Object Collections.ArrayList
[void]$inheritOnlyRules.Add(
    (New-Object Security.AccessControl.FileSystemAccessRule(
        $usersSid,
        [Security.AccessControl.FileSystemRights]::WriteData,
        [Security.AccessControl.InheritanceFlags]::ContainerInherit,
        [Security.AccessControl.PropagationFlags]::InheritOnly,
        [Security.AccessControl.AccessControlType]::Allow)))
$hasInheritedOnlyWrite = Test-UntrustedWriteAccess `
    $localSystemSid $true $inheritOnlyRules
Assert-True (-not $hasInheritedOnlyWrite) (
    "an inherit-only ACE does not make the current path component writable")

$isExpectedBonjourPath = Get-InternalMethod $serviceType (
    "IsExpectedBonjourExecutablePath")
$isTrustedMachinePath = Get-InternalMethod $serviceType (
    "IsTrustedMachinePath")
$windowsDirectory = [IO.Path]::GetFullPath(
    [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::Windows)).TrimEnd('\')
$systemControllerPath = [IO.Path]::Combine(
    [Environment]::SystemDirectory, "sc.exe")
Assert-True ([bool]$isTrustedMachinePath.Invoke(
        $null, @($systemControllerPath, $windowsDirectory))) (
    "the installed canonical sc.exe chain has trusted owners, ACLs, and no reparse points")
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
    "Bonjour prerequisite tests passed: read-only monitoring, explicit " +
    "contextual service start, no automatic UAC, and exact firewall.")

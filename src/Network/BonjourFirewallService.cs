using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace AirPlayReceiverMvp
{
    internal enum BonjourFirewallState
    {
        BonjourUnavailable,
        PolicyUnavailable,
        Missing,
        Configured
    }

    internal enum BonjourFirewallChangeResult
    {
        AlreadyConfigured,
        Applied,
        BonjourUnavailable,
        PolicyUnavailable,
        ElevationCanceled,
        Failed
    }

    internal sealed class BonjourFirewallAssessment
    {
        internal BonjourFirewallState State;
        internal string ExecutablePath;
        internal string Detail;
    }

    /*
     * This deliberately mirrors only the properties used by the narrow rule
     * contract. Keeping the matcher separate from COM makes the security-
     * relevant policy deterministic and testable without touching the host
     * firewall.
     */
    internal sealed class FirewallRuleSnapshot
    {
        internal string Name;
        internal bool Enabled;
        internal int Direction;
        internal int Action;
        internal int Protocol;
        internal int Profiles;
        internal string ApplicationName;
        internal string LocalPorts;
        internal string RemoteAddresses;
        internal bool EdgeTraversal;
    }

    internal static class BonjourFirewallService
    {
        internal const string OwnedRuleName =
            "AeroMirror Bonjour mDNS (Private)";

        private const string BonjourExecutableName = "mDNSResponder.exe";
        private const string ServiceImagePathValue = "ImagePath";
        private const int FirewallDirectionIn = 1;
        private const int FirewallActionAllow = 1;
        private const int FirewallProtocolUdp = 17;
        private const int FirewallProfilePrivate = 2;
        private const int ErrorCancelled = 1223;
        private const int ElevatedCommandTimeoutMilliseconds = 30000;

        private static readonly string[] BonjourServiceRegistryPaths =
        {
            @"SYSTEM\CurrentControlSet\Services\Bonjour Service",
            @"SYSTEM\CurrentControlSet\Services\mDNSResponder"
        };

        internal static BonjourFirewallAssessment AssessPrivateMdnsRule()
        {
            string executablePath;
            string pathError;
            if (!TryGetBonjourExecutablePath(
                    out executablePath, out pathError))
            {
                return new BonjourFirewallAssessment
                {
                    State = BonjourFirewallState.BonjourUnavailable,
                    ExecutablePath = "",
                    Detail = pathError
                };
            }

            List<FirewallRuleSnapshot> rules;
            string policyError;
            if (!TryReadFirewallRules(out rules, out policyError))
            {
                return new BonjourFirewallAssessment
                {
                    State = BonjourFirewallState.PolicyUnavailable,
                    ExecutablePath = executablePath,
                    Detail = policyError
                };
            }

            return new BonjourFirewallAssessment
            {
                State = HasExpectedPrivateMdnsRule(
                        rules, executablePath)
                    ? BonjourFirewallState.Configured
                    : BonjourFirewallState.Missing,
                ExecutablePath = executablePath,
                Detail = ""
            };
        }

        /*
         * Nothing calls this automatically. A future UI action must invoke it
         * explicitly; the only mutating process is the UAC-elevated, fixed
         * system netsh executable.
         */
        internal static BonjourFirewallChangeResult
            RepairPrivateMdnsRuleExplicitlyWithUac()
        {
            BonjourFirewallAssessment before = AssessPrivateMdnsRule();
            if (before.State == BonjourFirewallState.Configured)
                return BonjourFirewallChangeResult.AlreadyConfigured;
            if (before.State == BonjourFirewallState.BonjourUnavailable)
                return BonjourFirewallChangeResult.BonjourUnavailable;
            if (before.State == BonjourFirewallState.PolicyUnavailable)
                return BonjourFirewallChangeResult.PolicyUnavailable;

            int exitCode;
            BonjourFirewallChangeResult launchResult = RunNetshElevated(
                BuildAddRuleArguments(before.ExecutablePath), out exitCode);
            if (launchResult != BonjourFirewallChangeResult.Applied)
                return launchResult;
            if (exitCode != 0)
                return BonjourFirewallChangeResult.Failed;

            BonjourFirewallAssessment after = AssessPrivateMdnsRule();
            if (after.State == BonjourFirewallState.Configured)
                return BonjourFirewallChangeResult.Applied;
            if (after.State == BonjourFirewallState.PolicyUnavailable)
                return BonjourFirewallChangeResult.PolicyUnavailable;
            return BonjourFirewallChangeResult.Failed;
        }

        internal static bool TryParseBonjourServiceImagePath(
            string rawImagePath, out string executablePath)
        {
            executablePath = "";
            if (string.IsNullOrWhiteSpace(rawImagePath))
                return false;

            string value = rawImagePath.Trim();
            string candidate;
            if (value[0] == '"')
            {
                int closingQuote = value.IndexOf('"', 1);
                if (closingQuote <= 1 ||
                    value.Substring(closingQuote + 1).Trim().Length != 0)
                    return false;
                candidate = value.Substring(1, closingQuote - 1);
            }
            else
            {
                for (int index = 0; index < value.Length; index++)
                {
                    if (char.IsWhiteSpace(value[index]))
                        return false;
                }
                candidate = value;
            }

            return TryNormalizeBonjourExecutablePath(
                candidate, out executablePath);
        }

        internal static bool IsExpectedPrivateMdnsRule(
            FirewallRuleSnapshot rule, string exactExecutablePath)
        {
            if (rule == null || !rule.Enabled ||
                rule.Direction != FirewallDirectionIn ||
                rule.Action != FirewallActionAllow ||
                rule.Protocol != FirewallProtocolUdp ||
                rule.Profiles != FirewallProfilePrivate ||
                rule.EdgeTraversal ||
                !IsExactSingleValue(rule.LocalPorts, "5353") ||
                !IsExactSingleValue(
                    rule.RemoteAddresses, "LocalSubnet"))
                return false;

            string expectedPath;
            string rulePath;
            return TryNormalizeBonjourExecutablePath(
                    exactExecutablePath, out expectedPath) &&
                TryNormalizeBonjourExecutablePath(
                    rule.ApplicationName, out rulePath) &&
                string.Equals(
                    expectedPath, rulePath,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static string BuildAddRuleArguments(
            string exactExecutablePath)
        {
            string path = RequireValidatedBonjourExecutablePath(
                exactExecutablePath);
            return "advfirewall firewall add rule " +
                "name=" + QuoteNetshValue(OwnedRuleName) + " " +
                "dir=in action=allow enable=yes profile=private " +
                "protocol=UDP localport=5353 remoteip=LocalSubnet " +
                "edge=no program=" + QuoteNetshValue(path);
        }

        private static bool TryGetBonjourExecutablePath(
            out string executablePath, out string error)
        {
            executablePath = "";
            error = "Bonjour service ImagePath was not found.";

            foreach (string servicePath in BonjourServiceRegistryPaths)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        servicePath, false))
                    {
                        if (key == null)
                            continue;
                        object raw = key.GetValue(
                            ServiceImagePathValue,
                            null,
                            RegistryValueOptions.DoNotExpandEnvironmentNames);
                        string parsed;
                        if (!TryParseBonjourServiceImagePath(
                                raw as string, out parsed))
                        {
                            error = "Bonjour service ImagePath is not a " +
                                "strict absolute mDNSResponder executable path.";
                            return false;
                        }
                        if (!File.Exists(parsed))
                        {
                            error = "Bonjour service executable does not exist.";
                            return false;
                        }

                        executablePath = parsed;
                        error = "";
                        return true;
                    }
                }
                catch (SecurityException exception)
                {
                    error = exception.Message;
                    return false;
                }
                catch (UnauthorizedAccessException exception)
                {
                    error = exception.Message;
                    return false;
                }
                catch (IOException exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            return false;
        }

        private static bool TryNormalizeBonjourExecutablePath(
            string value, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.IndexOf('%') >= 0 || value.IndexOf('"') >= 0 ||
                value.IndexOf('*') >= 0 || value.IndexOf('?') >= 0)
                return false;

            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                    return false;
            }

            string candidate = value.Trim();
            if (candidate.Length < 4 ||
                !char.IsLetter(candidate[0]) ||
                candidate[1] != ':' ||
                candidate[2] != '\\' ||
                candidate.IndexOf(':', 2) >= 0 ||
                candidate.StartsWith(@"\\", StringComparison.Ordinal) ||
                candidate.StartsWith(@"\\?\", StringComparison.Ordinal))
                return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }

            if (!string.Equals(
                    fullPath, candidate,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(fullPath),
                    BonjourExecutableName,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            normalized = fullPath;
            return true;
        }

        private static string RequireValidatedBonjourExecutablePath(
            string value)
        {
            string normalized;
            if (!TryNormalizeBonjourExecutablePath(value, out normalized))
                throw new ArgumentException(
                    "Expected a strict absolute mDNSResponder.exe path.",
                    "value");
            return normalized;
        }

        private static bool HasExpectedPrivateMdnsRule(
            IEnumerable<FirewallRuleSnapshot> rules,
            string executablePath)
        {
            if (rules == null)
                return false;
            foreach (FirewallRuleSnapshot rule in rules)
            {
                if (IsExpectedPrivateMdnsRule(
                        rule, executablePath))
                    return true;
            }
            return false;
        }

        private static bool IsExactSingleValue(
            string actual, string expected)
        {
            return !string.IsNullOrWhiteSpace(actual) &&
                string.Equals(
                    actual.Trim(), expected,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteNetshValue(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.IndexOf('"') >= 0)
                throw new ArgumentException("Unsafe netsh value.", "value");
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                    throw new ArgumentException(
                        "Unsafe netsh value.", "value");
            }
            return "\"" + value + "\"";
        }

        private static bool TryReadFirewallRules(
            out List<FirewallRuleSnapshot> snapshots, out string error)
        {
            snapshots = new List<FirewallRuleSnapshot>();
            error = "";
            Type policyType = Type.GetTypeFromProgID(
                "HNetCfg.FwPolicy2", false);
            if (policyType == null)
            {
                error = "Windows Firewall policy COM API is unavailable.";
                return false;
            }

            object policy = null;
            object rulesObject = null;
            try
            {
                policy = Activator.CreateInstance(policyType);
                rulesObject = GetComProperty(policy, "Rules");
                IEnumerable rules = rulesObject as IEnumerable;
                if (rules == null)
                {
                    error = "Windows Firewall rule collection is unavailable.";
                    return false;
                }

                foreach (object ruleObject in rules)
                {
                    try
                    {
                        snapshots.Add(new FirewallRuleSnapshot
                        {
                            Name = Convert.ToString(
                                GetComProperty(ruleObject, "Name"),
                                CultureInfo.InvariantCulture),
                            Enabled = Convert.ToBoolean(
                                GetComProperty(ruleObject, "Enabled"),
                                CultureInfo.InvariantCulture),
                            Direction = Convert.ToInt32(
                                GetComProperty(ruleObject, "Direction"),
                                CultureInfo.InvariantCulture),
                            Action = Convert.ToInt32(
                                GetComProperty(ruleObject, "Action"),
                                CultureInfo.InvariantCulture),
                            Protocol = Convert.ToInt32(
                                GetComProperty(ruleObject, "Protocol"),
                                CultureInfo.InvariantCulture),
                            Profiles = Convert.ToInt32(
                                GetComProperty(ruleObject, "Profiles"),
                                CultureInfo.InvariantCulture),
                            ApplicationName = Convert.ToString(
                                GetComProperty(
                                    ruleObject, "ApplicationName"),
                                CultureInfo.InvariantCulture),
                            LocalPorts = Convert.ToString(
                                GetComProperty(ruleObject, "LocalPorts"),
                                CultureInfo.InvariantCulture),
                            RemoteAddresses = Convert.ToString(
                                GetComProperty(
                                    ruleObject, "RemoteAddresses"),
                                CultureInfo.InvariantCulture),
                            EdgeTraversal = Convert.ToBoolean(
                                GetComProperty(
                                    ruleObject, "EdgeTraversal"),
                                CultureInfo.InvariantCulture)
                        });
                    }
                    catch (COMException)
                    {
                        // A malformed or provider-owned rule cannot satisfy the
                        // complete narrow contract, so skip it safely.
                    }
                    catch (InvalidCastException)
                    {
                    }
                    catch (FormatException)
                    {
                    }
                    finally
                    {
                        ReleaseComObject(ruleObject);
                    }
                }
                return true;
            }
            catch (COMException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException != null
                    ? exception.InnerException.Message
                    : exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                ReleaseComObject(rulesObject);
                ReleaseComObject(policy);
            }
        }

        private static object GetComProperty(object target, string name)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                target,
                null,
                CultureInfo.InvariantCulture);
        }

        private static void ReleaseComObject(object value)
        {
            if (value == null || !Marshal.IsComObject(value))
                return;
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch (InvalidComObjectException)
            {
            }
        }

        private static BonjourFirewallChangeResult RunNetshElevated(
            string arguments, out int exitCode)
        {
            exitCode = -1;
            string netshPath = Path.Combine(
                Environment.SystemDirectory, "netsh.exe");
            if (!File.Exists(netshPath))
                return BonjourFirewallChangeResult.Failed;

            var start = new ProcessStartInfo
            {
                FileName = netshPath,
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Environment.SystemDirectory,
                ErrorDialog = false
            };

            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                        return BonjourFirewallChangeResult.Failed;
                    if (!process.WaitForExit(
                            ElevatedCommandTimeoutMilliseconds))
                        return BonjourFirewallChangeResult.Failed;
                    exitCode = process.ExitCode;
                    return BonjourFirewallChangeResult.Applied;
                }
            }
            catch (Win32Exception exception)
            {
                return exception.NativeErrorCode == ErrorCancelled
                    ? BonjourFirewallChangeResult.ElevationCanceled
                    : BonjourFirewallChangeResult.Failed;
            }
            catch (InvalidOperationException)
            {
                return BonjourFirewallChangeResult.Failed;
            }
        }
    }
}

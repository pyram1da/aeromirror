using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
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

    internal sealed class BonjourFirewallAssessment
    {
        internal BonjourFirewallState State;
        internal string ExecutablePath;
        internal string Detail;
    }

    internal sealed class BonjourServiceIdentity
    {
        internal string ServiceName;
        internal string ExecutablePath;
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
        private const string BonjourExecutableName = "mDNSResponder.exe";
        private const string ServiceImagePathValue = "ImagePath";
        private const int FirewallDirectionIn = 1;
        private const int FirewallActionAllow = 1;
        private const int FirewallProtocolUdp = 17;
        private const int FirewallProfilePrivate = 2;
        private static readonly string[] BonjourServiceNames =
        {
            "Bonjour Service",
            "mDNSResponder"
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

        internal static bool TryGetValidatedBonjourServiceIdentity(
            out BonjourServiceIdentity identity, out string error)
        {
            identity = null;
            error = "Bonjour service ImagePath was not found.";
            BonjourServiceIdentity resolvedIdentity = null;

            foreach (string serviceName in BonjourServiceNames)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        @"SYSTEM\CurrentControlSet\Services\" + serviceName,
                        false))
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
                        if (!IsTrustedInstalledBonjourExecutable(parsed))
                        {
                            error = "Bonjour service executable is outside the exact protected Program Files\\Bonjour path or is writable by the current user.";
                            return false;
                        }

                        if (resolvedIdentity != null)
                        {
                            error = "Multiple Bonjour service identities were found.";
                            return false;
                        }

                        resolvedIdentity = new BonjourServiceIdentity
                        {
                            ServiceName = serviceName,
                            ExecutablePath = parsed
                        };
                        error = "";
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

            identity = resolvedIdentity;
            return identity != null;
        }

        private static bool TryGetBonjourExecutablePath(
            out string executablePath, out string error)
        {
            BonjourServiceIdentity identity;
            if (!TryGetValidatedBonjourServiceIdentity(
                    out identity, out error))
            {
                executablePath = "";
                return false;
            }

            executablePath = identity.ExecutablePath;
            return true;
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

        private static bool IsTrustedInstalledBonjourExecutable(string path)
        {
            string normalized;
            if (!TryNormalizeBonjourExecutablePath(path, out normalized) ||
                !File.Exists(normalized))
                return false;

            if (!IsExpectedBonjourExecutablePath(
                    normalized,
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFilesX86)))
                return false;

            string trustedRoot;
            if (!TryGetContainingProgramFilesRoot(
                    normalized, out trustedRoot))
                return false;

            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (identity == null || identity.User == null)
                        return false;
                    var principal = new WindowsPrincipal(identity);
                    string current = normalized;
                    while (true)
                    {
                        FileAttributes attributes =
                            File.GetAttributes(current);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            return false;

                        FileSystemSecurity security =
                            Directory.Exists(current)
                                ? (FileSystemSecurity)Directory.GetAccessControl(
                                    current, AccessControlSections.Access |
                                    AccessControlSections.Owner)
                                : File.GetAccessControl(
                                    current, AccessControlSections.Access |
                                    AccessControlSections.Owner);
                        if (IsWritableByCurrentToken(
                                security, identity, principal))
                            return false;

                        if (string.Equals(
                                current, trustedRoot,
                                StringComparison.OrdinalIgnoreCase))
                            return true;
                        current = Path.GetDirectoryName(current);
                        if (string.IsNullOrWhiteSpace(current))
                            return false;
                    }
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (SecurityException)
            {
                return false;
            }
        }

        private static bool IsExpectedBonjourExecutablePath(
            string path, string programFiles, string programFilesX86)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string normalizedPath;
            if (!TryNormalizeBonjourExecutablePath(
                    path, out normalizedPath))
                return false;

            string[] roots = { programFiles, programFilesX86 };
            foreach (string rootValue in roots)
            {
                if (string.IsNullOrWhiteSpace(rootValue))
                    continue;
                try
                {
                    string expected = Path.Combine(
                        Path.GetFullPath(rootValue).TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar),
                        "Bonjour",
                        BonjourExecutableName);
                    if (string.Equals(
                            normalizedPath, expected,
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
            }
            return false;
        }

        private static bool TryGetContainingProgramFilesRoot(
            string path, out string trustedRoot)
        {
            trustedRoot = "";
            string[] roots =
            {
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;
                string normalizedRoot;
                try
                {
                    normalizedRoot = Path.GetFullPath(root).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                string prefix = normalizedRoot +
                    Path.DirectorySeparatorChar;
                if (path.StartsWith(
                        prefix, StringComparison.OrdinalIgnoreCase))
                {
                    trustedRoot = normalizedRoot;
                    return true;
                }
            }
            return false;
        }

        private static bool IsWritableByCurrentToken(
            FileSystemSecurity security,
            WindowsIdentity identity,
            WindowsPrincipal principal)
        {
            if (security == null || identity == null ||
                identity.User == null || principal == null)
                return true;

            SecurityIdentifier owner = security.GetOwner(
                typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner != null && (owner.Equals(identity.User) ||
                principal.IsInRole(owner)))
                return true;

            const FileSystemRights dangerous =
                FileSystemRights.WriteData |
                FileSystemRights.AppendData |
                FileSystemRights.WriteExtendedAttributes |
                FileSystemRights.WriteAttributes |
                FileSystemRights.DeleteSubdirectoriesAndFiles |
                FileSystemRights.Delete |
                FileSystemRights.ChangePermissions |
                FileSystemRights.TakeOwnership;
            AuthorizationRuleCollection rules = security.GetAccessRules(
                true, true, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                if ((rule.PropagationFlags & PropagationFlags.InheritOnly) != 0)
                    continue;

                SecurityIdentifier sid =
                    rule.IdentityReference as SecurityIdentifier;
                if (sid == null || (!sid.Equals(identity.User) &&
                    !principal.IsInRole(sid)))
                    continue;

                FileSystemRights rights =
                    rule.FileSystemRights & dangerous;
                // This is deliberately more conservative than trying to
                // reproduce Windows AccessCheck semantics. Any applicable
                // allow for a primitive mutation right makes this path
                // unsuitable as a trusted elevated-service target, even if
                // another ACE may deny the same right.
                if (rule.AccessControlType == AccessControlType.Allow &&
                    rights != 0)
                    return true;
            }
            return false;
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

    }
}

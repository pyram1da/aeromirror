using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace AirPlayReceiverMvp
{
    internal enum BonjourServiceState
    {
        Unknown,
        MissingOrUnsafe,
        StartPending,
        Running,
        StopPending,
        PausePending,
        Paused,
        Stopped
    }

    internal sealed class BonjourServiceAssessment
    {
        internal BonjourServiceState State;
        internal string Detail;
    }

    /*
     * The ordinary per-user application observes Bonjour and may offer one
     * explicit recovery action while the validated service is stopped.  The
     * action launches only Microsoft's protected sc.exe through UAC with one
     * allowlisted service name.  It is never invoked by a timer or startup.
     * Setup continues to own recovery-policy and firewall configuration.
     */
    internal static class BonjourServiceRecoveryService
    {
        private const int ErrorCancelled = 1223;
        private const int StartCommandTimeoutMilliseconds = 30000;
        private const int RunningWaitMilliseconds = 20000;

        internal static BonjourServiceAssessment Assess()
        {
            BonjourServiceIdentity identity;
            string error;
            if (!BonjourFirewallService.TryGetValidatedBonjourServiceIdentity(
                    out identity, out error))
            {
                return new BonjourServiceAssessment
                {
                    State = BonjourServiceState.MissingOrUnsafe,
                    Detail = error
                };
            }

            ServiceControllerStatus status;
            if (!TryGetServiceStatus(identity.ServiceName, out status))
            {
                return new BonjourServiceAssessment
                {
                    State = BonjourServiceState.Unknown,
                    Detail = "Bonjour service status is unavailable."
                };
            }

            return new BonjourServiceAssessment
            {
                State = MapServiceState(status),
                Detail = ""
            };
        }

        private static BonjourServiceState MapServiceState(
            ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running:
                    return BonjourServiceState.Running;
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.ContinuePending:
                    return BonjourServiceState.StartPending;
                case ServiceControllerStatus.StopPending:
                    return BonjourServiceState.StopPending;
                case ServiceControllerStatus.PausePending:
                    return BonjourServiceState.PausePending;
                case ServiceControllerStatus.Paused:
                    return BonjourServiceState.Paused;
                case ServiceControllerStatus.Stopped:
                    return BonjourServiceState.Stopped;
                default:
                    return BonjourServiceState.Unknown;
            }
        }

        internal static bool TryGetServiceStatus(
            string serviceName, out ServiceControllerStatus status)
        {
            status = ServiceControllerStatus.Stopped;
            if (!IsKnownServiceName(serviceName))
                return false;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    status = service.Status;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        internal static bool TryLaunchExplicitStart(
            out Process process, out bool cancelled, out string detail)
        {
            process = null;
            cancelled = false;
            detail = "";

            BonjourServiceIdentity identity;
            string error;
            if (!BonjourFirewallService.TryGetValidatedBonjourServiceIdentity(
                    out identity, out error))
            {
                detail = error;
                return false;
            }

            ServiceControllerStatus status;
            if (!TryGetServiceStatus(identity.ServiceName, out status))
            {
                detail = "Bonjour service status is unavailable.";
                return false;
            }
            if (status == ServiceControllerStatus.Running ||
                status == ServiceControllerStatus.StartPending ||
                status == ServiceControllerStatus.ContinuePending)
            {
                detail = "Bonjour is already running or starting.";
                return true;
            }
            if (!IsExplicitStartState(status))
            {
                detail = "Bonjour is not in the exact Stopped state.";
                return false;
            }

            ProcessStartInfo start;
            if (!TryCreateExplicitStartInfo(
                    identity.ServiceName, out start, out detail))
                return false;

            try
            {
                process = Process.Start(start);
                if (process == null)
                {
                    detail = "Windows did not start the Bonjour recovery command.";
                    return false;
                }
                return true;
            }
            catch (Win32Exception exception)
            {
                cancelled = exception.NativeErrorCode == ErrorCancelled;
                detail = cancelled
                    ? "Administrator confirmation was canceled."
                    : exception.Message;
                return false;
            }
            catch (InvalidOperationException exception)
            {
                detail = exception.Message;
                return false;
            }
        }

        internal static bool WaitForExplicitStart(
            Process process, out bool processExited, out string detail)
        {
            processExited = process == null;
            detail = "";
            string commandDetail = "";
            if (process != null)
            {
                try
                {
                    if (!process.WaitForExit(
                            StartCommandTimeoutMilliseconds))
                    {
                        commandDetail =
                            "Bonjour recovery command timed out.";
                    }
                    else
                    {
                        processExited = true;
                        if (process.ExitCode != 0)
                        {
                            commandDetail =
                                "Bonjour recovery command returned exit code " +
                                process.ExitCode + ".";
                        }
                    }
                }
                catch (InvalidOperationException exception)
                {
                    commandDetail = exception.Message;
                }
                catch (SystemException exception)
                {
                    commandDetail = exception.Message;
                }
            }

            bool succeeded = false;
            try
            {
                BonjourServiceIdentity identity;
                string error;
                if (!BonjourFirewallService.TryGetValidatedBonjourServiceIdentity(
                        out identity, out error))
                {
                    detail = error;
                    return false;
                }
                using (var service = new ServiceController(
                    identity.ServiceName))
                {
                    service.WaitForStatus(
                        ServiceControllerStatus.Running,
                        TimeSpan.FromMilliseconds(RunningWaitMilliseconds));
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        detail = string.IsNullOrWhiteSpace(commandDetail)
                            ? "Bonjour did not reach the Running state."
                            : commandDetail;
                        return false;
                    }
                }
                succeeded = true;
            }
            catch (InvalidOperationException exception)
            {
                detail = exception.Message;
                return false;
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                detail = string.IsNullOrWhiteSpace(commandDetail)
                    ? "Bonjour did not reach the Running state in time."
                    : commandDetail;
            }
            finally
            {
                if (!processExited)
                    processExited = IsProcessExitConfirmed(process);
            }
            return succeeded;
        }

        internal static bool WaitForExplicitStartProcessExit(Process process)
        {
            if (process == null)
                return true;
            try
            {
                while (!process.WaitForExit(1000))
                {
                    // This runs only on the dedicated background watcher. The
                    // bounded wait keeps shutdown paths responsive while the
                    // one-flight latch remains closed for this live process.
                }
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return IsProcessExitConfirmed(process);
            }
            catch (SystemException)
            {
                return IsProcessExitConfirmed(process);
            }
        }

        internal static bool IsProcessExitConfirmed(Process process)
        {
            if (process == null)
                return true;
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (SystemException)
            {
                return false;
            }
        }

        private static bool TryCreateExplicitStartInfo(
            string serviceName,
            out ProcessStartInfo start,
            out string detail)
        {
            start = null;
            detail = "";
            if (!IsKnownServiceName(serviceName))
            {
                detail = "Bonjour service identity is not allowlisted.";
                return false;
            }

            string windowsDirectory;
            string systemDirectory;
            string executable;
            try
            {
                windowsDirectory = Path.GetFullPath(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows)).TrimEnd('\\');
                systemDirectory = Path.GetFullPath(
                    Environment.SystemDirectory).TrimEnd('\\');
                executable = Path.GetFullPath(
                    Path.Combine(systemDirectory, "sc.exe"));
                string expectedSystemDirectory = Path.GetFullPath(
                    Path.Combine(windowsDirectory, "System32"));
                if (string.IsNullOrWhiteSpace(windowsDirectory) ||
                    string.IsNullOrWhiteSpace(systemDirectory) ||
                    !string.Equals(
                        systemDirectory, expectedSystemDirectory,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        Path.GetDirectoryName(executable), systemDirectory,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        Path.GetFileName(executable), "sc.exe",
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(executable) ||
                    (File.GetAttributes(executable) &
                        FileAttributes.ReparsePoint) != 0 ||
                    !BonjourFirewallService.IsTrustedMachinePath(
                        executable, windowsDirectory))
                {
                    detail = "The protected Windows service controller is unavailable.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                detail = exception.Message;
                return false;
            }

            start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "start \"" + serviceName + "\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = systemDirectory,
                ErrorDialog = false
            };
            return true;
        }

        private static bool IsExplicitStartState(
            ServiceControllerStatus status)
        {
            return status == ServiceControllerStatus.Stopped;
        }

        private static bool IsKnownServiceName(string serviceName)
        {
            return string.Equals(
                    serviceName, "Bonjour Service",
                    StringComparison.Ordinal) ||
                string.Equals(
                    serviceName, "mDNSResponder",
                    StringComparison.Ordinal);
        }
    }
}

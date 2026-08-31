using System;
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
        Stopped
    }

    internal sealed class BonjourServiceAssessment
    {
        internal BonjourServiceState State;
        internal string Detail;
    }

    /*
     * The ordinary per-user application only observes Bonjour. Setup owns the
     * one-time elevated recovery-policy and firewall configuration, so normal
     * startup never opens UAC or exposes a manual recovery button.
     */
    internal static class BonjourServiceRecoveryService
    {
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

            BonjourServiceState state;
            switch (status)
            {
                case ServiceControllerStatus.Running:
                    state = BonjourServiceState.Running;
                    break;
                case ServiceControllerStatus.StartPending:
                case ServiceControllerStatus.ContinuePending:
                    state = BonjourServiceState.StartPending;
                    break;
                case ServiceControllerStatus.StopPending:
                case ServiceControllerStatus.PausePending:
                case ServiceControllerStatus.Paused:
                    state = BonjourServiceState.StopPending;
                    break;
                default:
                    state = BonjourServiceState.Stopped;
                    break;
            }
            return new BonjourServiceAssessment
            {
                State = state,
                Detail = ""
            };
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

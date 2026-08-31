using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private static readonly Regex CoreHttpReadyMarker = new Regex(
            @"^AEROMIRROR_HTTP_READY stage=(initial|reset) " +
                @"port=(\d+)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex CoreHttpFailedMarker = new Regex(
            @"^AEROMIRROR_HTTP_FAILED stage=(initial|reset) " +
                @"(?:port=\d+ code=-?\d+|" +
                @"expected_port=\d+ port=\d+ code=-?\d+)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // The initial marker establishes support and the exact AirPlay port for
        // the lifetime of one native process. A reset may only preserve that
        // process after it explicitly confirms the same port.
        private int coreHttpMarkersReady;
        private int coreHttpPort;
        private int lostConnectionHttpResetStatus;
        private int lostConnectionHttpResetPort;

        private void ObserveCoreSocketReady(int processId)
        {
            if (Interlocked.CompareExchange(
                    ref activeCorePid, 0, 0) != processId)
                return;

            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return;

                MarkCoreSocketsReady();
                bool matchingRecovery =
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPid, 0, 0) == processId;
                if (matchingRecovery &&
                    Interlocked.CompareExchange(
                        ref coreHttpMarkersReady, 0, 0) == 0)
                {
                    Interlocked.Exchange(
                        ref lostConnectionHttpResetStatus, 2);
                    Interlocked.Exchange(
                        ref lostConnectionHttpResetPort, 0);
                }
            }
        }

        private void ObserveCoreHttpLifecycle(int processId, string line)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                Interlocked.CompareExchange(
                    ref activeCorePid, 0, 0) != processId)
                return;

            Match ready = CoreHttpReadyMarker.Match(line);
            if (ready.Success)
            {
                int port;
                if (!int.TryParse(ready.Groups[2].Value, out port) ||
                    port < 1 || port > 65535)
                    return;

                bool initial = string.Equals(
                    ready.Groups[1].Value, "initial",
                    StringComparison.OrdinalIgnoreCase);
                lock (postSessionMaintenanceSync)
                {
                    if (Interlocked.CompareExchange(
                            ref activeCorePid, 0, 0) != processId)
                        return;

                    int establishedPort = Interlocked.CompareExchange(
                        ref coreHttpPort, 0, 0);
                    if (initial)
                    {
                        bool recoveryPending =
                            Interlocked.CompareExchange(
                                ref lostConnectionRecoveryPending,
                                0, 0) == 1;
                        if (recoveryPending ||
                            (establishedPort > 0 &&
                                establishedPort != port))
                        {
                            Log("Ignored an out-of-sequence native initial " +
                                "HTTP-ready marker for port " + port + ".");
                            return;
                        }

                        Interlocked.Exchange(
                            ref coreHttpMarkersReady, 1);
                        Interlocked.Exchange(ref coreHttpPort, port);
                        MarkCoreSocketsReady();
                        return;
                    }

                    bool matchingRecovery =
                        Interlocked.CompareExchange(
                            ref lostConnectionRecoveryPending, 0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref lostConnectionRecoveryPid, 0, 0) ==
                                processId;
                    if (!matchingRecovery ||
                        Interlocked.CompareExchange(
                            ref coreHttpMarkersReady, 0, 0) != 1 ||
                        establishedPort <= 0)
                    {
                        Log("Ignored an out-of-sequence native reset " +
                            "HTTP-ready marker for port " + port + ".");
                        return;
                    }

                    if (port != establishedPort)
                    {
                        Interlocked.Exchange(ref coreSocketsReady, 0);
                        Interlocked.Exchange(
                            ref coreSocketsReadyDueTicks, 0);
                        Interlocked.Exchange(
                            ref lostConnectionHttpResetStatus, -1);
                        Interlocked.Exchange(
                            ref lostConnectionHttpResetPort, port);
                        Log("Rejected native reset HTTP-ready marker: port " +
                            port + " does not match advertised port " +
                            establishedPort + ".");
                        return;
                    }

                    Interlocked.Exchange(
                        ref lostConnectionHttpResetStatus, 1);
                    Interlocked.Exchange(
                        ref lostConnectionHttpResetPort, port);
                    MarkCoreSocketsReady();
                }
                return;
            }

            Match failed = CoreHttpFailedMarker.Match(line);
            if (!failed.Success)
                return;

            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return;
                bool resetFailure = string.Equals(
                    failed.Groups[1].Value, "reset",
                    StringComparison.OrdinalIgnoreCase);
                bool matchingRecovery =
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPid, 0, 0) == processId;
                if ((resetFailure && !matchingRecovery) ||
                    (!resetFailure &&
                        Interlocked.CompareExchange(
                            ref coreHttpMarkersReady, 0, 0) == 1))
                {
                    Log("Ignored an out-of-sequence native " +
                        (resetFailure ? "reset" : "initial") +
                        " HTTP-failed marker.");
                    return;
                }

                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                if (resetFailure)
                {
                    Interlocked.Exchange(
                        ref lostConnectionHttpResetStatus, -1);
                    Interlocked.Exchange(
                        ref lostConnectionHttpResetPort, 0);
                }
            }
        }

        private void MarkCoreSocketsReady()
        {
            Interlocked.Exchange(ref coreSocketsReady, 1);
            Interlocked.Exchange(
                ref coreSocketsReadyDueTicks,
                DateTime.UtcNow.AddMilliseconds(1500).Ticks);
        }

        private void ResetCoreHttpLifecycleTracking()
        {
            Interlocked.Exchange(ref coreHttpMarkersReady, 0);
            Interlocked.Exchange(ref coreHttpPort, 0);
            ResetLostConnectionHttpResetAttempt();
        }

        private void ResetLostConnectionHttpResetAttempt()
        {
            Interlocked.Exchange(ref lostConnectionHttpResetStatus, 0);
            Interlocked.Exchange(ref lostConnectionHttpResetPort, 0);
        }
    }
}

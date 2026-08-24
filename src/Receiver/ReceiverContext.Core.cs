using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private const int FeedbackGapPlaceholderSeconds = 4;
        private const int FeedbackVideoRecoveryWaitSeconds = 3;
        private const int IdleDiscoveryFirstRenewalMinutes = 10;
        private const int IdleDiscoveryRecurringRenewalMinutes = 20;
        private const int IdleDiscoveryUnlockRetryCooldownMinutes = 10;
        private const int IdleDiscoveryLegacyRestartLimit = 2;

        private enum LostConnectionRecoveryAction
        {
            None,
            RestartStalledSession,
            PreserveNativeRecovery,
            PreserveLegacyRecovery
        }

        private enum SessionUnlockDiscoveryAction
        {
            None,
            RetryLater,
            Refresh
        }

        private enum AutomaticDiscoveryRenewalAction
        {
            None,
            Refresh
        }

        private enum VideoSizeCandidateAction
        {
            None,
            CancelPending,
            RetainPendingDeadline,
            ArmPending
        }

        public void StartCore()
        {
            ResetRapidExitWindow();
            ResetSharedAutomaticRecoveryBudget();
            if (!networkProfileKnown)
            {
                startAfterNetworkCheck = true;
                SetState(false, "Проверяем безопасность сети…");
                BeginNetworkProfileRefresh();
                return;
            }
            StartCore(true);
        }

        private void StartCore(bool notify)
        {
            if (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
            {
                restartAfterStop = true;
                return;
            }
            if (coreProcess != null && !IsCoreRunning)
            {
                try
                {
                    int staleCode = coreProcess.ExitCode;
                    CancelCoreOutputReads(coreProcess);
                    Log("Disposing exited core before a new start; code " +
                        staleCode + ".");
                }
                catch { }
                finally
                {
                    Process staleProcess = coreProcess;
                    DetachCoreProcessForLifecycle(staleProcess);
                    staleProcess.Dispose();
                    NativeMethods.CloseHandleSafe(ref coreJob);
                    coreReadyPending = false;
                    Interlocked.Exchange(ref coreSocketsReady, 0);
                    Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                    Interlocked.Exchange(ref activeCorePid, 0);
                }
            }
            if (IsCoreRunning)
                return;
            restartPending = false;

            string beaconIpv4 = FirstNumericIpv4(physicalNetworkAddresses);
            if (!networkProfileKnown || beaconIpv4.Length == 0)
            {
                startAfterNetworkCheck = true;
                SetState(false, "Ждём адрес Wi-Fi/Ethernet…");
                Log("Receiver start deferred until the physical " +
                    "Wi-Fi/Ethernet profile has a usable IPv4 address.");
                BeginNetworkProfileRefresh();
                return;
            }

            if (publicNetwork && settings.PairingMode == "none")
            {
                SetState(false, "Публичная сеть · включите PIN");
                if (settings.Notify && notify)
                    tray.ShowBalloonTip(6000, AppTitle,
                        "Windows считает текущую Wi-Fi/Ethernet-сеть публичной. Включите PIN или измените профиль сети на «Частный» в параметрах Windows.",
                        ToolTipIcon.Warning);
                return;
            }

            if (!File.Exists(CorePath))
            {
                Log("ERROR: Core executable not found: " +
                    MaskSecrets(CorePath));
                SetState(false, "Ядро UxPlay не найдено");
                if (settings.Notify)
                    tray.ShowBalloonTip(5000, AppTitle,
                        "Не найден core\\uxplay-windows.exe. Переустановите AeroMirror.",
                        ToolTipIcon.Error);
                return;
            }

            try
            {
                var start = new ProcessStartInfo();
                start.FileName = CorePath;
                start.Arguments = "--headless --beacon-ipv4 " +
                    QuoteArgument(beaconIpv4) + " --uxplay " +
                    BuildUxPlayArguments();
                start.WorkingDirectory = Path.GetDirectoryName(CorePath);
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.RedirectStandardInput = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
                var process = new Process();
                process.StartInfo = start;
                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                if (!process.Start())
                    throw new InvalidOperationException("UxPlay process did not start.");
                Log("BLE discovery bound to physical IPv4 " +
                    beaconIpv4 + ".");
                coreProcess = process;
                coreJob = NativeMethods.CreateKillOnCloseJob(process);
                if (coreJob == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "UxPlay could not be isolated in a Windows Job Object.");
                int processId = process.Id;
                Interlocked.Exchange(ref activeCorePid, processId);
                ResetNativeDiscoveryRefreshForProcessLifecycle();
                ResetCoreSessionTracking(true);
                ArmIdleDiscoveryRenewalIfAvailable();
                fittedStreamWindow = IntPtr.Zero;
                lock (postSessionMaintenanceSync)
                {
                    coreReadyPending = true;
                    coreReadyChecks = 0;
                    coreReadyDueUtc = DateTime.UtcNow.AddSeconds(2);
                    coreReadinessPid = processId;
                    Interlocked.Exchange(
                        ref coreClientActivityReadyPending, 0);
                }
                string processPinSnapshot = settings.FixedPin;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        if (e.Data.IndexOf(
                                "Initialized server socket",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                            ObserveCoreSocketReady(processId);
                        ObserveCoreOutput(processId, e.Data);
                        Log("core[" + processId + "]/stdout: " +
                            RedactSensitiveText(e.Data, processPinSnapshot));
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        ObserveCoreDiscoveryMarker(processId, e.Data);
                        Log("core[" + processId + "]/stderr: " +
                            RedactSensitiveText(e.Data, processPinSnapshot));
                    }
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                InstallRendererMoveSizeHook(processId);
                startAfterNetworkCheck = false;
                resumeAfterSafeNetwork = false;
                Log("Core started, PID " + coreProcess.Id +
                    "; arguments: " + BuildSafeUxPlayArguments() + ".");
                SetState(true, "Приёмник запускается…");
            }
            catch (Exception ex)
            {
                Log("ERROR starting core: " + ex);
                if (coreProcess != null)
                {
                    try
                    {
                        if (!coreProcess.HasExited)
                            coreProcess.Kill();
                        coreProcess.WaitForExit(1500);
                        CancelCoreOutputReads(coreProcess);
                    }
                    catch { }
                    Process failedProcess = coreProcess;
                    DetachCoreProcessForLifecycle(failedProcess);
                    failedProcess.Dispose();
                }
                NativeMethods.CloseHandleSafe(ref coreJob);
                coreReadyPending = false;
                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                Interlocked.Exchange(ref activeCorePid, 0);
                ResetRendererMoveSizeTracking();
                SetState(false, "Ошибка запуска");
                if (settings.Notify)
                    tray.ShowBalloonTip(
                        5000, AppTitle, ex.Message, ToolTipIcon.Error);
            }
        }

        public void StopCore()
        {
            CloseLostConnectionPlaceholder();
            startAfterNetworkCheck = false;
            Interlocked.Exchange(
                ref discoveryRefreshAfterNetworkCheck, 0);
            resumeAfterSafeNetwork = false;
            restartPending = false;
            ResetCoreSessionTracking(true);
            if (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
            {
                restartAfterStop = false;
                SetState(false, "Приёмник останавливается…");
                return;
            }
            StopCoreInternal("manual stop", true, true);
        }

        private void StopCoreInternal(
            string reason, bool graceful, bool resetRapidExit)
        {
            FlushStreamWindowPlacementBeforeCoreStop();
            Process process = coreProcess;
            DetachCoreProcessForLifecycle(process);
            Interlocked.Exchange(ref activeCorePid, 0);
            ResetCoreSessionTracking(true);
            IntPtr job = coreJob;
            coreJob = IntPtr.Zero;
            if (resetRapidExit)
            {
                rapidExitCount = 0;
                rapidExitWindowStartedAt = DateTime.MinValue;
            }
            if (process == null)
            {
                SetState(false, "Приёмник остановлен");
                return;
            }
            StopDetachedCore(process, job, reason, graceful);
            fittedStreamWindow = IntPtr.Zero;
            coreReadyPending = false;
            Interlocked.Exchange(ref coreSocketsReady, 0);
            Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
            SetState(false, "Приёмник остановлен");
        }

        public void RestartCore()
        {
            ResetRapidExitWindow();
            RestartCore(true);
        }

        private void RestartCore(bool notify)
        {
            ResetSharedAutomaticRecoveryBudget();
            ScheduleRestart("manual restart", notify, 1000);
        }

        private void ScheduleRestart(
            string reason, bool notify, int delayMilliseconds)
        {
            restartPending = false;
            restartReason = reason;
            if (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
            {
                restartAfterStop = true;
                restartDelayAfterStop = delayMilliseconds;
                Log("Updated pending core restart; reason: " + reason + ".");
                return;
            }
            if (IsCoreRunning)
            {
                FlushStreamWindowPlacementBeforeCoreStop();
                Process process = coreProcess;
                DetachCoreProcessForLifecycle(process);
                Interlocked.Exchange(ref activeCorePid, 0);
                ResetCoreSessionTracking(true);
                IntPtr job = coreJob;
                coreJob = IntPtr.Zero;
                fittedStreamWindow = IntPtr.Zero;
                coreReadyPending = false;
                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                restartAfterStop = true;
                restartDelayAfterStop = delayMilliseconds;
                restartStopDone.Reset();
                Interlocked.Exchange(ref restartStopInProgress, 1);
                SetState(false, "Приёмник перезапускается…");
                Log("Asynchronous core stop started; reason: " +
                    reason + ".");
                ThreadPool.QueueUserWorkItem(delegate
                {
                    StopDetachedCore(process, job, reason, true);
                    Interlocked.Exchange(ref restartStopCompleted, 1);
                    restartStopDone.Set();
                });
                return;
            }
            restartDueUtc =
                DateTime.UtcNow.AddMilliseconds(delayMilliseconds);
            restartPending = true;
            Log("Core restart scheduled in " + delayMilliseconds +
                " ms; reason: " + reason + ".");
        }

        private static void StopDetachedCore(
            Process process, IntPtr job, string reason, bool graceful)
        {
            IntPtr jobHandle = job;
            try
            {
                Log("Stopping core PID " + process.Id +
                    "; reason: " + reason + "; graceful: " + graceful + ".");
                bool exited = process.HasExited;
                if (!exited && graceful && process.CloseMainWindow())
                    exited = process.WaitForExit(750);
                if (!exited)
                {
                    if (jobHandle != IntPtr.Zero)
                        NativeMethods.TerminateAndCloseJobSafe(ref jobHandle);
                    else
                        process.Kill();
                    exited = process.WaitForExit(2500);
                }
                if (!exited)
                {
                    Log("Core PID " + process.Id +
                        " did not exit after Job Object termination; " +
                        "using the direct process kill fallback.");
                    try { process.Kill(); }
                    catch { }
                    try { exited = process.WaitForExit(1500); }
                    catch { exited = false; }
                }
                CancelCoreOutputReads(process);
                NativeMethods.CloseHandleSafe(ref jobHandle);
                Log("Core stop completed; exited: " + exited + ".");
            }
            catch (Exception ex)
            {
                Log("ERROR stopping core: " + ex.Message);
                NativeMethods.CloseHandleSafe(ref jobHandle);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void CancelCoreOutputReads(Process process)
        {
            if (process == null)
                return;
            try { process.CancelOutputRead(); }
            catch { }
            try { process.CancelErrorRead(); }
            catch { }
        }

        public void RefreshDiscovery()
        {
            ResetRapidExitWindow();
            ResetSharedAutomaticRecoveryBudget();
            ResetIdleDiscoveryRenewalSchedule();
            Log("Manual AirPlay discovery refresh requested.");
            Interlocked.Exchange(
                ref discoveryRefreshAfterNetworkCheck, 1);
            networkUnknownRetries = 0;
            Log("Discovery refresh will re-register AirPlay after the latest " +
                "physical network profile and IPv4 address are confirmed.");
            BeginNetworkProfileRefresh();
        }

        private bool TryRequestNativeDiscoveryRefresh(
            string reason, bool restartOnFailure)
        {
            Process process = coreProcess;
            if (process == null || !IsCoreRunning ||
                Interlocked.CompareExchange(
                    ref coreDiscoveryRefreshCapability, 0, 0) != 1)
                return false;

            int expectedPort = Interlocked.CompareExchange(
                ref coreHttpPort, 0, 0);
            if (expectedPort <= 0)
                return false;

            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                int processId;
                try { processId = process.Id; }
                catch { return false; }
                if (!object.ReferenceEquals(coreProcess, process) ||
                    !IsCoreRunning || restartPending ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1 ||
                    Interlocked.CompareExchange(
                        ref coreDiscoveryRefreshCapability, 0, 0) != 1 ||
                    Interlocked.CompareExchange(ref activeCorePid, 0, 0) !=
                        processId ||
                    Interlocked.CompareExchange(ref coreHttpPort, 0, 0) !=
                        expectedPort)
                    return false;
                if (Interlocked.Read(
                        ref coreDiscoveryRefreshPendingRequest) > 0)
                    return true;
                long request = Interlocked.Increment(
                    ref coreDiscoveryRefreshRequestSequence);
                try
                {
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshPendingPid, processId);
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshPendingPort, expectedPort);
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshDueTicks,
                        DateTime.UtcNow.AddSeconds(12).Ticks);
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshPhase, 0);
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshFallbackPending,
                        restartOnFailure ? 1 : 0);
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshPendingRequest, request);
                    Log("Native same-process discovery refresh requested; " +
                        "request " + request + ", PID " + processId +
                        ", AirPlay port " + expectedPort + "; reason: " +
                        reason + ".");
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            lock (coreCommandSync)
                            {
                                if (request != Interlocked.Read(
                                        ref coreDiscoveryRefreshPendingRequest) ||
                                    processId != Interlocked.CompareExchange(
                                        ref activeCorePid, 0, 0))
                                    return;
                                process.StandardInput.WriteLine(
                                    "AEROMIRROR_COMMAND refresh-discovery request=" +
                                    request);
                                process.StandardInput.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("Native discovery command writer failed for " +
                                "request " + request + ": " + ex.Message);
                        }
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    Log("Native discovery command could not be written: " +
                        ex.Message);
                    if (request == Interlocked.Read(
                            ref coreDiscoveryRefreshPendingRequest))
                        ClearNativeDiscoveryRefreshRequestLocked();
                    return false;
                }
            }
        }

        private bool TryWriteNativeVideoCommand(
            string command, string description)
        {
            if (string.IsNullOrWhiteSpace(command) ||
                command.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                return false;
            Process process = coreProcess;
            if (process == null || !IsCoreRunning)
                return false;
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                int processId;
                try { processId = process.Id; }
                catch { return false; }
                if (!object.ReferenceEquals(coreProcess, process) ||
                    !IsCoreRunning || restartPending ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1 ||
                    Interlocked.CompareExchange(ref activeCorePid, 0, 0) !=
                        processId)
                    return false;
                try
                {
                    process.StandardInput.WriteLine(
                        "AEROMIRROR_COMMAND " + command);
                    process.StandardInput.Flush();
                    Log("Native video command requested; " + description +
                        ", PID " + processId + ".");
                    return true;
                }
                catch (Exception ex)
                {
                    Log("Native video command failed; " + description +
                        ": " + ex.Message);
                    return false;
                }
            }
        }

        private void ClearNativeDiscoveryRefreshRequest()
        {
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
                ClearNativeDiscoveryRefreshRequestLocked();
        }

        private void ResetNativeDiscoveryRefreshForProcessLifecycle()
        {
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                Interlocked.Exchange(
                    ref coreDiscoveryRefreshCapability, 0);
                ClearNativeDiscoveryRefreshRequestLocked();
            }
        }

        private void DetachCoreProcessForLifecycle(Process expectedProcess)
        {
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                if (object.ReferenceEquals(coreProcess, expectedProcess))
                    coreProcess = null;
                Interlocked.Exchange(
                    ref coreDiscoveryRefreshCapability, 0);
                ClearNativeDiscoveryRefreshRequestLocked();
            }
        }

        private void ClearNativeDiscoveryRefreshRequestLocked()
        {
            Interlocked.Exchange(ref coreDiscoveryRefreshPendingRequest, 0);
            Interlocked.Exchange(ref coreDiscoveryRefreshPendingPid, 0);
            Interlocked.Exchange(ref coreDiscoveryRefreshPendingPort, 0);
            Interlocked.Exchange(ref coreDiscoveryRefreshDueTicks, 0);
            Interlocked.Exchange(ref coreDiscoveryRefreshPhase, 0);
            Interlocked.Exchange(ref coreDiscoveryRefreshFallbackPending, 0);
        }

        private void HandleNativeDiscoveryRefreshTimeout()
        {
            long request = Interlocked.Read(
                ref coreDiscoveryRefreshPendingRequest);
            if (request <= 0)
                return;
            long dueTicks = Interlocked.Read(
                ref coreDiscoveryRefreshDueTicks);
            DateTime now = DateTime.UtcNow;
            if (dueTicks <= 0 || now.Ticks < dueTicks)
                return;

            lock (postSessionMaintenanceSync)
            {
                request = Interlocked.Read(
                    ref coreDiscoveryRefreshPendingRequest);
                dueTicks = Interlocked.Read(
                    ref coreDiscoveryRefreshDueTicks);
                if (request <= 0 || dueTicks <= 0 ||
                    Interlocked.CompareExchange(
                        ref coreDiscoveryRefreshPhase, 0, 0) == 1 ||
                    now.Ticks < dueTicks)
                    return;
                if (ShouldDeferDisruptiveMaintenance(
                        IsMirrorSessionActive,
                        Interlocked.Read(ref clientActivityGraceDueTicks),
                        now.Ticks))
                {
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshDueTicks,
                        now.AddSeconds(5).Ticks);
                    return;
                }

                bool fallback = false;
                bool claimed = false;
                lock (coreCommandSync)
                {
                    if (request == Interlocked.Read(
                            ref coreDiscoveryRefreshPendingRequest))
                    {
                        fallback = Interlocked.CompareExchange(
                            ref coreDiscoveryRefreshFallbackPending,
                            0, 0) == 1;
                        ClearNativeDiscoveryRefreshRequestLocked();
                        claimed = true;
                    }
                }
                if (!claimed)
                    return;
                if (fallback && IsCoreRunning)
                {
                    Log("Native discovery refresh request " + request +
                        " did not receive a correlated terminal result; " +
                        "using the bounded legacy process-restart fallback.");
                    ScheduleRestart(
                        "native discovery refresh timeout", false, 500);
                }
                else if (IsCoreRunning)
                {
                    Log("Periodic native discovery refresh request " +
                        request + " timed out; the receiver remains running " +
                        "and will retry on the recurring idle schedule.");
                    ArmIdleDiscoveryRenewalIfAvailable();
                }
            }
        }

        private void ObserveCoreOutput(int processId, string line)
        {
            if (Interlocked.CompareExchange(
                    ref activeCorePid, 0, 0) != processId)
                return;

            ObserveCoreHttpLifecycle(processId, line);

            if (IsIncomingAirPlayConnectionRequestMarker(line))
                HandleIncomingAirPlayClientActivity(
                    processId, ConnectionRequestGraceSeconds,
                    "AirPlay connection request");
            else if (IsAirPlayPinEntryMarker(line))
                HandleIncomingAirPlayClientActivity(
                    processId, PinEntryGraceSeconds,
                    "AirPlay authentication progress");

            ObserveClientFeedbackHealth(processId, line);
            ObserveRecoveredVideoPresentation(processId, line);

            Match chosenDeviceId = Regex.Match(
                line,
                @"\busing (?:system|user-set|randomly-generated) MAC address " +
                    @"([0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5})\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (chosenDeviceId.Success)
                AppSettings.RememberReceiverDeviceId(
                    chosenDeviceId.Groups[1].Value);

            if (line.IndexOf(
                    "raop_rtp_mirror starting mirroring",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (HandleMirroringStartedMaintenance(processId))
                {
                    lock (videoSizeSync)
                    {
                        pendingVideoSize = Size.Empty;
                        pendingVideoSizeDueUtc = DateTime.MinValue;
                        pendingVideoSizeSequence = 0;
                        pendingVideoSizeIsAmbiguousMediaCanvas = false;
                        currentVideoSize = Size.Empty;
                        currentVideoSizeSequence = 0;
                        currentVideoSizeIsAmbiguousMediaCanvas = false;
                        rawGeometryVideoSize = Size.Empty;
                        rawGeometryVideoSizeGeneration = 0;
                        rawGeometryIsAmbiguousMediaCanvas = false;
                        earlyDeviceFrameVideoSize = Size.Empty;
                        deviceFrameVideoSize = Size.Empty;
                        lastSuppressedVideoSize = Size.Empty;
                    }
                }
            }

            if (line.IndexOf(
                    "raop_rtp_mirror->running is no longer true",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                HandleMirroringEndedMaintenance(processId);
            }

            ObserveCoreDiscoveryMarker(processId, line);

            bool lostClient = line.IndexOf(
                "lost connection with client",
                StringComparison.OrdinalIgnoreCase) >= 0;
            bool mirrorReceiveError = line.IndexOf(
                "raop_rtp_mirror error in recv",
                StringComparison.OrdinalIgnoreCase) >= 0;
            if ((lostClient || mirrorReceiveError) && IsMirrorSessionActive)
                ArmLostConnectionRecovery(
                    processId,
                    lostClient ? "lost mirroring client" :
                        "fatal mirror receive error");

            Match videoGeometry = Regex.Match(
                line,
                @"^AEROMIRROR_VIDEO_GEOMETRY width0=(\d+) height0=(\d+) " +
                    @"source=(\d+)x(\d+) aux=(\d+)x(\d+) " +
                    @"encoded=(\d+)x(\d+)$",
                RegexOptions.CultureInvariant);
            if (videoGeometry.Success)
            {
                int width0 = 0;
                int height0 = 0;
                int sourceWidth = 0;
                int sourceHeight = 0;
                int auxiliaryWidth = 0;
                int auxiliaryHeight = 0;
                int encodedWidth = 0;
                int encodedHeight = 0;
                bool valid =
                    int.TryParse(videoGeometry.Groups[1].Value, out width0) &&
                    int.TryParse(videoGeometry.Groups[2].Value, out height0) &&
                    int.TryParse(videoGeometry.Groups[3].Value, out sourceWidth) &&
                    int.TryParse(videoGeometry.Groups[4].Value, out sourceHeight) &&
                    int.TryParse(videoGeometry.Groups[5].Value, out auxiliaryWidth) &&
                    int.TryParse(videoGeometry.Groups[6].Value, out auxiliaryHeight) &&
                    int.TryParse(videoGeometry.Groups[7].Value, out encodedWidth) &&
                    int.TryParse(videoGeometry.Groups[8].Value, out encodedHeight) &&
                    encodedWidth >= 64 && encodedWidth <= 8192 &&
                    encodedHeight >= 64 && encodedHeight <= 8192;
                lock (videoSizeSync)
                {
                    rawGeometryVideoSize = valid
                        ? new Size(encodedWidth, encodedHeight)
                        : Size.Empty;
                    rawGeometryVideoSizeGeneration = valid
                        ? Interlocked.CompareExchange(
                            ref mirrorSessionGeneration, 0, 0)
                        : 0;
                    rawGeometryIsAmbiguousMediaCanvas = valid &&
                        IsKnownAmbiguousMediaCanvasGeometry(
                            width0, height0,
                            sourceWidth, sourceHeight,
                            auxiliaryWidth, auxiliaryHeight,
                            encodedWidth, encodedHeight);
                }
            }

            Match videoSize = Regex.Match(
                line,
                @"^AEROMIRROR_VIDEO_SIZE source=(\d+)x(\d+) encoded=(\d+)x(\d+)$",
                RegexOptions.CultureInvariant);
            if (videoSize.Success)
            {
                int sourceWidth;
                int sourceHeight;
                int width;
                int height;
                if (int.TryParse(
                        videoSize.Groups[1].Value, out sourceWidth) &&
                    int.TryParse(
                        videoSize.Groups[2].Value, out sourceHeight) &&
                    int.TryParse(videoSize.Groups[3].Value, out width) &&
                    int.TryParse(videoSize.Groups[4].Value, out height) &&
                    sourceWidth >= 64 && sourceWidth <= 8192 &&
                    sourceHeight >= 64 && sourceHeight <= 8192 &&
                    width >= 64 && width <= 8192 &&
                    height >= 64 && height <= 8192)
                {
                    bool capturedEarlyDeviceFrame = false;
                    lock (videoSizeSync)
                    {
                        Size observedVideoSize = new Size(width, height);
                        int sessionGeneration =
                            Interlocked.CompareExchange(
                                ref mirrorSessionGeneration, 0, 0);
                        bool ambiguousMediaCanvas =
                            rawGeometryVideoSizeGeneration ==
                                sessionGeneration &&
                            rawGeometryVideoSize == observedVideoSize &&
                            sourceWidth == width &&
                            sourceHeight == height &&
                            rawGeometryIsAmbiguousMediaCanvas;
                        rawGeometryVideoSize = Size.Empty;
                        rawGeometryVideoSizeGeneration = 0;
                        rawGeometryIsAmbiguousMediaCanvas = false;
                        long geometrySequence =
                            ++videoGeometryEventSequence;
                        if (earlyDeviceFrameVideoSize.IsEmpty &&
                            IsLikelyModernIPhoneDeviceFrame(observedVideoSize))
                        {
                            earlyDeviceFrameVideoSize = observedVideoSize;
                            capturedEarlyDeviceFrame = true;
                        }
                        VideoSizeCandidateAction candidateAction =
                            DecideVideoSizeCandidateAction(
                                currentVideoSize,
                                currentVideoSizeIsAmbiguousMediaCanvas,
                                pendingVideoSize,
                                pendingVideoSizeIsAmbiguousMediaCanvas,
                                observedVideoSize,
                                ambiguousMediaCanvas);
                        if (candidateAction ==
                            VideoSizeCandidateAction.CancelPending)
                        {
                            ClearPendingVideoSizeLocked();
                        }
                        else if (candidateAction ==
                            VideoSizeCandidateAction.RetainPendingDeadline)
                        {
                            // A repeated codec-size packet proves a newer
                            // event but must not keep moving the same stable
                            // candidate's debounce deadline into the future.
                            pendingVideoSizeSequence = geometrySequence;
                        }
                        else if (candidateAction ==
                            VideoSizeCandidateAction.ArmPending)
                        {
                            pendingVideoSize = observedVideoSize;
                            pendingVideoSizeSequence = geometrySequence;
                            pendingVideoSizeIsAmbiguousMediaCanvas =
                                ambiguousMediaCanvas;
                            pendingVideoSizeDueUtc =
                                DateTime.UtcNow.AddMilliseconds(350);
                        }
                    }
                    if (capturedEarlyDeviceFrame)
                    {
                        Log("Captured early phone-shaped device frame " +
                            width + "x" + height +
                            " before video-size debounce.");
                    }
                }
            }

        }

        private static VideoSizeCandidateAction
            DecideVideoSizeCandidateAction(
            Size currentSize,
            bool currentIsAmbiguousMediaCanvas,
            Size pendingSize,
            bool pendingIsAmbiguousMediaCanvas,
            Size observedSize,
            bool observedIsAmbiguousMediaCanvas)
        {
            bool matchesCurrent = currentSize == observedSize &&
                currentIsAmbiguousMediaCanvas ==
                    observedIsAmbiguousMediaCanvas;
            if (matchesCurrent)
            {
                return pendingSize.IsEmpty
                    ? VideoSizeCandidateAction.None
                    : VideoSizeCandidateAction.CancelPending;
            }

            bool matchesPending = pendingSize == observedSize &&
                pendingIsAmbiguousMediaCanvas ==
                    observedIsAmbiguousMediaCanvas;
            return matchesPending
                ? VideoSizeCandidateAction.RetainPendingDeadline
                : VideoSizeCandidateAction.ArmPending;
        }

        private void ClearPendingVideoSizeLocked()
        {
            pendingVideoSize = Size.Empty;
            pendingVideoSizeDueUtc = DateTime.MinValue;
            pendingVideoSizeSequence = 0;
            pendingVideoSizeIsAmbiguousMediaCanvas = false;
        }

        private static bool IsIncomingAirPlayConnectionRequestMarker(
            string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
            return line.TrimStart().StartsWith(
                "connection request from ",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAirPlayPinEntryMarker(string line)
        {
            return !string.IsNullOrWhiteSpace(line) &&
                line.TrimStart().StartsWith(
                    "*** CLIENT MUST NOW ENTER PIN = ",
                    StringComparison.OrdinalIgnoreCase);
        }

        private void HandleIncomingAirPlayClientActivity(
            int processId, int graceSeconds, string evidence)
        {
            bool deferredSettingsPostponed = false;
            DateTime now = DateTime.UtcNow;
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return;
                ResolveCoreReadinessFromClientActivityLocked(
                    evidence);
                CancelCoreDiscoveryRecovery(true);
                Interlocked.Exchange(ref lostConnectionRecoveryPending, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
                ResetLostConnectionHttpResetAttempt();

                Interlocked.Exchange(
                    ref clientActivityGraceDueTicks,
                    now.AddSeconds(graceSeconds).Ticks);

                if (Interlocked.CompareExchange(
                        ref mirrorSessionEndedPending, 0, 0) == 1)
                {
                    if (IsSettingsRestartDeferred)
                    {
                        Interlocked.Exchange(
                            ref mirrorSessionEndedDueTicks,
                            now.AddSeconds(graceSeconds).Ticks);
                        deferredSettingsPostponed = true;
                    }
                    else
                    {
                        Interlocked.Exchange(
                            ref mirrorSessionEndedPending, 0);
                        Interlocked.Exchange(
                            ref mirrorSessionEndedDueTicks, 0);
                    }
                }

                Interlocked.Exchange(ref idleDiscoveryRenewalUsed, 0);
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks,
                    now.AddMinutes(
                        GetIdleDiscoveryRenewalDelayMinutes(0)).Ticks);
            }

            Log("Incoming AirPlay client activity observed; " +
                (deferredSettingsPostponed
                    ? "the deferred settings restart received a new " +
                        graceSeconds + "-second grace period"
                    : "no post-session settings restart was pending") +
                ", and the persistent idle discovery schedule was re-armed " +
                "with its first renewal in ten minutes.");
        }

        private bool HandleMirroringStartedMaintenance(int processId)
        {
            bool canceledDeferredRestart;
            bool feedbackRecoverySuperseded;
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return false;
                ResolveCoreReadinessFromClientActivityLocked(
                    "mirroring start");
                CancelCoreDiscoveryRecovery(true);
                Interlocked.Exchange(ref lostConnectionRecoveryPending, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
                ResetLostConnectionHttpResetAttempt();
                Interlocked.Exchange(ref clientActivityGraceDueTicks, 0);
                feedbackRecoverySuperseded =
                    Interlocked.CompareExchange(
                        ref feedbackGapPlaceholderActive, 0, 0) == 1;
                Interlocked.Exchange(ref feedbackGapEpisodeActive, 0);
                Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
                ResetFeedbackVideoRecoveryWaitLocked();
                int sessionGeneration = Interlocked.Increment(
                    ref mirrorSessionGeneration);
                Interlocked.Exchange(ref mirrorSessionActive, 1);
                if (feedbackRecoverySuperseded)
                {
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryPending, 1);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryPid, processId);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryEpoch, 0);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryGapSeconds, 0);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoverySessionGeneration,
                        sessionGeneration);
                    Interlocked.Exchange(
                        ref feedbackVideoMirrorStartArmExpected, 1);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryWaitDueTicks,
                        DateTime.UtcNow.AddSeconds(
                            FeedbackVideoRecoveryWaitSeconds).Ticks);
                    QueueLostConnectionRecoveredWait();
                }
                else
                {
                    Interlocked.Exchange(
                        ref feedbackGapPlaceholderActive, 0);
                    QueueLostConnectionRendererHandoff();
                }
                canceledDeferredRestart = Interlocked.Exchange(
                    ref mirrorSessionEndedPending, 0) == 1;
                Interlocked.Exchange(ref mirrorSessionEndedDueTicks, 0);
                Interlocked.Exchange(ref idleDiscoveryRenewalUsed, 0);
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks,
                    DateTime.UtcNow.AddMinutes(
                        GetIdleDiscoveryRenewalDelayMinutes(0)).Ticks);
            }
            if (canceledDeferredRestart)
                Log("Mirroring started; pending post-session settings " +
                    "maintenance was canceled and remains deferred until " +
                    "this session ends.");
            if (feedbackRecoverySuperseded)
            {
                Log("A new mirroring start superseded a same-session feedback " +
                    "recovery before presentation proof; continuity will wait " +
                    "for a new mirror-start presentation challenge instead of " +
                    "trusting the stale renderer window.");
            }
            return true;
        }

        private void ResolveCoreReadinessFromClientActivityLocked(
            string evidence)
        {
            if (!coreReadyPending && coreReadinessRecoveryAttempts == 0)
                return;
            coreReadyPending = false;
            coreReadyChecks = 0;
            coreReadyDueUtc = DateTime.MinValue;
            coreReadinessRecoveryAttempts = 0;
            coreReadinessPid = 0;
            Interlocked.Exchange(ref coreClientActivityReadyPending, 1);
            Log("Core readiness was confirmed by " + evidence +
                "; automatic readiness recovery was canceled.");
        }

        private void HandleMirroringEndedMaintenance(int processId)
        {
            string message = "";
            bool closeTransientFeedbackPlaceholder = false;
            bool showReconnectHint = false;
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return;
                bool abnormalLossRecoveryPending =
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPid, 0, 0) == processId;
                if (!abnormalLossRecoveryPending)
                {
                    Interlocked.Exchange(
                        ref lostConnectionRecoveryPending, 0);
                    Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
                    Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
                    ResetLostConnectionHttpResetAttempt();
                }
                if (Interlocked.Exchange(ref mirrorSessionActive, 0) != 1)
                    return;
                Interlocked.Exchange(ref feedbackGapEpisodeActive, 0);
                Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
                ResetFeedbackVideoRecoveryWaitLocked();
                if (!abnormalLossRecoveryPending)
                {
                    closeTransientFeedbackPlaceholder = Interlocked.Exchange(
                        ref feedbackGapPlaceholderActive, 0) == 1 ||
                        Interlocked.CompareExchange(
                            ref lostConnectionRendererHandoffPending,
                            0, 0) == 1;
                }
                else
                {
                    showReconnectHint = true;
                }

                DateTime now = DateTime.UtcNow;
                long reconnectGraceDueTicks = Interlocked.Read(
                    ref clientActivityGraceDueTicks);
                if (reconnectGraceDueTicks <= now.Ticks)
                {
                    reconnectGraceDueTicks = 0;
                    Interlocked.Exchange(ref clientActivityGraceDueTicks, 0);
                }
                if (Interlocked.CompareExchange(
                        ref idleDiscoveryRenewalUsed, 0, 0) == 0)
                {
                    Interlocked.Exchange(
                        ref idleDiscoveryRenewalDueTicks,
                        now.AddMinutes(
                            GetIdleDiscoveryRenewalDelayMinutes(0)).Ticks);
                }
                if (IsSettingsRestartDeferred)
                {
                    Interlocked.Exchange(
                        ref mirrorSessionEndedDueTicks,
                        Math.Max(
                            now.AddSeconds(5).Ticks,
                            reconnectGraceDueTicks));
                    Interlocked.Exchange(ref mirrorSessionEndedPending, 1);
                    message = "Mirroring session cleanup completed; saved " +
                        "receiver settings will be applied after the active " +
                        "reconnect grace period.";
                }
                else
                {
                    Interlocked.Exchange(ref mirrorSessionEndedPending, 0);
                    Interlocked.Exchange(ref mirrorSessionEndedDueTicks, 0);
                    message = abnormalLossRecoveryPending
                        ? "Mirroring session cleanup completed after an abnormal " +
                            "client loss; the bounded watchdog will preserve " +
                            "UxPlay's in-process recovery when its socket reset " +
                            "has completed."
                        : "Mirroring session cleanup completed; the receiver " +
                            "stays running and no post-session restart was " +
                            "scheduled. The bounded idle discovery sequence " +
                            "remains armed.";
                }
            }
            Log(message);
            if (showReconnectHint)
                QueueLostConnectionReconnectHint();
            if (closeTransientFeedbackPlaceholder)
                QueueLostConnectionPlaceholderClose();
        }

        private void ObserveClientFeedbackHealth(int processId, string line)
        {
            if (line.IndexOf(
                    "AEROMIRROR_FEEDBACK_HEALTH_READY",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Interlocked.Exchange(ref feedbackHealthMarkersReady, 1);
                return;
            }

            int warningSeconds = ParseClientFeedbackWarningSeconds(line);
            if (warningSeconds > 0)
            {
                bool showPlaceholder = false;
                lock (postSessionMaintenanceSync)
                {
                    if (Interlocked.CompareExchange(
                            ref activeCorePid, 0, 0) != processId ||
                        !IsMirrorSessionActive)
                        return;

                    if (Interlocked.CompareExchange(
                            ref feedbackGapEpisodeActive, 1, 0) == 0)
                    {
                        Interlocked.Increment(ref feedbackGapEpisodeCount);
                        ResetFeedbackVideoRecoveryWaitLocked();
                        if (Interlocked.CompareExchange(
                                ref feedbackGapPlaceholderActive, 0, 0) == 1)
                            QueueLostConnectionPlaceholder();
                    }
                    UpdateMaximum(
                        ref feedbackGapLongestSeconds, warningSeconds);
                    if (Interlocked.CompareExchange(
                            ref feedbackHealthMarkersReady, 0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref feedbackGapPlaceholderActive, 0, 0) == 0)
                    {
                        long candidateDueTicks =
                            CalculateFeedbackGapPlaceholderDueTicks(
                                warningSeconds, DateTime.UtcNow.Ticks);
                        long currentDueTicks = Interlocked.Read(
                            ref feedbackGapPlaceholderDueTicks);
                        if (currentDueTicks <= 0 ||
                            candidateDueTicks < currentDueTicks)
                        {
                            Interlocked.Exchange(
                                ref feedbackGapPlaceholderDueTicks,
                                candidateDueTicks);
                        }
                    }
                    showPlaceholder =
                        TryQueueDueFeedbackGapPlaceholderLocked(
                            DateTime.UtcNow.Ticks);
                }

                if (showPlaceholder)
                {
                    Log("AirPlay client feedback reached the bounded " +
                        "four-second continuity threshold; showing the " +
                        "last frame while the existing session is still " +
                        "allowed to recover.");
                }
                return;
            }

            int recoveredGapSeconds = ParseClientFeedbackRecoverySeconds(line);
            if (recoveredGapSeconds <= 0)
                return;
            int recoveredEpoch = ParseClientFeedbackRecoveryEpoch(line);

            bool waitForVideo = false;
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId ||
                    !IsMirrorSessionActive ||
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 1)
                    return;
                UpdateMaximum(
                    ref feedbackGapLongestSeconds, recoveredGapSeconds);
                Interlocked.Exchange(ref feedbackGapEpisodeActive, 0);
                Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
                waitForVideo = Interlocked.CompareExchange(
                    ref feedbackGapPlaceholderActive, 0, 0) == 1;
                if (waitForVideo)
                {
                    Interlocked.Exchange(
                        ref feedbackVideoMirrorStartArmExpected, 0);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryPending, 1);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryPid, processId);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryEpoch, recoveredEpoch);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryGapSeconds,
                        recoveredGapSeconds);
                    Interlocked.Exchange(
                        ref feedbackVideoRecoverySessionGeneration,
                        Interlocked.CompareExchange(
                            ref mirrorSessionGeneration, 0, 0));
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryWaitDueTicks,
                        DateTime.UtcNow.AddSeconds(
                            FeedbackVideoRecoveryWaitSeconds).Ticks);
                    QueueLostConnectionRecoveredWait();
                }
                else
                {
                    ResetFeedbackVideoRecoveryWaitLocked();
                }
            }
            Log("AirPlay client feedback resumed after a " +
                recoveredGapSeconds + "-second gap; the existing " +
                "mirroring session remains active" +
                (waitForVideo
                    ? recoveredEpoch > 0
                        ? "; continuity will wait for matching D3D11 " +
                            "presentation proof for recovery epoch " +
                            recoveredEpoch + "."
                        : "; this core supplied no presentation epoch, so " +
                            "continuity will remain visible and offer manual " +
                            "reconnect guidance."
                    : "."));
        }

        private void HandleFeedbackGapPlaceholderTimer()
        {
            bool showPlaceholder;
            lock (postSessionMaintenanceSync)
            {
                showPlaceholder =
                    TryQueueDueFeedbackGapPlaceholderLocked(
                        DateTime.UtcNow.Ticks);
            }
            if (showPlaceholder)
            {
                Log("AirPlay client feedback reached the bounded " +
                    "four-second continuity threshold; showing the last " +
                    "frame while the existing session is still allowed " +
                    "to recover.");
            }
        }

        private void ObserveRecoveredVideoPresentation(
            int processId, string line)
        {
            if (Regex.IsMatch(
                    line ?? "",
                    @"^AEROMIRROR_VIDEO_PRESENT_PROOF_READY " +
                        @"codec=(?:h264|h265) videosink=d3d11videosink$",
                    RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant))
            {
                lock (postSessionMaintenanceSync)
                {
                    if (Interlocked.CompareExchange(
                            ref activeCorePid, 0, 0) != processId)
                        return;
                    Interlocked.Exchange(
                        ref feedbackVideoPresentProofPid, processId);
                    Interlocked.Exchange(
                        ref feedbackVideoPresentProofReady, 1);
                }
                return;
            }

            int armedEpoch;
            if (TryParseVideoPresentArmed(line, out armedEpoch))
            {
                bool armed = false;
                int armedSessionGeneration = 0;
                lock (postSessionMaintenanceSync)
                {
                    int sessionGeneration = Interlocked.CompareExchange(
                        ref mirrorSessionGeneration, 0, 0);
                    if (Interlocked.CompareExchange(
                            ref activeCorePid, 0, 0) == processId &&
                        IsMirrorSessionActive &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoPresentProofReady, 0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoPresentProofPid, 0, 0) ==
                            processId &&
                        Interlocked.CompareExchange(
                            ref feedbackGapPlaceholderActive, 0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoRecoveryPending, 0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoRecoveryPid, 0, 0) == processId &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoRecoverySessionGeneration,
                            0, 0) == sessionGeneration &&
                        Interlocked.CompareExchange(
                            ref feedbackVideoMirrorStartArmExpected,
                            0, 0) == 1 &&
                        Interlocked.CompareExchange(
                            ref lostConnectionRecoveryPending, 0, 0) == 0)
                    {
                        Interlocked.Exchange(
                            ref feedbackVideoRecoveryEpoch, armedEpoch);
                        Interlocked.Exchange(
                            ref feedbackVideoMirrorStartArmExpected, 0);
                        Interlocked.Exchange(
                            ref feedbackVideoRecoveryGapSeconds, 0);
                        Interlocked.Exchange(
                            ref feedbackVideoRecoveryWaitDueTicks,
                            DateTime.UtcNow.AddSeconds(
                                FeedbackVideoRecoveryWaitSeconds).Ticks);
                        QueueLostConnectionRecoveredWait();
                        armed = true;
                        armedSessionGeneration = sessionGeneration;
                    }
                }
                Log(armed
                    ? "Accepted mirror-start D3D11 presentation challenge " +
                        armedEpoch + " for core " + processId +
                        ", session " + armedSessionGeneration + "."
                    : "Ignored stale or unmatched mirror-start D3D11 " +
                        "presentation challenge " + armedEpoch +
                        " for core " + processId + ".");
                return;
            }

            int epoch;
            int gapSeconds;
            int ptsDeltaMilliseconds;
            if (!TryParseVideoPresentReady(
                    line, out epoch, out gapSeconds,
                    out ptsDeltaMilliseconds))
                return;

            bool accepted = false;
            int acceptedSessionGeneration = 0;
            lock (postSessionMaintenanceSync)
            {
                int sessionGeneration = Interlocked.CompareExchange(
                    ref mirrorSessionGeneration, 0, 0);
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) == processId &&
                    IsMirrorSessionActive &&
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 0 &&
                    Interlocked.CompareExchange(
                        ref feedbackGapPlaceholderActive, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoPresentProofReady, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoPresentProofPid, 0, 0) == processId &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryPending, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryPid, 0, 0) == processId &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryEpoch, 0, 0) == epoch &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryGapSeconds, 0, 0) ==
                        gapSeconds &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoverySessionGeneration, 0, 0) ==
                        sessionGeneration &&
                    Interlocked.CompareExchange(
                        ref lostConnectionFeedbackHandoffPending, 0, 0) == 0)
                {
                    Interlocked.Exchange(
                        ref feedbackVideoRecoveryWaitDueTicks, 0);
                    QueueLostConnectionFeedbackRendererHandoff(
                        processId, sessionGeneration, epoch);
                    accepted = true;
                    acceptedSessionGeneration = sessionGeneration;
                }
            }

            Log(accepted
                ? "Fresh video reached D3D11 Present for recovery epoch " +
                    epoch + " (gap=" + gapSeconds + "s, pts delta=" +
                    ptsDeltaMilliseconds +
                    "ms, core=" + processId + ", session=" +
                    acceptedSessionGeneration +
                    "); beginning the continuity handoff."
                : "Ignored D3D11 Present proof for stale or unmatched " +
                    "recovery epoch " + epoch + " (gap=" + gapSeconds +
                    "s, core=" + processId + ").");
        }

        private void HandleFeedbackVideoRecoveryWaitTimer()
        {
            bool showReconnectHint = false;
            lock (postSessionMaintenanceSync)
            {
                long dueTicks = Interlocked.Read(
                    ref feedbackVideoRecoveryWaitDueTicks);
                if (dueTicks <= 0 || DateTime.UtcNow.Ticks < dueTicks)
                    return;

                Interlocked.Exchange(
                    ref feedbackVideoRecoveryWaitDueTicks, 0);
                int processId = Interlocked.CompareExchange(
                    ref feedbackVideoRecoveryPid, 0, 0);
                int sessionGeneration = Interlocked.CompareExchange(
                    ref feedbackVideoRecoverySessionGeneration, 0, 0);
                showReconnectHint =
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryPending, 0, 0) == 1 &&
                    processId > 0 &&
                    Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) == processId &&
                    IsMirrorSessionActive &&
                    Interlocked.CompareExchange(
                        ref mirrorSessionGeneration, 0, 0) ==
                        sessionGeneration &&
                    Interlocked.CompareExchange(
                        ref feedbackGapPlaceholderActive, 0, 0) == 1;
                if (showReconnectHint)
                {
                    Interlocked.Increment(
                        ref feedbackVideoRecoveryHintCount);
                    QueueLostConnectionReconnectHint();
                }
                else
                {
                    ResetFeedbackVideoRecoveryWaitLocked();
                }
            }
            if (showReconnectHint)
            {
                Log("AirPlay control traffic recovered, but no matching " +
                    "D3D11 Present proof arrived within " +
                    FeedbackVideoRecoveryWaitSeconds +
                    " seconds; continuity remains visible with manual " +
                    "Screen Mirroring reconnect guidance.");
            }
        }

        private void ResetFeedbackVideoRecoveryWaitLocked()
        {
            Interlocked.Exchange(ref feedbackVideoRecoveryPending, 0);
            Interlocked.Exchange(ref feedbackVideoRecoveryPid, 0);
            Interlocked.Exchange(ref feedbackVideoRecoveryEpoch, 0);
            Interlocked.Exchange(ref feedbackVideoRecoveryGapSeconds, 0);
            Interlocked.Exchange(
                ref feedbackVideoRecoverySessionGeneration, 0);
            Interlocked.Exchange(
                ref feedbackVideoMirrorStartArmExpected, 0);
            Interlocked.Exchange(ref feedbackVideoRecoveryWaitDueTicks, 0);
        }

        private bool TryQueueDueFeedbackGapPlaceholderLocked(long nowTicks)
        {
            long dueTicks = Interlocked.Read(
                ref feedbackGapPlaceholderDueTicks);
            if (!ShouldShowFeedbackGapPlaceholder(
                    dueTicks,
                    nowTicks,
                    IsMirrorSessionActive,
                    Interlocked.CompareExchange(
                        ref feedbackHealthMarkersReady, 0, 0) == 1,
                    Interlocked.CompareExchange(
                        ref feedbackGapEpisodeActive, 0, 0) == 1,
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 1))
                return false;

            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            if (Interlocked.Exchange(
                    ref feedbackGapPlaceholderActive, 1) == 1)
                return false;
            QueueLostConnectionPlaceholder();
            return true;
        }

        private static long CalculateFeedbackGapPlaceholderDueTicks(
            int warningSeconds, long nowTicks)
        {
            if (warningSeconds <= 0 || nowTicks <= 0)
                return 0;
            int remainingSeconds = Math.Max(
                0, FeedbackGapPlaceholderSeconds - warningSeconds);
            return nowTicks + TimeSpan.FromSeconds(remainingSeconds).Ticks;
        }

        private static bool ShouldShowFeedbackGapPlaceholder(
            long dueTicks, long nowTicks, bool mirrorActive,
            bool feedbackMarkersReady, bool gapEpisodeActive,
            bool fatalRecoveryPending)
        {
            return dueTicks > 0 && nowTicks >= dueTicks && mirrorActive &&
                feedbackMarkersReady && gapEpisodeActive &&
                !fatalRecoveryPending;
        }

        private static int ParseClientFeedbackWarningSeconds(string line)
        {
            Match match = Regex.Match(
                line ?? "",
                @"^\*\*\* ERROR:\s+(\d+) seconds since last client feedback request\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return ParseBoundedFeedbackGap(match);
        }

        private static int ParseClientFeedbackRecoverySeconds(string line)
        {
            Match match = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=(\d+)" +
                    @"(?: epoch=\d+)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return ParseBoundedFeedbackGap(match);
        }

        private static int ParseClientFeedbackRecoveryEpoch(string line)
        {
            Match match = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_CLIENT_FEEDBACK_RECOVERED gap_seconds=\d+ " +
                    @"epoch=(\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            int epoch;
            return match.Success &&
                int.TryParse(match.Groups[1].Value, out epoch) && epoch > 0
                    ? epoch : 0;
        }

        private static bool TryParseVideoPresentReady(
            string line, out int epoch, out int gapSeconds,
            out int ptsDeltaMilliseconds)
        {
            epoch = 0;
            gapSeconds = 0;
            ptsDeltaMilliseconds = 0;
            Match match = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_VIDEO_PRESENT_READY epoch=(\d+) " +
                    @"gap_seconds=(\d+) proof=d3d11-present " +
                    @"pts_delta_ms=(-?\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success &&
                int.TryParse(match.Groups[1].Value, out epoch) &&
                int.TryParse(match.Groups[2].Value, out gapSeconds) &&
                int.TryParse(
                    match.Groups[3].Value, out ptsDeltaMilliseconds) &&
                epoch > 0 && gapSeconds >= 0 && gapSeconds <= 3600;
        }

        private static bool TryParseVideoPresentArmed(
            string line, out int epoch)
        {
            epoch = 0;
            Match match = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_VIDEO_PRESENT_ARMED " +
                    @"reason=mirror-start epoch=(\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success &&
                int.TryParse(match.Groups[1].Value, out epoch) && epoch > 0;
        }

        private static int ParseBoundedFeedbackGap(Match match)
        {
            int seconds;
            return match.Success &&
                int.TryParse(match.Groups[1].Value, out seconds) &&
                seconds > 0 && seconds <= 3600
                ? seconds
                : 0;
        }

        private static void UpdateMaximum(ref int target, int candidate)
        {
            int observed = Interlocked.CompareExchange(ref target, 0, 0);
            while (candidate > observed)
            {
                int previous = Interlocked.CompareExchange(
                    ref target, candidate, observed);
                if (previous == observed)
                    return;
                observed = previous;
            }
        }

        private void ObserveCoreDiscoveryMarker(int processId, string line)
        {
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId)
                    return;
                bool discoveryReady = false;
                bool discoveryDegraded = false;
                if (line.IndexOf(
                        "AEROMIRROR_DISCOVERY_REFRESH_CAPABILITY version=1",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Interlocked.Exchange(
                        ref coreDiscoveryRefreshCapability, 1);
                }

                Match refreshProgress = Regex.Match(
                    line,
                    @"^AEROMIRROR_DISCOVERY_REFRESH_(DEFERRED|ACCEPTED)\s+" +
                    @"request=(\d+)\s+" +
                    @"(?:reason=\S+|next_generation=\d+)\s+pid=(\d+)\s+" +
                    @"raop_port=(\d+)\s+airplay_port=(\d+)$",
                    RegexOptions.CultureInvariant |
                    RegexOptions.IgnoreCase);
                if (refreshProgress.Success)
                {
                    long request;
                    int markerPid;
                    int raopPort;
                    int airplayPort;
                    if (long.TryParse(
                            refreshProgress.Groups[2].Value, out request) &&
                        int.TryParse(
                            refreshProgress.Groups[3].Value, out markerPid) &&
                        int.TryParse(
                            refreshProgress.Groups[4].Value, out raopPort) &&
                        int.TryParse(
                            refreshProgress.Groups[5].Value, out airplayPort) &&
                        request > 0)
                    {
                        if (coreCommandSync == null)
                            coreCommandSync = new object();
                        bool deferred = string.Equals(
                            refreshProgress.Groups[1].Value, "DEFERRED",
                            StringComparison.OrdinalIgnoreCase);
                        bool correlated = false;
                        bool changed = false;
                        lock (coreCommandSync)
                        {
                            int expectedPid = Interlocked.CompareExchange(
                                ref coreDiscoveryRefreshPendingPid, 0, 0);
                            int expectedPort = Interlocked.CompareExchange(
                                ref coreDiscoveryRefreshPendingPort, 0, 0);
                            correlated = request == Interlocked.Read(
                                    ref coreDiscoveryRefreshPendingRequest) &&
                                processId == expectedPid &&
                                markerPid == expectedPid &&
                                raopPort == expectedPort &&
                                airplayPort == expectedPort &&
                                Interlocked.CompareExchange(
                                    ref activeCorePid, 0, 0) == expectedPid &&
                                Interlocked.CompareExchange(
                                    ref coreHttpPort, 0, 0) == expectedPort;
                            if (correlated)
                            {
                                int phase = Interlocked.CompareExchange(
                                    ref coreDiscoveryRefreshPhase, 0, 0);
                                if (deferred && phase < 2)
                                {
                                    Interlocked.Exchange(
                                        ref coreDiscoveryRefreshPhase, 1);
                                    Interlocked.Exchange(
                                        ref coreDiscoveryRefreshDueTicks, 0);
                                    changed = phase != 1;
                                }
                                else if (!deferred)
                                {
                                    Interlocked.Exchange(
                                        ref coreDiscoveryRefreshPhase, 2);
                                    Interlocked.Exchange(
                                        ref coreDiscoveryRefreshDueTicks,
                                        DateTime.UtcNow.AddSeconds(12).Ticks);
                                    changed = phase != 2;
                                }
                            }
                        }
                        if (correlated && changed)
                        {
                            Log(deferred
                                ? "Native discovery refresh request " +
                                    request + " was deferred by the core; " +
                                    "the legacy fallback is suspended until " +
                                    "the unchanged process accepts it."
                                : "Native discovery refresh request " +
                                    request + " was accepted by the unchanged " +
                                    "core; a fresh bounded result deadline is " +
                                    "active.");
                        }
                    }
                }

                Match refreshResult = Regex.Match(
                    line,
                    @"^AEROMIRROR_DISCOVERY_REFRESH_(READY|FAILED)\s+" +
                    @"request=(\d+)\s+generation=(\d+)" +
                    @"(?:\s+error=-?\d+)?\s+pid=(\d+)\s+" +
                    @"raop_port=(\d+)\s+airplay_port=(\d+)$",
                    RegexOptions.CultureInvariant |
                    RegexOptions.IgnoreCase);
                if (refreshResult.Success)
                {
                    long request;
                    int markerPid;
                    int raopPort;
                    int airplayPort;
                    if (long.TryParse(
                            refreshResult.Groups[2].Value, out request) &&
                        int.TryParse(
                            refreshResult.Groups[4].Value, out markerPid) &&
                        int.TryParse(
                            refreshResult.Groups[5].Value, out raopPort) &&
                        int.TryParse(
                            refreshResult.Groups[6].Value, out airplayPort) &&
                        request > 0)
                    {
                        if (coreCommandSync == null)
                            coreCommandSync = new object();
                        int expectedPid = 0;
                        int expectedPort = 0;
                        bool fallback = false;
                        bool claimed = false;
                        lock (coreCommandSync)
                        {
                            expectedPid = Interlocked.CompareExchange(
                                ref coreDiscoveryRefreshPendingPid, 0, 0);
                            expectedPort = Interlocked.CompareExchange(
                                ref coreDiscoveryRefreshPendingPort, 0, 0);
                            if (request == Interlocked.Read(
                                    ref coreDiscoveryRefreshPendingRequest) &&
                                processId == expectedPid &&
                                markerPid == expectedPid &&
                                raopPort == expectedPort &&
                                airplayPort == expectedPort &&
                                Interlocked.CompareExchange(
                                    ref activeCorePid, 0, 0) == expectedPid &&
                                Interlocked.CompareExchange(
                                    ref coreHttpPort, 0, 0) == expectedPort)
                            {
                                fallback = Interlocked.CompareExchange(
                                    ref coreDiscoveryRefreshFallbackPending,
                                    0, 0) == 1;
                                ClearNativeDiscoveryRefreshRequestLocked();
                                claimed = true;
                            }
                        }
                        if (!claimed)
                            return;
                        bool ready = string.Equals(
                            refreshResult.Groups[1].Value, "READY",
                            StringComparison.OrdinalIgnoreCase);
                        if (ready)
                        {
                            Interlocked.Exchange(ref coreDnsSdStatus, 1);
                            CancelCoreDiscoveryRecovery(true);
                            Log("Native discovery refresh completed in PID " +
                                expectedPid + " on unchanged AirPlay port " +
                                expectedPort + ".");
                            ArmIdleDiscoveryRenewalIfAvailable();
                        }
                        else if (fallback && IsCoreRunning)
                        {
                            Log("Native discovery refresh failed or changed " +
                                "its PID/port contract; using the bounded " +
                                "legacy process-restart fallback.");
                            ScheduleRestart(
                                "native discovery refresh fallback",
                                false, 500);
                        }
                        else if (!ready && IsCoreRunning)
                        {
                            Log("Periodic native discovery refresh failed; " +
                                "the receiver remains running and will retry " +
                                "on the recurring idle schedule.");
                            ArmIdleDiscoveryRenewalIfAvailable();
                        }
                    }
                }

                if (line.IndexOf(
                        "AEROMIRROR_DNSSD_READY",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Interlocked.Exchange(ref coreDnsSdStatus, 1);
                    discoveryReady = true;
                }
                else if (line.IndexOf(
                        "AEROMIRROR_DNSSD_DEGRADED",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Interlocked.Exchange(ref coreDnsSdStatus, -1);
                    discoveryDegraded = true;
                }

                bool bleMarkerLine = line.IndexOf(
                        "AEROMIRROR_BLE",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf(
                        "[beacon]",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                if (bleMarkerLine)
                {
                    if (line.IndexOf(
                            "Advertising started",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Interlocked.Exchange(ref coreBleStatus, 1);
                        discoveryReady = true;
                    }
                    else if (line.IndexOf(
                                "Advertising failed",
                                StringComparison.OrdinalIgnoreCase) >= 0 ||
                             line.IndexOf(
                                "Failed to start",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Interlocked.Exchange(ref coreBleStatus, -1);
                        discoveryDegraded = true;
                    }
                }

                if (discoveryReady)
                {
                    CancelCoreDiscoveryRecovery(true);
                    return;
                }

                if (discoveryDegraded)
                    ArmCoreDiscoveryRecovery(processId);
            }
        }

        private void ArmCoreDiscoveryRecovery(int processId)
        {
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId ||
                    Interlocked.CompareExchange(
                        ref coreDnsSdStatus, 0, 0) != -1 ||
                    Interlocked.CompareExchange(
                        ref coreBleStatus, 0, 0) != -1 ||
                    Interlocked.CompareExchange(
                        ref coreDiscoveryRecoveryPending, 2, 0) != 0)
                    return;

                Interlocked.Exchange(
                    ref coreDiscoveryRecoveryPid, processId);
                Interlocked.Exchange(
                    ref coreDiscoveryRecoveryDueTicks,
                    DateTime.UtcNow.AddSeconds(5).Ticks);
                Interlocked.Exchange(ref coreDiscoveryRecoveryPending, 1);
                Log("Both native discovery registrations reported a failure; " +
                    "waiting five seconds before bounded host recovery.");
            }
        }

        private void CancelCoreDiscoveryRecovery(bool resetAttempts)
        {
            lock (postSessionMaintenanceSync)
            {
                Interlocked.Exchange(ref coreDiscoveryRecoveryPending, 0);
                Interlocked.Exchange(ref coreDiscoveryRecoveryPid, 0);
                Interlocked.Exchange(ref coreDiscoveryRecoveryDueTicks, 0);
                if (resetAttempts)
                    Interlocked.Exchange(ref coreDiscoveryRecoveryAttempts, 0);
            }
        }

        private bool ConsumeSharedAutomaticRecoveryBudget(
            bool readinessRecovery)
        {
            lock (postSessionMaintenanceSync)
            {
                bool available = coreReadinessRecoveryAttempts < 1 &&
                    Interlocked.CompareExchange(
                        ref coreDiscoveryRecoveryAttempts, 0, 0) < 1;
                coreReadinessRecoveryAttempts = 1;
                Interlocked.Exchange(ref coreDiscoveryRecoveryAttempts, 1);
                coreReadyPending = false;
                coreReadyChecks = 0;
                coreReadyDueUtc = DateTime.MinValue;
                coreReadinessPid = 0;
                if (readinessRecovery)
                {
                    CancelCoreDiscoveryRecovery(false);
                }
                return available;
            }
        }

        private void ResetSharedAutomaticRecoveryBudget()
        {
            lock (postSessionMaintenanceSync)
            {
                coreReadinessRecoveryAttempts = 0;
                Interlocked.Exchange(ref coreDiscoveryRecoveryAttempts, 0);
            }
        }

        private void ArmLostConnectionRecovery(int processId, string marker)
        {
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) != processId ||
                    !IsMirrorSessionActive ||
                    (Interlocked.Read(ref clientActivityGraceDueTicks) >
                        DateTime.UtcNow.Ticks) ||
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 2, 0) != 0)
                    return;
                Interlocked.Exchange(ref lostConnectionRecoveryPid, processId);
                Interlocked.Exchange(
                    ref lostConnectionRecoveryDueTicks,
                    DateTime.UtcNow.AddSeconds(3).Ticks);
                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                Interlocked.Exchange(
                    ref lostConnectionHttpResetStatus, 0);
                Interlocked.Exchange(
                    ref lostConnectionHttpResetPort, 0);
                ResetFeedbackVideoRecoveryWaitLocked();
                Interlocked.Exchange(ref lostConnectionRecoveryPending, 1);
                QueueLostConnectionPlaceholder();
                Log("UxPlay reported a " + marker + "; waiting three seconds " +
                    "for its internal reset before host recovery.");
            }
        }

        private void ResetCoreSessionTracking(bool clearDeferredRestart)
        {
            ResetRendererMoveSizeTracking();
            lock (postSessionMaintenanceSync)
            {
                Interlocked.Exchange(ref mirrorSessionActive, 0);
                Interlocked.Exchange(ref mirrorSessionEndedPending, 0);
                Interlocked.Exchange(ref mirrorSessionEndedDueTicks, 0);
                Interlocked.Exchange(ref idleDiscoveryRenewalDueTicks, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryPending, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
                Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
                ResetCoreHttpLifecycleTracking();
                Interlocked.Exchange(ref feedbackGapEpisodeActive, 0);
                Interlocked.Exchange(ref feedbackGapPlaceholderActive, 0);
                Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
                Interlocked.Exchange(ref feedbackHealthMarkersReady, 0);
                Interlocked.Exchange(ref feedbackVideoPresentProofReady, 0);
                Interlocked.Exchange(ref feedbackVideoPresentProofPid, 0);
                ResetFeedbackVideoRecoveryWaitLocked();
                Interlocked.Exchange(ref coreClientActivityReadyPending, 0);
                Interlocked.Exchange(ref clientActivityGraceDueTicks, 0);
                Interlocked.Exchange(ref physicalNetworkRestartDeferred, 0);
                coreReadinessPid = 0;
                if (clearDeferredRestart)
                    Interlocked.Exchange(ref settingsRestartDeferred, 0);
            }
            Interlocked.Exchange(ref coreDnsSdStatus, 0);
            Interlocked.Exchange(ref coreBleStatus, 0);
            ResetNativeDiscoveryRefreshForProcessLifecycle();
            CancelCoreDiscoveryRecovery(false);
            lock (videoSizeSync)
            {
                videoGeometryEventSequence = 0;
                ClearPendingVideoSizeLocked();
                currentVideoSize = Size.Empty;
                currentVideoSizeSequence = 0;
                currentVideoSizeIsAmbiguousMediaCanvas = false;
                rawGeometryVideoSize = Size.Empty;
                rawGeometryVideoSizeGeneration = 0;
                rawGeometryIsAmbiguousMediaCanvas = false;
                earlyDeviceFrameVideoSize = Size.Empty;
                deviceFrameVideoSize = Size.Empty;
                lastSuppressedVideoSize = Size.Empty;
            }
            Interlocked.Exchange(ref mirrorSessionGeneration, 0);
            videoSizeWindow = IntPtr.Zero;
            initialFitPendingWindow = IntPtr.Zero;
            exactVideoSizeFitSequence = -1;
            appliedVideoFitSize = Size.Empty;
            appliedVideoFitTargetKind = RendererFitTargetKind.None;
            appliedVideoOrientation = 0;
            Interlocked.Exchange(
                ref appliedPresentationScalePermille,
                RendererPresentationPolicy.NormalScalePermille);
            rendererFullscreenActive = false;
        }

        private void ResetIdleDiscoveryRenewalSchedule()
        {
            lock (postSessionMaintenanceSync)
            {
                Interlocked.Exchange(ref idleDiscoveryRenewalUsed, 0);
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks,
                    IsCoreRunning
                        ? DateTime.UtcNow.AddMinutes(
                            GetIdleDiscoveryRenewalDelayMinutes(0)).Ticks
                        : 0);
            }
        }

        private static int GetIdleDiscoveryRenewalDelayMinutes(
            int completedRenewals)
        {
            if (completedRenewals == 0)
                return IdleDiscoveryFirstRenewalMinutes;
            if (completedRenewals > 0)
                return IdleDiscoveryRecurringRenewalMinutes;
            return 0;
        }

        private static int IncrementIdleDiscoveryRenewalCount(
            int completedRenewals)
        {
            return completedRenewals < int.MaxValue
                ? completedRenewals + 1
                : int.MaxValue;
        }

        private void ArmIdleDiscoveryRenewalIfAvailable()
        {
            lock (postSessionMaintenanceSync)
            {
                int completedRenewals = Interlocked.CompareExchange(
                    ref idleDiscoveryRenewalUsed, 0, 0);
                int delayMinutes = GetIdleDiscoveryRenewalDelayMinutes(
                    completedRenewals);
                if (delayMinutes <= 0)
                {
                    Interlocked.Exchange(ref idleDiscoveryRenewalDueTicks, 0);
                    return;
                }
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks,
                    DateTime.UtcNow.AddMinutes(delayMinutes).Ticks);
            }
        }

        private static bool ShouldDeferDisruptiveMaintenance(
            bool mirrorActive, long clientGraceDueTicks, long nowTicks)
        {
            return mirrorActive ||
                (clientGraceDueTicks > 0 && nowTicks < clientGraceDueTicks);
        }

        private static AutomaticDiscoveryRenewalAction
            EvaluateAutomaticDiscoveryRenewal(
            int completedRenewals,
            long dueTicks,
            bool mirrorActive,
            long clientGraceDueTicks,
            long nowTicks,
            DateTime lastRefreshUtc,
            DateTime nowUtc,
            out long nextDueTicks,
            out int nextCompletedRenewals)
        {
            nextDueTicks = dueTicks;
            nextCompletedRenewals = completedRenewals;
            if (dueTicks <= 0 || nowTicks < dueTicks)
                return AutomaticDiscoveryRenewalAction.None;

            if (completedRenewals < 0)
            {
                nextDueTicks = 0;
                return AutomaticDiscoveryRenewalAction.None;
            }

            if (ShouldDeferDisruptiveMaintenance(
                    mirrorActive, clientGraceDueTicks, nowTicks))
                return AutomaticDiscoveryRenewalAction.None;

            if ((nowUtc - lastRefreshUtc).TotalMinutes < 2)
            {
                int delayMinutes = GetIdleDiscoveryRenewalDelayMinutes(
                    completedRenewals);
                nextDueTicks = delayMinutes > 0
                    ? nowUtc.AddMinutes(delayMinutes).Ticks
                    : 0;
                return AutomaticDiscoveryRenewalAction.None;
            }

            nextCompletedRenewals = IncrementIdleDiscoveryRenewalCount(
                completedRenewals);
            nextDueTicks = 0;
            return AutomaticDiscoveryRenewalAction.Refresh;
        }

        private void HandlePhysicalNetworkChangeMaintenance()
        {
            lock (postSessionMaintenanceSync)
            {
                if (!IsCoreRunning)
                    return;
                long nowTicks = DateTime.UtcNow.Ticks;
                if (ShouldDeferDisruptiveMaintenance(
                        IsMirrorSessionActive,
                        Interlocked.Read(ref clientActivityGraceDueTicks),
                        nowTicks))
                {
                    Interlocked.Exchange(
                        ref physicalNetworkRestartDeferred, 1);
                    Log("Physical network changed during AirPlay client " +
                        "activity; receiver restart was deferred until the " +
                        "session or connection grace ends.");
                    return;
                }

                Interlocked.Exchange(ref physicalNetworkRestartDeferred, 0);
                ScheduleRestart("physical network changed", false, 1200);
            }
        }

        private void HandleAutomaticDiscoveryMaintenance()
        {
            if (!IsCoreRunning || coreReadyPending || restartPending ||
                Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
                return;

            lock (postSessionMaintenanceSync)
            {
                if (!IsCoreRunning || coreReadyPending || restartPending ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1)
                    return;

                DateTime now = DateTime.UtcNow;
                if (Interlocked.CompareExchange(
                        ref physicalNetworkRestartDeferred, 0, 0) == 1)
                {
                    if (ShouldDeferDisruptiveMaintenance(
                            IsMirrorSessionActive,
                            Interlocked.Read(ref clientActivityGraceDueTicks),
                            now.Ticks))
                        return;
                    Interlocked.Exchange(
                        ref physicalNetworkRestartDeferred, 0);
                    Log("AirPlay client activity ended; applying the deferred " +
                        "physical-network receiver restart.");
                    ScheduleRestart(
                        "deferred physical network change", false, 1200);
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref mirrorSessionEndedPending, 0, 0) == 1)
                {
                    long dueTicks = Interlocked.Read(
                        ref mirrorSessionEndedDueTicks);
                    if (dueTicks > 0 && now.Ticks >= dueTicks &&
                        !IsMirrorSessionActive)
                    {
                        Interlocked.Exchange(
                            ref mirrorSessionEndedPending, 0);
                        Interlocked.Exchange(
                            ref mirrorSessionEndedDueTicks, 0);
                        if (IsSettingsRestartDeferred)
                        {
                            Log("Current mirroring session ended; applying " +
                                "the saved receiver settings.");
                            lastAutomaticDiscoveryRefreshUtc = now;
                            ScheduleRestart(
                                "deferred settings change", false, 1000);
                            return;
                        }

                        Log("Post-session maintenance elapsed without deferred " +
                            "settings; keeping the healthy receiver running.");
                    }
                }

                long idleDueTicks = Interlocked.Read(
                    ref idleDiscoveryRenewalDueTicks);
                int completedRenewals = Interlocked.CompareExchange(
                    ref idleDiscoveryRenewalUsed, 0, 0);
                long nextDueTicks;
                int nextCompletedRenewals;
                AutomaticDiscoveryRenewalAction action =
                    EvaluateAutomaticDiscoveryRenewal(
                        completedRenewals,
                        idleDueTicks,
                        IsMirrorSessionActive,
                        Interlocked.Read(ref clientActivityGraceDueTicks),
                        now.Ticks,
                        lastAutomaticDiscoveryRefreshUtc,
                        now,
                        out nextDueTicks,
                        out nextCompletedRenewals);
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks, nextDueTicks);
                if (action != AutomaticDiscoveryRenewalAction.Refresh)
                    return;

                Interlocked.Exchange(
                    ref idleDiscoveryRenewalUsed, nextCompletedRenewals);
                Interlocked.Exchange(
                    ref idleDiscoveryRenewalDueTicks, 0);
                bool allowLegacyRestart = nextCompletedRenewals <=
                    IdleDiscoveryLegacyRestartLimit;
                Log("Renewing idle AirPlay discovery (renewal " +
                    nextCompletedRenewals + ") after prolonged " +
                    "inactivity without a mirroring session.");
                lastAutomaticDiscoveryRefreshUtc = now;
                if (!TryRequestNativeDiscoveryRefresh(
                        "idle discovery renewal", allowLegacyRestart))
                {
                    if (allowLegacyRestart)
                    {
                        ScheduleRestart(
                            "idle discovery renewal", false, 1200);
                    }
                    else
                    {
                        Log("The periodic same-process discovery renewal " +
                            "could not start; the receiver remains running " +
                            "and will retry on the recurring idle schedule.");
                        ArmIdleDiscoveryRenewalIfAvailable();
                    }
                }
            }
        }

        private static SessionUnlockDiscoveryAction
            EvaluateSessionUnlockDiscoveryRefresh(
            int completedRenewals,
            bool coreRunning,
            bool readinessCheckIdle,
            bool localDiscoveryReady,
            bool physicalNetworkReady,
            bool restartBusy,
            bool mirrorActive,
            long clientGraceDueTicks,
            long nowTicks,
            DateTime lastRefreshUtc,
            DateTime nowUtc,
            out long nextDueTicks,
            out int nextCompletedRenewals)
        {
            nextDueTicks = 0;
            nextCompletedRenewals = completedRenewals;
            if (completedRenewals < 1 || !coreRunning || mirrorActive ||
                (clientGraceDueTicks > 0 && clientGraceDueTicks > nowTicks) ||
                lastRefreshUtc == DateTime.MinValue)
                return SessionUnlockDiscoveryAction.None;

            if (!readinessCheckIdle || !localDiscoveryReady ||
                !physicalNetworkReady || restartBusy)
            {
                nextDueTicks = nowUtc.AddSeconds(5).Ticks;
                return SessionUnlockDiscoveryAction.RetryLater;
            }

            DateTime cooldownDue = lastRefreshUtc.AddMinutes(
                IdleDiscoveryUnlockRetryCooldownMinutes);
            if (nowUtc < cooldownDue)
            {
                nextDueTicks = cooldownDue.Ticks;
                return SessionUnlockDiscoveryAction.RetryLater;
            }

            nextCompletedRenewals = IncrementIdleDiscoveryRenewalCount(
                completedRenewals);
            return SessionUnlockDiscoveryAction.Refresh;
        }

        private void HandleSessionUnlockDiscoveryRefresh()
        {
            if (Interlocked.CompareExchange(
                    ref sessionUnlockDiscoveryRefreshPending, 0, 0) != 1)
                return;
            long dueTicks = Interlocked.Read(
                ref sessionUnlockDiscoveryRefreshDueTicks);
            DateTime now = DateTime.UtcNow;
            if (dueTicks > 0 && now.Ticks < dueTicks)
                return;

            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref sessionUnlockDiscoveryRefreshPending, 0, 0) != 1)
                    return;

                // SessionSwitch is serialized by the same lock. Re-read the
                // deadline after entering it so a newer unlock cannot lose its
                // two-second network-settle window to an older due timer pass.
                dueTicks = Interlocked.Read(
                    ref sessionUnlockDiscoveryRefreshDueTicks);
                now = DateTime.UtcNow;
                if (dueTicks > 0 && now.Ticks < dueTicks)
                    return;

                bool restartBusy = restartPending ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1 ||
                    Interlocked.CompareExchange(
                        ref networkRefreshRunning, 0, 0) == 1 ||
                    Interlocked.CompareExchange(
                        ref networkRefreshPending, 0, 0) == 1;
                bool socketsReady = Interlocked.CompareExchange(
                        ref coreSocketsReady, 0, 0) == 1 &&
                    now.Ticks >= Interlocked.Read(
                        ref coreSocketsReadyDueTicks);
                int dnsSdStatus = Interlocked.CompareExchange(
                    ref coreDnsSdStatus, 0, 0);
                int bleStatus = Interlocked.CompareExchange(
                    ref coreBleStatus, 0, 0);
                bool localDiscoveryReady = socketsReady &&
                    (dnsSdStatus == 1 || bleStatus == 1);
                bool physicalNetworkReady = networkProfileKnown &&
                    FirstNumericIpv4(physicalNetworkAddresses).Length > 0;
                long nextDueTicks;
                int nextCompletedRenewals;
                SessionUnlockDiscoveryAction action =
                    EvaluateSessionUnlockDiscoveryRefresh(
                        Interlocked.CompareExchange(
                            ref idleDiscoveryRenewalUsed, 0, 0),
                        IsCoreRunning,
                        !coreReadyPending,
                        localDiscoveryReady,
                        physicalNetworkReady,
                        restartBusy,
                        IsMirrorSessionActive,
                        Interlocked.Read(ref clientActivityGraceDueTicks),
                        now.Ticks,
                        lastAutomaticDiscoveryRefreshUtc,
                        now,
                        out nextDueTicks,
                        out nextCompletedRenewals);
                if (action == SessionUnlockDiscoveryAction.RetryLater)
                {
                    Interlocked.Exchange(
                        ref sessionUnlockDiscoveryRefreshDueTicks,
                        nextDueTicks);
                    return;
                }

                Interlocked.Exchange(
                    ref sessionUnlockDiscoveryRefreshPending, 0);
                Interlocked.Exchange(
                    ref sessionUnlockDiscoveryRefreshDueTicks, 0);
                if (action != SessionUnlockDiscoveryAction.Refresh)
                {
                    return;
                }

                Interlocked.Exchange(
                    ref idleDiscoveryRenewalUsed,
                    nextCompletedRenewals);
                Interlocked.Exchange(ref idleDiscoveryRenewalDueTicks, 0);
                lastAutomaticDiscoveryRefreshUtc = now;
                bool allowLegacyRestart = nextCompletedRenewals <=
                    IdleDiscoveryLegacyRestartLimit;
                Log("Windows session unlocked after prolonged AirPlay idle; " +
                    "local sockets, at least one local discovery marker, and " +
                    "the cached physical IPv4 are ready, so a guarded " +
                    "discovery re-registration will run.");
                if (!TryRequestNativeDiscoveryRefresh(
                        "session-unlock discovery refresh",
                        allowLegacyRestart))
                {
                    if (allowLegacyRestart)
                    {
                        ScheduleRestart(
                            "session-unlock discovery refresh", false, 1200);
                    }
                    else
                    {
                        Log("The guarded session-unlock discovery refresh " +
                            "could not start; the receiver remains running " +
                            "and will retry on the recurring idle schedule.");
                        ArmIdleDiscoveryRenewalIfAvailable();
                    }
                }
            }
        }

        private void HandleLostConnectionRecovery()
        {
            if (Interlocked.CompareExchange(
                    ref lostConnectionRecoveryPending, 0, 0) != 1)
                return;
            lock (postSessionMaintenanceSync)
            {
                bool restartBusy = restartPending ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1;
                LostConnectionRecoveryAction action =
                    ConsumeDueLostConnectionRecoveryLocked(
                        DateTime.UtcNow, IsCoreRunning, restartBusy);
                if (action == LostConnectionRecoveryAction.RestartStalledSession)
                {
                    Log("UxPlay did not finish its internal lost-client reset " +
                        "within three seconds; restarting the receiver process.");
                    ScheduleRestart(
                        "stalled mirror after lost client", false, 500);
                }
                else if (action ==
                    LostConnectionRecoveryAction.PreserveNativeRecovery)
                {
                    Log("UxPlay completed its lost-client cleanup and " +
                        "explicitly confirmed its listening socket on " +
                        "AirPlay port " +
                        Interlocked.CompareExchange(
                            ref coreHttpPort, 0, 0) +
                        "; preserving the same receiver process for a " +
                        "faster reconnect.");
                }
                else if (action ==
                    LostConnectionRecoveryAction.PreserveLegacyRecovery)
                {
                    Log("A legacy UxPlay core emitted a generic listener-ready " +
                        "line after lost-client cleanup. Preserving it for " +
                        "bounded compatibility; the AirPlay port identity " +
                        "was not explicitly confirmed.");
                }
            }
        }

        private LostConnectionRecoveryAction
            ConsumeDueLostConnectionRecoveryLocked(
                DateTime now, bool coreRunning, bool restartBusy)
        {
            if (Interlocked.CompareExchange(
                    ref lostConnectionRecoveryPending, 0, 0) != 1)
                return LostConnectionRecoveryAction.None;

            long dueTicks = Interlocked.Read(
                ref lostConnectionRecoveryDueTicks);
            if (dueTicks <= 0 || now.Ticks < dueTicks)
                return LostConnectionRecoveryAction.None;

            int recoveryPid = Interlocked.CompareExchange(
                ref lostConnectionRecoveryPid, 0, 0);
            bool sameRunningCore = coreRunning && recoveryPid > 0 &&
                Interlocked.CompareExchange(ref activeCorePid, 0, 0) ==
                    recoveryPid;
            bool mirrorActive = IsMirrorSessionActive;
            bool socketsReady = Interlocked.CompareExchange(
                ref coreSocketsReady, 0, 0) == 1;
            int markerSupport = Interlocked.CompareExchange(
                ref coreHttpMarkersReady, 0, 0);
            int advertisedPort = Interlocked.CompareExchange(
                ref coreHttpPort, 0, 0);
            int resetStatus = Interlocked.CompareExchange(
                ref lostConnectionHttpResetStatus, 0, 0);
            int resetPort = Interlocked.CompareExchange(
                ref lostConnectionHttpResetPort, 0, 0);
            bool explicitResetReady = markerSupport == 1 &&
                resetStatus == 1 && advertisedPort > 0 &&
                resetPort == advertisedPort && socketsReady;
            bool legacyResetReady = markerSupport == 0 &&
                resetStatus == 2 && socketsReady;

            Interlocked.Exchange(ref lostConnectionRecoveryPending, 0);
            Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
            Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionHttpResetStatus, 0);
            Interlocked.Exchange(ref lostConnectionHttpResetPort, 0);

            if (!sameRunningCore || restartBusy)
                return LostConnectionRecoveryAction.None;
            if (mirrorActive)
                return LostConnectionRecoveryAction.RestartStalledSession;
            if (explicitResetReady)
                return LostConnectionRecoveryAction.PreserveNativeRecovery;
            if (legacyResetReady)
                return LostConnectionRecoveryAction.PreserveLegacyRecovery;
            return LostConnectionRecoveryAction.RestartStalledSession;
        }

        private void HandleCoreDiscoveryRecovery()
        {
            if (Interlocked.CompareExchange(
                    ref coreDiscoveryRecoveryPending, 0, 0) != 1)
                return;
            lock (postSessionMaintenanceSync)
            {
                if (Interlocked.CompareExchange(
                        ref coreDiscoveryRecoveryPending, 0, 0) != 1)
                    return;
                long dueTicks = Interlocked.Read(
                    ref coreDiscoveryRecoveryDueTicks);
                if (dueTicks <= 0 || DateTime.UtcNow.Ticks < dueTicks)
                    return;

                if (coreReadyPending || IsMirrorSessionActive)
                {
                    Interlocked.Exchange(
                        ref coreDiscoveryRecoveryDueTicks,
                        DateTime.UtcNow.AddSeconds(10).Ticks);
                    return;
                }
                if (restartPending || Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1)
                    return;
                if (Interlocked.CompareExchange(
                        ref coreDiscoveryRecoveryPending, 2, 1) != 1)
                    return;

                int recoveryPid = Interlocked.CompareExchange(
                    ref coreDiscoveryRecoveryPid, 0, 0);
                bool stillFailed =
                    Interlocked.CompareExchange(
                        ref coreDnsSdStatus, 0, 0) == -1 &&
                    Interlocked.CompareExchange(
                        ref coreBleStatus, 0, 0) == -1;
                bool sameRunningCore = IsCoreRunning && recoveryPid > 0 &&
                    Interlocked.CompareExchange(ref activeCorePid, 0, 0) ==
                        recoveryPid;
                bool socketsReady = Interlocked.CompareExchange(
                    ref coreSocketsReady, 0, 0) == 1;
                if (!sameRunningCore || !stillFailed || !socketsReady)
                {
                    CancelCoreDiscoveryRecovery(false);
                    return;
                }

                bool recoveryAvailable =
                    ConsumeSharedAutomaticRecoveryBudget(false);
                CancelCoreDiscoveryRecovery(false);
                if (recoveryAvailable)
                {
                    Log("DNS-SD and BLE discovery both remained degraded; " +
                        "performing the single shared automatic recovery.");
                    if (!TryRequestNativeDiscoveryRefresh(
                            "native discovery registration recovery", true))
                    {
                        ScheduleRestart(
                            "native discovery registration recovery",
                            false, 1200);
                    }
                    return;
                }

                Log("DNS-SD and BLE discovery remain degraded after the shared " +
                    "automatic recovery budget was consumed. The socket-ready " +
                    "receiver stays running; no automatic restart loop will " +
                    "be started.");
            }
        }

        private void ResetRapidExitWindow()
        {
            rapidExitCount = 0;
            rapidExitWindowStartedAt = DateTime.MinValue;
        }

        public void QuitApplication()
        {
            Quit();
        }

        public string BuildUxPlayArguments()
        {
            var parts = new List<string>();
            string name = AppSettings.NormalizeReceiverNameForDiscovery(
                settings.ReceiverName);
            parts.Add("-n");
            parts.Add(QuoteArgument(name));
            parts.Add("-nh");
            string receiverDeviceId = AppSettings.GetSavedReceiverDeviceId();
            if (receiverDeviceId.Length > 0)
            {
                parts.Add("-m");
                parts.Add(receiverDeviceId);
            }
            parts.Add("-key");
            parts.Add(QuoteArgument(AppSettings.ReceiverKeyPath));

            if (settings.PairingMode == "pin")
            {
                if (settings.FixedPin.Length == 4)
                    parts.Add("-pin " + settings.FixedPin);
                else
                    parts.Add("-pin");
                parts.Add("-reg");
                parts.Add(QuoteArgument(AppSettings.TrustedClientsPath));
            }
            else if (settings.PairingMode == "password")
            {
                parts.Add("-pw");
            }

            if (settings.QualityPreset == "720p30")
            {
                parts.Add("-s 1280x720@60");
                parts.Add("-fps 30");
            }
            else if (settings.QualityPreset == "1080p30")
            {
                parts.Add("-s 1920x1080@60");
                parts.Add("-fps 30");
            }
            else if (settings.QualityPreset == "4k60")
            {
                parts.Add("-h265");
                parts.Add("-s 3840x2160@60");
                parts.Add("-fps 60");
            }
            else
            {
                parts.Add("-s 1920x1080@60");
                parts.Add("-fps 60");
            }

            if (settings.Renderer == "d3d11")
            {
                parts.Add("-vd d3d11h264dec");
                parts.Add("-vs d3d11videosink");
            }
            else if (settings.Renderer == "d3d12")
            {
                parts.Add("-vd d3d12h264dec");
                parts.Add("-vs d3d12videosink");
            }

            if (settings.LatencyProfile == "low")
            {
                parts.Add("-vsync no");
            }
            else if (settings.LatencyProfile == "stable")
            {
                parts.Add("-al 0.35");
            }

            if (settings.AudioOutput == "mute")
            {
                parts.Add("-a");
            }
            else
            {
                parts.Add("-as " + QuoteArgument(
                    "wasapi2sink continue-on-error=true"));
            }

            parts.Add("-reset 15");
            if (!string.IsNullOrWhiteSpace(settings.AdvancedArguments))
                parts.Add(settings.AdvancedArguments.Trim());
            return string.Join(" ", parts.ToArray());
        }

        public string BuildSafeUxPlayArguments()
        {
            return MaskSecrets(BuildUxPlayArguments());
        }

        private string MaskSecrets(string text)
        {
            return RedactSensitiveText(text, settings.FixedPin);
        }

        private static int CountAddresses(string addresses)
        {
            if (string.IsNullOrWhiteSpace(addresses))
                return 0;
            return addresses.Split(new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static string FirstNumericIpv4(string addresses)
        {
            if (string.IsNullOrWhiteSpace(addresses))
                return "";
            foreach (string candidate in addresses.Split(new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                IPAddress parsed;
                string value = candidate.Trim();
                if (IPAddress.TryParse(value, out parsed) &&
                    parsed.GetAddressBytes().Length == 4 &&
                    !IPAddress.IsLoopback(parsed))
                    return value;
            }
            return "";
        }

        private static string RedactSensitiveText(
            string text, string knownPin)
        {
            string safe = text ?? "";
            if (!string.IsNullOrWhiteSpace(knownPin))
                safe = safe.Replace(knownPin, "****");
            safe = Regex.Replace(
                safe,
                @"(?i)(-{1,2}pin(?:[ \t]+|[=:][ \t]*))[""']?\d{4,}[""']?",
                "$1****");
            safe = Regex.Replace(
                safe,
                @"(?i)(-{1,2}(?:pw|password|passcode|token|secret)(?:[ \t]+|[=:][ \t]*))(?:""[^""]*""|'[^']*'|(?!-)[^\s,;\]]+)",
                "$1****");
            safe = Regex.Replace(
                safe,
                @"(?i)\b(pin|password|passcode|token|secret)[ \t]*[:=][ \t]*(?:""[^""]*""|'[^']*'|[^\s,;\]]+)",
                "$1: ****");
            safe = Regex.Replace(
                safe,
                @"(?i)\b(password|passcode|token|secret)[ \t]+(?:""[^""]*""|'[^']*'|(?!-)[^\s,;\]]+)",
                "$1 ****");
            safe = Regex.Replace(
                safe,
                @"(?i)\b(?:[0-9a-f]{2}:){5}[0-9a-f]{2}\b",
                "**:**:**:**:**:**");
            safe = Regex.Replace(
                safe,
                @"(?is)-----BEGIN [^-]*(?:PRIVATE KEY|SECRET)[^-]*-----.*?-----END [^-]*(?:PRIVATE KEY|SECRET)[^-]*-----",
                "[redacted cryptographic material]");
            safe = Regex.Replace(
                safe,
                @"(?i)\b((?:(?:aes|ecdh|session|shared|private|stream|fairplay)[\w -]{0,24})?(?:key|secret|iv))\s*[:=]\s*(?:[0-9a-f]{2}[\s:,-]?){8,}",
                "$1: [redacted cryptographic material]");
            safe = Regex.Replace(
                safe,
                @"(?im)(Physical network profile:\s+\w+\s+\()(?!physical interface )([^,\r\n]+)(,\s*)",
                "$1[redacted network]$3");
            string localData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string roamingData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string profile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(localData))
                safe = safe.Replace(localData, "%LOCALAPPDATA%");
            if (!string.IsNullOrWhiteSpace(roamingData))
                safe = safe.Replace(roamingData, "%APPDATA%");
            if (!string.IsNullOrWhiteSpace(profile))
                safe = safe.Replace(profile, "%USERPROFILE%");
            return safe;
        }

        private static string RedactSupportText(
            string text, string knownPin)
        {
            string safe = RedactSensitiveText(text, knownPin);
            safe = Regex.Replace(
                safe,
                @"\b(?:\d{1,3}\.){3}\d{1,3}\b",
                "[redacted IP]");
            safe = Regex.Replace(
                safe,
                @"(?i)(?<![0-9a-f:])(?:(?:[0-9a-f]{1,4}:){3,}[0-9a-f:]*|[0-9a-f:]*::[0-9a-f:]*)(?![0-9a-f:])",
                "[redacted IP]");
            safe = Regex.Replace(
                safe,
                @"(?im)^(\s*(?:Имя приёмника|Receiver name)\s*:\s*).*$",
                "$1[redacted]");
            safe = Regex.Replace(
                safe,
                @"(?i)(-{1,2}n\s+)(?:""[^""]*""|'[^']*'|[^\s]+)",
                "$1\"[redacted]\"");
            safe = Regex.Replace(
                safe,
                @"(?i)(connection request from\s+).*?(\s+\([^)\r\n]+\))",
                "$1[redacted device]$2");
            safe = Regex.Replace(
                safe,
                @"(?im)^(\s*Физическая сеть:\s+\S+\s+·\s+).*$",
                "$1[redacted network]");
            return safe;
        }
        private static void SanitizeExistingLogs(string knownPin)
        {
            string[] paths =
            {
                AppSettings.LogPath,
                AppSettings.LogPath + ".1"
            };
            foreach (string path in paths)
            {
                try
                {
                    if (!File.Exists(path) ||
                        new FileInfo(path).Length > 50L * 1024L * 1024L)
                        continue;
                    string original = File.ReadAllText(path, Encoding.UTF8);
                    string sanitized =
                        RedactSensitiveText(original, knownPin);
                    if (!string.Equals(
                            original, sanitized, StringComparison.Ordinal))
                    {
                        File.WriteAllText(
                            path, sanitized, new UTF8Encoding(false));
                    }
                }
                catch { }
            }
        }

        public string GetDiagnostics()
        {
            var text = new StringBuilder();
            text.AppendLine("AeroMirror — диагностика");
            text.AppendLine("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine();
            text.AppendLine("Оболочка: " + AppVersion.Display);
            text.AppendLine("Windows: " + Environment.OSVersion);
            text.AppendLine("64-bit процесс: " + Environment.Is64BitProcess);
            text.AppendLine("Ядро: " + (File.Exists(CorePath) ? "найдено" : "НЕ НАЙДЕНО"));
            text.AppendLine("Путь ядра: " + MaskSecrets(CorePath));
            text.AppendLine("Процесс ядра: " + (IsCoreRunning ? "работает, PID " + coreProcess.Id : "остановлен"));
            bool coreRunningSnapshot = IsCoreRunning;
            text.AppendLine("Runtime state: coreReady=" +
                (coreRunningSnapshot && !coreReadyPending) +
                "; socketsReady=" +
                (Interlocked.CompareExchange(
                    ref coreSocketsReady, 0, 0) == 1) +
                "; mirrorActive=" + IsMirrorSessionActive +
                "; lostRecovery=" +
                (Interlocked.CompareExchange(
                    ref lostConnectionRecoveryPending, 0, 0) == 1) +
                "; httpMarkers=" +
                (Interlocked.CompareExchange(
                    ref coreHttpMarkersReady, 0, 0) == 1) +
                "; httpPort=" +
                Interlocked.CompareExchange(ref coreHttpPort, 0, 0) +
                "; httpResetStatus=" +
                Interlocked.CompareExchange(
                    ref lostConnectionHttpResetStatus, 0, 0) +
                "; httpResetPort=" +
                Interlocked.CompareExchange(
                    ref lostConnectionHttpResetPort, 0, 0) +
                "; restartPending=" + restartPending +
                "; stopPending=" +
                (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1) +
                "; networkWait=" + IsWaitingForNetwork + ".");
            text.AppendLine("AirPlay feedback gaps: episodes=" +
                Interlocked.CompareExchange(
                    ref feedbackGapEpisodeCount, 0, 0) +
                "; longestSeconds=" +
                Interlocked.CompareExchange(
                    ref feedbackGapLongestSeconds, 0, 0) +
                "; active=" +
                (Interlocked.CompareExchange(
                    ref feedbackGapEpisodeActive, 0, 0) == 1) +
                "; nativeMarkers=" +
                (Interlocked.CompareExchange(
                    ref feedbackHealthMarkersReady, 0, 0) == 1) + ".");
            text.AppendLine("Discovery registration: DNS-SD=" +
                DiscoveryMarkerStatus(
                    Interlocked.CompareExchange(
                        ref coreDnsSdStatus, 0, 0),
                    "degraded") +
                "; BLE=" +
                DiscoveryMarkerStatus(
                    Interlocked.CompareExchange(
                        ref coreBleStatus, 0, 0),
                    "failed") +
                "; recoveryPending=" +
                (Interlocked.CompareExchange(
                    ref coreDiscoveryRecoveryPending, 0, 0) == 1) +
                "; recoveryAttempts=" +
                Interlocked.CompareExchange(
                    ref coreDiscoveryRecoveryAttempts, 0, 0) + ".");
            text.AppendLine("Bonjour Service: " + GetBonjourStatus());
            text.AppendLine("Bonjour · Windows Firewall: " +
                GetBonjourFirewallDiagnosticLine());
            text.AppendLine("Автозапуск: " + (IsAutostartEnabled() ? "включён" : "выключен"));
            text.AppendLine("Запуск в трее: " + (settings.StartMinimized ? "включён" : "выключен"));
            text.AppendLine("Кнопка ×: " + (settings.CloseToTray ? "свернуть в трей" : "закрыть приложение"));
            text.AppendLine("Физическая сеть: " +
                (networkProfileKnown ? (publicNetwork ? "публичная" : "частная") : "не определена") +
                " · " + networkProfileName + " · " + networkInterfaceName);
            text.AppendLine("VPN/виртуальные сетевые профили: " +
                nonPhysicalProfileCount +
                " (публичных: " + publicNonPhysicalProfileCount + ")");
            text.AppendLine("Защита подключения: " + settings.PairingMode);
            text.AppendLine("Имя приёмника: " + settings.ReceiverName);
            text.AppendLine("Запрашиваемое качество: " + settings.QualityPreset);
            text.AppendLine("Профиль задержки: " + settings.LatencyProfile);
            text.AppendLine("Вывод звука: " + settings.AudioOutput);
            text.AppendLine("Аргументы UxPlay: " + BuildSafeUxPlayArguments());
            text.AppendLine("Файл настроек: " +
                MaskSecrets(AppSettings.FilePath));
            text.AppendLine("Журнал: " + MaskSecrets(AppSettings.LogPath));
            text.AppendLine();
            string sourceVersion = AppVersion.Display;
            text.AppendLine("Исходники AeroMirror " + sourceVersion + ":");
            text.AppendLine(
                "https://github.com/Nadejny/aeromirror/tree/v" +
                sourceVersion);
            text.AppendLine("Исходники изменённого GPL-ядра:");
            text.AppendLine(
                "https://github.com/Nadejny/aeromirror/releases/download/v" +
                sourceVersion + "/AeroMirror-native-source-" +
                sourceVersion + ".zip");
            text.AppendLine("Неизменённый runtime загружается с:");
            text.AppendLine(
                "https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736");
            text.AppendLine();
            text.AppendLine("Для обнаружения iPhone и компьютер должны быть в одной локальной сети.");
            text.AppendLine("При первом запуске разрешите сетевой доступ в Windows Firewall.");
            text.AppendLine("Если устройство не видно, перезапустите Bonjour Service и приёмник.");
            return text.ToString();
        }

        private static string DiscoveryMarkerStatus(
            int status, string negativeStatus)
        {
            if (status > 0)
                return "ready";
            if (status < 0)
                return negativeStatus;
            return "unknown";
        }

        private static bool IsCoreReadinessConfirmed(
            bool socketsReady, bool bonjourRunning,
            int dnsSdStatus, int bleStatus)
        {
            bool bothDiscoveryPathsFailed =
                dnsSdStatus < 0 && bleStatus < 0;
            return socketsReady && !bothDiscoveryPathsFailed &&
                (bonjourRunning || dnsSdStatus == 1 || bleStatus == 1);
        }

        private void OnStartStop(object sender, EventArgs e)
        {
            if (IsCoreRunning) StopCore(); else StartCore();
        }

        private void OnAutostart(object sender, EventArgs e)
        {
            settings.AutoStartWindows = !IsAutostartEnabled();
            settings.Save();
            ApplyAutostart(settings.AutoStartWindows);
            autoStartItem.Checked = IsAutostartEnabled();
        }

        private void OnAlwaysOnTop(object sender, EventArgs e)
        {
            settings.AlwaysOnTop = !settings.AlwaysOnTop;
            settings.Save();
            topMostItem.Checked = settings.AlwaysOnTop;
            ApplyTopMost();
        }

        private void MonitorCore()
        {
            if (showEvent.WaitOne(0))
                ShowSettings();

            NetworkProfileInfo refreshedProfile = null;
            lock (networkProfileSync)
            {
                if (pendingNetworkProfile != null)
                {
                    refreshedProfile = pendingNetworkProfile;
                    pendingNetworkProfile = null;
                }
            }
            if (refreshedProfile != null)
            {
                bool networkChanged = ApplyNetworkProfile(
                    refreshedProfile, true);
                if (networkChanged && IsCoreRunning)
                {
                    HandlePhysicalNetworkChangeMaintenance();
                }
            }

            if (Interlocked.CompareExchange(ref networkRefreshPending, 0, 0) == 1 &&
                DateTime.UtcNow.Ticks >= Interlocked.Read(ref networkRefreshDueTicks))
            {
                Interlocked.Exchange(ref networkRefreshPending, 0);
                Log("Network event debounce elapsed; checking physical profile.");
                BeginNetworkProfileRefresh();
            }

            if (Interlocked.Exchange(ref restartStopCompleted, 0) == 1)
            {
                Interlocked.Exchange(ref restartStopInProgress, 0);
                if (restartAfterStop && !quitting)
                {
                    restartAfterStop = false;
                    restartDueUtc = DateTime.UtcNow.AddMilliseconds(
                        restartDelayAfterStop);
                    restartPending = true;
                    Log("Core stop settled; restart will run in " +
                        restartDelayAfterStop + " ms.");
                }
                else
                {
                    restartAfterStop = false;
                    SetState(false, "Приёмник остановлен");
                }
            }

            HandleExitedCore();

            if (Interlocked.Exchange(
                    ref coreClientActivityReadyPending, 0) == 1 &&
                IsCoreRunning)
            {
                SetState(true,
                    "Приёмник включён · ожидание подключения", true);
            }

            if (restartPending && DateTime.UtcNow >= restartDueUtc)
            {
                restartPending = false;
                Log("Starting core after scheduled restart; reason: " +
                    restartReason + ".");
                if (!quitting)
                    StartCore(false);
            }

            lock (postSessionMaintenanceSync)
            {
              if (coreReadyPending && IsCoreRunning &&
                  coreReadinessPid > 0 &&
                  Interlocked.CompareExchange(
                      ref activeCorePid, 0, 0) == coreReadinessPid &&
                  DateTime.UtcNow >= coreReadyDueUtc)
              {
                coreReadyChecks++;
                string bonjourStatus = GetBonjourStatus();
                bool socketsReady =
                    Interlocked.CompareExchange(
                        ref coreSocketsReady, 0, 0) == 1 &&
                    DateTime.UtcNow.Ticks >= Interlocked.Read(
                        ref coreSocketsReadyDueTicks);
                int dnsSdStatus = Interlocked.CompareExchange(
                    ref coreDnsSdStatus, 0, 0);
                int bleStatus = Interlocked.CompareExchange(
                    ref coreBleStatus, 0, 0);
                bool bonjourRunning = string.Equals(
                    bonjourStatus, "Running",
                    StringComparison.OrdinalIgnoreCase);
                Log("Core readiness check " + coreReadyChecks +
                    "; Bonjour Service: " + bonjourStatus +
                    "; sockets ready: " + socketsReady +
                    "; DNS-SD marker: " +
                    DiscoveryMarkerStatus(dnsSdStatus, "degraded") +
                    "; BLE marker: " +
                    DiscoveryMarkerStatus(bleStatus, "failed") + ".");
                if (IsCoreReadinessConfirmed(
                        socketsReady, bonjourRunning,
                        dnsSdStatus, bleStatus))
                {
                    coreReadyPending = false;
                    coreReadinessRecoveryAttempts = 0;
                    coreReadinessPid = 0;
                    SetState(true,
                        "Приёмник включён · ожидание подключения", true);
                }
                else if (coreReadyChecks < 8)
                {
                    coreReadyDueUtc = DateTime.UtcNow.AddSeconds(2);
                    SetState(true, "Приёмник запускается · ждём Bonjour…");
                }
                else
                {
                    bool recoveryAvailable =
                        ConsumeSharedAutomaticRecoveryBudget(true);
                    coreReadinessPid = 0;
                    if (recoveryAvailable)
                    {
                        SetState(false,
                            "AirPlay не опубликован · восстанавливаем…");
                        Log("Core readiness was not confirmed after eight checks; " +
                            "performing the single shared automatic recovery.");
                        ScheduleRestart(
                            "readiness recovery", false, 1500);
                    }
                    else
                    {
                        Log("Core readiness was not confirmed after automatic " +
                            "recovery, or the shared automatic recovery budget " +
                            "was already consumed by native discovery. The " +
                            "socket-running receiver stays available; no " +
                            "additional automatic restart or stop will run.");
                        SetState(false,
                            "AirPlay не опубликован · откройте диагностику");
                        if (settings.Notify)
                            tray.ShowBalloonTip(7000, AppTitle,
                                "AeroMirror не смог подтвердить публикацию AirPlay после автоматического перезапуска. Откройте диагностику или нажмите «Обновить обнаружение».",
                                ToolTipIcon.Warning);
                    }
                }
              }
            }
            HandleFeedbackGapPlaceholderTimer();
            HandleFeedbackVideoRecoveryWaitTimer();
            HandleLostConnectionPlaceholder();
            HandleLostConnectionRecovery();
            HandleNativeDiscoveryRefreshTimeout();
            HandleCoreDiscoveryRecovery();
            HandleSessionUnlockDiscoveryRefresh();
            HandleAutomaticDiscoveryMaintenance();
            HandleBonjourFirewallAssessment();
            ApplyTopMost();
            ApplyLostConnectionPlaceholderPolicy();
            if (form != null && !form.IsDisposed)
            {
                form.SyncTheme();
                form.SyncStatus();
            }
        }

        private void HandleExitedCore()
        {
            if (coreProcess == null)
                return;
            Process exitedProcess = coreProcess;
            try
            {
                if (!exitedProcess.HasExited)
                    return;
                int code = exitedProcess.ExitCode;
                CancelCoreOutputReads(exitedProcess);
                uint status = unchecked((uint)code);
                string codeHex = "0x" + status.ToString("X8");
                Log("Core exited with code " + code + " (" + codeHex + ").");
                DateTime now = DateTime.UtcNow;
                if (rapidExitWindowStartedAt == DateTime.MinValue ||
                    (now - rapidExitWindowStartedAt).TotalSeconds > 60)
                {
                    rapidExitWindowStartedAt = now;
                    rapidExitCount = 1;
                }
                else
                {
                    rapidExitCount++;
                }
                DetachCoreProcessForLifecycle(exitedProcess);
                Interlocked.Exchange(ref activeCorePid, 0);
                ResetCoreSessionTracking(true);
                coreReadyPending = false;
                Interlocked.Exchange(ref coreSocketsReady, 0);
                Interlocked.Exchange(ref coreSocketsReadyDueTicks, 0);
                NativeMethods.CloseHandleSafe(ref coreJob);
                exitedProcess.Dispose();
                bool loaderFailure =
                    status == 0xC0000135 ||
                    status == 0xC0000139 ||
                    status == 0xC000007B;
                if (loaderFailure)
                {
                    SetState(false, "Несовместимые или отсутствующие DLL ядра");
                    Log("Automatic restart disabled for permanent Windows " +
                        "loader failure " + codeHex + ".");
                    if (settings.Notify)
                        tray.ShowBalloonTip(7000, AppTitle,
                            "Windows не смог загрузить DLL ядра UxPlay (" +
                            codeHex + "). Переустановите AeroMirror и приложите журнал к отчёту.",
                            ToolTipIcon.Error);
                }
                else if (rapidExitCount >= 3)
                {
                    SetState(false, "Ядро аварийно завершилось");
                    Log("Automatic restart disabled after three exits in " +
                        "the 60-second crash window.");
                    if (settings.Notify)
                        tray.ShowBalloonTip(5000, AppTitle,
                            "Ядро UxPlay завершилось три раза за минуту. Откройте диагностику.",
                            ToolTipIcon.Error);
                }
                else
                {
                    SetState(false, "Приёмник остановлен");
                    if (!quitting && settings.AutoStartReceiver)
                    {
                        int delay = Math.Min(
                            5000, 1000 * Math.Max(1, rapidExitCount));
                        ScheduleRestart(
                            "unexpected exit code " + code + " (" + codeHex + ")",
                            false, delay);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERROR processing core exit: " + ex.Message);
            }
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            long dueTicks = DateTime.UtcNow.AddMilliseconds(1500).Ticks;
            long previousDueTicks = Interlocked.Read(
                ref networkRefreshDueTicks);
            int wasPending = Interlocked.Exchange(
                ref networkRefreshPending, 1);
            if (wasPending == 0 || previousDueTicks <= 0 ||
                dueTicks < previousDueTicks)
                Interlocked.Exchange(ref networkRefreshDueTicks, dueTicks);
        }

        private void OnSessionSwitch(
            object sender, SessionSwitchEventArgs e)
        {
            if (e == null || e.Reason != SessionSwitchReason.SessionUnlock)
                return;
            lock (postSessionMaintenanceSync)
            {
                Interlocked.Exchange(
                    ref sessionUnlockDiscoveryRefreshDueTicks,
                    DateTime.UtcNow.AddSeconds(2).Ticks);
                Interlocked.Exchange(
                    ref sessionUnlockDiscoveryRefreshPending, 1);
            }
        }
    }
}

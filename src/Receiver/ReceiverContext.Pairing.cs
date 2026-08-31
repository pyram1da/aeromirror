using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private enum PairingUiEventKind
        {
            Required,
            Trusted,
            Cancelled,
            TimedOut,
            PersistFailed,
            SessionProgress
        }

        private sealed class PairingUiEvent
        {
            internal PairingUiEventKind Kind;
            internal int ProcessId;
            internal long RequestId;
        }

        private readonly object pairingUiEventSync = new object();
        private readonly Queue<PairingUiEvent> pairingUiEvents =
            new Queue<PairingUiEvent>();
        private PairingPinOverlayForm pairingPinOverlay;
        private long activePairingPinRequest;
        private int activePairingPinProcessId;
        private long pendingPairingCancelRequest;
        private int pendingPairingCancelProcessId;
        private long pendingPairingCancelDueTicks;
        private const int PairingCancelAcknowledgementMilliseconds = 2000;

        private void ObserveNativePairingState(
            int processId, string line)
        {
            Match required = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_PAIRING_PIN_REQUIRED request=([1-9]\d{0,18}) timeout_seconds=60$",
                RegexOptions.CultureInvariant);
            if (required.Success)
            {
                long request;
                if (long.TryParse(required.Groups[1].Value, out request) &&
                    request > 0)
                {
                    QueuePairingUiEvent(
                        PairingUiEventKind.Required, processId, request);
                    HandleIncomingAirPlayClientActivity(
                        processId, PinEntryGraceSeconds,
                        "first-device pairing request");
                }
                return;
            }

            Match state = Regex.Match(
                line ?? "",
                @"^AEROMIRROR_PAIRING_STATE request=([1-9]\d{0,18}) state=(trusted|cancelled|timeout|persist-failed)$",
                RegexOptions.CultureInvariant);
            if (!state.Success)
                return;

            long stateRequest;
            if (!long.TryParse(
                    state.Groups[1].Value, out stateRequest) ||
                stateRequest <= 0)
                return;
            PairingUiEventKind kind;
            switch (state.Groups[2].Value)
            {
                case "trusted":
                    kind = PairingUiEventKind.Trusted;
                    break;
                case "cancelled":
                    kind = PairingUiEventKind.Cancelled;
                    break;
                case "timeout":
                    kind = PairingUiEventKind.TimedOut;
                    break;
                default:
                    kind = PairingUiEventKind.PersistFailed;
                    break;
            }
            QueuePairingUiEvent(kind, processId, stateRequest);
        }

        private void QueuePairingUiEvent(
            PairingUiEventKind kind, int processId, long requestId)
        {
            lock (pairingUiEventSync)
            {
                pairingUiEvents.Enqueue(new PairingUiEvent
                {
                    Kind = kind,
                    ProcessId = processId,
                    RequestId = requestId
                });
            }
        }

        private void QueuePairingSessionProgress(int processId)
        {
            QueuePairingUiEvent(
                PairingUiEventKind.SessionProgress, processId, 0);
        }

        private void HandlePendingPairingUiEvents()
        {
            while (true)
            {
                PairingUiEvent pairingEvent;
                lock (pairingUiEventSync)
                {
                    if (pairingUiEvents.Count == 0)
                        break;
                    pairingEvent = pairingUiEvents.Dequeue();
                }

                if (pairingEvent.ProcessId != Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0))
                    continue;

                if (pairingEvent.Kind == PairingUiEventKind.Required)
                {
                    ShowPairingPinOverlay(
                        pairingEvent.ProcessId, pairingEvent.RequestId);
                    continue;
                }

                if (pairingEvent.Kind ==
                        PairingUiEventKind.SessionProgress)
                {
                    // This marker has no pairing request id. It must not end a
                    // still-active PIN request or stop its cancellation timer.
                    continue;
                }

                if (pairingEvent.RequestId == Interlocked.Read(
                        ref pendingPairingCancelRequest) &&
                    pairingEvent.ProcessId == Interlocked.CompareExchange(
                        ref pendingPairingCancelProcessId, 0, 0))
                {
                    if (pairingEvent.Kind == PairingUiEventKind.Cancelled ||
                        pairingEvent.Kind == PairingUiEventKind.TimedOut)
                    {
                        ClearPendingPairingCancellation();
                        if (!TryDeleteTrustResetPendingMarker())
                        {
                            Log("The acknowledged pairing cancellation left " +
                                "its durable trust-reset marker in place; the " +
                                "next receiver start will clear trust before " +
                                "accepting clients.");
                        }
                        Log("Native pairing cancellation was acknowledged for " +
                            "request " + pairingEvent.RequestId + ".");
                    }
                    else
                    {
                        FailClosedPairingCancellation(
                            pairingEvent.ProcessId,
                            pairingEvent.RequestId,
                            "unexpected native state " +
                                pairingEvent.Kind.ToString());
                    }
                    continue;
                }

                if (pairingEvent.RequestId != activePairingPinRequest ||
                    pairingEvent.ProcessId != activePairingPinProcessId)
                    continue;

                DismissPairingPinOverlay(
                    pairingEvent.RequestId,
                    pairingEvent.Kind.ToString());
                if (pairingEvent.Kind == PairingUiEventKind.PersistFailed &&
                    settings.Notify)
                {
                    tray.ShowBalloonTip(
                        7000, AppTitle,
                        "iPhone подключён, но доверие не удалось сохранить. " +
                        "При следующем подключении код может потребоваться снова.",
                        ToolTipIcon.Warning);
                }
            }
            HandlePairingCancellationDeadline();
        }

        private void ShowPairingPinOverlay(int processId, long requestId)
        {
            if (requestId <= 0 || processId <= 0)
                return;
            if (activePairingPinRequest == requestId &&
                activePairingPinProcessId == processId &&
                pairingPinOverlay != null &&
                !pairingPinOverlay.IsDisposed)
                return;

            if (activePairingPinRequest > 0)
            {
                FailClosedPairingCancellation(
                    processId, requestId,
                    "overlapping native pairing requests");
                return;
            }

            string pin = PairingPinOverlayForm.GenerateCryptographicPin();
            var overlay = new PairingPinOverlayForm(
                pin,
                delegate(bool timedOut)
                {
                    CancelActivePairingRequest(
                        processId, requestId,
                        timedOut ? "timeout" : "user cancellation");
                });
            pairingPinOverlay = overlay;
            activePairingPinRequest = requestId;
            activePairingPinProcessId = processId;
            overlay.Show();
            overlay.Activate();

            if (!TryWriteNativePairingSecret(
                    processId, requestId, pin))
            {
                DismissPairingPinOverlay(requestId, "command failure");
                Log("Pairing PIN could not be delivered to the current " +
                    "native request " + requestId + ".");
                if (settings.Notify)
                {
                    tray.ShowBalloonTip(
                        5000, AppTitle,
                        "Не удалось начать безопасное подключение iPhone. " +
                        "Попробуйте выбрать AeroMirror ещё раз.",
                        ToolTipIcon.Warning);
                }
                FailClosedPairingCancellation(
                    processId, requestId,
                    "pairing secret command failure");
                return;
            }
            Log("First-device pairing overlay shown for native request " +
                requestId + ".");
        }

        private bool TryWriteNativePairingSecret(
            int processId, long requestId, string pin)
        {
            if (requestId <= 0 || !IsFourDigitPairingPin(pin))
                return false;
            Process process = coreProcess;
            if (process == null || !IsCoreRunning)
                return false;
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                if (!object.ReferenceEquals(coreProcess, process) ||
                    !IsCoreRunning || restartPending ||
                    processId != Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) ||
                    Interlocked.CompareExchange(
                        ref restartStopInProgress, 0, 0) == 1)
                    return false;
                try
                {
                    process.StandardInput.WriteLine(
                        "AEROMIRROR_SECRET pairing-pin request=" +
                        requestId + " pin=" + pin);
                    process.StandardInput.Flush();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private bool TryWriteNativePairingCancel(
            int processId, long requestId)
        {
            if (requestId <= 0)
                return false;
            Process process = coreProcess;
            if (process == null || !IsCoreRunning)
                return false;
            if (coreCommandSync == null)
                coreCommandSync = new object();
            lock (coreCommandSync)
            {
                if (!object.ReferenceEquals(coreProcess, process) ||
                    !IsCoreRunning ||
                    processId != Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0))
                    return false;
                try
                {
                    process.StandardInput.WriteLine(
                        "AEROMIRROR_COMMAND pairing-pin-cancel request=" +
                        requestId);
                    process.StandardInput.Flush();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private void CancelActivePairingRequest(
            int processId, long requestId, string reason)
        {
            if (requestId != activePairingPinRequest ||
                processId != activePairingPinProcessId)
                return;
            if (!TryCreateTrustResetPendingMarker())
            {
                Log("ERROR a durable trust-reset marker could not be created " +
                    "before cancelling pairing request " + requestId + ".");
                FailClosedPairingCancellation(
                    processId, requestId,
                    "trust-reset marker creation failure");
                return;
            }
            Interlocked.Exchange(
                ref pendingPairingCancelRequest, requestId);
            Interlocked.Exchange(
                ref pendingPairingCancelProcessId, processId);
            Interlocked.Exchange(
                ref pendingPairingCancelDueTicks,
                DateTime.UtcNow.AddMilliseconds(
                    PairingCancelAcknowledgementMilliseconds).Ticks);
            bool commandWritten =
                TryWriteNativePairingCancel(processId, requestId);
            pairingPinOverlay = null;
            activePairingPinRequest = 0;
            activePairingPinProcessId = 0;
            if (!commandWritten)
            {
                FailClosedPairingCancellation(
                    processId, requestId,
                    "pairing cancellation command failure");
                return;
            }
            Log("First-device pairing request " + requestId +
                " was cancelled by " + reason +
                "; waiting for the correlated native acknowledgement.");
        }

        private void HandlePairingCancellationDeadline()
        {
            long dueTicks = Interlocked.Read(ref pendingPairingCancelDueTicks);
            if (dueTicks <= 0 || DateTime.UtcNow.Ticks < dueTicks)
                return;
            long requestId = Interlocked.Read(ref pendingPairingCancelRequest);
            int processId = Interlocked.CompareExchange(
                ref pendingPairingCancelProcessId, 0, 0);
            if (requestId <= 0 || processId <= 0)
            {
                ClearPendingPairingCancellation();
                return;
            }
            FailClosedPairingCancellation(
                processId, requestId,
                "pairing cancellation acknowledgement timeout");
        }

        private void ClearPendingPairingCancellation()
        {
            Interlocked.Exchange(ref pendingPairingCancelRequest, 0);
            Interlocked.Exchange(ref pendingPairingCancelProcessId, 0);
            Interlocked.Exchange(ref pendingPairingCancelDueTicks, 0);
        }

        private void FailClosedPairingCancellation(
            int processId, long requestId, string reason)
        {
            bool trustResetRecorded = TryCreateTrustResetPendingMarker();
            ClearPendingPairingCancellation();
            DismissPairingPinOverlay(0, "fail-closed cancellation");
            if (processId != Interlocked.CompareExchange(
                    ref activeCorePid, 0, 0))
            {
                return;
            }

            Log("Pairing request " + requestId + " could not be cancelled " +
                "authoritatively; stopping the current core: " + reason + ".");
            bool stopConfirmed = StopCoreInternal(
                "fail-closed pairing cancellation", false, false);
            if (!stopConfirmed)
            {
                Log("ERROR native core exit was not confirmed after pairing " +
                    "cancellation; the durable trust-reset request remains " +
                    (trustResetRecorded ? "recorded." : "unavailable."));
                if (settings.Notify)
                {
                    tray.ShowBalloonTip(
                        7000, AppTitle,
                        "Не удалось безопасно остановить приёмник. " +
                            "Очистка доверия будет завершена после остановки " +
                            "приёмника; повторите остановку или перезапустите " +
                            "компьютер.",
                        ToolTipIcon.Error);
                }
                return;
            }
            bool trustCleared = trustResetRecorded &&
                TryResolvePendingTrustResetAfterConfirmedCoreExit(
                    stopConfirmed);
            if (trustCleared && settings.AutoStartReceiver)
            {
                ScheduleRestart(
                    "fail-closed pairing cancellation", false, 1000);
            }
            else if (!trustCleared)
            {
                Log("ERROR the trust store could not be cleared after an " +
                    "unacknowledged pairing cancellation; receiver remains " +
                    "stopped.");
            }
            if (settings.Notify)
            {
                tray.ShowBalloonTip(
                    7000, AppTitle,
                    trustCleared
                        ? "Безопасное подключение было отменено. Приёмник " +
                            "перезапускается."
                        : "Подключение отменено, но хранилище доверия не " +
                            "удалось очистить. Приёмник остановлен.",
                    ToolTipIcon.Warning);
            }
        }

        private void DismissPairingPinOverlay(
            long requestId, string reason)
        {
            if (requestId > 0 && requestId != activePairingPinRequest)
                return;
            PairingPinOverlayForm overlay = pairingPinOverlay;
            pairingPinOverlay = null;
            activePairingPinRequest = 0;
            activePairingPinProcessId = 0;
            if (overlay != null && !overlay.IsDisposed)
                overlay.Dismiss();
            if (overlay != null)
                Log("First-device pairing overlay dismissed: " + reason + ".");
        }

        private void ResetPairingForCoreLifecycle()
        {
            lock (pairingUiEventSync)
                pairingUiEvents.Clear();
            ClearPendingPairingCancellation();
            DismissPairingPinOverlay(0, "core lifecycle");
        }

        public bool HasTrustedDevices
        {
            get
            {
                try
                {
                    return File.Exists(AppSettings.TrustedClientsPath) &&
                        new FileInfo(
                            AppSettings.TrustedClientsPath).Length > 0;
                }
                catch { return false; }
            }
        }

        public bool RevokeTrustedDevices()
        {
            if (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
            {
                Log("Trusted-device revocation was deferred because a native " +
                    "core stop has not been confirmed yet.");
                return false;
            }
            bool restartAfterRevocation = coreProcess != null;
            if (!TryCreateTrustResetPendingMarker())
            {
                Log("ERROR trusted-device revocation could not record its " +
                    "durable pending state.");
                return false;
            }
            bool stopConfirmed = !restartAfterRevocation ||
                StopCoreInternal(
                    "trusted devices revoked", false, false);
            ResetPairingForCoreLifecycle();
            if (!stopConfirmed)
            {
                Log("ERROR trusted devices were not revoked because native " +
                    "core exit could not be confirmed.");
                return false;
            }
            bool cleared =
                TryResolvePendingTrustResetAfterConfirmedCoreExit(
                    stopConfirmed);
            if (!cleared)
            {
                Log("ERROR the receiver remains stopped because trusted-device " +
                    "revocation could not be committed atomically.");
                return false;
            }

            Log("All trusted AirPlay devices were revoked.");
            if (restartAfterRevocation && settings.AutoStartReceiver)
            {
                ResetRapidExitWindow();
                ScheduleRestart(
                    "trusted devices revoked", false, 500);
            }
            return true;
        }

        private static bool TryClearTrustedDeviceStore()
        {
            string target = AppSettings.TrustedClientsPath;
            string temporary = target + "." +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllText(
                    temporary, "", new UTF8Encoding(false));
                if (File.Exists(target))
                    File.Replace(temporary, target, null, true);
                else
                    File.Move(temporary, target);
            }
            catch (Exception ex)
            {
                Log("Trusted-device revocation failed: " + ex.Message);
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch { }
                return false;
            }
            return true;
        }

        private static bool TryClearTrustedDeviceStoreAfterConfirmedStop(
            bool stopConfirmed)
        {
            return stopConfirmed && TryClearTrustedDeviceStore();
        }

        private static bool IsTrustResetPending()
        {
            try
            {
                return File.Exists(AppSettings.TrustResetPendingPath);
            }
            catch
            {
                return true;
            }
        }

        private static bool TryCreateTrustResetPendingMarker()
        {
            string target = AppSettings.TrustResetPendingPath;
            string temporary = target + "." +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target))
                {
                    FileAttributes existing = File.GetAttributes(target);
                    return (existing & FileAttributes.ReparsePoint) == 0 &&
                        (existing & FileAttributes.Directory) == 0;
                }
                byte[] marker = Encoding.ASCII.GetBytes(
                    "AeroMirror trust reset pending\n");
                using (var stream = new FileStream(
                    temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(marker, 0, marker.Length);
                    stream.Flush(true);
                }
                File.Move(temporary, target);
                return true;
            }
            catch (Exception ex)
            {
                Log("Could not persist the trust-reset marker: " + ex.Message);
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch { }
                return false;
            }
        }

        private static bool TryDeleteTrustResetPendingMarker()
        {
            string target = AppSettings.TrustResetPendingPath;
            try
            {
                if (!File.Exists(target))
                    return true;
                FileAttributes attributes = File.GetAttributes(target);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    (attributes & FileAttributes.Directory) != 0)
                    return false;
                File.Delete(target);
                return !File.Exists(target);
            }
            catch (Exception ex)
            {
                Log("Could not clear the trust-reset marker: " + ex.Message);
                return false;
            }
        }

        private static bool TryResolvePendingTrustResetAfterConfirmedCoreExit(
            bool coreExitConfirmed)
        {
            if (!coreExitConfirmed)
                return false;
            if (!IsTrustResetPending())
                return true;
            if (!TryClearTrustedDeviceStore())
                return false;
            return TryDeleteTrustResetPendingMarker();
        }

        private bool TryResolvePendingTrustResetBeforeCoreStart()
        {
            if (!IsTrustResetPending())
                return true;
            if (coreProcess != null || IsCoreRunning)
                return false;
            bool resolved =
                TryResolvePendingTrustResetAfterConfirmedCoreExit(true);
            if (resolved)
                Log("Completed the durable trust reset before receiver start.");
            return resolved;
        }

        private static bool IsFourDigitPairingPin(string pin)
        {
            if (pin == null || pin.Length != 4)
                return false;
            foreach (char digit in pin)
            {
                if (digit < '0' || digit > '9')
                    return false;
            }
            return true;
        }
    }
}

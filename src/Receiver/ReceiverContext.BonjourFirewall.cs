using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private static readonly TimeSpan BonjourFirewallAssessmentLifetime =
            TimeSpan.FromMinutes(2);
        private readonly object bonjourFirewallAssessmentSync = new object();
        private BonjourFirewallAssessment bonjourFirewallAssessment;
        private BonjourServiceAssessment bonjourServiceAssessment;
        private DateTime bonjourFirewallAssessmentCompletedUtc =
            DateTime.MinValue;
        private int bonjourFirewallAssessmentRunning;
        private int bonjourFirewallAssessmentReady;
        private int bonjourFirewallAssessmentGeneration;
        private bool bonjourFirewallWarningShown;
        private bool bonjourServiceWarningShown;
        private readonly object bonjourExplicitRecoverySync = new object();
        private int bonjourExplicitRecoveryRunning;
        private int bonjourExplicitRecoveryReady;
        private bool bonjourExplicitRecoverySucceeded;
        private bool bonjourExplicitRecoveryCancelled;
        private string bonjourExplicitRecoveryDetail = "";

        private void BeginBonjourFirewallAssessment()
        {
            if (Interlocked.CompareExchange(
                    ref bonjourFirewallAssessmentRunning, 1, 0) != 0)
                return;

            int generation;
            lock (bonjourFirewallAssessmentSync)
                generation = bonjourFirewallAssessmentGeneration;

            ThreadPool.QueueUserWorkItem(delegate
            {
                BonjourFirewallAssessment assessment;
                BonjourServiceAssessment serviceAssessment;
                try
                {
                    assessment =
                        BonjourFirewallService.AssessPrivateMdnsRule();
                }
                catch (Exception exception)
                {
                    assessment = new BonjourFirewallAssessment
                    {
                        State = BonjourFirewallState.PolicyUnavailable,
                        ExecutablePath = "",
                        Detail = exception.Message
                    };
                }
                try
                {
                    serviceAssessment =
                        BonjourServiceRecoveryService.Assess();
                }
                catch (Exception exception)
                {
                    serviceAssessment = new BonjourServiceAssessment
                    {
                        State = BonjourServiceState.Unknown,
                        Detail = exception.Message
                    };
                }

                bool stale;
                lock (bonjourFirewallAssessmentSync)
                {
                    stale = generation !=
                        bonjourFirewallAssessmentGeneration;
                    if (!stale)
                    {
                        bonjourFirewallAssessment = assessment;
                        bonjourServiceAssessment = serviceAssessment;
                        bonjourFirewallAssessmentCompletedUtc =
                            DateTime.UtcNow;
                        Interlocked.Exchange(
                            ref bonjourFirewallAssessmentReady, 1);
                    }
                    Interlocked.Exchange(
                        ref bonjourFirewallAssessmentRunning, 0);
                }
                if (stale)
                    BeginBonjourFirewallAssessment();
            });
        }

        private void HandleBonjourFirewallAssessment()
        {
            if (Interlocked.Exchange(
                    ref bonjourFirewallAssessmentReady, 0) != 1)
                return;

            BonjourFirewallAssessment assessment;
            BonjourServiceAssessment serviceAssessment;
            lock (bonjourFirewallAssessmentSync)
            {
                assessment = bonjourFirewallAssessment;
                serviceAssessment = bonjourServiceAssessment;
            }
            bool missing = assessment != null &&
                assessment.State == BonjourFirewallState.Missing;
            bool serviceUnavailable = serviceAssessment != null &&
                (serviceAssessment.State == BonjourServiceState.Stopped ||
                 serviceAssessment.State == BonjourServiceState.StopPending ||
                 serviceAssessment.State == BonjourServiceState.PausePending ||
                 serviceAssessment.State == BonjourServiceState.Paused);
            if (serviceUnavailable)
            {
                Log("Bonjour service assessment: " +
                    serviceAssessment.State + ".");
                if (!bonjourServiceWarningShown && settings.Notify)
                {
                    bonjourServiceWarningShown = true;
                    string message;
                    switch (serviceAssessment.State)
                    {
                        case BonjourServiceState.StopPending:
                            message = "Bonjour завершает остановку. AeroMirror повторит проверку.";
                            break;
                        case BonjourServiceState.PausePending:
                            message = "Bonjour приостанавливается. AeroMirror повторит проверку.";
                            break;
                        case BonjourServiceState.Paused:
                            message = "Bonjour приостановлен. Возобновите службу в службах Windows.";
                            break;
                        default:
                            message = "Bonjour остановлен, поэтому приёмник сейчас не виден в AirPlay. Откройте AeroMirror и нажмите «Запустить Bonjour».";
                            break;
                    }
                    tray.ShowBalloonTip(
                        9000,
                        AppTitle,
                        message,
                        ToolTipIcon.Warning);
                }
                return;
            }

            bonjourServiceWarningShown = false;
            if (!missing)
            {
                bonjourFirewallWarningShown = false;
                return;
            }

            Log("Bonjour Private mDNS firewall assessment: missing exact " +
                "UDP 5353 LocalSubnet rule.");
            if (!bonjourFirewallWarningShown && settings.Notify)
            {
                bonjourFirewallWarningShown = true;
                tray.ShowBalloonTip(
                    9000,
                    AppTitle,
                    "Windows может блокировать Bonjour. Снова запустите Setup: он предложит безопасную проверку с правами администратора.",
                    ToolTipIcon.Warning);
            }
        }

        public bool IsBonjourServiceRecoveryRunning
        {
            get
            {
                return Interlocked.CompareExchange(
                        ref bonjourExplicitRecoveryRunning, 0, 0) == 1 ||
                    Interlocked.CompareExchange(
                        ref bonjourExplicitRecoveryReady, 0, 0) == 1;
            }
        }

        public bool CanRequestBonjourServiceRecovery
        {
            get
            {
                if (IsBonjourServiceRecoveryRunning)
                    return false;
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.Stopped;
            }
        }

        public void RequestBonjourServiceRecovery()
        {
            if (Interlocked.CompareExchange(
                    ref bonjourExplicitRecoveryReady, 0, 0) != 0)
                return;
            if (Interlocked.CompareExchange(
                    ref bonjourExplicitRecoveryRunning, 1, 0) != 0)
                return;

            Process process = null;
            bool cancelled;
            string detail;
            try
            {
                if (!BonjourServiceRecoveryService.TryLaunchExplicitStart(
                        out process, out cancelled, out detail))
                {
                    DisposeBonjourExplicitRecoveryProcess(process);
                    CompleteBonjourExplicitRecovery(
                        false, cancelled, detail, true);
                    return;
                }
            }
            catch (Exception exception)
            {
                DisposeBonjourExplicitRecoveryProcess(process);
                CompleteBonjourExplicitRecovery(
                    false, false, exception.Message, true);
                return;
            }

            try
            {
                bool queued = ThreadPool.QueueUserWorkItem(delegate
                {
                    bool succeeded = false;
                    bool processExited = process == null;
                    string resultDetail = "";
                    try
                    {
                        succeeded =
                            BonjourServiceRecoveryService.WaitForExplicitStart(
                                process, out processExited, out resultDetail);
                    }
                    catch (Exception exception)
                    {
                        resultDetail = exception.Message;
                        processExited =
                            BonjourServiceRecoveryService.
                                IsProcessExitConfirmed(process);
                    }
                    if (processExited)
                    {
                        DisposeBonjourExplicitRecoveryProcess(process);
                        CompleteBonjourExplicitRecovery(
                            succeeded, false, resultDetail, true);
                        return;
                    }

                    CompleteBonjourExplicitRecovery(
                        succeeded, false, resultDetail, false);
                    WaitForBonjourExplicitRecoveryProcessExitAndRelease(
                        process);
                });
                if (!queued)
                {
                    HandleBonjourExplicitRecoveryWorkerFailure(
                        process,
                        "Windows did not queue the Bonjour recovery wait.");
                    return;
                }
                Log("Explicit Bonjour start request accepted; waiting for " +
                    "the validated service to reach Running.");
            }
            catch (Exception exception)
            {
                HandleBonjourExplicitRecoveryWorkerFailure(
                    process, exception.Message);
            }
        }

        private void HandleBonjourExplicitRecoveryWorkerFailure(
            Process process, string detail)
        {
            if (BonjourServiceRecoveryService.IsProcessExitConfirmed(process))
            {
                DisposeBonjourExplicitRecoveryProcess(process);
                CompleteBonjourExplicitRecovery(
                    false, false, detail, true);
                return;
            }

            CompleteBonjourExplicitRecovery(
                false, false, detail, false);
            try
            {
                var watcher = new Thread(new ThreadStart(delegate
                    {
                        WaitForBonjourExplicitRecoveryProcessExitAndRelease(
                            process);
                    }));
                watcher.IsBackground = true;
                watcher.Name = "AeroMirror Bonjour recovery exit";
                watcher.Start();
            }
            catch (Exception exception)
            {
                Log("Bonjour recovery process is still active and its " +
                    "one-flight latch remains closed because an exit watcher " +
                    "could not start: " + exception.Message);
            }
        }

        private void WaitForBonjourExplicitRecoveryProcessExitAndRelease(
            Process process)
        {
            if (!BonjourServiceRecoveryService.
                    WaitForExplicitStartProcessExit(process))
            {
                Log("Bonjour recovery process exit could not be confirmed; " +
                    "the one-flight latch remains closed.");
                return;
            }

            DisposeBonjourExplicitRecoveryProcess(process);
            try
            {
                if (!quitting)
                    RefreshBonjourFirewallAssessment();
            }
            catch (Exception exception)
            {
                Log("Bonjour assessment refresh after recovery-process exit " +
                    "failed: " + exception.Message);
            }
            finally
            {
                Interlocked.Exchange(
                    ref bonjourExplicitRecoveryRunning, 0);
            }
            Log("Bonjour recovery process exit was confirmed; a later " +
                "explicit retry is permitted.");
        }

        private static void DisposeBonjourExplicitRecoveryProcess(
            Process process)
        {
            if (process == null)
                return;
            try
            {
                process.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void CompleteBonjourExplicitRecovery(
            bool succeeded, bool cancelled, string detail,
            bool releaseFlight)
        {
            lock (bonjourExplicitRecoverySync)
            {
                bonjourExplicitRecoverySucceeded = succeeded;
                bonjourExplicitRecoveryCancelled = cancelled;
                bonjourExplicitRecoveryDetail = detail ?? "";
            }
            Interlocked.Exchange(ref bonjourExplicitRecoveryReady, 1);
            if (releaseFlight)
                Interlocked.Exchange(
                    ref bonjourExplicitRecoveryRunning, 0);
        }

        private void HandleBonjourExplicitRecoveryResult()
        {
            if (Interlocked.Exchange(
                    ref bonjourExplicitRecoveryReady, 0) != 1)
                return;

            bool succeeded;
            bool cancelled;
            string detail;
            lock (bonjourExplicitRecoverySync)
            {
                succeeded = bonjourExplicitRecoverySucceeded;
                cancelled = bonjourExplicitRecoveryCancelled;
                detail = bonjourExplicitRecoveryDetail;
            }
            RefreshBonjourFirewallAssessment();

            if (succeeded)
            {
                bonjourServiceWarningShown = false;
                Log("Bonjour reached Running after the explicit user " +
                    "recovery request; resuming DNS-SD publication.");
                if (IsCoreRunning)
                    ResumeDiscoveryAfterBonjourRecovery();
                if (settings.Notify)
                {
                    tray.ShowBalloonTip(
                        5000,
                        AppTitle,
                        "Bonjour запущен. AeroMirror повторно публикует приёмник в AirPlay.",
                        ToolTipIcon.Info);
                }
                return;
            }

            if (cancelled)
            {
                Log("Bonjour start was canceled at the Windows " +
                    "administrator confirmation.");
                return;
            }

            Log("Bonjour explicit recovery failed: " + detail);
            if (settings.Notify)
            {
                tray.ShowBalloonTip(
                    7000,
                    AppTitle,
                    "Не удалось запустить Bonjour. Повторите действие или переустановите Apple Bonjour.",
                    ToolTipIcon.Warning);
            }
        }

        private BonjourFirewallAssessment GetBonjourFirewallAssessment()
        {
            BonjourFirewallAssessment assessment;
            bool refresh;
            lock (bonjourFirewallAssessmentSync)
            {
                assessment = bonjourFirewallAssessment;
                refresh = assessment == null ||
                    bonjourFirewallAssessmentCompletedUtc == DateTime.MinValue ||
                    DateTime.UtcNow - bonjourFirewallAssessmentCompletedUtc >=
                        BonjourFirewallAssessmentLifetime;
            }
            if (refresh)
                BeginBonjourFirewallAssessment();
            return assessment;
        }

        private void RefreshBonjourFirewallAssessment()
        {
            lock (bonjourFirewallAssessmentSync)
            {
                bonjourFirewallAssessmentGeneration++;
                bonjourFirewallAssessment = null;
                bonjourServiceAssessment = null;
                bonjourFirewallAssessmentCompletedUtc = DateTime.MinValue;
                Interlocked.Exchange(
                    ref bonjourFirewallAssessmentReady, 0);
            }
            BeginBonjourFirewallAssessment();
        }

        public bool IsBonjourFirewallRepairRequired
        {
            get
            {
                BonjourFirewallAssessment assessment =
                    GetBonjourFirewallAssessment();
                return assessment != null &&
                    assessment.State == BonjourFirewallState.Missing;
            }
        }

        public bool IsBonjourUnavailable
        {
            get
            {
                BonjourFirewallAssessment assessment =
                    GetBonjourFirewallAssessment();
                BonjourServiceAssessment serviceAssessment =
                    GetBonjourServiceAssessment();
                return (assessment != null && assessment.State ==
                        BonjourFirewallState.BonjourUnavailable) ||
                    (serviceAssessment != null &&
                     serviceAssessment.State ==
                        BonjourServiceState.MissingOrUnsafe);
            }
        }

        private BonjourServiceAssessment GetBonjourServiceAssessment()
        {
            GetBonjourFirewallAssessment();
            lock (bonjourFirewallAssessmentSync)
                return bonjourServiceAssessment;
        }

        public bool IsBonjourServiceRecoveryRequired
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.Stopped;
            }
        }

        public bool IsBonjourServiceStarting
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.StartPending;
            }
        }

        public bool IsBonjourServiceStopping
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.StopPending;
            }
        }

        public bool IsBonjourServicePausePending
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.PausePending;
            }
        }

        public bool IsBonjourServicePaused
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.Paused;
            }
        }

        public bool IsBonjourServiceStatusUnknown
        {
            get
            {
                BonjourServiceAssessment assessment =
                    GetBonjourServiceAssessment();
                return assessment != null &&
                    assessment.State == BonjourServiceState.Unknown;
            }
        }

        internal string GetBonjourFirewallDiagnosticLine()
        {
            BonjourFirewallAssessment assessment =
                GetBonjourFirewallAssessment();
            if (assessment == null)
                return "не проверено";
            switch (assessment.State)
            {
                case BonjourFirewallState.Configured:
                    return "Private UDP 5353 LocalSubnet — разрешён";
                case BonjourFirewallState.Missing:
                    return "Private UDP 5353 LocalSubnet — НЕТ ПРАВИЛА";
                case BonjourFirewallState.BonjourUnavailable:
                    return "Bonjour не найден или путь службы небезопасен";
                default:
                    return "политику Windows Firewall прочитать не удалось";
            }
        }

    }
}

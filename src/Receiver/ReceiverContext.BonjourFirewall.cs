using System;
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
            bool serviceStopped = serviceAssessment != null &&
                (serviceAssessment.State == BonjourServiceState.Stopped ||
                 serviceAssessment.State == BonjourServiceState.StopPending);
            if (serviceStopped)
            {
                Log("Bonjour service assessment: " +
                    serviceAssessment.State + ".");
                if (!bonjourServiceWarningShown && settings.Notify)
                {
                    bonjourServiceWarningShown = true;
                    tray.ShowBalloonTip(
                        9000,
                        AppTitle,
                        "Bonjour остановлен, поэтому приёмник сейчас не виден в AirPlay. Если служба не вернулась, снова запустите Setup или откройте диагностику.",
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
                    (assessment.State == BonjourServiceState.Stopped ||
                     assessment.State == BonjourServiceState.StopPending);
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

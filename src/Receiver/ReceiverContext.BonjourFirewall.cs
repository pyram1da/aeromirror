using System;
using System.Threading;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private readonly object bonjourFirewallAssessmentSync = new object();
        private readonly ToolStripMenuItem bonjourFirewallItem;
        private BonjourFirewallAssessment bonjourFirewallAssessment;
        private int bonjourFirewallAssessmentRunning;
        private int bonjourFirewallAssessmentReady;
        private bool bonjourFirewallWarningShown;
        private int bonjourFirewallRepairRunning;
        private int bonjourFirewallRepairReady;
        private BonjourFirewallChangeResult bonjourFirewallRepairResult;

        private void BeginBonjourFirewallAssessment()
        {
            if (Interlocked.CompareExchange(
                    ref bonjourFirewallAssessmentRunning, 1, 0) != 0)
                return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                BonjourFirewallAssessment assessment;
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

                lock (bonjourFirewallAssessmentSync)
                    bonjourFirewallAssessment = assessment;
                Interlocked.Exchange(
                    ref bonjourFirewallAssessmentRunning, 0);
                Interlocked.Exchange(ref bonjourFirewallAssessmentReady, 1);
            });
        }

        private void HandleBonjourFirewallAssessment()
        {
            HandleBonjourFirewallRepairResult();
            if (Interlocked.Exchange(
                    ref bonjourFirewallAssessmentReady, 0) != 1)
                return;

            BonjourFirewallAssessment assessment;
            lock (bonjourFirewallAssessmentSync)
                assessment = bonjourFirewallAssessment;
            bool missing = assessment != null &&
                assessment.State == BonjourFirewallState.Missing;
            bonjourFirewallItem.Visible = missing;
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
                    "Windows может блокировать обнаружение AirPlay в частной сети. Откройте меню AeroMirror и выберите «Исправить доступ Bonjour…».",
                    ToolTipIcon.Warning);
            }
        }

        private BonjourFirewallAssessment GetBonjourFirewallAssessment()
        {
            lock (bonjourFirewallAssessmentSync)
            {
                if (bonjourFirewallAssessment != null)
                    return bonjourFirewallAssessment;
            }
            BeginBonjourFirewallAssessment();
            return null;
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

        public void RepairBonjourFirewall(IWin32Window owner)
        {
            BonjourFirewallAssessment assessment =
                GetBonjourFirewallAssessment();

            if (assessment == null)
            {
                MessageBox.Show(
                    owner,
                    "Проверка правил Bonjour ещё выполняется. Повторите через несколько секунд.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (assessment.State == BonjourFirewallState.Configured)
            {
                MessageBox.Show(
                    owner,
                    "Доступ Bonjour в частной сети уже настроен правильно.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            if (assessment.State != BonjourFirewallState.Missing)
            {
                MessageBox.Show(
                    owner,
                    "AeroMirror не смог безопасно определить программу Bonjour или прочитать правила Windows Firewall. Никаких изменений не внесено.\r\n\r\n" +
                    assessment.Detail,
                    AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                owner,
                "Windows не разрешает программе Bonjour принимать mDNS-запросы в частной сети. Из-за этого приёмник может работать, но не появляться на iPhone.\r\n\r\n" +
                "AeroMirror добавит одно входящее правило только для найденного mDNSResponder.exe: частная сеть, UDP 5353, только локальная подсеть. Публичная сеть и остальные порты не изменятся.\r\n\r\n" +
                "Windows покажет стандартное подтверждение администратора. Продолжить?",
                "Исправить доступ Bonjour",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirmation != DialogResult.Yes)
                return;

            if (Interlocked.CompareExchange(
                    ref bonjourFirewallRepairRunning, 1, 0) != 0)
                return;
            bonjourFirewallItem.Enabled = false;
            bonjourFirewallItem.Text = "Исправляем доступ Bonjour…";
            Log("Bonjour Private mDNS firewall repair requested by user.");
            ThreadPool.QueueUserWorkItem(delegate
            {
                BonjourFirewallChangeResult result;
                try
                {
                    result = BonjourFirewallService.
                        RepairPrivateMdnsRuleExplicitlyWithUac();
                }
                catch (Exception exception)
                {
                    Log("Bonjour Private mDNS firewall repair failed: " +
                        exception.Message);
                    result = BonjourFirewallChangeResult.Failed;
                }
                lock (bonjourFirewallAssessmentSync)
                    bonjourFirewallRepairResult = result;
                Interlocked.Exchange(ref bonjourFirewallRepairRunning, 0);
                Interlocked.Exchange(ref bonjourFirewallRepairReady, 1);
            });
        }

        private void HandleBonjourFirewallRepairResult()
        {
            if (Interlocked.Exchange(
                    ref bonjourFirewallRepairReady, 0) != 1)
                return;

            BonjourFirewallChangeResult result;
            lock (bonjourFirewallAssessmentSync)
                result = bonjourFirewallRepairResult;
            bonjourFirewallItem.Enabled = true;
            bonjourFirewallItem.Text = "Исправить доступ Bonjour…";
            IWin32Window owner = form != null && !form.IsDisposed
                ? (IWin32Window)form
                : null;
            if (result == BonjourFirewallChangeResult.Applied ||
                result == BonjourFirewallChangeResult.AlreadyConfigured)
            {
                Log("Bonjour Private mDNS firewall repair completed: " +
                    result + ".");
                bonjourFirewallItem.Visible = false;
                bonjourFirewallWarningShown = false;
                BeginBonjourFirewallAssessment();
                MessageBox.Show(
                    owner,
                    "Доступ Bonjour исправлен. AeroMirror сейчас обновит обнаружение AirPlay.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                RefreshDiscovery();
                return;
            }

            string message = result ==
                    BonjourFirewallChangeResult.ElevationCanceled
                ? "Подтверждение администратора отменено. Никаких изменений не внесено."
                : "Не удалось добавить узкое правило Bonjour. Никаких широких правил AeroMirror не создавал.";
            Log("Bonjour Private mDNS firewall repair did not complete: " +
                result + ".");
            MessageBox.Show(
                owner,
                message,
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}

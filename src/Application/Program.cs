using System;
using System.Threading;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(
                UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(
                object sender, ThreadExceptionEventArgs e)
            {
                ReceiverContext.Log("FATAL UI: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(
                object sender, UnhandledExceptionEventArgs e)
            {
                ReceiverContext.Log(
                    "FATAL APPDOMAIN: " + Convert.ToString(e.ExceptionObject));
                ReceiverContext.FlushLog(1000);
            };
            bool created;
            using (var mutex = new Mutex(true, "Local\\AirPlayReceiverMvp.Singleton", out created))
            using (var showEvent = new EventWaitHandle(
                false, EventResetMode.AutoReset, "Local\\AirPlayReceiverMvp.Show"))
            {
                if (!created)
                {
                    showEvent.Set();
                    return;
                }

                try
                {
                    AutomaticUpdateService.CleanupStaleDownloads();
                    AppSettings startupSettings = AppSettings.Load();
                    bool recoveringFromBusySetup = HasArgument(
                        args, "--update-busy-recovery");
                    bool recoveringFromFailedUpdate = HasArgument(
                        args, "--update-recovery") ||
                        recoveringFromBusySetup;
                    if (!startupSettings.AutomaticUpdates)
                    {
                        AutomaticUpdateService.ClearStagedUpdate();
                    }
                    else if (recoveringFromBusySetup)
                    {
                        string busyRecoveryStatus;
                        AutomaticUpdateService
                            .RestorePendingLaunchAttemptAfterSetupBusy(
                                AppVersion.Current,
                                out busyRecoveryStatus);
                        ReceiverContext.Log(busyRecoveryStatus);
                    }
                    else if (!recoveringFromFailedUpdate)
                    {
                        string automaticUpdateStatus;
                        if (AutomaticUpdateService.TryLaunchPendingUpdate(
                                AppVersion.Current,
                                out automaticUpdateStatus))
                        {
                            ReceiverContext.Log(automaticUpdateStatus);
                            ReceiverContext.FlushLog(1000);
                            return;
                        }
                        if (!string.Equals(
                                automaticUpdateStatus,
                                "Подготовленного обновления нет.",
                                StringComparison.Ordinal))
                        {
                            ReceiverContext.Log(automaticUpdateStatus);
                        }
                    }
                    else
                    {
                        ReceiverContext.Log(
                            "Skipped one pending-update handoff while " +
                            "recovering the installed receiver after a failed " +
                            "Setup transaction.");
                    }
                }
                catch (Exception exception)
                {
                    // Update housekeeping must never prevent the receiver from
                    // starting. A later safe launch can retry or discard it.
                    ReceiverContext.Log(
                        "Automatic update startup handoff skipped: " +
                        exception.Message);
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new ReceiverContext(args, showEvent));
                }
                catch (Exception ex)
                {
                    ReceiverContext.Log("FATAL: " + ex);
                    ReceiverContext.FlushLog(1000);
                    MessageBox.Show("Приложение не удалось запустить.\r\n\r\n" + ex.Message,
                        "AeroMirror", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            if (args == null)
                return false;
            foreach (string argument in args)
            {
                if (string.Equals(
                        argument, expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

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
    }
}

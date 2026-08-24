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
        private void ApplyAutostart(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (enabled)
                    {
                        key.SetValue("AeroMirror",
                            "\"" + Assembly.GetExecutingAssembly().Location + "\" --startup",
                            RegistryValueKind.String);
                        key.DeleteValue("AirPlayReceiverMvp", false);
                    }
                    else
                    {
                        key.DeleteValue("AeroMirror", false);
                        key.DeleteValue("AirPlayReceiverMvp", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERROR updating autostart: " + ex.Message);
            }
        }

        private bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    return key != null &&
                        (key.GetValue("AeroMirror") != null ||
                         key.GetValue("AirPlayReceiverMvp") != null);
                }
            }
            catch { return false; }
        }

        private string GetBonjourStatus()
        {
            string[] names = { "Bonjour Service", "mDNSResponder" };
            foreach (string name in names)
            {
                try
                {
                    using (var service = new ServiceController(name))
                    {
                        ServiceControllerStatus status = service.Status;
                        return status.ToString();
                    }
                }
                catch { }
            }
            return "не установлен или недоступен";
        }

        private void ShowDiagnostics()
        {
            using (var dialog = new DiagnosticsForm(GetDiagnostics()))
                dialog.ShowDialog();
        }

        private void OpenLog()
        {
            if (!File.Exists(AppSettings.LogPath))
                File.WriteAllText(AppSettings.LogPath, "", Encoding.UTF8);
            Process.Start(new ProcessStartInfo(AppSettings.LogPath) { UseShellExecute = true });
        }

        public bool OpenProblemReport(IWin32Window owner)
        {
            try
            {
                FlushLog(1000);
                string folder = Path.Combine(
                    Path.GetTempPath(), "AeroMirror", "Support");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(
                    folder,
                    "AeroMirror-report-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");

                var report = new StringBuilder();
                report.AppendLine(
                    "AeroMirror support report — review before attaching");
                report.AppendLine(
                    "Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                report.AppendLine();
                report.AppendLine(GetDiagnostics());
                report.AppendLine();
                report.AppendLine("Recent application log:");
                report.AppendLine(ReadLogTail(AppSettings.LogPath, 1024 * 1024));
                File.WriteAllText(
                    path,
                    RedactSupportText(report.ToString(), settings.FixedPin),
                    new UTF8Encoding(false));

                string issueBody =
                    "Опишите, что произошло и как повторить проблему.\r\n\r\n" +
                    "Версия AeroMirror: " + AppVersion.Display + "\r\n" +
                    "Windows: " + Environment.OSVersion.Version + "\r\n\r\n" +
                    "AeroMirror подготовил обезличенный файл `" +
                    Path.GetFileName(path) + "`. GitHub не разрешает приложению " +
                    "прикрепить локальный файл автоматически: перетащите выбранный " +
                    "файл в это сообщение после входа в GitHub.";
                string issueUrl =
                    "https://github.com/Nadejny/aeromirror/issues/new" +
                    "?title=" + Uri.EscapeDataString("[Bug] ") +
                    "&body=" + Uri.EscapeDataString(issueBody);
                // Keep the hand-off understandable: first show the report that
                // must be attached, then open GitHub shortly afterwards.  A
                // success MessageBox here used to compete with both windows.
                Process.Start(new ProcessStartInfo("explorer.exe")
                {
                    Arguments = "/select,\"" + path + "\"",
                    UseShellExecute = true
                });
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(900);
                    try
                    {
                        Process.Start(new ProcessStartInfo(issueUrl)
                        {
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Log("GitHub issue form could not be opened: " +
                            ex.Message);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Log("Support report could not be prepared: " + ex.Message);
                MessageBox.Show(
                    owner,
                    "Не удалось подготовить сообщение о проблеме.\r\n\r\n" +
                    ex.Message,
                    AppTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private static string ReadLogTail(string path, int maximumBytes)
        {
            if (!File.Exists(path))
                return "(log file is empty)";
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                bool truncated = stream.Length > maximumBytes;
                if (truncated)
                    stream.Seek(-maximumBytes, SeekOrigin.End);
                using (var reader = new StreamReader(
                    stream, Encoding.UTF8, true, 4096, false))
                {
                    if (truncated)
                        reader.ReadLine();
                    string text = reader.ReadToEnd();
                    return truncated
                        ? "(older log entries omitted)\r\n" + text
                        : text;
                }
            }
        }

        private void SetState(bool running, string text, bool ready = false)
        {
            Interlocked.Exchange(ref receiverReady, ready ? 1 : 0);
            receiverStateText = text;
            statusItem.Text = running ? "● " + text : "○ " + text;
            startStopItem.Text = running ? "Остановить приёмник" : "Запустить приёмник";
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        private void Quit()
        {
            quitting = true;
            CloseLostConnectionPlaceholder();
            monitorTimer.Stop();
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            if (sessionSwitchSubscribed)
            {
                try { SystemEvents.SessionSwitch -= OnSessionSwitch; }
                catch { }
                sessionSwitchSubscribed = false;
            }
            restartPending = false;
            restartAfterStop = false;
            if (Interlocked.CompareExchange(
                    ref restartStopInProgress, 0, 0) == 1)
            {
                restartStopDone.WaitOne(4000);
                Interlocked.Exchange(ref restartStopInProgress, 0);
                Interlocked.Exchange(ref restartStopCompleted, 0);
            }
            StopCoreInternal("application exit", true, true);
            ResetRendererControls(true);
            Log("=== AeroMirror session ended ===");
            FlushLog(1000);
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }

        private void RequestQuit()
        {
            if (form != null && !form.IsDisposed &&
                !form.ConfirmCloseForQuit())
            {
                ShowSettings();
                return;
            }
            Quit();
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
                value = "";

            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    quoted.Append('\\', backslashCount * 2 + 1);
                    quoted.Append('"');
                    backslashCount = 0;
                    continue;
                }

                if (backslashCount != 0)
                {
                    quoted.Append('\\', backslashCount);
                    backslashCount = 0;
                }
                quoted.Append(character);
            }

            quoted.Append('\\', backslashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        internal static void Log(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                "  " + RedactSensitiveText(message, "") +
                Environment.NewLine;
            bool scheduleWriter = false;
            lock (LogSync)
            {
                if (LogQueue.Count >= 10000)
                {
                    LogQueue.Dequeue();
                    if (!logOverflowReported)
                    {
                        LogQueue.Enqueue(
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                            "  WARN: log queue overflow; oldest lines were dropped." +
                            Environment.NewLine);
                        logOverflowReported = true;
                    }
                }
                LogQueue.Enqueue(line);
                if (!logWriterScheduled)
                {
                    logWriterScheduled = true;
                    LogQueueDrained.Reset();
                    scheduleWriter = true;
                }
            }
            if (scheduleWriter)
                ThreadPool.QueueUserWorkItem(delegate { FlushLogQueue(); });
        }

        private static readonly object LogSync = new object();
        private static readonly Queue<string> LogQueue =
            new Queue<string>();
        private static bool logWriterScheduled;
        private static bool logOverflowReported;
        private static readonly ManualResetEvent LogQueueDrained =
            new ManualResetEvent(true);

        private static void FlushLogQueue()
        {
            const long maxLogBytes = 5L * 1024L * 1024L;
            while (true)
            {
                var batch = new StringBuilder();
                lock (LogSync)
                {
                    int count = 0;
                    while (LogQueue.Count > 0 && count < 250)
                    {
                        batch.Append(LogQueue.Dequeue());
                        count++;
                    }
                    if (batch.Length == 0)
                    {
                        logWriterScheduled = false;
                        logOverflowReported = false;
                        LogQueueDrained.Set();
                        return;
                    }
                }

                try
                {
                    if (File.Exists(AppSettings.LogPath) &&
                        new FileInfo(AppSettings.LogPath).Length >=
                            maxLogBytes)
                    {
                        string previous = AppSettings.LogPath + ".1";
                        try
                        {
                            if (File.Exists(previous))
                                File.Delete(previous);
                            File.Move(AppSettings.LogPath, previous);
                        }
                        catch
                        {
                            // Rotation must not prevent current diagnostics
                            // from being appended to the active file.
                        }
                    }
                    File.AppendAllText(
                        AppSettings.LogPath, batch.ToString(),
                        new UTF8Encoding(false));
                }
                catch { }
            }
        }

        internal static bool FlushLog(int timeoutMilliseconds)
        {
            try { return LogQueueDrained.WaitOne(timeoutMilliseconds); }
            catch { return false; }
        }
    }
}

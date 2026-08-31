using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using ServiceController = System.ServiceProcess.ServiceController;
using ServiceControllerStatus =
    System.ServiceProcess.ServiceControllerStatus;
using ServiceStartMode = System.ServiceProcess.ServiceStartMode;

[assembly: AssemblyTitle("AeroMirror Setup")]
[assembly: AssemblyProduct("AeroMirror")]
[assembly: AssemblyCompany("AeroMirror open-source project")]
[assembly: AssemblyVersion("0.12.22.0")]
[assembly: AssemblyFileVersion("0.12.22.0")]

namespace AirPlayReceiverSetup
{
    internal static class Program
    {
        internal const string BonjourMachineConfigurationArgument =
            "/configure-bonjour-machine";

        [STAThread]
        private static void Main(string[] args)
        {
            // This is the only elevated mode. Dispatch it before touching the
            // per-user log, current directory, UI, or any user-writable path.
            if (IsExactBonjourMachineConfigurationInvocation(args))
            {
                try
                {
                    InstallerOperations.ConfigureBonjourMachineElevated();
                }
                catch
                {
                    Environment.ExitCode = 2;
                }
                return;
            }

            string originalWorkingDirectory = Environment.CurrentDirectory;
            try
            {
                bool detached = EnsureCurrentDirectoryOutsideInstallTree(
                    InstallPaths.InstallDirectory);
                SetupLog.Write(
                    "Setup process started. Executable=\"" +
                    Assembly.GetExecutingAssembly().Location +
                    "\"; CurrentDirectoryBefore=\"" +
                    originalWorkingDirectory +
                    "\"; CurrentDirectoryAfter=\"" +
                    Environment.CurrentDirectory +
                    "\"; DetachedFromInstallTree=" + detached + ".");
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "Setup could not detach its current directory from the " +
                    "installation tree: " + ex);
                MessageBox.Show(
                    "Не удалось подготовить установщик AeroMirror к запуску.\r\n\r\n" +
                    "Переместите установщик в папку «Загрузки» и повторите попытку.",
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.ExitCode = 2;
                return;
            }

            bool automaticUpdateRequested =
                HasArgument(args, "/automatic-update");
            bool updateRequested = automaticUpdateRequested ||
                HasArgument(args, "/update");
            if (!Environment.Is64BitOperatingSystem)
            {
                MessageBox.Show(
                    "AeroMirror поддерживает только 64-разрядные версии Windows 10 и 11.",
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            if (args.Length > 0 &&
                string.Equals(
                    args[0], "/verify-runtime",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetupLog.Write("Runtime verification started.");
                try
                {
                    InstallerOperations.VerifyRuntimePayload();
                    SetupLog.Write("Runtime verification completed successfully.");
                }
                catch (Exception ex)
                {
                    SetupLog.Write("Runtime verification failed: " + ex);
                    Environment.ExitCode = 2;
                }
                return;
            }
            if (args.Length > 0 &&
                string.Equals(
                    args[0], "/verify-shortcut-selection",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    InstallerOperations.VerifyShortcutSelectionLogic();
                    SetupLog.Write(
                        "Shortcut selection verification completed successfully.");
                }
                catch (Exception ex)
                {
                    SetupLog.Write(
                        "Shortcut selection verification failed: " + ex);
                    Environment.ExitCode = 2;
                }
                return;
            }
            if (args.Length > 0 &&
                string.Equals(
                    args[0], "/verify-update-lifecycle",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    InstallerOperations.VerifyUpdateLifecycleLogic();
                    SetupLog.Write(
                        "Update lifecycle verification completed successfully.");
                }
                catch (Exception ex)
                {
                    SetupLog.Write(
                        "Update lifecycle verification failed: " + ex);
                    Environment.ExitCode = 2;
                }
                return;
            }
            if (args.Length > 0 &&
                string.Equals(
                    args[0], "/verify-bonjour-recovery",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    InstallerOperations.VerifyBonjourRecoveryPolicyLogic();
                    SetupLog.Write(
                        "Bonjour recovery-policy verification completed successfully.");
                }
                catch (Exception ex)
                {
                    SetupLog.Write(
                        "Bonjour recovery-policy verification failed: " + ex);
                    Environment.ExitCode = 2;
                }
                return;
            }
            bool uninstallWorkerRequested = args.Length > 0 &&
                string.Equals(
                    args[0], "/uninstall-worker",
                    StringComparison.OrdinalIgnoreCase);
            bool silentInstallRequested = args.Length > 0 &&
                string.Equals(
                    args[0], "/install-silent",
                    StringComparison.OrdinalIgnoreCase);
            bool uninstallRequested = args.Length > 0 &&
                string.Equals(
                    args[0], "/uninstall",
                    StringComparison.OrdinalIgnoreCase);

            if (uninstallRequested)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                BeginUninstall();
                return;
            }

            bool automaticInstallRequested = false;
            if (!silentInstallRequested && !uninstallWorkerRequested)
            {
                Version installedVersion =
                    InstallerOperations.GetInstalledVersion();
                automaticInstallRequested =
                    InstallerOperations.ShouldRunAutomaticInstall(
                        updateRequested, installedVersion,
                        SetupForm.SetupVersion);
                if (!automaticInstallRequested)
                {
                    if (InstallerOperations.ShouldAbortInstallAfterLock(
                            installedVersion, SetupForm.SetupVersion))
                    {
                        if (updateRequested)
                        {
                            // Re-enter the normal transaction route. The
                            // installed tree may belong to another Setup that
                            // has not committed yet; its version and recovery
                            // launch must be revalidated under the same mutex.
                            automaticInstallRequested = true;
                        }
                        else
                        {
                            SetupLog.Write(
                                "Setup refused a downgrade because the " +
                                "installed executable is newer than this Setup.");
                            Environment.ExitCode = 4;
                            MessageBox.Show(
                                "На компьютере уже установлена более новая " +
                                    "версия AeroMirror. Установка более старой " +
                                    "версии отменена.",
                                "AeroMirror",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            return;
                        }
                    }
                    if (!automaticInstallRequested)
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new SetupForm(updateRequested));
                        return;
                    }
                }
            }

            int mutexWaitMilliseconds =
                automaticUpdateRequested || uninstallWorkerRequested
                    ? 30000
                    : 0;
            Mutex installationMutex;
            bool installationMutexAcquired;
            try
            {
                installationMutexAcquired = TryAcquireInstallationMutex(
                    GetInstallationMutexName(), mutexWaitMilliseconds,
                    out installationMutex);
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "Installation transaction lock failed: " + ex);
                Environment.ExitCode = 3;
                if (updateRequested)
                    WaitForTransactionAndRelaunchAfterSetupGateFailure(
                        automaticUpdateRequested);
                else if (!uninstallWorkerRequested)
                    ShowInstallationTransactionUnavailable();
                return;
            }
            if (!installationMutexAcquired)
            {
                SetupLog.Write(
                    "Another AeroMirror installation transaction is already " +
                    "running; this invocation will exit without changes.");
                Environment.ExitCode = 3;
                if (updateRequested)
                    WaitForTransactionAndRelaunchAfterSetupGateFailure(
                        automaticUpdateRequested);
                else if (!uninstallWorkerRequested)
                    ShowInstallationTransactionUnavailable();
                return;
            }

            try
            {
                if (automaticInstallRequested)
                {
                    Version lockedInstalledVersion =
                        InstallerOperations.GetInstalledVersion();
                    if (InstallerOperations.ShouldAbortInstallAfterLock(
                            lockedInstalledVersion,
                            SetupForm.SetupVersion))
                    {
                        SetupLog.Write(
                            "Setup aborted under the installation lock because " +
                            "a newer executable was installed while this " +
                            "invocation was waiting.");
                        Environment.ExitCode = 4;
                        string recoveryDetail;
                        TryRelaunchInstalledShellAfterFailure(
                            automaticUpdateRequested,
                            out recoveryDetail);
                        SetupLog.Write(recoveryDetail);
                        return;
                    }
                }
                ScheduleSourceDeletion(args);
                if (silentInstallRequested)
                {
                    try
                    {
                        ShortcutSelection shortcuts =
                            InstallerOperations.GetShortcutSelection(true);
                        SetupLog.Write("Silent installation started.");
                        InstallerOperations.Install(
                            shortcuts.StartMenu, shortcuts.Desktop);
                        SetupLog.Write(
                            "Silent installation completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        SetupLog.Write("Silent installation failed: " + ex);
                        Environment.ExitCode = 2;
                    }
                    return;
                }

                if (uninstallWorkerRequested)
                {
                    UninstallWorker(args);
                    return;
                }

                if (!automaticInstallRequested)
                    throw new InvalidOperationException(
                        "Setup transaction routing was not resolved.");
                RunAutomaticInstall(
                    updateRequested, automaticUpdateRequested);
            }
            finally
            {
                try { installationMutex.ReleaseMutex(); }
                catch { }
                installationMutex.Dispose();
            }
        }

        internal static string GetInstallationMutexName()
        {
            string identity = "unknown";
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                if (current != null && current.User != null)
                    identity = current.User.Value;
            }
            catch
            {
                identity = Environment.UserDomainName + "-" +
                    Environment.UserName;
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(identity));
                return "Global\\AeroMirror.Setup.Install." +
                    BitConverter.ToString(digest).Replace("-", "");
            }
        }

        internal static bool TryAcquireInstallationMutex(
            string name, int waitMilliseconds, out Mutex mutex)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Mutex name is required.", "name");
            if (waitMilliseconds < 0)
                throw new ArgumentOutOfRangeException("waitMilliseconds");
            mutex = null;
            Mutex candidate = null;
            try
            {
                candidate = new Mutex(false, name);
                bool acquired;
                try
                {
                    acquired = candidate.WaitOne(waitMilliseconds, false);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
                if (!acquired)
                {
                    candidate.Dispose();
                    return false;
                }
                mutex = candidate;
                return true;
            }
            catch
            {
                if (candidate != null)
                    candidate.Dispose();
                throw;
            }
        }

        private static void ShowInstallationTransactionUnavailable()
        {
            MessageBox.Show(
                "Другая установка или обновление AeroMirror уже выполняется. " +
                    "Дождитесь её завершения и повторите попытку.",
                "AeroMirror",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static void WaitForTransactionAndRelaunchAfterSetupGateFailure(
            bool automaticUpdateRequested)
        {
            Mutex recoveryMutex;
            bool acquired;
            try
            {
                acquired = TryAcquireInstallationMutex(
                    GetInstallationMutexName(), 300000,
                    out recoveryMutex);
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "Could not wait for the active Setup transaction before " +
                    "receiver recovery: " + ex);
                return;
            }
            if (!acquired)
            {
                SetupLog.Write(
                    "The active Setup transaction did not finish within the " +
                    "bounded receiver-recovery wait; no executable was " +
                    "started from the mutable installation tree.");
                return;
            }
            try
            {
                // Keep the transaction boundary through path resolution,
                // process creation, and the bounded early-exit check. Another
                // Setup must not replace the installed tree between the wait
                // above and this recovery launch.
                string recoveryDetail;
                TryRelaunchInstalledShellAfterFailure(
                    automaticUpdateRequested, true, out recoveryDetail);
                SetupLog.Write(recoveryDetail);
            }
            finally
            {
                try { recoveryMutex.ReleaseMutex(); }
                catch { }
                recoveryMutex.Dispose();
            }
        }

        internal static bool IsExactBonjourMachineConfigurationInvocation(
            string[] args)
        {
            return args != null &&
                args.Length == 1 &&
                string.Equals(
                    args[0], BonjourMachineConfigurationArgument,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string arg in args)
            {
                if (string.Equals(
                    arg, expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void ScheduleSourceDeletion(string[] args)
        {
            if (!HasArgument(args, "/delete-source"))
                return;
            MoveFileEx(
                Assembly.GetExecutingAssembly().Location,
                null,
                MoveFileFlags.DelayUntilReboot);
        }

        private static void RunAutomaticInstall(
            bool updateRequested, bool automaticUpdateRequested)
        {
            string action = updateRequested ? "update" : "reinstall";
            string executable;
            try
            {
                ShortcutSelection shortcuts =
                    InstallerOperations.GetShortcutSelection(true);
                SetupLog.Write(
                    "Automatic " + action + " started. Start menu shortcut=" +
                    shortcuts.StartMenu + "; Desktop shortcut=" +
                    shortcuts.Desktop + ".");
                executable = InstallerOperations.Install(
                    shortcuts.StartMenu, shortcuts.Desktop);
                SetupLog.Write(
                    "Automatic " + action + " completed successfully.");
            }
            catch (Exception ex)
            {
                SetupLog.Write("Automatic " + action + " failed: " + ex);
                Environment.ExitCode = 2;
                string recoveryDetail;
                bool recovered = TryRelaunchInstalledShellAfterFailure(
                    automaticUpdateRequested, out recoveryDetail);
                SetupLog.Write(recoveryDetail);
                MessageBox.Show(
                    "Не удалось автоматически " +
                    (updateRequested ? "обновить" : "переустановить") +
                    " AeroMirror.\r\n\r\n" + ex.Message +
                    (recovered
                        ? "\r\n\r\nУстановленная копия AeroMirror снова запущена."
                        : "\r\n\r\nНе удалось снова запустить установленную " +
                            "копию; запустите AeroMirror из меню «Пуск»."),
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(executable)
                {
                    Arguments = InstallerOperations.GetPostInstallRelaunchArguments(
                        automaticUpdateRequested),
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    UseShellExecute = true
                });
                SetupLog.Write("AeroMirror relaunched after automatic install.");
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "AeroMirror relaunch after automatic install failed: " + ex);
                Environment.ExitCode = 2;
                MessageBox.Show(
                    "AeroMirror обновлён, но не запустился автоматически.\r\n\r\n" +
                    ex.Message,
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        internal static bool TryRelaunchInstalledShellAfterFailure(
            bool automaticUpdateRequested, out string detail)
        {
            return TryRelaunchInstalledShellAfterFailure(
                automaticUpdateRequested, false, out detail);
        }

        internal static bool TryRelaunchInstalledShellAfterFailure(
            bool automaticUpdateRequested,
            bool setupTransactionBusy,
            out string detail)
        {
            string executable =
                InstallerOperations.GetInstalledExecutablePath();
            if (string.IsNullOrEmpty(executable))
            {
                detail = "No installed AeroMirror executable was available " +
                    "after the failed Setup transaction.";
                return false;
            }
            try
            {
                using (Process relaunched = Process.Start(
                    new ProcessStartInfo(executable)
                    {
                        Arguments = InstallerOperations
                            .GetPostInstallFailureRelaunchArguments(
                                automaticUpdateRequested,
                                setupTransactionBusy),
                        WorkingDirectory = Path.GetDirectoryName(executable),
                        UseShellExecute = true
                    }))
                {
                    if (relaunched == null)
                    {
                        detail = "Windows did not return the relaunched " +
                            "AeroMirror process.";
                        return false;
                    }
                    if (relaunched.WaitForExit(1500))
                    {
                        detail = "The installed AeroMirror process exited " +
                            "during the recovery confirmation window; code " +
                            relaunched.ExitCode + ".";
                        return false;
                    }
                }
                detail = "The installed AeroMirror shell was relaunched after " +
                    "the failed Setup transaction.";
                return true;
            }
            catch (Exception ex)
            {
                detail = "Installed AeroMirror relaunch after failed Setup " +
                    "also failed: " + ex;
                return false;
            }
        }

        private static void BeginUninstall()
        {
            DialogResult answer = MessageBox.Show(
                "Удалить AeroMirror с этого компьютера?\r\n\r\n" +
                "Настройки и журнал пользователя будут сохранены.",
                "Удаление AeroMirror",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;

            string temporary = Path.Combine(
                Path.GetTempPath(),
                "AeroMirror-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                File.Copy(Assembly.GetExecutingAssembly().Location, temporary, true);
                var start = new ProcessStartInfo();
                start.FileName = temporary;
                start.Arguments = "/uninstall-worker " +
                    Quote(InstallPaths.InstallDirectory) + " " +
                    Process.GetCurrentProcess().Id;
                start.UseShellExecute = false;
                start.WorkingDirectory = Path.GetDirectoryName(temporary);
                if (InstallerOperations.IsPathWithinDirectory(
                    start.WorkingDirectory,
                    InstallPaths.InstallDirectory))
                {
                    throw new InvalidOperationException(
                        "The uninstall worker directory is inside the installation tree.");
                }
                Process.Start(start);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось запустить удаление.\r\n\r\n" + ex.Message,
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void UninstallWorker(string[] args)
        {
            if (args.Length < 3)
                return;

            string installDirectory = Path.GetFullPath(args[1]);
            string expected = Path.GetFullPath(InstallPaths.InstallDirectory);
            if (!string.Equals(
                installDirectory.TrimEnd('\\'),
                expected.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase))
                return;

            int parentPid;
            if (int.TryParse(args[2], out parentPid))
            {
                try
                {
                    using (Process parent = Process.GetProcessById(parentPid))
                        parent.WaitForExit(10000);
                }
                catch { }
            }

            try
            {
                InstallerOperations.StopInstalledProcesses(installDirectory);
                InstallerOperations.RemoveShortcuts();
                InstallerOperations.RemoveRegistryEntries(installDirectory);
                InstallerOperations.RemoveRuntimeCache();
                if (Directory.Exists(installDirectory))
                    Directory.Delete(installDirectory, true);
                MessageBox.Show(
                    "AeroMirror удалён. Пользовательские настройки сохранены.",
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось полностью удалить приложение.\r\n\r\n" + ex.Message,
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                MoveFileEx(
                    Assembly.GetExecutingAssembly().Location,
                    null,
                    MoveFileFlags.DelayUntilReboot);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        internal static bool EnsureCurrentDirectoryOutsideInstallTree(
            string installDirectory)
        {
            string current = Environment.CurrentDirectory;
            if (!InstallerOperations.IsPathWithinDirectory(
                current, installDirectory))
                return false;

            string normalizedInstall = Path.GetFullPath(installDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string safeDirectory = Path.GetDirectoryName(normalizedInstall);
            if (string.IsNullOrEmpty(safeDirectory) ||
                InstallerOperations.IsPathWithinDirectory(
                    safeDirectory, installDirectory))
            {
                safeDirectory = Path.GetFullPath(Path.GetTempPath());
            }
            if (InstallerOperations.IsPathWithinDirectory(
                safeDirectory, installDirectory))
            {
                throw new InvalidOperationException(
                    "No working directory outside the installation tree is available.");
            }

            Directory.CreateDirectory(safeDirectory);
            Environment.CurrentDirectory = safeDirectory;
            return true;
        }

        [Flags]
        private enum MoveFileFlags
        {
            DelayUntilReboot = 0x4
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(
            string existingFile,
            string newFile,
            MoveFileFlags flags);
    }

    internal static class SetupLog
    {
        private static readonly object Sync = new object();

        internal static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AirPlayReceiverMvp",
            "setup.log");

        internal static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    if (File.Exists(Path) &&
                        new FileInfo(Path).Length > 1024 * 1024)
                    {
                        string previous = Path + ".1";
                        if (File.Exists(previous))
                            File.Delete(previous);
                        File.Move(Path, previous);
                    }
                    File.AppendAllText(
                        Path,
                        DateTime.UtcNow.ToString("o") + "  " + message +
                        Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class SetupForm : Form
    {
        internal static readonly Version SetupVersion = new Version(0, 12, 22);
        private readonly CheckBox startMenu;
        private readonly CheckBox desktop;
        private readonly CheckBox launch;
        private readonly Button install;
        private readonly ProgressBar progress;
        private readonly Label state;
        private readonly Version installedVersion;
        private readonly int installedVersionComparison;
        private readonly bool updateRequested;
        private int installInProgress;

        public SetupForm(bool updateRequested)
        {
            this.updateRequested = updateRequested;
            installedVersion = InstallerOperations.GetInstalledVersion();
            installedVersionComparison = installedVersion == null
                ? 0
                : InstallerOperations.ComparePublicVersions(
                    installedVersion, SetupVersion);
            Text = "Установка AeroMirror";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 420);
            BackColor = Color.FromArgb(250, 250, 250);
            Font = new Font("Segoe UI", 9F);

            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 102;
            header.BackColor = Color.White;
            Controls.Add(header);

            var title = new Label();
            title.Text = "AeroMirror";
            title.Font = new Font("Segoe UI Semibold", 20F);
            title.AutoSize = true;
            title.Location = new Point(28, 20);
            header.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = installedVersion == null
                ? updateRequested
                ? "Обновление AeroMirror · текущие настройки сохранятся"
                : "Установка для текущего пользователя · без прав администратора"
                : installedVersionComparison < 0
                ? "Обновление " + installedVersion.ToString(3) +
                    " → " + SetupVersion.ToString(3) +
                    " · настройки сохранятся"
                : installedVersionComparison == 0
                ? "Переустановка версии " + SetupVersion.ToString(3) +
                    " · настройки сохранятся"
                : "Установлена более новая версия " +
                    installedVersion.ToString(3);
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.DimGray;
            subtitle.Location = new Point(31, 62);
            header.Controls.Add(subtitle);

            var pathTitle = MakeLabel("Приложение будет установлено в:", 28, 126);
            Controls.Add(pathTitle);

            var path = new TextBox();
            path.Text = InstallPaths.InstallDirectory;
            path.ReadOnly = true;
            path.BackColor = Color.White;
            path.Location = new Point(28, 150);
            path.Size = new Size(504, 27);
            Controls.Add(path);

            var runtimeNotice = MakeLabel(
                "Review-установщик скачает неизменённый runtime uxplay-windows " +
                "(около 110 МБ) с GitHub и проверит его SHA-256.",
                28, 184);
            runtimeNotice.AutoSize = false;
            runtimeNotice.Size = new Size(504, 34);
            runtimeNotice.ForeColor = Color.DimGray;
            Controls.Add(runtimeNotice);

            bool preserveShortcutChoice =
                updateRequested || installedVersion != null;
            ShortcutSelection shortcutSelection =
                InstallerOperations.GetShortcutSelection(
                    preserveShortcutChoice);
            startMenu = MakeCheckBox(
                "Добавить ярлык в меню «Пуск»", 28, 224,
                shortcutSelection.StartMenu);
            desktop = MakeCheckBox(
                "Добавить ярлык на рабочий стол", 28, 255,
                shortcutSelection.Desktop);
            launch = MakeCheckBox("Запустить AeroMirror после установки", 28, 286, true);
            Controls.Add(startMenu);
            Controls.Add(desktop);
            Controls.Add(launch);

            state = MakeLabel("", 28, 326);
            state.ForeColor = Color.FromArgb(42, 122, 74);
            Controls.Add(state);

            progress = new ProgressBar();
            progress.Location = new Point(28, 362);
            progress.Size = new Size(340, 25);
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 25;
            progress.Visible = false;
            Controls.Add(progress);

            install = new Button();
            install.Text = installedVersion == null
                ? updateRequested ? "Обновить" : "Установить"
                : installedVersionComparison < 0
                ? "Обновить"
                : "Переустановить";
            install.Size = new Size(150, 40);
            install.Location = new Point(382, 354);
            install.BackColor = Color.FromArgb(0, 95, 184);
            install.ForeColor = Color.White;
            install.FlatStyle = FlatStyle.Flat;
            install.FlatAppearance.BorderSize = 0;
            install.Click += OnInstall;
            Controls.Add(install);
        }

        private void OnInstall(object sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(
                    ref installInProgress, 1, 0) != 0)
                return;

            install.Enabled = false;
            startMenu.Enabled = false;
            desktop.Enabled = false;
            launch.Enabled = false;
            progress.Visible = true;
            ControlBox = false;
            state.Text = "Скачиваем и устанавливаем проверенный runtime…";

            bool createStartMenu = startMenu.Checked;
            bool createDesktop = desktop.Checked;
            bool launchAfter = launch.Checked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                SetupLog.Write(
                    "Interactive installation started. Update requested=" +
                    updateRequested + "; Start menu shortcut=" +
                    createStartMenu + "; Desktop shortcut=" +
                    createDesktop + ".");
                try
                {
                    Mutex transactionMutex;
                    if (!Program.TryAcquireInstallationMutex(
                            Program.GetInstallationMutexName(), 0,
                            out transactionMutex))
                    {
                        throw new InvalidOperationException(
                            "Другая установка или обновление AeroMirror уже " +
                            "выполняется. Дождитесь её завершения и повторите " +
                            "попытку.");
                    }
                    string executable;
                    try
                    {
                        Version lockedInstalledVersion =
                            InstallerOperations.GetInstalledVersion();
                        if (InstallerOperations.ShouldAbortInstallAfterLock(
                                lockedInstalledVersion, SetupVersion))
                        {
                            throw new InvalidOperationException(
                                "Во время ожидания была установлена более " +
                                "новая версия AeroMirror. Установка старой " +
                                "версии отменена.");
                        }
                        executable = InstallerOperations.Install(
                            createStartMenu, createDesktop);
                    }
                    finally
                    {
                        try { transactionMutex.ReleaseMutex(); }
                        catch { }
                        transactionMutex.Dispose();
                    }
                    SetupLog.Write(
                        "Interactive installation completed successfully.");
                    BeginInvoke((MethodInvoker)delegate
                    {
                        progress.Visible = false;
                        if (launchAfter)
                        {
                            // Remove the setup window before the application
                            // appears, so the two windows never cover each other.
                            Hide();
                            try
                            {
                                Process.Start(new ProcessStartInfo(executable)
                                {
                                    UseShellExecute = true
                                });
                                Interlocked.Exchange(ref installInProgress, 0);
                                ControlBox = true;
                                Close();
                            }
                            catch (Exception launchError)
                            {
                                SetupLog.Write(
                                    "Launching AeroMirror after installation failed: " +
                                    launchError);
                                Show();
                                Interlocked.Exchange(ref installInProgress, 0);
                                ControlBox = true;
                                state.Text = "Приложение установлено, но не запущено.";
                                state.ForeColor = Color.FromArgb(160, 45, 45);
                                MessageBox.Show(
                                    this,
                                    launchError.Message,
                                    "AeroMirror",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                            return;
                        }

                        state.Text = "Готово. Приложение установлено.";
                        Interlocked.Exchange(ref installInProgress, 0);
                        ControlBox = true;
                        install.Text = "Закрыть";
                        install.Enabled = true;
                        install.Click -= OnInstall;
                        install.Click += delegate { Close(); };
                    });
                }
                catch (Exception ex)
                {
                    SetupLog.Write("Interactive installation failed: " + ex);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        progress.Visible = false;
                        Interlocked.Exchange(ref installInProgress, 0);
                        ControlBox = true;
                        state.Text = "Установка не завершена.";
                        state.ForeColor = Color.FromArgb(160, 45, 45);
                        install.Enabled = true;
                        startMenu.Enabled = true;
                        desktop.Enabled = true;
                        launch.Enabled = true;
                        MessageBox.Show(
                            this,
                            ex.Message,
                            "AeroMirror",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing &&
                Interlocked.CompareExchange(
                    ref installInProgress, 0, 0) == 1)
            {
                e.Cancel = true;
                state.Text = "Дождитесь завершения установки…";
                return;
            }
            base.OnFormClosing(e);
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            return label;
        }

        private static CheckBox MakeCheckBox(
            string text, int x, int y, bool value)
        {
            var check = new CheckBox();
            check.Text = text;
            check.AutoSize = true;
            check.Location = new Point(x, y);
            check.Checked = value;
            return check;
        }
    }

    internal static class InstallPaths
    {
        internal static readonly string InstallDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "AirPlayReceiverMvp");

        internal static readonly string StartMenuShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "AeroMirror.lnk");

        internal static readonly string DesktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "AeroMirror.lnk");

        internal static readonly string LegacyStartMenuShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "AirPlay Receiver.lnk");

        internal static readonly string LegacyDesktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "AirPlay Receiver.lnk");
    }

    internal sealed class ShortcutSelection
    {
        internal readonly bool StartMenu;
        internal readonly bool Desktop;

        internal ShortcutSelection(bool startMenu, bool desktop)
        {
            StartMenu = startMenu;
            Desktop = desktop;
        }
    }

    internal static class InstallerOperations
    {
        private const string PayloadResource = "AirPlayReceiverPayload";
        private const string UninstallerResource = "AirPlayReceiverUninstaller";
        private const string ProvenanceResource = "AeroMirrorSourceProvenance";
        private const string RuntimeUrl =
            "https://github.com/leapbtw/uxplay-windows/releases/download/" +
            "2.0.0.1736/uxplay-windows.zip";
        private const string RuntimeSha256 =
            "9D3A51C15FC9DB857351195E7EB7BBB21700D9AE25D936A54BCF8536B62CCA18";
        private const string RequiredQtBuildVersion = "6.10.1";
        private const string RequiredRuntimeRelease = "2.0.0.1736";
        private const string RequiredCoreRuntimeCompatibility =
            "uxplay-windows-2.0.0.1736";
        private static readonly string RuntimeCacheDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "AirPlayReceiverMvp",
            "cache",
            "runtime");
        private static readonly string RuntimeCachePath = Path.Combine(
            RuntimeCacheDirectory,
            "sha256-" + RuntimeSha256.ToLowerInvariant() +
            "-uxplay-windows.zip");
        private const string UninstallKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AirPlayReceiverMvp";
        private const string RunKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const int ProcessStopTimeoutMilliseconds = 8000;
        private const int GracefulProcessStopMilliseconds = 1500;
        private const int InstallDirectoryMoveTimeoutMilliseconds = 10000;
        private const int InstallDirectoryMoveRetryDelayMilliseconds = 250;
        private const int BonjourElevatedSelfTimeoutMilliseconds = 180000;
        private const int BonjourHelperTimeoutMilliseconds = 210000;
        private const int BonjourServiceWaitMilliseconds = 20000;
        private const int ErrorCancelled = 1223;
        private const uint ScManagerConnect = 0x0001;
        private const uint ServiceQueryConfig = 0x0001;
        private const uint ServiceChangeConfig = 0x0002;
        private const uint ServiceStart = 0x0010;
        private const uint ServiceNoChange = 0xffffffff;
        private const uint ServiceAutoStart = 2;
        private const uint ServiceConfigFailureActions = 2;
        private const uint ServiceConfigFailureActionsFlag = 4;
        private const int ScActionNone = 0;
        private const int ScActionRestart = 1;
        private const int ErrorServiceAlreadyRunning = 1056;
        private const uint BonjourFailureResetSeconds = 86400;
        private const string BonjourFirewallRuleName =
            "AeroMirror Bonjour mDNS (Private)";

        private static readonly uint[] BonjourRestartDelaysMilliseconds =
        {
            5000,
            30000,
            120000
        };

        private static readonly string[] BonjourServiceNames =
        {
            "Bonjour Service",
            "mDNSResponder"
        };

        private sealed class BonjourWatchdogState
        {
            // 0 = running, 1 = completed, 2 = watchdog owns termination.
            internal int CompletionState;
        }

        private sealed class BonjourFirewallRuleSnapshot
        {
            internal string Name;
            internal bool Enabled;
            internal int Direction;
            internal int Action;
            internal int Protocol;
            internal int Profiles;
            internal string ApplicationName;
            internal string LocalPorts;
            internal string RemoteAddresses;
            internal bool EdgeTraversal;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceFailureActionsNative
        {
            internal uint ResetPeriod;
            internal IntPtr RebootMessage;
            internal IntPtr Command;
            internal uint ActionCount;
            internal IntPtr Actions;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceActionNative
        {
            internal int Type;
            internal uint Delay;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceFailureActionsFlagNative
        {
            [MarshalAs(UnmanagedType.Bool)]
            internal bool Enabled;
        }

        private sealed class RegistryValueSnapshot
        {
            internal string Name;
            internal bool Exists;
            internal object Value;
            internal RegistryValueKind Kind;
        }

        private sealed class ShortcutSnapshot
        {
            internal string Path;
            internal byte[] Data;
        }

        private sealed class InstallationMetadataSnapshot
        {
            private readonly List<ShortcutSnapshot> shortcuts =
                new List<ShortcutSnapshot>();
            private readonly List<RegistryValueSnapshot> uninstallValues =
                new List<RegistryValueSnapshot>();
            private readonly List<RegistryValueSnapshot> runValues =
                new List<RegistryValueSnapshot>();
            private bool uninstallKeyExisted;

            internal static InstallationMetadataSnapshot Capture()
            {
                var snapshot = new InstallationMetadataSnapshot();
                string[] shortcutPaths =
                {
                    InstallPaths.StartMenuShortcut,
                    InstallPaths.DesktopShortcut,
                    InstallPaths.LegacyStartMenuShortcut,
                    InstallPaths.LegacyDesktopShortcut
                };
                foreach (string path in shortcutPaths)
                {
                    snapshot.shortcuts.Add(new ShortcutSnapshot
                    {
                        Path = path,
                        Data = File.Exists(path) ? File.ReadAllBytes(path) : null
                    });
                }

                using (RegistryKey key =
                    Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    snapshot.uninstallKeyExisted = key != null;
                    if (key != null)
                    {
                        foreach (string name in key.GetValueNames())
                        {
                            snapshot.uninstallValues.Add(
                                CaptureRegistryValue(key, name));
                        }
                    }
                }

                using (RegistryKey run =
                    Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    snapshot.runValues.Add(
                        CaptureRegistryValue(run, "AeroMirror"));
                    snapshot.runValues.Add(
                        CaptureRegistryValue(run, "AirPlayReceiverMvp"));
                }
                return snapshot;
            }

            internal void Restore()
            {
                Exception firstError = null;
                try
                {
                    RemoveShortcuts();
                    foreach (ShortcutSnapshot shortcut in shortcuts)
                    {
                        if (shortcut.Data == null)
                            continue;
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(shortcut.Path));
                        File.WriteAllBytes(shortcut.Path, shortcut.Data);
                    }
                }
                catch (Exception ex)
                {
                    firstError = ex;
                }

                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                    if (uninstallKeyExisted)
                    {
                        using (RegistryKey key =
                            Registry.CurrentUser.CreateSubKey(UninstallKey))
                        {
                            foreach (RegistryValueSnapshot value in uninstallValues)
                                RestoreRegistryValue(key, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                try
                {
                    bool needsRunKey = false;
                    foreach (RegistryValueSnapshot value in runValues)
                    {
                        if (value.Exists)
                        {
                            needsRunKey = true;
                            break;
                        }
                    }
                    using (RegistryKey run = needsRunKey
                        ? Registry.CurrentUser.CreateSubKey(RunKey)
                        : Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (run != null)
                        {
                            foreach (RegistryValueSnapshot value in runValues)
                            {
                                if (value.Exists)
                                    RestoreRegistryValue(run, value);
                                else
                                    run.DeleteValue(value.Name, false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                if (firstError != null)
                    throw new InvalidOperationException(
                        "Не удалось полностью восстановить метаданные предыдущей установки.",
                        firstError);
            }

            private static RegistryValueSnapshot CaptureRegistryValue(
                RegistryKey key, string name)
            {
                if (key == null ||
                    Array.IndexOf(key.GetValueNames(), name) < 0)
                {
                    return new RegistryValueSnapshot
                    {
                        Name = name,
                        Exists = false
                    };
                }
                return new RegistryValueSnapshot
                {
                    Name = name,
                    Exists = true,
                    Value = key.GetValue(
                        name, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames),
                    Kind = key.GetValueKind(name)
                };
            }

            private static void RestoreRegistryValue(
                RegistryKey key, RegistryValueSnapshot value)
            {
                key.SetValue(value.Name, value.Value, value.Kind);
            }
        }

        internal static Version GetInstalledVersion()
        {
            Version registryVersion = null;
            try
            {
                using (RegistryKey key =
                    Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string value = key == null
                        ? null : key.GetValue("DisplayVersion") as string;
                    Version version;
                    if (TryParseInstalledPublicVersion(value, out version))
                        registryVersion = version;
                }
            }
            catch { }

            Version executableVersion = null;
            bool executablePresent = false;
            string[] executableNames =
            {
                "AeroMirror.exe",
                "AirPlayReceiverMvp.exe"
            };
            foreach (string executableName in executableNames)
            {
                string executable = Path.Combine(
                    InstallPaths.InstallDirectory, executableName);
                if (!File.Exists(executable))
                    continue;

                // The first existing name is authoritative. In particular,
                // legacy metadata must not override a present primary binary,
                // and unreadable/invalid primary metadata must force the
                // repair path instead of trusting stale registry data.
                executablePresent = true;
                try
                {
                    string value = FileVersionInfo.GetVersionInfo(
                        executable).FileVersion;
                    Version version;
                    if (TryParseInstalledPublicVersion(value, out version))
                        executableVersion = version;
                }
                catch { }
                break;
            }
            return ResolveInstalledVersion(
                registryVersion, executableVersion, executablePresent);
        }

        internal static string GetInstalledExecutablePath()
        {
            return ResolveInstalledExecutablePath(
                InstallPaths.InstallDirectory);
        }

        internal static string ResolveInstalledExecutablePath(
            string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                return null;
            string root;
            try { root = Path.GetFullPath(installDirectory); }
            catch { return null; }
            string[] executableNames =
            {
                "AeroMirror.exe",
                "AirPlayReceiverMvp.exe"
            };
            foreach (string executableName in executableNames)
            {
                string candidate = Path.Combine(root, executableName);
                if (IsPathWithinDirectory(candidate, root) &&
                    File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        internal static Version ResolveInstalledVersion(
            Version registryVersion, Version executableVersion)
        {
            // The executable is the installed product. Registry metadata is
            // only a fallback for an incomplete/missing application tree.
            return executableVersion ?? registryVersion;
        }

        internal static Version ResolveInstalledVersion(
            Version registryVersion,
            Version executableVersion,
            bool executablePresent)
        {
            return executablePresent
                ? executableVersion
                : registryVersion;
        }

        internal static bool TryParseInstalledPublicVersion(
            string value, out Version version)
        {
            Version parsed;
            if (!Version.TryParse(value, out parsed) || parsed.Build < 0)
            {
                version = null;
                return false;
            }
            version = new Version(parsed.Major, parsed.Minor, parsed.Build);
            return true;
        }

        internal static ShortcutSelection GetShortcutSelection(
            bool preserveExistingChoice)
        {
            return ResolveShortcutSelection(
                preserveExistingChoice,
                File.Exists(InstallPaths.StartMenuShortcut),
                File.Exists(InstallPaths.LegacyStartMenuShortcut),
                File.Exists(InstallPaths.DesktopShortcut),
                File.Exists(InstallPaths.LegacyDesktopShortcut));
        }

        internal static ShortcutSelection ResolveShortcutSelection(
            bool preserveExistingChoice,
            bool hasStartMenu,
            bool hasLegacyStartMenu,
            bool hasDesktop,
            bool hasLegacyDesktop)
        {
            if (!preserveExistingChoice)
                return new ShortcutSelection(true, false);
            return new ShortcutSelection(
                hasStartMenu || hasLegacyStartMenu,
                hasDesktop || hasLegacyDesktop);
        }

        internal static bool ShouldRunAutomaticInstall(
            bool updateRequested,
            Version installedVersion,
            Version setupVersion)
        {
            if (setupVersion == null)
                throw new ArgumentNullException("setupVersion");
            if (ShouldAbortInstallAfterLock(
                    installedVersion, setupVersion))
                return false;
            return updateRequested || installedVersion != null;
        }

        internal static bool ShouldAbortInstallAfterLock(
            Version installedVersion, Version setupVersion)
        {
            if (setupVersion == null)
                throw new ArgumentNullException("setupVersion");
            return installedVersion != null &&
                ComparePublicVersions(installedVersion, setupVersion) > 0;
        }

        internal static int ComparePublicVersions(
            Version left, Version right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            if (left.Build < 0 || right.Build < 0)
                throw new ArgumentException(
                    "Public versions must contain major, minor, and patch components.");

            int comparison = left.Major.CompareTo(right.Major);
            if (comparison != 0)
                return comparison;
            comparison = left.Minor.CompareTo(right.Minor);
            if (comparison != 0)
                return comparison;
            return left.Build.CompareTo(right.Build);
        }

        internal static string GetPostInstallRelaunchArguments(
            bool automaticUpdateRequested)
        {
            return automaticUpdateRequested ? "--startup" : "";
        }

        internal static string GetPostInstallFailureRelaunchArguments(
            bool automaticUpdateRequested)
        {
            return GetPostInstallFailureRelaunchArguments(
                automaticUpdateRequested, false);
        }

        internal static string GetPostInstallFailureRelaunchArguments(
            bool automaticUpdateRequested,
            bool setupTransactionBusy)
        {
            return automaticUpdateRequested
                ? setupTransactionBusy
                    ? "--startup --update-busy-recovery"
                    : "--startup --update-recovery"
                : "";
        }

        internal static void VerifyShortcutSelectionLogic()
        {
            AssertShortcutSelection(
                ResolveShortcutSelection(
                    false, false, false, false, false),
                true, false, "fresh install defaults");
            AssertShortcutSelection(
                ResolveShortcutSelection(
                    true, true, false, false, false),
                true, false, "Start menu-only update");
            AssertShortcutSelection(
                ResolveShortcutSelection(
                    true, false, false, true, false),
                false, true, "Desktop-only update");
            AssertShortcutSelection(
                ResolveShortcutSelection(
                    true, false, true, false, true),
                true, true, "legacy shortcut update");
            AssertShortcutSelection(
                ResolveShortcutSelection(
                    true, false, false, false, false),
                false, false, "update without shortcuts");
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(false, null, SetupForm.SetupVersion),
                false, "fresh manual install");
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(true, null, SetupForm.SetupVersion),
                true, "explicit application update");
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(
                    false, new Version(0, 12, 15), SetupForm.SetupVersion),
                true, "manual upgrade over an installed version");
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(
                    false, SetupForm.SetupVersion, SetupForm.SetupVersion),
                true, "same-version reinstall");
            AssertPublicVersionComparison(
                new Version(
                    SetupForm.SetupVersion.Major,
                    SetupForm.SetupVersion.Minor,
                    SetupForm.SetupVersion.Build,
                    0),
                SetupForm.SetupVersion,
                0,
                "four-part PE version equals its three-part public version");
            Version normalizedInstalledVersion;
            if (!TryParseInstalledPublicVersion(
                    SetupForm.SetupVersion.ToString(3) + ".65535",
                    out normalizedInstalledVersion) ||
                ComparePublicVersions(
                    normalizedInstalledVersion, SetupForm.SetupVersion) != 0 ||
                TryParseInstalledPublicVersion(
                    SetupForm.SetupVersion.ToString(2),
                    out normalizedInstalledVersion))
            {
                throw new InvalidOperationException(
                    "Installed versions were not normalized to three public components.");
            }
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(
                    false,
                    new Version(
                        SetupForm.SetupVersion.Major,
                        SetupForm.SetupVersion.Minor,
                        SetupForm.SetupVersion.Build,
                        65535),
                    SetupForm.SetupVersion),
                true, "PE revision does not turn a reinstall into a downgrade");
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(
                    true, new Version(
                        SetupForm.SetupVersion.Major,
                        SetupForm.SetupVersion.Minor,
                        SetupForm.SetupVersion.Build + 1),
                    SetupForm.SetupVersion),
                false, "automatic downgrade prevention");
            if (!ShouldRunAutomaticInstall(
                    true, new Version(0, 12, 20),
                    SetupForm.SetupVersion) ||
                !ShouldAbortInstallAfterLock(
                    new Version(0, 12, 23),
                    SetupForm.SetupVersion))
            {
                throw new InvalidOperationException(
                    "An update that became a downgrade while waiting for the " +
                    "installation lock was not rejected under that lock.");
            }
            Version resolvedInstalledVersion = ResolveInstalledVersion(
                new Version(0, 12, 20),
                new Version(0, 12, 22));
            if (resolvedInstalledVersion == null ||
                ComparePublicVersions(
                    resolvedInstalledVersion,
                    new Version(0, 12, 22)) != 0)
            {
                throw new InvalidOperationException(
                    "A stale lower uninstall-registry version must not " +
                    "override the newer installed executable version.");
            }
            resolvedInstalledVersion = ResolveInstalledVersion(
                new Version(0, 12, 23),
                new Version(0, 12, 20));
            if (resolvedInstalledVersion == null ||
                ComparePublicVersions(
                    resolvedInstalledVersion,
                    new Version(0, 12, 20)) != 0)
            {
                throw new InvalidOperationException(
                    "A stale higher uninstall-registry version must not " +
                    "override the authoritative installed executable version.");
            }
            resolvedInstalledVersion = ResolveInstalledVersion(
                new Version(0, 12, 23), null, true);
            if (resolvedInstalledVersion != null)
            {
                throw new InvalidOperationException(
                    "An unreadable or invalid primary executable must enter " +
                    "the repair path instead of trusting stale registry data.");
            }
            if (GetPostInstallRelaunchArguments(true) != "--startup" ||
                GetPostInstallRelaunchArguments(false) != "" ||
                GetPostInstallFailureRelaunchArguments(true) !=
                    "--startup --update-recovery" ||
                GetPostInstallFailureRelaunchArguments(false) != "" ||
                GetPostInstallFailureRelaunchArguments(true, true) !=
                    "--startup --update-busy-recovery" ||
                GetPostInstallFailureRelaunchArguments(false, true) != "")
            {
                throw new InvalidOperationException(
                    "Automatic update success and failure relaunch arguments " +
                    "are not isolated correctly.");
            }
        }

        internal static void VerifyUpdateLifecycleLogic()
        {
            VerifyInstallationMutexLogic();
            string verificationRoot = Path.Combine(
                Path.GetTempPath(),
                "AeroMirror-update-lifecycle-check-" +
                Guid.NewGuid().ToString("N"));
            string installDirectory = Path.Combine(
                verificationRoot, "installed");
            string nestedDirectory = Path.Combine(
                installDirectory, "core", "plugins");
            string backupDirectory = Path.Combine(
                verificationRoot, "installed.backup");
            string originalWorkingDirectory = Environment.CurrentDirectory;

            try
            {
                Directory.CreateDirectory(nestedDirectory);
                File.WriteAllText(
                    Path.Combine(installDirectory, "AeroMirror.exe"),
                    "update lifecycle verifier",
                    new UTF8Encoding(false));
                string resolvedExecutable = ResolveInstalledExecutablePath(
                    installDirectory);
                if (!string.Equals(
                        resolvedExecutable,
                        Path.Combine(installDirectory, "AeroMirror.exe"),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Failed-Setup recovery did not resolve the exact " +
                        "installed AeroMirror shell.");
                }
                Environment.CurrentDirectory = nestedDirectory;

                if (!Program.EnsureCurrentDirectoryOutsideInstallTree(
                    installDirectory))
                {
                    throw new InvalidOperationException(
                        "Current-directory detachment was not triggered.");
                }
                if (IsPathWithinDirectory(
                    Environment.CurrentDirectory, installDirectory))
                {
                    throw new InvalidOperationException(
                        "Current directory still points inside the installation tree.");
                }

                Directory.Move(installDirectory, backupDirectory);
                if (!Directory.Exists(backupDirectory) ||
                    Directory.Exists(installDirectory))
                {
                    throw new InvalidOperationException(
                        "The detached installation directory could not be moved.");
                }

                string sibling = installDirectory + "-old";
                if (!IsPathWithinDirectory(
                    Path.Combine(backupDirectory, "AeroMirror.exe"),
                    backupDirectory) ||
                    IsPathWithinDirectory(
                        Path.Combine(sibling, "AeroMirror.exe"),
                        installDirectory))
                {
                    throw new InvalidOperationException(
                        "Installation-tree path matching is not boundary-safe.");
                }

                if (CalculateWaitMilliseconds(0, 8000, 1500) != 1500 ||
                    CalculateWaitMilliseconds(7900, 8000, 1500) != 100 ||
                    CalculateWaitMilliseconds(8000, 8000, 1500) != 0)
                {
                    throw new InvalidOperationException(
                        "The shared process-stop deadline calculation is invalid.");
                }

                int attempts = 0;
                int recoverySweeps = 0;
                ExecuteWithBoundedIoRetry(
                    delegate
                    {
                        attempts++;
                        if (attempts < 3)
                            throw new IOException("Synthetic transient lock.");
                    },
                    1000,
                    1,
                    "Synthetic update move verifier",
                    "Synthetic update move verifier timed out.",
                    delegate { recoverySweeps++; });
                if (attempts != 3 || recoverySweeps != 1)
                {
                    throw new InvalidOperationException(
                        "The update move retry or recovery sweep count is invalid.");
                }
            }
            finally
            {
                try
                {
                    Environment.CurrentDirectory =
                        Directory.Exists(originalWorkingDirectory)
                        ? originalWorkingDirectory
                        : Path.GetFullPath(Path.GetTempPath());
                }
                catch
                {
                    Environment.CurrentDirectory =
                        Path.GetFullPath(Path.GetTempPath());
                }
                try
                {
                    if (Directory.Exists(verificationRoot))
                        Directory.Delete(verificationRoot, true);
                }
                catch { }
            }
        }

        private static void VerifyInstallationMutexLogic()
        {
            string mutexName = "Local\\AeroMirror.Setup.Install.Test." +
                Guid.NewGuid().ToString("N");
            using (var holderReady = new ManualResetEvent(false))
            using (var releaseHolder = new ManualResetEvent(false))
            {
                Exception holderFailure = null;
                var holder = new Thread(delegate()
                {
                    Mutex held = null;
                    try
                    {
                        if (!Program.TryAcquireInstallationMutex(
                                mutexName, 0, out held))
                        {
                            throw new InvalidOperationException(
                                "The first install mutex owner was rejected.");
                        }
                        holderReady.Set();
                        releaseHolder.WaitOne(5000);
                    }
                    catch (Exception ex)
                    {
                        holderFailure = ex;
                        holderReady.Set();
                    }
                    finally
                    {
                        if (held != null)
                        {
                            try { held.ReleaseMutex(); }
                            catch { }
                            held.Dispose();
                        }
                    }
                });
                holder.IsBackground = true;
                holder.Start();
                try
                {
                    if (!holderReady.WaitOne(3000))
                    {
                        throw new InvalidOperationException(
                            "The install mutex owner did not become ready.");
                    }
                    Mutex contender;
                    bool acquired = Program.TryAcquireInstallationMutex(
                        mutexName, 0, out contender);
                    if (acquired)
                    {
                        try { contender.ReleaseMutex(); }
                        catch { }
                        contender.Dispose();
                        throw new InvalidOperationException(
                            "A concurrent Setup acquired the install mutex.");
                    }
                }
                finally
                {
                    releaseHolder.Set();
                    holder.Join(5000);
                }
                if (holderFailure != null)
                {
                    throw new InvalidOperationException(
                        "The install mutex holder failed.", holderFailure);
                }

                Mutex afterRelease;
                if (!Program.TryAcquireInstallationMutex(
                        mutexName, 0, out afterRelease))
                {
                    throw new InvalidOperationException(
                        "The install mutex was not released after completion.");
                }
                try { afterRelease.ReleaseMutex(); }
                finally { afterRelease.Dispose(); }
            }
        }

        private static void AssertShortcutSelection(
            ShortcutSelection actual,
            bool expectedStartMenu,
            bool expectedDesktop,
            string scenario)
        {
            if (actual.StartMenu != expectedStartMenu ||
                actual.Desktop != expectedDesktop)
            {
                throw new InvalidOperationException(
                    "Shortcut selection check failed for " + scenario + ".");
            }
        }

        private static void AssertAutomaticInstall(
            bool actual, bool expected, string scenario)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    "Automatic-install policy failed for " + scenario +
                    ": got " + actual + ", expected " + expected + ".");
            }
        }

        private static void AssertPublicVersionComparison(
            Version left,
            Version right,
            int expectedSign,
            string scenario)
        {
            int actual = Math.Sign(ComparePublicVersions(left, right));
            if (actual != Math.Sign(expectedSign))
            {
                throw new InvalidOperationException(
                    "Public-version comparison verification failed for " +
                    scenario + ".");
            }
        }

        internal static void VerifyRuntimePayload()
        {
            string staging = Path.Combine(
                Path.GetTempPath(),
                "AeroMirror-runtime-check-" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(staging, "payload.zip");
            Directory.CreateDirectory(staging);
            try
            {
                using (Stream resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(PayloadResource))
                {
                    if (resource == null)
                        throw new InvalidOperationException(
                            "В установщике не найден пакет приложения.");
                    using (var output = File.Create(zipPath))
                        resource.CopyTo(output);
                }
                string extracted = Path.Combine(staging, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extracted);
                string source = Path.Combine(extracted, "AeroMirror");
                PreparePinnedRuntime(source, staging);
                string core = Path.Combine(source, "core");
                if (!File.Exists(Path.Combine(core, "Qt6Core.dll")) ||
                    !File.Exists(Path.Combine(core, "LICENSE.rtf")) ||
                    !File.Exists(Path.Combine(core, "uxplay-windows.exe")))
                {
                    throw new InvalidOperationException(
                        "Runtime payload verification failed.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging))
                        Directory.Delete(staging, true);
                }
                catch { }
            }
        }

        internal static void VerifyBonjourRecoveryPolicyLogic()
        {
            if (!Program.IsExactBonjourMachineConfigurationInvocation(
                    new[] { Program.BonjourMachineConfigurationArgument }) ||
                Program.IsExactBonjourMachineConfigurationInvocation(
                    new string[0]) ||
                Program.IsExactBonjourMachineConfigurationInvocation(
                    new[]
                    {
                        Program.BonjourMachineConfigurationArgument,
                        "/unexpected"
                    }))
            {
                throw new InvalidOperationException(
                    "Elevated Bonjour dispatch is not an exact one-argument mode.");
            }

            ProcessStartInfo start =
                CreateBonjourMachineConfigurationStartInfo();
            string setupPath = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(
                    start.FileName, setupPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    start.Arguments,
                    Program.BonjourMachineConfigurationArgument,
                    StringComparison.Ordinal) ||
                !string.Equals(start.Verb, "runas", StringComparison.Ordinal) ||
                !string.Equals(
                    start.WorkingDirectory,
                    Path.GetDirectoryName(setupPath),
                    StringComparison.OrdinalIgnoreCase) ||
                !start.UseShellExecute ||
                start.WindowStyle != ProcessWindowStyle.Hidden ||
                start.ErrorDialog)
            {
                throw new InvalidOperationException(
                    "Bonjour administrator helper launch is not fixed to this Setup.");
            }

            ServiceActionNative[] restartActions =
                CreateBonjourRestartActions();
            if (BonjourFailureResetSeconds != 86400 ||
                restartActions.Length != 4 ||
                restartActions[0].Type != ScActionRestart ||
                restartActions[0].Delay != 5000 ||
                restartActions[1].Type != ScActionRestart ||
                restartActions[1].Delay != 30000 ||
                restartActions[2].Type != ScActionRestart ||
                restartActions[2].Delay != 120000 ||
                restartActions[3].Type != ScActionNone ||
                restartActions[3].Delay != 0 ||
                !CreateBonjourFailureActionsFlag().Enabled ||
                ServiceConfigFailureActions != 2 ||
                ServiceConfigFailureActionsFlag != 4 ||
                ServiceAutoStart != 2)
            {
                throw new InvalidOperationException(
                    "Bonjour recovery must be restart/restart/restart/none at " +
                    "5/30/120 seconds with non-crash failures enabled.");
            }
            if (BonjourElevatedSelfTimeoutMilliseconds <= 0 ||
                BonjourServiceWaitMilliseconds <= 0 ||
                BonjourServiceWaitMilliseconds >=
                    BonjourElevatedSelfTimeoutMilliseconds ||
                BonjourHelperTimeoutMilliseconds <=
                    BonjourElevatedSelfTimeoutMilliseconds + 20000)
            {
                throw new InvalidOperationException(
                    "Bonjour helper deadlines are not safely nested.");
            }

            if (!IsExpectedBonjourExecutablePath(
                    @"C:\Program Files\Bonjour\mDNSResponder.exe",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)") ||
                !IsExpectedBonjourExecutablePath(
                    @"C:\Program Files (x86)\Bonjour\mDNSResponder.exe",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)") ||
                IsExpectedBonjourExecutablePath(
                    @"C:\Program Files\Other\mDNSResponder.exe",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)") ||
                IsExpectedBonjourExecutablePath(
                    @"C:\Program Files\Bonjour\sub\mDNSResponder.exe",
                    @"C:\Program Files",
                    @"C:\Program Files (x86)"))
            {
                throw new InvalidOperationException(
                    "Bonjour executable-path policy is not exact.");
            }

            string parsed;
            if (!TryParseBonjourImagePath(
                    "\"C:\\Program Files\\Bonjour\\mDNSResponder.exe\"",
                    out parsed) ||
                !string.Equals(
                    parsed,
                    @"C:\Program Files\Bonjour\mDNSResponder.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                TryParseBonjourImagePath(
                    @"C:\Program Files\Bonjour\mDNSResponder.exe -server",
                    out parsed) ||
                TryParseBonjourImagePath(
                    @"%LOCALAPPDATA%\mDNSResponder.exe",
                    out parsed) ||
                TryParseBonjourImagePath(
                    @"\\server\share\mDNSResponder.exe",
                    out parsed))
            {
                throw new InvalidOperationException(
                    "Bonjour ImagePath parser did not remain fail-closed.");
            }

            if (BonjourServiceNames.Length != 2 ||
                !string.Equals(
                    BonjourServiceNames[0], "Bonjour Service",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    BonjourServiceNames[1], "mDNSResponder",
                    StringComparison.Ordinal) ||
                !IsKnownBonjourServiceName("Bonjour Service") ||
                !IsKnownBonjourServiceName("mDNSResponder") ||
                IsKnownBonjourServiceName("bonjour service") ||
                IsKnownBonjourServiceName("Bonjour Service "))
            {
                throw new InvalidOperationException(
                    "Bonjour service-name allowlist is not exact.");
            }

            var exactRule = CreateExpectedBonjourFirewallRuleSnapshot();
            if (!IsExpectedBonjourFirewallRule(
                    exactRule, exactRule.ApplicationName))
            {
                throw new InvalidOperationException(
                    "Exact Bonjour firewall matcher rejected the narrow rule.");
            }
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Name = "Other Bonjour rule"; }, "different name");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Enabled = false; }, "disabled");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Direction = 2; }, "outbound");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Action = 0; }, "blocked");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Protocol = 6; }, "TCP");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.Profiles = 6; }, "broadened profile");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.ApplicationName = @"C:\Other\mDNSResponder.exe"; },
                "different executable");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.LocalPorts = "Any"; }, "broad local port");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.RemoteAddresses = "Any"; }, "broad remote scope");
            VerifyRejectedBonjourFirewallMutation(
                delegate(BonjourFirewallRuleSnapshot rule)
                { rule.EdgeTraversal = true; }, "edge traversal");

            VerifyBonjourSecurityDescriptorLogic();
        }

        private static ServiceActionNative[] CreateBonjourRestartActions()
        {
            var actions = new ServiceActionNative[
                BonjourRestartDelaysMilliseconds.Length + 1];
            for (int index = 0;
                index < BonjourRestartDelaysMilliseconds.Length;
                index++)
            {
                actions[index] = new ServiceActionNative
                {
                    Type = ScActionRestart,
                    Delay = BonjourRestartDelaysMilliseconds[index]
                };
            }
            actions[actions.Length - 1] = new ServiceActionNative
            {
                Type = ScActionNone,
                Delay = 0
            };
            return actions;
        }

        private static ServiceFailureActionsFlagNative
            CreateBonjourFailureActionsFlag()
        {
            return new ServiceFailureActionsFlagNative { Enabled = true };
        }

        private static BonjourFirewallRuleSnapshot
            CreateExpectedBonjourFirewallRuleSnapshot()
        {
            return new BonjourFirewallRuleSnapshot
            {
                Name = BonjourFirewallRuleName,
                Enabled = true,
                Direction = 1,
                Action = 1,
                Protocol = 17,
                Profiles = 2,
                ApplicationName =
                    @"C:\Program Files\Bonjour\mDNSResponder.exe",
                LocalPorts = "5353",
                RemoteAddresses = "LocalSubnet",
                EdgeTraversal = false
            };
        }

        private static void VerifyRejectedBonjourFirewallMutation(
            Action<BonjourFirewallRuleSnapshot> mutate, string scenario)
        {
            BonjourFirewallRuleSnapshot rule =
                CreateExpectedBonjourFirewallRuleSnapshot();
            mutate(rule);
            if (IsExpectedBonjourFirewallRule(
                    rule,
                    @"C:\Program Files\Bonjour\mDNSResponder.exe"))
            {
                throw new InvalidOperationException(
                    "Bonjour firewall matcher accepted " + scenario + ".");
            }
        }

        private static void VerifyBonjourSecurityDescriptorLogic()
        {
            var system = new SecurityIdentifier(
                WellKnownSidType.LocalSystemSid, null);
            var administrators = new SecurityIdentifier(
                WellKnownSidType.BuiltinAdministratorsSid, null);
            var trustedInstaller = new SecurityIdentifier(
                "S-1-5-80-956008885-3418522649-1831038044-" +
                "1853292631-2271478464");
            var users = new SecurityIdentifier(
                WellKnownSidType.BuiltinUsersSid, null);
            var noRules = new FileSystemAccessRule[0];
            var untrustedWrite = new FileSystemAccessRule(
                users,
                FileSystemRights.WriteData,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow);
            var untrustedReadOnly = new FileSystemAccessRule(
                users,
                FileSystemRights.ReadData | FileSystemRights.ReadAttributes,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow);
            var inheritedOnlyWrite = new FileSystemAccessRule(
                users,
                FileSystemRights.WriteData,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow);
            var administratorWrite = new FileSystemAccessRule(
                administrators,
                FileSystemRights.WriteData,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow);

            if (!HasUntrustedWriteAccess(system, false, noRules) ||
                !HasUntrustedWriteAccess(users, true, noRules) ||
                !HasUntrustedWriteAccess(
                    system, true, new[] { untrustedWrite }) ||
                HasUntrustedWriteAccess(
                    system, true, new[] { untrustedReadOnly }) ||
                HasUntrustedWriteAccess(
                    system, true, new[] { inheritedOnlyWrite }) ||
                HasUntrustedWriteAccess(
                    administrators, true,
                    new[] { administratorWrite }) ||
                HasUntrustedWriteAccess(
                    trustedInstaller, true, noRules) ||
                !IsTrustedBonjourPathComponent(
                    FileAttributes.Normal, false) ||
                IsTrustedBonjourPathComponent(
                    FileAttributes.ReparsePoint, false) ||
                IsTrustedBonjourPathComponent(
                    FileAttributes.Normal, true))
            {
                throw new InvalidOperationException(
                    "Bonjour owner, ACL, or reparse-point policy is not fail-closed.");
            }
        }

        internal static void EnsureBonjourAutomaticRecovery()
        {
            string serviceName;
            string executablePath;
            string detail;
            if (!TryResolveBonjourServiceIdentity(
                    out serviceName, out executablePath, out detail))
            {
                SetupLog.Write(
                    "Bonjour automatic recovery was not configured because " +
                    "the installed service identity is unavailable: " + detail);
                return;
            }

            if (IsBonjourMachineReady(
                    serviceName, executablePath, out detail))
            {
                SetupLog.Write(
                    "Bonjour automatic recovery is already configured and " +
                    "the service is running.");
                return;
            }

            ProcessStartInfo start =
                CreateBonjourMachineConfigurationStartInfo();

            try
            {
                SetupLog.Write(
                    "Requesting one Windows administrator confirmation for " +
                    "Bonjour service recovery and Private mDNS policy.");
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                    {
                        SetupLog.Write(
                            "Bonjour configuration helper did not start.");
                        return;
                    }
                    if (!process.WaitForExit(BonjourHelperTimeoutMilliseconds))
                    {
                        SetupLog.Write(
                            "Bonjour configuration helper exceeded its " +
                            "bounded wait and will be terminated before " +
                            "installation continues.");
                        if (!TerminateProcessAndWait(process))
                        {
                            SetupLog.Write(
                                "Bonjour configuration helper did not exit " +
                                "after termination was requested.");
                        }
                        return;
                    }
                    if (process.ExitCode != 0)
                    {
                        SetupLog.Write(
                            "Bonjour configuration helper returned exit code " +
                            process.ExitCode + ". Installation will continue.");
                        return;
                    }
                }
            }
            catch (Win32Exception exception)
            {
                if (exception.NativeErrorCode == ErrorCancelled)
                {
                    SetupLog.Write(
                        "Bonjour administrator confirmation was canceled; " +
                        "installation will continue without changing the service.");
                    return;
                }
                SetupLog.Write(
                    "Bonjour configuration helper could not be launched: " +
                    exception);
                return;
            }
            catch (InvalidOperationException exception)
            {
                SetupLog.Write(
                    "Bonjour configuration helper could not be launched: " +
                    exception);
                return;
            }

            if (!TryResolveBonjourServiceIdentity(
                    out serviceName, out executablePath, out detail) ||
                !IsBonjourMachineReady(
                    serviceName, executablePath, out detail))
            {
                SetupLog.Write(
                    "Bonjour configuration helper completed, but the final " +
                    "machine state was not confirmed: " + detail);
                return;
            }
            SetupLog.Write(
                "Bonjour automatic service recovery and running state were " +
                "confirmed after administrator configuration.");
        }

        private static ProcessStartInfo
            CreateBonjourMachineConfigurationStartInfo()
        {
            string setupPath = Assembly.GetExecutingAssembly().Location;
            return new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = Program.BonjourMachineConfigurationArgument,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(setupPath),
                ErrorDialog = false
            };
        }

        internal static void ConfigureBonjourMachineElevated()
        {
            var state = new BonjourWatchdogState();
            using (var watchdog = new System.Threading.Timer(
                HandleBonjourConfigurationTimeout,
                state,
                BonjourElevatedSelfTimeoutMilliseconds,
                Timeout.Infinite))
            {
                try
                {
                    ConfigureBonjourMachineElevatedCore();
                }
                finally
                {
                    Interlocked.CompareExchange(
                        ref state.CompletionState, 1, 0);
                    using (var drained = new ManualResetEvent(false))
                    {
                        if (watchdog.Dispose(drained))
                            drained.WaitOne(5000);
                    }
                }
            }
        }

        private static void HandleBonjourConfigurationTimeout(object value)
        {
            var state = value as BonjourWatchdogState;
            if (state == null || Interlocked.CompareExchange(
                    ref state.CompletionState, 2, 0) != 0)
                return;

            Environment.FailFast(
                "Bonjour machine configuration exceeded its deadline.");
        }

        private static void ConfigureBonjourMachineElevatedCore()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(
                        WindowsBuiltInRole.Administrator))
                {
                    throw new UnauthorizedAccessException(
                        "Bonjour machine configuration requires administrator rights.");
                }
            }

            string serviceName;
            string executablePath;
            string error;
            if (!TryResolveBonjourServiceIdentity(
                    out serviceName, out executablePath, out error))
            {
                throw new InvalidOperationException(
                    "Bonjour service identity is unavailable: " + error);
            }

            EnsureBonjourIdentityUnchanged(serviceName, executablePath);
            ConfigureBonjourServicePolicy(serviceName);
            EnsureBonjourIdentityUnchanged(serviceName, executablePath);
            ConfigureBonjourFirewallRule(executablePath);

            EnsureBonjourIdentityUnchanged(serviceName, executablePath);
            StartBonjourService(serviceName);
            if (!WaitForBonjourRunning(serviceName))
            {
                throw new InvalidOperationException(
                    "Bonjour did not reach the Running state after configuration.");
            }

            string finalServiceName;
            string finalExecutablePath;
            if (!TryResolveBonjourServiceIdentity(
                    out finalServiceName, out finalExecutablePath, out error) ||
                !string.Equals(
                    finalServiceName, serviceName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    finalExecutablePath, executablePath,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsBonjourMachineReady(
                    finalServiceName, finalExecutablePath, out error))
            {
                throw new InvalidOperationException(
                    "Bonjour identity or recovery policy changed during configuration.");
            }
        }

        private static void EnsureBonjourIdentityUnchanged(
            string expectedServiceName, string expectedExecutablePath)
        {
            string serviceName;
            string executablePath;
            string detail;
            if (!TryResolveBonjourServiceIdentity(
                    out serviceName, out executablePath, out detail) ||
                !string.Equals(
                    serviceName, expectedServiceName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    executablePath, expectedExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Bonjour service identity changed during configuration.");
            }
        }

        private static void ConfigureBonjourServicePolicy(string serviceName)
        {
            if (!IsKnownBonjourServiceName(serviceName))
                throw new InvalidOperationException(
                    "Unknown Bonjour service identity.");

            IntPtr manager = OpenSCManager(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr service = OpenService(
                    manager,
                    serviceName,
                    ServiceQueryConfig |
                    ServiceChangeConfig |
                    ServiceStart);
                if (service == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    if (!ChangeServiceConfig(
                            service,
                            ServiceNoChange,
                            ServiceAutoStart,
                            ServiceNoChange,
                            null,
                            null,
                            IntPtr.Zero,
                            null,
                            null,
                            null,
                            null))
                    {
                        throw new Win32Exception(
                            Marshal.GetLastWin32Error());
                    }

                    ServiceActionNative[] restartActions =
                        CreateBonjourRestartActions();
                    int actionSize = Marshal.SizeOf(
                        typeof(ServiceActionNative));
                    IntPtr actions = Marshal.AllocHGlobal(
                        checked(actionSize *
                            restartActions.Length));
                    try
                    {
                        for (int index = 0;
                            index < restartActions.Length;
                            index++)
                        {
                            Marshal.StructureToPtr(
                                restartActions[index],
                                IntPtr.Add(actions, index * actionSize),
                                false);
                        }
                        var failure = new ServiceFailureActionsNative
                        {
                            ResetPeriod = BonjourFailureResetSeconds,
                            RebootMessage = IntPtr.Zero,
                            Command = IntPtr.Zero,
                            ActionCount = checked((uint)
                                restartActions.Length),
                            Actions = actions
                        };
                        if (!ChangeServiceConfig2FailureActions(
                                service,
                                ServiceConfigFailureActions,
                                ref failure))
                        {
                            throw new Win32Exception(
                                Marshal.GetLastWin32Error());
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(actions);
                    }

                    ServiceFailureActionsFlagNative flag =
                        CreateBonjourFailureActionsFlag();
                    if (!ChangeServiceConfig2FailureFlag(
                            service,
                            ServiceConfigFailureActionsFlag,
                            ref flag))
                    {
                        throw new Win32Exception(
                            Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        private static void StartBonjourService(string serviceName)
        {
            if (!IsKnownBonjourServiceName(serviceName))
                throw new InvalidOperationException(
                    "Unknown Bonjour service identity.");
            IntPtr manager = OpenSCManager(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr service = OpenService(
                    manager, serviceName, ServiceStart);
                if (service == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    if (!StartService(service, 0, null))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ErrorServiceAlreadyRunning)
                            throw new Win32Exception(error);
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        private static void ConfigureBonjourFirewallRule(
            string executablePath)
        {
            Type policyType = Type.GetTypeFromProgID(
                "HNetCfg.FwPolicy2", false);
            Type ruleType = Type.GetTypeFromProgID(
                "HNetCfg.FWRule", false);
            if (policyType == null || ruleType == null)
            {
                throw new InvalidOperationException(
                    "Windows Firewall COM API is unavailable.");
            }

            object policy = null;
            object rules = null;
            object rule = null;
            try
            {
                policy = Activator.CreateInstance(policyType);
                rules = GetFirewallComProperty(policy, "Rules");
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    int before = CountOwnedBonjourFirewallRules(rules);
                    if (before == 0)
                        break;
                    InvokeFirewallComMethod(
                        rules,
                        "Remove",
                        new object[] { BonjourFirewallRuleName });
                    int after = CountOwnedBonjourFirewallRules(rules);
                    if (after >= before)
                    {
                        throw new InvalidOperationException(
                            "Existing Bonjour firewall rule could not be removed.");
                    }
                }
                if (CountOwnedBonjourFirewallRules(rules) != 0)
                {
                    throw new InvalidOperationException(
                        "Too many duplicate Bonjour firewall rules exist.");
                }

                rule = Activator.CreateInstance(ruleType);
                SetFirewallComProperty(
                    rule, "Name", BonjourFirewallRuleName);
                SetFirewallComProperty(
                    rule, "Description",
                    "AeroMirror AirPlay discovery on the private local subnet");
                SetFirewallComProperty(
                    rule, "ApplicationName", executablePath);
                SetFirewallComProperty(rule, "Protocol", 17);
                SetFirewallComProperty(rule, "LocalPorts", "5353");
                SetFirewallComProperty(
                    rule, "RemoteAddresses", "LocalSubnet");
                SetFirewallComProperty(rule, "Direction", 1);
                SetFirewallComProperty(rule, "Enabled", true);
                SetFirewallComProperty(rule, "Profiles", 2);
                SetFirewallComProperty(rule, "Action", 1);
                SetFirewallComProperty(rule, "EdgeTraversal", false);
                InvokeFirewallComMethod(
                    rules, "Add", new object[] { rule });
            }
            finally
            {
                ReleaseFirewallComObject(rule);
                ReleaseFirewallComObject(rules);
                ReleaseFirewallComObject(policy);
            }
        }

        private static int CountOwnedBonjourFirewallRules(object rulesObject)
        {
            IEnumerable rules = rulesObject as IEnumerable;
            if (rules == null)
                throw new InvalidOperationException(
                    "Windows Firewall rule collection is unavailable.");
            int count = 0;
            foreach (object rule in rules)
            {
                try
                {
                    string name = Convert.ToString(
                        GetFirewallComProperty(rule, "Name"),
                        CultureInfo.InvariantCulture);
                    if (string.Equals(
                            name, BonjourFirewallRuleName,
                            StringComparison.Ordinal))
                        count++;
                }
                finally
                {
                    ReleaseFirewallComObject(rule);
                }
            }
            return count;
        }

        private static void SetFirewallComProperty(
            object target, string name, object value)
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.SetProperty,
                null,
                target,
                new object[] { value },
                CultureInfo.InvariantCulture);
        }

        private static object InvokeFirewallComMethod(
            object target, string name, object[] arguments)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod,
                null,
                target,
                arguments,
                CultureInfo.InvariantCulture);
        }

        private static bool IsBonjourMachineReady(
            string serviceName,
            string executablePath,
            out string detail)
        {
            detail = "";
            if (!HasExpectedBonjourFailureActions(serviceName))
            {
                detail = "expected restart-on-failure policy is missing";
                return false;
            }

            if (!HasExpectedBonjourFirewallRule(executablePath, out detail))
                return false;

            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        detail = "service is " + service.Status;
                        return false;
                    }
                    if (service.StartType != ServiceStartMode.Automatic)
                    {
                        detail = "service startup is " + service.StartType;
                        return false;
                    }
                }
                return true;
            }
            catch (InvalidOperationException exception)
            {
                detail = exception.Message;
                return false;
            }
        }

        private static bool HasExpectedBonjourFirewallRule(
            string executablePath, out string detail)
        {
            detail = "";
            Type policyType = Type.GetTypeFromProgID(
                "HNetCfg.FwPolicy2", false);
            if (policyType == null)
            {
                detail = "Windows Firewall policy COM API is unavailable";
                return false;
            }

            object policy = null;
            object rulesObject = null;
            int ownedRuleCount = 0;
            bool exactRuleFound = false;
            try
            {
                policy = Activator.CreateInstance(policyType);
                rulesObject = GetFirewallComProperty(policy, "Rules");
                IEnumerable rules = rulesObject as IEnumerable;
                if (rules == null)
                {
                    detail = "Windows Firewall rule collection is unavailable";
                    return false;
                }

                foreach (object ruleObject in rules)
                {
                    try
                    {
                        string name = Convert.ToString(
                            GetFirewallComProperty(ruleObject, "Name"),
                            CultureInfo.InvariantCulture);
                        if (!string.Equals(
                                name, BonjourFirewallRuleName,
                                StringComparison.Ordinal))
                            continue;

                        ownedRuleCount++;
                        var rule = new BonjourFirewallRuleSnapshot
                        {
                            Name = name,
                            Enabled = Convert.ToBoolean(
                                GetFirewallComProperty(
                                    ruleObject, "Enabled"),
                                CultureInfo.InvariantCulture),
                            Direction = Convert.ToInt32(
                                GetFirewallComProperty(
                                    ruleObject, "Direction"),
                                CultureInfo.InvariantCulture),
                            Action = Convert.ToInt32(
                                GetFirewallComProperty(
                                    ruleObject, "Action"),
                                CultureInfo.InvariantCulture),
                            Protocol = Convert.ToInt32(
                                GetFirewallComProperty(
                                    ruleObject, "Protocol"),
                                CultureInfo.InvariantCulture),
                            Profiles = Convert.ToInt32(
                                GetFirewallComProperty(
                                    ruleObject, "Profiles"),
                                CultureInfo.InvariantCulture),
                            ApplicationName = Convert.ToString(
                                GetFirewallComProperty(
                                    ruleObject, "ApplicationName"),
                                CultureInfo.InvariantCulture),
                            LocalPorts = Convert.ToString(
                                GetFirewallComProperty(
                                    ruleObject, "LocalPorts"),
                                CultureInfo.InvariantCulture),
                            RemoteAddresses = Convert.ToString(
                                GetFirewallComProperty(
                                    ruleObject, "RemoteAddresses"),
                                CultureInfo.InvariantCulture),
                            EdgeTraversal = Convert.ToBoolean(
                                GetFirewallComProperty(
                                    ruleObject, "EdgeTraversal"),
                                CultureInfo.InvariantCulture)
                        };
                        exactRuleFound = IsExpectedBonjourFirewallRule(
                            rule, executablePath);
                    }
                    catch (COMException)
                    {
                    }
                    catch (InvalidCastException)
                    {
                    }
                    catch (FormatException)
                    {
                    }
                    finally
                    {
                        ReleaseFirewallComObject(ruleObject);
                    }
                }
            }
            catch (COMException exception)
            {
                detail = exception.Message;
                return false;
            }
            catch (TargetInvocationException exception)
            {
                detail = exception.InnerException != null
                    ? exception.InnerException.Message
                    : exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                detail = exception.Message;
                return false;
            }
            finally
            {
                ReleaseFirewallComObject(rulesObject);
                ReleaseFirewallComObject(policy);
            }

            if (ownedRuleCount != 1 || !exactRuleFound)
            {
                detail = ownedRuleCount == 0
                    ? "expected Private Bonjour firewall rule is missing"
                    : "owned Bonjour firewall rule is duplicated or not exact";
                return false;
            }
            return true;
        }

        private static bool IsExpectedBonjourFirewallRule(
            BonjourFirewallRuleSnapshot rule, string executablePath)
        {
            return rule != null &&
                string.Equals(
                    rule.Name, BonjourFirewallRuleName,
                    StringComparison.Ordinal) &&
                rule.Enabled &&
                rule.Direction == 1 &&
                rule.Action == 1 &&
                rule.Protocol == 17 &&
                rule.Profiles == 2 &&
                !rule.EdgeTraversal &&
                string.Equals(
                    rule.ApplicationName, executablePath,
                    StringComparison.OrdinalIgnoreCase) &&
                IsExactFirewallValue(rule.LocalPorts, "5353") &&
                IsExactFirewallValue(
                    rule.RemoteAddresses, "LocalSubnet");
        }

        private static bool IsExactFirewallValue(
            string actual, string expected)
        {
            return !string.IsNullOrWhiteSpace(actual) &&
                string.Equals(
                    actual.Trim(), expected,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static object GetFirewallComProperty(
            object target, string name)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                null,
                target,
                null,
                CultureInfo.InvariantCulture);
        }

        private static void ReleaseFirewallComObject(object value)
        {
            if (value == null || !Marshal.IsComObject(value))
                return;
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch (InvalidComObjectException)
            {
            }
        }

        private static bool HasExpectedBonjourFailureActions(
            string serviceName)
        {
            if (!IsKnownBonjourServiceName(serviceName))
                return false;

            IntPtr manager = OpenSCManager(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
                return false;
            try
            {
                IntPtr service = OpenService(
                    manager, serviceName, ServiceQueryConfig);
                if (service == IntPtr.Zero)
                    return false;
                try
                {
                    uint bytesNeeded;
                    QueryServiceConfig2(
                        service,
                        ServiceConfigFailureActions,
                        IntPtr.Zero,
                        0,
                        out bytesNeeded);
                    if (bytesNeeded == 0)
                        return false;

                    IntPtr buffer = Marshal.AllocHGlobal(
                        checked((int)bytesNeeded));
                    try
                    {
                        if (!QueryServiceConfig2(
                                service,
                                ServiceConfigFailureActions,
                                buffer,
                                bytesNeeded,
                                out bytesNeeded))
                            return false;
                        var actions =
                            (ServiceFailureActionsNative)Marshal.PtrToStructure(
                                buffer,
                                typeof(ServiceFailureActionsNative));
                        if (actions.ResetPeriod !=
                                BonjourFailureResetSeconds ||
                            actions.ActionCount !=
                                BonjourRestartDelaysMilliseconds.Length + 1 ||
                            actions.Actions == IntPtr.Zero)
                            return false;

                        int actionSize = Marshal.SizeOf(
                            typeof(ServiceActionNative));
                        for (int index = 0;
                            index < BonjourRestartDelaysMilliseconds.Length;
                            index++)
                        {
                            IntPtr actionPointer = IntPtr.Add(
                                actions.Actions, checked(index * actionSize));
                            var action =
                                (ServiceActionNative)Marshal.PtrToStructure(
                                    actionPointer,
                                    typeof(ServiceActionNative));
                            if (action.Type != ScActionRestart ||
                                action.Delay !=
                                    BonjourRestartDelaysMilliseconds[index])
                                return false;
                        }
                        IntPtr terminalActionPointer = IntPtr.Add(
                            actions.Actions,
                            checked(BonjourRestartDelaysMilliseconds.Length *
                                actionSize));
                        var terminalAction =
                            (ServiceActionNative)Marshal.PtrToStructure(
                                terminalActionPointer,
                                typeof(ServiceActionNative));
                        if (terminalAction.Type != ScActionNone ||
                            terminalAction.Delay != 0)
                            return false;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }

                    int flagSize = Marshal.SizeOf(
                        typeof(ServiceFailureActionsFlagNative));
                    IntPtr flagBuffer = Marshal.AllocHGlobal(flagSize);
                    try
                    {
                        if (!QueryServiceConfig2(
                                service,
                                ServiceConfigFailureActionsFlag,
                                flagBuffer,
                                checked((uint)flagSize),
                                out bytesNeeded))
                            return false;
                        var flag =
                            (ServiceFailureActionsFlagNative)
                                Marshal.PtrToStructure(
                                    flagBuffer,
                                    typeof(ServiceFailureActionsFlagNative));
                        return flag.Enabled;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(flagBuffer);
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        private static bool TryResolveBonjourServiceIdentity(
            out string serviceName,
            out string executablePath,
            out string error)
        {
            serviceName = "";
            executablePath = "";
            error = "Bonjour service was not found.";
            foreach (string candidateName in BonjourServiceNames)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\" + candidateName,
                    false))
                {
                    if (key == null)
                        continue;
                    if (serviceName.Length != 0)
                    {
                        error = "Multiple Bonjour service identities were found.";
                        serviceName = "";
                        executablePath = "";
                        return false;
                    }
                    object raw = key.GetValue(
                        "ImagePath",
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    string parsed;
                    if (!TryParseBonjourImagePath(raw as string, out parsed) ||
                        !File.Exists(parsed) ||
                        !IsProtectedProgramFilesPath(parsed))
                    {
                        error = "Bonjour ImagePath is not a protected absolute mDNSResponder.exe path.";
                        return false;
                    }
                    serviceName = candidateName;
                    executablePath = parsed;
                }
            }
            if (serviceName.Length == 0)
                return false;
            error = "";
            return true;
        }

        private static bool TryParseBonjourImagePath(
            string raw, out string executablePath)
        {
            executablePath = "";
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string value = raw.Trim();
            string candidate;
            if (value[0] == '\"')
            {
                int closingQuote = value.IndexOf('\"', 1);
                if (closingQuote <= 1 ||
                    value.Substring(closingQuote + 1).Trim().Length != 0)
                    return false;
                candidate = value.Substring(1, closingQuote - 1);
            }
            else
            {
                for (int index = 0; index < value.Length; index++)
                {
                    if (char.IsWhiteSpace(value[index]))
                        return false;
                }
                candidate = value;
            }

            if (candidate.IndexOf('%') >= 0 ||
                candidate.IndexOf('*') >= 0 ||
                candidate.IndexOf('?') >= 0 ||
                candidate.IndexOf('\"') >= 0)
                return false;
            for (int index = 0; index < candidate.Length; index++)
            {
                if (char.IsControl(candidate[index]))
                    return false;
            }
            if (candidate.Length < 4 ||
                !char.IsLetter(candidate[0]) ||
                candidate[1] != ':' ||
                candidate[2] != '\\' ||
                candidate.IndexOf(':', 2) >= 0 ||
                candidate.StartsWith(@"\\", StringComparison.Ordinal) ||
                candidate.StartsWith(@"\\?\", StringComparison.Ordinal))
                return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            if (!string.Equals(
                    fullPath, candidate,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(fullPath),
                    "mDNSResponder.exe",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            executablePath = fullPath;
            return true;
        }

        private static bool IsProtectedProgramFilesPath(string path)
        {
            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);
            if (!IsExpectedBonjourExecutablePath(
                    path, programFiles, programFilesX86))
                return false;

            string root = IsPathUnderExactRoot(path, programFiles)
                ? Path.GetFullPath(programFiles).TrimEnd('\\')
                : Path.GetFullPath(programFilesX86).TrimEnd('\\');
            string current = path;
            while (true)
            {
                FileAttributes attributes = File.GetAttributes(current);
                if (!IsTrustedBonjourPathComponent(
                        attributes,
                        HasUntrustedWriteAccess(current)))
                    return false;
                if (string.Equals(
                        current, root,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
                current = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(current))
                    return false;
            }
        }

        private static bool IsTrustedBonjourPathComponent(
            FileAttributes attributes, bool hasUntrustedWriteAccess)
        {
            return (attributes & FileAttributes.ReparsePoint) == 0 &&
                !hasUntrustedWriteAccess;
        }

        private static bool IsExpectedBonjourExecutablePath(
            string path, string programFiles, string programFilesX86)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            string[] roots = { programFiles, programFilesX86 };
            foreach (string rootValue in roots)
            {
                if (string.IsNullOrWhiteSpace(rootValue))
                    continue;
                string expected = Path.Combine(
                    Path.GetFullPath(rootValue).TrimEnd('\\'),
                    "Bonjour",
                    "mDNSResponder.exe");
                if (string.Equals(
                        Path.GetFullPath(path), expected,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsPathUnderExactRoot(
            string path, string rootValue)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(rootValue))
                return false;
            string root = Path.GetFullPath(rootValue).TrimEnd('\\');
            return Path.GetFullPath(path).StartsWith(
                root + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasUntrustedWriteAccess(string path)
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? (FileSystemSecurity)Directory.GetAccessControl(
                    path,
                    AccessControlSections.Access |
                    AccessControlSections.Owner)
                : File.GetAccessControl(
                    path,
                    AccessControlSections.Access |
                    AccessControlSections.Owner);
            var raw = new RawSecurityDescriptor(
                security.GetSecurityDescriptorBinaryForm(), 0);
            var owner = security.GetOwner(
                typeof(SecurityIdentifier)) as SecurityIdentifier;
            AuthorizationRuleCollection rules = security.GetAccessRules(
                true, true, typeof(SecurityIdentifier));
            return HasUntrustedWriteAccess(
                owner, raw.DiscretionaryAcl != null, rules);
        }

        private static bool HasUntrustedWriteAccess(
            SecurityIdentifier owner,
            bool hasDiscretionaryAcl,
            IEnumerable accessRules)
        {
            if (!hasDiscretionaryAcl ||
                owner == null ||
                !IsTrustedMachineWriter(owner) ||
                accessRules == null)
                return true;
            foreach (object value in accessRules)
            {
                var rule = value as FileSystemAccessRule;
                if (rule == null)
                    return true;
                if (rule.AccessControlType != AccessControlType.Allow ||
                    (rule.PropagationFlags &
                        PropagationFlags.InheritOnly) != 0)
                    continue;
                int rights = unchecked((int)rule.FileSystemRights);
                int writeRights = unchecked((int)(
                    FileSystemRights.WriteData |
                    FileSystemRights.AppendData |
                    FileSystemRights.WriteExtendedAttributes |
                    FileSystemRights.DeleteSubdirectoriesAndFiles |
                    FileSystemRights.WriteAttributes |
                    FileSystemRights.Delete |
                    FileSystemRights.ChangePermissions |
                    FileSystemRights.TakeOwnership));
                int genericWriteOrAll = unchecked((int)0x50000000);
                if ((rights & writeRights) == 0 &&
                    (rights & genericWriteOrAll) == 0)
                    continue;

                var sid = rule.IdentityReference as SecurityIdentifier;
                if (sid == null || !IsTrustedMachineWriter(sid))
                    return true;
            }
            return false;
        }

        private static bool IsTrustedMachineWriter(SecurityIdentifier sid)
        {
            if (sid == null)
                return false;
            return sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                sid.IsWellKnown(
                    WellKnownSidType.BuiltinAdministratorsSid) ||
                string.Equals(
                    sid.Value,
                    "S-1-5-80-956008885-3418522649-1831038044-" +
                    "1853292631-2271478464",
                    StringComparison.Ordinal);
        }

        private static bool IsKnownBonjourServiceName(string value)
        {
            return string.Equals(
                    value, "Bonjour Service",
                    StringComparison.Ordinal) ||
                string.Equals(
                    value, "mDNSResponder",
                    StringComparison.Ordinal);
        }

        private static bool TerminateProcessAndWait(Process process)
        {
            if (process == null)
                return true;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
                return process.HasExited;
            }
            return process.HasExited || process.WaitForExit(5000);
        }

        private static bool WaitForBonjourRunning(string serviceName)
        {
            if (!IsKnownBonjourServiceName(serviceName))
                return false;
            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    service.WaitForStatus(
                        ServiceControllerStatus.Running,
                        TimeSpan.FromMilliseconds(
                            BonjourServiceWaitMilliseconds));
                    service.Refresh();
                    return service.Status == ServiceControllerStatus.Running;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                return false;
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr OpenService(
            IntPtr serviceManager,
            string serviceName,
            uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(
            IntPtr service,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password,
            string displayName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2FailureActions(
            IntPtr service,
            uint infoLevel,
            ref ServiceFailureActionsNative info);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2FailureFlag(
            IntPtr service,
            uint infoLevel,
            ref ServiceFailureActionsFlagNative info);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool StartService(
            IntPtr service,
            int argumentCount,
            string[] arguments);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryServiceConfig2(
            IntPtr service,
            uint infoLevel,
            IntPtr buffer,
            uint bufferSize,
            out uint bytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        internal static string Install(bool startMenu, bool desktop)
        {
            string staging = Path.Combine(
                Path.GetTempPath(),
                "AeroMirror-install-" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(staging, "payload.zip");
            Directory.CreateDirectory(staging);

            try
            {
                using (Stream resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(PayloadResource))
                {
                    if (resource == null)
                        throw new InvalidOperationException(
                            "В установщике не найден пакет приложения.");
                    using (var output = File.Create(zipPath))
                        resource.CopyTo(output);
                }

                string extracted = Path.Combine(staging, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extracted);
                string source = Path.Combine(extracted, "AeroMirror");
                if (!File.Exists(Path.Combine(source, "AeroMirror.exe")))
                    throw new InvalidOperationException(
                        "Пакет приложения повреждён.");
                PreparePinnedRuntime(source, staging);

                string sourceUninstaller = Path.Combine(
                    source, "Uninstall.exe");
                using (Stream resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(UninstallerResource))
                {
                    if (resource == null)
                        throw new InvalidOperationException(
                            "В установщике не найден модуль удаления.");
                    using (var output = File.Create(sourceUninstaller))
                        resource.CopyTo(output);
                }

                InstallationMetadataSnapshot metadata =
                    InstallationMetadataSnapshot.Capture();
                StopInstalledProcesses(InstallPaths.InstallDirectory);
                string backup = InstallPaths.InstallDirectory +
                    ".backup-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(
                    Path.GetDirectoryName(InstallPaths.InstallDirectory));
                if (Directory.Exists(InstallPaths.InstallDirectory))
                {
                    MoveInstallDirectoryToBackup(
                        InstallPaths.InstallDirectory, backup);
                }
                string installedExecutable;
                try
                {
                    MoveOrCopyDirectory(source, InstallPaths.InstallDirectory);

                    string uninstaller = Path.Combine(
                        InstallPaths.InstallDirectory, "Uninstall.exe");
                    RemoveShortcuts();
                    string executable = Path.Combine(
                        InstallPaths.InstallDirectory, "AeroMirror.exe");
                    if (startMenu)
                        CreateShortcut(InstallPaths.StartMenuShortcut, executable);
                    if (desktop)
                        CreateShortcut(InstallPaths.DesktopShortcut, executable);
                    WriteUninstallRegistry(executable, uninstaller);
                    MigrateAutostart(executable);
                    TryDeleteDirectory(backup);
                    installedExecutable = executable;
                }
                catch
                {
                    try
                    {
                        if (Directory.Exists(InstallPaths.InstallDirectory))
                            Directory.Delete(
                                InstallPaths.InstallDirectory, true);
                        if (Directory.Exists(backup))
                            Directory.Move(
                                backup, InstallPaths.InstallDirectory);
                    }
                    catch (Exception ex)
                    {
                        SetupLog.Write(
                            "Installation directory rollback failed: " + ex);
                    }
                    try
                    {
                        metadata.Restore();
                    }
                    catch (Exception ex)
                    {
                        SetupLog.Write(
                            "Installation metadata rollback failed: " + ex);
                    }
                    throw;
                }

                // App installation is committed at this point. Bonjour is a
                // shared machine prerequisite, so its best-effort recovery
                // setup must never roll back or delete a successful update.
                try
                {
                    EnsureBonjourAutomaticRecovery();
                }
                catch (Exception exception)
                {
                    SetupLog.Write(
                        "Bonjour automatic recovery configuration was " +
                        "skipped after an unexpected error: " + exception);
                }
                return installedExecutable;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging))
                        Directory.Delete(staging, true);
                }
                catch { }
            }
        }

        private static void PreparePinnedRuntime(string source, string staging)
        {
            string core = Path.Combine(source, "core");
            string delivery = Path.Combine(
                core, "resources", "runtime-delivery.json");
            if (!File.Exists(delivery))
                throw new InvalidOperationException(
                    "В review-пакете отсутствует описание загрузки runtime.");
            string manifest = Path.Combine(
                core, "resources", "build-manifest.json");
            string provenance = Path.Combine(
                core, "resources", "source-provenance.json");
            string payloadCore = Path.Combine(core, "uxplay-windows.exe");
            ValidateReviewedManifest(manifest, provenance, payloadCore);

            string patchedCore = Path.Combine(staging, "headless-core.exe");
            string reviewedManifest = Path.Combine(
                staging, "aeromirror-headless-build.json");
            string reviewedProvenance = Path.Combine(
                staging, "aeromirror-source-provenance.json");
            string deliveryCopy = Path.Combine(
                staging, "runtime-delivery.json");
            File.Copy(
                Path.Combine(core, "uxplay-windows.exe"),
                patchedCore,
                true);
            File.Copy(
                Path.Combine(core, "resources", "build-manifest.json"),
                reviewedManifest,
                true);
            File.Copy(provenance, reviewedProvenance, true);
            File.Copy(delivery, deliveryCopy, true);

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string archive = Path.Combine(staging, "uxplay-windows.zip");
            AcquirePinnedRuntimeArchive(archive);
            string actualHash = ComputeSha256(archive);
            if (!string.Equals(
                    actualHash, RuntimeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(archive);
                throw new InvalidOperationException(
                    "Проверка runtime не пройдена: SHA-256 не совпал. " +
                    "Установка остановлена без замены текущей версии.");
            }

            string extractedRuntime = Path.Combine(staging, "upstream-runtime");
            ZipFile.ExtractToDirectory(archive, extractedRuntime);
            string runtimeRoot = FindRuntimeRoot(extractedRuntime);
            if (runtimeRoot == null ||
                !File.Exists(Path.Combine(runtimeRoot, "LICENSE.rtf")))
            {
                throw new InvalidOperationException(
                    "Проверенный архив runtime имеет неизвестную структуру.");
            }

            string upstreamManifest = Path.Combine(
                runtimeRoot, "resources", "build-manifest.json");
            string upstreamManifestCopy = Path.Combine(
                staging, "upstream-build-manifest.json");
            if (File.Exists(upstreamManifest))
                File.Copy(upstreamManifest, upstreamManifestCopy, true);

            CopyDirectory(runtimeRoot, core);
            File.Copy(patchedCore, Path.Combine(core, "uxplay-windows.exe"), true);
            string resources = Path.Combine(core, "resources");
            Directory.CreateDirectory(resources);
            File.Copy(
                reviewedManifest,
                Path.Combine(resources, "build-manifest.json"),
                true);
            File.Copy(
                deliveryCopy,
                Path.Combine(resources, "runtime-delivery.json"),
                true);
            File.Copy(
                reviewedProvenance,
                Path.Combine(resources, "source-provenance.json"),
                true);
            if (File.Exists(upstreamManifestCopy))
            {
                File.Copy(
                    upstreamManifestCopy,
                    Path.Combine(resources, "upstream-build-manifest.json"),
                    true);
            }

            if (!File.Exists(Path.Combine(core, "Qt6Core.dll")) ||
                !File.Exists(Path.Combine(core, "LICENSE.rtf")))
            {
                throw new InvalidOperationException(
                    "Runtime не прошёл проверку полноты после распаковки.");
            }
            VerifyCoreLoaderCompatibility(core);
        }

        private static void AcquirePinnedRuntimeArchive(string archive)
        {
            bool cacheHit = false;
            try
            {
                PruneRuntimeCache();
                if (File.Exists(RuntimeCachePath) &&
                    string.Equals(
                        ComputeSha256(RuntimeCachePath),
                        RuntimeSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(RuntimeCachePath, archive, true);
                    cacheHit = string.Equals(
                        ComputeSha256(archive),
                        RuntimeSha256,
                        StringComparison.OrdinalIgnoreCase);
                    if (cacheHit)
                        SetupLog.Write(
                            "Verified pinned runtime cache reused.");
                }
                else if (File.Exists(RuntimeCachePath))
                {
                    SetupLog.Write(
                        "Pinned runtime cache was invalid and will be replaced.");
                    TryDeleteFile(RuntimeCachePath);
                }
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "Pinned runtime cache could not be reused: " + ex.Message);
                TryDeleteFile(archive);
            }

            if (cacheHit)
                return;

            SetupLog.Write("Downloading pinned runtime.");
            using (var client = new PinnedDownloadClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] =
                    "AeroMirror-Setup/" + SetupForm.SetupVersion.ToString(3);
                client.DownloadFile(RuntimeUrl, archive);
            }
            if (!string.Equals(
                    ComputeSha256(archive),
                    RuntimeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(archive);
                throw new InvalidOperationException(
                    "Проверка runtime не пройдена: SHA-256 не совпал. " +
                    "Установка остановлена без замены текущей версии.");
            }
            TryStorePinnedRuntimeCache(archive);
        }

        private static void PruneRuntimeCache()
        {
            if (!Directory.Exists(RuntimeCacheDirectory))
                return;
            foreach (string candidate in Directory.GetFiles(
                RuntimeCacheDirectory))
            {
                if (!string.Equals(
                        candidate,
                        RuntimeCachePath,
                        StringComparison.OrdinalIgnoreCase))
                    TryDeleteFile(candidate);
            }
        }

        private static void TryStorePinnedRuntimeCache(string archive)
        {
            string temporary = "";
            try
            {
                Directory.CreateDirectory(RuntimeCacheDirectory);
                temporary = Path.Combine(
                    RuntimeCacheDirectory,
                    "." + Guid.NewGuid().ToString("N") + ".partial");
                File.Copy(archive, temporary, true);
                if (!string.Equals(
                        ComputeSha256(temporary),
                        RuntimeSha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Runtime cache copy failed SHA-256 verification.");

                if (File.Exists(RuntimeCachePath))
                {
                    if (string.Equals(
                            ComputeSha256(RuntimeCachePath),
                            RuntimeSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteFile(temporary);
                        return;
                    }
                    TryDeleteFile(RuntimeCachePath);
                }
                File.Move(temporary, RuntimeCachePath);
                temporary = "";
                SetupLog.Write("Verified pinned runtime stored in cache.");
            }
            catch (Exception ex)
            {
                SetupLog.Write(
                    "Pinned runtime cache could not be stored: " + ex.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporary))
                    TryDeleteFile(temporary);
            }
        }

        private static void ValidateReviewedManifest(
            string path,
            string provenancePath,
            string corePath)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "В review-пакете отсутствует manifest headless core.");
            if (!File.Exists(provenancePath))
                throw new InvalidOperationException(
                    "В review-пакете отсутствует source provenance.");
            if (!File.Exists(corePath))
                throw new InvalidOperationException(
                    "В review-пакете отсутствует headless core.");

            Dictionary<string, object> manifest;
            Dictionary<string, object> provenance;
            byte[] embeddedProvenance;
            try
            {
                var serializer = new JavaScriptSerializer();
                manifest = serializer.DeserializeObject(File.ReadAllText(path))
                    as Dictionary<string, object>;
                provenance = serializer.DeserializeObject(
                    File.ReadAllText(provenancePath))
                    as Dictionary<string, object>;
                using (Stream resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(ProvenanceResource))
                {
                    if (resource == null)
                        throw new InvalidDataException(
                            "Installer provenance resource is missing.");
                    using (var memory = new MemoryStream())
                    {
                        resource.CopyTo(memory);
                        embeddedProvenance = memory.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Manifest headless core повреждён.", ex);
            }

            string embeddedProvenanceHash =
                ComputeSha256(embeddedProvenance);
            if (manifest == null ||
                provenance == null ||
                !string.Equals(
                    ComputeSha256(provenancePath),
                    embeddedProvenanceHash,
                    StringComparison.OrdinalIgnoreCase) ||
                !ManifestValueEquals(
                    manifest, "qtBuildVersion", RequiredQtBuildVersion) ||
                !ManifestValueEquals(
                    manifest, "pinnedRuntimeRelease", RequiredRuntimeRelease) ||
                !ManifestValueEquals(
                    manifest,
                    "coreRuntimeCompatibility",
                    RequiredCoreRuntimeCompatibility) ||
                !ManifestValueEquals(
                    manifest,
                    "sourceProvenanceSha256",
                    embeddedProvenanceHash) ||
                !ManifestValueEquals(
                    manifest,
                    "headlessExecutableSha256",
                    ComputeSha256(corePath)) ||
                !ManifestMatchesProvenance(
                    manifest, provenance, "headlessExecutableSha256") ||
                !ManifestMatchesProvenance(
                    manifest, provenance, "uxplayWindowsCommit") ||
                !ManifestMatchesProvenance(
                    manifest, provenance, "libuxplayCommit") ||
                !ManifestMatchesProvenance(
                    manifest, provenance, "uxplayWindowsPatchSha256") ||
                !ManifestMatchesProvenance(
                    manifest, provenance, "libuxplayPatchSha256") ||
                !ManifestMapMatchesProvenance(
                    manifest, provenance, "patchedSources") ||
                !ManifestMapMatchesProvenance(
                    manifest, provenance, "buildInputs"))
            {
                throw new InvalidOperationException(
                    "Headless core не соответствует закреплённым " +
                    "исходникам и runtime " + RequiredRuntimeRelease + ".");
            }
        }

        private static bool ManifestValueEquals(
            Dictionary<string, object> manifest,
            string name,
            string expected)
        {
            object value;
            return manifest.TryGetValue(name, out value) &&
                string.Equals(
                    value as string,
                    expected,
                    StringComparison.Ordinal);
        }

        private static bool ManifestMatchesProvenance(
            Dictionary<string, object> manifest,
            Dictionary<string, object> provenance,
            string name)
        {
            object manifestValue;
            object provenanceValue;
            return manifest.TryGetValue(name, out manifestValue) &&
                provenance.TryGetValue(name, out provenanceValue) &&
                string.Equals(
                    manifestValue as string,
                    provenanceValue as string,
                    StringComparison.Ordinal);
        }

        private static bool ManifestMapMatchesProvenance(
            Dictionary<string, object> manifest,
            Dictionary<string, object> provenance,
            string name)
        {
            object manifestValue;
            object provenanceValue;
            if (!manifest.TryGetValue(name, out manifestValue) ||
                !provenance.TryGetValue(name, out provenanceValue))
                return false;

            var manifestMap = manifestValue as Dictionary<string, object>;
            var provenanceMap = provenanceValue as Dictionary<string, object>;
            if (manifestMap == null ||
                provenanceMap == null ||
                manifestMap.Count != provenanceMap.Count)
                return false;

            foreach (KeyValuePair<string, object> pair in provenanceMap)
            {
                object actual;
                if (!manifestMap.TryGetValue(pair.Key, out actual) ||
                    !string.Equals(
                        actual as string,
                        pair.Value as string,
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static void VerifyCoreLoaderCompatibility(string core)
        {
            string executable = Path.Combine(core, "uxplay-windows.exe");
            var start = new ProcessStartInfo();
            start.FileName = executable;
            start.Arguments = "--loader-test";
            start.WorkingDirectory = core;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;

            try
            {
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                        throw new InvalidOperationException(
                            "Не удалось запустить проверку совместимости core.");
                    if (!process.WaitForExit(15000))
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                        catch { }
                        throw new TimeoutException(
                            "Проверка совместимости core не завершилась за 15 секунд.");
                    }

                    int exitCode = process.ExitCode;
                    if (exitCode != 0)
                    {
                        string code = "0x" +
                            unchecked((uint)exitCode).ToString("X8");
                        if (exitCode == unchecked((int)0xC0000139))
                        {
                            throw new InvalidOperationException(
                                "Core несовместим с runtime: отсутствует " +
                                "требуемая точка входа (" + code + ").");
                        }
                        throw new InvalidOperationException(
                            "Проверка совместимости core завершилась с кодом " +
                            code + ".");
                    }
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Не удалось проверить совместимость core с runtime.", ex);
            }
        }

        private static string FindRuntimeRoot(string extracted)
        {
            string direct = Path.Combine(extracted, "uxplay-windows.exe");
            if (File.Exists(direct))
                return extracted;
            foreach (string candidate in Directory.GetFiles(
                extracted, "uxplay-windows.exe", SearchOption.AllDirectories))
            {
                return Path.GetDirectoryName(candidate);
            }
            return null;
        }

        private static string ComputeSha256(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static string ComputeSha256(byte[] data)
        {
            using (var algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(data);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        internal static bool IsPathWithinDirectory(
            string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
                return false;
            try
            {
                string normalizedDirectory = Path.GetFullPath(directory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                string normalizedPath = Path.GetFullPath(path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                return string.Equals(
                    normalizedPath,
                    normalizedDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(
                        normalizedDirectory + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        internal static int CalculateWaitMilliseconds(
            long elapsedMilliseconds,
            int timeoutMilliseconds,
            int maximumWaitMilliseconds)
        {
            long remaining = (long)timeoutMilliseconds -
                Math.Max(0L, elapsedMilliseconds);
            if (remaining <= 0 || maximumWaitMilliseconds <= 0)
                return 0;
            return (int)Math.Min(
                remaining,
                (long)maximumWaitMilliseconds);
        }

        private static void MoveInstallDirectoryToBackup(
            string installDirectory, string backupDirectory)
        {
            SetupLog.Write(
                "Preparing existing installation for replacement. Source=\"" +
                installDirectory + "\"; Backup=\"" + backupDirectory +
                "\"; CurrentDirectory=\"" + Environment.CurrentDirectory +
                "\"; SetupExecutable=\"" +
                Assembly.GetExecutingAssembly().Location + "\".");
            ExecuteWithBoundedIoRetry(
                delegate
                {
                    Directory.Move(installDirectory, backupDirectory);
                },
                InstallDirectoryMoveTimeoutMilliseconds,
                InstallDirectoryMoveRetryDelayMilliseconds,
                "Move existing installation to backup",
                "Не удалось подготовить предыдущую версию AeroMirror к обновлению: " +
                "её файлы всё ещё используются другим процессом. " +
                "Закройте окна AeroMirror и повторите обновление.\r\n\r\n" +
                "Журнал установки: " + SetupLog.Path,
                delegate
                {
                    SetupLog.Write(
                        "Rechecking installed processes after the first " +
                        "directory-move failure.");
                    StopInstalledProcesses(installDirectory);
                });
        }

        private static void ExecuteWithBoundedIoRetry(
            Action operation,
            int timeoutMilliseconds,
            int retryDelayMilliseconds,
            string description,
            string failureMessage,
            Action firstRetryRecovery)
        {
            var clock = Stopwatch.StartNew();
            int attempt = 0;
            Exception lastError = null;
            while (true)
            {
                if (lastError != null &&
                    clock.ElapsedMilliseconds >= timeoutMilliseconds)
                {
                    SetupLog.Write(
                        description + " failed after " + attempt +
                        " attempt(s) and " + clock.ElapsedMilliseconds +
                        " ms. LastError=" + lastError);
                    throw new IOException(failureMessage, lastError);
                }

                attempt++;
                try
                {
                    operation();
                    SetupLog.Write(
                        description + " succeeded on attempt " + attempt +
                        " after " + clock.ElapsedMilliseconds + " ms.");
                    return;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastError = ex;
                }

                if (attempt == 1 && firstRetryRecovery != null)
                {
                    try
                    {
                        firstRetryRecovery();
                    }
                    catch (Exception ex)
                    {
                        SetupLog.Write(
                            description +
                            " recovery after the first failure did not " +
                            "complete: " + ex);
                        throw new IOException(failureMessage, ex);
                    }
                }

                int waitMilliseconds = CalculateWaitMilliseconds(
                    clock.ElapsedMilliseconds,
                    timeoutMilliseconds,
                    retryDelayMilliseconds);
                SetupLog.Write(
                    description + " attempt " + attempt +
                    " failed with " + lastError.GetType().Name + ": " +
                    lastError.Message + "; RetryDelayMs=" +
                    waitMilliseconds + ".");
                if (waitMilliseconds <= 0)
                    throw new IOException(failureMessage, lastError);
                Thread.Sleep(waitMilliseconds);
            }
        }

        internal static void StopInstalledProcesses(string installDirectory)
        {
            string normalizedInstallDirectory =
                Path.GetFullPath(installDirectory);
            int setupPid = Process.GetCurrentProcess().Id;
            var clock = Stopwatch.StartNew();
            SetupLog.Write(
                "Stopping processes from installation tree. Directory=\"" +
                normalizedInstallDirectory + "\"; SetupPid=" + setupPid +
                "; DeadlineMs=" + ProcessStopTimeoutMilliseconds + ".");
            while (true)
            {
                bool foundInstalledProcess = false;
                foreach (Process process in Process.GetProcesses())
                {
                    using (process)
                    {
                        int processId;
                        try
                        {
                            processId = process.Id;
                            if (processId == setupPid)
                                continue;
                        }
                        catch (InvalidOperationException)
                        {
                            continue;
                        }

                        string executablePath;
                        try
                        {
                            executablePath = process.MainModule.FileName;
                        }
                        catch (InvalidOperationException)
                        {
                            continue;
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            continue;
                        }
                        catch (NotSupportedException)
                        {
                            continue;
                        }
                        if (!IsPathWithinDirectory(
                            executablePath, normalizedInstallDirectory))
                            continue;

                        foundInstalledProcess = true;
                        try
                        {
                            StopInstalledProcess(
                                process,
                                executablePath,
                                clock,
                                ProcessStopTimeoutMilliseconds);
                        }
                        catch (InvalidOperationException)
                        {
                            SetupLog.Write(
                                "Installed process exited during inspection. " +
                                "Pid=" + processId + "; Executable=\"" +
                                executablePath + "\".");
                        }
                    }
                }
                if (!foundInstalledProcess)
                {
                    SetupLog.Write(
                        "No running process remains in the installation tree. " +
                        "ElapsedMs=" + clock.ElapsedMilliseconds + ".");
                    return;
                }

                int pauseMilliseconds = CalculateWaitMilliseconds(
                    clock.ElapsedMilliseconds,
                    ProcessStopTimeoutMilliseconds,
                    150);
                if (pauseMilliseconds <= 0)
                {
                    // Every path-scoped process found in this scan was already
                    // confirmed stopped by StopInstalledProcess. Do not turn a
                    // successful final bounded wait into a false timeout merely
                    // because there is no time left for another discovery pass.
                    SetupLog.Write(
                        "Process-stop deadline reached after all discovered " +
                        "installation processes exited. Proceeding with the " +
                        "directory move; elapsedMs=" +
                        clock.ElapsedMilliseconds + ".");
                    return;
                }
                Thread.Sleep(pauseMilliseconds);
            }
        }

        private static void StopInstalledProcess(
            Process process,
            string executablePath,
            Stopwatch clock,
            int timeoutMilliseconds)
        {
            int processId = process.Id;
            string processName = process.ProcessName;
            SetupLog.Write(
                "Installed process found. Pid=" + processId +
                "; Name=\"" + processName + "\"; Executable=\"" +
                executablePath + "\"; ElapsedMs=" +
                clock.ElapsedMilliseconds + ".");

            bool exited = process.HasExited;
            if (!exited)
            {
                bool closeRequested = process.CloseMainWindow();
                if (closeRequested)
                {
                    int gracefulWait = CalculateWaitMilliseconds(
                        clock.ElapsedMilliseconds,
                        timeoutMilliseconds,
                        GracefulProcessStopMilliseconds);
                    SetupLog.Write(
                        "Graceful close requested. Pid=" + processId +
                        "; WaitMs=" + gracefulWait + ".");
                    if (gracefulWait > 0)
                        exited = process.WaitForExit(gracefulWait);
                }
            }

            if (!exited)
            {
                int forcedWait = CalculateWaitMilliseconds(
                    clock.ElapsedMilliseconds,
                    timeoutMilliseconds,
                    int.MaxValue);
                if (forcedWait <= 0)
                {
                    throw CreateProcessStopTimeoutException(
                        processName, executablePath, clock);
                }

                SetupLog.Write(
                    "Terminating installed process. Pid=" + processId +
                    "; Name=\"" + processName + "\"; RemainingMs=" +
                    forcedWait + ".");
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    exited = true;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    SetupLog.Write(
                        "Failed to terminate installed process. Pid=" +
                        processId + "; Executable=\"" + executablePath +
                        "\"; Error=" + ex);
                    throw new IOException(
                        "Не удалось остановить процесс AeroMirror " +
                        processName + ".",
                        ex);
                }

                if (!exited)
                {
                    forcedWait = CalculateWaitMilliseconds(
                        clock.ElapsedMilliseconds,
                        timeoutMilliseconds,
                        int.MaxValue);
                    if (forcedWait > 0)
                        exited = process.WaitForExit(forcedWait);
                }
            }

            if (!exited)
            {
                throw CreateProcessStopTimeoutException(
                    processName, executablePath, clock);
            }
            SetupLog.Write(
                "Installed process stopped. Pid=" + processId +
                "; Name=\"" + processName + "\"; ElapsedMs=" +
                clock.ElapsedMilliseconds + ".");
        }

        private static IOException CreateProcessStopTimeoutException(
            string processName,
            string executablePath,
            Stopwatch clock)
        {
            SetupLog.Write(
                "Installed process stop deadline expired. Name=\"" +
                (processName ?? "unknown") + "\"; Executable=\"" +
                (executablePath ?? "unknown") + "\"; ElapsedMs=" +
                clock.ElapsedMilliseconds + ".");
            return new IOException(
                "Не удалось вовремя остановить процессы AeroMirror. " +
                "Закройте приложение и повторите попытку.");
        }

        internal static void RemoveShortcuts()
        {
            TryDeleteFile(InstallPaths.StartMenuShortcut);
            TryDeleteFile(InstallPaths.DesktopShortcut);
            TryDeleteFile(InstallPaths.LegacyStartMenuShortcut);
            TryDeleteFile(InstallPaths.LegacyDesktopShortcut);
        }

        internal static void RemoveRegistryEntries(string installDirectory)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            }
            catch { }

            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(
                    RunKey, true))
                {
                    if (run != null)
                    {
                        string[] valueNames =
                        {
                            "AeroMirror",
                            "AirPlayReceiverMvp"
                        };
                        foreach (string valueName in valueNames)
                        {
                            string value = run.GetValue(valueName) as string;
                            if (!string.IsNullOrWhiteSpace(value) &&
                                value.IndexOf(
                                    installDirectory,
                                    StringComparison.OrdinalIgnoreCase) >= 0)
                                run.DeleteValue(valueName, false);
                        }
                    }
                }
            }
            catch { }
        }

        internal static void RemoveRuntimeCache()
        {
            TryDeleteDirectory(RuntimeCacheDirectory);
            if (Directory.Exists(RuntimeCacheDirectory))
                SetupLog.Write(
                    "Runtime cache could not be fully removed during uninstall.");
        }

        private static void WriteUninstallRegistry(
            string executable, string uninstaller)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "AeroMirror");
                key.SetValue(
                    "DisplayVersion", SetupForm.SetupVersion.ToString(3));
                key.SetValue("Publisher", "AeroMirror open-source project");
                key.SetValue("InstallLocation", InstallPaths.InstallDirectory);
                key.SetValue("DisplayIcon", executable);
                key.SetValue("UninstallString",
                    "\"" + uninstaller + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long bytes = DirectorySize(InstallPaths.InstallDirectory);
                key.SetValue("EstimatedSize",
                    (int)Math.Min(int.MaxValue, Math.Max(1, bytes / 1024)),
                    RegistryValueKind.DWord);
            }
        }

        private static void MigrateAutostart(string executable)
        {
            try
            {
                using (RegistryKey run = Registry.CurrentUser.CreateSubKey(
                    RunKey))
                {
                    bool enabled =
                        run.GetValue("AeroMirror") != null ||
                        run.GetValue("AirPlayReceiverMvp") != null;
                    if (enabled)
                    {
                        run.SetValue(
                            "AeroMirror",
                            "\"" + executable + "\" --startup",
                            RegistryValueKind.String);
                    }
                    run.DeleteValue("AirPlayReceiverMvp", false);
                }
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException(
                    "Windows Script Host недоступен для создания ярлыка.");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember(
                    "TargetPath",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { target });
                shortcutType.InvokeMember(
                    "WorkingDirectory",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { Path.GetDirectoryName(target) });
                shortcutType.InvokeMember(
                    "IconLocation",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { target + ",0" });
                shortcutType.InvokeMember(
                    "Save",
                    BindingFlags.InvokeMethod,
                    null,
                    shortcut,
                    null);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                    Marshal.FinalReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell))
                    Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void MoveOrCopyDirectory(string source, string destination)
        {
            try
            {
                Directory.Move(source, destination);
            }
            catch (IOException)
            {
                CopyDirectory(source, destination);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static long DirectorySize(string path)
        {
            long total = 0;
            foreach (string file in Directory.GetFiles(
                path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch { }
            }
            return total;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }
    }

    internal sealed class PinnedDownloadClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 300000;
            HttpWebRequest http = request as HttpWebRequest;
            if (http != null)
            {
                http.ReadWriteTimeout = 300000;
                http.AllowAutoRedirect = true;
            }
            return request;
        }
    }
}

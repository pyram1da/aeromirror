using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("AeroMirror Setup")]
[assembly: AssemblyProduct("AeroMirror")]
[assembly: AssemblyCompany("AeroMirror open-source project")]
[assembly: AssemblyVersion("0.12.19.0")]
[assembly: AssemblyFileVersion("0.12.19.0")]

namespace AirPlayReceiverSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
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

            bool updateRequested = HasArgument(args, "/update");
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
            ScheduleSourceDeletion(args);
            if (args.Length > 0 &&
                string.Equals(
                    args[0], "/install-silent",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ShortcutSelection shortcuts =
                        InstallerOperations.GetShortcutSelection(true);
                    SetupLog.Write("Silent installation started.");
                    InstallerOperations.Install(
                        shortcuts.StartMenu, shortcuts.Desktop);
                    SetupLog.Write("Silent installation completed successfully.");
                }
                catch (Exception ex)
                {
                    SetupLog.Write("Silent installation failed: " + ex);
                    Environment.ExitCode = 2;
                }
                return;
            }

            if (args.Length > 0 &&
                string.Equals(args[0], "/uninstall-worker", StringComparison.OrdinalIgnoreCase))
            {
                UninstallWorker(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 &&
                string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                BeginUninstall();
                return;
            }

            Version installedVersion = InstallerOperations.GetInstalledVersion();
            if (InstallerOperations.ShouldRunAutomaticInstall(
                    updateRequested, installedVersion, SetupForm.SetupVersion))
            {
                RunAutomaticInstall(updateRequested);
                return;
            }

            Application.Run(new SetupForm(updateRequested));
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

        private static void RunAutomaticInstall(bool updateRequested)
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
                MessageBox.Show(
                    "Не удалось автоматически " +
                    (updateRequested ? "обновить" : "переустановить") +
                    " AeroMirror.\r\n\r\n" + ex.Message,
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(executable)
                {
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
        internal static readonly Version SetupVersion = new Version(0, 12, 19);
        private readonly CheckBox startMenu;
        private readonly CheckBox desktop;
        private readonly CheckBox launch;
        private readonly Button install;
        private readonly ProgressBar progress;
        private readonly Label state;
        private readonly Version installedVersion;
        private readonly bool updateRequested;

        public SetupForm(bool updateRequested)
        {
            this.updateRequested = updateRequested;
            installedVersion = InstallerOperations.GetInstalledVersion();
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
                : installedVersion.CompareTo(SetupVersion) < 0
                ? "Обновление " + installedVersion.ToString(3) +
                    " → " + SetupVersion.ToString(3) +
                    " · настройки сохранятся"
                : installedVersion.CompareTo(SetupVersion) == 0
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
                : installedVersion.CompareTo(SetupVersion) < 0
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
            if (installedVersion != null &&
                installedVersion.CompareTo(SetupVersion) > 0)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "На компьютере установлена более новая версия " +
                    installedVersion.ToString(3) +
                    ".\r\n\r\nУстановить более старую версию " +
                    SetupVersion.ToString(3) + "?",
                    "AeroMirror",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                    return;
            }

            install.Enabled = false;
            startMenu.Enabled = false;
            desktop.Enabled = false;
            launch.Enabled = false;
            progress.Visible = true;
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
                    string executable = InstallerOperations.Install(
                        createStartMenu, createDesktop);
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
                                Close();
                            }
                            catch (Exception launchError)
                            {
                                SetupLog.Write(
                                    "Launching AeroMirror after installation failed: " +
                                    launchError);
                                Show();
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
            try
            {
                using (RegistryKey key =
                    Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string value = key == null
                        ? null : key.GetValue("DisplayVersion") as string;
                    Version version;
                    if (Version.TryParse(value, out version))
                        return version;
                }
            }
            catch { }

            try
            {
                string[] executableNames =
                {
                    "AeroMirror.exe",
                    "AirPlayReceiverMvp.exe"
                };
                foreach (string executableName in executableNames)
                {
                    string executable = Path.Combine(
                        InstallPaths.InstallDirectory, executableName);
                    if (File.Exists(executable))
                    {
                        string value = FileVersionInfo.GetVersionInfo(
                            executable).FileVersion;
                        Version version;
                        if (Version.TryParse(value, out version))
                            return version;
                    }
                }
            }
            catch { }
            return null;
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
            if (installedVersion != null &&
                installedVersion.CompareTo(setupVersion) > 0)
                return false;
            return updateRequested || installedVersion != null;
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
            AssertAutomaticInstall(
                ShouldRunAutomaticInstall(
                    true, new Version(
                        SetupForm.SetupVersion.Major,
                        SetupForm.SetupVersion.Minor,
                        SetupForm.SetupVersion.Build + 1),
                    SetupForm.SetupVersion),
                false, "automatic downgrade prevention");
        }

        internal static void VerifyUpdateLifecycleLogic()
        {
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
                    return executable;
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

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
    internal sealed class SettingsForm : Form
    {
        private readonly ReceiverContext context;
        private readonly Panel homePage;
        private readonly Panel settingsPage;
        private readonly Panel advancedPage;
        private readonly Panel updatesPage;
        private readonly Label status;
        private readonly Label statusDot;
        private readonly Panel networkCard;
        private readonly Label networkTitle;
        private readonly NetworkHelpGlyph networkHelp;
        private readonly Label bonjourFirewallNotice;
        private readonly Button bonjourFirewallRepair;
        private readonly Panel trustCard;
        private readonly Button refreshDiscovery;
        private readonly Button settingsButton;
        private readonly Button updatesButton;
        private readonly LinkLabel reportProblem;
        private readonly Label reportStatus;
        private readonly System.Windows.Forms.Timer reportStatusTimer;
        private readonly Label homeQuality;
        private readonly ToolTip toolTips;
        private readonly TextBox receiverName;
        private readonly ComboBox quality;
        private readonly ComboBox pairing;
        private readonly Label accessNote;
        private readonly Panel pinPanel;
        private readonly TextBox fixedPin;
        private readonly ComboBox latency;
        private readonly ComboBox audioOutput;
        private readonly ComboBox theme;
        private readonly CheckBox topMost;
        private readonly CheckBox autoFit;
        private readonly CheckBox showStreamInTaskbar;
        private readonly CheckBox autoReceiver;
        private readonly CheckBox autoWindows;
        private readonly CheckBox startMinimized;
        private readonly CheckBox closeToTray;
        private readonly CheckBox notifications;
        private readonly Button startStop;
        private readonly Button saveButton;
        private readonly Label savedLabel;
        private readonly ComboBox renderer;
        private readonly TextBox arguments;
        private readonly TextBox argumentPreview;
        private readonly Button advancedSave;
        private readonly Label updateState;
        private readonly Label updateTitle;
        private readonly TextBox updateNotes;
        private readonly Button checkUpdate;
        private readonly Button installUpdate;
        private readonly Button openRelease;
        private readonly Button updatesBack;
        private UpdateInfo availableUpdate;
        private string pendingInstallerPath = "";
        private bool suppressDirty;
        private bool? appliedDarkTheme;
        private DateTime nextThemeCheck;
        private bool homePageSelected;

        public SettingsForm(ReceiverContext context)
        {
            this.context = context;
            Text = "AeroMirror";
            Icon = AppIcon.Current;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(620, 430);
            MinimumSize = new Size(636, 300);
            BackColor = Color.FromArgb(247, 247, 247);
            Font = new Font("Segoe UI", 9.5F);

            homePage = new Panel();
            homePage.Dock = DockStyle.Fill;
            homePage.BackColor = BackColor;
            Controls.Add(homePage);

            toolTips = new ToolTip();
            toolTips.AutoPopDelay = 12000;
            toolTips.InitialDelay = 350;
            toolTips.ReshowDelay = 100;
            toolTips.ShowAlways = true;

            reportStatusTimer = new System.Windows.Forms.Timer();
            reportStatusTimer.Interval = 8000;
            reportStatusTimer.Tick += delegate
            {
                reportStatusTimer.Stop();
                reportStatus.Text = "";
            };

            var homeHeader = new Panel();
            homeHeader.Dock = DockStyle.Top;
            homeHeader.Height = 96;
            homeHeader.BackColor = Color.White;
            homePage.Controls.Add(homeHeader);

            var logo = new PictureBox();
            logo.Image = AppIcon.Image;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Location = new Point(24, 20);
            logo.Size = new Size(52, 52);
            homeHeader.Controls.Add(logo);

            var title = MakeLabel("AeroMirror", 91, 15);
            title.Font = new Font("Segoe UI Semibold", 19F);
            homeHeader.Controls.Add(title);

            statusDot = MakeLabel("●", 92, 51);
            statusDot.AutoSize = false;
            statusDot.Size = new Size(16, 24);
            statusDot.Font = new Font("Segoe UI", 11F);
            statusDot.AccessibleName = "Состояние приёмника";
            statusDot.Cursor = Cursors.Help;
            homeHeader.Controls.Add(statusDot);

            status = MakeLabel("", 112, 53);
            status.AutoSize = false;
            status.Size = new Size(1, 24);
            status.AutoEllipsis = true;
            status.Font = new Font("Segoe UI Semibold", 10.5F);
            status.Cursor = Cursors.Help;
            homeHeader.Controls.Add(status);

            settingsButton = MakeButton(
                "\uE713", 552, 28, 44, 36, false);
            settingsButton.Font = new Font("Segoe MDL2 Assets", 14F);
            settingsButton.AccessibleName = "Настройки";
            settingsButton.Click += delegate { ShowSettingsPage(false); };
            toolTips.SetToolTip(settingsButton, "Настройки");
            homeHeader.Controls.Add(settingsButton);

            networkCard = new Panel();
            networkCard.Location = new Point(24, 112);
            networkCard.Size = new Size(572, 46);
            homePage.Controls.Add(networkCard);

            networkTitle = MakeLabel("", 16, 11);
            networkTitle.AutoSize = false;
            networkTitle.Size = new Size(506, 24);
            networkTitle.AutoEllipsis = true;
            networkTitle.TextAlign = ContentAlignment.MiddleLeft;
            networkTitle.Font = new Font("Segoe UI Semibold", 9.5F);
            networkCard.Controls.Add(networkTitle);

            networkHelp = new NetworkHelpGlyph();
            networkHelp.Location = new Point(536, 11);
            networkHelp.AccessibleName = "Подробнее о проверке сети";
            networkCard.Controls.Add(networkHelp);

            bonjourFirewallNotice = MakeFixedLabel("", 16, 41, 336, 42);
            bonjourFirewallNotice.Font = new Font(
                "Segoe UI Semibold", 9.25F, FontStyle.Regular);
            bonjourFirewallNotice.Visible = false;
            networkCard.Controls.Add(bonjourFirewallNotice);

            bonjourFirewallRepair = MakeButton(
                "Разрешить Bonjour", 372, 43, 180, 34, true);
            bonjourFirewallRepair.AccessibleName =
                "Разрешить обнаружение AirPlay через Bonjour";
            bonjourFirewallRepair.Visible = false;
            bonjourFirewallRepair.Click += delegate
            {
                context.RepairBonjourFirewall(this);
                SyncStatus();
            };
            toolTips.SetToolTip(
                bonjourFirewallRepair,
                "Добавить только узкое правило Private UDP 5353 для Bonjour. Windows покажет одно подтверждение администратора.");
            networkCard.Controls.Add(bonjourFirewallRepair);

            trustCard = new Panel();
            trustCard.Location = new Point(24, 178);
            trustCard.Size = new Size(572, 94);
            trustCard.BackColor = Color.FromArgb(242, 247, 253);
            homePage.Controls.Add(trustCard);

            var trustTitle = MakeLabel(
                "Подключайтесь безопасно в любой знакомой и новой сети", 15, 10);
            trustTitle.Font = new Font("Segoe UI Semibold", 9.5F);
            trustCard.Controls.Add(trustTitle);

            var trustText = new Label();
            trustText.Text =
                "Настройте PIN один раз: iPhone и ноутбук сохранят ключи друг друга.\r\n" +
                "После этого смена домашнего Wi-Fi сама по себе не потребует нового PIN.";
            trustText.AutoSize = false;
            trustText.Size = new Size(405, 48);
            trustText.Location = new Point(15, 37);
            trustText.ForeColor = Color.FromArgb(55, 55, 55);
            trustCard.Controls.Add(trustText);

            var dismissPinSuggestion = MakeButton("×", 531, 7, 26, 26, false);
            dismissPinSuggestion.FlatAppearance.BorderSize = 0;
            dismissPinSuggestion.Click += delegate
            {
                AppSettings updated = context.CurrentSettings.Copy();
                updated.DismissPinSuggestion = true;
                context.SaveSettings(updated, false);
                SyncStatus();
            };
            trustCard.Controls.Add(dismissPinSuggestion);

            var pinSetup = MakeButton("Настроить PIN", 425, 48, 130, 34, false);
            pinSetup.Click += delegate { ShowSettingsPage(true); };
            trustCard.Controls.Add(pinSetup);

            refreshDiscovery = MakeButton(
                "Перезапустить обнаружение", 24, 314, 224, 38, false);
            refreshDiscovery.Click += delegate
            {
                context.RefreshDiscovery();
                SyncStatus();
            };
            homePage.Controls.Add(refreshDiscovery);

            startStop = MakeButton("", 260, 314, 145, 38, true);
            startStop.Click += delegate
            {
                if (context.IsCoreRunning) context.StopCore(); else context.StartCore();
                SyncStatus();
            };
            homePage.Controls.Add(startStop);

            updatesButton = MakeButton(
                "Обновления", 417, 314, 179, 38, false);
            updatesButton.Click += delegate { ShowUpdatesPage(); };
            homePage.Controls.Add(updatesButton);

            homeQuality = MakeLabel("", 26, 365);
            homeQuality.AutoSize = false;
            homeQuality.Size = new Size(570, 22);
            homeQuality.ForeColor = Color.DimGray;
            homePage.Controls.Add(homeQuality);

            reportProblem = new LinkLabel();
            reportProblem.Text = "Сообщить о проблеме";
            reportProblem.AutoSize = true;
            reportProblem.Location = new Point(452, 398);
            reportProblem.LinkClicked += delegate
            {
                reportStatusTimer.Stop();
                reportProblem.Enabled = false;
                reportStatus.Text = "Подготавливаем обезличенный журнал…";
                reportStatus.Refresh();
                bool opened = context.OpenProblemReport(this);
                reportStatus.Text = opened
                    ? "Файл выделен в папке · GitHub откроется следом"
                    : "Не удалось подготовить журнал";
                reportProblem.Enabled = true;
                reportStatusTimer.Start();
            };
            toolTips.SetToolTip(
                reportProblem,
                "Подготовит обезличенный журнал, затем откроет папку и форму GitHub.");
            homePage.Controls.Add(reportProblem);

            reportStatus = MakeLabel("", 26, 398);
            reportStatus.AutoSize = false;
            reportStatus.Size = new Size(414, 22);
            reportStatus.ForeColor = Color.DimGray;
            homePage.Controls.Add(reportStatus);

            settingsPage = new Panel();
            settingsPage.Dock = DockStyle.Fill;
            settingsPage.AutoScroll = true;
            settingsPage.BackColor = BackColor;
            settingsPage.Visible = false;
            Controls.Add(settingsPage);

            var settingsContent = new Panel();
            settingsContent.Location = new Point(0, 0);
            settingsContent.Size = new Size(600, 1090);
            settingsContent.BackColor = BackColor;
            settingsPage.Controls.Add(settingsContent);

            var back = MakeBackButton(20, 14);
            back.Click += delegate { TryLeaveSettingsToHome(); };
            settingsContent.Controls.Add(back);

            var settingsTitle = MakeLabel("Настройки", 138, 17);
            settingsTitle.Font = new Font("Segoe UI Semibold", 18F);
            settingsContent.Controls.Add(settingsTitle);

            AddSection(settingsContent, "Видео и звук", 24, 72);

            settingsContent.Controls.Add(MakeLabel(
                "Имя в меню «Повтор экрана»", 24, 108));
            receiverName = new TextBox();
            receiverName.Location = new Point(24, 130);
            receiverName.Size = new Size(552, 27);
            settingsContent.Controls.Add(receiverName);

            settingsContent.Controls.Add(MakeLabel("Качество трансляции", 24, 174));
            quality = new WheelSafeComboBox();
            quality.Location = new Point(24, 196);
            quality.Size = new Size(552, 42);
            quality.DropDownStyle = ComboBoxStyle.DropDownList;
            quality.FlatStyle = FlatStyle.Flat;
            quality.DrawMode = DrawMode.OwnerDrawFixed;
            quality.ItemHeight = 38;
            quality.DropDownHeight = 160;
            quality.IntegralHeight = false;
            quality.DrawItem += DrawQualityItem;
            quality.Items.Add(new NamedValue(
                "4K · 60 FPS", "4k60",
                "HEVC · максимальный запрос; фактическое разрешение выбирает iPhone"));
            quality.Items.Add(new NamedValue(
                "Full HD · 60 FPS", "1080p60",
                "Рекомендуется · плавное движение"));
            quality.Items.Add(new NamedValue(
                "Full HD · 30 FPS", "1080p30",
                "Меньше нагрузка на сеть и компьютер"));
            quality.Items.Add(new NamedValue(
                "HD · 30 FPS", "720p30",
                "Для слабой сети или маломощного компьютера"));
            settingsContent.Controls.Add(quality);

            var qualityNote = MakeFixedLabel(
                "Качество применяется после сохранения и нового подключения iPhone.",
                27, 241, 548, 30);
            qualityNote.ForeColor = Color.DimGray;
            settingsContent.Controls.Add(qualityNote);

            settingsContent.Controls.Add(MakeLabel("Профиль задержки", 24, 276));
            latency = MakeCombo(24, 298, 552);
            latency.Items.Add(new NamedValue(
                "Сбалансированный — рекомендуется", "balanced"));
            latency.Items.Add(new NamedValue(
                "Интерактивный — плавнее движение, возможен рассинхрон звука",
                "low"));
            latency.Items.Add(new NamedValue(
                "Стабильный — больше буфер и заметнее задержка", "stable"));
            settingsContent.Controls.Add(latency);

            settingsContent.Controls.Add(MakeLabel("Вывод звука", 24, 340));
            audioOutput = MakeCombo(24, 362, 552);
            audioOutput.Items.Add(new NamedValue(
                "Системное устройство Windows по умолчанию", "default"));
            audioOutput.Items.Add(new NamedValue(
                "Без звука на компьютере", "mute"));
            settingsContent.Controls.Add(audioOutput);

            topMost = MakeCheckBox(
                "Показывать окно трансляции поверх остальных окон", 24, 406);
            settingsContent.Controls.Add(topMost);

            autoFit = MakeCheckBox(
                "Автоматически сохранять пропорции окна трансляции", 24, 438);
            settingsContent.Controls.Add(autoFit);

            showStreamInTaskbar = MakeCheckBox(
                "Показывать окно трансляции на панели задач", 24, 470);
            settingsContent.Controls.Add(showStreamInTaskbar);

            AddSection(settingsContent, "Защита подключения", 24, 516);

            pairing = MakeCombo(24, 550, 552);
            pairing.Items.Add(new NamedValue(
                "Без PIN — удобно для частной домашней сети", "none"));
            pairing.Items.Add(new NamedValue(
                "С PIN — один раз для доверия этому iPhone", "pin"));
            pairing.SelectedIndexChanged += delegate { UpdatePinPanel(); };
            settingsContent.Controls.Add(pairing);

            accessNote = MakeFixedLabel("", 27, 585, 548, 60);
            accessNote.ForeColor = Color.DimGray;
            settingsContent.Controls.Add(accessNote);

            pinPanel = new Panel();
            pinPanel.Location = new Point(24, 584);
            pinPanel.Size = new Size(552, 76);
            pinPanel.BackColor = Color.FromArgb(237, 246, 255);
            settingsContent.Controls.Add(pinPanel);

            pinPanel.Controls.Add(MakeLabel(
                "PIN, который нужно ввести на iPhone", 13, 7));
            fixedPin = new TextBox();
            fixedPin.Location = new Point(15, 28);
            fixedPin.Size = new Size(130, 25);
            fixedPin.MaxLength = 4;
            fixedPin.Font = new Font("Segoe UI Semibold", 10F);
            pinPanel.Controls.Add(fixedPin);

            var generate = MakeButton("Новый PIN", 154, 27, 105, 28, false);
            generate.Click += delegate { fixedPin.Text = GeneratePin(); };
            pinPanel.Controls.Add(generate);

            var pinNote = MakeFixedLabel(
                "Ключ хранится для пары iPhone + ноутбук,\r\nа не для конкретной Wi-Fi-сети.",
                276, 25, 260, 43);
            pinNote.ForeColor = Color.DimGray;
            pinPanel.Controls.Add(pinNote);

            AddSection(settingsContent, "Запуск и поведение приложения", 24, 682);

            settingsContent.Controls.Add(MakeLabel("Цветовая тема", 24, 718));
            theme = MakeCombo(24, 740, 552);
            theme.Items.Add(new NamedValue(
                "Как в Windows", "system"));
            theme.Items.Add(new NamedValue(
                "Светлая", "light"));
            theme.Items.Add(new NamedValue(
                "Тёмная", "dark"));
            theme.SelectedIndexChanged += delegate
            {
                if (!suppressDirty)
                    MarkDirty();
            };
            settingsContent.Controls.Add(theme);

            autoReceiver = MakeCheckBox(
                "Держать приёмник включённым, пока приложение работает", 24, 790);
            settingsContent.Controls.Add(autoReceiver);

            autoWindows = MakeCheckBox(
                "Запускать AeroMirror вместе с Windows", 24, 822);
            autoWindows.CheckedChanged += delegate { UpdateStartupChild(); };
            settingsContent.Controls.Add(autoWindows);

            startMinimized = MakeCheckBox(
                "При запуске с Windows сразу скрывать в трей", 48, 854);
            startMinimized.ForeColor = Color.FromArgb(70, 70, 70);
            settingsContent.Controls.Add(startMinimized);

            closeToTray = MakeCheckBox(
                "По кнопке × сворачивать в трей, а не закрывать приложение", 24, 886);
            settingsContent.Controls.Add(closeToTray);

            notifications = MakeCheckBox(
                "Показывать служебные уведомления приёмника", 24, 918);
            settingsContent.Controls.Add(notifications);

            var notificationsNote = MakeLabel(
                "Это предупреждения об ошибках и безопасности сети — не SMS с iPhone.",
                48, 942);
            notificationsNote.ForeColor = Color.DimGray;
            settingsContent.Controls.Add(notificationsNote);

            var advancedButton = MakeButton(
                "Дополнительные настройки", 24, 908, 220, 36, false);
            advancedButton.Location = new Point(24, 980);
            advancedButton.Click += delegate { ShowAdvancedPage(); };
            settingsContent.Controls.Add(advancedButton);

            savedLabel = MakeLabel("", 24, 976);
            savedLabel.AutoSize = false;
            savedLabel.Size = new Size(552, 22);
            savedLabel.TextAlign = ContentAlignment.MiddleRight;
            savedLabel.ForeColor = Color.FromArgb(42, 122, 74);
            settingsContent.Controls.Add(savedLabel);

            saveButton = MakeButton("Сохранить", 426, 1008, 150, 40, true);
            saveButton.Click += OnSave;
            settingsContent.Controls.Add(saveButton);

            advancedButton.Location = new Point(24, 1010);

            advancedPage = new Panel();
            advancedPage.Dock = DockStyle.Fill;
            advancedPage.BackColor = BackColor;
            advancedPage.Visible = false;
            Controls.Add(advancedPage);

            var advancedBack = MakeBackButton(20, 14);
            advancedBack.Click += delegate { TryLeaveAdvancedToSettings(); };
            advancedPage.Controls.Add(advancedBack);

            var advancedTitle = MakeLabel("Дополнительные настройки", 138, 17);
            advancedTitle.Font = new Font("Segoe UI Semibold", 18F);
            advancedPage.Controls.Add(advancedTitle);

            var advancedWarning = MakeFixedLabel(
                "Эти параметры нужны для совместимости и диагностики. " +
                "Если всё работает, их лучше не менять.",
                24, 67, 552, 42);
            advancedWarning.ForeColor = Color.DimGray;
            advancedPage.Controls.Add(advancedWarning);

            advancedPage.Controls.Add(MakeLabel("Видеорендер", 24, 122));
            renderer = MakeCombo(24, 145, 552);
            renderer.Items.Add(new NamedValue(
                "Direct3D 11 — рекомендуется для стабильной работы", "d3d11"));
            renderer.Items.Add(new NamedValue(
                "Direct3D 12 — экспериментальный декодер и вывод", "d3d12"));
            advancedPage.Controls.Add(renderer);
            advancedPage.Controls.Add(MakeLabel(
                "Дополнительные аргументы UxPlay", 24, 195));
            arguments = new TextBox();
            arguments.Location = new Point(24, 218);
            arguments.Size = new Size(552, 88);
            arguments.Multiline = true;
            arguments.ScrollBars = ScrollBars.Vertical;
            arguments.Font = new Font("Consolas", 9F);
            advancedPage.Controls.Add(arguments);

            var argsNote = MakeFixedLabel(
                "Аргументы добавляются в конец команды и могут переопределить " +
                "обычные настройки приложения.",
                24, 312, 552, 42);
            argsNote.ForeColor = Color.DimGray;
            advancedPage.Controls.Add(argsNote);

            advancedPage.Controls.Add(MakeLabel(
                "Текущие аргументы запуска", 24, 365));
            argumentPreview = new TextBox();
            argumentPreview.Location = new Point(24, 388);
            argumentPreview.Size = new Size(552, 76);
            argumentPreview.Multiline = true;
            argumentPreview.ReadOnly = true;
            argumentPreview.BackColor = Color.White;
            argumentPreview.Font = new Font("Consolas", 8.5F);
            advancedPage.Controls.Add(argumentPreview);

            var hotspot = MakeButton(
                "Временная личная сеть…", 24, 492, 205, 36, false);
            hotspot.Click += delegate { OpenMobileHotspot(this); };
            advancedPage.Controls.Add(hotspot);

            var diagnostics = MakeButton(
                "Диагностика", 244, 492, 145, 36, false);
            diagnostics.Click += delegate
            {
                using (var dialog = new DiagnosticsForm(context.GetDiagnostics()))
                    dialog.ShowDialog(this);
            };
            advancedPage.Controls.Add(diagnostics);

            advancedSave = MakeButton("Сохранить", 426, 488, 150, 40, true);
            advancedSave.Click += OnAdvancedSave;
            advancedPage.Controls.Add(advancedSave);

            updatesPage = new Panel();
            updatesPage.Dock = DockStyle.Fill;
            updatesPage.BackColor = BackColor;
            updatesPage.Visible = false;
            Controls.Add(updatesPage);

            updatesBack = MakeBackButton(20, 14);
            updatesBack.Click += delegate { ShowHomePage(); };
            updatesPage.Controls.Add(updatesBack);

            var updatesTitle = MakeLabel("Обновления", 138, 17);
            updatesTitle.Font = new Font("Segoe UI Semibold", 18F);
            updatesPage.Controls.Add(updatesTitle);

            var currentVersion = MakeLabel(
                "Установлена версия " +
                AppVersion.Display,
                24, 76);
            currentVersion.ForeColor = Color.DimGray;
            updatesPage.Controls.Add(currentVersion);

            checkUpdate = MakeButton(
                "Проверить обновления", 396, 66, 180, 38, true);
            checkUpdate.Click += delegate { CheckForUpdates(); };
            updatesPage.Controls.Add(checkUpdate);

            updateState = MakeFixedLabel(
                "Проверка выполняется только по нажатию. " +
                "Приложение обращается к публичному GitHub Release.",
                24, 116, 552, 42);
            updateState.ForeColor = Color.DimGray;
            updatesPage.Controls.Add(updateState);

            updateTitle = MakeLabel("", 24, 172);
            updateTitle.Font = new Font("Segoe UI Semibold", 14F);
            updatesPage.Controls.Add(updateTitle);

            updateNotes = new TextBox();
            updateNotes.Location = new Point(24, 210);
            updateNotes.Size = new Size(552, 240);
            updateNotes.Multiline = true;
            updateNotes.ReadOnly = true;
            updateNotes.ScrollBars = ScrollBars.Vertical;
            updateNotes.BackColor = Color.White;
            updateNotes.Text =
                "Здесь появится короткое описание новой версии: " +
                "что добавлено, что исправлено и стоит ли обновляться.";
            updatesPage.Controls.Add(updateNotes);

            openRelease = MakeButton(
                "Открыть страницу релиза", 24, 478, 200, 38, false);
            openRelease.Enabled = false;
            openRelease.Click += delegate
            {
                if (availableUpdate != null &&
                    !string.IsNullOrWhiteSpace(availableUpdate.ReleasePage))
                    Process.Start(new ProcessStartInfo(
                        availableUpdate.ReleasePage)
                    {
                        UseShellExecute = true
                    });
            };
            updatesPage.Controls.Add(openRelease);

            installUpdate = MakeButton(
                "Скачать и установить", 376, 474, 200, 42, true);
            installUpdate.Enabled = false;
            installUpdate.Click += delegate { DownloadUpdate(); };
            updatesPage.Controls.Add(installUpdate);

            LoadSettings();
            WireDirtyTracking();
            settingsPage.Scroll += delegate { CloseOpenDropDowns(); };
            settingsPage.MouseWheel += delegate { CloseOpenDropDowns(); };
            SetDirty(false);
            UpdateAdvancedDirty();
            ShowHomePage();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    if (!ConfirmAllUnsavedChanges())
                    {
                        e.Cancel = true;
                        return;
                    }
                    if (context.CurrentSettings.CloseToTray)
                    {
                        e.Cancel = true;
                        Hide();
                    }
                    else
                    {
                        context.QuitApplication();
                    }
                }
            };
            FormClosed += delegate
            {
                reportStatusTimer.Stop();
                reportStatusTimer.Dispose();
                DeletePendingInstaller();
            };
        }

        public void SyncStatus()
        {
            if (IsDisposed)
                return;

            bool unsafeAccess = context.IsPublicNetwork &&
                context.CurrentSettings.PairingMode == "none";
            bool dark = appliedDarkTheme ??
                ThemeHelper.IsDark(context.CurrentSettings.ThemeMode);
            bool bonjourFirewallMissing =
                context.IsBonjourFirewallRepairRequired;
            bool bonjourUnavailable = context.IsBonjourUnavailable;
            bool bonjourFirewallRepairRunning =
                context.IsBonjourFirewallRepairRunning;
            bool receiverReady = context.IsCoreRunning &&
                context.IsReceiverReady;
            if (receiverReady)
            {
                status.Text = "Приёмник включён";
                status.ForeColor = dark
                    ? Color.FromArgb(94, 204, 126)
                    : Color.FromArgb(31, 122, 67);
                statusDot.ForeColor = status.ForeColor;
                startStop.Text = "Остановить";
            }
            else if (context.IsCoreRunning)
            {
                status.Text = "Приёмник запускается";
                status.ForeColor = dark
                    ? Color.FromArgb(255, 197, 92)
                    : Color.FromArgb(154, 92, 0);
                statusDot.ForeColor = status.ForeColor;
                startStop.Text = "Остановить";
            }
            else if (context.IsWaitingForNetwork)
            {
                status.Text = "Проверяем сеть";
                status.ForeColor = dark
                    ? Color.FromArgb(255, 197, 92)
                    : Color.FromArgb(154, 92, 0);
                statusDot.ForeColor = status.ForeColor;
                startStop.Text = "Проверить";
            }
            else if (unsafeAccess)
            {
                status.Text = "Приёмник приостановлен — включите PIN";
                status.ForeColor = dark
                    ? Color.FromArgb(255, 197, 92)
                    : Color.FromArgb(154, 92, 0);
                statusDot.ForeColor = status.ForeColor;
                startStop.Text = "Включить";
            }
            else
            {
                status.Text = "Приёмник выключен";
                status.ForeColor = dark
                    ? Color.FromArgb(225, 126, 126)
                    : Color.FromArgb(128, 70, 70);
                statusDot.ForeColor = dark
                    ? Color.FromArgb(238, 92, 92)
                    : Color.FromArgb(196, 43, 43);
                startStop.Text = "Включить";
            }
            status.Width = Math.Min(
                420,
                Math.Max(1, TextRenderer.MeasureText(status.Text, status.Font).Width));
            string receiverDetails = context.ReceiverStateText;
            statusDot.AccessibleDescription = receiverDetails;
            status.AccessibleDescription = receiverDetails;
            toolTips.SetToolTip(statusDot, receiverDetails);
            toolTips.SetToolTip(status, receiverDetails);

            string networkDetails;
            if (unsafeAccess)
            {
                networkCard.BackColor = dark
                    ? Color.FromArgb(73, 57, 24)
                    : Color.FromArgb(255, 244, 215);
                networkTitle.Text = "Сеть «" + DisplayNetworkName() +
                    "» · публичная · требуется PIN" +
                    (context.HasNetworkOverlay
                        ? " · VPN/виртуальная сеть" : "");
                networkDetails = context.HasNetworkOverlay
                    ? "Проверен именно физический Wi-Fi или Ethernet. VPN и виртуальный профиль не меняют режим защиты. Windows считает физическую сеть публичной, поэтому для подключения нужен PIN."
                    : "Windows считает это физическое подключение публичной сетью. Без PIN приёмник приостановлен.";
                networkTitle.ForeColor = dark
                    ? Color.FromArgb(255, 197, 92)
                    : Color.FromArgb(133, 78, 0);
            }
            else
            {
                networkCard.BackColor = dark
                    ? Color.FromArgb(31, 50, 67)
                    : Color.FromArgb(237, 246, 255);
                if (!context.IsNetworkProfileKnown)
                {
                    networkTitle.Text = "Проверяем физическую сеть…";
                    networkDetails =
                        "Компьютер и iPhone должны находиться в одной локальной сети. VPN и виртуальные адаптеры не используются для определения режима защиты.";
                }
                else if (context.IsPublicNetwork)
                {
                    networkTitle.Text = "Сеть «" + DisplayNetworkName() +
                        "» · публичная · PIN включён" +
                        (context.HasNetworkOverlay
                            ? " · VPN/виртуальная сеть" : "");
                    networkDetails = context.HasNetworkOverlay
                        ? "Проверен именно физический Wi-Fi или Ethernet. VPN и виртуальный профиль не меняют режим защиты. Знакомый iPhone проверяется по сохранённому ключу."
                        : "PIN защищает первое подключение. Знакомый iPhone затем проверяется по сохранённому ключу.";
                }
                else
                {
                    networkTitle.Text = "Сеть «" + DisplayNetworkName() +
                        "» · частная" +
                        (context.HasNetworkOverlay
                            ? " · VPN/виртуальная сеть" : "");
                    networkDetails = context.HasNetworkOverlay
                        ? "Проверен именно физический Wi-Fi или Ethernet. VPN и виртуальный профиль не меняют режим защиты.\r\n" +
                          "В частной сети PIN необязателен, но его можно включить."
                        : "Windows пометила физическое подключение как частную сеть.\r\n" +
                          "В частной сети PIN необязателен, но его можно включить.";
                }
                networkTitle.ForeColor = dark
                    ? Color.FromArgb(111, 190, 255)
                    : Color.FromArgb(0, 80, 145);
            }

            bool showBonjourNotice =
                bonjourFirewallMissing || bonjourUnavailable;
            networkCard.Height = showBonjourNotice ? 94 : 46;
            bonjourFirewallNotice.Visible = showBonjourNotice;
            if (bonjourUnavailable)
            {
                networkCard.BackColor = dark
                    ? Color.FromArgb(72, 36, 36)
                    : Color.FromArgb(255, 235, 235);
                networkTitle.ForeColor = dark
                    ? Color.FromArgb(255, 166, 166)
                    : Color.FromArgb(150, 45, 45);
                bonjourFirewallNotice.Text =
                    "Bonjour недоступен или установлен некорректно. Проверьте установку, чтобы iPhone находил AeroMirror.";
                bonjourFirewallNotice.Size = new Size(536, 42);
                bonjourFirewallNotice.ForeColor = networkTitle.ForeColor;
                bonjourFirewallRepair.Visible = false;
                networkDetails +=
                    "\r\nBonjour не найден или путь службы небезопасен. AeroMirror не устанавливает системную службу автоматически.";
            }
            else if (bonjourFirewallMissing)
            {
                networkCard.BackColor = dark
                    ? Color.FromArgb(73, 57, 24)
                    : Color.FromArgb(255, 244, 215);
                networkTitle.ForeColor = dark
                    ? Color.FromArgb(255, 197, 92)
                    : Color.FromArgb(133, 78, 0);
                bonjourFirewallNotice.Text =
                    "Windows может блокировать обнаружение AirPlay через Bonjour.";
                bonjourFirewallNotice.Size = new Size(336, 42);
                bonjourFirewallNotice.ForeColor = networkTitle.ForeColor;
                bonjourFirewallRepair.Visible = true;
                bonjourFirewallRepair.Enabled =
                    !bonjourFirewallRepairRunning;
                bonjourFirewallRepair.Text = bonjourFirewallRepairRunning
                    ? "Настраиваем…" : "Разрешить Bonjour";
                networkDetails +=
                    "\r\nНет точного правила для Bonjour: Private, UDP 5353, только локальная подсеть.";
            }
            else
            {
                bonjourFirewallNotice.Text = "";
                bonjourFirewallRepair.Visible = false;
                bonjourFirewallRepair.Enabled = true;
                bonjourFirewallRepair.Text = "Разрешить Bonjour";
            }
            networkHelp.ForeColor = networkTitle.ForeColor;
            networkHelp.AccessibleDescription = networkDetails;
            networkHelp.Invalidate();
            toolTips.SetToolTip(networkHelp, networkDetails);
            toolTips.SetToolTip(bonjourFirewallNotice, networkDetails);

            homeQuality.Text = "Качество: " +
                QualityDisplayName(context.CurrentSettings.QualityPreset) +
                "   ·   Имя приёмника: " + context.CurrentSettings.ReceiverName;
            bool showTrustCard =
                context.CurrentSettings.PairingMode != "pin" &&
                !context.CurrentSettings.DismissPinSuggestion;
            trustCard.Visible = showTrustCard;
            LayoutHome(showTrustCard);
            UpdateAccessNote();
        }

        public bool ConfirmCloseForQuit()
        {
            return ConfirmAllUnsavedChanges();
        }

        public void SyncTheme()
        {
            string mode = context.CurrentSettings.ThemeMode;
            if (string.Equals(mode, "system", StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow < nextThemeCheck)
                return;
            nextThemeCheck = DateTime.UtcNow.AddSeconds(2);
            bool dark = ThemeHelper.IsDark(mode);
            if (!appliedDarkTheme.HasValue || appliedDarkTheme.Value != dark)
                ApplyTheme();
        }

        private void ApplyTheme()
        {
            string mode = context.CurrentSettings.ThemeMode;
            bool dark = ThemeHelper.IsDark(mode);
            Point settingsScroll = settingsPage == null
                ? Point.Empty : settingsPage.AutoScrollPosition;
            NativeMethods.SetImmersiveDarkMode(Handle, dark);
            ThemeHelper.Apply(this, dark);
            if (settingsPage != null)
            {
                settingsPage.AutoScrollPosition = new Point(
                    Math.Max(0, -settingsScroll.X),
                    Math.Max(0, -settingsScroll.Y));
            }
            appliedDarkTheme = dark;
            nextThemeCheck = DateTime.UtcNow.AddSeconds(2);
            SyncStatus();
        }

        private void ShowHomePage()
        {
            CloseOpenDropDowns();
            homePageSelected = true;
            settingsPage.Visible = false;
            advancedPage.Visible = false;
            updatesPage.Visible = false;
            homePage.Visible = true;
            homePage.BringToFront();
            SyncStatus();
        }

        private void LayoutHome(bool showTrustCard)
        {
            int actionTop;
            int contentTop = networkCard.Bottom + 16;
            if (showTrustCard)
            {
                trustCard.Location = new Point(24, contentTop);
                actionTop = trustCard.Bottom + 14;
            }
            else
            {
                actionTop = contentTop;
            }
            refreshDiscovery.Location = new Point(24, actionTop);
            startStop.Location = new Point(260, actionTop);
            updatesButton.Location = new Point(417, actionTop);
            homeQuality.Location = new Point(26, actionTop + 51);
            reportStatus.Location = new Point(26, actionTop + 81);
            reportProblem.Location = new Point(452, actionTop + 81);
            if (homePageSelected)
                ClientSize = new Size(620, actionTop + 113);
        }

        private void CloseOpenDropDowns()
        {
            ComboBox[] controls =
            {
                quality, latency, audioOutput, pairing, theme, renderer
            };
            foreach (ComboBox combo in controls)
            {
                if (combo != null && combo.DroppedDown)
                    combo.DroppedDown = false;
            }
        }

        private void TryLeaveSettingsToHome()
        {
            if (!ConfirmGeneralUnsavedChanges())
                return;
            ShowHomePage();
        }

        private void TryLeaveAdvancedToSettings()
        {
            if (!ConfirmAdvancedUnsavedChanges())
                return;
            ShowSettingsPage(false);
        }

        private void ShowSettingsPage(bool focusPin)
        {
            CloseOpenDropDowns();
            homePageSelected = false;
            ClientSize = new Size(620, 700);
            homePage.Visible = false;
            advancedPage.Visible = false;
            updatesPage.Visible = false;
            settingsPage.Visible = true;
            settingsPage.BringToFront();
            if (focusPin)
            {
                SelectValue(pairing, "pin");
                fixedPin.Focus();
            }
        }

        private void ShowAdvancedPage()
        {
            CloseOpenDropDowns();
            homePageSelected = false;
            ClientSize = new Size(620, 570);
            homePage.Visible = false;
            settingsPage.Visible = false;
            updatesPage.Visible = false;
            advancedPage.Visible = true;
            advancedPage.BringToFront();
            argumentPreview.Text = context.BuildSafeUxPlayArguments();
            UpdateAdvancedDirty();
        }

        private void ShowUpdatesPage()
        {
            CloseOpenDropDowns();
            homePageSelected = false;
            ClientSize = new Size(620, 570);
            homePage.Visible = false;
            settingsPage.Visible = false;
            advancedPage.Visible = false;
            updatesPage.Visible = true;
            updatesPage.BringToFront();
        }

        private void CheckForUpdates()
        {
            checkUpdate.Enabled = false;
            installUpdate.Enabled = false;
            openRelease.Enabled = false;
            updateTitle.Text = "";
            updateState.Text = "Проверяем последний опубликованный GitHub Release…";
            updateNotes.Text = "";
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    UpdateInfo info = UpdateService.Check();
                    if (IsDisposed)
                        return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        availableUpdate = info;
                        checkUpdate.Enabled = true;
                        openRelease.Enabled =
                            !string.IsNullOrWhiteSpace(info.ReleasePage);
                        updateNotes.Text = string.IsNullOrWhiteSpace(info.Notes)
                            ? "Автор релиза не добавил описание изменений."
                            : info.Notes;
                        if (info.IsNewer)
                        {
                            updateTitle.Text =
                                "Доступна версия " + info.Version.ToString(3);
                            if (string.IsNullOrWhiteSpace(info.InstallerUrl))
                            {
                                updateState.Text =
                                    "Версия найдена, но установщик не прикреплён к релизу.";
                                SetPrimaryButtonState(installUpdate, false);
                            }
                            else if (string.IsNullOrWhiteSpace(
                                info.InstallerSha256))
                            {
                                updateState.Text =
                                    "Версия найдена, но GitHub ещё не рассчитал SHA-256.";
                                SetPrimaryButtonState(installUpdate, false);
                            }
                            else
                            {
                                updateState.Text =
                                    "Прочитайте изменения и решите, нужно ли обновление.";
                                SetPrimaryButtonState(installUpdate, true);
                            }
                        }
                        else
                        {
                            updateTitle.Text =
                                "Установлена актуальная версия";
                            updateState.Text =
                                "Новых опубликованных версий сейчас нет.";
                            SetPrimaryButtonState(installUpdate, false);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (IsDisposed)
                        return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        checkUpdate.Enabled = true;
                        updateTitle.Text = "Не удалось проверить обновления";
                        updateState.Text = ex.Message;
                        updateNotes.Text =
                            "Проверьте интернет-соединение и повторите попытку позже.";
                        SetPrimaryButtonState(installUpdate, false);
                    });
                }
            });
        }

        private void DownloadUpdate()
        {
            if (availableUpdate == null || !availableUpdate.IsNewer)
                return;
            if (!ConfirmAllUnsavedChanges())
                return;
            updatesBack.Enabled = false;
            SetPrimaryButtonState(installUpdate, false);
            checkUpdate.Enabled = false;
            updateState.Text =
                "Скачиваем установщик и проверяем его SHA-256…";
            ThreadPool.QueueUserWorkItem(delegate
            {
                string installerPath = "";
                try
                {
                    installerPath =
                        UpdateService.DownloadAndVerify(availableUpdate);
                    pendingInstallerPath = installerPath;
                    if (IsDisposed)
                    {
                        DeleteFileQuietly(installerPath);
                        pendingInstallerPath = "";
                        return;
                    }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        LaunchVerifiedUpdateInstaller(installerPath);
                    });
                }
                catch (Exception ex)
                {
                    DeleteFileQuietly(installerPath);
                    pendingInstallerPath = "";
                    if (IsDisposed)
                        return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        updatesBack.Enabled = true;
                        checkUpdate.Enabled = true;
                        updateState.Text =
                            "Обновление не скачано: " + ex.Message;
                        SetPrimaryButtonState(installUpdate, true);
                    });
                }
            });
        }

        private void LaunchVerifiedUpdateInstaller(string installerPath)
        {
            updateState.Text =
                "Установщик загружен и проверен. Запускаем обновление…";
            try
            {
                using (Process setup = Process.Start(
                    new ProcessStartInfo(installerPath)
                    {
                        Arguments = "/update /delete-source",
                        WorkingDirectory = Path.GetDirectoryName(installerPath),
                        UseShellExecute = true
                    }))
                {
                    if (setup == null)
                    {
                        throw new InvalidOperationException(
                            "Windows не вернула запущенный процесс установщика.");
                    }
                }
            }
            catch (Exception ex)
            {
                ReceiverContext.Log(
                    "Verified Setup launch failed: " + ex);
                DeleteFileQuietly(installerPath);
                if (string.Equals(
                    pendingInstallerPath, installerPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    pendingInstallerPath = "";
                }
                updatesBack.Enabled = true;
                checkUpdate.Enabled = true;
                updateState.Text =
                    "Не удалось запустить обновление: " + ex.Message;
                SetPrimaryButtonState(installUpdate, true);
                return;
            }

            pendingInstallerPath = "";
            context.QuitApplication();
        }

        private void DeletePendingInstaller()
        {
            string path = pendingInstallerPath;
            pendingInstallerPath = "";
            DeleteFileQuietly(path);
        }

        private static void DeleteFileQuietly(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private string DisplayNetworkName()
        {
            return string.IsNullOrWhiteSpace(context.NetworkProfileName)
                ? "текущая" : context.NetworkProfileName;
        }

        private void LoadSettings()
        {
            suppressDirty = true;
            AppSettings s = context.CurrentSettings;
            receiverName.Text = s.ReceiverName;
            SelectValue(quality, s.QualityPreset);
            SelectValue(pairing,
                s.PairingMode == "password" ? "none" : s.PairingMode);
            fixedPin.Text = s.FixedPin;
            SelectValue(latency, s.LatencyProfile);
            SelectValue(audioOutput, s.AudioOutput);
            SelectValue(theme, s.ThemeMode);
            topMost.Checked = s.AlwaysOnTop;
            autoFit.Checked = s.AutoFitWindow;
            showStreamInTaskbar.Checked = s.ShowStreamInTaskbar;
            autoReceiver.Checked = s.AutoStartReceiver;
            autoWindows.Checked = s.AutoStartWindows;
            startMinimized.Checked = s.StartMinimized;
            closeToTray.Checked = s.CloseToTray;
            notifications.Checked = s.Notify;
            SelectValue(renderer, s.Renderer);
            arguments.Text = s.AdvancedArguments;
            argumentPreview.Text = context.BuildSafeUxPlayArguments();
            UpdatePinPanel();
            UpdateStartupChild();
            suppressDirty = false;
            SetDirty(false);
            UpdateAdvancedDirty();
            ApplyTheme();
            SyncStatus();
        }

        private void WireDirtyTracking()
        {
            receiverName.TextChanged += delegate { MarkDirty(); };
            quality.SelectedIndexChanged += delegate { MarkDirty(); };
            pairing.SelectedIndexChanged += delegate { MarkDirty(); };
            fixedPin.TextChanged += delegate { MarkDirty(); };
            latency.SelectedIndexChanged += delegate { MarkDirty(); };
            audioOutput.SelectedIndexChanged += delegate { MarkDirty(); };
            topMost.CheckedChanged += delegate { MarkDirty(); };
            autoFit.CheckedChanged += delegate { MarkDirty(); };
            showStreamInTaskbar.CheckedChanged += delegate { MarkDirty(); };
            autoReceiver.CheckedChanged += delegate { MarkDirty(); };
            autoWindows.CheckedChanged += delegate { MarkDirty(); };
            startMinimized.CheckedChanged += delegate { MarkDirty(); };
            closeToTray.CheckedChanged += delegate { MarkDirty(); };
            notifications.CheckedChanged += delegate { MarkDirty(); };
            renderer.SelectedIndexChanged += delegate { UpdateAdvancedDirty(); };
            arguments.TextChanged += delegate { UpdateAdvancedDirty(); };
        }

        private void MarkDirty()
        {
            if (!suppressDirty)
                SetDirty(HasUnsavedChanges());
        }

        private bool HasUnsavedChanges()
        {
            AppSettings s = context.CurrentSettings;
            string mode = SelectedValue(pairing);
            string currentMode =
                s.PairingMode == "password" ? "none" : s.PairingMode;
            string pin = mode == "pin" ? fixedPin.Text.Trim() : "";
            string currentPin =
                currentMode == "pin" ? s.FixedPin.Trim() : "";
            return receiverName.Text.Trim() != s.ReceiverName.Trim() ||
                SelectedValue(quality) != s.QualityPreset ||
                mode != currentMode ||
                pin != currentPin ||
                SelectedValue(latency) != s.LatencyProfile ||
                SelectedValue(audioOutput) != s.AudioOutput ||
                SelectedValue(theme) != s.ThemeMode ||
                topMost.Checked != s.AlwaysOnTop ||
                autoFit.Checked != s.AutoFitWindow ||
                showStreamInTaskbar.Checked != s.ShowStreamInTaskbar ||
                autoReceiver.Checked != s.AutoStartReceiver ||
                autoWindows.Checked != s.AutoStartWindows ||
                (autoWindows.Checked && startMinimized.Checked) !=
                    (s.AutoStartWindows && s.StartMinimized) ||
                closeToTray.Checked != s.CloseToTray ||
                notifications.Checked != s.Notify;
        }

        private void SetDirty(bool dirty)
        {
            SetPrimaryButtonState(saveButton, dirty);
            if (dirty)
                savedLabel.Text = "";
        }

        private void UpdateAdvancedDirty()
        {
            if (advancedSave == null || renderer == null || arguments == null)
                return;
            SetPrimaryButtonState(advancedSave, HasAdvancedUnsavedChanges());
        }

        private bool HasAdvancedUnsavedChanges()
        {
            return SelectedValue(renderer) != context.CurrentSettings.Renderer ||
                arguments.Text.Trim() !=
                    context.CurrentSettings.AdvancedArguments.Trim();
        }

        private bool ConfirmAllUnsavedChanges()
        {
            bool generalDirty = HasUnsavedChanges();
            bool advancedDirty = HasAdvancedUnsavedChanges();
            if (generalDirty && advancedDirty)
            {
                DialogResult combinedAnswer = MessageBox.Show(
                    this,
                    "Сохранить все изменения в обычных и дополнительных настройках?",
                    "AeroMirror",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (combinedAnswer == DialogResult.Cancel)
                    return false;
                if (combinedAnswer == DialogResult.Yes)
                    return TrySaveGeneralSettings(true);
                LoadSettings();
                return true;
            }
            if (!ConfirmAdvancedUnsavedChanges())
                return false;
            return ConfirmGeneralUnsavedChanges();
        }

        private bool ConfirmGeneralUnsavedChanges()
        {
            if (!HasUnsavedChanges())
                return true;
            DialogResult answer = MessageBox.Show(
                this,
                "Сохранить изменения в настройках перед выходом?",
                "AeroMirror",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.Yes)
                return TrySaveGeneralSettings();
            LoadSettings();
            return true;
        }

        private bool ConfirmAdvancedUnsavedChanges()
        {
            if (!HasAdvancedUnsavedChanges())
                return true;
            DialogResult answer = MessageBox.Show(
                this,
                "Сохранить изменения в дополнительных настройках?",
                "AeroMirror",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.Yes)
                return TrySaveAdvancedSettings();
            suppressDirty = true;
            SelectValue(renderer, context.CurrentSettings.Renderer);
            arguments.Text = context.CurrentSettings.AdvancedArguments;
            argumentPreview.Text = context.BuildSafeUxPlayArguments();
            suppressDirty = false;
            UpdateAdvancedDirty();
            return true;
        }

        private void UpdatePinPanel()
        {
            bool enabled = SelectedValue(pairing) == "pin";
            pinPanel.Visible = enabled;
            accessNote.Visible = !enabled;
            if (enabled && string.IsNullOrWhiteSpace(fixedPin.Text))
                fixedPin.Text = GeneratePin();
            UpdateAccessNote();
        }

        private void UpdateAccessNote()
        {
            if (accessNote == null || pairing == null)
                return;
            if (SelectedValue(pairing) == "pin")
            {
                accessNote.Text =
                    "PIN вводится при первом знакомстве устройств. Доверие сохраняется " +
                    "между этим iPhone и ноутбуком независимо от названия Wi-Fi-сети.";
            }
            else if (!context.IsNetworkProfileKnown)
            {
                accessNote.Text =
                    "Профиль физической сети пока не определён. Без PIN приёмник " +
                    "не запустится. Повторите проверку сети.";
            }
            else if (context.IsPublicNetwork)
            {
                accessNote.Text = context.HasNetworkOverlay
                    ? "VPN или виртуальная сеть обнаружены, но Windows считает физический Wi-Fi/Ethernet " +
                      "публичным. Включите PIN либо отключите VPN и повторите проверку."
                    : "В публичной сети приёмник без PIN будет приостановлен. " +
                      "Включите PIN или измените профиль сети в Windows.";
            }
            else
            {
                accessNote.Text =
                    "В частной домашней сети PIN необязателен. Его можно включить " +
                    "заранее, чтобы знакомый iPhone подключался и в других сетях.";
            }
        }

        private void UpdateStartupChild()
        {
            startMinimized.Visible = autoWindows.Checked;
            startMinimized.Enabled = autoWindows.Checked;
        }

        private void OnSave(object sender, EventArgs e)
        {
            TrySaveGeneralSettings();
        }

        private bool TrySaveGeneralSettings()
        {
            return TrySaveGeneralSettings(false);
        }

        private bool TrySaveGeneralSettings(bool includeAdvanced)
        {
            Point scrollPosition = settingsPage.AutoScrollPosition;
            string name = receiverName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Введите имя приёмника.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string canonicalName =
                AppSettings.NormalizeReceiverNameForDiscovery(name);
            if (!string.Equals(
                    name, canonicalName, StringComparison.Ordinal))
            {
                MessageBox.Show(this,
                    "Имя приёмника ограничено 50 байтами UTF-8, чтобы " +
                    "Bonjour стабильно публиковал его. На iPhone будет " +
                    "отображаться: " + canonicalName,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                name = canonicalName;
                receiverName.Text = canonicalName;
            }

            string mode = SelectedValue(pairing);
            string pin = fixedPin.Text.Trim();
            if (mode == "pin" && (pin.Length != 4 || !IsDigits(pin)))
            {
                MessageBox.Show(this,
                    "Для защищённого подключения нужен PIN из четырёх цифр.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            AppSettings updated = context.CurrentSettings.Copy();
            updated.ReceiverName = name;
            updated.QualityPreset = SelectedValue(quality);
            updated.PairingMode = mode;
            updated.FixedPin = mode == "pin" ? pin : "";
            updated.LatencyProfile = SelectedValue(latency);
            updated.AudioOutput = SelectedValue(audioOutput);
            updated.ThemeMode = SelectedValue(theme);
            updated.AlwaysOnTop = topMost.Checked;
            updated.AutoFitWindow = autoFit.Checked;
            updated.ShowStreamInTaskbar = showStreamInTaskbar.Checked;
            updated.AutoStartReceiver = autoReceiver.Checked;
            updated.AutoStartWindows = autoWindows.Checked;
            updated.StartMinimized =
                autoWindows.Checked && startMinimized.Checked;
            updated.CloseToTray = closeToTray.Checked;
            updated.Notify = notifications.Checked;
            if (includeAdvanced)
            {
                updated.Renderer = SelectedValue(renderer);
                updated.AdvancedArguments = arguments.Text.Trim();
            }
            if (mode == "pin")
                updated.DismissPinSuggestion = true;
            bool qualityChanged =
                updated.QualityPreset != context.CurrentSettings.QualityPreset;
            bool restarted = context.SaveSettings(updated, true);
            bool deferred = context.IsSettingsRestartDeferred;
            SetDirty(false);
            UpdateAdvancedDirty();
            ApplyTheme();
            savedLabel.ForeColor = ThemeHelper.IsDark(
                    context.CurrentSettings.ThemeMode)
                ? Color.FromArgb(94, 204, 126)
                : Color.FromArgb(42, 122, 74);
            savedLabel.Text = deferred
                ? "Сохранено · применится после отключения iPhone"
                : restarted && qualityChanged
                ? "Сохранено · применяется для нового подключения"
                : restarted
                ? "Сохранено · приёмник перезапускается"
                : "Сохранено";
            SyncStatus();
            settingsPage.AutoScrollPosition = new Point(
                Math.Max(0, -scrollPosition.X),
                Math.Max(0, -scrollPosition.Y));
            return true;
        }

        private void OnAdvancedSave(object sender, EventArgs e)
        {
            TrySaveAdvancedSettings();
        }

        private bool TrySaveAdvancedSettings()
        {
            AppSettings updated = context.CurrentSettings.Copy();
            updated.Renderer = SelectedValue(renderer);
            updated.AdvancedArguments = arguments.Text.Trim();
            context.SaveSettings(updated, true);
            argumentPreview.Text = context.BuildSafeUxPlayArguments();
            UpdateAdvancedDirty();
            return true;
        }

        private void DrawQualityItem(object sender, DrawItemEventArgs e)
        {
            bool isClosedSelection =
                (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool highlight =
                !isClosedSelection && (e.State & DrawItemState.Selected) != 0;
            if (isClosedSelection)
            {
                using (var background = new SolidBrush(quality.BackColor))
                    e.Graphics.FillRectangle(background, e.Bounds);
            }
            else
                e.DrawBackground();
            if (e.Index < 0 || e.Index >= quality.Items.Count)
                return;
            NamedValue item = quality.Items[e.Index] as NamedValue;
            if (item == null)
                return;

            Color mainColor = highlight
                ? SystemColors.HighlightText : quality.ForeColor;
            Color noteColor = highlight
                ? SystemColors.HighlightText
                : (appliedDarkTheme == true
                    ? Color.FromArgb(185, 185, 185)
                    : Color.DimGray);
            using (var mainFont = new Font(Font, FontStyle.Regular))
            using (var noteFont = new Font(Font.FontFamily, 8.25F))
            {
                TextRenderer.DrawText(e.Graphics, item.Name, mainFont,
                    new Point(e.Bounds.Left + 8, e.Bounds.Top + 3),
                    mainColor, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, item.Subtitle, noteFont,
                    new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 20,
                        e.Bounds.Width - 24, 16),
                    noteColor, TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);
            }
            if (highlight)
                e.DrawFocusRectangle();
        }

        private static string QualityDisplayName(string value)
        {
            if (value == "4k60") return "4K · 60 FPS";
            if (value == "1080p30") return "Full HD · 30 FPS";
            if (value == "720p30") return "HD · 30 FPS";
            return "Full HD · 60 FPS";
        }

        private static void AddSection(
            Control parent, string text, int x, int y)
        {
            var label = MakeLabel(text, x, y);
            label.Font = new Font("Segoe UI Semibold", 12F);
            parent.Controls.Add(label);
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            return label;
        }

        private static Label MakeFixedLabel(
            string text, int x, int y, int width, int height)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            return label;
        }

        private static Button MakeButton(
            string text, int x, int y, int width, int height, bool primary)
        {
            var button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            if (primary)
            {
                button.FlatAppearance.BorderSize = 0;
                button.BackColor = Color.FromArgb(0, 95, 184);
                button.ForeColor = Color.White;
            }
            return button;
        }

        private static Button MakeBackButton(int x, int y)
        {
            var button = MakeButton("←  Назад", x, y, 112, 40, false);
            button.Font = new Font("Segoe UI", 11.5F, FontStyle.Regular);
            button.TextAlign = ContentAlignment.MiddleCenter;
            return button;
        }

        private static ComboBox MakeCombo(int x, int y, int width)
        {
            var combo = new WheelSafeComboBox();
            combo.Location = new Point(x, y);
            combo.Size = new Size(width, 27);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += DrawSimpleComboItem;
            return combo;
        }

        private static void DrawSimpleComboItem(
            object sender, DrawItemEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null)
                return;
            bool isClosed =
                (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected =
                !isClosed && (e.State & DrawItemState.Selected) != 0;
            using (var background = new SolidBrush(
                selected ? SystemColors.Highlight : combo.BackColor))
                e.Graphics.FillRectangle(background, e.Bounds);
            if (e.Index < 0 || e.Index >= combo.Items.Count)
                return;
            Color textColor = selected
                ? SystemColors.HighlightText : combo.ForeColor;
            TextRenderer.DrawText(
                e.Graphics,
                combo.Items[e.Index].ToString(),
                combo.Font,
                new Rectangle(
                    e.Bounds.Left + 4,
                    e.Bounds.Top + 1,
                    e.Bounds.Width - 8,
                    e.Bounds.Height - 2),
                textColor,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
            if (selected)
                e.DrawFocusRectangle();
        }

        private static CheckBox MakeCheckBox(string text, int x, int y)
        {
            var check = new CheckBox();
            check.Text = text;
            check.AutoSize = true;
            check.Location = new Point(x, y);
            return check;
        }

        private void SetPrimaryButtonState(Button button, bool enabled)
        {
            bool dark = appliedDarkTheme ??
                ThemeHelper.IsDark(context.CurrentSettings.ThemeMode);
            button.Enabled = enabled;
            button.BackColor = enabled
                ? Color.FromArgb(0, 95, 184)
                : dark
                ? Color.FromArgb(57, 58, 63)
                : Color.FromArgb(225, 225, 225);
            button.ForeColor = enabled
                ? Color.White
                : dark
                ? Color.FromArgb(190, 190, 190)
                : Color.DimGray;
        }

        private static void SelectValue(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as NamedValue;
                if (item != null && item.Value == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static string SelectedValue(ComboBox combo)
        {
            var item = combo.SelectedItem as NamedValue;
            return item == null ? "" : item.Value;
        }

        private static bool IsDigits(string text)
        {
            foreach (char c in text)
                if (c < '0' || c > '9') return false;
            return true;
        }

        private static string GeneratePin()
        {
            return new Random(
                Guid.NewGuid().GetHashCode()).Next(1000, 10000).ToString();
        }

        internal static void OpenMobileHotspot(IWin32Window owner)
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "ms-settings:network-mobilehotspot")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner,
                    "Не удалось открыть параметры Windows.\r\n\r\n" + ex.Message,
                    "AeroMirror",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}

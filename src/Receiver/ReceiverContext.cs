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
    internal sealed partial class ReceiverContext : ApplicationContext
    {
        private const string AppTitle = "AeroMirror";
        private const int ConnectionRequestGraceSeconds = 30;
        private const int PinEntryGraceSeconds = 60;
        private const int CoreStopCompletionWaitMilliseconds = 7000;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem startStopItem;
        private readonly ToolStripMenuItem autoStartItem;
        private readonly ToolStripMenuItem topMostItem;
        private readonly System.Windows.Forms.Timer monitorTimer;
        private readonly EventWaitHandle showEvent;
        private AppSettings settings;
        private Process coreProcess;
        private IntPtr coreJob = IntPtr.Zero;
        private int rapidExitCount;
        private DateTime rapidExitWindowStartedAt = DateTime.MinValue;
        private SettingsForm form;
        private bool quitting;
        private bool publicNetwork;
        private bool networkProfileKnown;
        private string networkProfileName = "";
        private string networkInterfaceName = "";
        private string physicalNetworkAddresses = "";
        private string networkSignature = "";
        private int nonPhysicalProfileCount;
        private int publicNonPhysicalProfileCount;
        private IntPtr fittedStreamWindow = IntPtr.Zero;
        private int networkRefreshPending;
        private long networkRefreshDueTicks;
        private int networkRefreshRunning;
        private int networkUnknownRetries;
        private int knownNetworkUnknownRetries;
        private readonly object networkProfileSync = new object();
        private NetworkProfileInfo pendingNetworkProfile;
        private bool restartPending;
        private DateTime restartDueUtc;
        private string restartReason = "";
        private int restartStopInProgress;
        private int restartStopCompleted;
        private int restartStopSucceeded;
        private int blockAutomaticRestartAfterUnconfirmedStop;
        private int restartDelayAfterStop;
        private bool restartAfterStop;
        private Func<Process, IntPtr, string, bool, bool>
            detachedCoreStopOperation = StopDetachedCore;
        private readonly ManualResetEvent restartStopDone =
            new ManualResetEvent(true);
        private bool coreReadyPending;
        private DateTime coreReadyDueUtc;
        private int coreReadyChecks;
        private int coreReadinessRecoveryAttempts;
        private int coreReadinessPid;
        private int coreClientActivityReadyPending;
        private int coreSocketsReady;
        private long coreSocketsReadyDueTicks;
        private int coreDnsSdStatus;
        private int coreBonjourUnavailable;
        private int coreBonjourStateChanged;
        private long coreBonjourServiceCheckDueTicks;
        private int coreBonjourRecoveryAttempted;
        private int coreDiscoveryRefreshCapability;
        private long coreDiscoveryRefreshRequestSequence;
        private long coreDiscoveryRefreshPendingRequest;
        private int coreDiscoveryRefreshPendingPid;
        private int coreDiscoveryRefreshPendingPort;
        private long coreDiscoveryRefreshDueTicks;
        private int coreDiscoveryRefreshPhase;
        private int coreDiscoveryRefreshFallbackPending;
        private object coreCommandSync = new object();
        private int coreBleStatus;
        private int coreDiscoveryRecoveryPending;
        private int coreDiscoveryRecoveryAttempts;
        private int coreDiscoveryRecoveryPid;
        private long coreDiscoveryRecoveryDueTicks;
        private int activeCorePid;
        private int mirrorSessionActive;
        private int mirrorSessionEndedPending;
        private long mirrorSessionEndedDueTicks;
        private int settingsRestartDeferred;
        private readonly object postSessionMaintenanceSync = new object();
        private long clientActivityGraceDueTicks;
        private int physicalNetworkRestartDeferred;
        private long idleDiscoveryRenewalDueTicks;
        private int idleDiscoveryRenewalUsed;
        private DateTime lastAutomaticDiscoveryRefreshUtc = DateTime.MinValue;
        private int sessionUnlockDiscoveryRefreshPending;
        private long sessionUnlockDiscoveryRefreshDueTicks;
        private bool sessionSwitchSubscribed;
        private readonly object videoSizeSync = new object();
        private long videoGeometryEventSequence;
        private Size pendingVideoSize = Size.Empty;
        private DateTime pendingVideoSizeDueUtc = DateTime.MinValue;
        private long pendingVideoSizeSequence;
        private bool pendingVideoSizeIsAmbiguousMediaCanvas;
        private Size currentVideoSize = Size.Empty;
        private long currentVideoSizeSequence;
        private bool currentVideoSizeIsAmbiguousMediaCanvas;
        private Size rawGeometryVideoSize = Size.Empty;
        private int rawGeometryVideoSizeGeneration;
        private bool rawGeometryIsAmbiguousMediaCanvas;
        private Size earlyDeviceFrameVideoSize = Size.Empty;
        private Size deviceFrameVideoSize = Size.Empty;
        private Size lastSuppressedVideoSize = Size.Empty;
        private int mirrorSessionGeneration;
        private IntPtr videoSizeWindow = IntPtr.Zero;
        private IntPtr initialFitPendingWindow = IntPtr.Zero;
        private long exactVideoSizeFitSequence = -1;
        private Size appliedVideoFitSize = Size.Empty;
        private RendererFitTargetKind appliedVideoFitTargetKind =
            RendererFitTargetKind.None;
        private int appliedVideoOrientation;
        private readonly NativeMethods.WinEventProc rendererMoveSizeEventProc;
        private readonly NativeMethods.WinEventProc rendererWindowShowEventProc;
        private IntPtr rendererMoveSizeHook = IntPtr.Zero;
        private IntPtr rendererWindowShowHook = IntPtr.Zero;
        private int rendererMoveSizeHookPid;
        private IntPtr rendererPolicyWindow = IntPtr.Zero;
        private bool rendererPolicyApplied;
        private bool rendererPolicyAlwaysOnTop;
        private int appliedPresentationScalePermille =
            RendererPresentationPolicy.NormalScalePermille;
        private bool rendererFullscreenActive;
        private int nativeFullscreenState;
        private long nativeFullscreenGeneration;
        private bool rendererPolicyShowInTaskbar;
        private IntPtr rendererMoveSizeWindow = IntPtr.Zero;
        private Size rendererMoveSizeStartClientSize = Size.Empty;
        private IntPtr pendingManualFitWindow = IntPtr.Zero;
        private long pendingManualFitDueTicks;
        private int pendingManualFit;
        private readonly object streamWindowPlacementSync = new object();
        private IntPtr pendingStreamWindowPlacementWindow = IntPtr.Zero;
        private DateTime pendingStreamWindowPlacementDueUtc = DateTime.MinValue;
        private int streamWindowPlacementSaveFailures;
        private IntPtr persistableStreamWindowPlacementWindow = IntPtr.Zero;
        private IntPtr restoredStreamWindowPlacementWindow = IntPtr.Zero;
        private int lostConnectionRecoveryPending;
        private int lostConnectionRecoveryPid;
        private long lostConnectionRecoveryDueTicks;
        private int feedbackGapEpisodeActive;
        private int feedbackGapEpisodeCount;
        private int feedbackGapLongestSeconds;
        private int feedbackGapPlaceholderActive;
        private int feedbackHealthMarkersReady;
        private int feedbackVideoPresentProofReady;
        private int feedbackVideoPresentProofPid;
        private int feedbackVideoRecoveryPending;
        private int feedbackVideoRecoveryPid;
        private int feedbackVideoRecoveryEpoch;
        private int feedbackVideoRecoveryGapSeconds;
        private int feedbackVideoRecoverySessionGeneration;
        private int feedbackVideoMirrorStartArmExpected;
        private long feedbackVideoRecoveryWaitDueTicks;
        private int feedbackVideoRecoveryCompletedCount;
        private int feedbackVideoRecoveryHintCount;
        private bool startAfterNetworkCheck;
        private int discoveryRefreshAfterNetworkCheck;
        private string receiverStateText = "Приёмник остановлен";
        private int receiverReady;

        public ReceiverContext(string[] args, EventWaitHandle showEvent)
        {
            this.showEvent = showEvent;
            rendererMoveSizeEventProc = OnRendererMoveSizeEvent;
            rendererWindowShowEventProc = OnRendererWindowShowEvent;
            bool show = false;
            bool startup = false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase))
                    show = true;
                if (string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase))
                    startup = true;
            }

            settings = AppSettings.Load();
            settings.Save();
            ApplyAutostart(settings.AutoStartWindows);
            SanitizeExistingLogs(
                settings.LegacyFixedPinForSanitization);

            statusItem = new ToolStripMenuItem("● Приёмник остановлен");
            statusItem.Enabled = false;
            startStopItem = new ToolStripMenuItem("Запустить приёмник", null, OnStartStop);
            autoStartItem = new ToolStripMenuItem("Запускать вместе с Windows", null, OnAutostart);
            autoStartItem.Checked = IsAutostartEnabled();
            topMostItem = new ToolStripMenuItem("Окно трансляции поверх остальных", null, OnAlwaysOnTop);
            topMostItem.Checked = settings.AlwaysOnTop;
            var menu = new ContextMenuStrip();
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Открыть настройки", null, delegate { ShowSettings(); });
            menu.Items.Add(startStopItem);
            menu.Items.Add("Перезапустить приёмник", null, delegate { RestartCore(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(autoStartItem);
            menu.Items.Add(topMostItem);
            menu.Items.Add("Показать окно трансляции", null, delegate
            {
                ShowStreamWindow(true);
            });
            menu.Items.Add("Восстановить пропорции окна", null, delegate { FitStreamWindow(true); });
            menu.Items.Add("Полный экран (Esc — выйти)", null, delegate
            {
                ToggleStreamWindowFullscreen(true);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Диагностика", null, delegate { ShowDiagnostics(); });
            menu.Items.Add("Открыть журнал", null, delegate { OpenLog(); });
            menu.Items.Add("Сообщить о проблеме", null, delegate
            {
                OpenProblemReport(null);
            });
            menu.Items.Add("Выход", null, delegate { RequestQuit(); });

            tray = new NotifyIcon();
            tray.Icon = AppIcon.Current;
            tray.Text = AppTitle;
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    ShowSettings();
            };
            tray.BalloonTipClicked += delegate { ShowSettings(); };
            tray.Visible = true;

            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = 250;
            monitorTimer.Tick += delegate { MonitorCore(); };
            monitorTimer.Start();
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            try
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                sessionSwitchSubscribed = true;
            }
            catch (Exception ex)
            {
                Log("Windows session-unlock discovery maintenance is " +
                    "unavailable: " + ex.Message);
            }

            Log("=== AeroMirror session started ===");
            Log("Shell version " +
                AppVersion.Display +
                "; Windows " + Environment.OSVersion +
                "; 64-bit process: " + Environment.Is64BitProcess +
                "; startup: " + startup + ".");
            Log("Executable: " +
                Path.GetFileName(Assembly.GetExecutingAssembly().Location));
            BeginAutomaticUpdateCheck();
            BeginNetworkProfileRefresh();
            BeginBonjourFirewallAssessment();
            if (settings.AutoStartReceiver)
            {
                startAfterNetworkCheck = true;
                SetState(false, "Проверяем безопасность сети…");
            }

            if (!startup || !settings.StartMinimized)
                show = true;
            if (show)
                ShowSettings();
        }

        public bool IsCoreRunning
        {
            get
            {
                try { return coreProcess != null && !coreProcess.HasExited; }
                catch { return false; }
            }
        }

        public AppSettings CurrentSettings { get { return settings; } }
        public bool IsPublicNetwork { get { return publicNetwork; } }
        public bool IsNetworkProfileKnown { get { return networkProfileKnown; } }
        public bool IsWaitingForNetwork
        {
            get
            {
                return startAfterNetworkCheck ||
                    Interlocked.CompareExchange(
                        ref discoveryRefreshAfterNetworkCheck, 0, 0) == 1;
            }
        }
        public string NetworkProfileName { get { return networkProfileName; } }
        public string NetworkInterfaceName { get { return networkInterfaceName; } }
        public bool HasNetworkOverlay
        {
            get { return nonPhysicalProfileCount > 0; }
        }
        public string ReceiverStateText { get { return receiverStateText; } }
        public bool IsReceiverReady
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref receiverReady, 0, 0) == 1;
            }
        }
        public bool IsMirrorSessionActive
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref mirrorSessionActive, 0, 0) == 1;
            }
        }
        public bool IsSettingsRestartDeferred
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref settingsRestartDeferred, 0, 0) == 1;
            }
        }

        public string CorePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "core", "uxplay-windows.exe");
            }
        }

        public void ShowSettings()
        {
            Log("Opening settings window.");
            if (form == null || form.IsDisposed)
            {
                form = new SettingsForm(this);
                form.FormClosed += delegate { form = null; };
            }
            form.SyncStatus();
            form.Show();
            form.WindowState = FormWindowState.Normal;
            form.Activate();
            Log("Settings window visible: " + form.Visible + ".");
        }

        public bool SaveSettings(AppSettings updated, bool restartIfCoreArgumentsChanged)
        {
            ResetRapidExitWindow();
            string previousArguments = BuildUxPlayArguments();
            bool wasRunning = IsCoreRunning;
            settings = updated;
            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
            settings.Save();
            ApplyAutostart(settings.AutoStartWindows);
            autoStartItem.Checked = IsAutostartEnabled();
            topMostItem.Checked = settings.AlwaysOnTop;
            Log("Settings saved.");
            string currentArguments = BuildUxPlayArguments();
            bool argumentsChanged = !string.Equals(
                previousArguments, currentArguments, StringComparison.Ordinal);
            bool restarted = false;

            if (!settings.AutoStartReceiver)
            {
                CloseLostConnectionPlaceholder();
                restartPending = false;
                restartAfterStop = false;
                startAfterNetworkCheck = false;
                Interlocked.Exchange(
                    ref discoveryRefreshAfterNetworkCheck, 0);
                if (IsCoreRunning)
                    StopCore();
            }
            else
            {
                if (wasRunning && IsCoreRunning &&
                    restartIfCoreArgumentsChanged && argumentsChanged)
                {
                    ApplyOrDeferSettingsRestart();
                    restarted = true;
                }
                else if (!IsCoreRunning)
                {
                    StartCore(false);
                    restarted = IsCoreRunning;
                }
                else
                {
                    ApplyTopMost();
                }
            }
            return restarted;
        }

        private void ApplyOrDeferSettingsRestart()
        {
            lock (postSessionMaintenanceSync)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                long clientGraceDueTicks = Interlocked.Read(
                    ref clientActivityGraceDueTicks);
                bool mirrorActive = IsMirrorSessionActive;
                if (ShouldDeferDisruptiveMaintenance(
                        mirrorActive, clientGraceDueTicks, nowTicks))
                {
                    Interlocked.Exchange(ref settingsRestartDeferred, 1);
                    if (!mirrorActive)
                    {
                        Interlocked.Exchange(
                            ref mirrorSessionEndedDueTicks,
                            Math.Max(clientGraceDueTicks,
                                DateTime.UtcNow.AddSeconds(5).Ticks));
                        Interlocked.Exchange(
                            ref mirrorSessionEndedPending, 1);
                    }
                    Log("Core argument changes were saved; restart deferred " +
                        "until the current AirPlay session or connection " +
                        "grace ends.");
                    return;
                }

                ScheduleRestart("settings changed", false, 1000);
            }
        }

        private bool ApplyNetworkProfile(NetworkProfileInfo profile, bool notify)
        {
            if (!profile.IsKnown && networkProfileKnown)
            {
                if (knownNetworkUnknownRetries < 3)
                {
                    knownNetworkUnknownRetries++;
                    Interlocked.Exchange(
                        ref networkRefreshDueTicks,
                        DateTime.UtcNow.AddSeconds(5).Ticks);
                    Interlocked.Exchange(ref networkRefreshPending, 1);
                    Log("Physical network profile temporarily returned Unknown; " +
                        "keeping the last known profile during safety grace period " +
                        "(" + knownNetworkUnknownRetries + "/3).");
                    return false;
                }
                Log("Physical network profile remained Unknown after the safety " +
                    "grace period; discarding the last known profile.");
            }
            if (profile.IsKnown)
                knownNetworkUnknownRetries = 0;
            string previousSignature = networkSignature;
            networkProfileKnown = profile.IsKnown;
            publicNetwork = profile.IsPublic;
            networkProfileName = profile.Name;
            networkInterfaceName = profile.InterfaceName;
            physicalNetworkAddresses = profile.IsKnown
                ? profile.Addresses
                : "";
            bool physicalNetworkReady = profile.IsKnown &&
                FirstNumericIpv4(physicalNetworkAddresses).Length > 0;
            nonPhysicalProfileCount = profile.NonPhysicalProfileCount;
            publicNonPhysicalProfileCount =
                profile.PublicNonPhysicalProfileCount;
            networkSignature = profile.Signature;
            // Every connection now uses per-device PIN trust, so Windows'
            // Public/Private label is informational rather than an access-
            // control choice. A usable physical IPv4 is still required.
            bool receiverStartedByProfile = false;
            bool changed = previousSignature.Length > 0 &&
                !string.Equals(previousSignature, networkSignature,
                    StringComparison.Ordinal);
            if (changed)
                ResetIdleDiscoveryRenewalSchedule();
            if (previousSignature.Length == 0 || changed)
            {
                Log("Physical network profile: " +
                    (profile.IsKnown ? profile.Category : "Unknown") +
                    " (physical interface " + profile.InterfaceName +
                    ", IPv4 count " + CountAddresses(profile.Addresses) + ")" +
                    "; non-physical overlays " +
                    profile.NonPhysicalProfileCount +
                    " (public " +
                    profile.PublicNonPhysicalProfileCount + ")" +
                    "; access: per-device trust" +
                    "; changed: " + changed + ".");
            }

            if (form != null && !form.IsDisposed)
                form.SyncStatus();

            bool discoveryRefreshPending =
                Interlocked.CompareExchange(
                    ref discoveryRefreshAfterNetworkCheck, 0, 0) == 1;
            if (discoveryRefreshPending && physicalNetworkReady)
            {
                Interlocked.Exchange(
                    ref discoveryRefreshAfterNetworkCheck, 0);
                startAfterNetworkCheck = false;
                networkUnknownRetries = 0;
                if (IsCoreRunning)
                {
                    ScheduleRestart(
                        "manual discovery refresh after network check",
                        false, 500);
                    receiverStartedByProfile = true;
                }
                else
                {
                    StartCore(false);
                    receiverStartedByProfile = IsCoreRunning;
                }
            }
            if (startAfterNetworkCheck)
            {
                if (physicalNetworkReady)
                {
                    startAfterNetworkCheck = false;
                    networkUnknownRetries = 0;
                    StartCore(false);
                    receiverStartedByProfile = IsCoreRunning;
                }
                else
                {
                    networkUnknownRetries++;
                    int retrySeconds = networkUnknownRetries <= 3 ? 2 : 5;
                    Interlocked.Exchange(
                        ref networkRefreshDueTicks,
                        DateTime.UtcNow.AddSeconds(retrySeconds).Ticks);
                    Interlocked.Exchange(ref networkRefreshPending, 1);
                    SetState(false,
                        "Проверяем сеть ещё раз…");
                    if (networkUnknownRetries <= 3 ||
                        networkUnknownRetries % 12 == 0)
                        Log("Initial physical network check returned Unknown; " +
                            "retry " + networkUnknownRetries + " scheduled in " +
                            retrySeconds + " seconds.");
                }
            }
            else if (discoveryRefreshPending && !physicalNetworkReady)
            {
                networkUnknownRetries++;
                int retrySeconds = networkUnknownRetries <= 3 ? 2 : 5;
                Interlocked.Exchange(
                    ref networkRefreshDueTicks,
                    DateTime.UtcNow.AddSeconds(retrySeconds).Ticks);
                Interlocked.Exchange(ref networkRefreshPending, 1);
                SetState(false, "Ждём адрес Wi-Fi/Ethernet…");
                if (networkUnknownRetries <= 3 ||
                    networkUnknownRetries % 12 == 0)
                    Log("Discovery refresh is still waiting for a physical " +
                        "IPv4 address; retry " + networkUnknownRetries +
                        " scheduled in " + retrySeconds + " seconds.");
            }
            return changed && !receiverStartedByProfile;
        }

        private void BeginNetworkProfileRefresh()
        {
            if (Interlocked.CompareExchange(ref networkRefreshRunning, 1, 0) != 0)
            {
                Interlocked.Exchange(
                    ref networkRefreshDueTicks,
                    DateTime.UtcNow.AddSeconds(1).Ticks);
                Interlocked.Exchange(ref networkRefreshPending, 1);
                return;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkProfileInfo profile = NetworkSafety.DetectPhysicalProfile();
                lock (networkProfileSync)
                    pendingNetworkProfile = profile;
                Interlocked.Exchange(ref networkRefreshRunning, 0);
            });
        }
    }
}

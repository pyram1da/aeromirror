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
        private enum RendererFitTargetKind
        {
            None,
            DeviceFrame,
            MediaCanvas
        }

        private void InstallRendererMoveSizeHook(int processId)
        {
            ResetRendererMoveSizeTracking();
            ClearStreamWindowPlacementPersistence(IntPtr.Zero);
            if (processId <= 0 || rendererMoveSizeEventProc == null ||
                rendererWindowShowEventProc == null)
                return;

            IntPtr moveSizeHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
                NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                rendererMoveSizeEventProc,
                (uint)processId,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT);
            IntPtr showHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_SHOW,
                NativeMethods.EVENT_OBJECT_SHOW,
                IntPtr.Zero,
                rendererWindowShowEventProc,
                (uint)processId,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT);
            rendererMoveSizeHook = moveSizeHook;
            rendererWindowShowHook = showHook;
            rendererMoveSizeHookPid = processId;
            if (moveSizeHook == IntPtr.Zero)
            {
                Log("Renderer move/size event hook was not available; " +
                    "manual window fitting remains available from the tray.");
            }
            if (showHook == IntPtr.Zero)
            {
                Log("Renderer show event hook was not available; saved " +
                    "placement will be restored by normal supervision.");
            }
            if (moveSizeHook != IntPtr.Zero || showHook != IntPtr.Zero)
            {
                Log("Watching renderer window events for core PID " +
                    processId + ".");
            }
        }

        private void ResetRendererMoveSizeTracking()
        {
            IntPtr hook = rendererMoveSizeHook;
            IntPtr showHook = rendererWindowShowHook;
            rendererMoveSizeHook = IntPtr.Zero;
            rendererWindowShowHook = IntPtr.Zero;
            rendererMoveSizeHookPid = 0;
            rendererPolicyWindow = IntPtr.Zero;
            rendererPolicyApplied = false;
            rendererPolicyAlwaysOnTop = false;
            rendererPolicyShowInTaskbar = false;
            rendererMoveSizeWindow = IntPtr.Zero;
            rendererMoveSizeStartClientSize = Size.Empty;
            ClearPendingManualRendererFit();
            ClearPendingStreamWindowPlacementSave();
            restoredStreamWindowPlacementWindow = IntPtr.Zero;
            if (hook != IntPtr.Zero)
            {
                try { NativeMethods.UnhookWinEvent(hook); }
                catch { }
            }
            if (showHook != IntPtr.Zero)
            {
                try { NativeMethods.UnhookWinEvent(showHook); }
                catch { }
            }
        }

        private void OnRendererWindowShowEvent(
            IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint eventThread, uint eventTime)
        {
            if (hook == IntPtr.Zero || hook != rendererWindowShowHook ||
                eventType != NativeMethods.EVENT_OBJECT_SHOW ||
                window == IntPtr.Zero ||
                objectId != NativeMethods.OBJID_WINDOW || childId != 0)
                return;

            int processId = Interlocked.CompareExchange(
                ref activeCorePid, 0, 0);
            if (processId <= 0 || processId != rendererMoveSizeHookPid)
                return;

            uint windowProcessId;
            NativeMethods.GetWindowThreadProcessId(window, out windowProcessId);
            if (windowProcessId != (uint)processId)
                return;

            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(window, title, title.Capacity);
            if (!IsRendererWindowTitle(title.ToString()))
                return;
            if (IsRendererFullscreenWindow(window))
                return;

            // EVENT_OBJECT_SHOW reaches the shell before the 250 ms polling
            // path. Apply only the already-loaded placement here: no settings
            // writes, logging, activation or aspect fitting belong in this
            // out-of-context callback.
            Rectangle ignoredBounds;
            int ignoredDpi;
            TryApplySavedStreamWindowPlacement(
                window, out ignoredBounds, out ignoredDpi);
        }

        private void OnRendererMoveSizeEvent(
            IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint eventThread, uint eventTime)
        {
            if (hook == IntPtr.Zero || hook != rendererMoveSizeHook ||
                window == IntPtr.Zero ||
                objectId != NativeMethods.OBJID_WINDOW || childId != 0)
                return;

            int processId = Interlocked.CompareExchange(
                ref activeCorePid, 0, 0);
            if (processId <= 0 || processId != rendererMoveSizeHookPid)
                return;

            uint windowProcessId;
            NativeMethods.GetWindowThreadProcessId(
                window, out windowProcessId);
            if (windowProcessId != (uint)processId)
                return;

            if (eventType == NativeMethods.EVENT_SYSTEM_MOVESIZESTART)
            {
                if (window != fittedStreamWindow &&
                    window != videoSizeWindow)
                    return;
                if (IsRendererFullscreenWindow(window))
                    return;

                Size clientSize;
                if (!TryGetRendererClientSize(window, out clientSize))
                    return;

                rendererMoveSizeWindow = window;
                rendererMoveSizeStartClientSize = clientSize;
                ClearPendingManualRendererFit();
                return;
            }

            if (eventType != NativeMethods.EVENT_SYSTEM_MOVESIZEEND ||
                rendererMoveSizeWindow != window)
                return;

            Size startSize = rendererMoveSizeStartClientSize;
            rendererMoveSizeWindow = IntPtr.Zero;
            rendererMoveSizeStartClientSize = Size.Empty;

            Size endSize;
            if (NativeMethods.IsIconic(window) ||
                NativeMethods.IsZoomed(window) ||
                IsRendererFullscreenWindow(window) ||
                !TryGetRendererClientSize(window, out endSize))
            {
                ClearPendingManualRendererFit();
                return;
            }

            // The out-of-context callback only queues persistence work.
            // Outer-bounds capture, normalization and the atomic settings
            // write run later on the WinForms supervision timer.
            MarkStreamWindowPlacementPersistable(window);
            QueueStreamWindowPlacementSave(window, 200);
            if (!ShouldQueueManualRendererFit(
                    settings.AutoFitWindow, startSize, endSize))
            {
                ClearPendingManualRendererFit();
                return;
            }

            pendingManualFitWindow = window;
            Interlocked.Exchange(
                ref pendingManualFitDueTicks,
                DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref pendingManualFit, 1);
        }

        private static bool ShouldQueueManualRendererFit(
            bool autoFitEnabled, Size startSize, Size endSize)
        {
            return autoFitEnabled && !startSize.IsEmpty && !endSize.IsEmpty &&
                (Math.Abs(endSize.Width - startSize.Width) > 4 ||
                 Math.Abs(endSize.Height - startSize.Height) > 4);
        }

        private static bool TryGetRendererClientSize(
            IntPtr window, out Size clientSize)
        {
            clientSize = Size.Empty;
            NativeMethods.RECT client;
            if (!NativeMethods.GetClientRect(window, out client))
                return false;
            int width = client.Right - client.Left;
            int height = client.Bottom - client.Top;
            if (width <= 0 || height <= 0)
                return false;
            clientSize = new Size(width, height);
            return true;
        }

        private static bool IsRendererFullscreenWindow(IntPtr window)
        {
            if (window == IntPtr.Zero || !NativeMethods.IsWindow(window) ||
                NativeMethods.IsIconic(window))
                return false;

            try
            {
                NativeMethods.RECT outer;
                NativeMethods.RECT client;
                if (!NativeMethods.GetWindowRect(window, out outer) ||
                    !NativeMethods.GetClientRect(window, out client))
                    return false;

                Rectangle monitor = Screen.FromHandle(window).Bounds;
                int outerWidth = outer.Right - outer.Left;
                int outerHeight = outer.Bottom - outer.Top;
                int clientWidth = client.Right - client.Left;
                int clientHeight = client.Bottom - client.Top;
                const int tolerance = 8;
                return Math.Abs(outer.Left - monitor.Left) <= tolerance &&
                    Math.Abs(outer.Top - monitor.Top) <= tolerance &&
                    Math.Abs(outer.Right - monitor.Right) <= tolerance &&
                    Math.Abs(outer.Bottom - monitor.Bottom) <= tolerance &&
                    Math.Abs(outerWidth - clientWidth) <= tolerance &&
                    Math.Abs(outerHeight - clientHeight) <= tolerance;
            }
            catch
            {
                return false;
            }
        }

        private void ClearPendingManualRendererFit()
        {
            Interlocked.Exchange(ref pendingManualFit, 0);
            Interlocked.Exchange(ref pendingManualFitDueTicks, 0);
            pendingManualFitWindow = IntPtr.Zero;
        }

        private bool ApplyPendingManualRendererFit(
            IntPtr window, Size videoSize, long videoSizeSequence,
            RendererFitTargetKind fitTargetKind)
        {
            if (Interlocked.CompareExchange(
                    ref pendingManualFit, 0, 0) != 1)
                return false;
            long dueTicks = Interlocked.Read(ref pendingManualFitDueTicks);
            if (dueTicks <= 0 || DateTime.UtcNow.Ticks < dueTicks)
                return false;

            IntPtr targetWindow = pendingManualFitWindow;
            ClearPendingManualRendererFit();
            if (!settings.AutoFitWindow || targetWindow != window ||
                rendererMoveSizeWindow == window ||
                !NativeMethods.IsWindow(window) ||
                !NativeMethods.IsWindowVisible(window) ||
                NativeMethods.IsIconic(window) ||
                NativeMethods.IsZoomed(window) ||
                IsRendererFullscreenWindow(window) ||
                NativeMethods.IsLeftMouseButtonDown())
                return false;

            if (!FitRendererWindow(window, videoSize, true))
            {
                Log("Automatic renderer fit after manual resize failed.");
                return false;
            }

            fittedStreamWindow = window;
            videoSizeWindow = window;
            initialFitPendingWindow = IntPtr.Zero;
            RecordAppliedRendererFit(
                window, videoSizeSequence, videoSize, fitTargetKind);
            Log("Automatically fitted renderer window after manual resize" +
                VideoSizeLogSuffix(videoSize) + ".");
            MarkStreamWindowPlacementPersistable(window);
            QueueStreamWindowPlacementSave(window, 250);
            return true;
        }

        private void MarkStreamWindowPlacementPersistable(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return;
            lock (streamWindowPlacementSync)
                persistableStreamWindowPlacementWindow = window;
        }

        private void ClearStreamWindowPlacementPersistence(IntPtr window)
        {
            lock (streamWindowPlacementSync)
            {
                if (window == IntPtr.Zero ||
                    persistableStreamWindowPlacementWindow == window)
                {
                    persistableStreamWindowPlacementWindow = IntPtr.Zero;
                }
            }
        }

        private bool CanPersistStreamWindowPlacement(IntPtr window)
        {
            lock (streamWindowPlacementSync)
            {
                return window != IntPtr.Zero &&
                    persistableStreamWindowPlacementWindow == window;
            }
        }

        private void QueueStreamWindowPlacementSave(
            IntPtr window, int delayMilliseconds)
        {
            if (window == IntPtr.Zero)
                return;
            lock (streamWindowPlacementSync)
            {
                pendingStreamWindowPlacementWindow = window;
                pendingStreamWindowPlacementDueUtc = DateTime.UtcNow.AddMilliseconds(
                    Math.Max(0, delayMilliseconds));
                streamWindowPlacementSaveFailures = 0;
            }
        }

        private void ClearPendingStreamWindowPlacementSave()
        {
            lock (streamWindowPlacementSync)
            {
                pendingStreamWindowPlacementWindow = IntPtr.Zero;
                pendingStreamWindowPlacementDueUtc = DateTime.MinValue;
                streamWindowPlacementSaveFailures = 0;
            }
        }

        private void SavePendingStreamWindowPlacement(IntPtr currentWindow)
        {
            IntPtr targetWindow;
            lock (streamWindowPlacementSync)
            {
                if (pendingStreamWindowPlacementWindow == IntPtr.Zero ||
                    DateTime.UtcNow < pendingStreamWindowPlacementDueUtc)
                    return;
                targetWindow = pendingStreamWindowPlacementWindow;
                pendingStreamWindowPlacementWindow = IntPtr.Zero;
                pendingStreamWindowPlacementDueUtc = DateTime.MinValue;
            }

            if (targetWindow != currentWindow ||
                !CanPersistStreamWindowPlacement(targetWindow))
                return;
            SaveStreamWindowPlacement(targetWindow, true);
        }

        private bool SaveStreamWindowPlacement(
            IntPtr targetWindow, bool allowRetry)
        {
            if (targetWindow == IntPtr.Zero ||
                !NativeMethods.IsWindow(targetWindow) ||
                !NativeMethods.IsWindowVisible(targetWindow) ||
                NativeMethods.IsIconic(targetWindow) ||
                NativeMethods.IsZoomed(targetWindow) ||
                IsRendererFullscreenWindow(targetWindow))
                return false;

            Rectangle bounds;
            if (!TryGetRendererOuterBounds(targetWindow, out bounds))
                return false;
            if (bounds.Width < 100 || bounds.Height < 100)
                return false;
            int dpi = NativeMethods.GetWindowDpi(targetWindow);
            if (settings.StreamWindowLeft == bounds.Left &&
                settings.StreamWindowTop == bounds.Top &&
                settings.StreamWindowWidth == bounds.Width &&
                settings.StreamWindowHeight == bounds.Height &&
                settings.StreamWindowDpi == dpi)
            {
                lock (streamWindowPlacementSync)
                    streamWindowPlacementSaveFailures = 0;
                return true;
            }

            int oldVersion = settings.SettingsVersion;
            int oldLeft = settings.StreamWindowLeft;
            int oldTop = settings.StreamWindowTop;
            int oldWidth = settings.StreamWindowWidth;
            int oldHeight = settings.StreamWindowHeight;
            int oldDpi = settings.StreamWindowDpi;
            settings.StreamWindowLeft = bounds.Left;
            settings.StreamWindowTop = bounds.Top;
            settings.StreamWindowWidth = bounds.Width;
            settings.StreamWindowHeight = bounds.Height;
            settings.StreamWindowDpi = dpi;
            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
            try
            {
                settings.Save();
                Log("Remembered renderer window placement " +
                    bounds.Left + "," + bounds.Top + " " +
                    bounds.Width + "x" + bounds.Height +
                    " at " + dpi + " DPI.");
                lock (streamWindowPlacementSync)
                    streamWindowPlacementSaveFailures = 0;
                return true;
            }
            catch (Exception ex)
            {
                settings.SettingsVersion = oldVersion;
                settings.StreamWindowLeft = oldLeft;
                settings.StreamWindowTop = oldTop;
                settings.StreamWindowWidth = oldWidth;
                settings.StreamWindowHeight = oldHeight;
                settings.StreamWindowDpi = oldDpi;
                Log("Renderer window placement could not be saved: " +
                    ex.Message);
                if (allowRetry)
                {
                    lock (streamWindowPlacementSync)
                    {
                        if (pendingStreamWindowPlacementWindow == IntPtr.Zero &&
                            streamWindowPlacementSaveFailures < 2)
                        {
                            streamWindowPlacementSaveFailures++;
                            pendingStreamWindowPlacementWindow = targetWindow;
                            pendingStreamWindowPlacementDueUtc =
                                DateTime.UtcNow.AddSeconds(
                                    streamWindowPlacementSaveFailures);
                        }
                    }
                }
                return false;
            }
        }

        private void FlushStreamWindowPlacementBeforeCoreStop()
        {
            IntPtr window;
            if (!TryGetRendererWindow(out window))
            {
                ClearStreamWindowPlacementPersistence(IntPtr.Zero);
                return;
            }
            if (IsRendererFullscreenWindow(window))
            {
                Log("Skipped renderer placement persistence while fullscreen.");
                ClearPendingStreamWindowPlacementSave();
                ClearStreamWindowPlacementPersistence(window);
                return;
            }
            if (CanPersistStreamWindowPlacement(window))
            {
                SaveStreamWindowPlacement(window, false);
            }
            else
            {
                Log("Skipped renderer placement persistence because the " +
                    "session never established a device-frame orientation " +
                    "and the user did not move or resize the window.");
            }
            RememberRendererBounds(window);
            ClearStreamWindowPlacementPersistence(window);
        }

        private bool TryRestoreStreamWindowPlacement(IntPtr window)
        {
            Rectangle restored;
            int targetDpi;
            if (!TryApplySavedStreamWindowPlacement(
                    window, out restored, out targetDpi))
                return false;

            Log("Restored renderer window placement " +
                restored.Left + "," + restored.Top + " " +
                restored.Width + "x" + restored.Height +
                " at " + targetDpi + " DPI.");
            return true;
        }

        private bool TryApplySavedStreamWindowPlacement(
            IntPtr window, out Rectangle restored, out int targetDpi)
        {
            restored = Rectangle.Empty;
            targetDpi = 96;
            if (!settings.HasValidStreamWindowPlacement())
                return false;

            Rectangle currentBounds;
            if (!TryGetRendererOuterBounds(window, out currentBounds))
                return false;

            Rectangle[] workAreas;
            try
            {
                Screen[] screens = Screen.AllScreens;
                workAreas = new Rectangle[screens.Length];
                for (int index = 0; index < screens.Length; index++)
                    workAreas[index] = screens[index].WorkingArea;
            }
            catch
            {
                workAreas = new[] { Screen.FromHandle(window).WorkingArea };
            }

            Rectangle savedBounds = new Rectangle(
                settings.StreamWindowLeft,
                settings.StreamWindowTop,
                settings.StreamWindowWidth,
                settings.StreamWindowHeight);
            Rectangle preliminary = ClampSavedStreamWindowBounds(
                savedBounds, currentBounds, workAreas,
                settings.StreamWindowDpi, settings.StreamWindowDpi);
            if (preliminary.IsEmpty || !SetRendererOuterBounds(window, preliminary))
                return false;

            targetDpi = NativeMethods.GetWindowDpi(window);
            restored = ClampSavedStreamWindowBounds(
                savedBounds, preliminary, workAreas,
                settings.StreamWindowDpi, targetDpi);
            if (restored.IsEmpty || !SetRendererOuterBounds(window, restored))
                return false;
            return true;
        }

        private static Rectangle ClampSavedStreamWindowBounds(
            Rectangle savedBounds, Rectangle currentBounds,
            Rectangle[] workAreas, int savedDpi, int targetDpi)
        {
            if (savedBounds.Width <= 0 || savedBounds.Height <= 0 ||
                workAreas == null || workAreas.Length == 0)
                return Rectangle.Empty;

            Rectangle targetArea = FindBestRendererWorkArea(
                savedBounds, workAreas);
            if (targetArea.IsEmpty)
                targetArea = FindBestRendererWorkArea(
                    currentBounds, workAreas);
            if (targetArea.IsEmpty)
            {
                foreach (Rectangle candidate in workAreas)
                {
                    if (candidate.Width > 0 && candidate.Height > 0)
                    {
                        targetArea = candidate;
                        break;
                    }
                }
            }
            if (targetArea.IsEmpty)
                return Rectangle.Empty;

            double dpiScale = savedDpi >= 48 && savedDpi <= 768 &&
                targetDpi >= 48 && targetDpi <= 768
                ? (double)targetDpi / savedDpi
                : 1.0;
            int width = Math.Max(1, (int)Math.Round(
                Math.Min(100000.0, savedBounds.Width * dpiScale)));
            int height = Math.Max(1, (int)Math.Round(
                Math.Min(100000.0, savedBounds.Height * dpiScale)));
            double fitScale = Math.Min(
                1.0,
                Math.Min(
                    (double)targetArea.Width / width,
                    (double)targetArea.Height / height));
            if (fitScale < 1.0)
            {
                width = Math.Max(1, (int)Math.Floor(width * fitScale));
                height = Math.Max(1, (int)Math.Floor(height * fitScale));
            }
            width = Math.Min(width, targetArea.Width);
            height = Math.Min(height, targetArea.Height);
            width = Math.Max(Math.Min(100, targetArea.Width), width);
            height = Math.Max(Math.Min(100, targetArea.Height), height);

            int maximumLeft = targetArea.Right - width;
            int maximumTop = targetArea.Bottom - height;
            int left = Math.Max(targetArea.Left,
                Math.Min(savedBounds.Left, maximumLeft));
            int top = Math.Max(targetArea.Top,
                Math.Min(savedBounds.Top, maximumTop));
            return new Rectangle(left, top, width, height);
        }

        private static Rectangle FindBestRendererWorkArea(
            Rectangle bounds, Rectangle[] workAreas)
        {
            Rectangle best = Rectangle.Empty;
            long bestArea = 0;
            if (bounds.Width <= 0 || bounds.Height <= 0 || workAreas == null)
                return best;
            foreach (Rectangle candidate in workAreas)
            {
                if (candidate.Width <= 0 || candidate.Height <= 0)
                    continue;
                long left = Math.Max((long)bounds.Left, candidate.Left);
                long top = Math.Max((long)bounds.Top, candidate.Top);
                long right = Math.Min((long)bounds.Right, candidate.Right);
                long bottom = Math.Min((long)bounds.Bottom, candidate.Bottom);
                long overlap = Math.Max(0, right - left) *
                    Math.Max(0, bottom - top);
                if (overlap > bestArea)
                {
                    bestArea = overlap;
                    best = candidate;
                }
            }
            return best;
        }

        private static bool TryGetRendererOuterBounds(
            IntPtr window, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            NativeMethods.RECT outer;
            if (!NativeMethods.GetWindowRect(window, out outer))
                return false;
            int width = outer.Right - outer.Left;
            int height = outer.Bottom - outer.Top;
            if (width <= 0 || height <= 0)
                return false;
            bounds = new Rectangle(outer.Left, outer.Top, width, height);
            return true;
        }

        private static bool SetRendererOuterBounds(
            IntPtr window, Rectangle bounds)
        {
            return bounds.Width > 0 && bounds.Height > 0 &&
                NativeMethods.SetWindowPos(
                    window, IntPtr.Zero, bounds.Left, bounds.Top,
                    bounds.Width, bounds.Height,
                    NativeMethods.SWP_NOZORDER |
                    NativeMethods.SWP_NOACTIVATE);
        }

        private bool TryGetRendererWindow(out IntPtr rendererWindow)
        {
            rendererWindow = IntPtr.Zero;
            if (!IsCoreRunning)
                return false;

            if (fittedStreamWindow != IntPtr.Zero &&
                NativeMethods.IsWindow(fittedStreamWindow) &&
                NativeMethods.IsWindowVisible(fittedStreamWindow))
            {
                uint cachedPid;
                NativeMethods.GetWindowThreadProcessId(
                    fittedStreamWindow, out cachedPid);
                if (cachedPid == (uint)coreProcess.Id)
                {
                    rendererWindow = fittedStreamWindow;
                    return true;
                }
                fittedStreamWindow = IntPtr.Zero;
            }

            int pid = coreProcess.Id;
            IntPtr foundWindow = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                uint windowPid;
                NativeMethods.GetWindowThreadProcessId(window, out windowPid);
                if (windowPid == (uint)pid && NativeMethods.IsWindowVisible(window))
                {
                    var title = new StringBuilder(512);
                    NativeMethods.GetWindowText(window, title, title.Capacity);
                    if (IsRendererWindowTitle(title.ToString()))
                    {
                        foundWindow = window;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            if (foundWindow != IntPtr.Zero)
            {
                rendererWindow = foundWindow;
                fittedStreamWindow = rendererWindow;
                return true;
            }
            return false;
        }

        private void ApplyTopMost()
        {
            if (!IsCoreRunning)
            {
                return;
            }
            IntPtr previousWindow = fittedStreamWindow;
            IntPtr window;
            if (!TryGetRendererWindow(out window))
            {
                return;
            }
            bool fullscreen = UpdateRendererFullscreenState(window);
            if (fullscreen)
            {
                // If fullscreen was reached before the first supervision pass,
                // re-discover the window after exit so normal placement and
                // policy initialization still run exactly once.
                if (previousWindow != window)
                    fittedStreamWindow = IntPtr.Zero;
                ClearPendingManualRendererFit();
                ApplyPresentationScale(
                    RendererPresentationPolicy.NormalScalePermille,
                    "fullscreen presentation");
                CompleteLostConnectionRendererHandoff();
                return;
            }
            RememberRendererBounds(window);

            bool newWindow = previousWindow != window;
            if (ShouldApplyRendererWindowPolicy(
                    window, rendererPolicyWindow,
                    rendererPolicyApplied,
                    settings.AlwaysOnTop, rendererPolicyAlwaysOnTop,
                    settings.ShowStreamInTaskbar,
                    rendererPolicyShowInTaskbar))
            {
                NativeMethods.SetWindowText(window, "iPhone · AeroMirror");
                NativeMethods.SetToolWindowStyle(
                    window, !settings.ShowStreamInTaskbar);
                NativeMethods.SetWindowPos(window,
                    settings.AlwaysOnTop
                        ? NativeMethods.HWND_TOPMOST
                        : NativeMethods.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOACTIVATE);
                rendererPolicyWindow = window;
                rendererPolicyApplied = true;
                rendererPolicyAlwaysOnTop = settings.AlwaysOnTop;
                rendererPolicyShowInTaskbar = settings.ShowStreamInTaskbar;
            }
            if (newWindow)
            {
                ClearPendingManualRendererFit();
                ClearPendingStreamWindowPlacementSave();
                ClearStreamWindowPlacementPersistence(IntPtr.Zero);
                rendererMoveSizeWindow = IntPtr.Zero;
                rendererMoveSizeStartClientSize = Size.Empty;
                NativeMethods.SetImmersiveDarkMode(window, true);
                videoSizeWindow = window;
                initialFitPendingWindow = window;
                exactVideoSizeFitSequence = -1;
                appliedVideoFitSize = Size.Empty;
                appliedVideoFitTargetKind = RendererFitTargetKind.None;
                appliedVideoOrientation = 0;
                restoredStreamWindowPlacementWindow =
                    TryRestoreStreamWindowPlacement(window)
                        ? window : IntPtr.Zero;
            }

            long videoSizeSequence;
            bool ambiguousMediaCanvas;
            Size videoSize = GetStableVideoSize(
                out videoSizeSequence, out ambiguousMediaCanvas);
            bool orientationAuthoritative;
            bool suppressionChanged;
            Size automaticVideoSize = ResolveAutomaticVideoSize(
                videoSize, ambiguousMediaCanvas,
                out orientationAuthoritative,
                out suppressionChanged);
            ApplyNonCroppingPresentationScale(ambiguousMediaCanvas);
            bool provisionalMediaCanvasFit = ambiguousMediaCanvas;
            RendererFitTargetKind fitTargetKind =
                ResolveRendererFitTargetKind(
                    automaticVideoSize, provisionalMediaCanvasFit);
            int automaticOrientation = VideoOrientation(automaticVideoSize);
            if (suppressionChanged)
            {
                Log("Retained the current renderer orientation for non-device " +
                    "video canvas " + videoSize.Width + "x" +
                    videoSize.Height + "; last device frame " +
                    (automaticVideoSize.IsEmpty
                        ? "is not known"
                        : automaticVideoSize.Width + "x" +
                            automaticVideoSize.Height) + ".");
            }
            if (!settings.AutoFitWindow)
                ClearPendingManualRendererFit();
            if (settings.AutoFitWindow &&
                rendererMoveSizeWindow != window &&
                !NativeMethods.IsLeftMouseButtonDown())
            {
                if (initialFitPendingWindow == window)
                {
                    if (FitRendererWindow(
                            window, automaticVideoSize,
                            restoredStreamWindowPlacementWindow == window))
                    {
                        ClearPendingManualRendererFit();
                        initialFitPendingWindow = IntPtr.Zero;
                        RecordAppliedRendererFit(
                            window, videoSizeSequence, automaticVideoSize,
                            fitTargetKind);
                        Log(provisionalMediaCanvasFit
                            ? "Applied a temporary renderer window fit for " +
                                "the Photos/media canvas " +
                                automaticVideoSize.Width + "x" +
                                automaticVideoSize.Height + "."
                            : "Applied initial renderer window fit" +
                                VideoSizeLogSuffix(automaticVideoSize) + ".");
                        UpdateStreamWindowPlacementAfterAutomaticFit(
                            window, automaticVideoSize,
                            provisionalMediaCanvasFit);
                    }
                }
                else if (videoSizeWindow == window &&
                    ShouldApplyRendererFitTarget(
                        appliedVideoFitSize, appliedVideoFitTargetKind,
                        automaticVideoSize, fitTargetKind))
                {
                    bool firstExactFit = appliedVideoFitTargetKind ==
                            RendererFitTargetKind.None ||
                        appliedVideoFitSize.IsEmpty;
                    if (FitRendererWindow(
                            window, automaticVideoSize,
                            firstExactFit
                                ? restoredStreamWindowPlacementWindow == window
                                : true))
                    {
                        ClearPendingManualRendererFit();
                        RecordAppliedRendererFit(
                            window, videoSizeSequence, automaticVideoSize,
                            fitTargetKind);
                        if (firstExactFit)
                        {
                            Log(provisionalMediaCanvasFit
                                ? "Temporarily fitted the renderer window to " +
                                    "the debounced Photos/media canvas " +
                                    automaticVideoSize.Width + "x" +
                                    automaticVideoSize.Height + "."
                                : "Refined renderer window fit for the first " +
                                    "exact device-frame size " +
                                    automaticVideoSize.Width + "x" +
                                    automaticVideoSize.Height + ".");
                        }
                        else
                        {
                            Log(provisionalMediaCanvasFit
                                ? "Temporarily adapted the renderer window to " +
                                    "the Photos/media canvas " +
                                    automaticVideoSize.Width + "x" +
                                    automaticVideoSize.Height + "."
                                : "Adapted renderer window to device-frame " +
                                    "aspect " + automaticVideoSize.Width +
                                    "x" + automaticVideoSize.Height + ".");
                        }
                        UpdateStreamWindowPlacementAfterAutomaticFit(
                            window, automaticVideoSize,
                            provisionalMediaCanvasFit);
                    }
                }
                else if (videoSizeWindow == window &&
                    !automaticVideoSize.IsEmpty &&
                    fitTargetKind != RendererFitTargetKind.None &&
                    exactVideoSizeFitSequence != videoSizeSequence)
                {
                    // A newer geometry event can resolve to the same target
                    // class and exact aspect (for example, a scaled copy or a
                    // suppressed media canvas). Consume it without moving the
                    // outer window so supervision does not reconsider it on
                    // every timer tick.
                    RecordAppliedRendererFit(
                        window, videoSizeSequence, automaticVideoSize,
                        fitTargetKind);
                }
                else if (appliedVideoOrientation == 0 &&
                    automaticOrientation != 0)
                {
                    appliedVideoOrientation = automaticOrientation;
                }
                ApplyPendingManualRendererFit(
                    window, automaticVideoSize, videoSizeSequence,
                    fitTargetKind);
            }
            SavePendingStreamWindowPlacement(window);
            RememberRendererBounds(window);
            CompleteLostConnectionRendererHandoff();
        }

        private bool UpdateRendererFullscreenState(IntPtr window)
        {
            bool fullscreen = IsRendererFullscreenWindow(window);
            if (rendererFullscreenActive != fullscreen)
            {
                rendererFullscreenActive = fullscreen;
                Log(fullscreen
                    ? "Renderer entered fullscreen; automatic window fitting is suspended."
                    : "Renderer left fullscreen; automatic window fitting resumed.");
            }
            return fullscreen;
        }

        private static RendererFitTargetKind ResolveRendererFitTargetKind(
            Size videoSize, bool provisionalMediaCanvasFit)
        {
            if (videoSize.IsEmpty)
                return RendererFitTargetKind.None;
            return provisionalMediaCanvasFit
                ? RendererFitTargetKind.MediaCanvas
                : RendererFitTargetKind.DeviceFrame;
        }

        private static bool ShouldApplyRendererFitTarget(
            Size appliedSize, RendererFitTargetKind appliedKind,
            Size targetSize, RendererFitTargetKind targetKind)
        {
            if (targetSize.IsEmpty ||
                targetKind == RendererFitTargetKind.None)
                return false;
            // The applied target is the durable acknowledgement. A geometry
            // sequence can already be consumed when a live setting change is
            // temporarily blocked by a drag, mouse press, or AutoFit=false.
            // Keep any class/aspect mismatch eligible until a successful fit
            // records the new target.
            return appliedKind != targetKind ||
                !HaveExactRendererFitAspect(appliedSize, targetSize);
        }

        private static bool HaveExactRendererFitAspect(
            Size first, Size second)
        {
            return first.Width > 0 && first.Height > 0 &&
                second.Width > 0 && second.Height > 0 &&
                (long)first.Width * second.Height ==
                    (long)second.Width * first.Height;
        }

        private void RecordAppliedRendererFit(
            IntPtr window, long videoSizeSequence, Size videoSize,
            RendererFitTargetKind fitTargetKind)
        {
            if (videoSize.IsEmpty ||
                fitTargetKind == RendererFitTargetKind.None)
            {
                exactVideoSizeFitSequence = -1;
                appliedVideoFitSize = Size.Empty;
                appliedVideoFitTargetKind = RendererFitTargetKind.None;
            }
            else
            {
                exactVideoSizeFitSequence = videoSizeSequence;
                appliedVideoFitSize = videoSize;
                appliedVideoFitTargetKind = fitTargetKind;
            }

            int orientation = VideoOrientation(videoSize);
            appliedVideoOrientation = orientation != 0
                ? orientation : GetWindowOrientation(window);
        }

        private void UpdateStreamWindowPlacementAfterAutomaticFit(
            IntPtr window, Size videoSize, bool provisionalMediaCanvasFit)
        {
            if (provisionalMediaCanvasFit)
            {
                // Automatic Photos/media landscape is an A/B presentation,
                // not a trusted device placement. Do not let it overwrite the
                // saved user position. An explicit move/resize will mark the
                // resulting placement as user-owned through the normal hook.
                ClearPendingStreamWindowPlacementSave();
                ClearStreamWindowPlacementPersistence(window);
                return;
            }
            if (!videoSize.IsEmpty)
            {
                MarkStreamWindowPlacementPersistable(window);
                QueueStreamWindowPlacementSave(window, 250);
                return;
            }

            // A fallback fit must not replace a previously valid placement
            // before the stream exposes a trustworthy device-frame shape.
            ClearPendingStreamWindowPlacementSave();
        }

        private static bool ShouldApplyRendererWindowPolicy(
            IntPtr window, IntPtr previousWindow, bool policyApplied,
            bool alwaysOnTop, bool previousAlwaysOnTop,
            bool showInTaskbar, bool previousShowInTaskbar)
        {
            return window != IntPtr.Zero &&
                (!policyApplied || window != previousWindow ||
                 alwaysOnTop != previousAlwaysOnTop ||
                 showInTaskbar != previousShowInTaskbar);
        }

        private void ShowStreamWindow(bool notifyIfMissing)
        {
            if (!IsCoreRunning)
            {
                if (notifyIfMissing && settings.Notify)
                    tray.ShowBalloonTip(3000, AppTitle,
                        "Сначала подключите iPhone: окно трансляции пока не открыто.",
                        ToolTipIcon.Info);
                return;
            }

            IntPtr window = IntPtr.Zero;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (TryGetRendererWindow(out window))
                    break;
                if (attempt < 4)
                    Thread.Sleep(150);
            }

            if (window != IntPtr.Zero &&
                NativeMethods.RestoreAndActivateWindow(window))
            {
                ClearRendererDismissedForCurrentSession();
                Log("Renderer window restored and activated explicitly.");
                return;
            }

            Log("Renderer window restore skipped: no visible renderer " +
                "window could be restored.");
            if (notifyIfMissing && settings.Notify)
                tray.ShowBalloonTip(3000, AppTitle,
                    "Окно трансляции пока не найдено. Подключите iPhone и повторите.",
                    ToolTipIcon.Info);
        }

        private void FitStreamWindow(bool notifyIfMissing)
        {
            if (!IsCoreRunning)
            {
                if (notifyIfMissing && settings.Notify)
                    tray.ShowBalloonTip(3000, AppTitle,
                        "Сначала подключите iPhone: окно трансляции пока не открыто.",
                        ToolTipIcon.Info);
                return;
            }

            IntPtr window = IntPtr.Zero;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (TryGetRendererWindow(out window))
                    break;
                if (attempt < 4)
                    Thread.Sleep(150);
            }

            if (window != IntPtr.Zero)
            {
                if (IsRendererFullscreenWindow(window))
                {
                    Log("Manual renderer fit skipped while fullscreen.");
                    if (notifyIfMissing && settings.Notify)
                        tray.ShowBalloonTip(2500, AppTitle,
                            "Сначала выйдите из полноэкранного режима клавишей Esc.",
                            ToolTipIcon.Info);
                    return;
                }
                long videoSizeSequence;
                bool ambiguousMediaCanvas;
                Size rawVideoSize = GetStableVideoSize(
                    out videoSizeSequence, out ambiguousMediaCanvas);
                Size videoSize = ResolveManualFitVideoSize(
                    rawVideoSize, ambiguousMediaCanvas);
                bool provisionalMediaCanvasFit = ambiguousMediaCanvas;
                RendererFitTargetKind fitTargetKind =
                    ResolveRendererFitTargetKind(
                        videoSize, provisionalMediaCanvasFit);
                if (FitRendererWindow(window, videoSize, false))
                {
                    ClearPendingManualRendererFit();
                    fittedStreamWindow = window;
                    videoSizeWindow = window;
                    initialFitPendingWindow = IntPtr.Zero;
                    RecordAppliedRendererFit(
                        window, videoSizeSequence, videoSize, fitTargetKind);
                    Log("Renderer window fitted manually" +
                        VideoSizeLogSuffix(videoSize) + ".");
                    MarkStreamWindowPlacementPersistable(window);
                    QueueStreamWindowPlacementSave(window, 250);
                    RememberRendererBounds(window);
                    return;
                }
                Log("Manual renderer window fit failed for the visible " +
                    "renderer window.");
                return;
            }

            Log("Manual renderer window fit skipped: no visible renderer " +
                "window was found after five attempts.");
            if (notifyIfMissing && settings.Notify)
                tray.ShowBalloonTip(3000, AppTitle,
                    "Окно трансляции пока не найдено. Подключите iPhone и повторите.",
                    ToolTipIcon.Info);
        }

        private void ToggleStreamWindowFullscreen(bool notifyIfMissing)
        {
            if (!IsCoreRunning)
            {
                if (notifyIfMissing && settings.Notify)
                    tray.ShowBalloonTip(3000, AppTitle,
                        "Сначала подключите iPhone: окно трансляции пока не открыто.",
                        ToolTipIcon.Info);
                return;
            }

            IntPtr window = IntPtr.Zero;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (TryGetRendererWindow(out window))
                    break;
                if (attempt < 4)
                    Thread.Sleep(150);
            }

            if (window != IntPtr.Zero)
            {
                bool fullscreenSnapshot =
                    IsRendererFullscreenWindow(window);
                if (!fullscreenSnapshot)
                    ApplyPresentationScale(
                        RendererPresentationPolicy.NormalScalePermille,
                        "fullscreen entry");
                bool enterFullscreen =
                    Interlocked.CompareExchange(
                        ref nativeFullscreenState, 0, 0) == 0;
                string command = "video-fullscreen-set state=" +
                    (enterFullscreen ? "1" : "0");
                if (TryWriteNativeVideoCommand(
                        command,
                        enterFullscreen
                            ? "fullscreen entry"
                            : "fullscreen exit"))
                {
                    Log(enterFullscreen
                        ? "Requested renderer fullscreen entry."
                        : "Requested renderer fullscreen exit.");
                    return;
                }
            }

            Log("Renderer fullscreen toggle skipped: no visible renderer " +
                "window accepted the native command.");
            if (notifyIfMissing && settings.Notify)
                tray.ShowBalloonTip(3000, AppTitle,
                    "Окно трансляции пока не найдено. Подключите iPhone и повторите.",
                    ToolTipIcon.Info);
        }

        private void ApplyNonCroppingPresentationScale(
            bool photosCanvas)
        {
            // The Photos marker identifies only a transport canvas. Without a
            // trusted content rectangle, any cover/fill scale can discard real
            // pixels. Keep the complete frame visible and let the sink contain
            // it inside the portrait-oriented outer window.
            ApplyPresentationScale(
                RendererPresentationPolicy.NormalScalePermille,
                photosCanvas
                    ? "non-cropping Photos presentation"
                    : "normal presentation");
        }

        private bool ApplyPresentationScale(int desired, string reason)
        {
            int current = Interlocked.CompareExchange(
                ref appliedPresentationScalePermille, 0, 0);
            if (current == desired)
                return true;
            if (!TryWriteNativeVideoCommand(
                    "video-scale permille=" + desired,
                    reason + " scale " + desired + "/1000"))
                return false;

            Interlocked.Exchange(
                ref appliedPresentationScalePermille, desired);
            Log("Renderer presentation scale set to " +
                (desired / 10.0).ToString("0.0") + "% (" + reason + ").");
            return true;
        }

        private Size ResolveManualFitVideoSize(
            Size rawVideoSize, bool ambiguousMediaCanvas)
        {
            bool orientationAuthoritative;
            bool suppressionChanged;
            return ResolveAutomaticVideoSize(
                rawVideoSize, ambiguousMediaCanvas,
                out orientationAuthoritative,
                out suppressionChanged);
        }

        private static bool IsRendererWindowTitle(string value)
        {
            return value.IndexOf("renderer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("AirPlay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("AeroMirror", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Size GetStableVideoSize(
            out long sequence, out bool ambiguousMediaCanvas)
        {
            lock (videoSizeSync)
            {
                if (!pendingVideoSize.IsEmpty &&
                    DateTime.UtcNow >= pendingVideoSizeDueUtc)
                {
                    currentVideoSize = pendingVideoSize;
                    currentVideoSizeSequence = pendingVideoSizeSequence;
                    currentVideoSizeIsAmbiguousMediaCanvas =
                        pendingVideoSizeIsAmbiguousMediaCanvas;
                    ClearPendingVideoSizeLocked();
                }
                sequence = currentVideoSizeSequence;
                ambiguousMediaCanvas =
                    currentVideoSizeIsAmbiguousMediaCanvas;
                return currentVideoSize;
            }
        }

        private Size ResolveAutomaticVideoSize(
            Size videoSize, bool ambiguousMediaCanvas,
            out bool orientationAuthoritative,
            out bool suppressionChanged)
        {
            orientationAuthoritative = false;
            suppressionChanged = false;
            if (videoSize.IsEmpty)
                return Size.Empty;

            lock (videoSizeSync)
            {
                if (deviceFrameVideoSize.IsEmpty &&
                    !earlyDeviceFrameVideoSize.IsEmpty)
                {
                    deviceFrameVideoSize = earlyDeviceFrameVideoSize;
                }

                if (ambiguousMediaCanvas)
                {
                    // Photos uses a landscape transport canvas even for a
                    // portrait phone. Keep the last trusted device shape (or
                    // the conservative iPhone portrait fallback) as the
                    // shell-window target. The complete transport frame stays
                    // contained because this marker is not a content rectangle.
                    Size presentationTarget = deviceFrameVideoSize.IsEmpty
                        ? RendererPresentationPolicy.ProvisionalPortraitSize
                        : deviceFrameVideoSize;
                    suppressionChanged = lastSuppressedVideoSize != videoSize;
                    lastSuppressedVideoSize = videoSize;
                    return presentationTarget;
                }

                if (deviceFrameVideoSize.IsEmpty)
                {
                    deviceFrameVideoSize = videoSize;
                    lastSuppressedVideoSize = Size.Empty;
                    orientationAuthoritative = true;
                    return videoSize;
                }

                if (HaveEquivalentDeviceFrameAspect(
                        deviceFrameVideoSize, videoSize))
                {
                    deviceFrameVideoSize = videoSize;
                    lastSuppressedVideoSize = Size.Empty;
                    orientationAuthoritative = true;
                    return videoSize;
                }

                suppressionChanged = lastSuppressedVideoSize != videoSize;
                lastSuppressedVideoSize = videoSize;
                return deviceFrameVideoSize;
            }
        }

        private static bool IsKnownAmbiguousMediaCanvasGeometry(
            int width0, int height0,
            int sourceWidth, int sourceHeight,
            int auxiliaryWidth, int auxiliaryHeight,
            int encodedWidth, int encodedHeight)
        {
            // This exact signature is replayed from the observed Photos
            // presentation canvas. The auxiliary pair is deliberately not
            // interpreted as crop, PAR, or rotation metadata; it is only
            // part of a complete, conservative signature that keeps a
            // generic 4K 16:9 canvas from becoming the device baseline.
            return RendererPresentationPolicy.IsKnownPhotosCanvas(
                width0, height0, sourceWidth, sourceHeight,
                auxiliaryWidth, auxiliaryHeight,
                encodedWidth, encodedHeight);
        }

        private static bool HaveEquivalentDeviceFrameAspect(
            Size first, Size second)
        {
            return RendererPresentationPolicy.HaveEquivalentDeviceAspect(
                first, second);
        }

        private static bool IsLikelyModernIPhoneDeviceFrame(Size videoSize)
        {
            return RendererPresentationPolicy.IsLikelyModernIPhoneFrame(
                videoSize);
        }

        private static double NormalizedVideoAspect(Size videoSize)
        {
            return RendererPresentationPolicy.NormalizedAspect(videoSize);
        }

        private static int VideoOrientation(Size videoSize)
        {
            if (videoSize.Width <= 0 || videoSize.Height <= 0 ||
                videoSize.Width == videoSize.Height)
                return 0;
            return videoSize.Height > videoSize.Width ? 1 : 2;
        }

        private static int GetWindowOrientation(IntPtr window)
        {
            NativeMethods.RECT client;
            if (!NativeMethods.GetClientRect(window, out client))
                return 0;
            int width = client.Right - client.Left;
            int height = client.Bottom - client.Top;
            if (width <= 0 || height <= 0 || width == height)
                return 0;
            return height > width ? 1 : 2;
        }

        private static string VideoSizeLogSuffix(Size videoSize)
        {
            return videoSize.Width > 0 && videoSize.Height > 0
                ? " for " + videoSize.Width + "x" + videoSize.Height
                : " using the iPhone fallback aspect";
        }

        private static bool FitRendererWindow(
            IntPtr window, Size videoSize, bool preserveClientArea)
        {
            NativeMethods.RECT outer;
            NativeMethods.RECT client;
            if (!NativeMethods.GetWindowRect(window, out outer) ||
                !NativeMethods.GetClientRect(window, out client))
                return false;

            int outerWidth = outer.Right - outer.Left;
            int outerHeight = outer.Bottom - outer.Top;
            int clientWidth = client.Right - client.Left;
            int clientHeight = client.Bottom - client.Top;
            if (outerWidth <= 0 || outerHeight <= 0 ||
                clientWidth <= 0 || clientHeight <= 0)
                return false;

            double aspect = videoSize.Width > 0 && videoSize.Height > 0
                ? (double)videoSize.Width / videoSize.Height
                : (clientHeight >= clientWidth
                    ? RendererPresentationPolicy.ModernIPhonePortraitAspect :
                        1.0 /
                            RendererPresentationPolicy.ModernIPhonePortraitAspect);
            int targetClientWidth;
            int targetClientHeight;
            if (preserveClientArea)
            {
                double area = Math.Max(
                    1.0, (double)clientWidth * clientHeight);
                targetClientWidth = Math.Max(
                    1, (int)Math.Round(Math.Sqrt(area * aspect)));
                targetClientHeight = Math.Max(
                    1, (int)Math.Round(targetClientWidth / aspect));
            }
            else if (aspect <= 1.0)
            {
                targetClientHeight = clientHeight;
                targetClientWidth =
                    (int)Math.Round(targetClientHeight * aspect);
                if (targetClientWidth < 280)
                {
                    targetClientWidth = 280;
                    targetClientHeight =
                        (int)Math.Round(targetClientWidth / aspect);
                }
            }
            else
            {
                targetClientWidth = clientWidth;
                targetClientHeight =
                    (int)Math.Round(targetClientWidth / aspect);
                if (targetClientHeight < 280)
                {
                    targetClientHeight = 280;
                    targetClientWidth =
                        (int)Math.Round(targetClientHeight * aspect);
                }
            }

            int borderWidth = outerWidth - clientWidth;
            int borderHeight = outerHeight - clientHeight;
            Rectangle workArea = Screen.FromHandle(window).WorkingArea;
            int maxClientWidth = Math.Max(
                280, (int)Math.Floor(workArea.Width * 0.88) - borderWidth);
            int maxClientHeight = Math.Max(
                280, (int)Math.Floor(workArea.Height * 0.88) - borderHeight);
            double scale = Math.Min(
                1.0,
                Math.Min(
                    (double)maxClientWidth / targetClientWidth,
                    (double)maxClientHeight / targetClientHeight));
            if (scale < 1.0)
            {
                targetClientWidth =
                    Math.Max(1, (int)Math.Round(targetClientWidth * scale));
                targetClientHeight =
                    Math.Max(1, (int)Math.Round(targetClientHeight * scale));
            }
            if (Math.Abs(targetClientWidth - clientWidth) <= 4 &&
                Math.Abs(targetClientHeight - clientHeight) <= 4)
                return true;

            int targetOuterWidth = targetClientWidth + borderWidth;
            int targetOuterHeight = targetClientHeight + borderHeight;
            int x = outer.Left;
            int y = outer.Top;
            uint flags = NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE;
            if (preserveClientArea)
            {
                int centerX = outer.Left + outerWidth / 2;
                int centerY = outer.Top + outerHeight / 2;
                x = centerX - targetOuterWidth / 2;
                y = centerY - targetOuterHeight / 2;
                x = Math.Max(
                    workArea.Left,
                    Math.Min(x, workArea.Right - targetOuterWidth));
                y = Math.Max(
                    workArea.Top,
                    Math.Min(y, workArea.Bottom - targetOuterHeight));
            }
            else
            {
                flags |= NativeMethods.SWP_NOMOVE;
            }
            return NativeMethods.SetWindowPos(
                window, IntPtr.Zero, x, y,
                targetOuterWidth, targetOuterHeight, flags);
        }
    }
}

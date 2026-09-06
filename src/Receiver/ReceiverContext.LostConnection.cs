using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private enum LostConnectionPlaceholderAction
        {
            None,
            Show,
            Close
        }

        private enum LostConnectionPresentationState
        {
            None,
            Lost,
            ReconnectHint,
            Recovered
        }

        private int lostConnectionPlaceholderShowPending;
        private int lostConnectionPlaceholderClosePending;
        private int lostConnectionRendererHandoffPending;
        private int lostConnectionLostStatePending;
        private int lostConnectionReconnectHintPending;
        private int lostConnectionRecoveredStatePending;
        private int lostConnectionPlaceholderDismissedSessionGeneration = -1;
        private int lostConnectionRendererDismissedSessionGeneration = -1;
        private long nativeMirrorSessionId;
        private long lostConnectionContinuityToken;
        private int lostConnectionFeedbackHandoffPending;
        private long lostConnectionFeedbackHandoffToken;
        private int lostConnectionFeedbackHandoffPid;
        private int lostConnectionFeedbackHandoffSessionGeneration;
        private int lostConnectionFeedbackHandoffEpoch;
        private long feedbackGapPlaceholderDueTicks;
        private Rectangle lastRendererBounds = Rectangle.Empty;
        private LostConnectionForm lostConnectionForm;

        private void QueueLostConnectionPlaceholder()
        {
            int sessionGeneration = Interlocked.CompareExchange(
                ref mirrorSessionGeneration, 0, 0);
            if (Interlocked.CompareExchange(
                    ref lostConnectionPlaceholderDismissedSessionGeneration,
                    0,
                    0) == sessionGeneration ||
                Interlocked.CompareExchange(
                    ref lostConnectionRendererDismissedSessionGeneration,
                    0,
                    0) == sessionGeneration)
            {
                Interlocked.Exchange(
                    ref lostConnectionPlaceholderShowPending, 0);
                return;
            }
            InvalidateLostConnectionRendererHandoff();
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 1);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(
                ref lostConnectionRecoveredStatePending, 0);
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderShowPending, 1);
        }

        private void QueueLostConnectionPlaceholderClose()
        {
            InvalidateLostConnectionRendererHandoff();
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(
                ref lostConnectionRecoveredStatePending, 0);
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderShowPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 1);
        }

        private void QueueLostConnectionRendererHandoff()
        {
            InvalidateLostConnectionRendererHandoff();
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 1);
            Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 1);
        }

        private void QueueLostConnectionRecoveredWait()
        {
            InvalidateLostConnectionRendererHandoff();
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 1);
        }

        private void QueueLostConnectionReconnectHint()
        {
            InvalidateLostConnectionRendererHandoff();
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(
                ref lostConnectionRecoveredStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 1);
        }

        private void QueueLostConnectionFeedbackRendererHandoff(
            int processId, int sessionGeneration, int epoch)
        {
            long continuityToken = Interlocked.Read(
                ref lostConnectionContinuityToken);
            Interlocked.Exchange(
                ref lostConnectionFeedbackHandoffToken, continuityToken);
            Interlocked.Exchange(
                ref lostConnectionFeedbackHandoffPid, processId);
            Interlocked.Exchange(
                ref lostConnectionFeedbackHandoffSessionGeneration,
                sessionGeneration);
            Interlocked.Exchange(
                ref lostConnectionFeedbackHandoffEpoch, epoch);
            Interlocked.Exchange(ref lostConnectionFeedbackHandoffPending, 1);
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 1);
            Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 1);
        }

        private void InvalidateLostConnectionRendererHandoff()
        {
            Interlocked.Increment(ref lostConnectionContinuityToken);
            Interlocked.Exchange(ref lostConnectionFeedbackHandoffPending, 0);
            Interlocked.Exchange(ref lostConnectionFeedbackHandoffToken, 0);
            Interlocked.Exchange(ref lostConnectionFeedbackHandoffPid, 0);
            Interlocked.Exchange(
                ref lostConnectionFeedbackHandoffSessionGeneration, 0);
            Interlocked.Exchange(ref lostConnectionFeedbackHandoffEpoch, 0);
        }

        private void RememberRendererBounds(IntPtr window)
        {
            NativeMethods.RECT nativeBounds;
            if (window == IntPtr.Zero ||
                IsRendererFullscreenWindow(window) ||
                !NativeMethods.GetWindowRect(window, out nativeBounds))
                return;

            int width = nativeBounds.Right - nativeBounds.Left;
            int height = nativeBounds.Bottom - nativeBounds.Top;
            if (width <= 0 || height <= 0)
                return;
            lastRendererBounds = new Rectangle(
                nativeBounds.Left, nativeBounds.Top, width, height);
        }

        private void HandleLostConnectionPlaceholder()
        {
            bool lostStateRequested = Interlocked.Exchange(
                ref lostConnectionLostStatePending, 0) == 1;
            bool reconnectHintRequested = Interlocked.Exchange(
                ref lostConnectionReconnectHintPending, 0) == 1;
            bool recoveredStateRequested = Interlocked.Exchange(
                ref lostConnectionRecoveredStatePending, 0) == 1;
            bool closeRequested = Interlocked.Exchange(
                ref lostConnectionPlaceholderClosePending, 0) == 1;
            bool showRequested = Interlocked.Exchange(
                ref lostConnectionPlaceholderShowPending, 0) == 1;
            if (Interlocked.Exchange(
                    ref lostConnectionPlaceholderClosePending, 0) == 1)
                closeRequested = true;

            bool visible = lostConnectionForm != null &&
                !lostConnectionForm.IsDisposed;
            LostConnectionPresentationState presentationState =
                DecideLostConnectionPresentationState(
                    lostStateRequested,
                    reconnectHintRequested,
                    recoveredStateRequested);
            LostConnectionPlaceholderAction action =
                DecideLostConnectionPlaceholderAction(
                    showRequested, closeRequested, visible);
            if (action == LostConnectionPlaceholderAction.Close)
            {
                CloseLostConnectionPlaceholder();
                return;
            }
            if (visible)
            {
                ApplyLostConnectionPresentation(
                    lostConnectionForm, presentationState);
                if (presentationState ==
                    LostConnectionPresentationState.Recovered)
                {
                    Log("AirPlay connection recovered; continuity remains " +
                        "visible until the renderer produces an image.");
                }
                else if (presentationState ==
                    LostConnectionPresentationState.ReconnectHint)
                {
                    Log("The lost AirPlay session finished; continuity now " +
                        "explains how to reconnect from the iPhone.");
                }
            }
            if (action != LostConnectionPlaceholderAction.Show || quitting)
                return;

            // The displayed warning belongs to this session, not to whichever
            // phone happens to be streaming when its Close event is delivered.
            int placeholderPid;
            int placeholderGeneration;
            long placeholderNativeSessionId;
            lock (postSessionMaintenanceSync)
            {
                placeholderPid = Interlocked.CompareExchange(ref activeCorePid, 0, 0);
                placeholderGeneration = Interlocked.CompareExchange(
                    ref mirrorSessionGeneration, 0, 0);
                placeholderNativeSessionId = Interlocked.Read(ref nativeMirrorSessionId);
            }

            IntPtr rendererWindow;
            if (TryGetRendererWindow(out rendererWindow))
                RememberRendererBounds(rendererWindow);
            else
                rendererWindow = IntPtr.Zero;

            Rectangle rendererBounds = lastRendererBounds;
            Rectangle bounds = ResolveLostConnectionPlaceholderBounds(
                rendererBounds);
            Bitmap snapshot = TryCaptureRendererSnapshot(
                rendererWindow, rendererBounds);
            try
            {
                if (quitting || Interlocked.Exchange(
                        ref lostConnectionPlaceholderClosePending, 0) == 1)
                    return;
                lock (postSessionMaintenanceSync)
                {
                    if (!IsCurrentLostConnectionSessionLocked(placeholderPid,
                            placeholderGeneration, placeholderNativeSessionId))
                        return;
                }
                var placeholder = new LostConnectionForm(bounds, snapshot);
                snapshot = null;
                placeholder.ShowInTaskbar = settings.ShowStreamInTaskbar;
                placeholder.TopMost = settings.AlwaysOnTop;
                placeholder.UserDismissed += delegate
                {
                    DismissLostConnectionPlaceholderForSession(placeholderPid,
                        placeholderGeneration, placeholderNativeSessionId);
                };
                placeholder.FormClosed += delegate
                {
                    if (ReferenceEquals(lostConnectionForm, placeholder))
                    {
                        lostConnectionForm = null;
                        Log("Lost-connection placeholder was closed.");
                    }
                };
                lostConnectionForm = placeholder;
                ApplyLostConnectionPresentation(
                    placeholder, presentationState);
                placeholder.Show();
                if (!placeholder.BringAboveRendererWithoutActivation(
                        rendererWindow))
                {
                    Log("Lost-connection placeholder could not be moved above " +
                        "the renderer without activation.");
                }
                if (presentationState ==
                    LostConnectionPresentationState.Recovered)
                {
                    Log("AirPlay connection recovered before continuity was " +
                        "displayed; waiting for the renderer image.");
                }
                else if (presentationState ==
                    LostConnectionPresentationState.ReconnectHint)
                {
                    Log("The lost AirPlay session finished before continuity " +
                        "was displayed; reconnect guidance is visible.");
                }
                Log("Lost-connection placeholder opened at the last renderer " +
                    "bounds; waiting for a new mirroring start.");
            }
            finally
            {
                if (snapshot != null)
                    snapshot.Dispose();
            }
        }

        private bool IsCurrentLostConnectionSessionLocked(
            int processId, int sessionGeneration, long nativeSessionId)
        {
            return processId > 0 && sessionGeneration > 0 &&
                processId == Interlocked.CompareExchange(ref activeCorePid, 0, 0) &&
                sessionGeneration == Interlocked.CompareExchange(
                    ref mirrorSessionGeneration, 0, 0) &&
                nativeSessionId == Interlocked.Read(ref nativeMirrorSessionId);
        }

        private void DismissLostConnectionPlaceholderForSession(
            int processId, int sessionGeneration, long nativeSessionId)
        {
            lock (postSessionMaintenanceSync)
            {
                if (!IsCurrentLostConnectionSessionLocked(
                        processId, sessionGeneration, nativeSessionId))
                    return;
                Interlocked.Exchange(
                    ref lostConnectionPlaceholderDismissedSessionGeneration,
                    sessionGeneration);
                QueueLostConnectionPlaceholderClose();
            }
            // Never take the pipe lock while holding the lifecycle lock. The
            // writer rechecks the PID; the native owner rechecks the stream ID.
            bool requested = nativeSessionId > 0 && TryWriteNativeVideoCommand(
                "close-session session=" + nativeSessionId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                "continuity Close for session " + nativeSessionId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture), processId);
            Log("Lost-connection placeholder dismissed for session " +
                nativeSessionId + "; native close " +
                (requested ? "requested" : "unavailable") +
                ". Delayed loss events for this session stay hidden.");
        }

        private void ObserveNativeSessionClose(int processId, string line)
        {
            long sessionId;
            bool started = false;
            bool completed = TryParseCompletedNativeSessionClose(
                line, processId, out sessionId);
            if (!completed && !TryParseNativeSessionWindowMarker(
                    line, out sessionId, out started))
                return;
            lock (postSessionMaintenanceSync)
            {
                if (processId != Interlocked.CompareExchange(ref activeCorePid, 0, 0))
                    return;
                if (started)
                    Interlocked.Exchange(ref nativeMirrorSessionId, sessionId);
                else if (Interlocked.Read(ref nativeMirrorSessionId) == sessionId)
                {
                    MarkRendererDismissedForCurrentSession();
                    if (completed)
                    {
                        // A deliberate, acknowledged close is not a failed
                        // connection for the loss watchdog to recover. Worker
                        // wake/EOF paths need not emit the legacy stop line.
                        Interlocked.Exchange(ref lostConnectionRecoveryPending, 0);
                        Interlocked.Exchange(ref lostConnectionRecoveryPid, 0);
                        Interlocked.Exchange(ref lostConnectionRecoveryDueTicks, 0);
                        ResetLostConnectionHttpResetAttempt();
                        HandleMirroringEndedMaintenance(processId);
                    }
                }
            }
        }

        private static bool TryParseCompletedNativeSessionClose(
            string line, int processId, out long sessionId)
        {
            sessionId = 0;
            if (string.IsNullOrEmpty(line)) return false;
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"\AAEROMIRROR_SESSION_CLOSE session=([1-9][0-9]{0,18}) request=[1-9][0-9]{0,19} result=closed pid=([1-9][0-9]{0,9}) raop_port=[0-9]{1,5} airplay_port=[0-9]{1,5}\z",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            int markerPid;
            return match.Success && int.TryParse(match.Groups[2].Value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out markerPid) &&
                markerPid == processId && long.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out sessionId);
        }

        private static bool TryParseNativeSessionWindowMarker(
            string line, out long sessionId, out bool started)
        {
            sessionId = 0;
            started = false;
            if (string.IsNullOrEmpty(line)) return false;
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"\AAEROMIRROR_MIRROR_SESSION session=([1-9][0-9]{0,18}) state=started\z",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (match.Success)
                started = true;
            else
                match = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"\AAEROMIRROR_VIDEO_WINDOW state=closed source=(?:caption-close|continuity-close) session=([1-9][0-9]{0,18}) request=[1-9][0-9]{0,18}\z",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return match.Success && long.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out sessionId);
        }

        private void MarkRendererDismissedForCurrentSession()
        {
            int sessionGeneration = Interlocked.CompareExchange(
                ref mirrorSessionGeneration, 0, 0);
            if (sessionGeneration <= 0)
                return;
            Interlocked.Exchange(
                ref lostConnectionRendererDismissedSessionGeneration,
                sessionGeneration);
            QueueLostConnectionPlaceholderClose();
            Log("The renderer was dismissed with a Close action; " +
                "loss UI is suppressed for the current mirroring session.");
        }

        private void ClearRendererDismissedForCurrentSession()
        {
            int sessionGeneration = Interlocked.CompareExchange(
                ref mirrorSessionGeneration, 0, 0);
            Interlocked.CompareExchange(
                ref lostConnectionRendererDismissedSessionGeneration,
                -1,
                sessionGeneration);
        }

        private static LostConnectionPlaceholderAction
            DecideLostConnectionPlaceholderAction(
                bool showRequested, bool closeRequested, bool visible)
        {
            if (closeRequested)
                return LostConnectionPlaceholderAction.Close;
            if (showRequested && !visible)
                return LostConnectionPlaceholderAction.Show;
            return LostConnectionPlaceholderAction.None;
        }

        private static LostConnectionPresentationState
            DecideLostConnectionPresentationState(
                bool lostRequested, bool reconnectHintRequested,
                bool recoveredRequested)
        {
            if (recoveredRequested)
                return LostConnectionPresentationState.Recovered;
            if (reconnectHintRequested)
                return LostConnectionPresentationState.ReconnectHint;
            if (lostRequested)
                return LostConnectionPresentationState.Lost;
            return LostConnectionPresentationState.None;
        }

        private void ApplyLostConnectionPresentation(
            LostConnectionForm placeholder,
            LostConnectionPresentationState presentationState)
        {
            if (placeholder == null || placeholder.IsDisposed)
                return;
            if (presentationState ==
                LostConnectionPresentationState.Recovered)
            {
                placeholder.ShowConnectionRecovered();
            }
            else if (presentationState ==
                LostConnectionPresentationState.ReconnectHint)
            {
                placeholder.ShowReconnectHint(settings.ReceiverName);
            }
            else if (presentationState ==
                LostConnectionPresentationState.Lost)
            {
                placeholder.ShowConnectionLost();
            }
        }

        private static Rectangle ResolveLostConnectionPlaceholderBounds(
            Rectangle rememberedBounds)
        {
            if (rememberedBounds.Width > 0 && rememberedBounds.Height > 0)
            {
                Rectangle rememberedWorkArea = Screen.FromRectangle(
                    rememberedBounds).WorkingArea;
                return ClampLostConnectionPlaceholderBounds(
                    rememberedBounds, rememberedWorkArea);
            }

            Rectangle workArea = Screen.PrimaryScreen == null
                ? SystemInformation.WorkingArea
                : Screen.PrimaryScreen.WorkingArea;
            int height = Math.Min(760, Math.Max(360,
                (int)Math.Round(workArea.Height * 0.78)));
            int width = Math.Min(520, Math.Max(320,
                (int)Math.Round(
                    height *
                        RendererPresentationPolicy.ModernIPhonePortraitAspect) +
                    32));
            Rectangle fallback = new Rectangle(
                workArea.Left + (workArea.Width - width) / 2,
                workArea.Top + (workArea.Height - height) / 2,
                width,
                height);
            return ClampLostConnectionPlaceholderBounds(
                fallback, workArea);
        }

        private static Rectangle ClampLostConnectionPlaceholderBounds(
            Rectangle desiredBounds, Rectangle workArea)
        {
            if (workArea.Width <= 0 || workArea.Height <= 0)
                return desiredBounds;

            int width = Math.Min(
                Math.Max(1, desiredBounds.Width), workArea.Width);
            int height = Math.Min(
                Math.Max(1, desiredBounds.Height), workArea.Height);
            int x = Math.Max(
                workArea.Left,
                Math.Min(desiredBounds.Left, workArea.Right - width));
            int y = Math.Max(
                workArea.Top,
                Math.Min(desiredBounds.Top, workArea.Bottom - height));
            return new Rectangle(x, y, width, height);
        }

        private static Bitmap TryCaptureRendererSnapshot(
            IntPtr rendererWindow, Rectangle bounds)
        {
            if (rendererWindow == IntPtr.Zero ||
                !NativeMethods.IsWindow(rendererWindow) ||
                bounds.Width <= 0 || bounds.Height <= 0 ||
                bounds.Width > 8192 || bounds.Height > 8192)
                return null;

            Bitmap snapshot = null;
            try
            {
                Rectangle captureBounds;
                if (!TryGetRendererClientScreenBounds(
                        rendererWindow, out captureBounds))
                    return null;
                Rectangle virtualScreen = SystemInformation.VirtualScreen;
                bool fullyOnScreen = Rectangle.Intersect(
                    virtualScreen, captureBounds) == captureBounds;
                if (NativeMethods.IsWindowVisible(rendererWindow) &&
                    IsRendererWindowUnoccluded(
                        rendererWindow, captureBounds) &&
                    fullyOnScreen)
                {
                    snapshot = new Bitmap(
                        captureBounds.Width, captureBounds.Height);
                    using (Graphics graphics = Graphics.FromImage(snapshot))
                    {
                        graphics.CopyFromScreen(
                            captureBounds.Left, captureBounds.Top,
                            0, 0, captureBounds.Size,
                            CopyPixelOperation.SourceCopy);
                    }
                    Log("Renderer snapshot is available from the desktop " +
                        "compositor for the lost-connection placeholder.");
                    return snapshot;
                }

                Log("Renderer snapshot is unavailable or the renderer is " +
                    "covered by another window; the lost-connection placeholder " +
                    "will use a dark fallback.");
                return null;
            }
            catch
            {
                if (snapshot != null)
                    snapshot.Dispose();
                Log("Renderer snapshot is unavailable; the lost-connection " +
                    "placeholder will use a dark fallback.");
                return null;
            }
        }

        private static bool TryGetRendererClientScreenBounds(
            IntPtr rendererWindow, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            NativeMethods.RECT client;
            var origin = new NativeMethods.POINT();
            if (!NativeMethods.GetClientRect(rendererWindow, out client) ||
                !NativeMethods.ClientToScreen(rendererWindow, ref origin))
                return false;
            int width = client.Right - client.Left;
            int height = client.Bottom - client.Top;
            if (width <= 0 || height <= 0 ||
                width > 8192 || height > 8192)
                return false;
            bounds = new Rectangle(origin.X, origin.Y, width, height);
            return true;
        }

        private static bool IsRendererWindowUnoccluded(
            IntPtr rendererWindow, Rectangle rendererBounds)
        {
            IntPtr window = NativeMethods.GetWindow(
                rendererWindow, NativeMethods.GW_HWNDPREV);
            int inspected = 0;
            while (window != IntPtr.Zero && inspected++ < 512)
            {
                if (NativeMethods.IsWindowVisible(window) &&
                    !NativeMethods.IsIconic(window))
                {
                    NativeMethods.RECT nativeBounds;
                    if (NativeMethods.GetWindowRect(window, out nativeBounds))
                    {
                        var bounds = Rectangle.FromLTRB(
                            nativeBounds.Left, nativeBounds.Top,
                            nativeBounds.Right, nativeBounds.Bottom);
                        Rectangle overlap = Rectangle.Intersect(
                            rendererBounds, bounds);
                        if (overlap.Width > 0 && overlap.Height > 0)
                            return false;
                    }
                }
                window = NativeMethods.GetWindow(
                    window, NativeMethods.GW_HWNDPREV);
            }
            return window == IntPtr.Zero;
        }

        private void CompleteLostConnectionRendererHandoff()
        {
            if (Interlocked.Exchange(
                    ref lostConnectionRendererHandoffPending, 0) != 1)
                return;

            bool feedbackHandoff = Interlocked.CompareExchange(
                ref lostConnectionFeedbackHandoffPending, 0, 0) == 1;
            long continuityToken = feedbackHandoff
                ? Interlocked.Read(ref lostConnectionFeedbackHandoffToken)
                : Interlocked.Read(ref lostConnectionContinuityToken);
            int feedbackPid = Interlocked.CompareExchange(
                ref lostConnectionFeedbackHandoffPid, 0, 0);
            int feedbackSessionGeneration = Interlocked.CompareExchange(
                ref lostConnectionFeedbackHandoffSessionGeneration, 0, 0);
            int feedbackEpoch = Interlocked.CompareExchange(
                ref lostConnectionFeedbackHandoffEpoch, 0, 0);

            if (feedbackHandoff && !IsFeedbackRendererHandoffCurrent(
                    continuityToken, feedbackPid,
                    feedbackSessionGeneration, feedbackEpoch))
                return;

            bool placeholderVisible = lostConnectionForm != null &&
                !lostConnectionForm.IsDisposed;
            bool placeholderQueued = Interlocked.CompareExchange(
                ref lostConnectionPlaceholderShowPending, 0, 0) == 1;
            if (!IsMirrorSessionActive)
            {
                Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
                Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 0);
                return;
            }
            if (!placeholderVisible && !placeholderQueued)
            {
                Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
                Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 0);
                if (feedbackHandoff)
                {
                    TryCompleteFeedbackRendererHandoff(
                        continuityToken, feedbackPid,
                        feedbackSessionGeneration, feedbackEpoch);
                }
                return;
            }

            if (!placeholderVisible)
            {
                if (feedbackHandoff && !TryCompleteFeedbackRendererHandoff(
                        continuityToken, feedbackPid,
                        feedbackSessionGeneration, feedbackEpoch))
                    return;
                Log("Mirroring renderer is visible and positioned; canceled " +
                    "the queued lost-connection placeholder before display.");
                QueueLostConnectionPlaceholderClose();
                HandleLostConnectionPlaceholder();
                return;
            }

            LostConnectionForm placeholder = lostConnectionForm;
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderShowPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            IntPtr rendererWindow;
            TryGetRendererWindow(out rendererWindow);
            /* A fresh proof can arrive before an older fade observes its
             * invalidation on the next 20 ms tick. Cancel it synchronously on
             * the UI thread so the one-shot fresh proof is not consumed. */
            placeholder.CancelRendererHandoff();
            if (!placeholder.BeginRendererHandoff(
                rendererWindow,
                delegate
                {
                    if (!ReferenceEquals(lostConnectionForm, placeholder))
                        return;
                    bool current = feedbackHandoff
                        ? TryCompleteFeedbackRendererHandoff(
                            continuityToken, feedbackPid,
                            feedbackSessionGeneration, feedbackEpoch)
                        : Interlocked.Read(
                            ref lostConnectionContinuityToken) ==
                            continuityToken;
                    if (!current)
                    {
                        placeholder.Opacity = 1.0;
                        HandleLostConnectionPlaceholder();
                        return;
                    }
                    Log("Renderer handoff fade completed; closing the " +
                        "lost-connection placeholder.");
                    CloseLostConnectionPlaceholder();
                },
                delegate
                {
                    return Interlocked.CompareExchange(
                            ref lostConnectionPlaceholderShowPending, 0, 0) ==
                            1 ||
                        Interlocked.Read(ref lostConnectionContinuityToken) !=
                            continuityToken ||
                        (feedbackHandoff &&
                            !IsFeedbackRendererHandoffCurrent(
                                continuityToken, feedbackPid,
                                feedbackSessionGeneration, feedbackEpoch));
                }))
            {
                bool stillCurrent = feedbackHandoff
                    ? IsFeedbackRendererHandoffCurrent(
                        continuityToken, feedbackPid,
                        feedbackSessionGeneration, feedbackEpoch)
                    : Interlocked.Read(
                        ref lostConnectionContinuityToken) == continuityToken;
                if (stillCurrent)
                {
                    Interlocked.CompareExchange(
                        ref lostConnectionRendererHandoffPending, 1, 0);
                }
                return;
            }

            Log("Mirroring renderer is visible and positioned; beginning " +
                "the lost-connection handoff fade.");
        }

        private bool IsFeedbackRendererHandoffCurrent(
            long continuityToken, int processId,
            int sessionGeneration, int epoch)
        {
            lock (postSessionMaintenanceSync)
            {
                return Interlocked.Read(ref lostConnectionContinuityToken) ==
                        continuityToken &&
                    Interlocked.CompareExchange(
                        ref lostConnectionFeedbackHandoffPending, 0, 0) == 1 &&
                    Interlocked.Read(ref lostConnectionFeedbackHandoffToken) ==
                        continuityToken &&
                    Interlocked.CompareExchange(
                        ref lostConnectionFeedbackHandoffPid, 0, 0) ==
                        processId &&
                    Interlocked.CompareExchange(
                        ref lostConnectionFeedbackHandoffSessionGeneration,
                        0, 0) == sessionGeneration &&
                    Interlocked.CompareExchange(
                        ref lostConnectionFeedbackHandoffEpoch, 0, 0) == epoch &&
                    Interlocked.CompareExchange(
                        ref activeCorePid, 0, 0) == processId &&
                    IsMirrorSessionActive &&
                    Interlocked.CompareExchange(
                        ref mirrorSessionGeneration, 0, 0) ==
                        sessionGeneration &&
                    Interlocked.CompareExchange(
                        ref feedbackGapPlaceholderActive, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref feedbackGapEpisodeActive, 0, 0) == 0 &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryPending, 0, 0) == 1 &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryPid, 0, 0) == processId &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoverySessionGeneration, 0, 0) ==
                        sessionGeneration &&
                    Interlocked.CompareExchange(
                        ref feedbackVideoRecoveryEpoch, 0, 0) == epoch &&
                    Interlocked.CompareExchange(
                        ref lostConnectionRecoveryPending, 0, 0) == 0;
            }
        }

        private bool TryCompleteFeedbackRendererHandoff(
            long continuityToken, int processId,
            int sessionGeneration, int epoch)
        {
            lock (postSessionMaintenanceSync)
            {
                if (!IsFeedbackRendererHandoffCurrent(
                        continuityToken, processId,
                        sessionGeneration, epoch))
                    return false;

                Interlocked.Exchange(
                    ref lostConnectionFeedbackHandoffPending, 0);
                Interlocked.Exchange(
                    ref feedbackGapPlaceholderActive, 0);
                ResetFeedbackVideoRecoveryWaitLocked();
                Interlocked.Increment(
                    ref feedbackVideoRecoveryCompletedCount);
                return true;
            }
        }

        private void CloseLostConnectionPlaceholder()
        {
            Interlocked.Exchange(ref lostConnectionRendererHandoffPending, 0);
            Interlocked.Exchange(ref lostConnectionLostStatePending, 0);
            Interlocked.Exchange(ref lostConnectionReconnectHintPending, 0);
            Interlocked.Exchange(ref lostConnectionRecoveredStatePending, 0);
            Interlocked.Exchange(ref feedbackGapPlaceholderDueTicks, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderShowPending, 0);
            Interlocked.Exchange(ref lostConnectionPlaceholderClosePending, 0);
            LostConnectionForm placeholder = lostConnectionForm;
            if (placeholder == null)
                return;
            try
            {
                if (!placeholder.IsDisposed)
                    placeholder.CloseProgrammatically();
            }
            catch { }
            finally
            {
                if (ReferenceEquals(lostConnectionForm, placeholder))
                    lostConnectionForm = null;
                placeholder.Dispose();
            }
        }

        private void ApplyLostConnectionPlaceholderPolicy()
        {
            LostConnectionForm placeholder = lostConnectionForm;
            if (placeholder == null || placeholder.IsDisposed)
                return;
            if (placeholder.TopMost != settings.AlwaysOnTop)
                placeholder.TopMost = settings.AlwaysOnTop;
            if (placeholder.ShowInTaskbar != settings.ShowStreamInTaskbar)
                placeholder.ShowInTaskbar = settings.ShowStreamInTaskbar;
        }
    }
}

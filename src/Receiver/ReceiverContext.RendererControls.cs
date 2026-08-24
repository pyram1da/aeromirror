using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private NativeMethods.LowLevelKeyboardProc rendererKeyboardHookProc;
        private IntPtr rendererKeyboardHook = IntPtr.Zero;
        private IntPtr rendererKeyboardHookWindow = IntPtr.Zero;
        private int rendererKeyboardHookPid;
        // 0 = idle, 1 = pressed/pending, 2 = sent while held,
        // 3 = released/pending. Keeping state 3 preserves a short press until
        // the UI timer sends it; releasing state 2 arms the next Escape press.
        private int rendererEscapeRequestState;
        private IntPtr rendererStaleBorderlessWindow = IntPtr.Zero;
        private RendererFullscreenButtonForm rendererFullscreenButton;

        private void UpdateRendererControls(
            IntPtr window, bool fullscreen, bool staleBorderless)
        {
            UpdateRendererKeyboardHook(window, fullscreen);
            HandlePendingRendererEscape(window, fullscreen);

            RendererFullscreenButtonForm button = EnsureRendererButton();
            if (window == IntPtr.Zero || !NativeMethods.IsWindow(window))
            {
                button.HideSafely();
                return;
            }

            bool visible = NativeMethods.IsWindowVisible(window);
            bool minimized = NativeMethods.IsIconic(window);
            bool foreground = IsRendererOrOverlayForeground(window, button);
            if (!RendererFullscreenButtonForm.ShouldShow(
                    visible, minimized, foreground))
            {
                button.HideSafely();
                return;
            }

            NativeMethods.RECT nativeBounds;
            if (!NativeMethods.GetWindowRect(window, out nativeBounds))
            {
                button.HideSafely();
                return;
            }
            Rectangle rendererBounds = Rectangle.FromLTRB(
                nativeBounds.Left, nativeBounds.Top,
                nativeBounds.Right, nativeBounds.Bottom);
            int dpi = NativeMethods.GetWindowDpi(window);
            bool exitMode = fullscreen || staleBorderless;
            Rectangle bounds = RendererFullscreenButtonForm.CalculateBounds(
                rendererBounds, exitMode, dpi,
                NativeMethods.GetCaptionButtonWidth(dpi));
            button.SetFullscreenState(exitMode);
            // Fullscreen renderers may occupy the topmost band even when the
            // normal-window preference is off. The foreground guard above
            // prevents this exit affordance from covering unrelated apps.
            button.ShowAt(
                bounds, settings.AlwaysOnTop || exitMode, window);
        }

        private RendererFullscreenButtonForm EnsureRendererButton()
        {
            if (rendererFullscreenButton == null ||
                rendererFullscreenButton.IsDisposed)
            {
                rendererFullscreenButton =
                    new RendererFullscreenButtonForm(delegate
                    {
                        ToggleStreamWindowFullscreen(true);
                    });
            }
            return rendererFullscreenButton;
        }

        private bool IsRendererOrOverlayForeground(
            IntPtr rendererWindow, RendererFullscreenButtonForm button)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;
            if (button != null && button.ContainsTopLevelWindow(foreground))
                return true;

            uint foregroundPid;
            NativeMethods.GetWindowThreadProcessId(
                foreground, out foregroundPid);
            IntPtr foregroundRoot = NativeMethods.GetAncestor(
                foreground, NativeMethods.GA_ROOT);
            int rendererPid = Interlocked.CompareExchange(
                ref activeCorePid, 0, 0);
            return ShouldCaptureRendererEscape(
                true, rendererPid, foregroundPid,
                foregroundRoot == rendererWindow);
        }

        private void UpdateRendererKeyboardHook(
            IntPtr window, bool fullscreen)
        {
            if (!fullscreen || window == IntPtr.Zero)
            {
                UninstallRendererKeyboardHook();
                return;
            }
            int processId = Interlocked.CompareExchange(
                ref activeCorePid, 0, 0);
            if (processId <= 0)
            {
                UninstallRendererKeyboardHook();
                return;
            }
            if (rendererKeyboardHook != IntPtr.Zero &&
                rendererKeyboardHookWindow == window &&
                rendererKeyboardHookPid == processId)
                return;

            UninstallRendererKeyboardHook();
            if (rendererKeyboardHookProc == null)
                rendererKeyboardHookProc = OnRendererLowLevelKeyboard;
            rendererKeyboardHookWindow = window;
            rendererKeyboardHookPid = processId;
            rendererKeyboardHook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                rendererKeyboardHookProc,
                NativeMethods.GetCurrentModuleHandle(), 0);
            if (rendererKeyboardHook == IntPtr.Zero)
            {
                rendererKeyboardHookWindow = IntPtr.Zero;
                rendererKeyboardHookPid = 0;
                Log("Renderer Esc keyboard hook was unavailable; " +
                    "the on-window exit button remains available.");
            }
            else
            {
                Log("Renderer Esc keyboard hook enabled for actual fullscreen.");
            }
        }

        private IntPtr OnRendererLowLevelKeyboard(
            int code, IntPtr message, IntPtr data)
        {
            try
            {
                bool keyDown = message == NativeMethods.WM_KEYDOWN;
                bool keyUp = message == NativeMethods.WM_KEYUP;
                if (code >= 0 && (keyDown || keyUp) &&
                    data != IntPtr.Zero &&
                    unchecked((uint)Marshal.ReadInt32(data)) ==
                        NativeMethods.VK_ESCAPE &&
                    rendererKeyboardHook != IntPtr.Zero)
                {
                    if (keyUp)
                    {
                        ApplyRendererEscapeKeyEvent(false);
                        return NativeMethods.CallNextHookEx(
                            rendererKeyboardHook, code, message, data);
                    }
                    IntPtr rendererWindow = rendererKeyboardHookWindow;
                    int rendererPid = rendererKeyboardHookPid;
                    IntPtr foreground = NativeMethods.GetForegroundWindow();
                    uint foregroundPid;
                    NativeMethods.GetWindowThreadProcessId(
                        foreground, out foregroundPid);
                    bool sameRoot = foreground != IntPtr.Zero &&
                        NativeMethods.GetAncestor(
                            foreground, NativeMethods.GA_ROOT) ==
                                rendererWindow;
                    if (ShouldCaptureRendererEscape(
                            rendererFullscreenActive, rendererPid,
                            foregroundPid, sameRoot))
                    {
                        ApplyRendererEscapeKeyEvent(true);
                    }
                }
            }
            catch
            {
                // A low-level hook must return promptly and never break the
                // system keyboard chain because renderer state changed.
            }
            return NativeMethods.CallNextHookEx(
                rendererKeyboardHook, code, message, data);
        }

        private void HandlePendingRendererEscape(
            IntPtr window, bool fullscreen)
        {
            int requestState = Interlocked.CompareExchange(
                ref rendererEscapeRequestState, 0, 0);
            if (requestState != 1 && requestState != 3)
                return;
            if (!fullscreen || window != rendererKeyboardHookWindow)
            {
                Interlocked.Exchange(ref rendererEscapeRequestState, 0);
                return;
            }
            if (TryWriteNativeVideoCommand(
                    "video-fullscreen-toggle", "fullscreen escape"))
            {
                CompleteRendererEscapeRequest(true);
                Log("Requested renderer fullscreen exit from Esc.");
            }
            else
            {
                CompleteRendererEscapeRequest(false);
            }
        }

        private void ApplyRendererEscapeKeyEvent(bool keyDown)
        {
            while (true)
            {
                int current = Interlocked.CompareExchange(
                    ref rendererEscapeRequestState, 0, 0);
                int next = ResolveRendererEscapeKeyState(current, keyDown);
                if (next == current || Interlocked.CompareExchange(
                        ref rendererEscapeRequestState, next, current) ==
                            current)
                    return;
            }
        }

        private void CompleteRendererEscapeRequest(bool accepted)
        {
            if (!accepted)
            {
                Interlocked.Exchange(ref rendererEscapeRequestState, 0);
                return;
            }
            while (true)
            {
                int current = Interlocked.CompareExchange(
                    ref rendererEscapeRequestState, 0, 0);
                int next = current == 1 ? 2 :
                    current == 3 ? 0 : current;
                if (next == current || Interlocked.CompareExchange(
                        ref rendererEscapeRequestState, next, current) ==
                            current)
                    return;
            }
        }

        private void UninstallRendererKeyboardHook()
        {
            IntPtr hook = rendererKeyboardHook;
            rendererKeyboardHook = IntPtr.Zero;
            rendererKeyboardHookWindow = IntPtr.Zero;
            rendererKeyboardHookPid = 0;
            Interlocked.Exchange(ref rendererEscapeRequestState, 0);
            if (hook != IntPtr.Zero)
            {
                try { NativeMethods.UnhookWindowsHookEx(hook); }
                catch { }
                Log("Renderer Esc keyboard hook disabled.");
            }
        }

        private void ResetRendererControls(bool disposeButton)
        {
            UninstallRendererKeyboardHook();
            rendererStaleBorderlessWindow = IntPtr.Zero;
            RendererFullscreenButtonForm button = rendererFullscreenButton;
            if (button == null)
                return;
            button.HideSafely();
            if (disposeButton)
            {
                button.Dispose();
                rendererFullscreenButton = null;
            }
        }

        private bool HandleStaleBorderlessRenderer(
            IntPtr window, bool wasFullscreen, bool fullscreen)
        {
            if (rendererStaleBorderlessWindow != IntPtr.Zero &&
                rendererStaleBorderlessWindow != window)
            {
                rendererStaleBorderlessWindow = IntPtr.Zero;
            }
            bool borderless = IsRendererBorderlessWindow(window);
            if (IsStaleBorderlessTransition(
                    wasFullscreen, fullscreen, borderless))
            {
                rendererStaleBorderlessWindow = window;
                Log("Renderer left monitor-sized fullscreen without restoring " +
                    "its frame; keeping the visible exit control available.");
            }
            if (!borderless || fullscreen)
                rendererStaleBorderlessWindow = IntPtr.Zero;
            // A toggle is not idempotent: sending one automatically during an
            // ordinary asynchronous Alt+Enter exit could re-enter fullscreen.
            // Keep a visible user-owned recovery action instead.
            return borderless && !fullscreen &&
                rendererStaleBorderlessWindow == window;
        }

        private static bool IsRendererBorderlessWindow(IntPtr window)
        {
            NativeMethods.RECT outer;
            NativeMethods.RECT client;
            if (window == IntPtr.Zero ||
                !NativeMethods.GetWindowRect(window, out outer) ||
                !NativeMethods.GetClientRect(window, out client))
                return false;
            int outerWidth = outer.Right - outer.Left;
            int outerHeight = outer.Bottom - outer.Top;
            int clientWidth = client.Right - client.Left;
            int clientHeight = client.Bottom - client.Top;
            const int tolerance = 8;
            return outerWidth > 0 && outerHeight > 0 &&
                Math.Abs(outerWidth - clientWidth) <= tolerance &&
                Math.Abs(outerHeight - clientHeight) <= tolerance;
        }

        internal static bool ShouldCaptureRendererEscape(
            bool fullscreen, int rendererPid, uint foregroundPid,
            bool foregroundRootMatchesRenderer)
        {
            return fullscreen && rendererPid > 0 &&
                foregroundPid == (uint)rendererPid &&
                foregroundRootMatchesRenderer;
        }

        internal static int ResolveRendererEscapeKeyState(
            int current, bool keyDown)
        {
            if (keyDown)
                return current == 0 ? 1 : current;
            if (current == 1)
                return 3;
            if (current == 2)
                return 0;
            return current;
        }

        internal static bool IsStaleBorderlessTransition(
            bool wasFullscreen, bool fullscreen, bool borderless)
        {
            return wasFullscreen && !fullscreen && borderless;
        }
    }
}

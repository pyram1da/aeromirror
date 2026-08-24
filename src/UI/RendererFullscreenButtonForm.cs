using System;
using System.Drawing;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed class RendererFullscreenButtonForm : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private readonly Button actionButton;
        private readonly ToolTip toolTip;
        private bool overlayTopMost;

        internal RendererFullscreenButtonForm(Action clicked)
        {
            if (clicked == null)
                throw new ArgumentNullException("clicked");

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(28, 28, 30);
            Padding = new Padding(1);

            actionButton = new Button();
            actionButton.Dock = DockStyle.Fill;
            actionButton.FlatStyle = FlatStyle.Flat;
            actionButton.FlatAppearance.BorderSize = 0;
            actionButton.BackColor = Color.FromArgb(45, 45, 48);
            actionButton.ForeColor = Color.White;
            actionButton.Font = new Font("Segoe UI Symbol", 11F,
                FontStyle.Regular, GraphicsUnit.Point);
            actionButton.TabStop = false;
            actionButton.Cursor = Cursors.Hand;
            actionButton.Click += delegate { clicked(); };
            Controls.Add(actionButton);

            toolTip = new ToolTip();
            SetFullscreenState(false);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        internal void SetFullscreenState(bool fullscreen)
        {
            string description = fullscreen
                ? "Выйти из полноэкранного режима (Esc)"
                : "Полный экран (Alt+Enter)";
            actionButton.Text = fullscreen ? "⤡" : "⛶";
            actionButton.AccessibleName = description;
            actionButton.AccessibleDescription = description;
            toolTip.SetToolTip(actionButton, description);
        }

        internal void ShowAt(
            Rectangle bounds, bool topMost, IntPtr rendererWindow)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                HideSafely();
                return;
            }

            if (!IsHandleCreated)
                CreateControl();
            if (overlayTopMost != topMost)
            {
                NativeMethods.SetWindowPos(
                    Handle,
                    topMost
                        ? NativeMethods.HWND_TOPMOST
                        : NativeMethods.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOACTIVATE);
                overlayTopMost = topMost;
            }

            NativeMethods.SetWindowPos(
                Handle,
                IntPtr.Zero,
                bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
            if (!Visible)
                Show();
            EnsureAboveRendererWithoutActivation(rendererWindow, topMost);
        }

        private void EnsureAboveRendererWithoutActivation(
            IntPtr rendererWindow, bool topMost)
        {
            if (rendererWindow != IntPtr.Zero &&
                NativeMethods.IsWindow(rendererWindow) &&
                NativeMethods.GetWindow(
                    rendererWindow, NativeMethods.GW_HWNDPREV) == Handle)
                return;

            NativeMethods.SetWindowPos(
                Handle,
                topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_TOP,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE);
        }

        internal void HideSafely()
        {
            if (Visible)
                Hide();
        }

        internal bool ContainsTopLevelWindow(IntPtr window)
        {
            return IsHandleCreated && window != IntPtr.Zero &&
                NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT) ==
                    Handle;
        }

        internal static Rectangle CalculateBounds(
            Rectangle rendererBounds, bool fullscreen, int dpi,
            int captionButtonWidth)
        {
            if (rendererBounds.Width <= 0 || rendererBounds.Height <= 0)
                return Rectangle.Empty;
            dpi = Math.Max(48, Math.Min(768, dpi));
            int width = Scale(36, dpi);
            int height = Scale(28, dpi);
            int inset = Scale(fullscreen ? 8 : 4, dpi);
            int x;
            if (fullscreen)
            {
                x = rendererBounds.Right - width - inset;
            }
            else
            {
                int standardWidth = Math.Max(
                    Scale(32, dpi), captionButtonWidth);
                x = rendererBounds.Right - standardWidth * 3 - width - inset;
                if (x < rendererBounds.Left + inset)
                    return Rectangle.Empty;
            }
            x = Math.Max(rendererBounds.Left + inset, x);
            int y = rendererBounds.Top + inset;
            return new Rectangle(x, y, width, height);
        }

        internal static bool ShouldShow(
            bool rendererVisible, bool rendererMinimized,
            bool rendererOrOverlayForeground)
        {
            return rendererVisible && !rendererMinimized &&
                rendererOrOverlayForeground;
        }

        private static int Scale(int value, int dpi)
        {
            return Math.Max(1, (int)Math.Round(value * dpi / 96.0));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

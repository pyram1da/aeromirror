using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    internal sealed class PairingPinOverlayForm : Form
    {
        private const int LifetimeSeconds = 60;
        private readonly Label titleLabel;
        private readonly Label pinLabel;
        private readonly Label detailLabel;
        private readonly Timer lifetimeTimer;
        private readonly Action<bool> cancellationRequested;
        private bool dismissedInternally;

        internal PairingPinOverlayForm(
            string pin, Action<bool> cancellationRequested)
        {
            if (!IsFourDigitAsciiPin(pin))
                throw new ArgumentException(
                    "A four-digit pairing PIN is required.", "pin");

            this.cancellationRequested = cancellationRequested;
            Text = "Подключение iPhone · AeroMirror";
            StartPosition = FormStartPosition.Manual;
            Bounds = ResolveActiveScreen().Bounds;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(8, 10, 14);
            ForeColor = Color.White;
            KeyPreview = true;
            DoubleBuffered = true;

            titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font(
                "Segoe UI Semibold", 27F, FontStyle.Regular,
                GraphicsUnit.Point);
            titleLabel.Text = "Введите код на iPhone";
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.AccessibleName = "Код подключения iPhone";

            pinLabel = new Label();
            pinLabel.AutoSize = false;
            pinLabel.BackColor = Color.Transparent;
            pinLabel.ForeColor = Color.White;
            pinLabel.Font = new Font(
                "Segoe UI Semibold", 78F, FontStyle.Bold,
                GraphicsUnit.Point);
            pinLabel.Text = FormatPin(pin);
            pinLabel.TextAlign = ContentAlignment.MiddleCenter;
            pinLabel.AccessibleName = "Четырёхзначный код подключения";

            detailLabel = new Label();
            detailLabel.AutoSize = false;
            detailLabel.BackColor = Color.Transparent;
            detailLabel.ForeColor = Color.FromArgb(195, 201, 211);
            detailLabel.Font = new Font(
                "Segoe UI", 15F, FontStyle.Regular, GraphicsUnit.Point);
            detailLabel.Text =
                "Этот код нужен только для первого подключения этого iPhone.\r\n" +
                "Код действует одну минуту · Esc — отменить";
            detailLabel.TextAlign = ContentAlignment.MiddleCenter;

            Controls.Add(titleLabel);
            Controls.Add(pinLabel);
            Controls.Add(detailLabel);
            Resize += delegate { LayoutContent(); };
            LayoutContent();

            lifetimeTimer = new Timer();
            lifetimeTimer.Interval = LifetimeSeconds * 1000;
            lifetimeTimer.Tick += delegate
            {
                lifetimeTimer.Stop();
                RequestCancellation(true);
            };
            lifetimeTimer.Start();
        }

        internal static string GenerateCryptographicPin()
        {
            byte[] bytes = new byte[2];
            using (RandomNumberGenerator generator =
                RandomNumberGenerator.Create())
            {
                while (true)
                {
                    generator.GetBytes(bytes);
                    int value = bytes[0] | (bytes[1] << 8);
                    if (value < 60000)
                        return (value % 10000).ToString("D4");
                }
            }
        }

        internal void Dismiss()
        {
            if (IsDisposed)
                return;
            dismissedInternally = true;
            lifetimeTimer.Stop();
            pinLabel.Text = "";
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetImmersiveDarkMode(Handle, true);
        }

        protected override bool ProcessCmdKey(
            ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                RequestCancellation(false);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!dismissedInternally &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                RequestCancellation(false);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lifetimeTimer.Stop();
                lifetimeTimer.Dispose();
                titleLabel.Font.Dispose();
                pinLabel.Font.Dispose();
                detailLabel.Font.Dispose();
            }
            base.Dispose(disposing);
        }

        private void RequestCancellation(bool timedOut)
        {
            if (dismissedInternally || IsDisposed)
                return;
            dismissedInternally = true;
            lifetimeTimer.Stop();
            pinLabel.Text = "";
            if (cancellationRequested != null)
                cancellationRequested(timedOut);
            Close();
        }

        private void LayoutContent()
        {
            int width = Math.Max(320, ClientSize.Width);
            int height = Math.Max(360, ClientSize.Height);
            int centerY = height / 2;
            int contentWidth = Math.Max(280, width - 64);
            titleLabel.SetBounds(32, centerY - 190, contentWidth, 58);
            pinLabel.SetBounds(32, centerY - 125, contentWidth, 150);
            detailLabel.SetBounds(32, centerY + 48, contentWidth, 78);
        }

        private static Screen ResolveActiveScreen()
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground != IntPtr.Zero)
                    return Screen.FromHandle(foreground);
            }
            catch { }
            return Screen.FromPoint(Cursor.Position);
        }

        private static string FormatPin(string pin)
        {
            return pin[0] + "   " + pin[1] + "   " +
                pin[2] + "   " + pin[3];
        }

        private static bool IsFourDigitAsciiPin(string pin)
        {
            if (pin == null || pin.Length != 4)
                return false;
            foreach (char digit in pin)
            {
                if (digit < '0' || digit > '9')
                    return false;
            }
            return true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}

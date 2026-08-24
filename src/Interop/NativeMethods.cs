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
    internal static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        internal delegate void WinEventProc(
            IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint eventThread, uint eventTime);
        internal delegate IntPtr LowLevelKeyboardProc(
            int code, IntPtr message, IntPtr data);
        internal static readonly IntPtr HWND_TOP = IntPtr.Zero;
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        internal const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        internal const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        internal const uint EVENT_OBJECT_SHOW = 0x8002;
        internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        internal const int OBJID_WINDOW = 0;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const uint GW_HWNDPREV = 3;
        internal const uint GA_ROOT = 2;
        internal const int WH_KEYBOARD_LL = 13;
        internal static readonly IntPtr WM_KEYDOWN = new IntPtr(0x0100);
        internal static readonly IntPtr WM_KEYUP = new IntPtr(0x0101);
        internal const uint VK_ESCAPE = 0x1B;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;
        private const int VK_LBUTTON = 0x01;
        private const int SM_CXSIZE = 30;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JobObjectExtendedLimitInformation = 9;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            internal long PerProcessUserTimeLimit;
            internal long PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSetSize;
            internal UIntPtr MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal IntPtr Affinity;
            internal uint PriorityClass;
            internal uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            internal ulong ReadOperationCount;
            internal ulong WriteOperationCount;
            internal ulong OtherOperationCount;
            internal ulong ReadTransferCount;
            internal ulong WriteTransferCount;
            internal ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            internal JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            internal IO_COUNTERS IoInfo;
            internal UIntPtr ProcessMemoryLimit;
            internal UIntPtr JobMemoryLimit;
            internal UIntPtr PeakProcessMemoryUsed;
            internal UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool IsZoomed(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int hookId, LowLevelKeyboardProc callback,
            IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        internal static IntPtr GetCurrentModuleHandle()
        {
            return GetModuleHandle(null);
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(
            uint eventMinimum, uint eventMaximum, IntPtr eventHookModule,
            WinEventProc eventProc, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr eventHook);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(
            IntPtr securityAttributes, string name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(
            IntPtr job, int informationClass, IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(
            IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool TerminateJobObject(
            IntPtr job, uint exitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        internal static IntPtr CreateKillOnCloseJob(Process process)
        {
            IntPtr job = IntPtr.Zero;
            IntPtr information = IntPtr.Zero;
            try
            {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero)
                    return IntPtr.Zero;
                var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                limits.BasicLimitInformation.LimitFlags =
                    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                int size = Marshal.SizeOf(
                    typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                information = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(limits, information, false);
                if (!SetInformationJobObject(
                        job, JobObjectExtendedLimitInformation,
                        information, (uint)size) ||
                    !AssignProcessToJobObject(job, process.Handle))
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
                return job;
            }
            catch
            {
                if (job != IntPtr.Zero)
                    CloseHandle(job);
                return IntPtr.Zero;
            }
            finally
            {
                if (information != IntPtr.Zero)
                    Marshal.FreeHGlobal(information);
            }
        }

        internal static void CloseHandleSafe(ref IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;
            try { CloseHandle(handle); }
            catch { }
            handle = IntPtr.Zero;
        }

        internal static bool TerminateAndCloseJobSafe(ref IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return false;
            bool terminated = false;
            try { terminated = TerminateJobObject(handle, 1); }
            catch { }
            CloseHandleSafe(ref handle);
            return terminated;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr window, int attribute, ref int value, int valueSize);

        internal static void SetImmersiveDarkMode(
            IntPtr window, bool enabled)
        {
            if (window == IntPtr.Zero)
                return;
            int value = enabled ? 1 : 0;
            try
            {
                int result = DwmSetWindowAttribute(
                    window, 20, ref value, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(
                        window, 19, ref value, sizeof(int));
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool SetWindowText(IntPtr window, string text);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr window, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(
            IntPtr window, int index, IntPtr value);

        internal static void SetToolWindowStyle(IntPtr window, bool hideFromTaskbar)
        {
            long current = IntPtr.Size == 8
                ? GetWindowLongPtr64(window, GWL_EXSTYLE).ToInt64()
                : GetWindowLong32(window, GWL_EXSTYLE);
            long updated = hideFromTaskbar
                ? (current | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW
                : (current | WS_EX_APPWINDOW) & ~WS_EX_TOOLWINDOW;
            if (updated == current)
                return;
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(window, GWL_EXSTYLE, new IntPtr(updated));
            else
                SetWindowLong32(window, GWL_EXSTYLE, (int)updated);
            SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER |
                SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll")]
        internal static extern bool GetClientRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll")]
        internal static extern bool ClientToScreen(
            IntPtr window, ref POINT point);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        internal static int GetWindowDpi(IntPtr window)
        {
            try
            {
                uint dpi = GetDpiForWindow(window);
                if (dpi >= 48 && dpi <= 768)
                    return (int)dpi;
            }
            catch { }
            return 96;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        internal static int GetCaptionButtonWidth(int dpi)
        {
            try
            {
                int width = GetSystemMetricsForDpi(
                    SM_CXSIZE, (uint)Math.Max(48, Math.Min(768, dpi)));
                if (width > 0)
                    return width;
            }
            catch { }
            return Math.Max(1, GetSystemMetrics(SM_CXSIZE));
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        internal static bool IsLeftMouseButtonDown()
        {
            return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}

param([Parameter(Mandatory = $true)][uint32]$CoreProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class RendererWindowSnapshot {
    public delegate bool EnumProc(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle(IntPtr window,int index);
    static void Print(IntPtr window) {
        Rect r,c; GetWindowRect(window,out r); GetClientRect(window,out c);
        var name=new StringBuilder(256); GetClassName(window,name,name.Capacity);
        Console.WriteLine("hwnd={0:X} parent={1:X} class={2} visible={3} style={4:X} ex={5:X} outer={6},{7},{8},{9} client={10}x{11}",
            window.ToInt64(),GetParent(window).ToInt64(),name,IsWindowVisible(window),GetStyle(window,-16).ToInt64(),GetStyle(window,-20).ToInt64(),r.L,r.T,r.R,r.B,c.R,c.B);
    }
    public static void Run(uint targetPid) {
        int count=0;
        bool ok=EnumWindows(delegate(IntPtr window, IntPtr data) {
            uint pid; GetWindowThreadProcessId(window,out pid);
            if(pid==targetPid) {
                ++count; Print(window);
                EnumChildWindows(window,delegate(IntPtr child, IntPtr ignored) {
                    uint childPid; GetWindowThreadProcessId(child,out childPid);
                    if(childPid==targetPid) Print(child);
                    return true;
                },IntPtr.Zero);
            }
            return true;
        },IntPtr.Zero);
        Console.WriteLine("enumeration={0} root_count={1} last_error={2}",ok,count,Marshal.GetLastWin32Error());
    }
}
'@
[RendererWindowSnapshot]::Run($CoreProcessId)

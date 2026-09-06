using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

internal static class RendererShowBoundaryHarness
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Fields).SetValue(target, value);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Assembly assembly = Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
        bool expectBug = args.Length > 1 && args[1] == "--expect-bug";
        Type contextType = assembly.GetType("AirPlayReceiverMvp.ReceiverContext", true);
        Type settingsType = assembly.GetType("AirPlayReceiverMvp.AppSettings", true);
        // Do not construct/start the receiver, read settings, or touch the LAN.
        object context = FormatterServices.GetUninitializedObject(contextType);
        object settings = FormatterServices.GetUninitializedObject(settingsType);
        Set(context, "settings", settings);
        Set(settings, "StreamWindowLeft", 170);
        Set(settings, "StreamWindowTop", 160);
        Set(settings, "StreamWindowWidth", 400);
        Set(settings, "StreamWindowHeight", 500);
        Set(settings, "StreamWindowDpi", 96);
        int pid = Process.GetCurrentProcess().Id;
        IntPtr hook = new IntPtr(1234);
        Set(context, "activeCorePid", pid);
        Set(context, "rendererMoveSizeHookPid", pid);
        Set(context, "rendererWindowShowHook", hook);
        MethodInfo shown = contextType.GetMethod("OnRendererWindowShowEvent", Methods);
        MethodInfo outerBounds = contextType.GetMethod("TryGetRendererOuterBounds", Methods);
        using (Form host = new Form())
        using (Panel surface = new Panel())
        using (Panel sink = new Panel())
        {
            host.Text = "AeroMirror";
            host.Bounds = new Rectangle(30, 40, 600, 500);
            surface.Bounds = new Rectangle(0, 0, 400, 450);
            sink.Text = "Direct3D11 renderer";
            sink.Bounds = new Rectangle(0, 0, 400, 450);
            host.Controls.Add(surface);
            surface.Controls.Add(sink);
            IntPtr hostHandle = host.Handle, surfaceHandle = surface.Handle, sinkHandle = sink.Handle;
            object[] beforeArgs = { sinkHandle, Rectangle.Empty };
            Require((bool)outerBounds.Invoke(null, beforeArgs), "real child bounds are readable");
            shown.Invoke(context, new object[] { hook, (uint)0x8002, sinkHandle, 0, 0, (uint)0, (uint)0 });
            object[] afterArgs = { sinkHandle, Rectangle.Empty };
            Require((bool)outerBounds.Invoke(null, afterArgs), "child survives show notification");
            Rectangle before = (Rectangle)beforeArgs[1], after = (Rectangle)afterArgs[1];
            bool unchanged = before == after;
            Require(expectBug ? !unchanged : unchanged,
                expectBug ? "baseline must reproduce child displacement" : "sink SHOW must not apply desktop coordinates to a child");
            Console.WriteLine("Child before={0}; after={1}; unchanged={2}", before, after, unchanged);
            if (!expectBug)
            {
                MethodInfo setOuter = contextType.GetMethod("SetRendererOuterBounds", Methods);
                Require(!(bool)setOuter.Invoke(null, new object[] { sinkHandle, new Rectangle(200, 200, 500, 600) }),
                    "outer-placement mutation boundary also rejects child HWNDs");
                MethodInfo fit = contextType.GetMethod("FitRendererWindow", Methods);
                Require(!(bool)fit.Invoke(null, new object[] { sinkHandle, new Size(998, 2160), false }),
                    "automatic and manual aspect fitting cannot resize the sink child");
                shown.Invoke(context, new object[] { hook, (uint)0x8002, hostHandle, 0, 0, (uint)0, (uint)0 });
                object[] hostArgs = { hostHandle, Rectangle.Empty };
                outerBounds.Invoke(null, hostArgs);
                Require((Rectangle)hostArgs[1] == new Rectangle(170, 160, 400, 500),
                    "outer host still receives its saved placement");
                Type nativeType = assembly.GetType("AirPlayReceiverMvp.NativeMethods", true);
                MethodInfo isTop = nativeType.GetMethod("IsTopLevelWindow", Methods);
                using (Form owned = new Form())
                {
                    owned.Owner = host;
                    Require((bool)isTop.Invoke(null, new object[] { owned.Handle }),
                        "owned top-level windows are not confused with child windows");
                }
            }
            Require(!host.Visible, "no test window is shown");
        }
        GC.SuppressFinalize(context);
        Console.WriteLine(expectBug ? "0.12.27 child-displacement regression reproduced." : "Renderer show/placement boundary checks passed.");
    }
}

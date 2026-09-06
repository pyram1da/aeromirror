using System;
using System.Threading;
using System.Windows.Forms;
using AirPlayReceiverMvp;

internal sealed class QueueOwner : Control
{
    internal void Recreate() { RecreateHandle(); }
}

internal static class ControlWorkQueueHarness
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void FromWorker(Action action)
    {
        Exception failure = null;
        Thread worker = new Thread(delegate()
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        worker.IsBackground = true;
        worker.Start();
        Require(worker.Join(5000), "worker must not block on UI thread");
        if (failure != null) throw new Exception("worker failed", failure);
    }

    [STAThread]
    private static void Main()
    {
        Require(!NativeMethods.SetToolWindowStyle(IntPtr.Zero, true),
            "invalid HWND cannot acknowledge a taskbar policy");
        IntPtr destroyed;
        using (Form window = new Form())
        {
            destroyed = window.Handle;
            Require(NativeMethods.SetToolWindowStyle(window.Handle, true),
                "hidden taskbar style is acknowledged on a real HWND");
            Require(NativeMethods.SetToolWindowStyle(window.Handle, true),
                "idempotent policy still confirms the frame refresh");
            Require(NativeMethods.SetToolWindowStyle(window.Handle, false),
                "normal taskbar style can be restored");
            Require(!window.Visible, "window policy tests never show the form");
        }
        Require(!NativeMethods.SetToolWindowStyle(destroyed, false),
            "destroyed HWND cannot acknowledge a taskbar policy");
        int delivered = 0, abandoned = 0;
        using (QueueOwner owner = new QueueOwner())
        using (ControlWorkQueue queue = new ControlWorkQueue(owner))
        {
            FromWorker(delegate { queue.Post(delegate { ++delivered; }, delegate { ++abandoned; }); });
            Require(delivered == 0 && abandoned == 1, "uncreated handle cancels exactly once");
            IntPtr handle = owner.Handle;
            int uiThread = Thread.CurrentThread.ManagedThreadId;
            FromWorker(delegate { queue.Post(delegate
            {
                Require(Thread.CurrentThread.ManagedThreadId == uiThread, "delivery stays on owner thread");
                ++delivered;
            }, delegate { ++abandoned; }); });
            Require(delivered == 0, "worker never runs UI action inline");
            Application.DoEvents();
            Require(delivered == 1 && abandoned == 1, "successful result transfers ownership");
            FromWorker(delegate { queue.Post(delegate { ++delivered; }, delegate { ++abandoned; }); });
            owner.Recreate();
            Application.DoEvents();
            Require(delivered == 1 && abandoned == 2, "destroyed handle cancels queued result");
            FromWorker(delegate { queue.Post(delegate { ++delivered; }, delegate { ++abandoned; }); });
            Application.DoEvents();
            Require(delivered == 2 && abandoned == 2, "recreated handle accepts fresh work");
            FromWorker(delegate { queue.Post(delegate { ++delivered; }, delegate { ++abandoned; }); });
            owner.Dispose();
            Application.DoEvents();
            Require(delivered == 2 && abandoned == 3, "closed form cancels queued installer handoff");
            FromWorker(delegate { queue.Post(delegate { ++delivered; }, delegate { ++abandoned; }); });
            Require(delivered == 2 && abandoned == 4, "late download result is cleaned after closure");
        }
        Require(abandoned == 4, "repeated disposal does not clean a result twice");

        for (int i = 0; i < 250; ++i)
        {
            using (QueueOwner owner = new QueueOwner())
            using (ControlWorkQueue queue = new ControlWorkQueue(owner))
            using (ManualResetEvent start = new ManualResetEvent(false))
            {
                IntPtr handle = owner.Handle;
                int ran = 0, cleaned = 0;
                Exception failure = null;
                Thread worker = new Thread(delegate()
                {
                    start.WaitOne();
                    try { queue.Post(delegate { ++ran; }, delegate { Interlocked.Increment(ref cleaned); }); }
                    catch (Exception ex) { failure = ex; }
                });
                worker.IsBackground = true;
                worker.Start();
                start.Set();
                owner.Dispose();
                Require(worker.Join(5000), "disposal race must be bounded");
                Application.DoEvents();
                Require(failure == null && ran == 0 && cleaned == 1,
                    "check/queue/dispose race cancels exactly once without escaping worker");
            }
        }
        Console.WriteLine("UI boundary checks passed: window policy, delivery, handle recreation, late download, and 250 disposal races.");
    }
}

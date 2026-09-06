using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AirPlayReceiverMvp
{
    // Owns worker results until the UI accepts them. Destroying a handle also
    // cancels queued callbacks: BeginInvoke success alone is not a handoff.
    internal sealed class ControlWorkQueue : IDisposable
    {
        private sealed class WorkItem
        {
            internal Action Run;
            internal Action Abandon;
        }

        private readonly Control owner;
        private readonly object sync = new object();
        private readonly HashSet<WorkItem> pending = new HashSet<WorkItem>();
        private bool disposed;

        internal ControlWorkQueue(Control owner)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            this.owner = owner;
            owner.HandleDestroyed += HandleDestroyed;
            owner.Disposed += OwnerDisposed;
        }

        internal void Post(Action action, Action abandoned)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            WorkItem item = new WorkItem { Run = action, Abandon = abandoned };
            bool accepted;
            lock (sync)
            {
                accepted = !disposed;
                if (accepted)
                    pending.Add(item);
            }
            if (!accepted)
            {
                Abandon(item);
                return;
            }
            try
            {
                if (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated)
                {
                    Cancel(item);
                    return;
                }
                owner.BeginInvoke((MethodInvoker)delegate
                {
                    if (owner.IsDisposed || owner.Disposing)
                    {
                        Cancel(item);
                        return;
                    }
                    lock (sync)
                    {
                        if (!pending.Remove(item))
                            return;
                    }
                    // Runs on the owner thread, serialized with form disposal.
                    // Exceptions in the actual UI action are not queue failures.
                    item.Run();
                });
            }
            catch (ObjectDisposedException) { Cancel(item); }
            catch (InvalidOperationException) { Cancel(item); }
        }

        internal void Post(Action action)
        {
            Post(action, null);
        }

        private static void Abandon(WorkItem item)
        {
            if (item.Abandon != null)
                item.Abandon();
        }

        private void Cancel(WorkItem item)
        {
            lock (sync)
            {
                if (!pending.Remove(item))
                    return;
            }
            Abandon(item);
        }

        private void CancelPending(bool close)
        {
            WorkItem[] cancelled;
            lock (sync)
            {
                disposed |= close;
                cancelled = new WorkItem[pending.Count];
                pending.CopyTo(cancelled);
                pending.Clear();
            }
            foreach (WorkItem item in cancelled)
                Abandon(item);
        }

        private void HandleDestroyed(object sender, EventArgs args)
        {
            CancelPending(false);
        }

        private void OwnerDisposed(object sender, EventArgs args)
        {
            Dispose();
        }

        public void Dispose()
        {
            CancelPending(true);
            owner.HandleDestroyed -= HandleDestroyed;
            owner.Disposed -= OwnerDisposed;
        }
    }
}

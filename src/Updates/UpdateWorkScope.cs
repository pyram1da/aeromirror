using System;
using System.Collections.Generic;
using System.Threading;

namespace AirPlayReceiverMvp
{
    // Cancellation belongs to the UI/application owner; disposal belongs to
    // the completed worker. Serialize Cancel and Dispose of each source. Token
    // callbacks may abort I/O, but must never call UI or re-enter this scope.
    internal sealed class UpdateWorkScope : IDisposable
    {
        internal sealed class Operation : IDisposable
        {
            private readonly UpdateWorkScope owner;
            internal readonly CancellationTokenSource Source;
            internal readonly CancellationToken Token;

            internal Operation(UpdateWorkScope owner)
            {
                this.owner = owner;
                Source = new CancellationTokenSource();
                Token = Source.Token;
            }

            public void Dispose()
            {
                owner.Complete(this);
            }
        }

        private readonly object sync = new object();
        private readonly HashSet<Operation> pending = new HashSet<Operation>();
        private bool closed;

        internal Operation Begin()
        {
            lock (sync)
            {
                if (closed)
                    return null;
                var operation = new Operation(this);
                pending.Add(operation);
                return operation;
            }
        }

        internal void CancelPending()
        {
            lock (sync)
                CancelPendingLocked();
        }

        private void CancelPendingLocked()
        {
            foreach (Operation operation in pending)
                operation.Source.Cancel();
        }

        private void Complete(Operation operation)
        {
            lock (sync)
            {
                if (pending.Remove(operation))
                    operation.Source.Dispose();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                closed = true;
                CancelPendingLocked();
            }
        }
    }
}

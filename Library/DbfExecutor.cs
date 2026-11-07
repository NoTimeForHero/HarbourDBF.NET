using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HarbourDBF.NET
{
    // Serializes work on one thread. Capacity counts queued work, not the running item.
    internal sealed class DbfExecutor : IDisposable
    {
        private readonly object gate = new object();
        private readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        private readonly SemaphoreSlim slots;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private readonly Thread thread;
        private readonly TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool disposed;

        internal DbfExecutor(int capacity, Action initialize, Action shutdown)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            slots = new SemaphoreSlim(capacity, capacity);
            thread = new Thread(() => Run(initialize, shutdown)) { IsBackground = true, Name = "Harbour VM" };
            thread.Start();
            started.Task.GetAwaiter().GetResult();
        }

        private void Run(Action initialize, Action shutdown)
        {
            try { initialize?.Invoke(); started.TrySetResult(true); }
            catch (Exception error) { started.TrySetException(error); return; }
            try
            {
                foreach (var work in queue.GetConsumingEnumerable())
                {
                    slots.Release();
                    work();
                }
            }
            finally
            {
                try { shutdown?.Invoke(); stopped.TrySetResult(true); }
                catch (Exception error) { stopped.TrySetException(error); }
            }
        }

        internal T Invoke<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread == thread) return action();
            return InvokeAsync(action, CancellationToken.None).GetAwaiter().GetResult();
        }

        internal async Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread == thread) throw new InvalidOperationException("Cannot queue work from the Harbour executor thread.");
            using (var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, closing.Token))
            {
                try { await slots.WaitAsync(wait.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && closing.IsCancellationRequested)
                { throw new ObjectDisposedException(nameof(DbfExecutor)); }
            }
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                if (disposed) { slots.Release(); throw new ObjectDisposedException(nameof(DbfExecutor)); }
                if (cancellationToken.IsCancellationRequested) { slots.Release(); cancellationToken.ThrowIfCancellationRequested(); }
                queue.Add(() =>
                {
                    try { result.TrySetResult(action()); }
                    catch (Exception error) { result.TrySetException(error); }
                });
            }
            return await result.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Thread.CurrentThread == thread) throw new InvalidOperationException("Cannot stop the Harbour executor from its own thread.");
            lock (gate)
            {
                if (!disposed)
                {
                    disposed = true;
                    closing.Cancel();
                    queue.CompleteAdding();
                }
            }
            thread.Join();
            stopped.Task.GetAwaiter().GetResult();
        }
    }
}

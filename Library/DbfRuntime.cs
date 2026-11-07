using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HarbourDBF.NET
{
    /// <summary>Настройка и доступ к единому исполнителю Harbour VM.</summary>
    public static class DbfRuntime
    {
        private static readonly object Gate = new object();
        private static DbfExecutor executor;
        private static int capacity = 256;
        private static bool stopped;

        [DllImport(Constants.DllName, EntryPoint = "_DBF_VM_INIT", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Init();
        [DllImport(Constants.DllName, EntryPoint = "_DBF_VM_QUIT", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Quit();

        /// <summary>Задаёт число ожидающих операций до первого обращения к VM.</summary>
        public static void Configure(int queueCapacity = 256)
        {
            if (queueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            lock (Gate)
            {
                if (executor != null || stopped) throw new InvalidOperationException("The Harbour runtime is already started or stopped.");
                capacity = queueCapacity;
            }
        }

        private static DbfExecutor Executor
        {
            get
            {
                lock (Gate)
                {
                    if (stopped && (executor == null || !executor.IsExecutorThread)) throw new ObjectDisposedException(nameof(DbfRuntime));
                    if (executor == null)
                    {
                        executor = new DbfExecutor(capacity, Init, Quit);
                        AppDomain.CurrentDomain.ProcessExit += (_, __) => { try { Shutdown(); } catch { /* Process exit cannot surface cleanup failures. */ } };
                    }
                    return executor;
                }
            }
        }

        internal static T Invoke<T>(Func<T> action) => Executor.Invoke(action);
        internal static void Invoke(Action action) => Invoke(() => { action(); return true; });
        internal static bool IsExecutorThread
        {
            get { var current = Volatile.Read(ref executor); return current != null && current.IsExecutorThread; }
        }

        /// <summary>Ожидает место в очереди с отменой; после постановки операция выполняется до завершения.</summary>
        public static Task<T> ExecuteAsync<T>(Func<T> operation, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (typeof(Task).IsAssignableFrom(typeof(T)) || ContainsAsync(operation))
                throw new ArgumentException("The executor operation must be synchronous.", nameof(operation));
            return Executor.InvokeAsync(operation, cancellationToken);
        }

        public static Task ExecuteAsync(Action operation, CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (ContainsAsync(operation))
                throw new ArgumentException("The executor operation must be synchronous.", nameof(operation));
            return Executor.InvokeAsync(() => { operation(); return true; }, cancellationToken);
        }

        internal static bool ContainsAsync(Delegate operation)
        {
            foreach (var item in operation.GetInvocationList())
                if (Attribute.IsDefined(item.Method, typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute))) return true;
            return false;
        }

        /// <summary>Завершает принятые операции и останавливает VM. Повторный запуск в процессе не поддерживается.</summary>
        public static void Shutdown()
        {
            DbfExecutor current;
            lock (Gate)
            {
                if (executor != null && executor.IsExecutorThread) throw new InvalidOperationException("The runtime cannot be stopped from its executor thread.");
                stopped = true;
                current = executor;
            }
            current?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace HarbourDBF.NET
{
    public enum DbfDeletedRecords { Exclude, Include }
    public enum DbfOpenMode { SharedReadOnly, SharedReadWrite, ExclusiveReadOnly, ExclusiveReadWrite }

    /// <summary>Владеет таблицами и допускает работу только на создавшем сеанс потоке.</summary>
    public sealed class DbfSession : IDisposable
    {
        private static readonly object Gate = new object();
        private static Thread owner;
        private static bool callbackActive;
        private readonly Thread thread;
        private readonly List<IDisposable> tables = new List<IDisposable>();
        private bool disposed;
        internal string Root { get; }

        private DbfSession(string root)
        {
            Root = DbfPaths.Root(root);
            thread = Thread.CurrentThread;
            lock (Gate)
            {
                if (owner != null && owner != thread) throw new InvalidOperationException("The Harbour runtime is owned by another thread.");
                owner = thread;
            }
        }

        public static DbfSession Open(string root) => new DbfSession(root);

        internal void Check()
        {
            if (Thread.CurrentThread != thread) throw new InvalidOperationException("The session may only be used on its owning thread.");
            if (disposed) throw new ObjectDisposedException(nameof(DbfSession));
            if (callbackActive) throw new InvalidOperationException("Typed DBF operations cannot be called from an update callback.");
        }

        internal static void EnterCallback() { callbackActive = true; }
        internal static void ExitCallback() { callbackActive = false; }

        public DbfTable<T> OpenTable<T>(string path, DbfMapping<T> mapping, string codepage = "", Encoding encoding = null,
            DbfDeletedRecords deletedRecords = DbfDeletedRecords.Exclude, DbfOpenMode mode = DbfOpenMode.SharedReadOnly) where T : class, new()
        {
            Check();
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            if (deletedRecords != DbfDeletedRecords.Exclude && deletedRecords != DbfDeletedRecords.Include)
                throw new ArgumentOutOfRangeException(nameof(deletedRecords));
            if (!Enum.IsDefined(typeof(DbfOpenMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            string resolved = DbfPaths.Table(Root, path);
            if (!File.Exists(resolved)) throw new FileNotFoundException("DBF file not found.", resolved);
            var table = new DbfTable<T>(this, resolved, mapping, codepage, encoding, deletedRecords, mode);
            tables.Add(table);
            return table;
        }

        internal void Remove(IDisposable table) => tables.Remove(table);

        public void Dispose()
        {
            if (Thread.CurrentThread != thread) throw new InvalidOperationException("The session may only be disposed on its owning thread.");
            if (disposed) return;
            if (callbackActive) throw new InvalidOperationException("The session cannot be disposed from an update callback.");
            var errors = new List<Exception>();
            while (tables.Count != 0)
            {
                var table = tables[tables.Count - 1];
                tables.RemoveAt(tables.Count - 1);
                try { table.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
            disposed = true;
            if (errors.Count != 0) throw new AggregateException("Failed to close one or more tables.", errors);
        }
    }
}

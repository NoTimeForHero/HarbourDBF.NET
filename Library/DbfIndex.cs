using System;

namespace HarbourDBF.NET
{
    public abstract class DbfIndexDefinition
    {
        internal DbfIndexDefinition(object owner, DbfFieldMapping field, string file, string tag) { Owner = owner; Field = field; File = file; Tag = tag; }
        internal object Owner { get; }
        internal DbfFieldMapping Field { get; }
        internal string File { get; }
        internal string Tag { get; }
    }

    public sealed class DbfIndexMapping<T, TKey> : DbfIndexDefinition where T : class, new()
    {
        internal DbfIndexMapping(object owner, DbfFieldMapping field, string file, string tag) : base(owner, field, file, tag) { }
    }

    public sealed class DbfIndex<T, TKey> where T : class, new()
    {
        private readonly DbfTable<T> table;
        private readonly DbfIndexMapping<T, TKey> mapping;
        internal DbfIndex(DbfTable<T> table, DbfIndexMapping<T, TKey> mapping) { this.table = table; this.mapping = mapping; }
        public DbfRecord<T> FindFirst(TKey key) => table.Find(mapping, key, false);
        public DbfRecord<T> FindLast(TKey key) => table.Find(mapping, key, true);
    }
}

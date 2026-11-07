using System;
using System.Collections.Generic;

namespace HarbourDBF.NET
{
    public abstract class DbfIndexDefinition
    {
        internal DbfIndexDefinition(object owner, DbfFieldMapping field, string file, string tag, DbfKeyFormat format) { Owner = owner; Field = field; File = file; Tag = tag; Format = format; }
        internal object Owner { get; }
        internal DbfFieldMapping Field { get; }
        internal string File { get; }
        internal string Tag { get; }
        internal DbfKeyFormat Format { get; }
        public string HarbourExpression => Format == null ? Field.Name : Format.Expression(Field.Name);
    }

    public sealed class DbfIndexMapping<T, TKey> : DbfIndexDefinition where T : class, new()
    {
        internal DbfIndexMapping(object owner, DbfFieldMapping field, string file, string tag, DbfKeyFormat format) : base(owner, field, file, tag, format) { }
    }

    public sealed class DbfIndex<T, TKey> where T : class, new()
    {
        private readonly DbfTable<T> table;
        private readonly DbfIndexMapping<T, TKey> mapping;
        internal DbfIndex(DbfTable<T> table, DbfIndexMapping<T, TKey> mapping) { this.table = table; this.mapping = mapping; }
        public DbfRecord<T> FindFirst(TKey key) => table.Find(mapping, key, false);
        public DbfRecord<T> FindLast(TKey key) => table.Find(mapping, key, true);
        public IEnumerable<DbfRecord<T>> EqualTo(TKey key) => table.EqualTo(mapping, key);
        public IEnumerable<DbfRecord<T>> Scan() => table.ScanIndex(mapping);
    }
}

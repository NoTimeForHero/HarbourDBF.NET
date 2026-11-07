using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HarbourDBF.NET
{
    [Flags]
    public enum DbfRangeBounds { ExcludeBoth = 0, IncludeStart = 1, IncludeEnd = 2, IncludeBoth = 3 }

    public abstract class DbfIndexDefinition
    {
        internal DbfIndexDefinition(object owner, DbfFieldMapping field, string file, string tag, DbfKeyFormat format) { Owner = owner; Field = field; File = file; Tag = tag; Format = format; }
        internal object Owner { get; }
        internal DbfFieldMapping Field { get; }
        internal string File { get; }
        internal string Tag { get; }
        internal DbfKeyFormat Format { get; }
        internal readonly List<DbfCompositePart> Parts = new List<DbfCompositePart>();
        internal string SearchBlock { get; set; }
        internal Func<object, object[]> SearchArguments { get; set; }
        internal bool IsSearchKey => SearchBlock != null;
        internal bool IsComposite => Field == null && !IsSearchKey;
        public string HarbourExpression => IsSearchKey ? null : IsComposite
            ? string.Join("+", Parts.Select(part => part.Format.Expression(part.Field.Name)))
            : Format == null ? Field.Name : Format.Expression(Field.Name);
    }

    public sealed class DbfIndexMapping<T, TKey> : DbfIndexDefinition where T : class, new()
    {
        private readonly DbfMapping<T> mapping;
        internal DbfIndexMapping(object owner, DbfFieldMapping field, string file, string tag, DbfKeyFormat format) : base(owner, field, file, tag, format) { }
        internal DbfIndexMapping(DbfMapping<T> mapping, string file, string tag) : base(mapping, null, file, tag, null) { this.mapping = mapping; }

        public DbfIndexMapping<T, TKey> Part<TPart>(Expression<Func<TKey, TPart>> key, Expression<Func<T, TPart>> field, DbfKeyFormat format)
        {
            if (!IsComposite) throw new InvalidOperationException("Parts can only be added to a composite index.");
            mapping.CheckMutable();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (format == null) throw new ArgumentNullException(nameof(format));
            var member = key.Body as MemberExpression;
            var property = member?.Member as PropertyInfo;
            if (property == null || member.Expression != key.Parameters[0] || property.GetGetMethod() == null || property.GetIndexParameters().Length != 0)
                throw new ArgumentException("Select a public property of the key.", nameof(key));
            if (Parts.Any(part => part.KeyProperty == property)) throw new ArgumentException("The key property is already used.", nameof(key));
            if (format.InputType != typeof(TPart) || !format.Width.HasValue) throw new ArgumentException("A fixed-width format matching the part type is required.", nameof(format));
            var mapped = mapping.FindField(field);
            var getter = key.Compile();
            Parts.Add(new DbfCompositePart(mapped, property, value => getter((TKey)value), format));
            return this;
        }

        public DbfIndexMapping<T, TKey> SearchKey(string harbour, Expression<Func<TKey, object[]>> arguments)
        {
            if (Field != null || IsSearchKey || Parts.Count != 0) throw new InvalidOperationException("SearchKey requires an empty index descriptor.");
            mapping.CheckMutable();
            if (string.IsNullOrWhiteSpace(harbour)) throw new ArgumentException("A Harbour block is required.", nameof(harbour));
            string block = harbour.Trim();
            if (!block.StartsWith("{|", StringComparison.Ordinal) || !block.EndsWith("}", StringComparison.Ordinal))
                throw new ArgumentException("SearchKey must be a Harbour block literal.", nameof(harbour));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            var get = arguments.Compile();
            SearchBlock = block;
            SearchArguments = value => get((TKey)value);
            return this;
        }
    }

    internal sealed class DbfCompositePart
    {
        internal DbfCompositePart(DbfFieldMapping field, PropertyInfo property, Func<object, object> get, DbfKeyFormat format)
        { Field = field; KeyProperty = property; Get = get; Format = format; }
        internal DbfFieldMapping Field { get; }
        internal PropertyInfo KeyProperty { get; }
        internal Func<object, object> Get { get; }
        internal DbfKeyFormat Format { get; }
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
        public IEnumerable<DbfRecord<T>> Range(TKey from, TKey to, DbfRangeBounds bounds)
        {
            if (mapping.IsSearchKey) throw new NotSupportedException("Range is not supported for SearchKey blocks.");
            if ((bounds & ~DbfRangeBounds.IncludeBoth) != 0) throw new ArgumentOutOfRangeException(nameof(bounds));
            return table.Range(mapping, from, to, bounds);
        }
    }
}

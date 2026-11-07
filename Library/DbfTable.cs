using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HarbourDBF.NET.Beta;
using HarbourDBF.NET.Data;
using Newtonsoft.Json.Linq;

namespace HarbourDBF.NET
{
    public sealed class DbfRecordRef<T> where T : class, new()
    {
        internal DbfRecordRef(DbfTable<T> owner, int number) { Owner = owner; Number = number; }
        internal DbfTable<T> Owner { get; }
        internal int Number { get; }
    }

    public sealed class DbfRecord<T> where T : class, new()
    {
        internal DbfRecord(T value, DbfRecordRef<T> reference, bool deleted) { Value = value; Reference = reference; IsDeleted = deleted; }
        public T Value { get; }
        public DbfRecordRef<T> Reference { get; }
        public long RecordNumber => Reference.Number;
        public bool IsDeleted { get; }
    }

    /// <summary>Открытая для чтения таблица; результаты являются отдельными объектами.</summary>
    public sealed class DbfTable<T> : IDisposable where T : class, new()
    {
        private readonly DbfSession session;
        private readonly string alias = "A" + Guid.NewGuid().ToString("N");
        private readonly IReadOnlyList<DbfFieldMapping> fields;
        private readonly Encoding encoding;
        private readonly DbfMapping<T> mapping;
        private readonly Dictionary<DbfIndexDefinition, int> orders = new Dictionary<DbfIndexDefinition, int>();
        private readonly Dictionary<string, FieldType> schemaFields = new Dictionary<string, FieldType>(StringComparer.OrdinalIgnoreCase);
        private bool disposed;

        internal DbfTable(DbfSession session, string path, DbfMapping<T> mapping, string codepage, Encoding encoding)
        {
            this.session = session;
            this.mapping = mapping;
            this.encoding = encoding ?? Constants.DefaultEncoding;
            fields = mapping.Freeze();
            string autoOpen = DbfHarbour.Eval("Set(45)");
            try
            {
                DbfHarbour.Eval("Set(45,.F.)");
                DbfHarbour.UseReadOnly(path, alias, false, codepage);
                try { ValidateSchema(InternalUniversal.GetFields()); LoadIndexes(path); }
                catch (Exception error)
                {
                    try { DbfHarbour.CloseArea(alias); }
                    catch (Exception close) { throw new AggregateException(error, close); }
                    throw;
                }
            }
            finally { DbfHarbour.Eval(autoOpen == "true" ? "Set(45,.T.)" : "Set(45,.F.)"); }
        }

        private void ValidateSchema(FieldType[] schema)
        {
            foreach (var field in fields)
            {
                var matches = schema.Where(x => string.Equals(x.Name, field.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("DBF field not found or ambiguous: " + field.Name);
                var dbf = matches[0];
                schemaFields[field.Name] = dbf;
                Type type = field.Property.PropertyType;
                Type underlying = Nullable.GetUnderlyingType(type) ?? type;
                bool valid = (dbf.Type == "C" || dbf.Type == "M") && type == typeof(string)
                    || dbf.Type == "L" && type == typeof(bool)
                    || (dbf.Type == "N" && dbf.Point == 0 || dbf.Type == "+" || dbf.Type == "^") && (type == typeof(int) || type == typeof(long))
                    || (dbf.Type == "D" || dbf.Type == "@" || dbf.Type == "=") && underlying == typeof(DateTime) && Nullable.GetUnderlyingType(type) != null;
                if (!valid) throw new InvalidOperationException("Unsupported property type for DBF field " + field.Name + " (" + dbf.Type + ").");
                fieldTypes[field.Name] = dbf.Type;
            }
        }

        private readonly Dictionary<string, string> fieldTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private void LoadIndexes(string tablePath)
        {
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bagNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var index in mapping.Indexes)
            {
                string path = DbfPaths.Index(tablePath, index.File);
                if (!File.Exists(path)) throw new FileNotFoundException("Index file not found.", path);
                string bagName = Path.GetFileNameWithoutExtension(path);
                if (bagNames.TryGetValue(bagName, out var previous) && !string.Equals(previous, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Index files with the same bag name cannot be distinguished: " + bagName);
                bagNames[bagName] = path;
                if (loaded.Add(path)) DbfHarbour.Indexes.Load(path);
            }
            int count = int.Parse(DbfHarbour.Eval("OrdCount()"), CultureInfo.InvariantCulture);
            foreach (var index in mapping.Indexes)
            {
                int match = 0;
                for (int order = 1; order <= count; order++)
                {
                    var data = JArray.Parse(DbfHarbour.Eval("{OrdName(" + order + "),OrdBagName(" + order + "),OrdKey(" + order + "),OrdDescend(" + order + "),OrdFor(" + order + "),OrdIsUnique(" + order + ")}"));
                    if (!string.Equals((string)data[0], index.Tag, StringComparison.OrdinalIgnoreCase)) continue;
                    if (match != 0) throw new InvalidOperationException("Duplicate index tag: " + index.Tag);
                    string bag = Path.GetFileNameWithoutExtension(DbfPaths.Index(tablePath, index.File));
                    var schema = schemaFields[index.Field.Name];
                    if (!string.Equals((string)data[1], bag, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(((string)data[2]).Trim(), index.Field.Name, StringComparison.OrdinalIgnoreCase) ||
                        (bool)data[3] || !string.IsNullOrWhiteSpace((string)data[4]) || (bool)data[5] ||
                        !(index.Field.Property.PropertyType == typeof(int) && schema.Type == "N" && schema.Point == 0 ||
                          index.Field.Property.PropertyType == typeof(string) && schema.Type == "C" && encoding.IsSingleByte))
                        throw new InvalidOperationException("Unsupported or mismatched index tag: " + index.Tag);
                    match = order;
                }
                if (match == 0) throw new InvalidOperationException("Index tag not found: " + index.Tag);
                orders.Add(index, match);
            }
        }

        public DbfIndex<T, TKey> Index<TKey>(DbfIndexMapping<T, TKey> descriptor)
        {
            Check();
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (!ReferenceEquals(descriptor.Owner, mapping) || !orders.ContainsKey(descriptor)) throw new ArgumentException("Index belongs to another mapping.", nameof(descriptor));
            return new DbfIndex<T, TKey>(this, descriptor);
        }

        internal DbfRecord<T> Find<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key, bool last)
        {
            Check();
            DbfHarbour.SelectArea(alias);
            DbfHarbour.Indexes.Select(orders[descriptor]);
            string oldDeleted = DbfHarbour.Eval("Set(11)");
            try
            {
                DbfHarbour.Eval("Set(11,.T.)");
                bool found;
                if (key is int number) found = DbfHarbour.Indexes.Seek(number, false, last);
                else if (key is string text)
                {
                    var strict = (Encoding)encoding.Clone();
                    strict.EncoderFallback = EncoderFallback.ExceptionFallback;
                    int length = strict.GetByteCount(text);
                    int width = schemaFields[descriptor.Field.Name].Length;
                    if (length > width) throw new ArgumentException("The key exceeds the field width.", nameof(key));
                    found = DbfHarbour.Indexes.Seek(text.PadRight(text.Length + width - length), false, last, strict);
                }
                else throw new ArgumentException("A non-null index key is required.", nameof(key));
                return found ? ReadAt(DbfHarbour.ActiveRecord) : null;
            }
            finally { DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)"); }
        }

        private void Check() { session.Check(); if (disposed) throw new ObjectDisposedException(nameof(DbfTable<T>)); }

        public DbfRecord<T> ReadAt(long recordNumber)
        {
            Check();
            if (recordNumber < 1 || recordNumber > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(recordNumber));
            DbfHarbour.SelectArea(alias);
            if (recordNumber > DbfHarbour.TotalRecords) return null;
            DbfHarbour.GoTo(recordNumber);
            bool deleted = DbfHarbour.IsRecordDeleted;
            if (deleted) return null;
            object[] values = DbfHarbour.GetValues(fields.Select(x => x.Name), encoding).ToArray();
            if (values.Length != fields.Count) throw new InvalidOperationException("DBF returned an unexpected number of fields.");
            var model = new T();
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                object value = ConvertValue(values[i], field.Property.PropertyType, fieldTypes[field.Name]);
                if (field.TrimEnd && value is string s) value = s.TrimEnd();
                field.Property.SetValue(model, value);
            }
            return new DbfRecord<T>(model, new DbfRecordRef<T>(this, (int)recordNumber), deleted);
        }

        private static object ConvertValue(object value, Type type, string dbfType)
        {
            if (dbfType == "D" || dbfType == "@" || dbfType == "=")
            {
                string text = value as string;
                if (string.IsNullOrWhiteSpace(text) || text.All(c => c == '0')) return null;
                return DateTime.ParseExact(text, dbfType == "D" ? "yyyyMMdd" : "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None);
            }
            if (type == typeof(int)) return checked(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            if (type == typeof(long)) return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (value == null || !type.IsInstanceOfType(value)) throw new InvalidOperationException("Unexpected DBF value for " + dbfType);
            return value;
        }

        public DbfRecord<T> Read(DbfRecordRef<T> reference)
        {
            Check();
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (!ReferenceEquals(reference.Owner, this)) throw new ArgumentException("The reference belongs to another table.", nameof(reference));
            return ReadAt(reference.Number);
        }

        public IEnumerable<DbfRecord<T>> Scan()
        {
            Check();
            for (long number = 1; ; number++)
            {
                Check();
                DbfHarbour.SelectArea(alias);
                if (number > DbfHarbour.TotalRecords) yield break;
                var record = ReadAt(number);
                if (record != null) yield return record;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            session.Check();
            disposed = true;
            session.Remove(this);
            DbfHarbour.CloseArea(alias);
        }
    }
}

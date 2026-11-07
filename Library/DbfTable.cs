using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HarbourDBF.NET.Beta;
using HarbourDBF.NET.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HarbourDBF.NET
{
    public sealed class DbfLockException : InvalidOperationException
    {
        internal DbfLockException(int recordNumber) : base("Could not lock DBF record " + recordNumber + ".") { }
    }

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
        private readonly string codepage;
        private readonly DbfMapping<T> mapping;
        private readonly DbfDeletedRecords deletedRecords;
        private readonly bool writable;
        private readonly bool shared;
        private readonly Dictionary<DbfIndexDefinition, int> orders = new Dictionary<DbfIndexDefinition, int>();
        private readonly Dictionary<DbfIndexDefinition, int> searchKeyWidths = new Dictionary<DbfIndexDefinition, int>();
        private readonly Dictionary<string, FieldType> schemaFields = new Dictionary<string, FieldType>(StringComparer.OrdinalIgnoreCase);
        private FieldType[] fullSchema;
        private bool disposed;

        internal DbfTable(DbfSession session, string path, DbfMapping<T> mapping, string codepage, Encoding encoding, DbfDeletedRecords deletedRecords, DbfOpenMode mode)
        {
            this.session = session;
            this.mapping = mapping;
            this.codepage = codepage;
            this.deletedRecords = deletedRecords;
            writable = mode == DbfOpenMode.SharedReadWrite || mode == DbfOpenMode.ExclusiveReadWrite;
            shared = mode == DbfOpenMode.SharedReadOnly || mode == DbfOpenMode.SharedReadWrite;
            this.encoding = (Encoding)(encoding ?? Constants.DefaultEncoding).Clone();
            this.encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
            fields = mapping.Freeze();
            string autoOpen = DbfHarbour.Eval("Set(45)");
            bool opened = false;
            Exception failure = null;
            try
            {
                DbfHarbour.Eval("Set(45,.F.)");
                bool exclusive = mode == DbfOpenMode.ExclusiveReadOnly || mode == DbfOpenMode.ExclusiveReadWrite;
                if (writable) DbfHarbour.Use(path, alias, exclusive, codepage);
                else DbfHarbour.UseReadOnly(path, alias, exclusive, codepage);
                opened = true;
                ValidateSchema(InternalUniversal.GetFields());
                LoadIndexes(path);
            }
            catch (Exception error) { failure = error; }
            try { DbfHarbour.Eval(autoOpen == "true" ? "Set(45,.T.)" : "Set(45,.F.)"); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            if (failure != null)
            {
                if (opened)
                {
                    try { DbfHarbour.CloseArea(alias); }
                    catch (Exception close) { failure = new AggregateException(failure, close); }
                }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private void ValidateSchema(FieldType[] schema)
        {
            fullSchema = schema;
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

        private TResult InCodepage<TResult>(Func<TResult> action) => DbfRuntime.Invoke(() => DbfCodepage.Run(codepage, action));

        private void LoadIndexes(string tablePath)
        {
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bagNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in mapping.Indexes.Select(index => index.File).Concat(mapping.IndexBags))
            {
                string path = DbfPaths.Index(tablePath, file);
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
                    if (!string.Equals((string)data[1], bag, StringComparison.OrdinalIgnoreCase) ||
                        (!index.IsSearchKey && !string.Equals(RemoveWhitespace((string)data[2]), RemoveWhitespace(index.HarbourExpression), StringComparison.OrdinalIgnoreCase)) ||
                        (bool)data[3] || !string.IsNullOrWhiteSpace((string)data[4]) || (bool)data[5] ||
                        !IsSupportedIndex(index))
                        throw new InvalidOperationException("Unsupported or mismatched index tag: " + index.Tag);
                    match = order;
                }
                if (match == 0) throw new InvalidOperationException("Index tag not found: " + index.Tag);
                if (index.IsSearchKey)
                {
                    DbfHarbour.Indexes.Select(match);
                    string keyType = JsonConvert.DeserializeObject<string>(DbfHarbour.Eval("HDBF_KEYTYPE()"));
                    int width = int.Parse(DbfHarbour.Eval("HDBF_KEYSIZE()"), CultureInfo.InvariantCulture);
                    if (keyType != "C" || width < 1 || width > 254) throw new InvalidOperationException("SearchKey requires a fixed-width character tag: " + index.Tag);
                    RunSearchBlock(index, null, true);
                    searchKeyWidths.Add(index, width);
                }
                orders.Add(index, match);
            }
        }

        private static string RemoveWhitespace(string value) => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

        private bool IsSupportedIndex(DbfIndexDefinition index)
        {
            if (index.IsSearchKey) return encoding.IsSingleByte;
            if (index.IsComposite)
                return encoding.IsSingleByte && index.Parts.Count > 0 &&
                    index.Parts.Sum(part => part.Format.Width.Value) <= 254 &&
                    index.Parts.All(part => IsSupportedIndexField(part.Field, part.Format));
            return IsSupportedIndexField(index.Field, index.Format);
        }

        private bool IsSupportedIndexField(DbfFieldMapping field, DbfKeyFormat format)
        {
            var schema = schemaFields[field.Name];
            bool fieldSupported = field.Property.PropertyType == typeof(int) && schema.Type == "N" && schema.Point == 0 ||
                field.Property.PropertyType == typeof(string) && schema.Type == "C" && encoding.IsSingleByte;
            return fieldSupported && (format == null || encoding.IsSingleByte && format.Width.HasValue);
        }

        public DbfIndex<T, TKey> Index<TKey>(DbfIndexMapping<T, TKey> descriptor)
        {
            if (!DbfRuntime.IsExecutorThread) return DbfRuntime.Invoke(() => Index(descriptor));
            Check();
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (!ReferenceEquals(descriptor.Owner, mapping) || !orders.ContainsKey(descriptor)) throw new ArgumentException("Index belongs to another mapping.", nameof(descriptor));
            return new DbfIndex<T, TKey>(this, descriptor);
        }

        internal DbfRecord<T> Find<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key, bool last)
            => InCodepage(() => FindCore(descriptor, key, last));

        private DbfRecord<T> FindCore<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key, bool last)
        {
            Check();
            DbfHarbour.SelectArea(alias);
            DbfHarbour.Indexes.Select(orders[descriptor]);
            string oldDeleted = DbfHarbour.Eval("Set(11)");
            try
            {
                DbfHarbour.Eval(deletedRecords == DbfDeletedRecords.Exclude ? "Set(11,.T.)" : "Set(11,.F.)");
                bool found = SeekPrepared(PrepareKey(descriptor, key), false, last);
                return found ? ReadAt(DbfHarbour.ActiveRecord) : null;
            }
            finally { DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)"); }
        }

        private object PrepareKey<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key)
        {
            if (descriptor.IsSearchKey)
            {
                if ((object)key == null) throw new ArgumentNullException(nameof(key));
                string formatted = RunSearchBlock(descriptor, descriptor.SearchArguments(key), false);
                if (encoding.GetByteCount(formatted) != searchKeyWidths[descriptor]) throw new ArgumentException("SearchKey must return the complete tag key width.", nameof(key));
                return formatted;
            }
            if (descriptor.IsComposite)
            {
                if ((object)key == null) throw new ArgumentNullException(nameof(key));
                var formattedParts = descriptor.Parts.Select(part => part.Format.Format(part.Get(key), encoding)).ToArray();
                for (int i = 0; i < formattedParts.Length; i++)
                    if (encoding.GetByteCount(formattedParts[i]) != descriptor.Parts[i].Format.Width.Value)
                        throw new InvalidOperationException("Formatted key part has an unexpected width.");
                return string.Concat(formattedParts);
            }
            if (descriptor.Format != null)
            {
                string formatted = descriptor.Format.Format(key, encoding);
                if (encoding.GetByteCount(formatted) != descriptor.Format.Width.Value) throw new InvalidOperationException("Formatted key has an unexpected width.");
                return formatted;
            }
            if (key is int number) return number;
            if (key is string text)
            {
                var strict = (Encoding)encoding.Clone();
                strict.EncoderFallback = EncoderFallback.ExceptionFallback;
                int length = strict.GetByteCount(text);
                int width = schemaFields[descriptor.Field.Name].Length;
                if (length > width) throw new ArgumentException("The key exceeds the field width.", nameof(key));
                if (text.IndexOf('\0') >= 0) throw new ArgumentException("A key cannot contain a null character.", nameof(key));
                return text.PadRight(text.Length + width - length);
            }
            throw new ArgumentException("A non-null index key is required.", nameof(key));
        }

        private bool SeekPrepared(object key, bool soft, bool last)
        {
            if (key is int number) return DbfHarbour.Indexes.Seek(number, soft, last);
            return DbfHarbour.Indexes.Seek((string)key, soft, last, encoding);
        }

        private string RunSearchBlock(DbfIndexDefinition descriptor, object[] arguments, bool validateOnly)
        {
            var strict = (Encoding)encoding.Clone();
            strict.EncoderFallback = EncoderFallback.ExceptionFallback;
            strict.GetByteCount(descriptor.SearchBlock);
            if (!validateOnly)
            {
                if (arguments == null) throw new ArgumentException("SearchKey arguments cannot be null.");
                foreach (object value in arguments)
                {
                    if (value is string text)
                    {
                        if (text.IndexOf('\0') >= 0) throw new ArgumentException("SearchKey arguments cannot contain a null character.");
                        strict.GetByteCount(text);
                    }
                    else if (!(value is int) && !(value is bool)) throw new ArgumentException("SearchKey arguments must be strings, int values or booleans.");
                }
            }
            string input = JsonConvert.SerializeObject(new { block = descriptor.SearchBlock, arguments = arguments ?? Array.Empty<object>() });
            JToken result = InternalUniversal.ProcedureCall<JToken>(validateOnly ? "VALIDATE_SEARCH_BLOCK" : "SEARCH_BLOCK", input, strict);
            if (validateOnly) return null;
            if (result == null || result.Type != JTokenType.String) throw new InvalidOperationException("SearchKey returned a non-character value.");
            string output = result.Value<string>();
            strict.GetByteCount(output);
            if (output.IndexOf('\0') >= 0) throw new ArgumentException("SearchKey returned a null character.");
            return output;
        }

        internal IEnumerable<DbfRecord<T>> EqualTo<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key)
        {
            int previous = 0;
            object prepared = null;
            while (true)
            {
                DbfRecord<T> next = null;
                bool found = InCodepage(() => NextEqual(descriptor, key, ref prepared, ref previous, out next));
                if (!found) yield break;
                yield return next;
            }
        }

        private bool NextEqual<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey key, ref object prepared, ref int previous, out DbfRecord<T> next)
        {
            while (true)
            {
                Check();
                next = null;
                DbfHarbour.SelectArea(alias);
                DbfHarbour.Indexes.Select(orders[descriptor]);
                string oldDeleted = DbfHarbour.Eval("Set(11)");
                try
                {
                    // OrdKeyNo/OrdKeyGoto use physical index positions. Keep deleted
                    // keys in that numbering and apply the visibility policy after positioning.
                    DbfHarbour.Eval("Set(11,.F.)");
                    if (prepared == null) prepared = PrepareKey(descriptor, key);
                    if (!SeekPrepared(prepared, false, false)) return false;
                    long first = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                    if (!SeekPrepared(prepared, false, true)) return false;
                    long last = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                    long position = first;
                    if (previous != 0)
                    {
                        DbfHarbour.GoTo(previous);
                        position = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                        if (position < first || position > last) return false;
                        position++;
                    }
                    if (position > last || DbfHarbour.Eval("OrdKeyGoto(" + position.ToString(CultureInfo.InvariantCulture) + ")") != "true") return false;
                    previous = DbfHarbour.ActiveRecord;
                    next = ReadAt(previous);
                }
                finally { DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)"); }
                if (next != null) return true;
            }
        }

        internal IEnumerable<DbfRecord<T>> ScanIndex<TKey>(DbfIndexMapping<T, TKey> descriptor)
        {
            int previous = 0;
            while (true)
            {
                DbfRecord<T> next = null;
                bool found = InCodepage(() => NextIndex(descriptor, ref previous, out next));
                if (!found) yield break;
                yield return next;
            }
        }

        private bool NextIndex<TKey>(DbfIndexMapping<T, TKey> descriptor, ref int previous, out DbfRecord<T> next)
        {
            while (true)
            {
                Check();
                next = null;
                DbfHarbour.SelectArea(alias);
                DbfHarbour.Indexes.Select(orders[descriptor]);
                string oldDeleted = DbfHarbour.Eval("Set(11)");
                try
                {
                    DbfHarbour.Eval("Set(11,.F.)");
                    long position = 1;
                    if (previous != 0)
                    {
                        DbfHarbour.GoTo(previous);
                        position = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                        if (position < 1) return false;
                        position++;
                    }
                    if (DbfHarbour.Eval("OrdKeyGoto(" + position.ToString(CultureInfo.InvariantCulture) + ")") != "true") return false;
                    previous = DbfHarbour.ActiveRecord;
                    next = ReadAt(previous);
                }
                finally { DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)"); }
                if (next != null) return true;
            }
        }

        internal IEnumerable<DbfRecord<T>> Range<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey from, TKey to, DbfRangeBounds bounds)
        {
            int previous = 0;
            object lower = null, upper = null;
            while (true)
            {
                DbfRecord<T> next = null;
                bool found = InCodepage(() => NextRange(descriptor, from, to, bounds, ref lower, ref upper, ref previous, out next));
                if (!found) yield break;
                yield return next;
            }
        }

        private bool NextRange<TKey>(DbfIndexMapping<T, TKey> descriptor, TKey from, TKey to, DbfRangeBounds bounds,
            ref object lower, ref object upper, ref int previous, out DbfRecord<T> next)
        {
            while (true)
            {
                Check();
                next = null;
                DbfHarbour.SelectArea(alias);
                DbfHarbour.Indexes.Select(orders[descriptor]);
                string oldDeleted = DbfHarbour.Eval("Set(11)");
                try
                {
                    DbfHarbour.Eval("Set(11,.F.)");
                    if (lower == null) lower = PrepareKey(descriptor, from);
                    if (upper == null) upper = PrepareKey(descriptor, to);
                    long first;
                    if (SeekPrepared(lower, false, false))
                    {
                        if ((bounds & DbfRangeBounds.IncludeStart) == 0) SeekPrepared(lower, false, true);
                        first = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                        if ((bounds & DbfRangeBounds.IncludeStart) == 0) first++;
                    }
                    else
                    {
                        SeekPrepared(lower, true, false);
                        if (DbfHarbour.EndOfFile) return false;
                        first = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                    }
                    long last;
                    if (SeekPrepared(upper, false, false))
                    {
                        if ((bounds & DbfRangeBounds.IncludeEnd) != 0) SeekPrepared(upper, false, true);
                        last = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                        if ((bounds & DbfRangeBounds.IncludeEnd) == 0) last--;
                    }
                    else
                    {
                        SeekPrepared(upper, true, false);
                        last = DbfHarbour.EndOfFile ? DbfHarbour.Indexes.OrdKeyCount() :
                            long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture) - 1;
                    }
                    long position = first;
                    if (previous != 0)
                    {
                        DbfHarbour.GoTo(previous);
                        position = long.Parse(DbfHarbour.Eval("OrdKeyNo()"), CultureInfo.InvariantCulture);
                        if (position < first || position > last) return false;
                        position++;
                    }
                    if (position < 1 || position > last || DbfHarbour.Eval("OrdKeyGoto(" + position.ToString(CultureInfo.InvariantCulture) + ")") != "true") return false;
                    previous = DbfHarbour.ActiveRecord;
                    next = ReadAt(previous);
                }
                finally { DbfHarbour.Eval(oldDeleted == "true" ? "Set(11,.T.)" : "Set(11,.F.)"); }
                if (next != null) return true;
            }
        }

        private void Check() { session.Check(); if (disposed) throw new ObjectDisposedException(nameof(DbfTable<T>)); }

        public DbfRecord<T> ReadAt(long recordNumber)
            => InCodepage(() => ReadAtCore(recordNumber));

        private DbfRecord<T> ReadAtCore(long recordNumber)
        {
            Check();
            if (recordNumber < 1 || recordNumber > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(recordNumber));
            DbfHarbour.SelectArea(alias);
            if (recordNumber > DbfHarbour.TotalRecords) return null;
            DbfHarbour.GoTo(recordNumber);
            bool deleted = DbfHarbour.IsRecordDeleted;
            if (deleted && deletedRecords == DbfDeletedRecords.Exclude) return null;
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
                if (text == null) throw new InvalidOperationException("Unexpected DBF date or timestamp value.");
                if (string.IsNullOrWhiteSpace(text) || text.All(c => c == '0')) return null;
                return DateTime.ParseExact(text, dbfType == "D" ? "yyyyMMdd" : "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None);
            }
            if (type == typeof(int) || type == typeof(long))
            {
                long integer;
                if (value is long n) integer = n;
                else if (value is int i) integer = i;
                else if (value is double d && !double.IsNaN(d) && !double.IsInfinity(d) &&
                         Math.Abs(d) < 9007199254740992d && Math.Truncate(d) == d)
                    integer = checked((long)d);
                else throw new InvalidOperationException("The DBF numeric value cannot be converted to an exact integer.");
                return type == typeof(int) ? (object)checked((int)integer) : integer;
            }
            if (value == null || !type.IsInstanceOfType(value)) throw new InvalidOperationException("Unexpected DBF value for " + dbfType);
            return value;
        }

        public DbfRecord<T> Read(DbfRecordRef<T> reference)
            => InCodepage(() => ReadCore(reference));

        private DbfRecord<T> ReadCore(DbfRecordRef<T> reference)
        {
            Check();
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (!ReferenceEquals(reference.Owner, this)) throw new ArgumentException("The reference belongs to another table.", nameof(reference));
            return ReadAt(reference.Number);
        }

        public void MarkDeleted(DbfRecordRef<T> reference) => SetDeleted(reference, true);

        public void Recall(DbfRecordRef<T> reference) => SetDeleted(reference, false);

        public DbfRecord<T> Update(DbfRecordRef<T> reference, Action<T> update)
            => InCodepage(() => UpdateCore(reference, update));

        private DbfRecord<T> UpdateCore(DbfRecordRef<T> reference, Action<T> update)
        {
            Check();
            if (!writable) throw new InvalidOperationException("The table is read-only.");
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (!ReferenceEquals(reference.Owner, this)) throw new ArgumentException("The reference belongs to another table.", nameof(reference));
            if (update == null) throw new ArgumentNullException(nameof(update));
            if (DbfRuntime.ContainsAsync(update))
                throw new ArgumentException("The update callback must be synchronous.", nameof(update));
            DbfHarbour.SelectArea(alias);
            if (reference.Number > DbfHarbour.TotalRecords) throw new InvalidOperationException("The referenced record no longer exists.");
            EnsureNoExistingLocks();
            if (!DbfHarbour.RecordLock(reference.Number)) throw new DbfLockException(reference.Number);
            Exception failure = null;
            DbfRecord<T> result = null;
            try
            {
                DbfHarbour.GoTo(reference.Number);
                if (DbfHarbour.IsRecordDeleted) throw new InvalidOperationException("A deleted record cannot be updated.");
                var current = ReadAt(reference.Number);
                if (current == null) throw new InvalidOperationException("The referenced record could not be read.");
                var before = fields.Select(field => field.Property.GetValue(current.Value)).ToArray();
                DbfSession.EnterCallback();
                try { update(current.Value); }
                finally { DbfSession.ExitCallback(); }
                var changed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var strictEncoding = (Encoding)encoding.Clone();
                strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    var schema = schemaFields[field.Name];
                    if (IsGenerated(schema.Type)) continue;
                    object value = field.Property.GetValue(current.Value);
                    if (Equals(before[i], value)) continue;
                    ValidateWriteValue(schema, value, strictEncoding);
                    changed.Add(field.Name, value);
                }
                if (changed.Count != 0)
                {
                    DbfHarbour.SelectArea(alias);
                    InternalUniversal.SetValues(new DatabaseFieldsInfo(fullSchema), changed, strictEncoding);
                    DbfHarbour.Commit();
                }
                result = ReadAt(reference.Number);
                if (result == null) throw new InvalidOperationException("The updated record could not be read.");
            }
            catch (Exception error) { failure = error; }
            try { DbfHarbour.SelectArea(alias); DbfHarbour.RecordLock(reference.Number, true); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        public DbfRecord<T> Insert(T value, bool strict = false)
            => InCodepage(() => InsertCore(value, strict));

        private DbfRecord<T> InsertCore(T value, bool strict)
        {
            Check();
            if (!writable) throw new InvalidOperationException("The table is read-only.");
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (strict)
                foreach (var field in fullSchema)
                    if (!schemaFields.ContainsKey(field.Name))
                        throw new InvalidOperationException("Strict insert requires a mapping for DBF field " + field.Name + ".");

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var strictEncoding = (Encoding)encoding.Clone();
            strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
            foreach (var field in fields)
            {
                var schema = schemaFields[field.Name];
                if (IsGenerated(schema.Type)) continue;
                object item = field.Property.GetValue(value);
                ValidateWriteValue(schema, item, strictEncoding);
                values.Add(field.Name, item);
            }

            DbfHarbour.SelectArea(alias);
            EnsureNoExistingLocks();
            DbfHarbour.Append();
            int number = DbfHarbour.ActiveRecord;
            Exception failure = null;
            DbfRecord<T> result = null;
            try
            {
                if (number < 1 || number > DbfHarbour.TotalRecords) throw new InvalidOperationException("Append did not return a valid record number.");
                if (shared && !JArray.Parse(DbfHarbour.Eval("DbRLockList()")).Any(x => (long)x == number))
                    throw new InvalidOperationException("Append did not hold the new record lock.");
                InternalUniversal.SetValues(new DatabaseFieldsInfo(fullSchema), values, strictEncoding);
                DbfHarbour.Commit();
                result = ReadAt(number);
                if (result == null) throw new InvalidOperationException("The appended record could not be read.");
            }
            catch (Exception error) { failure = error; }
            try { if (number > 0) { DbfHarbour.SelectArea(alias); DbfHarbour.RecordLock(number, true); } }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            return result;
        }

        private static bool IsGenerated(string type) => type == "+" || type == "=" || type == "^";

        private static void EnsureNoExistingLocks()
        {
            if (JArray.Parse(DbfHarbour.Eval("DbRLockList()")).Count != 0)
                throw new InvalidOperationException("The work area already holds record locks.");
        }

        private static void ValidateWriteValue(FieldType field, object value, Encoding encoding)
        {
            if (field.Type == "C" || field.Type == "M")
            {
                if (!(value is string text)) throw new ArgumentException("A string is required for field " + field.Name + ".");
                if (text.IndexOf('\0') >= 0) throw new ArgumentException("A string cannot contain a null character: " + field.Name);
                int bytes = encoding.GetByteCount(text);
                if (field.Type == "C" && bytes > field.Length) throw new ArgumentException("Value exceeds the width of field " + field.Name + ".");
            }
            else if (field.Type == "N")
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (value == null || text.Length > field.Length) throw new ArgumentException("Number exceeds the width of field " + field.Name + ".");
            }
            else if (field.Type == "D" || field.Type == "@")
            {
                if (value == null) return;
                var date = (DateTime)value;
                if (date.Kind != DateTimeKind.Unspecified || (field.Type == "D" && date.TimeOfDay != TimeSpan.Zero) ||
                    (field.Type == "@" && date.Ticks % TimeSpan.TicksPerMillisecond != 0))
                    throw new ArgumentException("Invalid date, time or precision for field " + field.Name + ".");
            }
            else if (field.Type != "L") throw new InvalidOperationException("Writing field type " + field.Type + " is not supported.");
        }

        private void SetDeleted(DbfRecordRef<T> reference, bool deleted)
            => InCodepage(() => { SetDeletedCore(reference, deleted); return true; });

        private void SetDeletedCore(DbfRecordRef<T> reference, bool deleted)
        {
            Check();
            if (!writable) throw new InvalidOperationException("The table is read-only.");
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (!ReferenceEquals(reference.Owner, this)) throw new ArgumentException("The reference belongs to another table.", nameof(reference));
            DbfHarbour.SelectArea(alias);
            if (reference.Number > DbfHarbour.TotalRecords) throw new InvalidOperationException("The referenced record no longer exists.");
            EnsureNoExistingLocks();
            if (!DbfHarbour.RecordLock(reference.Number)) throw new DbfLockException(reference.Number);
            Exception failure = null;
            try
            {
                DbfHarbour.GoTo(reference.Number);
                if (DbfHarbour.IsRecordDeleted != deleted)
                {
                    if (deleted) DbfHarbour.Delete(); else DbfHarbour.Recall();
                    DbfHarbour.Commit();
                }
            }
            catch (Exception error) { failure = error; }
            try { DbfHarbour.SelectArea(alias); DbfHarbour.RecordLock(reference.Number, true); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        public IEnumerable<DbfRecord<T>> Scan()
        {
            for (long number = 1; ; number++)
            {
                DbfRecord<T> record = null;
                bool exists = InCodepage(() =>
                {
                    Check();
                    DbfHarbour.SelectArea(alias);
                    if (number > DbfHarbour.TotalRecords) return false;
                    record = ReadAt(number);
                    return true;
                });
                if (!exists) yield break;
                if (record != null) yield return record;
            }
        }

        public void Dispose()
            => InCodepage(() => { DisposeCore(); return true; });

        private void DisposeCore()
        {
            if (disposed) return;
            session.Check();
            disposed = true;
            session.Remove(this);
            DbfHarbour.CloseArea(alias);
        }
    }
}

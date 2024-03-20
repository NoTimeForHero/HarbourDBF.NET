using HarbourDBF.NET.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET.Beta
{
    public class Database : IDisposable
    {
        private readonly string alias;
        private readonly Dictionary<string, IndexInfo> indexesByField = new();
        private readonly DatabaseFieldsInfo _fieldsInfo;
        private static readonly Dictionary<string, DatabaseFieldsInfo> globalFieldCache = new();

        /// <summary>
        /// Кодировка внутри открываемого DBF
        /// </summary>
        public Encoding DatabaseEncoding = null;

        /// <summary>
        /// Необходимо ли удалять пробелы из текстовых полей
        /// </summary>
        public bool TrimText = true;

        /// <summary>
        /// По умолчанию JSON.NET превращает числа в Long
        /// Данная опция принудит возвращать Int
        /// </summary>
        public bool IntInsteadLong = false;

        public Database(string path, string alias = null, bool exclusive = false, string codepage = "", bool useGlobalCache = false)
        {
            this.alias = alias ?? "alias_" + Guid.NewGuid();
            if (useGlobalCache) throw new NotImplementedException("Global cache is under construction!");
            DbfHarbour.Use(path, alias, exclusive, codepage);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!useGlobalCache || !globalFieldCache.TryGetValue(path, out _fieldsInfo))
            {
                FieldType[] fields = InternalUniversal.GetFields();
                _fieldsInfo = new DatabaseFieldsInfo(fields);
                globalFieldCache[path] = _fieldsInfo;
            }
        }

        public IEnumerable<object> GetValues(int recNo, IEnumerable<string> keys)
        {
            DbfHarbour.SelectArea(alias);
            DbfHarbour.GoTo(recNo);
            var values = DbfHarbour.GetValues(keys, DatabaseEncoding).ToArray();
            if (TrimText) Utils.TrimText(values);
            if (IntInsteadLong) Utils.LongToInt(values);
            return values;
        }

        public Dictionary<string, object> GetValuesDict(int recNo, IEnumerable<string> fieldKeys)
        {
            var keys = fieldKeys.ToArray();
            var values = GetValues(recNo, keys).ToArray();
            if (keys.Length != values.Length) throw new InvalidDataException("Wrong size of keys/values!");
            var result = new Dictionary<string, object>();
            for (var i = 0; i < keys.Length; i++) result[keys[i]] = values[i];
            return result;
        }

        public T GetValue<T>(int recNo, string field, T defValue = default)
        {
            var rawValue = GetValues(recNo, new[] { field }).FirstOrDefault();
            if (rawValue is T value) return value;
            var convRes = (T)Convert.ChangeType(rawValue, typeof(T));
            if (convRes != null) return convRes;
            return defValue;
        }

        public void SetValues(int recNo, Dictionary<string, object> values)
        {
            DbfHarbour.SelectArea(alias);
            DbfHarbour.GoTo(recNo);
            InternalUniversal.SetValues(_fieldsInfo, values, DatabaseEncoding);
        }

        public void RegisterIndex(string field, string path)
        {
            if (indexesByField.ContainsKey(field))
                throw new InvalidOperationException($"Index for field \"{field}\" is already registered!");
            var order = indexesByField.Count + 1;
            var index = new IndexInfo(field, path, order);
            indexesByField.Add(field, index);
            DbfHarbour.SelectArea(alias);
            DbfHarbour.Indexes.Load(path); // IDZ
        }

        public RecordLock MakeLock(int recNo) => new(recNo);

        public int Search(string field, int value, bool soft = false, bool last = false)
        {
            if (!indexesByField.TryGetValue(field, out var indexInfo))
                throw new InvalidOperationException($"No index for field: {field}!");
            DbfHarbour.SelectArea(alias);
            DbfHarbour.Indexes.Select(indexInfo.Order);
            DbfHarbour.Indexes.Seek(value, soft, last);
            var found = DbfHarbour.Indexes.Found();
            if (!found) return -1;
            return DbfHarbour.ActiveRecord;
        }

        public void Dispose()
        {
            DbfHarbour.CloseArea(alias);
        }

        public record IndexInfo(string Field, string Path, int Order);


        private static class Utils
        {
            public static void TrimText(object[] values)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var item = values[i];
                    if (item is string text) values[i] = text.Trim();
                }
            }

            public static void LongToInt(object[] values)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var item = values[i];
                    if (item is long longVal) values[i] = Convert.ToInt32(longVal);
                }
            }
        }
    }
}

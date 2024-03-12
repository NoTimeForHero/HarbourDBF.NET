using HarbourDBF.NET.Data;
using Newtonsoft.Json;
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

        public void SetValues(int recNo, Dictionary<string, object> values)
        {
            DbfHarbour.SelectArea(alias);
            DbfHarbour.GoTo(recNo);
            InternalUniversal.SetValues(_fieldsInfo, values);
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
    }
}

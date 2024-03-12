using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET.Data
{
    internal class DatabaseFieldsInfo
    {
        public FieldType[] Fields { get; }
        public Dictionary<string, FieldType> FieldsByName { get; }
        public Dictionary<string, int> OrderByName { get; }
        public DatabaseFieldsInfo(FieldType[] fields)
        {
            Fields = fields;
            FieldsByName = new();
            OrderByName = new();
            for (var i = 0; i < Fields.Length; i++)
            {
                var field = Fields[i];
                var key = field.Name.ToLower();
                FieldsByName[key] = field;
                OrderByName[key] = i + 1; // Нумерация в Harbour с единицы
            }
        }
    }
}

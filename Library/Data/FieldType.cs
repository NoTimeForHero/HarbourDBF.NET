using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET.Data
{
    public struct FieldType
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public ushort Length { get; set; }
        public ushort Point { get; set; }
        public FieldType(string name, string type, ushort length, ushort point)
        {
            Name = name;
            Type = type;
            Length = length;
            Point = point;
        }
        public object ToJson() => new object[] { Name, Type, Length, Point };
    }
}

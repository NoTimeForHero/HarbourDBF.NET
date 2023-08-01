// File: FieldType.cs
// Created by NoTimeForHero, 2023
// Distributed under the Apache License 2.0

namespace Loader.Data
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
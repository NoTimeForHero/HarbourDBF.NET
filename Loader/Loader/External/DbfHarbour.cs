// File: DbfHarbour.cs
// Created by NoTimeForHero, 2023
// Distributed under the Apache License 2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Loader.Data;

namespace Loader.External
{
    internal static class DbfHarbour
    {
        [DllImport("test1.dll", EntryPoint = "DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Create(string databaseName, string json);

        [DllImport("test1.dll", EntryPoint = "DBF_SET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _SetValues(string json);

        [DllImport("test1.dll", EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Use(string databaseName);

        [DllImport("test1.dll", EntryPoint = "DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Append();

        [DllImport("test1.dll", EntryPoint = "DBF_GOTO", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr GoTo(long recordNumber);

        public static void SetValues(Dictionary<string, object> values)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var raw = values.Select(x => new[] { x.Key, x.Value, x.Value.GetType().FullName });
            var json = JsonSerializer.Serialize(raw);
            _SetValues(json);
        }

        public static void Create(string databaseName, IEnumerable<FieldType> fields)
        {
            var json = JsonSerializer.Serialize(fields.Select(x => x.ToJson()));
            _Create(databaseName, json);
        }
    }
}
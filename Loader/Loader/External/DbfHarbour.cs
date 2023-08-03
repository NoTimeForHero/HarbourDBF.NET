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
        private const string DllName = "hbdbf.dll";

        // TODO: Добавить GetLastError для каждого поля

        // TODO: Добавить следующие методы
        // 1. DbSelectArea(cAliasName) - переключиться на указанный алиас
        // 2. DbRLock([xRecNo]) - блокировка активной записи (по умолчанию активной или xRecNo)
        // 3. DbRUnlock([xRecNo]) - разблокировка
        // 4. DbCommit() - сброс изменений на диск
        // 5. DbCloseArea() - закрыть активную DbArea

        [DllImport(DllName, EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Use(string databaseName, string cAlias, bool lExclusive = false, string cCodepage = "");

        [DllImport(DllName, EntryPoint = "DBF_APPEND", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Append();

        [DllImport(DllName, EntryPoint = "DBF_GOTO", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr GoTo(long recordNumber);

        [DllImport(DllName, EntryPoint = "DBF_GET_LAST_ERROR", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _GetLastError();
        public static string GetLastError() => Marshal.PtrToStringAnsi(_GetLastError());

        [DllImport(DllName, EntryPoint = "DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Create(string databaseName, string json);
        public static void Create(string databaseName, IEnumerable<FieldType> fields)
        {
            var json = JsonSerializer.Serialize(fields.Select(x => x.ToJson()));
            _Create(databaseName, json);
        }

        [DllImport(DllName, EntryPoint = "DBF_SET_VALUES", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _SetValues(string json);
        public static void SetValues(Dictionary<string, object> values)
        {
            // TODO: Приведение сложных типов вроде DateTime к корректному Harbour формату
            var raw = values.Select(x => new[] { x.Key, x.Value, x.Value.GetType().FullName });
            var json = JsonSerializer.Serialize(raw);
            _SetValues(json);
        }
    }
}
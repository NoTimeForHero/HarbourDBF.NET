using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Loader
{
    internal static  class TestLibrary
    {
        [DllImport("test1.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "C_TEST", CharSet = CharSet.Ansi)]
        private static extern IntPtr _Test1(string procName, string cText1);

        public static string Test1(string procName, string cText1) => Marshal.PtrToStringAnsi(_Test1(procName, cText1));
    }

    internal static class DbfHarbour
    {
        [DllImport("test1.dll", EntryPoint = "DBF_CREATE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr _Create(string databaseName, string jsonStructure);

        [DllImport("test1.dll", EntryPoint = "DBF_USE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr Use(string databaseName);

        public static void Create(string databaseName, IEnumerable<FieldType> fields)
        {
            var json = JsonSerializer.Serialize(fields.Select(x => x.ToJson()));
            _Create(databaseName, json);
        }
    }

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

    internal static class Win32
    {
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32")]
        public static extern bool AllocConsole();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Loader
{
    internal static  class TestLibrary
    {
        [DllImport("test1.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "C_TEST", CharSet = CharSet.Ansi)]
        private static extern IntPtr RawTest1(string procName, string cText1);

        public static string Test1(string procName, string cText1) => Marshal.PtrToStringAnsi(RawTest1(procName, cText1));
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

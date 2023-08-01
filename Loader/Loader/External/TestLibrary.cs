using System;
using System.Runtime.InteropServices;

namespace Loader.External
{
    internal static  class TestLibrary
    {
        [DllImport("test1.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "C_TEST", CharSet = CharSet.Ansi)]
        private static extern IntPtr _Test1(string procName, string cText1);

        public static string Test1(string procName, string cText1) => Marshal.PtrToStringAnsi(_Test1(procName, cText1));
    }
}

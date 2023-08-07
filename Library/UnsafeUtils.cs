using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HarbourDBF.NET
{
    internal class UnsafeUtils
    {
        [DllImport(Constants.DllName, EntryPoint = "DBF_UNSAFE_FREE", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr UnsafeFree(IntPtr target);

        // Идея взята отсюда: https://stackoverflow.com/a/42503844
        public static unsafe string GetString(IntPtr target, Encoding encoding = null)
        {
            if (target == IntPtr.Zero) return string.Empty;
            encoding = encoding ?? Constants.DefaultEncoding;
            var raw = (byte*)target.ToPointer();
            int length = 0;
            while (raw[length] != 0) length++;
            var message = encoding.GetString(raw, length);
            UnsafeFree(target);
            return message;
        }
    }
}

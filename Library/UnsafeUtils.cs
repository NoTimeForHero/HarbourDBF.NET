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
        [DllImport(Constants.DllName, EntryPoint = "DBF_UNSAFE_FREE", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr UnsafeFree(IntPtr target);

        // Идея взята отсюда: https://stackoverflow.com/a/42503844

        // TODO: Использовать unsafe fixed вместо Marshal.ReadByte для производительности?
        public static string GetString(IntPtr target, Encoding encoding = null)
        {
            encoding = encoding ?? Constants.DefaultEncoding;
            int length = 0;
            if (target != IntPtr.Zero)
                while (Marshal.ReadByte(target + length) != 0)
                    length++;
            byte[] managedArray = new byte[length];
            if (length > 0) Marshal.Copy(target, managedArray, 0, length);
            var message = encoding.GetString(managedArray);
            UnsafeFree(target);
            return message;
        }
    }
}

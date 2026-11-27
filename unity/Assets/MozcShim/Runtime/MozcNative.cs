using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MozcShim
{
    /// <summary>P/Invoke bindings for libmozc_shim (see native/mozc_shim.h).</summary>
    internal static class MozcNative
    {
        private const string Library = "mozc_shim";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mozc_shim_initialize(byte[] profileDir, byte[] dataFilePath);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mozc_shim_eval(byte[] commandJson);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mozc_shim_free(IntPtr str);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mozc_shim_data_version();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mozc_shim_last_error();

        public static bool Initialize(string profileDir, string dataFilePath) =>
            mozc_shim_initialize(Utf8(profileDir), Utf8(dataFilePath)) != 0;

        /// <returns>Response JSON, or null on failure (see <see cref="LastError"/>).</returns>
        public static string Eval(string commandJson)
        {
            IntPtr result = mozc_shim_eval(Utf8(commandJson));
            if (result == IntPtr.Zero) return null;
            try { return FromUtf8(result); }
            finally { mozc_shim_free(result); }
        }

        public static string DataVersion => FromUtf8(mozc_shim_data_version());
        public static string LastError => FromUtf8(mozc_shim_last_error());

        private static byte[] Utf8(string s)
        {
            if (s == null) return null;
            int length = Encoding.UTF8.GetByteCount(s);
            var bytes = new byte[length + 1]; // Null-terminated.
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        private static string FromUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            int length = 0;
            while (Marshal.ReadByte(p, length) != 0) length++;
            var bytes = new byte[length];
            Marshal.Copy(p, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}

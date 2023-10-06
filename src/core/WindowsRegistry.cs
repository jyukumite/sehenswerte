using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace SehensWerte.Utils
{
    public class WindowsRegistry
    {
        private static string? Key()
        {
            string? filename = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            string? registryKey = filename == null ? null : $@"HKEY_CURRENT_USER\Software\{System.IO.Path.GetFileNameWithoutExtension(filename)}";
            return registryKey;
        }

        public static bool Read<T>(string key, out T? result)
        {
            string? registryKey = Key();
            string? value = registryKey == null ? null : Registry.GetValue(registryKey, key, null) as string;
            if (value == null)
            {
                result = default(T);
                return false;
            }
            else
            {
                result = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
        }

        public static void Write(string key, double value)
        {
            string? registryKey = Key();
            if (registryKey != null)
            {
                Registry.SetValue(registryKey, key, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void Write<T>(string key, T value)
        {
            string? registryKey = Key();
            if (registryKey != null)
            {
                Registry.SetValue(registryKey, key, value.ToString());
            }
        }
    }
}

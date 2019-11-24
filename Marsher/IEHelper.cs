using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Marsher
{
    internal static class IEHelper
    {
        public static void EnsureBrowserEmulationEnabled(string exename, bool uninstall = false)
        {
            var current = GetInternetExplorerMajorVersion();
            if (current < 11) throw new IeVersionTooOldException();

            var rk = Registry.CurrentUser.OpenSubKey(
                         @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true) ??
                     Registry.CurrentUser.CreateSubKey(
                         @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);
            if (!uninstall)
            {
                dynamic value = rk.GetValue(exename);
                if (value == null)
                    rk.SetValue(exename, (uint)0x2AF9, RegistryValueKind.DWord); // Use IE11
            }
            else
                rk.DeleteValue(exename);
            rk.Dispose();
        }

        public static int GetInternetExplorerMajorVersion()
        {
            var result = -1;
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Internet Explorer");
                if (key == null) return -1;
                var value = key.GetValue("svcVersion", null) ?? key.GetValue("Version", null);
                if (value == null) return -1;

                string version = value.ToString();
                int separator = version.IndexOf('.');
                int.TryParse(version.Substring(0, separator), out result);
            }
            catch (SecurityException)
            {
                // The user does not have the permissions required to read from the registry key.
            }
            catch (UnauthorizedAccessException)
            {
                // The user does not have the necessary registry rights.
            }

            return result;
        }
    }

    internal class IeVersionTooOldException : Exception
    {

    }

    // taken from https://stackoverflow.com/questions/912741/how-to-delete-cookies-from-windows-form
    public static class WinInetHelper
    {
        public static bool SuppressCookiePersist()
        {
            // 3 = INTERNET_SUPPRESS_COOKIE_PERSIST
            // 81 = INTERNET_OPTION_SUPPRESS_BEHAVIOR
            return SetOption(81, 3);
        }

        public static bool EndBrowserSession()
        {
            // 42 = INTERNET_OPTION_END_BROWSER_SESSION
            return SetOption(42, null);
        }

        private static bool SetOption(int settingCode, int? option)
        {
            var optionPtr = IntPtr.Zero;
            var size = 0;
            if (option.HasValue)
            {
                size = sizeof(int);
                optionPtr = Marshal.AllocCoTaskMem(size);
                Marshal.WriteInt32(optionPtr, option.Value);
            }

            var success = InternetSetOption(0, settingCode, optionPtr, size);

            if (optionPtr != IntPtr.Zero) Marshal.Release(optionPtr);
            return success;
        }

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InternetSetOption(
            int hInternet,
            int dwOption,
            IntPtr lpBuffer,
            int dwBufferLength
        );
    }
}

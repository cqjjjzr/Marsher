using System;
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

            using (
                var rk = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true)
            )
            {
                if (rk == null) throw new NotSupportedException("Couldn't change IE emulation mode!!!");
                if (!uninstall)
                {
                    dynamic value = rk.GetValue(exename);
                    if (value == null)
                        rk.SetValue(exename, (uint)0x2AF9, RegistryValueKind.DWord); // Use IE11
                }
                else
                    rk.DeleteValue(exename);
            }
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
}

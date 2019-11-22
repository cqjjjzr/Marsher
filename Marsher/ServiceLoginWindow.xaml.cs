using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Marsher
{
    /// <summary>
    /// ServiceLoginWindow.xaml 的交互逻辑
    /// </summary>
    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public partial class ServiceLoginWindow
    {
        public CookieContainer ResultContainer = null;

        private Uri _cookiesUri;

        public ServiceLoginWindow()
        {
            InitializeComponent();
        }

        public void Initialize(Uri browserUri, Uri cookiesUri, string title)
        {
            WinInetHelper.SuppressCookiePersist();
            LoginBrowser.Navigate(browserUri);
            HideScriptErrors(LoginBrowser, true);
            _cookiesUri = cookiesUri;
            Title = title;
        }

        // taken from https://stackoverflow.com/questions/1298255/how-do-i-suppress-script-errors-when-using-the-wpf-webbrowser-control
        private void HideScriptErrors(WebBrowser wb, bool hide)
        {
            var fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiComWebBrowser == null) return;
            var objComWebBrowser = fiComWebBrowser.GetValue(wb);
            objComWebBrowser?.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, objComWebBrowser, new object[] { hide });
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            ResultContainer = GetUriCookieContainer(_cookiesUri);
            LoginBrowser.Navigate("about:blank");
            WinInetHelper.EndBrowserSession();
            Close();
        }

        private static CookieContainer GetUriCookieContainer(Uri uri)
        {
            // Determine the size of the cookie
            var dataSize = 8192 * 16;
            var cookieData = new StringBuilder(dataSize);
            if (!InternetGetCookieEx(
                uri.ToString(),
                null,
                cookieData,
                ref dataSize,
                InternetCookieHttpOnly,
                IntPtr.Zero))
            {
                if (dataSize < 0)
                    return null;
                // Allocate stringbuilder large enough to hold the cookie
                cookieData = new StringBuilder(dataSize);
                if (!InternetGetCookieEx(
                    uri.ToString(),
                    null, cookieData,
                    ref dataSize,
                    InternetCookieHttpOnly,
                    IntPtr.Zero))
                    return null;
            }

            if (cookieData.Length <= 0) return null;
            var cookies = new CookieContainer();
            cookies.SetCookies(uri, cookieData.ToString().Replace(';', ','));
            return cookies;
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetGetCookieEx(
            string url,
            string cookieName,
            StringBuilder cookieData,
            ref int size,
            Int32 dwFlags,
            IntPtr lpReserved);

        private const Int32 InternetCookieHttpOnly = 0x2000;
    }
}

using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Marsher
{
    /// <summary>
    /// ServiceLoginWindow.xaml 的交互逻辑
    /// </summary>
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
            LoginBrowser.Navigate(browserUri);
            _cookiesUri = cookiesUri;
            Title = title;
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            ResultContainer = GetUriCookieContainer(_cookiesUri);
            LoginBrowser.Navigate("about:blank");
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

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InternetSetCookieEx(
            string lpszUrlName,
            string lpszCookieName,
            string lpszCookieData,
            uint dwFlags,
            IntPtr dwReserved);

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

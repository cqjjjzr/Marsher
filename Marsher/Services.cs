using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.XPath;
using HtmlAgilityPack;
using WebSocketSharp;

namespace Marsher
{
    public abstract class Service
    {
        protected readonly string HttpAccept =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";

        protected readonly string HttpAcceptEncoding = "UTF-8";

        protected readonly string HttpUserAgent =
            $"Marsher {Assembly.GetExecutingAssembly().GetName().Version}: a marshmallow client for livestreamers.";
        private const string SessionFolder = "sessions";

        protected CookieContainer _container = new CookieContainer();

        protected Service()
        {
            LoadCookie();
        }

        public void UpdateCookie(CookieContainer container)
        {
            _container = container;
            SaveCookie();
            OnCookieUpdated();
        }

        public void ClearCookie()
        {
            _container = new CookieContainer();
            SaveCookie();
            OnCookieUpdated();
        }

        protected void SaveCookie()
        {
            if (!Directory.Exists(SessionFolder))
                Directory.CreateDirectory(SessionFolder);

            using (Stream stream = File.Create(Path.Combine(SessionFolder, $"{GetType().Name}.session")))
                new BinaryFormatter().Serialize(stream, _container);
        }

        protected void LoadCookie()
        {
            var path = Path.Combine(SessionFolder, $"{GetType().Name}.session");
            CookieContainer container;
            if (File.Exists(path))
                using (Stream stream = File.OpenRead(path))
                    container = (CookieContainer) new BinaryFormatter().Deserialize(stream);
            else
                container = new CookieContainer();
            UpdateCookie(container);
        }

        protected HttpWebRequest CreateWebRequest(string uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = HttpUserAgent;
            request.Accept = HttpAccept;
            request.CookieContainer = _container;
            return request;
        }

        protected void FireOnLoginStatusChanged(ServiceStatus status)
        {
            OnLoginStatusChanged?.Invoke(this, status);
        }

        protected void CheckLoginStatusAndFailOnRedirect(string url)
        {
            var request = CreateWebRequest(url);
            request.AllowAutoRedirect = false;
            request.GetResponseAsync().ContinueWith(task =>
            {
                if (task.IsFaulted) FireOnLoginStatusChanged(ServiceStatus.Error);
                else
                {
                    var resp = (HttpWebResponse)task.Result;
                    if (resp.StatusCode != HttpStatusCode.OK)
                        FireOnLoginStatusChanged(ServiceStatus.NotLoggedIn);
                    else
                        FireOnLoginStatusChanged(ServiceStatus.Available);
                }
            });
        }

        protected abstract void OnCookieUpdated();
        public abstract void Fetch(Func<IEnumerable<QaItem>, bool> update);

        public event Action<Service, ServiceStatus> OnLoginStatusChanged;
    }

    public enum ServiceStatus
    {
        Available, NotLoggedIn, Error, Unknown
    }

    public class MarshmallowService : Service
    {
        private readonly XPathExpression _findAllMarshmallowsExpression
            = XPathExpression.Compile("//li[contains(@class, 'list-group-item') and not(@id='sample-message') and not(contains(@class, 'tip'))]//a[contains(@data-target, 'message.content')]");
        private readonly XPathExpression _findLoadNextPageExpression = XPathExpression.Compile("//a[contains(@class, 'load-more')]");
        private readonly Regex _extractIdRegex = new Regex("/messages/([a-zA-Z0-9\\-]+)");

        protected override void OnCookieUpdated()
        {
            CheckLoginStatusAndFailOnRedirect("https://marshmallow-qa.com/messages/personal");
        }

        public override void Fetch(Func<IEnumerable<QaItem>, bool> update)
        {
            var nextUri = "https://marshmallow-qa.com/messages/personal";
            while (true)
            {
                var req = CreateWebRequest(nextUri);
                req.AllowAutoRedirect = false;
                try
                {
                    var resp = (HttpWebResponse) req.GetResponse();
                    if (resp.StatusCode != HttpStatusCode.OK)
                    {
                        FireOnLoginStatusChanged(ServiceStatus.NotLoggedIn);
                        break;
                    }

                    Encoding encoding;
                    try
                    {
                        encoding = Encoding.GetEncoding(resp.ContentEncoding);
                    }
                    catch (ArgumentException)
                    {
                        encoding = Encoding.UTF8;
                    }
                    var doc = new HtmlDocument();
                    doc.Load(resp.GetResponseStream(), encoding);

                    var nodes = doc.DocumentNode.SelectNodes(_findAllMarshmallowsExpression);
                    var items = (
                        from node in nodes
                        let href = node.GetAttributeValue("href", "null")
                        where !node.InnerText.IsNullOrEmpty() && href != "null"
                        let idMatch = _extractIdRegex.Match(href)
                        where idMatch.Success
                        let id = idMatch.Groups[1].Captures[0].Value
                        select new QaItem {Id = id, Content = node.InnerText, Service = QaService.Marshmallow}).ToList();

                    if (!update(items)) break;

                    var nextPageNodes = doc.DocumentNode.SelectNodes(_findLoadNextPageExpression);
                    if (nextPageNodes == null || nextPageNodes.Count == 0) break;
                    var nextPageNode = nextPageNodes[0];
                    var nextUriRelative = nextPageNode.GetAttributeValue("href", "null");
                    if (nextUriRelative == "null") return;
                    nextUri = "https://marshmallow-qa.com" + nextUriRelative;
                }
                catch (Exception)
                {
                    FireOnLoginStatusChanged(ServiceStatus.Error);
                    break;
                }
            }
            SaveCookie();
        }
    }

    public class PeingService : Service
    {
        protected override void OnCookieUpdated()
        {
            CheckLoginStatusAndFailOnRedirect("https://peing.net/ja/stg");
        }

        public override void Fetch(Func<IEnumerable<QaItem>, bool> update)
        {
            //throw new NotImplementedException();
        }
    }
}

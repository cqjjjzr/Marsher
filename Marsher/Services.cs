using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Marsher
{
    public abstract class Service
    {
        protected readonly string HttpAccept =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";

        protected readonly string HttpUserAgent =
            $"Marsher {Assembly.GetExecutingAssembly().GetName().Version}: a marshmallow client for livestreamers.";
        private const string SessionFolder = "sessions";

        protected CookieContainer _container = new CookieContainer();

        protected Service()
        {
            LoadCookie();
            OnCookieUpdated();
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
            var request = (HttpWebRequest)WebRequest.Create("https://marshmallow-qa.com/messages/personal");
            request.UserAgent = HttpUserAgent;
            request.Accept = HttpAccept;
            request.CookieContainer = _container;
            return request;
        }

        protected void FireOnLoginStatusChanged(ServiceStatus status)
        {
            OnLoginStatusChanged?.Invoke(status);
        }

        protected abstract void OnCookieUpdated();
        public abstract void Fetch(Func<IEnumerable<QaItem>, bool> update);

        public event Action<ServiceStatus> OnLoginStatusChanged;
    }

    public enum ServiceStatus
    {
        Available, NotLoggedIn, Error, Unknown
    }

    public class MarshmallowService : Service
    {
        protected override void OnCookieUpdated()
        {
            var request = CreateWebRequest("https://marshmallow-qa.com/messages/personal");
            request.AllowAutoRedirect = false;
            request.GetResponseAsync().ContinueWith(task =>
            {
                if (task.IsFaulted) FireOnLoginStatusChanged(ServiceStatus.Error);
                else
                {
                    var resp = (HttpWebResponse)task.Result;
                    if (resp.StatusCode == HttpStatusCode.Moved ||
                        resp.StatusCode == HttpStatusCode.MovedPermanently)
                        FireOnLoginStatusChanged(ServiceStatus.NotLoggedIn);
                    FireOnLoginStatusChanged(ServiceStatus.Available);
                }
            });
        }

        public override void Fetch(Func<IEnumerable<QaItem>, bool> update)
        {
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(1000);
                var l = new List<QaItem>();
                for (int j = 0; j < 10; j++)
                {
                    l.Add(new QaItem()
                    {
                        Content = Guid.NewGuid().ToString(), Id = Guid.NewGuid().ToString(), Service = QaService.Marshmallow
                    });
                }

                if (!update(l)) break;
            }
            SaveCookie();
        }
    }

    public class PeingService : Service
    {
        protected override void OnCookieUpdated()
        {
            //throw new NotImplementedException();
        }

        public override void Fetch(Func<IEnumerable<QaItem>, bool> update)
        {
            //throw new NotImplementedException();
        }
    }
}

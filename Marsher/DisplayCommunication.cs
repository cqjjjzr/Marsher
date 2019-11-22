using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace Marsher
{
    internal class DisplayCommunication
    {
        public const int DisplayWSPort = 19100;

        private readonly HttpServer _wsServer;
        internal JObject _lastMessage = new JObject { ["type"] = (int) QaService.Marshmallow, ["text"] = "" };

        public DisplayCommunication()
        {
            _wsServer = new HttpServer(DisplayWSPort);
            _wsServer.AddWebSocketService("/display", () => new MarsherWS(this));

            _wsServer.OnGet += (sender, e) => {
                var req = e.Request;
                var res = e.Response;

                var path = req.Url.AbsolutePath;
                if (path.StartsWith("/"))
                    path = path.Substring(1);
                if (path == "/")
                    path += "index.html";
                path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "resources", path));
                if (!path.StartsWith(Directory.GetCurrentDirectory()))
                {
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                byte[] contents;
                try
                {
                    contents = File.ReadAllBytes(path);
                    if (path.EndsWith(".html"))
                    {
                        res.ContentType = "text/html";
                        res.ContentEncoding = Encoding.UTF8;
                    }
                    else if (path.EndsWith(".js"))
                    {
                        res.ContentType = "application/javascript";
                        res.ContentEncoding = Encoding.UTF8;
                    }

                    res.WriteContent(contents);
                } catch(Exception) {
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                }
            };
        }

        public void Start()
        {
            _wsServer.Start();
            if (!_wsServer.IsListening) throw new IOException("Server not started.");
        }

        public void Stop()
        {
            _wsServer.Stop();
        }

        public void UpdateText(QaItem item, Action onCompleted)
        {
            var txtObj = new JObject {["type"] = (int) item.Service, ["text"] = item.Content};
            _wsServer.WebSocketServices.BroadcastAsync(txtObj.ToString(), onCompleted);
            _lastMessage = txtObj;
        }

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        internal void FireConnected()
        {
            OnConnected?.Invoke();
        }

        internal void FireOnDisconnected()
        {
            OnDisconnected?.Invoke();
        }

        internal void FireOnError(string message)
        {
            OnError?.Invoke(message);
        }
    }

    internal class MarsherWS : WebSocketBehavior
    {
        private DisplayCommunication _comm;

        internal MarsherWS(DisplayCommunication comm)
        {
            _comm = comm;
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            _comm.FireConnected();
            SendAsync(_comm._lastMessage.ToString(), (b => { }));
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            _comm.FireOnDisconnected();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            _comm.FireOnError(e.Exception + "\n" + e.Message);
        }
    }
}
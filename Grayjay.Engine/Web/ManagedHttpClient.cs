using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using static Grayjay.Engine.Packages.PackageHttp;

using HttpHeaders = Grayjay.Engine.Models.HttpHeaders;

namespace Grayjay.Engine.Web
{
    public class ManagedHttpClient
    {
        private CookieContainer _container = new CookieContainer();
        private bool _cookies = true;
        public bool UseCookies
        {
            get
            {
                return _cookies;
            }
            set
            {
                _cookies = value;
                _handler.UseCookies = value;
            }
        }

        private HttpClientHandler _handler;
        private HttpClient _client;

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; rv:91.0) Gecko/20100101 Firefox/91.0";


        public ManagedHttpClient() {
            _handler = new HttpClientHandler()
            {
                CookieContainer = _container,
                UseCookies = UseCookies,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _client = new HttpClient(_handler);
        }

        public virtual void BeforeRequest(HttpRequestMessage request)
        {

        }
        public virtual void AfterRequest(HttpResponseMessage response)
        {

        }


        public Response Request(string method, string url, HttpHeaders headers) => Request(method, url, null, headers);

        public Response Request(string method, string url, object body, HttpHeaders headers)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    throw new ArgumentException("No url provided");
                if (string.IsNullOrEmpty(method))
                    throw new ArgumentException("No method provided");


                headers = headers ?? new HttpHeaders();

                /*
                HttpWebRequest req = WebRequest.CreateHttp(url);
                if (UseCookies)
                    req.CookieContainer = _container;
                req.Method = method;
                foreach (var header in headers)
                    req.Headers.Add(header.Key, header.Value);

                BeforeRequest(req);

                if (body != null)
                {
                    if (body is string)
                        using (StreamWriter writer = new StreamWriter(req.GetRequestStream()))
                            writer.Write(body);
                    else if (body is byte[])
                        req.GetRequestStream().Write((byte[])body);
                    else
                        throw new NotImplementedException("Unsupported http body type: " + (body?.GetType()?.ToString() ?? ""));
                }

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                */
                var req = new HttpRequestMessage(new HttpMethod(method), url);
                req.Version = HttpVersion.Version11;
                req.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

                foreach (var header in headers)
                    req.Headers.TryAddWithoutValidation(header.Key, header.Value);

                if (!headers.Any(x => x.Key.ToLower() == "user-agent"))
                    req.Headers.Add("User-Agent", UserAgent);

                BeforeRequest(req);

                if (body != null)
                {
                    var contentType = headers.FirstOrDefault(x => x.Key.ToLower() == "content-type");
                    if (body is string bodyStr)
                    {
                        if (contentType.Value != null)
                        {
                            var contentTypeVal = contentType.Value;
                            if (contentTypeVal.Contains(";"))
                                contentTypeVal = contentType.Value.Substring(0, contentType.Value.IndexOf(";"));
                            req.Content = new StringContent(bodyStr, Encoding.UTF8, contentTypeVal);
                        }
                        else
                            req.Content = new StringContent(bodyStr, Encoding.UTF8);
                    }
                    else if (body is byte[] bodyBytes)
                    {
                        req.Content = new ByteArrayContent(bodyBytes);
                        if (contentType.Value != null)
                            req.Content.Headers.TryAddWithoutValidation("Content-Type", contentType.Value);
                    }
                    else throw new NotImplementedException("Unsupported http body type: " + (body?.GetType()?.ToString() ?? ""));
                }

                HttpResponseMessage resp = _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).Result;


                AfterRequest(resp);

                var hds = new HttpHeaders(resp.Headers);
                var respContentHeaders = new HttpHeaders(resp.Content.Headers);
                hds.MergeIfAbsentFrom(respContentHeaders);

                if (!resp.IsSuccessStatusCode)
                {
                    return new Response()
                    {
                        Code = (int)resp.StatusCode,
                        Headers = hds,
                        Body = new ResponseContent(resp.Content.ReadAsStreamAsync().Result),
                        Url = resp.RequestMessage.RequestUri?.ToString()
                    };
                }

                return new Response()
                {
                    Code = (int)resp.StatusCode,
                    Headers = hds,
                    Body = new ResponseContent(resp.Content.ReadAsStreamAsync().Result),
                    Url = resp.RequestMessage.RequestUri?.ToString()
                };
            }
            catch(WebException ex)
            {
                if (ex.Response == null)
                    throw;
                var httpResponse = (HttpWebResponse)ex.Response;
                return new Response()
                {
                    Code = (int)httpResponse.StatusCode,
                    Headers = new HttpHeaders(httpResponse.Headers),
                    Body = new ResponseContent(httpResponse.GetResponseStream()),
                    Url = httpResponse.ResponseUri?.ToString()
                };
            }
        }


        public Response TryHead(string url)
        {
            return Request("HEAD", url, new HttpHeaders());
        }


        public Response GET(string url, HttpHeaders headers) => Request("GET", url, headers);
        public Response POST(string url, string body, HttpHeaders headers) => Request("POST", url, body, headers);

        public SocketObject Socket(string url, HttpHeaders headers = null, SocketObject.Handlers handlers = null)
        {
            var socket = new SocketObject(url, headers, handlers);
            socket.Connect();
            return socket;
        }
        public class SocketObject
        {
            private string _url;
            private HttpHeaders _headers = null;
            private SocketObject.Handlers _handlers;

            private ClientWebSocket _socket = null;

            public SocketObject(string url, HttpHeaders headers = null, SocketObject.Handlers handlers = null)
            {
                _handlers = handlers ?? new Handlers();
                _url = url;
                _headers = headers;
                _socket = new ClientWebSocket();
            }
            
            public void Connect()
            {
                foreach (var kv in _headers)
                    _socket.Options.SetRequestHeader(kv.Key, kv.Value);

                Task.Run(async () =>
                {
                    try
                    {
                        await _socket.ConnectAsync(new Uri(_url), CancellationToken.None);
                        _handlers?.Open();

                        var buffer = new byte[4096];
                        while (_socket.State == WebSocketState.Open)
                        {
                            var result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                _handlers?.Closing();
                                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                                _handlers?.Closed();
                            }
                            else
                            {
                                if (result.MessageType == WebSocketMessageType.Text)
                                    _handlers?.Message(Encoding.UTF8.GetString(buffer, 0, result.Count));
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        _handlers?.Failure(ex);
                    }
                    _socket.Dispose();
                });
            }
            
            public void Send(string msg)
            {
                _socket.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            public void Close(int code, string reason)
            {
                _socket.CloseAsync((WebSocketCloseStatus)code, reason, CancellationToken.None);
            }

            public class Handlers
            {
                public event Action OnOpen;
                public event Action<string> OnMessage;
                public event Action OnClosing;
                public event Action OnClosed;
                public event Action<Exception> OnFailure;

                public void Open() => OnOpen?.Invoke();
                public void Message(string msg) => OnMessage?.Invoke(msg);
                public void Closing() => OnClosing?.Invoke();
                public void Closed() => OnClosed?.Invoke();
                public void Failure(Exception ex) => OnFailure?.Invoke(ex);
            }
        }

        public class Response
        {
            public bool IsOk => Code >= 200 && Code < 300;
            public int Code { get; set; }
            public string Url { get; set; }
            public HttpHeaders Headers { get; set; } = new HttpHeaders();
            public int ContentLength => Headers.TryGetFirst("content-length", out var cls) ? int.Parse(cls!) : 0;
            public ResponseContent Body { get; set; }
        }

        public class ResponseContent
        {
            private Stream _stream = null;

            private byte[] _bytes = null;
            private string _string = null;

            public long Length => _stream?.Length ?? 0;

            public ResponseContent(Stream stream)
            {
                _stream = stream;
            }


            public byte[] AsBytes()
            {
                if (_bytes != null)
                    return _bytes;
                using (MemoryStream str = new MemoryStream())
                {
                    _stream.CopyTo(str);
                    _bytes = str.ToArray();
                }
                _stream.Dispose();
                return _bytes;
            }
            public string AsString()
            {
                if (_string != null)
                    return _string;
                using (StreamReader reader = new StreamReader(_stream))
                {
                    _string = reader.ReadToEnd();
                }
                _stream.Dispose();
                return _string;
            }
            public Stream AsStream()
            {
                return _stream;
            }
        }
    }
}

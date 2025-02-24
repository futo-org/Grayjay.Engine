using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static Grayjay.Engine.Packages.PackageHttp;

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


        public Response Request(string method, string url, Dictionary<string, string> headers) => Request(method, url, null, headers);

        public Response Request(string method, string url, object body, Dictionary<string, string> headers)
        {
            try
            {
                headers = headers ?? new Dictionary<string, string>();

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
                req.Version = HttpVersion.Version20;
                
                foreach (var header in headers)
                    req.Headers.TryAddWithoutValidation(header.Key, header.Value);

                BeforeRequest(req);

                if(body != null)
                {
                    var contentType = headers.FirstOrDefault(x => x.Key.ToLower() == "content-type");
                    if (body is string bodyStr)
                    {
                        if (contentType.Value != null)
                            req.Content = new StringContent(bodyStr, Encoding.UTF8, contentType.Value);
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

                Task<HttpResponseMessage> respTask = _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                //TODO: Determine if there is a more optimal way to keep this context synchronous. For now this is the safest.
                //respTask.Wait();
                HttpResponseMessage resp = respTask.GetAwaiter().GetResult();//respTask.Result;

                var hds = resp.Headers.ToDictionary(x => x.Key.ToLower(), y => string.Join(", ", y.Value));
                foreach(var header in resp.Content.Headers)
                {
                    if (!hds.ContainsKey(header.Key))
                        hds.Add(header.Key.ToLower(), string.Join(", ", header.Value));
                }

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
                    Headers = httpResponse.Headers.AllKeys.ToDictionary(x => x.ToLower(), y => httpResponse.Headers[y]),
                    Body = new ResponseContent(httpResponse.GetResponseStream()),
                    Url = httpResponse.ResponseUri?.ToString()
                };
            }
        }


        public Response TryHead(string url)
        {
            return Request("HEAD", url, new Dictionary<string, string>());
        }


        public Response GET(string url, Dictionary<string, string> headers) => Request("GET", url, headers);
        public Response POST(string url, string body, Dictionary<string, string> headers) => Request("POST", url, body, headers);



        public class Response
        {
            public bool IsOk => Code >= 200 && Code < 300;
            public int Code { get; set; }
            public string Url { get; set; }
            public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
            public int ContentLength => (Headers.ContainsKey("content-length") ? int.Parse(Headers["content-length"]) : 0);
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

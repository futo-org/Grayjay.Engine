using Grayjay.Engine.Models;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.Web;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using static Grayjay.Engine.Extensions;
using static Grayjay.Engine.Packages.PackageHttp;
using static Grayjay.Engine.Packages.PackageHttp.PackageHttpClient;
using static Grayjay.Engine.Web.ManagedHttpClient;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public class PackageHttp: Package
    {
        public override string VariableName => "http";

        private static string[] WHITELISTED_RESPONSE_HEADERS = new string[]
        {
            "content-type", "date", "content-length", "last-modified", "etag", "cache-control", "content-encoding", "content-disposition", "connection"
        };


        private PackageHttpClient _client;
        private PackageHttpClient _clientAuth;

        public string ClientID { get; } = Guid.NewGuid().ToString();

        private bool DoUpdateCookies { get; set; } = true;
        public bool DoApplyCookies { get; set; } = true;
        public bool DoAllowNewCookies { get; set; } = true;


        private Dictionary<string, Dictionary<string, string>> _currentCookieMap = null;
        private Dictionary<string, Dictionary<string, string>> _otherCookieMap = null;

        protected List<SocketResult> aliveSockets = new List<SocketResult>();
        protected bool _cleanedUp = false;


        public PackageHttp(GrayjayPlugin plugin) : base(plugin)
        {
            _client = new PackageHttpClient(this, plugin.HttpClient);
            _clientAuth = new PackageHttpClient(this, plugin.HttpClientAuth);
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject("http", this);
        }



        [ScriptMember("batch")]
        public HttpBatchBuilder Batch()
        {
            return new HttpBatchBuilder(this);
        }

        [ScriptMember("getDefaultClient")]
        public PackageHttpClient GetDefaultClient(bool withAuth)
        {
            return (withAuth) ? _clientAuth : _client;
        }

        [ScriptMember("request")]
        public object Request(string method, string url, ScriptObject headers = null, bool useAuth = false, bool useByteResponses = false)
        {
            return (useAuth) ?
                _clientAuth.Request(method, url, headers, useByteResponses) :
                _client.Request(method, url, headers, useByteResponses);
        }
        [ScriptMember("requestWithBody")]
        public object RequestWithBody(string method, string url, object body, ScriptObject headers = null, bool useAuth = false, bool useByteResponses = false)
        {
            return (useAuth) ?
                _clientAuth.RequestWithBody(method, url, body, headers, useByteResponses) :
                _client.RequestWithBody(method, url, body, headers, useByteResponses);
        }



        [ScriptMember]
        public object GET(string url, ScriptObject headers, bool auth = false, bool useByteResponses = false)
        {
            return (auth) ?
                _clientAuth.GET(url, headers, auth, useByteResponses) :
                _client.GET(url, headers, auth, useByteResponses);
        }
        [ScriptMember]
        public object POST(string url, object body, IScriptObject headers, bool auth = false, bool useByteResponses = false)
        {
            return (auth) ?
                _clientAuth.POST(url, body, headers, auth, useByteResponses) :
                _client.POST(url, body, headers, auth, useByteResponses);
        }

        [ScriptMember("socket")]
        public SocketResult Socket(string url, ScriptObject headers = null, bool useAuth = false)
        {
            return GetDefaultClient(useAuth).Socket(url, headers);
        }


        public HttpResponse RequestInternal(RequestDescriptor descriptor, PackageHttpClient client = null)
        {
            if (descriptor.Method == "DUMMY")
                return null;

            Stopwatch w = new Stopwatch();
            w.Start();
            if(client == null)
                client = descriptor.UseAuth ? _clientAuth : _client;
            ManagedHttpClient.Response resp = client.Client.Request(descriptor.Method, descriptor.Url, descriptor.Body, descriptor.Headers);
            HttpResponse result = (descriptor.ReturnType == ReturnType.Bytes) ?
                 (HttpResponse)new HttpJSBytesResponse(_plugin, resp.Code, resp.Body.AsBytes(), SanitizeResponseHeaders(resp.Headers, descriptor.UseAuth || !_plugin.Config.AllowAllHttpHeaderAccess).ToDictionaryList(), resp.Url) :
                 (HttpResponse)new HttpStringResponse(resp.Code, resp.Body.AsString(), SanitizeResponseHeaders(resp.Headers, descriptor.UseAuth || !_plugin.Config.AllowAllHttpHeaderAccess).ToDictionaryList(), resp.Url);

            w.Stop();
            if (Logger.WillLog(LogLevel.Debug))
                Logger.Debug<PackageHttp>($"PackageHttp.Request [{descriptor.Url}]({result.Code}) {w.ElapsedMilliseconds}ms");
            return result;
        }

        private static HttpHeaders SanitizeResponseHeaders(HttpHeaders headers, bool onlyWhitelisted = false)
        {
            HttpHeaders results = new HttpHeaders();
            if (onlyWhitelisted)
            {
                foreach (var header in headers)
                {
                    if (WHITELISTED_RESPONSE_HEADERS.Contains(header.Key.ToLower()))
                        results.Add(header.Key, header.Value);
                }
            }
            else
            {
                foreach (var h in headers)
                {
                    if (h.Key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!h.Value.Contains("httponly", StringComparison.InvariantCultureIgnoreCase))
                            results.Add(h.Key, h.Value);
                    }
                    else
                        results.Add(h.Key, h.Value);
                }
            }

            return results;
        }

        public override void Dispose()
        {

        }




        [NoDefaultScriptAccess]
        public class HttpResponse
        {
            [ScriptMember("code")]
            public int Code { get; set; }
            [ScriptMember("isOk")]
            public bool IsOk => Code >= 200 && Code < 300;
            public Dictionary<string, List<string>> Headers { get; private set; } = new Dictionary<string, List<string>>();
            [ScriptMember("headers")]
            public PropertyBag HeadersForScript { get; private set; }

            [ScriptMember("url")]
            public string Url { get; set; }

            [ScriptMember("body")]
            public virtual object Body { get; }

            public HttpResponse(int code, Dictionary<string, List<string>> headers = null, string url = null)
            {
                Code = code;
                Headers = headers ?? new Dictionary<string, List<string>>();
                HeadersForScript = new PropertyBag();
                foreach (var header in Headers)
                    HeadersForScript.Add(header.Key, header.Value);
                Url = url;
            }
        }
        [NoDefaultScriptAccess]
        public class HttpStringResponse: HttpResponse
        {
            public string BodyString { get; set; }
            public override object Body => BodyString;

            public HttpStringResponse(int code, string body = null, Dictionary<string, List<string>> headers = null, string url = null) : base(code, headers, url)
            {
                BodyString = body;
            }
        }
        [NoDefaultScriptAccess]
        public class HttpBytesResponse : HttpResponse
        {
            public byte[] BodyBytes { get; set; }
            public override object Body => BodyBytes;

            public HttpBytesResponse(int code, byte[] body = null, Dictionary<string, List<string>> headers = null, string url = null) : base(code, headers, url)
            {
                BodyBytes = body;
            }
        }
        [NoDefaultScriptAccess]
        public class HttpJSBytesResponse : HttpResponse
        {
            public IArrayBuffer BodyBytes { get; set; }
            public override object Body => BodyBytes;

            public HttpJSBytesResponse(GrayjayPlugin plugin, int code, byte[] body = null, Dictionary<string, List<string>> headers = null, string url = null) : base(code, headers, url)
            {
                //TODO: Can we do better than this?
                var buffer = plugin.GetUnderlyingEngine().Evaluate("new ArrayBuffer(" + body.Length.ToString() + ")");
                if (!(buffer is IArrayBuffer))
                    throw new InvalidDataException("Expected an ArrayBuffer, but got " + buffer?.GetType()?.ToString());
                if(body.Length != 0)
                    ((IArrayBuffer)buffer).WriteBytes(body, 0, (ulong)body.Length, 0);
                BodyBytes = (IArrayBuffer)buffer;
            }
        }

        [NoDefaultScriptAccess]
        public class HttpBatchBuilder
        {
            private PackageHttp _package;
            private List<RequestDescriptor> _descriptors = new List<RequestDescriptor>();

            public HttpBatchBuilder(PackageHttp package)
            {
                _package = package;
            }

            [ScriptMember("request")]
            public HttpBatchBuilder Request(string method, string url, ScriptObject headers = null, bool useAuth = false)
            {
                _descriptors.Add(new RequestDescriptor()
                {
                    Method = method,
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    UseAuth = useAuth
                });
                return this;
            }
            [ScriptMember("requestWithBody")]
            public HttpBatchBuilder RequestWithBody(string method, string url, object body, ScriptObject headers = null, bool useAuth = false)
            {
                _descriptors.Add(new RequestDescriptor()
                {
                    Method = method,
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    Body = body,
                    UseAuth = useAuth
                });
                return this;
            }

            [ScriptMember]
            public HttpBatchBuilder GET(string url, ScriptObject headers = null, bool useAuth = false)
            {
                return Request("GET", url, headers, useAuth);
            }
            [ScriptMember]
            public HttpBatchBuilder POST(string url, string body, ScriptObject headers = null, bool useAuth = false)
            {
                return RequestWithBody("POST", url, body, headers, useAuth);
            }
            [ScriptMember]
            public HttpBatchBuilder DUMMY()
            {
                _descriptors.Add(new RequestDescriptor()
                {
                    Method = "DUMMY"
                });
                return this;
            }

            [ScriptMember("execute")]
            public object Execute()
            {
                return _descriptors.AsParallel()
                    .AsOrdered()
                    .Select(x => _package.RequestInternal(x))
                    .ToScriptArray();
            }
        }



        public class RequestDescriptor
        {
            public string Method { get; set; }
            public string Url { get; set; }
            public HttpHeaders Headers { get; set; }
            public bool UseAuth { get; set; }
            public object Body { get; set; }
            public string ContentType { get; set; }

            public ReturnType ReturnType { get; set; }
        }

        public enum ReturnType
        {
            String = 1,
            Bytes = 2
        }



        public class PackageHttpClient
        {
            private PackageHttp _package;
            private PluginHttpClient _client;

            private HttpHeaders _defaultHeaders = new HttpHeaders();
            private string _clientId = null;

            [ScriptMember("clientId")]
            public string ClientID { get; }

            internal PluginHttpClient Client => _client;

            public PackageHttpClient(PackageHttp package, PluginHttpClient client)
            {
                _package = package;
                _client = client;
                _clientId = client.ClientID;
            }


            [ScriptMember("setDefaultHeaders")]
            public void SetDefaultHeaders(ScriptObject defaultHeaders)
            {
                foreach (var key in defaultHeaders.PropertyNames)
                {
                    _defaultHeaders[key] = (string)defaultHeaders[key];
                }
            }
            [ScriptMember("setDoApplyCookies")]
            public void SetDoApplyCookies(bool apply)
            {
                if (_client is PluginHttpClient pc)
                    pc.DoApplyCookies = apply;
            }
            [ScriptMember("setDoUpdateCookies")]
            public void SetDoUpdateCookies(bool update)
            {
                if (_client is PluginHttpClient pc)
                    pc.DoUpdateCookies = update;
            }
            [ScriptMember("setDoAllowNewCookies")]
            public void SetDoAllowNewCookies(bool allow)
            {
                if (_client is PluginHttpClient pc)
                    pc.DoAllowNewCookies = allow;
            }

            [ScriptMember("setTimeout")]
            public void SetTimeout(int timeoutMs)
            {
                //tODO: Timeout support
            }

            private void ApplyDefaultHeaders(ScriptObject headerMap)
            {
                lock (_defaultHeaders)
                {
                    foreach (var toApply in _defaultHeaders)
                    {
                        if (!headerMap.PropertyNames.Contains(toApply.Key))
                            headerMap[toApply.Key] = toApply.Value;
                    }
                }
            }

            [ScriptMember("request")]
            public object Request(string method, string url, ScriptObject headers = null, bool useByteResponses = false)
            {
                ApplyDefaultHeaders(headers);

                return _package.RequestInternal(new RequestDescriptor()
                {
                    Method = method,
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                }, this);
            }
            [ScriptMember("requestWithBody")]
            public object RequestWithBody(string method, string url, object body, ScriptObject headers = null, bool useByteResponses = false)
            {
                if (body is string)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = method,
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        Body = (string)body,
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                else if (body is ITypedArray tbody)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = method,
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        Body = tbody.ArrayBuffer.GetBytes(),
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                else if (body is JArray jabody)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = method,
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        Body = jabody.Select(x => (byte)((int)x)).ToArray(),
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                throw new NotImplementedException();
            }



            [ScriptMember]
            public object GET(string url, ScriptObject headers, bool auth = false, bool useByteResponses = false)
            {
                return _package.RequestInternal(new RequestDescriptor()
                {
                    Method = "GET",
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    UseAuth = auth,
                    ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                }, this);
            }
            [ScriptMember]
            public object POST(string url, object body, IScriptObject headers, bool auth = false, bool useByteResponses = false)
            {
                if (body is string)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = "POST",
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        UseAuth = auth,
                        Body = (string)body,
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                else if (body is ITypedArray tbody)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = "POST",
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        UseAuth = auth,
                        Body = tbody.ArrayBuffer.GetBytes(),
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                else if (body is JArray jabody)
                {
                    return _package.RequestInternal(new RequestDescriptor()
                    {
                        Method = "POST",
                        Url = url,
                        Headers = HttpHeaders.FromScriptObject(headers),
                        UseAuth = auth,
                        Body = jabody.Select(x => (byte)((int)x)).ToArray(),
                        ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                    }, this);
                }
                throw new NotImplementedException("Invalid HTTP Body (" + body?.GetType().Name + ")");
            }

            [ScriptMember("socket")]
            public SocketResult Socket(string url, ScriptObject headers = null)
            {
                ApplyDefaultHeaders(headers);
                var socket = new SocketResult(_package, this, _client, url, headers);
                Logger.Warning<PackageHttp>("PackageHttp Socket opened");
                lock (_package.aliveSockets)
                {
                    _package.aliveSockets.Add(socket);
                }
                return socket;
            }

        }


        [NoDefaultScriptAccess]
        public class SocketResult
        {
            private bool _isOpen = false;
            private ManagedHttpClient.SocketObject _socket = null;

            private IJavaScriptObject _listeners = null;

            private PackageHttp _package;
            private PackageHttpClient _packageClient;
            private PluginHttpClient _client;
            private string _url = null;
            private ScriptObject _headers = null;

            public SocketResult(PackageHttp parent, PackageHttpClient pack, PluginHttpClient client, string url, ScriptObject headers)
            {
                _packageClient = pack;
                _package = parent;
                _client = client;
                _url = url;
                _headers = headers;
            }

            [ScriptMember("isOpen")]
            public bool isOpen() => _isOpen;

            [ScriptMember("connect")]
            public void connect(IJavaScriptObject socketObj, object extraPara = null)
            {
                bool hasOpen = socketObj.HasFunction("open");
                bool hasMessage = socketObj.HasFunction("message");
                bool hasClosing = socketObj.HasFunction("closing");
                bool hasClosed = socketObj.HasFunction("closed");
                bool hasFailure = socketObj.HasFunction("failure");
                _listeners = socketObj;

                var client = _client;
                var handlers = new SocketObject.Handlers();
                if (hasOpen)
                    handlers.OnOpen += () =>
                    {
                        _isOpen = true;
                        socketObj.InvokeV8(_package._plugin.Config, "open");
                    };
                if (hasMessage)
                    handlers.OnMessage += (msg) => socketObj.InvokeV8(_package._plugin.Config, "message", msg);
                if (hasClosing)
                    handlers.OnClosing += () => socketObj.InvokeV8(_package._plugin.Config, "closing");
                if (hasClosed)
                    handlers.OnClosed += () =>
                    {
                        _isOpen = false;
                        socketObj.InvokeV8(_package._plugin.Config, "closed");
                    };
                if (hasFailure)
                    handlers.OnFailure += (ex) =>
                    {
                        _isOpen = false;
                        socketObj.InvokeV8(_package._plugin.Config, "failure", ex.Message);
                    };
                _socket = client.Socket(_url, new HttpHeaders(_headers.ToDictionary<string>()), handlers);
            }

            [ScriptMember("send")]
            public void send(string msg)
            {
                _socket?.Send(msg);
            }
            [ScriptMember("close")]
            public void close()
            {
                _socket?.Close(1000, "");
            }
            [ScriptMember("close")]
            public void close(int code, string reason = "")
            {
                _socket?.Close(code, reason);
            }
        }
    }
}

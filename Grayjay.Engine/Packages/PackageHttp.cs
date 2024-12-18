using Grayjay.Engine.Web;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using static Grayjay.Engine.Extensions;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public class PackageHttp: Package
    {
        public static bool LogRequests = true;

        private ManagedHttpClient _client;
        private ManagedHttpClient _clientAuth;

        public string ClientID { get; } = Guid.NewGuid().ToString();

        private bool DoUpdateCookies { get; set; } = true;
        public bool DoApplyCookies { get; set; } = true;
        public bool DoAllowNewCookies { get; set; } = true;


        private Dictionary<string, Dictionary<string, string>> _currentCookieMap = null;
        private Dictionary<string, Dictionary<string, string>> _otherCookieMap = null;


        public PackageHttp(GrayjayPlugin plugin) : base(plugin)
        {
            _client = plugin.HttpClient;
            _clientAuth = plugin.HttpClientAuth;
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject("http", this);
        }


        public override void Dispose()
        {

        }

        [ScriptMember("request")]
        public HttpResponse Request(RequestDescriptor descriptor)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            var client = (descriptor.UseAuth) ? _clientAuth : _client;
            ManagedHttpClient.Response resp = client.Request(descriptor.Method, descriptor.Url, descriptor.Body, descriptor.Headers);
            HttpResponse result = (descriptor.ReturnType == ReturnType.Bytes) ?
                 (HttpResponse)new HttpJSBytesResponse(_plugin, resp.Code, resp.Body.AsBytes(), new Dictionary<string, List<string>>(), resp.Url) :
                 (HttpResponse)new HttpStringResponse(resp.Code, resp.Body.AsString(), new Dictionary<string, List<string>>(), resp.Url);

            w.Stop();
            if(LogRequests)
                Console.WriteLine($"PackageHttp.Request [{descriptor.Url}]({result.Code}) {w.ElapsedMilliseconds}ms");
            return result;
        }
        [ScriptMember("batch")]
        public HttpBatchBuilder Batch()
        {
            return new HttpBatchBuilder(this);
        }


        [ScriptMember]
        public object GET(string url, ScriptObject headers, bool auth = false, bool useByteResponses = false)
        {
            return Request(new RequestDescriptor()
            {
                Method = "GET",
                Url = url,
                Headers = headers.ToDictionary<string>(),
                UseAuth = auth,
                ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
            });
        }
        [ScriptMember]
        public object POST(string url, object body, IScriptObject headers, bool auth = false, bool useByteResponses = false)
        {
            if (body is string)
                return Request(new RequestDescriptor()
                {
                    Method = "POST",
                    Url = url,
                    Headers = headers.ToDictionary<string>(),
                    UseAuth = auth,
                    Body = (string)body,
                    ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                });
            else if(body is ITypedArray tbody)
            {
                return Request(new RequestDescriptor()
                {
                    Method = "POST",
                    Url = url,
                    Headers = headers.ToDictionary<string>(),
                    UseAuth = auth,
                    Body = tbody.ArrayBuffer.GetBytes(),
                    ReturnType = (useByteResponses) ? ReturnType.Bytes : ReturnType.String
                });
            }
            throw new NotImplementedException();
        }



        [NoDefaultScriptAccess]
        public class HttpResponse
        {
            [ScriptMember("code")]
            public int Code { get; set; }
            [ScriptMember("isOk")]
            public bool IsOk => Code >= 200 && Code < 300;
            [ScriptMember("headers")]
            public Dictionary<string, List<string>> Headers { get; private set; } = new Dictionary<string, List<string>>();

            [ScriptMember("url")]
            public string Url { get; set; }

            [ScriptMember("body")]
            public virtual object Body { get; }

            public HttpResponse(int code, Dictionary<string, List<string>> headers = null, string url = null)
            {
                Code = code;
                Headers = headers ?? new Dictionary<string, List<string>>();
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
                    Headers = headers?.ToDictionary<string>(),
                    UseAuth = useAuth
                });
                return this;
            }
            [ScriptMember("requestWithBody")]
            public HttpBatchBuilder RequestWithBody(string method, string url, string body, ScriptObject headers = null, bool useAuth = false)
            {
                _descriptors.Add(new RequestDescriptor()
                {
                    Method = method,
                    Url = url,
                    Headers = headers?.ToDictionary<string>(),
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

            [ScriptMember("execute")]
            public object Execute()
            {
                return _descriptors.AsParallel()
                    .AsOrdered()
                    .Select(x => _package.Request(x))
                    .ToScriptArray();
            }
        }



        public class RequestDescriptor
        {
            public string Method { get; set; }
            public string Url { get; set; }
            public Dictionary<string, string> Headers { get; set; }
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
    }

    public class PackageHttpClient
    {
        private PackageHttp _package;
        private ManagedHttpClient _client;

        private Dictionary<string, string> _defaultHeaders;
        private string _clientId = null;

        [ScriptMember("clientId")]
        public string ClientID { get; }

        public PackageHttpClient(PackageHttp package, PluginHttpClient client)
        {
            _package = package;
            _client = client;
            _clientId = client.ClientID;
        }
    }
}

using Grayjay.Engine.Exceptions;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.Web;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using HttpHeaders = Grayjay.Engine.Models.HttpHeaders;
//TODO: Cookies

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public sealed class PackageHttpImp : Package, IDisposable
    {
        public override string VariableName => "httpimp";

        private static readonly string[] WHITELISTED_RESPONSE_HEADERS = new[]
        {
            "content-type","date","content-length","last-modified","etag","cache-control",
            "content-encoding","content-disposition","connection"
        };

        private readonly PackageHttpImpClient _client;
        private readonly PackageHttpImpClient _clientAuth;

        public string ClientID { get; } = Guid.NewGuid().ToString();

        public PackageHttpImp(GrayjayPlugin plugin) : base(plugin)
        {
            _client = new PackageHttpImpClient(this, withAuth: false);
            _clientAuth = new PackageHttpImpClient(this, withAuth: true);
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject(VariableName, this);
        }

        [ScriptMember("socket")]
        public object Socket(string url, ScriptObject headers = null, bool useAuth = false)
        {
            throw new NotSupportedException("WebSocket is not supported by the curl-impersonate HTTP implementation.");
        }

        [ScriptMember("batch")]
        public HttpBatchBuilder Batch()
        {
            return new HttpBatchBuilder(this);
        }

        [ScriptMember("getDefaultClient")]
        public PackageHttpImpClient GetDefaultClient(bool withAuth) => withAuth ? _clientAuth : _client;

        [ScriptMember("request")]
        public object Request(
            string method,
            string url,
            ScriptObject headers = null,
            bool useAuth = false,
            bool useByteResponses = false,
            ScriptObject options = null)
        {
            return (useAuth ? _clientAuth : _client).Request(method, url, headers, useByteResponses, options);
        }

        [ScriptMember("requestWithBody")]
        public object RequestWithBody(
            string method,
            string url,
            object body,
            ScriptObject headers = null,
            bool useAuth = false,
            bool useByteResponses = false,
            ScriptObject options = null)
        {
            return (useAuth ? _clientAuth : _client).RequestWithBody(method, url, body, headers, useByteResponses, options);
        }

        [ScriptMember]
        public object GET(
            string url,
            ScriptObject headers,
            bool auth = false,
            bool useByteResponses = false,
            ScriptObject options = null)
        {
            return Request("GET", url, headers, auth, useByteResponses, options);
        }

        [ScriptMember]
        public object POST(
            string url,
            object body,
            ScriptObject headers,
            bool auth = false,
            bool useByteResponses = false,
            ScriptObject options = null)
        {
            return RequestWithBody("POST", url, body, headers, auth, useByteResponses, options);
        }

        internal ImpResponse Perform(RequestDescriptor d, PackageHttpImpClient client = null)
        {
            if (!_plugin.Config.IsUrlAllowed(d.Url))
                throw new ScriptImplementationException(_plugin.Config, $"Attempted to access non-whitelisted url: {d.Url}\nAdd it to your config");

            client ??= d.UseAuth ? _clientAuth : _client;
            var target = d.ImpersonateTarget ?? client.DefaultImpersonateTarget;
            bool builtInHdrs = d.UseBuiltInHeaders ?? client.DefaultUseBuiltInHeaders;
            int timeoutMs = d.TimeoutMs > 0 ? d.TimeoutMs : client.DefaultTimeoutMs;

            byte[] bytes = d.Body switch
            {
                null => Array.Empty<byte>(),
                string s => Encoding.UTF8.GetBytes(s),
                ITypedArray a => a.ArrayBuffer.GetBytes(),
                JArray ja => ja.Select(x => (byte)(int)x).ToArray(),
                _ => throw new NotImplementedException("Unsupported body type " + d.Body?.GetType()?.Name)
            };

            var uri = new Uri(d.Url);
            var pluginClient = d.UseAuth ? _plugin.HttpClientAuth : _plugin.HttpClient;
            pluginClient.ApplyHeaders(uri, d.Headers, d.UseAuth, true);

            var req = new Libcurl.Request
            {
                Url = d.Url,
                Method = d.Method,
                Headers = d.Headers?.ToList() ?? new List<KeyValuePair<string, string>>(),
                Body = bytes,
                UseBuiltInHeaders = builtInHdrs,
                ImpersonateTarget = target,
                TimeoutMs = (timeoutMs > 0) ? timeoutMs : 30000,
                SendCookies = client.DoApplyCookies,
                PersistCookies = client.DoUpdateCookies && client.DoAllowNewCookies
            };

            var res = Libcurl.Perform(req);
            var resHeaders = new Models.HttpHeaders(res.Headers);
            pluginClient.ProcessRequest(d.Method, res.Status, uri, resHeaders);

            var sanitized = SanitizeResponseHeaders(resHeaders, onlyWhitelisted: client.UseAuth || !_plugin.Config.AllowAllHttpHeaderAccess);
            if (d.ReturnType == ReturnType.Bytes)
            {
                return new ImpJSBytesResponse(_plugin, res.Status, res.BodyBytes ?? Array.Empty<byte>(), sanitized.ToDictionaryList(), res.EffectiveUrl);
            }
            else
            {
                var text = res.BodyBytes != null ? Encoding.UTF8.GetString(res.BodyBytes) : null;
                return new ImpStringResponse(res.Status, text, sanitized.ToDictionaryList(), res.EffectiveUrl);
            }
        }

        private static HttpHeaders SanitizeResponseHeaders(HttpHeaders headers, bool onlyWhitelisted)
        {
            var results = new HttpHeaders();
            if (onlyWhitelisted)
            {
                foreach (var h in headers)
                    if (WHITELISTED_RESPONSE_HEADERS.Contains(h.Key.ToLower()))
                        results.Add(h.Key, h.Value);
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
        public class ImpResponse
        {
            [ScriptMember("code")] public int Code { get; set; }
            [ScriptMember("isOk")] public bool IsOk => Code >= 200 && Code < 300;

            public Dictionary<string, List<string>> Headers { get; private set; } = new();
            [ScriptMember("headers")] public PropertyBag HeadersForScript { get; private set; } = new();

            [ScriptMember("url")] public string Url { get; set; }
            [ScriptMember("body")] public virtual object Body { get; }

            public ImpResponse(int code, Dictionary<string, List<string>> headers = null, string url = null)
            {
                Code = code;
                Headers = headers ?? new Dictionary<string, List<string>>();
                HeadersForScript = new PropertyBag();
                foreach (var h in Headers) HeadersForScript.Add(h.Key, h.Value);
                Url = url;
            }
        }

        [NoDefaultScriptAccess]
        public sealed class ImpStringResponse : ImpResponse
        {
            public string BodyString { get; set; }
            public override object Body => BodyString;

            public ImpStringResponse(int code, string body = null, Dictionary<string, List<string>> headers = null, string url = null)
                : base(code, headers, url) => BodyString = body;
        }

        [NoDefaultScriptAccess]
        public sealed class ImpJSBytesResponse : ImpResponse
        {
            public IArrayBuffer BodyBytes { get; set; }
            public override object Body => BodyBytes;

            public ImpJSBytesResponse(GrayjayPlugin plugin, int code, byte[] body = null, Dictionary<string, List<string>> headers = null, string url = null)
                : base(code, headers, url)
            {
                var buffer = plugin.GetUnderlyingEngine().Evaluate("new ArrayBuffer(" + (body?.Length ?? 0) + ")");
                if (buffer is not IArrayBuffer abuf) throw new InvalidDataException("Expected an ArrayBuffer");
                if (body is { Length: > 0 }) abuf.WriteBytes(body, 0, (ulong)body.Length, 0);
                BodyBytes = abuf;
            }
        }
        
        [NoDefaultScriptAccess]
        public class HttpBatchBuilder
        {
            private readonly PackageHttpImp _package;
            private readonly List<RequestDescriptor> _descriptors = new List<RequestDescriptor>();

            public HttpBatchBuilder(PackageHttpImp package)
            {
                _package = package;
            }

            [ScriptMember("request")]
            public HttpBatchBuilder Request(
                string method,
                string url,
                ScriptObject headers = null,
                bool useAuth = false,
                ScriptObject options = null)
            {
                var d = new RequestDescriptor
                {
                    Method = method,
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    UseAuth = useAuth,
                    ReturnType = ReturnType.String
                };

                if (options != null && options.PropertyNames.Contains("useByteResponses"))
                {
                    if (options["useByteResponses"] is bool b && b)
                        d.ReturnType = ReturnType.Bytes;
                }

                ApplyOptionsToDescriptor(d, options);
                _descriptors.Add(d);
                return this;
            }

            [ScriptMember("requestWithBody")]
            public HttpBatchBuilder RequestWithBody(
                string method,
                string url,
                object body,
                ScriptObject headers = null,
                bool useAuth = false,
                ScriptObject options = null)
            {
                var d = new RequestDescriptor
                {
                    Method = method,
                    Url = url,
                    Headers = HttpHeaders.FromScriptObject(headers),
                    Body = body,
                    UseAuth = useAuth,
                    ReturnType = ReturnType.String
                };

                if (options != null && options.PropertyNames.Contains("useByteResponses"))
                {
                    if (options["useByteResponses"] is bool b && b)
                        d.ReturnType = ReturnType.Bytes;
                }

                ApplyOptionsToDescriptor(d, options);
                _descriptors.Add(d);
                return this;
            }

            [ScriptMember]
            public HttpBatchBuilder GET(
                string url,
                ScriptObject headers = null,
                bool useAuth = false,
                ScriptObject options = null)
            {
                return Request("GET", url, headers, useAuth, options);
            }

            [ScriptMember]
            public HttpBatchBuilder POST(
                string url,
                string body,
                ScriptObject headers = null,
                bool useAuth = false,
                ScriptObject options = null)
            {
                return RequestWithBody("POST", url, body, headers, useAuth, options);
            }

            [ScriptMember]
            public HttpBatchBuilder DUMMY()
            {
                _descriptors.Add(new RequestDescriptor
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
                    .Select(x => _package.Perform(x))
                    .ToScriptArray();
            }
        }

        public sealed class RequestDescriptor
        {
            public string Method { get; set; }
            public string Url { get; set; }
            public Grayjay.Engine.Models.HttpHeaders Headers { get; set; }
            public object Body { get; set; }
            public ReturnType ReturnType { get; set; }
            public int TimeoutMs { get; set; }
            public string ImpersonateTarget { get; set; }
            public bool? UseBuiltInHeaders { get; set; }
            public bool UseAuth { get; set; }
        }

        public enum ReturnType { String = 1, Bytes = 2 }

        [NoDefaultScriptAccess]
        public sealed class PackageHttpImpClient
        {
            private readonly PackageHttpImp _owner;
            public bool UseAuth { get; }
            public string ClientID { get; } = Guid.NewGuid().ToString();
            public string DefaultImpersonateTarget { get; set; } = "chrome136";
            public bool DefaultUseBuiltInHeaders { get; set; } = true;
            public int DefaultTimeoutMs { get; private set; } = 30000;
            public bool DoApplyCookies { get; set; } = true;
            public bool DoUpdateCookies { get; set; } = true;
            public bool DoAllowNewCookies { get; set; } = true;


            private readonly Dictionary<string, string> _defaults = new();
            public string CookieJarPath { get; }

            public PackageHttpImpClient(PackageHttpImp owner, bool withAuth)
            {
                _owner = owner;
                UseAuth = withAuth;

                CookieJarPath = Path.Combine(Path.GetTempPath(), $"imphttp_{ClientID}.cookies.txt");
            }

            [ScriptMember("setDefaultHeaders")]
            public void SetDefaultHeaders(ScriptObject defaultHeaders)
            {
                lock (_defaults)
                {
                    foreach (var key in defaultHeaders.PropertyNames)
                        _defaults[key] = (string)defaultHeaders[key];
                }
            }

            [ScriptMember("setDefaultImpersonateTarget")]
            public void SetDefaultImpersonateTarget(string target) => DefaultImpersonateTarget = target;

            [ScriptMember("setUseBuiltInHeaders")]
            public void SetUseBuiltInHeaders(bool enable) => DefaultUseBuiltInHeaders = enable;

            [ScriptMember("setTimeout")]
            public void SetTimeout(int timeoutMs) => DefaultTimeoutMs = timeoutMs > 0 ? timeoutMs : 30000;

            [ScriptMember("setDoApplyCookies")]
            public void SetDoApplyCookies(bool apply) => DoApplyCookies = apply;

            [ScriptMember("setDoUpdateCookies")]
            public void SetDoUpdateCookies(bool update) => DoUpdateCookies = update;

            [ScriptMember("setDoAllowNewCookies")]
            public void SetDoAllowNewCookies(bool allow) => DoAllowNewCookies = allow;

            [ScriptMember("request")]
            public object Request(
                string method,
                string url,
                ScriptObject headers = null,
                bool useByteResponses = false,
                ScriptObject options = null)
            {
                var map = headers?.ToDictionary<string>() ?? new Dictionary<string, string>();
                ApplyDefaults(map);

                var descriptor = new RequestDescriptor
                {
                    Method = method,
                    Url = url,
                    Headers = new HttpHeaders(map),
                    ReturnType = useByteResponses ? ReturnType.Bytes : ReturnType.String
                };

                ApplyOptionsToDescriptor(descriptor, options);
                return _owner.Perform(descriptor, this);
            }

            [ScriptMember("requestWithBody")]
            public object RequestWithBody(
                string method,
                string url,
                object body,
                ScriptObject headers = null,
                bool useByteResponses = false,
                ScriptObject options = null)
            {
                var map = headers?.ToDictionary<string>() ?? new Dictionary<string, string>();
                ApplyDefaults(map);

                var descriptor = new RequestDescriptor
                {
                    Method = method,
                    Url = url,
                    Headers = new HttpHeaders(map),
                    Body = body,
                    ReturnType = useByteResponses ? ReturnType.Bytes : ReturnType.String
                };

                ApplyOptionsToDescriptor(descriptor, options);
                return _owner.Perform(descriptor, this);
            }

            [ScriptMember]
            public object GET(
                string url,
                ScriptObject headers,
                bool auth = false,
                bool useByteResponses = false,
                ScriptObject options = null)
            {
                return Request("GET", url, headers, useByteResponses, options);
            }

            [ScriptMember]
            public object POST(
                string url,
                object body,
                ScriptObject headers,
                bool auth = false,
                bool useByteResponses = false,
                ScriptObject options = null)
            {
                return RequestWithBody("POST", url, body, headers, useByteResponses, options);
            }

            private void ApplyDefaults(Dictionary<string, string> map)
            {
                lock (_defaults)
                    foreach (var kv in _defaults)
                        if (!map.ContainsKey(kv.Key)) map[kv.Key] = kv.Value;
            }
        }

        internal static void ApplyOptionsToDescriptor(RequestDescriptor d, ScriptObject? options)
        {
            if (options == null)
                return;

            var names = options.PropertyNames;
            if (names.Contains("impersonateTarget"))
            {
                if (options["impersonateTarget"] is string s && !string.IsNullOrWhiteSpace(s))
                    d.ImpersonateTarget = s;
            }

            if (names.Contains("useBuiltInHeaders"))
            {
                if (options["useBuiltInHeaders"] is bool b)
                    d.UseBuiltInHeaders = b;
            }

            if (names.Contains("timeoutMs"))
            {
                var v = options["timeoutMs"];
                switch (v)
                {
                    case int i when i > 0:
                        d.TimeoutMs = i;
                        break;
                    case long l when l > 0 && l <= int.MaxValue:
                        d.TimeoutMs = (int)l;
                        break;
                    case double dbl when dbl > 0 && dbl <= int.MaxValue:
                        d.TimeoutMs = (int)dbl;
                        break;
                }
            }
        }
    }
}

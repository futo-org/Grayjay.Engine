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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

            var req = new Libcurl.Request
            {
                Url = d.Url,
                Method = d.Method,
                Headers = d.Headers ?? new Dictionary<string, string>(),
                Body = bytes,
                UseBuiltInHeaders = builtInHdrs,
                ImpersonateTarget = target,
                TimeoutMs = (timeoutMs > 0) ? timeoutMs : 30000,
                CookieJarPath = client.CookieJarPath,
                SendCookies = client.DoApplyCookies,
                PersistCookies = client.DoUpdateCookies && client.DoAllowNewCookies
            };

            var res = Libcurl.Perform(req);

            var sanitized = SanitizeResponseHeaders(res.Headers, onlyWhitelisted: client.UseAuth || !_plugin.Config.AllowAllHttpHeaderAccess);
            if (d.ReturnType == ReturnType.Bytes)
            {
                return new ImpJSBytesResponse(_plugin, res.Status, res.BodyBytes ?? Array.Empty<byte>(), sanitized, res.EffectiveUrl);
            }
            else
            {
                var text = res.BodyBytes != null ? Encoding.UTF8.GetString(res.BodyBytes) : null;
                return new ImpStringResponse(res.Status, text, sanitized, res.EffectiveUrl);
            }
        }

        private static Dictionary<string, List<string>> SanitizeResponseHeaders(Dictionary<string, List<string>> headers, bool onlyWhitelisted)
        {
            var results = new Dictionary<string, List<string>>();
            if (onlyWhitelisted)
            {
                foreach (var h in headers)
                    if (WHITELISTED_RESPONSE_HEADERS.Contains(h.Key.ToLower()))
                        results[h.Key] = h.Value;
            }
            else
            {
                foreach (var h in headers)
                {
                    if (h.Key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase))
                        results[h.Key] = h.Value.Where(v => !v.ToLower().Contains("httponly")).ToList();
                    else
                        results[h.Key] = h.Value;
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
                    Headers = headers?.ToDictionary<string>(),
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
                    Headers = headers?.ToDictionary<string>(),
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
            public Dictionary<string, string> Headers { get; set; }
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
                    Headers = map,
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
                    Headers = map,
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

        internal static class Libcurl
        {
            private const string Lib = "curlshim";

            public sealed class Request
            {
                public string Url;
                public string Method;
                public Dictionary<string, string> Headers = new();
                public byte[] Body;
                public string ImpersonateTarget = "chrome136";
                public bool UseBuiltInHeaders = true;
                public int TimeoutMs = 30000;
                public string CookieJarPath;
                public bool SendCookies = true;
                public bool PersistCookies = true;
            }

            public sealed class Response
            {
                public int Status;
                public string EffectiveUrl;
                public byte[] BodyBytes;
                public Dictionary<string, List<string>> Headers = new(StringComparer.OrdinalIgnoreCase);
            }

            private enum CURLcode : int
            {
                CURLE_OK = 0,
                CURLE_UNKNOWN_OPTION = 48
            }

            internal static class CurlInfoConsts
            {
                public const int CURLINFO_STRING = 0x100000;
                public const int CURLINFO_LONG = 0x200000;
                public const int CURLINFO_DOUBLE = 0x300000;
                public const int CURLINFO_SLIST = 0x400000;
                public const int CURLINFO_PTR = 0x400000;
                public const int CURLINFO_SOCKET = 0x500000;
                public const int CURLINFO_OFF_T = 0x600000;
                public const int CURLINFO_MASK = 0x0fffff;
                public const int CURLINFO_TYPEMASK = 0xf00000;
            }

            internal enum CURLINFO : int
            {
                CURLINFO_NONE = 0,
                EFFECTIVE_URL = CurlInfoConsts.CURLINFO_STRING + 1,
                CONTENT_TYPE = CurlInfoConsts.CURLINFO_STRING + 18,
                PRIVATE = CurlInfoConsts.CURLINFO_STRING + 21,
                FTP_ENTRY_PATH = CurlInfoConsts.CURLINFO_STRING + 30,
                REDIRECT_URL = CurlInfoConsts.CURLINFO_STRING + 31,
                PRIMARY_IP = CurlInfoConsts.CURLINFO_STRING + 32,
                RTSP_SESSION_ID = CurlInfoConsts.CURLINFO_STRING + 36,
                LOCAL_IP = CurlInfoConsts.CURLINFO_STRING + 41,
                SCHEME = CurlInfoConsts.CURLINFO_STRING + 49,
                EFFECTIVE_METHOD = CurlInfoConsts.CURLINFO_STRING + 58,
                REFERER = CurlInfoConsts.CURLINFO_STRING + 60,
                CAINFO = CurlInfoConsts.CURLINFO_STRING + 61,
                CAPATH = CurlInfoConsts.CURLINFO_STRING + 62,

                RESPONSE_CODE = CurlInfoConsts.CURLINFO_LONG + 2,
                HEADER_SIZE = CurlInfoConsts.CURLINFO_LONG + 11,
                REQUEST_SIZE = CurlInfoConsts.CURLINFO_LONG + 12,
                SSL_VERIFYRESULT = CurlInfoConsts.CURLINFO_LONG + 13,
                FILETIME = CurlInfoConsts.CURLINFO_LONG + 14,
                REDIRECT_COUNT = CurlInfoConsts.CURLINFO_LONG + 20,
                HTTP_CONNECTCODE = CurlInfoConsts.CURLINFO_LONG + 22,
                HTTPAUTH_AVAIL = CurlInfoConsts.CURLINFO_LONG + 23,
                PROXYAUTH_AVAIL = CurlInfoConsts.CURLINFO_LONG + 24,
                OS_ERRNO = CurlInfoConsts.CURLINFO_LONG + 25,
                NUM_CONNECTS = CurlInfoConsts.CURLINFO_LONG + 26,
                LASTSOCKET = CurlInfoConsts.CURLINFO_LONG + 29, // deprecated
                PRIMARY_PORT = CurlInfoConsts.CURLINFO_LONG + 40,
                LOCAL_PORT = CurlInfoConsts.CURLINFO_LONG + 42,
                HTTP_VERSION = CurlInfoConsts.CURLINFO_LONG + 46,

                TOTAL_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 3,
                NAMELOOKUP_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 4,
                CONNECT_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 5,
                PRETRANSFER_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 6,
                STARTTRANSFER_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 17,
                REDIRECT_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 19,
                APPCONNECT_TIME = CurlInfoConsts.CURLINFO_DOUBLE + 33,

                SSL_ENGINES = CurlInfoConsts.CURLINFO_SLIST + 27,
                COOKIELIST = CurlInfoConsts.CURLINFO_SLIST + 28,

                ACTIVESOCKET = CurlInfoConsts.CURLINFO_SOCKET + 44,

                SIZE_UPLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 7,
                SIZE_DOWNLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 8,
                SPEED_DOWNLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 9,
                SPEED_UPLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 10,
                FILETIME_T = CurlInfoConsts.CURLINFO_OFF_T + 14,
                CONTENT_LENGTH_DOWNLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 15,
                CONTENT_LENGTH_UPLOAD_T = CurlInfoConsts.CURLINFO_OFF_T + 16,
                TOTAL_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 50,
                NAMELOOKUP_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 51,
                CONNECT_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 52,
                PRETRANSFER_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 53,
                STARTTRANSFER_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 54,
                REDIRECT_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 55,
                APPCONNECT_TIME_T = CurlInfoConsts.CURLINFO_OFF_T + 56,

                LASTONE = 70
            }

            private enum CURLOPT : int
            {
                URL = 10002,
                FOLLOWLOCATION = 52,
                MAXREDIRS = 68,
                CONNECTTIMEOUT_MS = 156,
                TIMEOUT_MS = 155,
                HTTP_VERSION = 84,
                USERAGENT = 10018,
                ACCEPT_ENCODING = 10102,
                REFERER = 10016,
                HTTPHEADER = 10023,
                COOKIEFILE = 10031,
                COOKIEJAR = 10082,
                CUSTOMREQUEST = 10036,
                POSTFIELDS = 10015,
                POSTFIELDSIZE = 60,
                WRITEFUNCTION = 20011,
                HEADERFUNCTION = 20079,
                WRITEDATA = 10001,
                HEADERDATA = 10029
            }

            private enum CURL_HTTP_VERSION : int { TWO_TLS = 4 }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate UIntPtr WriteCb(IntPtr ptr, UIntPtr size, UIntPtr nmemb, IntPtr userdata);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_global_init(long flags);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern void ce_global_cleanup();

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern IntPtr ce_easy_init();

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern void ce_easy_cleanup(IntPtr easy);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_easy_perform(IntPtr easy);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern CURLcode ce_easy_impersonate(IntPtr easy, string target, int default_headers);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_easy_getinfo_long(IntPtr e, CURLINFO i, out long l);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_easy_getinfo_ptr(IntPtr e, CURLINFO i, out IntPtr p);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern IntPtr ce_easy_strerror(CURLcode code);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern IntPtr ce_slist_append(IntPtr slist, string header);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern void ce_slist_free_all(IntPtr slist);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_setopt_long(IntPtr easy, CURLOPT opt, long val);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern CURLcode ce_setopt_str(IntPtr easy, CURLOPT opt, string str);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            private static extern CURLcode ce_setopt_ptr(IntPtr easy, CURLOPT opt, IntPtr ptr);

            private static bool _globalInitDone;
            private static readonly object _initLock = new();

            private static readonly WriteCb s_bodyCb = BodyThunk;
            private static readonly WriteCb s_headerCb = HeaderThunk;

            private sealed class CallbackState
            {
                public readonly MemoryStream Body = new(64 * 1024);
                public readonly List<string> Headers = new();
                public byte[] Scratch = Array.Empty<byte>();
            }

            private static UIntPtr BodyThunk(IntPtr p, UIntPtr size, UIntPtr nmemb, IntPtr userdata)
            {
                ulong sz = size.ToUInt64() * nmemb.ToUInt64();
                if (sz == 0) return (UIntPtr)0;

                var state = (CallbackState)GCHandle.FromIntPtr(userdata).Target!;
                int len = checked((int)sz);

                if (state.Scratch.Length < len) state.Scratch = new byte[len];
                Marshal.Copy(p, state.Scratch, 0, len);
                state.Body.Write(state.Scratch, 0, len);

                return (UIntPtr)sz;
            }

            private static UIntPtr HeaderThunk(IntPtr p, UIntPtr size, UIntPtr nmemb, IntPtr userdata)
            {
                ulong sz = size.ToUInt64() * nmemb.ToUInt64();
                if (sz == 0) return (UIntPtr)0;

                var state = (CallbackState)GCHandle.FromIntPtr(userdata).Target!;
                int len = checked((int)sz);

                if (state.Scratch.Length < len) state.Scratch = new byte[len];
                Marshal.Copy(p, state.Scratch, 0, len);

                // Header lines may contain binary; we’ll trim CRLF only
                var line = Encoding.ASCII.GetString(state.Scratch, 0, len).TrimEnd('\r', '\n');
                if (!string.IsNullOrWhiteSpace(line))
                    state.Headers.Add(line);

                return (UIntPtr)sz;
            }

            public static Response Perform(Request req)
            {
                EnsureGlobalInit();

                IntPtr easy = IntPtr.Zero;
                IntPtr hdrs = IntPtr.Zero;
                IntPtr bodyPtr = IntPtr.Zero;

                var state = new CallbackState();
                var gch = GCHandle.Alloc(state, GCHandleType.Normal);

                try
                {
                    easy = ce_easy_init();
                    if (easy == IntPtr.Zero) throw new InvalidOperationException("curl_easy_init failed");

                    var imp = ce_easy_impersonate(easy, req.ImpersonateTarget, req.UseBuiltInHeaders ? 1 : 0);
                    if (imp != CURLcode.CURLE_OK && imp != CURLcode.CURLE_UNKNOWN_OPTION)
                        throw new InvalidOperationException($"curl_easy_impersonate failed: {GetErr(imp)}");

                    Check(ce_setopt_str(easy, CURLOPT.URL, req.Url));
                    Check(ce_setopt_long(easy, CURLOPT.FOLLOWLOCATION, 1));
                    Check(ce_setopt_long(easy, CURLOPT.MAXREDIRS, 10));
                    Check(ce_setopt_long(easy, CURLOPT.CONNECTTIMEOUT_MS, req.TimeoutMs));
                    Check(ce_setopt_long(easy, CURLOPT.TIMEOUT_MS, req.TimeoutMs));
                    Check(ce_setopt_long(easy, CURLOPT.HTTP_VERSION, (long)CURL_HTTP_VERSION.TWO_TLS));
                    Check(ce_setopt_str(easy, CURLOPT.ACCEPT_ENCODING, string.Empty));

                    foreach (var kv in req.Headers)
                        hdrs = ce_slist_append(hdrs, $"{kv.Key}: {kv.Value}");
                    if (hdrs != IntPtr.Zero)
                        Check(ce_setopt_ptr(easy, CURLOPT.HTTPHEADER, hdrs));

                    if (req.SendCookies || req.PersistCookies)
                    {
                        var jar = string.IsNullOrEmpty(req.CookieJarPath)
                            ? Path.Combine(Path.GetTempPath(), "imphttp.cookies.txt")
                            : req.CookieJarPath;

                        if (req.SendCookies)    Check(ce_setopt_str(easy, CURLOPT.COOKIEFILE, jar));
                        if (req.PersistCookies) Check(ce_setopt_str(easy, CURLOPT.COOKIEJAR,  jar));
                    }

                    if (!string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        Check(ce_setopt_str(easy, CURLOPT.CUSTOMREQUEST, req.Method));
                        if (req.Body is { Length: > 0 })
                        {
                            bodyPtr = Marshal.AllocHGlobal(req.Body.Length);
                            Marshal.Copy(req.Body, 0, bodyPtr, req.Body.Length);
                            Check(ce_setopt_ptr(easy, CURLOPT.POSTFIELDS, bodyPtr));
                            Check(ce_setopt_long(easy, CURLOPT.POSTFIELDSIZE, req.Body.Length));
                        }
                    }

                    IntPtr bodyFn = Marshal.GetFunctionPointerForDelegate(s_bodyCb);
                    IntPtr hdrFn  = Marshal.GetFunctionPointerForDelegate(s_headerCb);
                    Check(ce_setopt_ptr(easy, CURLOPT.WRITEFUNCTION, bodyFn));
                    Check(ce_setopt_ptr(easy, CURLOPT.HEADERFUNCTION, hdrFn));

                    IntPtr user = GCHandle.ToIntPtr(gch);
                    Check(ce_setopt_ptr(easy, CURLOPT.WRITEDATA, user));
                    Check(ce_setopt_ptr(easy, CURLOPT.HEADERDATA, user));

                    var rc = ce_easy_perform(easy);
                    if (rc != CURLcode.CURLE_OK)
                        throw new InvalidOperationException($"curl_easy_perform failed: {GetErr(rc)}");

                    Check(ce_easy_getinfo_long(easy, CURLINFO.RESPONSE_CODE, out long code));
                    Check(ce_easy_getinfo_ptr(easy, CURLINFO.EFFECTIVE_URL, out IntPtr urlPtr));
                    var effective = urlPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(urlPtr) : req.Url;

                    return new Response
                    {
                        Status = (int)code,
                        EffectiveUrl = effective,
                        BodyBytes = state.Body.ToArray(),
                        Headers = ParseHeaders(state.Headers)
                    };
                }
                finally
                {
                    if (bodyPtr != IntPtr.Zero) Marshal.FreeHGlobal(bodyPtr);
                    if (easy != IntPtr.Zero) ce_easy_cleanup(easy);
                    if (hdrs != IntPtr.Zero) ce_slist_free_all(hdrs);
                    if (gch.IsAllocated) gch.Free();
                }
            }

            private static void EnsureGlobalInit()
            {
                if (_globalInitDone) return;
                lock (_initLock)
                {
                    if (_globalInitDone) return;
                    var rc = ce_global_init(3 /* CURL_GLOBAL_ALL */);
                    if (rc != CURLcode.CURLE_OK) throw new InvalidOperationException($"curl_global_init failed: {GetErr(rc)}");
                    _globalInitDone = true;
                    AppDomain.CurrentDomain.ProcessExit += (_, __) => ce_global_cleanup();
                }
            }

            private static string GetErr(CURLcode c)
            {
                var p = ce_easy_strerror(c);
                return p != IntPtr.Zero ? Marshal.PtrToStringAnsi(p) ?? c.ToString() : c.ToString();
            }

            private static void Check(CURLcode c)
            {
                if (c != CURLcode.CURLE_OK) throw new InvalidOperationException($"libcurl error: {GetErr(c)}");
            }

            private static Dictionary<string, List<string>> ParseHeaders(List<string> lines)
            {
                var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines)
                {
                    var idx = line.IndexOf(':');
                    if (idx <= 0) continue;
                    var name = line[..idx].Trim();
                    var val  = line[(idx + 1)..].Trim();
                    if (!dict.TryGetValue(name, out var list))
                    {
                        list = new List<string>();
                        dict[name] = list;
                    }
                    list.Add(val);
                }
                return dict;
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

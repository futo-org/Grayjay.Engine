using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Grayjay.Engine.Packages;
        
public static class Libcurl
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
        HEADERDATA = 10029,
        CAINFO = 10065,
        CAPATH = 10097,
        DNS_SERVERS = 10211
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

    private static string? _defaultCAPath;

    public static void SetDefaultCAPath(string path)
    {
        _defaultCAPath = path;
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

            Check(ce_setopt_str(easy, CURLOPT.DNS_SERVERS, "1.1.1.1,8.8.8.8"));

            var ca = _defaultCAPath;
            if (!string.IsNullOrEmpty(ca))
            {
                Check(ce_setopt_str(easy, CURLOPT.CAINFO, ca));
            }

            foreach (var kv in req.Headers)
                hdrs = ce_slist_append(hdrs, $"{kv.Key}: {kv.Value}");
            if (hdrs != IntPtr.Zero)
                Check(ce_setopt_ptr(easy, CURLOPT.HTTPHEADER, hdrs));

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

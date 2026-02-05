using DotCef;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public sealed class PackageBrowser : Package
    {
        public static DotCefProcess? Process;

        public override string Name => "Browser";
        public override string VariableName => "browser";

        private readonly object _gate = new();

        private DotCefWindow? _window;
        private volatile string? _currentUrl;

        private TaskCompletionSource<bool>? _loadTcs;

        private readonly ConcurrentDictionary<string, ScriptObject> _callbacks = new();

        private SynchronizationContext? _v8SyncContext;
        private V8ScriptEngine? _engine;

        private bool _interopInstalled;
        private long _loadSeq;
        private long _activeLoadSeq;
        private long _activeLoadStartTs;
        private string? _activeLoadUrl;


        public PackageBrowser(GrayjayPlugin plugin) : base(plugin) { }

        public override void Initialize(V8ScriptEngine engine)
        {
            _engine = engine;
            _v8SyncContext = SynchronizationContext.Current;
            engine.AddHostObject(VariableName, this);
        }

        private int _disposed;
        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            try { deinitialize(); } catch { }
        }

        private DotCefWindow WindowOrThrow()
            => _window ?? throw new InvalidOperationException("Browser not initialized. Call browser.initialize().");

        [ScriptMember]
        public void initialize()
        {
            lock (_gate)
            {
                if (_window != null)
                    return;
            }

            var proc = Process ?? throw new InvalidOperationException("Can't use PackageBrowser without setting CEF process.");
            var win = proc.CreateWindowAsync(
                url: "about:blank",
                minimumWidth: 320,
                minimumHeight: 240,
                preferredWidth: 800,
                preferredHeight: 600,
                shown: false,
                developerToolsEnabled: false,
                proxyRequests: false,
                modifyRequests: false,
                logConsole: false
            ).GetAwaiter().GetResult();
            win.HideAsync().GetAwaiter().GetResult();

            win.OnLoadEnd += url =>
            {
                bool isActiveUrl = _activeLoadUrl != null && url != null && _activeLoadUrl.TrimEnd('/') == url.TrimEnd('/');
                if (!isActiveUrl)
                {
                    Logger.Info<PackageBrowser>($"OnLoadEnd Ignored (url={url}, _activeLoadUrl={_activeLoadUrl})");
                    return;
                }

                _currentUrl = url;
                Logger.Info<PackageBrowser>($"OnLoadEnd (url={url})");
                CompleteLoadTcs(success: true, reason: "OnLoadEnd", url: url);
            };

            win.OnLoadError += (code, text, failedUrl) =>
            {
                bool isActiveUrl = _activeLoadUrl != null && failedUrl != null && _activeLoadUrl.TrimEnd('/') == failedUrl.TrimEnd('/');
                if (!isActiveUrl)
                {
                    Logger.Info<PackageBrowser>($"OnLoadError Ignored (failedUrl={failedUrl}, _activeLoadUrl={_activeLoadUrl})");
                    return;
                }

                _currentUrl = failedUrl;
                Logger.Warning<PackageBrowser>($"OnLoadError (code={code}, text={text}, url={failedUrl})");
                CompleteLoadTcs(success: false, reason: $"OnLoadError:{code}", url: failedUrl);
            };

            win.OnDevToolsEvent += (method, payload) => OnDevToolsEvent(method, payload);

            lock (_gate)
            {
                _window = win;
                _interopInstalled = false;
                _loadTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            EnsureInteropInstalledBlocking();
        }

        [ScriptMember]
        public void deinitialize()
        {
            DotCefWindow? w;
            lock (_gate)
            {
                w = _window;
                _window = null;
                _interopInstalled = false;
                _loadTcs?.TrySetResult(false);
                Logger.Info<PackageBrowser>("deinitialize() completed pending LoadTCS with false");
                _loadTcs = null;
            }

            _callbacks.Clear();

            if (w != null)
            {
                try { w.CloseAsync(forceClose: true).GetAwaiter().GetResult(); }
                catch { /* ignore */ }
            }
        }

        [ScriptMember]
        public string? getCurrentUrl()
        {
            var url = _currentUrl;
            if (!string.IsNullOrEmpty(url))
                return url;

            try
            {
                return EvaluateWithReturnBlocking("location.href") ?? _currentUrl;
            }
            catch
            {
                return _currentUrl;
            }
        }

        [ScriptMember]
        public bool waitTillLoaded(int timeout = 1000)
        {
            TaskCompletionSource<bool>? tcs;
            long seq;
            string? url;
            long started;

            lock (_gate)
            {
                tcs = _loadTcs;
                seq = _activeLoadSeq;
                url = _activeLoadUrl;
                started = _activeLoadStartTs;
            }

            if (tcs == null)
            {
                Logger.Info<PackageBrowser>($"waitTillLoaded() no TCS -> true)");
                return true;
            }

            var ageMs = started != 0 ? (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency : -1;
            Logger.Info<PackageBrowser>($"waitTillLoaded() wait begin (seq={seq}, url={url ?? "null"}, timeoutMs={timeout}, ageMs={ageMs:F1})");

            try
            {
                var completed = tcs.Task.Wait(timeout);
                if (!completed)
                {
                    Logger.Warning<PackageBrowser>($"waitTillLoaded() timeout (seq={seq}, url={url ?? "null"}, timeoutMs={timeout})");
                    return false;
                }

                var result = tcs.Task.Result;
                Logger.Info<PackageBrowser>($"waitTillLoaded() wait end (seq={seq}, result={result}, url={url ?? "null"})");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning<PackageBrowser>($"waitTillLoaded() exception (seq={seq}, url={url ?? "null"}, ex={ex.GetType().Name}:{ex.Message})");
                return false;
            }
        }

        [ScriptMember]
        public void load(string url)
        {
            Logger.Info<PackageBrowser>($"load() begin (url={url})");
            _currentUrl = url;
            ResetLoadTcs("load start", url);
            WindowOrThrow().LoadUrlAsync(url).GetAwaiter().GetResult();
            Logger.Info<PackageBrowser>($"load() end (url={url})");
        }

        [ScriptMember]
        public void run(string js, string? callbackId = null, ScriptObject? callback = null)
        {
            Logger.Info<PackageBrowser>($"run() waiting)");
            waitTillLoaded();

            if (!string.IsNullOrEmpty(callbackId) && callback != null)
            {
                Logger.Info<PackageBrowser>($"run() with callback, set {callbackId})");
                _callbacks[callbackId] = callback;
            }

            _ = ExecuteDevToolsBlocking("Runtime.evaluate", new
            {
                expression = js,
                awaitPromise = false,
                returnByValue = false
            });
        }

        [ScriptMember]
        public void runWithReturn(string js, ScriptObject? callback = null)
        {
            Logger.Info<PackageBrowser>($"runWithReturn() waiting");
            waitTillLoaded();

            Logger.Info<PackageBrowser>($"runWithReturn() with callback, before");

            var result = EvaluateWithReturnBlocking(js);

            if (callback != null)
            {
                Logger.Info<PackageBrowser>($"runWithReturn() with callback, invoke");
                InvokeOnV8(() => callback.Invoke(false, result));
            }
        }

        private void ResetLoadTcs(string reason, string? url)
        {
            TaskCompletionSource<bool>? oldTcs;
            long oldSeq;
            long newSeq = Interlocked.Increment(ref _loadSeq);

            lock (_gate)
            {
                oldTcs = _loadTcs;
                oldSeq = _activeLoadSeq;

                _activeLoadSeq = newSeq;
                _activeLoadStartTs = Stopwatch.GetTimestamp();
                _activeLoadUrl = url;

                _loadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            oldTcs?.TrySetResult(false);

            Logger.Info<PackageBrowser>($"LoadTCS reset (reason={reason}, newSeq={newSeq}, oldSeq={oldSeq}, url={url ?? "null"})");
        }


        private void CompleteLoadTcs(bool success, string reason, string? url)
        {
            TaskCompletionSource<bool>? tcs;
            long seq;
            long started;
            string? activeUrl;

            lock (_gate)
            {
                tcs = _loadTcs;
                seq = _activeLoadSeq;
                started = _activeLoadStartTs;
                activeUrl = _activeLoadUrl;
            }

            var elapsedMs = started != 0 ? (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency : -1;
            var set = tcs?.TrySetResult(success) ?? false;
            Logger.Info<PackageBrowser>($"LoadTCS complete (seq={seq}, set={set}, success={success}, reason={reason}, eventUrl={url ?? "null"}, activeUrl={activeUrl ?? "null"}, elapsedMs={elapsedMs:F1})");
        }


        private void EnsureInteropInstalledBlocking()
        {
            DotCefWindow w;
            lock (_gate)
            {
                if (_interopInstalled)
                    return;
                w = WindowOrThrow();
            }

            w.AddDevToolsEventMethod("Runtime.bindingCalled").GetAwaiter().GetResult();
            w.AddDevToolsEventMethod("Runtime.consoleAPICalled").GetAwaiter().GetResult();

            ExecuteDevToolsBlocking("Runtime.enable", new { });
            ExecuteDevToolsBlocking("Page.enable", new { });

            ExecuteDevToolsBlocking("Runtime.addBinding", new { name = "__GJ_callback" });
            ExecuteDevToolsBlocking("Runtime.addBinding", new { name = "__GJ_log" });

            var bootstrap = """
            (() => {
                if (window.__GJ) return;
                window.__GJ = {
                    callback: (id, result) => {
                        try { __GJ_callback(JSON.stringify({ id: String(id), result: (typeof result === 'string' ? result : JSON.stringify(result)) })); } catch (e) {}
                    },
                    log: (msg) => {
                        try { __GJ_log(String(msg)); } catch (e) {}
                    }
                };
            })();
            """;

            ExecuteDevToolsBlocking("Page.addScriptToEvaluateOnNewDocument", new { source = bootstrap });
            ExecuteDevToolsBlocking("Runtime.evaluate", new { expression = bootstrap, awaitPromise = false, returnByValue = false });

            lock (_gate) _interopInstalled = true;
        }

        private void OnDevToolsEvent(string? method, byte[] payloadUtf8)
        {
            if (string.IsNullOrEmpty(method))
                return;

            try
            {
                using var doc = JsonDocument.Parse(payloadUtf8);
                var root = doc.RootElement;

                if (method == "Runtime.bindingCalled")
                {
                    var name = root.GetProperty("name").GetString();
                    var payload = root.GetProperty("payload").GetString() ?? "";

                    if (name == "__GJ_callback")
                        HandleGJCallback(payload);
                    else if (name == "__GJ_log")
                        HandleGJLog(payload);

                    return;
                }

                if (method == "Runtime.consoleAPICalled")
                {
                    if (root.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
                    {
                        var sb = new StringBuilder();
                        foreach (var a in args.EnumerateArray())
                        {
                            if (a.TryGetProperty("value", out var v))
                                sb.Append(v.ToString()).Append(' ');
                            else if (a.TryGetProperty("description", out var d))
                                sb.Append(d.GetString()).Append(' ');
                        }
                        var msg = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(msg))
                            Logger.Info<PackageBrowser>("Browser Console: " + msg);
                    }
                }
            }
            catch
            {
                // ignore CDP noise
            }
        }

        private void HandleGJLog(string payload)
        {
            Logger.Info<PackageBrowser>("Browser Log: " + payload);
        }

        private void HandleGJCallback(string payload)
        {
            try
            {
                Logger.Info<PackageBrowser>($"HandleGJCallback(): " + payload);

                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var id = root.GetProperty("id").GetString();
                var result = root.GetProperty("result").GetString();

                Logger.Info<PackageBrowser>($"HandleGJCallback(id = {id}, result = {result})");

                if (string.IsNullOrEmpty(id))
                {
                    Logger.Warning<PackageBrowser>($"HandleGJCallback id must be set (id = {id})");
                    return;
                }

                if (_callbacks.TryRemove(id, out var cb))
                {
                    Logger.Info<PackageBrowser>($"HandleGJCallback invoke result (id = {id}, result = {result})");
                    InvokeOnV8(() => cb.Invoke(false, result));
                }
                else
                    Logger.Warning<PackageBrowser>($"HandleGJCallback could not find callback matching (id = {id})");
            }
            catch (Exception e)
            {
                Logger.Warning<PackageBrowser>($"HandleGJCallback ignored malformed payload", e);
            }
        }

        private string? EvaluateWithReturnBlocking(string expression)
        {
            var raw = ExecuteDevToolsBlocking("Runtime.evaluate", new
            {
                expression,
                awaitPromise = true,
                returnByValue = true
            });

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("exceptionDetails", out var ex))
            {
                Logger.Warning<PackageBrowser>("Runtime.evaluate exception: " + ex.ToString());
                return null;
            }

            if (!root.TryGetProperty("result", out var res))
                return null;

            if (res.TryGetProperty("value", out var value))
            {
                return value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : value.GetRawText();
            }

            if (res.TryGetProperty("description", out var desc))
                return desc.GetString();

            return null;
        }

        private string ExecuteDevToolsBlocking(string method, object args)
        {
            var json = JsonSerializer.Serialize(args);
            var (ok, data) = WindowOrThrow().ExecuteDevToolsMethodAsync(method, json).GetAwaiter().GetResult();
            if (!ok)
                throw new InvalidOperationException($"DevTools method failed: {method}");

            return Encoding.UTF8.GetString(data);
        }

        private void InvokeOnV8(Action action)
        {
            var sc = _v8SyncContext;
            if (sc != null)
            {
                sc.Post(_ => action(), null);
                return;
            }

            action();
        }
    }
}

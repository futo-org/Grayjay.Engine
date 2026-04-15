using JustCef;
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
        public static JustCefProcess? Process;

        public override string Name => "Browser";
        public override string VariableName => "browser";

        private readonly object _gate = new();

        private JustCefWindow? _window;
        private volatile string? _currentUrl;

        private readonly ConcurrentDictionary<string, ScriptObject> _callbacks = new();

        private SynchronizationContext? _v8SyncContext;
        private V8ScriptEngine? _engine;

        private bool _interopInstalled;
        private readonly ConcurrentDictionary<string, string> _pageLoadScripts = new();

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

        private JustCefWindow WindowOrThrow()
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
            //win.SetDevelopmentToolsVisibleAsync(true).GetAwaiter().GetResult();

            win.OnFrameLoadEnd += info =>
            {
                if (!info.IsMainFrame)
                    return;

                _currentUrl = info.Url;
                Logger.Info<PackageBrowser>($"OnFrameLoadEnd (url={info.Url}, httpStatusCode={info.HttpStatusCode})");
            };

            win.OnFrameLoadError += info =>
            {
                if (!info.IsMainFrame)
                    return;

                if (info.ErrorCode == -3)
                {
                    Logger.Info<PackageBrowser>($"OnFrameLoadError ignored ERR_ABORTED (url={info.FailedUrl})");
                    return;
                }

                _currentUrl = info.FailedUrl;
                Logger.Warning<PackageBrowser>($"OnFrameLoadError (code={info.ErrorCode}, text={info.ErrorText}, url={info.FailedUrl})");
            };

            win.OnDevToolsEvent += (method, payload) => OnDevToolsEvent(method, payload);

            lock (_gate)
            {
                _window = win;
                _interopInstalled = false;
            }

            EnsureInteropInstalledBlocking();
        }

        [ScriptMember]
        public void deinitialize()
        {
            JustCefWindow? w;
            lock (_gate)
            {
                w = _window;
                _window = null;
                _interopInstalled = false;
            }

            _callbacks.Clear();
            _pageLoadScripts.Clear();

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
            JustCefWindow? w;
            lock (_gate)
                w = _window;

            if (w == null)
            {
                Logger.Info<PackageBrowser>("waitTillLoaded() no window -> true");
                return true;
            }

            Logger.Info<PackageBrowser>($"waitTillLoaded() wait begin (timeoutMs={timeout})");

            try
            {
                using var cts = timeout > 0
                    ? new CancellationTokenSource(timeout)
                    : new CancellationTokenSource();

                w.WaitUntilLoadedAsync(cts.Token).GetAwaiter().GetResult();

                Logger.Info<PackageBrowser>("waitTillLoaded() wait end (result=true)");
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning<PackageBrowser>($"waitTillLoaded() timeout (timeoutMs={timeout})");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning<PackageBrowser>(
                    $"waitTillLoaded() exception (ex={ex.GetType().Name}:{ex.Message})");
                return false;
            }
        }

        [ScriptMember]
        public void load(string url)
        {
            Logger.Info<PackageBrowser>($"load() begin (url={url})");
            _currentUrl = url;
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

        private void EnsureInteropInstalledBlocking()
        {
            JustCefWindow w;
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

        [ScriptMember]
        public string addScriptOnLoad(string js)
        {
            if (string.IsNullOrWhiteSpace(js))
                throw new ArgumentException("Script must be non-empty.", nameof(js));

            EnsureInteropInstalledBlocking();
            var raw = ExecuteDevToolsBlocking("Page.addScriptToEvaluateOnNewDocument", new
            {
                source = js
            });

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("identifier", out var idEl))
                throw new InvalidOperationException("CDP did not return a script identifier.");

            var id = idEl.GetString();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("CDP returned an empty script identifier.");

            _pageLoadScripts[id] = js;
            Logger.Info<PackageBrowser>($"addScriptOnLoad() registered (id={id})");
            return id;
        }

        [ScriptMember]
        public bool removeScriptOnLoad(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            try
            {
                EnsureInteropInstalledBlocking();

                ExecuteDevToolsBlocking("Page.removeScriptToEvaluateOnNewDocument", new
                {
                    identifier
                });

                _pageLoadScripts.TryRemove(identifier, out _);
                Logger.Info<PackageBrowser>($"removeScriptOnLoad() removed (id={identifier})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning<PackageBrowser>($"removeScriptOnLoad() failed (id={identifier})", ex);
                return false;
            }
        }

        [ScriptMember]
        public void clearScriptsOnLoad()
        {
            EnsureInteropInstalledBlocking();

            foreach (var id in _pageLoadScripts.Keys)
            {
                try
                {
                    ExecuteDevToolsBlocking("Page.removeScriptToEvaluateOnNewDocument", new { identifier = id });
                }
                catch
                {
                    // ignore
                }

                _pageLoadScripts.TryRemove(id, out _);
            }

            Logger.Info<PackageBrowser>("clearScriptsOnLoad() cleared");
        }
    }
}

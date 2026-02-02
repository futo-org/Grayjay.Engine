using DotCef;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public sealed class PackageBrowser : Package
    {
        public override string VariableName => "browser";
        private static readonly object s_procGate = new();
        private static DotCefProcess? s_process;
        private static bool s_processStarted;

        private static DotCefProcess EnsureProcessStarted()
        {
            lock (s_procGate)
            {
                if (s_processStarted && s_process != null && !s_process.HasExited)
                    return s_process;

                // (Re)start process
                s_process = new DotCefProcess();
                s_process.Start();
                s_process.WaitForReady();
                s_processStarted = true;

                return s_process;
            }
        }

        private readonly object _gate = new();

        private DotCefWindow? _window;
        private volatile string? _currentUrl;

        private TaskCompletionSource<bool>? _loadTcs;

        private readonly ConcurrentDictionary<string, ScriptObject> _callbacks = new();

        private SynchronizationContext? _v8SyncContext;
        private V8ScriptEngine? _engine;

        private bool _interopInstalled;

        public PackageBrowser(GrayjayPlugin plugin) : base(plugin) { }

        public override void Initialize(V8ScriptEngine engine)
        {
            _engine = engine;
            _v8SyncContext = SynchronizationContext.Current;
            engine.AddHostObject(VariableName, this);
        }

        public override void Dispose()
        {
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

            var proc = EnsureProcessStarted();
            var win = proc.CreateWindowAsync(
                url: "about:blank",
                minimumWidth: 320,
                minimumHeight: 240,
                preferredWidth: 800,
                preferredHeight: 600,
                shown: true,
                developerToolsEnabled: false,
                proxyRequests: false,
                modifyRequests: false,
                logConsole: false
            ).GetAwaiter().GetResult();

            win.OnLoadStart += url =>
            {
                _currentUrl = url;
                ResetLoadTcs();
            };
            win.OnLoadEnd += url =>
            {
                _currentUrl = url;
                CompleteLoadTcs(success: true);
            };
            win.OnLoadError += (code, text, failedUrl) =>
            {
                _currentUrl = failedUrl;
                CompleteLoadTcs(success: false);
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
            lock (_gate) tcs = _loadTcs;

            if (tcs == null)
                return true;

            try
            {
                if (tcs.Task.Wait(timeout))
                    return tcs.Task.Result;
                return false;
            }
            catch
            {
                return false;
            }
        }

        [ScriptMember]
        public void load(string url)
        {
            _currentUrl = url;
            ResetLoadTcs();
            WindowOrThrow().LoadUrlAsync(url).GetAwaiter().GetResult();
        }

        [ScriptMember]
        public void run(string js, string? callbackId = null, ScriptObject? callback = null)
        {
            waitTillLoaded();

            if (!string.IsNullOrEmpty(callbackId) && callback != null)
                _callbacks[callbackId] = callback;

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
            waitTillLoaded();

            var result = EvaluateWithReturnBlocking(js);

            if (callback != null)
                InvokeOnV8(() => callback.Invoke(false, result));
        }

        private void ResetLoadTcs()
        {
            lock (_gate)
            {
                _loadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private void CompleteLoadTcs(bool success)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_gate) tcs = _loadTcs;
            tcs?.TrySetResult(success);
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
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var id = root.GetProperty("id").GetString();
                var result = root.GetProperty("result").GetString();

                if (string.IsNullOrEmpty(id))
                    return;

                if (_callbacks.TryRemove(id, out var cb))
                    InvokeOnV8(() => cb.Invoke(false, result));
            }
            catch
            {
                // ignore malformed callback payloads
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

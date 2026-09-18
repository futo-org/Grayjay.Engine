using Grayjay.Engine.Exceptions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Additions
{
    public class RequestExecutor
    {
        private PluginConfig _config;
        private GrayjayPlugin _plugin;
        private IJavaScriptObject _executor;

        [V8Property("urlPrefix", true)]
        public string UrlPrefix { get; set; }

        public bool HasCleanup { get; private set; }

        public bool DidCleanup { get; private set; }

        public RequestExecutor(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _executor = obj;
            _plugin = plugin;
            _config = plugin.Config;

            if (!obj.HasFunction("executeRequest"))
                throw new ScriptImplementationException(plugin.Config, "RequestExecutor is missing executeRequest");

            HasCleanup = obj.HasFunction("cleanup");
        }

        public byte[] ExecuteRequest(string url, Dictionary<string, string> headers)
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor object is closed");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {

                var result = _executor.InvokeV8(_config, "executeRequest", url, MarshalHeaders(headers));
                return HandleExecuteRequestResult(result);
            }
            finally
            {
                stopwatch.Stop();
                Logger.Info<RequestExecutor>("RequestExecutor executeRequest finished in " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }

        public byte[] ExecuteRequest(string url, Dictionary<string, string> headers, string method, byte[] body)
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor object is closed");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                object jsBody = null;
                if (body != null)
                {
                    var typedArrayObj = GetEngineOrThrow().Evaluate("new Uint8Array(" + body.Length.ToString() + ")");
                    if (!(typedArrayObj is ITypedArray typedBody))
                        throw new InvalidOperationException("Expected a Uint8Array, but got " + typedArrayObj?.GetType()?.ToString());
                    if (body.Length != 0)
                        typedBody.ArrayBuffer.WriteBytes(body, 0, (ulong)body.Length, 0);
                    jsBody = typedArrayObj;
                }

                var result = _executor.InvokeV8(_config, "executeRequest", url, MarshalHeaders(headers), method, jsBody);
                return HandleExecuteRequestResult(result);
            }
            finally
            {
                stopwatch.Stop();
                Logger.Info<RequestExecutor>("RequestExecutor executeRequest finished in " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }

        private object MarshalHeaders(Dictionary<string, string> headers)
        {
            object jsHeaders = headers;
            if (headers != null && GetEngineOrThrow().Evaluate("({})") is IScriptObject headerObject)
            {
                foreach (var pair in headers)
                    headerObject.SetProperty(pair.Key, pair.Value);
                jsHeaders = headerObject;
            }
            return jsHeaders;
        }

        private V8ScriptEngine GetEngineOrThrow()
        {
            return _plugin.GetUnderlyingEngine()
                ?? throw new InvalidOperationException("Executor object is closed");
        }

        private byte[] HandleExecuteRequestResult(object result)
        {
            if (result is byte[] rawBytes)
            {
                return rawBytes;
            }
            if (result is string str)
            {
                return Convert.FromBase64String(str);
            }
            if (result is ITypedArray typedArray)
            {
                return ReadTypedArray(typedArray);
            }
            if (result is IArrayBuffer arrayBuffer)
            {
                return ReadArrayBuffer(arrayBuffer);
            }
            throw new ScriptImplementationException(_config,
                $"executeRequest returned an unsupported result type [{result?.GetType().FullName ?? "null"}]");
        }

        private static byte[] ReadTypedArray(ITypedArray typedArray)
        {
            return typedArray.Size == 0 ? Array.Empty<byte>() : typedArray.GetBytes();
        }

        private static byte[] ReadArrayBuffer(IArrayBuffer buffer)
        {
            return buffer.Size == 0 ? Array.Empty<byte>() : buffer.GetBytes();
        }

        public virtual void Cleanup()
        {
            DidCleanup = true;
            if (!HasCleanup || _executor == null)
                return;

            try
            {
                _executor.InvokeV8(_config, "cleanup");
            }
            catch(InvalidOperationException ex)
            {
                //Already cleaned up?
            }
            finally { }
        }

        ~RequestExecutor()
        {
            Cleanup();
        }
    }

}

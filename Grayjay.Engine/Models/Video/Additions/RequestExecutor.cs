using Grayjay.Engine.Exceptions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Additions
{
    public class RequestExecutor
    {
        private IJavaScriptObject _executor;

        [V8Property("urlPrefix", true)]
        public string UrlPrefix { get; set; }

        public bool HasCleanup { get; private set; }

        public bool DidCleanup { get; private set; }

        public RequestExecutor(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _executor = obj;

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

                var result = _executor.InvokeMethod("executeRequest", url, headers);

                if (result is string str)
                {
                    var base64Result = Convert.FromBase64String(str);
                    return base64Result;
                }
                else if (result is ITypedArray typedArray)
                {
                    var buffer = typedArray.ArrayBuffer;
                    byte[] data = new byte[buffer.Size];
                    buffer.ReadBytes(0, buffer.Size, data, 0);
                    return data;
                }
                else if (result is IArrayBuffer buffer)
                {
                    byte[] data = new byte[buffer.Size];
                    buffer.ReadBytes(0, buffer.Size, data, 0);
                    return data;
                }
                else
                    throw new NotImplementedException();
            }
            finally
            {
                stopwatch.Stop();
                Logger.Info<RequestExecutor>("RequestExecutor executeRequest finished in " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }

        public virtual void Cleanup()
        {
            DidCleanup = true;
            if (!HasCleanup || _executor == null)
                return;

            try
            {
                _executor.InvokeMethod("cleanup");
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

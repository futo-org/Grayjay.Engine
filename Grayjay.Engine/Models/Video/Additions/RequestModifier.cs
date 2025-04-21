using Grayjay.Engine.Exceptions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Additions
{
    public class RequestModifier
    {
        private GrayjayPlugin _plugin;
        private IJavaScriptObject _modifier;

        [V8Property("allowByteSkip", true)]
        public bool AllowByteSkip { get; set; }

        public RequestModifier(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _plugin = plugin;
            _modifier = obj;

            if (!obj.HasFunction("modifyRequest"))
                throw new ScriptImplementationException(plugin.Config, "RequestModifier is missing modifyRequest");
        }

        public Request ModifyRequest(string url, Dictionary<string, string> headers)
        {
            if (_modifier == null)
                return new Request(url, headers);

            var result = _modifier.InvokeMethod("modifyRequest", url, headers);

            return V8Converter.ConvertValue<Request>(_plugin, result);
        }
    }

    public class Request
    {
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("headers")]
        public Dictionary<string, string> Headers { get; set; }

        public Request(string url, Dictionary<string, string> headers)
        {
            Url = url;
            Headers = headers;
        }
    }
}

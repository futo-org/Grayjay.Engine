using Grayjay.Engine.Models.Video.Additions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface IJSSource
    {
        public string Type { get; }
    }
    public abstract class JSSource : IJSSource
    {
        protected GrayjayPlugin _plugin { get; private set; }
        protected IJavaScriptObject _obj;
        public abstract string Type { get; }

        public bool HasRequestModifier { get; private set; }
        private Request _requestModifier = null;

        public bool HasRequestExecutor { get; private set; }

        public virtual bool CanSerialize { get; } = true;

        public JSSource()
        {
            _obj = null;
            _plugin = null;
            HasRequestModifier = false;
            HasRequestExecutor = false;
        }
        public JSSource(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _obj = obj;
            _plugin = plugin;

            _requestModifier = obj.GetOrDefault<Request>(plugin, "requestModifier", nameof(JSSource), null);
            HasRequestModifier = _requestModifier != null || obj.HasFunction("getRequestModifier");
            HasRequestExecutor = obj.HasFunction("getRequestExecutor");
        }

        public virtual IRequestModifier GetRequestModifier()
        {
            if (_obj == null)
                return null;

            if (_requestModifier != null)
                return new AdhocRequestModifier((url, headers) =>
                {
                    return _requestModifier.Modify(_plugin, url, headers);
                });

            if (!HasRequestModifier || _obj == null)
                return null;

            var result = _obj.InvokeV8(_plugin.Config, "getRequestModifier");
            if (result is IJavaScriptObject)
                return V8Converter.ConvertValue<RequestModifier>(_plugin, result);
            else
                return null;
        }

        public RequestExecutor GetRequestExecutor()
        {
            if (!HasRequestExecutor || _obj == null)
                return null;

            var result = _obj.InvokeV8(_plugin.Config, "getRequestExecutor");
            if (result is IJavaScriptObject)
                return V8Converter.ConvertValue<RequestExecutor>(_plugin, result);
            else
                return null;
        }


        public GrayjayPlugin GetUnderlyingPlugin()
        {
            return _plugin;
        }
        public IJavaScriptObject GetUnderlyingObject()
        {
            return _obj;
        }
    }
}

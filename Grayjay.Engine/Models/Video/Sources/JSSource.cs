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
        public GrayjayPlugin Plugin { get; private set; }
        protected IJavaScriptObject _obj;
        public abstract string Type { get; }

        public bool HasRequestModifier { get; private set; }
        public bool HasRequestExecutor { get; private set; }

        public virtual bool CanSerialize { get; } = true;

        public JSSource(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _obj = obj;
            Plugin = plugin;

            HasRequestModifier = obj.HasFunction("getRequestModifier");
            HasRequestExecutor = obj.HasFunction("getRequestExecutor");
        }

        public RequestModifier GetRequestModifier()
        {
            if (!HasRequestModifier || _obj == null)
                return null;

            var result = _obj.InvokeMethod("getRequestModifier");
            if (result is IJavaScriptObject)
                return V8Converter.ConvertValue<RequestModifier>(Plugin, result);
            else
                return null;
        }

        public RequestExecutor GetRequestExecutor()
        {
            if (!HasRequestExecutor || _obj == null)
                return null;

            var result = _obj.InvokeMethod("getRequestExecutor");
            if (result is IJavaScriptObject)
                return V8Converter.ConvertValue<RequestExecutor>(Plugin, result);
            else
                return null;
        }


        public IJavaScriptObject GetUnderlyingObject()
        {
            return _obj;
        }
    }
}

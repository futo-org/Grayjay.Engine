using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Exceptions
{
    public class ScriptCaptchaRequiredException : ScriptException
    {
        public string Url { get; set; }
        public string Body { get; set; }
        public ScriptCaptchaRequiredException(PluginConfig config, string error, string url, string body, Exception ex = null, string stack = null, string code = null) : base(config, error, ex, stack, code)
        {
            Url = url;
            Body = body;
        }

        public static ScriptCaptchaRequiredException FromV8(GrayjayPlugin plugin, ScriptEngineException ex, IJavaScriptObject obj)
        {
            return new ScriptCaptchaRequiredException(plugin.Config, "Captcha required",
                obj.GetOrDefault<string>(plugin, "url", nameof(ScriptCaptchaRequiredException), null),
                obj.GetOrDefault<string>(plugin, "body", nameof(ScriptCaptchaRequiredException), null));
        }
    }
}

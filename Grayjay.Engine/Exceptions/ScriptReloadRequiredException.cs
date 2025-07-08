using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace Grayjay.Engine.Exceptions
{
    public class ScriptReloadRequiredException : ScriptException
    {
        public string ReloadData { get; set; }

        public ScriptReloadRequiredException(PluginConfig config, string error, string reloadData, Exception? ex = null, string? stack = null, string? code = null) : base(config, error, ex, stack, code)
        {
            ReloadData = reloadData;
        }


        public static ScriptReloadRequiredException FromV8(GrayjayPlugin plugin, ScriptEngineException ex, IJavaScriptObject obj)
        {
            string msg = obj.GetOrDefault<string>(plugin, "message", "ScriptReloadRequiredException", "Reload required");
            string reloadData = obj.GetOrDefault<string>(plugin, "reloadData", "ScriptReloadRequiredException", "");

            return new ScriptReloadRequiredException(plugin.Config, msg, reloadData, ex);
        }
    }
}

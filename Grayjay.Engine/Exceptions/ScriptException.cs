using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Exceptions
{
    public class ScriptException: PluginException
    {
        public string Stack { get; private set; }
        public string Code { get; private set; }

        public ScriptException(PluginConfig config, string error, Exception? ex = null, string? stack = null, string? code = null) : base(config, error, ex)
        {
            Stack = stack;
            Code = code;
        }
    }
}

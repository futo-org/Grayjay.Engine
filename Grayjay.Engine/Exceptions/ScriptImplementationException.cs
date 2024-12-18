using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Exceptions
{
    public class ScriptImplementationException : ScriptException
    {
        public ScriptImplementationException(PluginConfig config, string error, Exception ex = null, string stack = null, string code = null) : base(config, error, ex, stack, code)
        {
        }
    }
}

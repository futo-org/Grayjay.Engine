using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Exceptions
{
    public class PluginException: Exception
    {
        public PluginConfig Config { get; private set; }

        public PluginException(PluginConfig config, string ex, Exception inner = null): base(ex, inner)
        {
            Config = config;
        }
    }
}

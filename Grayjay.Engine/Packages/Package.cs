using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Packages
{
    public abstract class Package: IDisposable
    {
        protected GrayjayPlugin _plugin;

        public Package(GrayjayPlugin plugin)
        {
            _plugin = plugin;
        }

        public abstract void Initialize(V8ScriptEngine engine);

        public abstract void Dispose();
    }
}

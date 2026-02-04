using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Packages
{
    public abstract class Package: IDisposable
    {
        protected GrayjayPlugin _plugin;

        public abstract string Name { get; }
        public virtual string VariableName { get; } = null;

        public Package(GrayjayPlugin plugin)
        {
            _plugin = plugin;
        }

        public abstract void Initialize(V8ScriptEngine engine);

        public abstract void Dispose();
    }
}

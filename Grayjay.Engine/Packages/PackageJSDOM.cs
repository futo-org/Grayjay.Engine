using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public class PackageJSDOM : Package
    {
        public override string VariableName => "packageJSDOM";

        public PackageJSDOM(GrayjayPlugin plugin) : base(plugin)
        {
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.Execute(Resources.ScriptJSDOM);
        }

        public override void Dispose()
        {

        }
    }
}

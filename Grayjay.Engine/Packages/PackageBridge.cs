using Grayjay.Engine.Web;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public class PackageBridge: Package
    {
        public override string VariableName => "bridge";

        private Action<string> _onLog;

        private GrayjayPlugin _plugin;

        [ScriptMember("buildVersion")]
        public int buildVersion => 282;
        [ScriptMember("buildFlavor")]
        public string buildFlavor => "desktopStable";
        [ScriptMember("buildSpecVersion")]
        public int buildSpecVersion => 2;

        public PackageBridge(GrayjayPlugin plugin, Action<string> onLog) : base(plugin)
        {
            _plugin = plugin;
            _onLog = onLog;
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject("bridge", this);
        }

        public override void Dispose()
        {

        }


        [ScriptMember]
        public void toast(string str)
        {
            Console.WriteLine("Toast:" + str);
            _plugin?.TriggerToast(str);
        }


        [ScriptMember]
        public void log(string str)
        {
            _onLog?.Invoke(str);
        }


        [ScriptMember]
        public bool isLoggedIn()
        {
            return _plugin.Descriptor.HasLoggedIn;
        }

    }
}

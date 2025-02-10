using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Grayjay.Engine
{
    public static class Resources
    {
        public static string ScriptPolyfil { get; private set; } = ReadStringResource("ScriptDeps.polyfil.js");
        public static string ScriptSource { get; private set; } = ReadStringResource("ScriptDeps.source.js");
        public static string ScriptJSDOM { get; private set; } = ReadStringResource("ScriptDeps.JSDOM.js");


        public static string[] GetResourceNames()
        {
            return typeof(Resources).Assembly.GetManifestResourceNames();
        }


        public static string ReadStringResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Grayjay.Engine" + "." + name;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();
                return result;
            }
        }
    }
}

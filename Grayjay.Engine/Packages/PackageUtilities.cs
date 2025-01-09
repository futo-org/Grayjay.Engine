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
    public class PackageUtilities : Package
    {
        public override string VariableName => "utility";

        public PackageUtilities(GrayjayPlugin plugin) : base(plugin)
        {
        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject("utility", this);
        }

        public override void Dispose()
        {

        }

        [ScriptMember("toBase64")]
        public string? toBase64(object? input = null)
        {
            if (input == null || input is Undefined)
                return null;
                
            return Convert.ToBase64String((byte[])V8Converter.ConvertValue(_plugin, typeof(byte[]), input));
        }
        [ScriptMember("fromBase64")]
        public byte[]? fromBase64(object? base64 = null)
        {
            if (base64 == null)
                return null;
            return Convert.FromBase64String(base64 as string);
        }

        [ScriptMember("md5")]
        public object md5(object? input = null)
        {
            if (input == null || input is Undefined)
                return null;

            byte[] data = (byte[])input;

            using (MD5 md5 = MD5.Create())
                return md5.ComputeHash(data).ToScriptArray();
        }
        [ScriptMember("md5String")]
        public string md5String(string str)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);
            using(MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                     sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }


        [ScriptMember("randomUUID")]
        public string randomUUID()
        {
            return Guid.NewGuid().ToString();
        }
    }
}

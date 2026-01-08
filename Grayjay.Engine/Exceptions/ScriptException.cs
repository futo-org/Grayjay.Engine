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

        public override string? StackTrace
        {
            get
            {
                var net = base.StackTrace;

                if (string.IsNullOrWhiteSpace(Stack))
                    return net;

                if (string.IsNullOrWhiteSpace(net))
                    return "----- JavaScript stack -----\n" + Stack;

                return net + "\n\n----- JavaScript stack -----\n" + Stack;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(base.ToString());

            if (!string.IsNullOrWhiteSpace(Stack))
            {
                sb.AppendLine();
                sb.AppendLine("----- JavaScript stack -----");
                sb.AppendLine(Stack);
            }

            if (!string.IsNullOrWhiteSpace(Code))
            {
                sb.AppendLine();
                sb.AppendLine("----- Script -----");
                sb.AppendLine(Code);
            }

            return sb.ToString();
        }
    }
}

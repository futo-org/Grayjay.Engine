using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.General
{
    public class PlatformID
    {
        [V8Property("platform")]
        public string Platform { get; set; }
        [V8Property("value", true)]
        public string Value { get; set; }
        [V8Property("pluginId", true)]
        public string PluginID { get; set; }
        [V8Property("claimType", true)]
        public int ClaimType { get; set; }
        [V8Property("claimFieldType", true)]
        public int ClaimFieldType { get; set; }


        public override bool Equals(object obj)
        {
            if(obj is PlatformID id)
                return id.Platform == this.Platform && id.Value == this.Value;
            return false;
        }
    }
}

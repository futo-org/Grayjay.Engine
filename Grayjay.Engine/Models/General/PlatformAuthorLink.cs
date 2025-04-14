using Grayjay.Engine.Serializers;
using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.General
{
    public class PlatformAuthorLink
    {
        [V8Property("id")]
        public PlatformID ID { get; set; }
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("thumbnail", true)]
        public string Thumbnail { get; set; }
        [V8Property("subscribers", true)]
        [JsonConverter(typeof(NullableDefaultLongConverter))]
        public long Subscribers { get; set; }
    }
}

using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Channel
{
    public class PlatformChannel
    {
        [V8Property("id")]
        public General.PlatformID ID { get; set; }
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("thumbnail", true)]
        public string? Thumbnail { get; set; }
        [V8Property("banner", true)]
        public string? Banner { get; set; }
        [V8Property("subscribers")]
        public int Subscribers { get; set; }
        [V8Property("description", true)]
        public string? Description { get; set; }
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("urlAlternatives", true)]
        public List<string> UrlAlternatives { get; set; } = new List<string>();
        [V8Property("links", true)]
        public Dictionary<string, string> Links { get; set; } = new Dictionary<string, string>();


        public bool IsSameUrl(string url)
        {
            return Url == url || UrlAlternatives.Contains(url);
        }
    }
}

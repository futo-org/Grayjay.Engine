using System.Text.Json.Serialization;
using Grayjay.Engine.Pagers;
using Grayjay.Engine.V8;
using Grayjay.Engine.Models.General;

namespace Grayjay.Engine.Models.Feed
{
    //TODO: This should inherit from PlatformPlaylist
    public class PlatformPlaylistDetails
    {
        
        [V8Property("id")]
        public PlatformID ID { get; set; }

        [V8Property("name")]
        public string Name { get; set; }

        [V8Property("thumbnail", true)]
        public string Thumbnail { get; set; }

        [V8Property("author")]
        public PlatformAuthorLink Author { get; set; }

        [V8Property("datetime", true)]
        [JsonConverter(typeof(UnixSupportedDateTimeConverter))]
        public System.DateTime DateTime { get; set; }

        [V8Property("url")]
        public string Url { get; set; }

        [V8Property("shareUrl", true)]
        public string ShareUrl { get; set; }

        [V8Property("videoCount")]
        public int VideoCount { get; set; }

        [V8Property("contents", true)]
        public V8Pager<PlatformContent> Contents { get; set; }
    }
}

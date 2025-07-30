using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformVideo : PlatformContent
    {
        public override ContentType ContentType { get; } = Models.ContentType.MEDIA;

        [V8Property("thumbnails")]
        public virtual Thumbnails Thumbnails { get; set; }
        [V8Property("duration")]
        public virtual long Duration { get; set; }
        [V8Property("viewCount")]
        public virtual long ViewCount { get; set; }

        [V8Property("isLive")]
        public virtual bool IsLive { get; set; }

        [V8Property("playbackDate", true)]
        public DateTime? PlaybackDate { get; set; }
        [V8Property("playbackTime", true)]
        public long PlaybackTime { get; set; }


        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();


        public PlatformVideo(IJavaScriptObject obj): base(obj)
        {
            
        }
        public PlatformVideo() : base(null)
        {

        }

        public PlatformVideo AddMetadata(string name, object value)
        {
            Metadata[name] = value;
            return this;
        }
    }
}

using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class HLSManifestAudioSource: IAudioSource
    {
        public string Type => "HLSSource";

        public string Container { get; } = "application/vnd.apple.mpegurl";
        public string Codec { get; } = "HLS";
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("duration", true)]
        public int Duration { get; set; }
        [V8Property("bitrate", true)]
        public int Bitrate { get; set; }
        [V8Property("language", true)]
        public string Language { get; set; }
        [V8Property("priority", true)]
        public bool Priority { get; set; }
        [V8Property("original", true)]
        public bool Original { get; set; }

    }
}

using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class HLSManifestSource: JSSource, IVideoSource
    {
        public override string Type => "HLSSource";

        [V8Property("width", true)]
        public int Width { get; set; }
        [V8Property("height", true)]
        public int Height { get; set; }
        public string Container { get; } = "application/vnd.apple.mpegurl";
        public string Codec { get; } = "HLS";
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("duration", true)]
        public int Duration { get; set; }
        [V8Property("priority", true)]
        public bool Priority { get; set; }

        [V8Property("original", true)]
        public bool Original { get; set; } = false;
        [V8Property("language", true)]
        public string Language { get; set; } = null;

        public HLSManifestSource() { }
        public HLSManifestSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj) { }
    }
}

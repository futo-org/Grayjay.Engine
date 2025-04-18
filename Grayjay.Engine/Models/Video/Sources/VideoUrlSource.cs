using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class VideoUrlSource: JSSource, IVideoSource
    {
        public override string Type => "VideoUrlSource";

        [V8Property("width")]
        public int Width { get; set; }
        [V8Property("height")]
        public int Height { get; set; }
        [V8Property("container")]
        public string Container { get; set; }
        [V8Property("codec")]
        public string Codec { get; set; }
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("bitrate")]
        public int Bitrate { get; set; }
        [V8Property("duration")]
        public int Duration { get; set; }
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("priority", true)]
        public bool Priority { get; set; }

        public VideoUrlSource() { }
        public VideoUrlSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj)
        {

        }
    }
}

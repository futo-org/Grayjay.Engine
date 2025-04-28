using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class AudioUrlSource: JSSource, IAudioSource
    {
        public override string Type => "AudioUrlSource";

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
        [V8Property("language")]
        public string Language { get; set; }

        [V8Property("priority", true)]
        public bool Priority { get; set; }

        [V8Property("original", true)]
        public bool Original { get; set; }


        public AudioUrlSource() : base() { }
        public AudioUrlSource(GrayjayPlugin plugin, IJavaScriptObject obj): base(plugin, obj)
        {

        }
    }
}

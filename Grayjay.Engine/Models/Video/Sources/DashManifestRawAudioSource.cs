using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class DashManifestRawAudioSource: JSSource, IAudioSource, IDashManifestRawSource
    {
        public override string Type => "DashRawAudioSource";
        public override bool CanSerialize => false;

        public string Container { get; } = "application/dash+xml";

        public bool HasGenerate { get; private set; }


        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }

        [V8Property("codec")]
        public string Codec { get; set; }
        [V8Property("bitrate", true)]
        public int Bitrate { get; set; }

        [V8Property("duration", true)]
        public int Duration { get; set; }

        [V8Property("priority", true)]
        public bool Priority { get; set; }

        [V8Property("language", true)]
        public string Language { get; set; }

        [V8Property("manifest", true)]
        public string Manifest { get; set; }


        [V8Property("initStart", true)]
        public int InitStart { get; set; }
        [V8Property("initEnd", true)]
        public int InitEnd { get; set; }
        [V8Property("indexStart", true)]
        public int IndexStart { get; set; }
        [V8Property("indexEnd", true)]
        public int IndexEnd { get; set; }

        public StreamMetaData MetaData => new StreamMetaData()
        {
            FileInitStart = InitStart,
            FileInitEnd = InitEnd,
            FileIndexStart = IndexStart,
            FileIndexEnd = IndexEnd
        };

        public DashManifestRawAudioSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj)
        {
            HasGenerate = obj.HasFunction("generate");
        }

        public virtual string Generate()
        {
            if (!HasGenerate)
                return Manifest;
            if (_obj == null)
                throw new InvalidOperationException("Source object already closed");

            var result = _obj.InvokeMethod("generate");
            if (result is string str)
            {
                InitStart = _obj.GetOrDefault<int>(Plugin, "initStart", nameof(DashManifestRawSource), InitStart);
                InitEnd = _obj.GetOrDefault<int>(Plugin, "initEnd", nameof(DashManifestRawSource), InitEnd);
                IndexStart = _obj.GetOrDefault<int>(Plugin, "indexStart", nameof(DashManifestRawSource), IndexStart);
                IndexEnd = _obj.GetOrDefault<int>(Plugin, "indexEnd", nameof(DashManifestRawSource), IndexEnd);

                return str;
            }
            else throw new NotImplementedException("Unsupported generate type: " + (result?.GetType()?.ToString() ?? ""));
        }

    }
}

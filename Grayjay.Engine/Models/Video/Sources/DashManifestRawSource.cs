using Grayjay.Engine.Models.Video.Additions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface IDashManifestRawSource
    {
        RequestModifier GetRequestModifier();
        RequestExecutor GetRequestExecutor();
        string Generate();


        public int InitStart { get; set; }
        public int InitEnd { get; set; }
        public int IndexStart { get; set; }
        public int IndexEnd { get; set; }
        public bool HasStreamMetadata => IndexStart > 0 && IndexEnd > 0 && InitEnd > 0;
    }

    public class DashManifestRawSource: JSSource, IVideoSource, IDashManifestRawSource, IStreamMetaDataSource
    {
        public override string Type => "DashRawSource";
        public override bool CanSerialize => false;

        public string Container { get; } = "application/dash+xml";


        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }

        [V8Property("width")]
        public int Width { get; set; }
        [V8Property("height")]
        public int Height { get; set; }

        [V8Property("codec")]
        public string Codec { get; set; }
        [V8Property("bitrate", true)]
        public int Bitrate { get; set; }

        [V8Property("duration", true)]
        public int Duration { get; set; }

        [V8Property("priority", true)]
        public bool Priority { get; set; }

        [V8Property("manifest", true)]
        public string Manifest { get; set; }

        public bool HasGenerate { get; private set; }


        [V8Property("initStart", true)]
        public int InitStart { get; set; }
        [V8Property("initEnd", true)]
        public int InitEnd { get; set; }
        [V8Property("indexStart", true)]
        public int IndexStart { get; set; }
        [V8Property("indexEnd", true)]
        public int IndexEnd { get; set; }

        public bool HasStreamMetadata => IndexStart > 0 && IndexEnd > 0 && InitEnd > 0;

        public StreamMetaData MetaData => new StreamMetaData()
        {
            FileInitStart = InitStart,
            FileInitEnd = InitEnd,
            FileIndexStart = IndexStart,
            FileIndexEnd = IndexEnd
        };

        public DashManifestRawSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj)
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
            if (result is string str) {
                InitStart = _obj.GetOrDefault<int>(Plugin, "initStart", nameof(DashManifestRawSource), InitStart);
                InitEnd = _obj.GetOrDefault<int>(Plugin, "initEnd", nameof(DashManifestRawSource), InitEnd);
                IndexStart = _obj.GetOrDefault<int>(Plugin, "indexStart", nameof(DashManifestRawSource), IndexStart);
                IndexEnd = _obj.GetOrDefault<int>(Plugin, "indexEnd", nameof(DashManifestRawSource), IndexEnd);

                return str;
            }
            else throw new NotImplementedException("Unsupported generate type: " + (result?.GetType()?.ToString() ?? ""));
        }

    }
    public class DashManifestMergingRawSource : DashManifestRawSource
    {
        public string Type => "DashRawSource";
        public string Container { get; } = "application/dash+xml";

        public DashManifestRawSource Video { get; private set; }
        public DashManifestRawAudioSource Audio { get; private set; }


        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url")]
        public string Url { get; set; }

        [V8Property("width")]
        public int Width { get; set; }
        [V8Property("height")]
        public int Height { get; set; }

        [V8Property("codec")]
        public string Codec { get; set; }
        [V8Property("bitrate", true)]
        public int Bitrate { get; set; }

        [V8Property("duration", true)]
        public int Duration { get; set; }

        [V8Property("priority", true)]
        public bool Priority { get; set; }


        public DashManifestMergingRawSource(DashManifestRawSource video, DashManifestRawAudioSource audio) : base(video.Plugin, video.GetUnderlyingObject())
        {
            this.Video = video;
            this.Audio = audio;
        }

        public override string Generate()
        {
            Stopwatch genWatch = Stopwatch.StartNew();
            string videoDash = null;
            string audioDash = null;
            Task.WaitAll(
                Task.Run(() => { videoDash = Video.Generate(); }),
                Task.Run(() => { audioDash = Audio?.Generate(); }));

            if (videoDash != null && audioDash == null) return videoDash;
            if (audioDash != null && videoDash == null) return audioDash;
            if (videoDash == null) return null;

            genWatch.Stop();
            Logger.Info<DashManifestRawSource>("Generated in: " + genWatch.Elapsed.TotalMilliseconds + "ms");

            var audioAdaptationSet = ADAPTATION_SET_REGEX.Match(audioDash);
            if (audioAdaptationSet != null && audioAdaptationSet.Success)
                return videoDash.Replace("</AdaptationSet>", "</AdaptationSet>\n" + audioAdaptationSet.Value);
            
            return audioDash;
        }


        private static Regex ADAPTATION_SET_REGEX = new Regex("<AdaptationSet.*?>.*?<\\/AdaptationSet>", RegexOptions.Singleline);
    }
}

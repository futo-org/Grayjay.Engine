using Grayjay.Engine.Exceptions;
using Grayjay.Engine.Models.Video.Additions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface IDashManifestRawSource
    {
        IRequestModifier GetRequestModifier();
        RequestExecutor GetRequestExecutor();
        string Generate();
        Task<string> GenerateAsync(out V8PromiseMetadata promiseMeta);


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

            var result = _obj.InvokeV8("generate");
            if (result is string str) {
                InitStart = _obj.GetOrDefault<int>(_plugin, "initStart", nameof(DashManifestRawSource), InitStart);
                InitEnd = _obj.GetOrDefault<int>(_plugin, "initEnd", nameof(DashManifestRawSource), InitEnd);
                IndexStart = _obj.GetOrDefault<int>(_plugin, "indexStart", nameof(DashManifestRawSource), IndexStart);
                IndexEnd = _obj.GetOrDefault<int>(_plugin, "indexEnd", nameof(DashManifestRawSource), IndexEnd);

                return str;
            }
            else throw new NotImplementedException("Unsupported generate type: " + (result?.GetType()?.ToString() ?? ""));
        }
        public virtual Task<string> GenerateAsync(out V8PromiseMetadata promiseMeta)
        {
            if (!HasGenerate)
            {
                promiseMeta = null;
                return Task.FromResult(Manifest);
            }
            if (_obj == null)
                throw new InvalidOperationException("Source object already closed");

            var task = _obj.InvokeV8Async("generate", out promiseMeta);

            return task.ContinueWith((t) =>
            {
                var result = task.Result;
                if (result is string str)
                {
                    InitStart = _obj.GetOrDefault<int>(_plugin, "initStart", nameof(DashManifestRawSource), InitStart);
                    InitEnd = _obj.GetOrDefault<int>(_plugin, "initEnd", nameof(DashManifestRawSource), InitEnd);
                    IndexStart = _obj.GetOrDefault<int>(_plugin, "indexStart", nameof(DashManifestRawSource), IndexStart);
                    IndexEnd = _obj.GetOrDefault<int>(_plugin, "indexEnd", nameof(DashManifestRawSource), IndexEnd);

                    return str;
                }
                else throw new NotImplementedException("Unsupported generate type: " + (result?.GetType()?.ToString() ?? ""));
            });
        }

    }
    public class DashManifestMergingRawSource : DashManifestRawSource
    {
        public string Type => "DashRawSource";
        public string Container { get; } = "application/dash+xml";

        public DashManifestRawSource Video { get; private set; }
        public DashManifestRawAudioSource Audio { get; private set; }
        public ISubtitleSource Subtitles { get; private set; }


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


        public DashManifestMergingRawSource(DashManifestRawSource video, DashManifestRawAudioSource audio, ISubtitleSource source = null) : base(video.GetUnderlyingPlugin(), video.GetUnderlyingObject())
        {
            this.Video = video;
            this.Audio = audio;
            this.Subtitles = source;
        }

        public override string Generate()
        {
            Stopwatch genWatch = Stopwatch.StartNew();
            string videoDash = null;
            string audioDash = null;
            try
            {
                Task.WaitAll(
                    Task.Run(() => { videoDash = Video.Generate(); }),
                    Task.Run(() => { audioDash = Audio?.Generate(); }));
            }
            catch(AggregateException exs)
            {
                var reloadReq = exs.InnerExceptions.FirstOrDefault(x => x is ScriptReloadRequiredException);
                if (reloadReq != null)
                    throw reloadReq;
                throw;
            }
            if((videoDash != null || audioDash != null) && Subtitles != null && !string.IsNullOrEmpty(Subtitles.Url))
            {
                string dashToChange = videoDash ?? audioDash;
                var lastAdaptationSet = ADAPTATION_SET_REGEX.Match(dashToChange);
                if (lastAdaptationSet != null && lastAdaptationSet.Success)
                {
                    dashToChange = dashToChange.Replace("</AdaptationSet>", "</AdaptationSet>" + $@"
<AdaptationSet mimeType=""{Subtitles.Format}"" lang=""en""> 
    <Representation id=""99"" bandwidth=""123"">
        <BaseURL>{Subtitles.Url.Replace("&", "&amp;")}</BaseURL> 
    </Representation>
</AdaptationSet>
");

                    if (videoDash != null)
                        videoDash = dashToChange;
                    else
                        audioDash = dashToChange;
                }
            }

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
        public override Task<string?> GenerateAsync(out V8PromiseMetadata promiseMeta)
        {
            Stopwatch genWatch = Stopwatch.StartNew();

            Task<string> videoDashTask = null;
            Task<string> audioDashTask = null;
            V8PromiseMetadata videoMetadata = null;
            V8PromiseMetadata audioMetadata = null;
            try
            {
                videoDashTask = Video.GenerateAsync(out videoMetadata);
                audioDashTask = Audio?.GenerateAsync(out audioMetadata);
            }
            catch (AggregateException exs)
            {
                var reloadReq = exs.InnerExceptions.FirstOrDefault(x => x is ScriptReloadRequiredException);
                if (reloadReq != null)
                    throw reloadReq;
                throw;
            }

            if (videoMetadata != null)
                promiseMeta = videoMetadata;
            else if (audioMetadata != null)
                promiseMeta = audioMetadata;
            else
                promiseMeta = null;

            string videoDash = null;
            string audioDash = null;

            return Task.WhenAll(new Task[]
            {
                videoDashTask,
                audioDashTask
            }.Where(x=>x != null)).ContinueWith(t =>
            {
                try
                {
                    videoDash = videoDashTask?.Result;
                    audioDash = audioDashTask?.Result;
                }
                catch (AggregateException exs)
                {
                    var reloadReq = exs.InnerExceptions.FirstOrDefault(x => x is ScriptReloadRequiredException);
                    if (reloadReq != null)
                        throw reloadReq;
                    throw;
                }
                if ((videoDash != null || audioDash != null) && Subtitles != null && !string.IsNullOrEmpty(Subtitles.Url))
                {
                    string dashToChange = videoDash ?? audioDash;
                    var lastAdaptationSet = ADAPTATION_SET_REGEX.Match(dashToChange);
                    if (lastAdaptationSet != null && lastAdaptationSet.Success)
                    {
                        dashToChange = dashToChange.Replace("</AdaptationSet>", "</AdaptationSet>" + $@"
<AdaptationSet mimeType=""{Subtitles.Format}"" lang=""en""> 
    <Representation id=""99"" bandwidth=""123"">
        <BaseURL>{Subtitles.Url.Replace("&", "&amp;")}</BaseURL> 
    </Representation>
</AdaptationSet>
");

                        if (videoDash != null)
                            videoDash = dashToChange;
                        else
                            audioDash = dashToChange;
                    }
                }

                if (videoDash != null && audioDash == null) return videoDash;
                if (audioDash != null && videoDash == null) return audioDash;
                if (videoDash == null) return null;

                genWatch.Stop();
                Logger.Info<DashManifestRawSource>("Generated in: " + genWatch.Elapsed.TotalMilliseconds + "ms");

                var audioAdaptationSet = ADAPTATION_SET_REGEX.Match(audioDash);
                if (audioAdaptationSet != null && audioAdaptationSet.Success)
                    return videoDash.Replace("</AdaptationSet>", "</AdaptationSet>\n" + audioAdaptationSet.Value);

                return audioDash;
            });
        }


        private static Regex ADAPTATION_SET_REGEX = new Regex("<AdaptationSet.*?>.*?<\\/AdaptationSet>", RegexOptions.Singleline);
    }
}

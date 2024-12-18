using Grayjay.Engine.Models.Subtitles;
using Grayjay.Engine.Models.Video.Sources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Grayjay.Engine.Dash
{
    public class DashBuilder : XMLBuilder
    {

        public static Regex REGEX_REPRESENTATION = new Regex("<Representation .*?mimeType=\"(.*?)\".*?>(.*?)<\\/Representation>", RegexOptions.Singleline);
        public static Regex REGEX_MEDIA_INITIALIZATION = new Regex("(media|initiali[sz]ation)=\"([^\"]+)\"", RegexOptions.Singleline);


        public static string PROFILE_MAIN = "urn:mpeg:dash:profile:isoff-main:2011";
        public static string PROFILE_ON_DEMAND = "urn:mpeg:dash:profile:isoff-on-demand:2011";

        public DashBuilder(long durationS, string profile)
        {
            WriteXmlHeader();
            WriteTag("MPD", new Dictionary<string, string>
            {
                { "xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance" },
                { "xmlns", "urn:mpeg:dash:schema:mpd:2011" },
                { "xsi:schemaLocation", "urn:mpeg:dash:schema:mpd:2011 DASH-MPD.xsd" },
                { "type", "static" },
                { "mediaPresentationDuration", $"PT{durationS}S" },
                { "minBufferTime", "PT2S" },
                { "profiles", profile }
            }, false);
            //Temporary...always Period wrapped
            WriteTag("Period", new Dictionary<string, string>(), false);
        }

        public void WithAdaptationSet(Dictionary<string, string> parameters, Action<DashBuilder> writeBody)
        {
            Tag("AdaptationSet", parameters, (builder) =>
            {
                writeBody(builder as DashBuilder);
            });
        }
        public void WithRepresentation(string id, Dictionary<string, string> parameters, Action<DashBuilder> writeBody)
        {
            var modParas = new Dictionary<string, string>(parameters);
            modParas["id"] = id;
            Tag("Representation", modParas, (XMLBuilder dashBuilder) =>
            {
                writeBody((DashBuilder)dashBuilder);
            });
        }

        void WithRepresentationOnDemand(string id, IAudioSource audioSource, string audioUrl)
        {
            if (!(audioSource is IStreamMetaDataSource))
                throw new NotImplementedException("Currently onDemand dash only works with IStreamMetaDataSource");
            if (((IStreamMetaDataSource)audioSource).MetaData == null)
                throw new Exception("Stream metadata information missing, the video will need to be redownloaded to be casted");
            WithRepresentation(id, new Dictionary<string, string>
            {
                { "mimeType", audioSource.Container },
                { "codecs", audioSource.Codec },
                { "startWithSAP", "1" },
                { "bandwidth", "100000" }
            }, representation =>
            {
                representation.WithSegmentBase(
                    audioUrl,
                    (long)((IStreamMetaDataSource)audioSource).MetaData.FileInitStart,
                    (long)((IStreamMetaDataSource)audioSource).MetaData.FileInitEnd,
                    (long)(((IStreamMetaDataSource)audioSource).MetaData.FileIndexStart ?? 0),
                    (long)(((IStreamMetaDataSource)audioSource).MetaData.FileIndexEnd ?? 0)
                );
            });
        }
        public void WithRepresentationOnDemand(string id, IVideoSource videoSource, string videoUrl)
        {

            if (videoSource is IStreamMetaDataSource)
            {
                if (!(videoSource is IStreamMetaDataSource) || ((IStreamMetaDataSource)videoSource).MetaData == null)
                    throw new NotImplementedException("Currently onDemand dash only works with IStreamMetaDataSource");

                if (((IStreamMetaDataSource)videoSource).MetaData == null)
                    throw new Exception("Stream metadata information missing, the video will need to be redownloaded to be casted");

                WithRepresentation(id, new Dictionary<string, string>
                {
                    { "mimeType", videoSource.Container },
                    { "codecs", videoSource.Codec },
                    { "width", videoSource.Width.ToString() },
                    { "height", videoSource.Height.ToString() },
                    { "startWithSAP", "1" },
                    { "bandwidth", "100000" }
                }, representation =>
                {
                    representation.WithSegmentBase(
                        videoUrl.Trim(),
                        ((IStreamMetaDataSource)videoSource).MetaData.FileInitStart ?? 0,
                        ((IStreamMetaDataSource)videoSource).MetaData.FileInitEnd ?? 0,
                        ((IStreamMetaDataSource)videoSource).MetaData.FileIndexStart ?? 0,
                        ((IStreamMetaDataSource)videoSource).MetaData.FileIndexEnd ?? 0
                    );
                });
            }
            else
                throw new NotImplementedException("Currently onDemand dash only works with IStreamMetaDataSource");
        }
        public void WithRepresentationOnDemand(string id, ISubtitleSource subtitleSource, string subtitleUrl)
        {
            WithRepresentation(id, new Dictionary<string, string>
            {
                { "mimeType", subtitleSource.Format ?? "text/vtt" },
                { "default", "true" },
                { "lang", "en" },
                { "bandwidth", "1000" },
            }, representation =>
            {
                representation.WithBaseURL(subtitleUrl);
            });
        }


        public void WithBaseURL(string url)
        {
            ValueTag("BaseURL", url);
        }

        // Segments
        public void WithSegmentBase(string url, long initStart, long initEnd, long segStart, long segEnd)
        {
            ValueTag("BaseURL", url);

            Tag("SegmentBase", (segStart > 0) ? new Dictionary<string, string> { { "indexRange", $"{segStart}-{segEnd}" } } : new Dictionary<string, string>(), (XMLBuilder builder) =>
            {
                TagClosed("Initialization", new Dictionary<string, string>
                {
                    { "sourceURL", url },
                    { "range", $"{initStart}-{initEnd}" }
                });
            });
        }

        public override string Build()
        {
            WriteCloseTag("Period");
            WriteCloseTag("MPD");

            return base.Build();
        }

        public static string GenerateOnDemandDash(IVideoSource vidSource, string vidUrl, IAudioSource audioSource, string audioUrl, ISubtitleSource subtitleSource, string subtitleUrl)
        {

            if (vidSource is VideoUrlSource && vidUrl == null)
                vidUrl = ((VideoUrlSource)vidSource).Url;
            if (audioSource is AudioUrlSource && audioUrl == null)
                audioUrl = ((AudioUrlSource)audioSource).Url;

            var duration = vidSource?.Duration ?? audioSource?.Duration;
            if (duration == null)
                throw new Exception("Either video or audio source needs to be set.");

            var dashBuilder = new DashBuilder(duration.Value, PROFILE_ON_DEMAND);

            // Audio
            if (audioSource != null && audioUrl != null)
            {
                dashBuilder.WithAdaptationSet(new Dictionary<string, string>
            {
                { "mimeType", audioSource.Container },
                { "codecs", audioSource.Codec },
                { "subsegmentAlignment", "true" },
                { "subsegmentStartsWithSAP", "1" }
            }, adaptationSet =>
            {
                //TODO: Verify if & really should be replaced like this?
                adaptationSet.WithRepresentationOnDemand("1", audioSource, audioUrl.Replace("&", "&amp;"));
            });
            }

            // Subtitles
            if (subtitleSource != null && subtitleUrl != null)
            {
                dashBuilder.WithAdaptationSet(new Dictionary<string, string>
                {
                    { "mimeType", subtitleSource.Format ?? "text/vtt" },
                    { "lang", "en" },
                    { "default", "true" }
                }, adaptationSet =>
                {
                    //TODO: Verify if & really should be replaced like this?
                    adaptationSet.WithRepresentationOnDemand("caption_en", (ISubtitleSource)subtitleSource, subtitleUrl.Replace("&", "&amp;"));
                });
            }

            // Video
            if (vidSource != null && vidUrl != null)
            {
                dashBuilder.WithAdaptationSet(new Dictionary<string, string>
                {
                    { "mimeType", vidSource.Container },
                    { "codecs", vidSource.Codec },
                    { "subsegmentAlignment", "true" },
                    { "subsegmentStartsWithSAP", "1" }
                }, adaptationSet =>
                {
                    adaptationSet.WithRepresentationOnDemand("2", vidSource, vidUrl.Replace("&", "&amp;"));
                });
            }

            return dashBuilder.Build();
        }

    }

    public class XMLBuilder
    {
        protected StringWriter writer = new StringWriter();
        private int _indentation = 0;

        public void WriteXmlHeader(string version = "1.0", string encoding = "UTF-8")
        {
            writer.Write($"<?xml version=\"{version}\" encoding=\"{encoding}\"?>\n");
        }

        public void TagClosed(string tagName, params KeyValuePair<string, string>[] parameters)
        {
            TagClosed(tagName, parameters.ToDictionary(p => p.Key, p => p.Value));
        }

        public void TagClosed(string tagName, Dictionary<string, string> parameters)
        {
            WriteTag(tagName, parameters, true);
        }

        public void Tag(string tagName, Dictionary<string, string> parameters, Action<XMLBuilder> fill)
        {
            WriteTag(tagName, parameters, false);
            fill(this);
            WriteCloseTag(tagName);
        }

        public void ValueTag(string tagName, string value)
        {
            ValueTag(tagName, new Dictionary<string, string>(), value);
        }

        public void ValueTag(string tagName, Dictionary<string, string> parameters, string value)
        {
            WriteTag(tagName, parameters, false, false);
            writer.Write(value);
            WriteCloseTagWithoutIndent(tagName);
        }

        public void Value(string value)
        {
            WriteIndentation(_indentation);
            writer.Write(value + "\n");
        }

        protected void WriteTag(string tagName, Dictionary<string, string> parameters = null, bool closed = true, bool withNewLine = true)
        {
            WriteIndentation(_indentation);
            writer.Write($"<{tagName}");
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                    writer.Write($" {parameter.Key}=\"{parameter.Value}\"");
            }
            if (closed)
            {
                writer.Write("/>");
                if (withNewLine)
                    writer.Write("\n");
            }
            else
            {
                writer.Write(">");
                if (withNewLine)
                    writer.Write("\n");
                _indentation++;
            }
        }

        protected void WriteCloseTagWithoutIndent(string tagName, bool withNewLine = true)
        {
            writer.Write($"</{tagName}>");
            if (withNewLine)
                writer.Write("\n");
        }

        protected void WriteCloseTag(string tagName, bool withNewLine = true)
        {
            _indentation--;
            if (_indentation < 0)
                _indentation = 0;
            WriteIndentation(_indentation);
            writer.Write($"</{tagName}>");
            if (withNewLine)
                writer.Write("\n");
        }

        protected void WriteIndentation(int indentation)
        {
            for (int i = 0; i < indentation; i++)
                writer.Write("    ");
        }

        public virtual string Build()
        {
            return writer.ToString();
        }
    }
}

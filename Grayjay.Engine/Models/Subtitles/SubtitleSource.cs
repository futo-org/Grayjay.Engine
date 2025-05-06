using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Subtitles
{
    public class SubtitleSource : ISubtitleSource
    {
        private IJavaScriptObject _obj = null;
        private FileInfo _fileSubtitle = null;

        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("url", true)]
        public string Url { get; set; }
        [V8Property("format")]
        public string Format { get; set; }

        public bool HasFetch { get; set; }

        public SubtitleSource() { }
        public SubtitleSource(IJavaScriptObject obj)
        {
            _obj = obj;
            if (obj == null)
                HasFetch = false;
            else
                HasFetch = obj.HasFunction("getSubtitles");
        }

        public virtual string GetSubtitles()
        {
            if (!HasFetch)
                throw new InvalidOperationException("This subtitle doesn't support getSubtitles");
            return (string)_obj.InvokeMethod("getSubtitles");
        }

        public Uri? GetSubtitlesUri()
        {
            if (_fileSubtitle != null)
                return new Uri("file://" + _fileSubtitle.FullName);
            if (!HasFetch)
                return new Uri(Url);

            var subtitleText = GetSubtitles();
            var subFile = Path.GetTempFileName();
            File.WriteAllText(subFile, subtitleText);
            _fileSubtitle = new FileInfo(subFile);
            return new Uri("file://" + _fileSubtitle.FullName);
        }

        public SubtitleRawSource ToRaw()
        {
            if(HasFetch)
            {
                var subs = GetSubtitles();
                return new SubtitleRawSource()
                {
                    Name = Name,
                    Format = Format,
                    Url = Url,
                    HasFetch = true,
                    _Subtitles = subs
                };
            }
            else
            {
                using(WebClient client = new WebClient())
                {
                    var subs = client.DownloadString(Url);
                    return new SubtitleRawSource()
                    {
                        Name = Name,
                        Format = Format,
                        Url = Url,
                        HasFetch = true,
                        _Subtitles = subs
                    };
                }
            }
        }


        public class Serializable: SubtitleSource
        {
            public Serializable() : base(null)
            {

            }

            public Serializable(ISubtitleSource source) : base(null)
            {
                Name = source.Name;
                Url = source.Url;
                Format = source.Format;
                if (source.HasFetch)
                    throw new InvalidDataException("Cannot make a live subtitle source serializable");
                HasFetch = false;
            }

            public override string GetSubtitles()
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SubtitleRawSource : SubtitleSource
    {
        [JsonPropertyName("_subtitles")]
        public string _Subtitles { get; set; }

        public SubtitleRawSource() : base(null)
        {

        }

        public override string GetSubtitles()
        {
            return _Subtitles;
        }
    }
}

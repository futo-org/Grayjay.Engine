using System;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class LocalSubtitleSource : ISubtitleSource
    {
        public string Name { get; set; }
        public string? Url { get; set; }
        public string? Format { get; set; }
        public bool HasFetch { get; set; } = false;

        public string FilePath { get; set; }


        public static LocalSubtitleSource FromSource(ISubtitleSource source, string path)
        {
            return new LocalSubtitleSource()
            {
                Name = source.Name,
                Url = source.Url,
                Format = source.Format,
                FilePath = path
            };
        }

        public string? GetSubtitles()
        {
            return null;
        }

        public Uri? GetSubtitlesUri()
        {
            return new Uri("file://" + FilePath);
        }
    }
}

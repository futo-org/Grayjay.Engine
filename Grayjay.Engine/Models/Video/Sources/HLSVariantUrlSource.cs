using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class HLSVariantVideoUrlSource: VideoUrlSource
    {

    }
    public class HLSVariantAudioUrlSource: AudioUrlSource
    {
    }
    public class HLSVariantSubtitleUrlSource: ISubtitleSource
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Format { get; set; }

        public bool HasFetch => false;

        public string? GetSubtitles()
        {
            throw new NotImplementedException();
        }

        public Uri? GetSubtitlesUri()
        {
            return new Uri(Url);
        }
    }
}

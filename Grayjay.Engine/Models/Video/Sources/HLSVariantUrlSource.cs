using Grayjay.Engine.Models.Video.Additions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class HLSVariantVideoUrlSource: VideoUrlSource
    {
        public IRequestModifier Modifier { get; set; }

        public override IRequestModifier GetRequestModifier()
        {
            return Modifier ?? base.GetRequestModifier();
        }
    }
    public class HLSVariantAudioUrlSource: AudioUrlSource
    {
        public IRequestModifier Modifier { get; set; }

        public override IRequestModifier GetRequestModifier()
        {
            return Modifier ?? base.GetRequestModifier();
        }
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

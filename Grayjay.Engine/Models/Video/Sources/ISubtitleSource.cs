using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface ISubtitleSource
    {
        string Name { get; }
        string? Url { get; }
        string? Format { get; }
        bool HasFetch { get; }

        string? GetSubtitles();

        Uri? GetSubtitlesUri();
    }
}

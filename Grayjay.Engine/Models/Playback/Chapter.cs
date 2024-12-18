using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Playback
{
    public enum ChapterType: int
    {
        Normal = 0,
        Skippable = 5,
        Skip = 6,
        SkipOnce = 7
    }
    public class Chapter
    {
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("type")]
        public string Type { get; set; }
        [V8Property("timeStart")]
        public double TimeStart { get; set; }
        [V8Property("timeEnd")]
        public double TimeEnd { get; set; }
    }
}

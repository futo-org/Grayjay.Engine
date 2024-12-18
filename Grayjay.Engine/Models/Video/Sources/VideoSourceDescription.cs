using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class VideoSourceDescription : IVideoSource
    {
        public string Type => "VideoSourceDescription";

        public int Width { get; set; }
        public int Height { get; set; }
        public string Container { get; set; }
        public string Codec { get; set; }
        public string Name { get; set; }
        public int Duration { get; set; }
        public bool Priority { get; set; }


        public VideoSourceDescription() { }

        public static VideoSourceDescription FromSource(IVideoSource source)
        {
            return new VideoSourceDescription()
            {
                Width = source.Width,
                Height = source.Height,
                Container = source.Container,
                Codec = source.Codec,
                Name = source.Name,
                Duration = source.Duration,
                Priority = source.Priority
            };
        }
    }
    public class AudioSourceDescription : IAudioSource
    {
        public string Type => "AudioSourceDescription";

        public string Container { get; set; }
        public string Codec { get; set; }
        public string Name { get; set; }
        public int Bitrate { get; set; }
        public int Duration { get; set; }
        public bool Priority { get; set; }
        public string? Language { get; set; }

        public AudioSourceDescription() { }

        public static AudioSourceDescription FromSource(IAudioSource source)
        {
            return new AudioSourceDescription()
            {
                Bitrate = source.Bitrate,
                Language = source.Language,
                Container = source.Container,
                Codec = source.Codec,
                Name = source.Name,
                Duration = source.Duration,
                Priority = source.Priority
            };
        }
    }
}

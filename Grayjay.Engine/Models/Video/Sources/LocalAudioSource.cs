using System.Runtime.CompilerServices;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class LocalAudioSource : IAudioSource, IStreamMetaDataSource
    {
        public string Type => "LocalAudioSource";

        public string Container { get; set; }
        public string Codec { get; set; }

        public string Name { get; set; }

        public int Duration { get; set; }
        public bool Priority { get; set; }

        public int Bitrate { get; set; }

        public string? Language { get; set; }

        public string FilePath { get; set; }
        public long FileSize { get; set; }

        public StreamMetaData MetaData { get; set; }

        public static LocalAudioSource FromSource(IAudioSource source, string path, long length, StreamMetaData? metaData = null, string mimeType = null)
        {
            return new LocalAudioSource()
            {
                Container = mimeType ?? ((source is HLSVariantAudioUrlSource) ? "video/mp4a" : source.Container),
                Codec = source.Codec,
                Name = source.Name,
                Duration = source.Duration,
                Priority = source.Priority,
                Bitrate = source.Bitrate,
                Language = source.Language,

                FilePath = path,
                FileSize = length,

                MetaData = ((source is IStreamMetaDataSource msource) ? msource.MetaData : null) ?? metaData
            };
        }
    }
}

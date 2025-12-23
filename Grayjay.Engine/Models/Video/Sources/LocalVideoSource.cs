using Grayjay.Engine.V8;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class LocalVideoSource : IVideoSource, IStreamMetaDataSource
    {
        public string Type => "LocalVideoSource";

        public int Width { get; set; }
        public int Height { get; set; }

        public string Container { get; set; }
        public string Codec { get; set; }

        public string Name { get; set; }

        public int Duration { get; set; }
        public bool Priority { get; set; }

        public string FilePath { get; set; }
        public long FileSize { get; set; }

        public bool Original { get; set; } = false;
        public string Language { get; set; } = null;

        public StreamMetaData MetaData { get; set; } = null;

        public static LocalVideoSource FromSource(IVideoSource source, string path, long size, StreamMetaData? metaData = null, string mimeType = null)
        {
            return new LocalVideoSource()
            {
                Width = source.Width,
                Height = source.Height,
                Container = mimeType ?? ((source is HLSVariantVideoUrlSource) ? "video/mp4" : source.Container),
                Codec = source.Codec,
                Name = source.Name,
                Duration = source.Duration,
                Priority = source.Priority,
                FilePath = path,
                FileSize = size,
                MetaData = ((source is IStreamMetaDataSource msource) ? msource.MetaData : null) ?? metaData,
                Original = source.Original,
                Language = source.Language
            };
        }
    }
}

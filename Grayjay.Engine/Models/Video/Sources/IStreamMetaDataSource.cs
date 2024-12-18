using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{

    public interface IStreamMetaDataSource
    {
        public StreamMetaData MetaData { get; }
    }
    public class StreamMetaData
    {
        public int? FileInitStart { get; set; }
        public int? FileInitEnd { get; set; }
        public int? FileIndexStart { get; set; }
        public int? FileIndexEnd { get; set; }
    }
}

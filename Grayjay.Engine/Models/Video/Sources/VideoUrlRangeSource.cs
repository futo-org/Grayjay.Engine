using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class VideoUrlRangeSource: VideoUrlSource, IStreamMetaDataSource
    {
        public override string Type => "VideoUrlRangeSource";

        [V8Property("itagId")]
        public int ITagID { get; set; }
        [V8Property("initStart")]
        public int InitStart { get; set; }
        [V8Property("initEnd")]
        public int InitEnd { get; set; }
        [V8Property("indexStart")]
        public int IndexStart { get; set; }
        [V8Property("indexEnd")]
        public int IndexEnd { get; set; }

        public StreamMetaData MetaData => new StreamMetaData()
        {
            FileInitStart = InitStart,
            FileInitEnd = InitEnd,
            FileIndexStart = IndexStart,
            FileIndexEnd = IndexEnd
        };



        public VideoUrlRangeSource() : base() { }
        public VideoUrlRangeSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj)
        {

        }
    }
}

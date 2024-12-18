using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformPlaylist : PlatformContent
    {
        public override ContentType ContentType { get; } = Models.ContentType.PLAYLIST;

        [V8Property("thumbnail", true)]
        public string Thumbnail { get; set; }

        [V8Property("videoCount")]
        public int VideoCount { get; set; }


        public PlatformPlaylist() : base()
        {

        }
    }
}

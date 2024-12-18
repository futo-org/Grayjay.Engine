using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.Models.Subtitles;
using Grayjay.Engine.Models.Video;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Detail
{
    public class PlatformPostDetails : PlatformPost, IPlatformContentDetails
    {
        [V8Property("rating", true)]
        public IRating Rating { get; set; }

        [V8Property("textType")]
        public int TextType { get; set; }

        [V8Property("content")]
        public string Content { get; set; }


        public PlatformPostDetails() : base(null) { }
        public PlatformPostDetails(IJavaScriptObject obj) : base(obj)
        {

        }
    }
}

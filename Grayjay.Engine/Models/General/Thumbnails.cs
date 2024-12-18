using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.General
{
    public class Thumbnails
    {
        [V8Property("sources")]
        public Thumbnail[] Sources { get; set; }
    }

    public class Thumbnail
    {
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("quality")]
        public int Quality { get; set; }
    }
}

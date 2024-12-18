using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformPost: PlatformContent
    {
        public override ContentType ContentType { get; } = ContentType.POST;

        [V8Property("thumbnails")]
        public Thumbnails[] Thumbnails { get; set; }
        [V8Property("images")]
        public string[] Images { get; set; }
        [V8Property("description")]
        public string Description { get; set; }

        public PlatformPost(IJavaScriptObject obj) : base(obj)
        {

        }
        public PlatformPost() : base(null)
        {

        }
    }
}

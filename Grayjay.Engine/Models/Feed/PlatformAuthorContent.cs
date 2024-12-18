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
    public class PlatformAuthorContent : PlatformContent
    {
        public override ContentType ContentType { get; } = Models.ContentType.CHANNEL;

        [V8Property("thumbnail")]
        public string Thumbnail { get; set; }

        [V8Property("subscribers", true)]
        public long Subscribers { get; set; }

        public PlatformAuthorContent() : base()
        {

        }
        public PlatformAuthorContent(PlatformAuthorLink link) : base()
        {
            ID = link.ID;
            Name = link.Name;
            Url = link.Url;
            Thumbnail = link.Thumbnail;
            Subscribers = link.Subscribers;
        }
    }
}

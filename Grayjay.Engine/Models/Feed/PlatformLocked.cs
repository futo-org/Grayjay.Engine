using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformLockedContent: PlatformContent
    {
        public override ContentType ContentType { get; } = ContentType.LOCKED;

        [V8Property("contentName", true)]
        public string ContentName { get; set; }
        [V8Property("contentThumbnails")]
        public Thumbnails[] ContentThumbnails { get; set; }
        [V8Property("unlockUrl", true)]
        public string UnlockUrl { get; set; }
        [V8Property("lockDescription", true)]
        public string LockDescription { get; set; }

        public PlatformLockedContent(IJavaScriptObject obj) : base(obj)
        {

        }
        public PlatformLockedContent() : base(null)
        {

        }
    }
}

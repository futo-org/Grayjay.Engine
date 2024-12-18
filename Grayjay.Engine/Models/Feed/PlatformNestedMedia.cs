using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformNestedMedia: PlatformContent
    {
        public override ContentType ContentType { get; } = ContentType.NESTED_VIDEO;

        [V8Property("contentUrl")]
        public string ContentUrl { get; set; }
        [V8Property("contentName")]
        public string ContentName { get; set; }
        [V8Property("contentDescription")]
        public string ContentDescription { get; set; }
        [V8Property("contentProvider")]
        public string ContentProvider { get; set; }
        [V8Property("contentThumbnails")]
        public Thumbnails ContentThumbnails { get; set; }

        public string PluginID { get; set; }
        public string PluginName { get; set; }
        public string PluginThumbnail { get; set; }

        public PlatformNestedMedia(IJavaScriptObject obj) : base(obj)
        {
            var resolver = _pluginResolver;
            if (resolver != null)
            {
                if (obj.PropertyNames.Contains("contentUrl"))
                {
                    var contentUrl = obj["contentUrl"];
                    if (contentUrl is string contentUrlStr)
                    {
                        (var id, var name, var thumbnail) = resolver(contentUrlStr);
                        PluginID = id;
                        PluginName = name;
                        PluginThumbnail = thumbnail;
                    }
                }
            }
        }
        public PlatformNestedMedia() : base(null)
        {

        }


        private static Func<string, (string, string, string)> _pluginResolver = null;
        public static void SetPluginResolver(Func<string, (string, string, string)> resolver)
        {
            _pluginResolver = resolver;
        }
    }
}

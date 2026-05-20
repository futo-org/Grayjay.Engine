using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformArticle : PlatformContent
    {
        public override ContentType ContentType { get; } = ContentType.ARTICLE;

        [V8Property("summary", true)]
        public string Summary { get; set; }

        [V8Property("thumbnails", true)]
        public Thumbnails Thumbnails { get; set; }

        public PlatformArticle(IJavaScriptObject obj) : base(obj)
        {

        }

        public PlatformArticle() : base(null)
        {

        }
    }
}

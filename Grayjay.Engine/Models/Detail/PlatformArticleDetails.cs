using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Detail
{
    public class PlatformArticleDetails : PlatformArticle, IPlatformContentDetails
    {
        [V8Property("rating", true)]
        public IRating Rating { get; set; } = new RatingLikes { Likes = 0 };

        [V8Property("segments", true)]
        public ArticleSegment[] Segments { get; set; } = Array.Empty<ArticleSegment>();

        public PlatformArticleDetails() : base(null) { }

        public PlatformArticleDetails(IJavaScriptObject obj) : base(obj)
        {

        }
    }

    public enum ArticleSegmentType
    {
        UNKNOWN = 0,
        TEXT = 1,
        IMAGES = 2,
        HEADER = 3,
        NESTED = 9
    }

    [JsonDerivedType(typeof(ArticleTextSegment))]
    [JsonDerivedType(typeof(ArticleImagesSegment))]
    [JsonDerivedType(typeof(ArticleHeaderSegment))]
    [JsonDerivedType(typeof(ArticleNestedSegment))]
    public class ArticleSegment : IV8Polymorphic
    {
        [V8Property("type")]
        public int Type { get; set; }

        public ArticleSegment(IJavaScriptObject obj = null)
        {

        }

        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            var type = (ArticleSegmentType)Convert.ToInt32(obj.GetProperty("type"));
            return type switch
            {
                ArticleSegmentType.TEXT => typeof(ArticleTextSegment),
                ArticleSegmentType.IMAGES => typeof(ArticleImagesSegment),
                ArticleSegmentType.HEADER => typeof(ArticleHeaderSegment),
                ArticleSegmentType.NESTED => typeof(ArticleNestedSegment),
                _ => typeof(ArticleSegment)
            };
        }
    }

    public class ArticleTextSegment : ArticleSegment
    {
        [V8Property("textType", true)]
        public int TextType { get; set; } = 0;

        [V8Property("content", true)]
        public string Content { get; set; } = string.Empty;

        public ArticleTextSegment() : base(null)
        {
            Type = (int)ArticleSegmentType.TEXT;
        }

        public ArticleTextSegment(IJavaScriptObject obj) : base(obj)
        {

        }
    }

    public class ArticleImagesSegment : ArticleSegment
    {
        [V8Property("images", true)]
        public string[] Images { get; set; } = Array.Empty<string>();

        [V8Property("caption", true)]
        public string Caption { get; set; } = string.Empty;

        public ArticleImagesSegment() : base(null)
        {
            Type = (int)ArticleSegmentType.IMAGES;
        }

        public ArticleImagesSegment(IJavaScriptObject obj) : base(obj)
        {

        }
    }

    public class ArticleHeaderSegment : ArticleSegment
    {
        [V8Property("content", true)]
        public string Content { get; set; } = string.Empty;

        [V8Property("level", true)]
        public int Level { get; set; } = 1;

        public ArticleHeaderSegment() : base(null)
        {
            Type = (int)ArticleSegmentType.HEADER;
        }

        public ArticleHeaderSegment(IJavaScriptObject obj) : base(obj)
        {

        }
    }

    public class ArticleNestedSegment : ArticleSegment
    {
        [V8Property("nested")]
        public PlatformContent Nested { get; set; }

        public ArticleNestedSegment() : base(null)
        {
            Type = (int)ArticleSegmentType.NESTED;
        }

        public ArticleNestedSegment(IJavaScriptObject obj) : base(obj)
        {

        }
    }
}

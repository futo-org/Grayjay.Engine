using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Ratings
{
    public enum RatingTypes: int
    {
        Likes = 1,
        Dislikes = 2,
        Scaler = 3
    }

    [JsonDerivedType(typeof(RatingLikes))]
    [JsonDerivedType(typeof(RatingDislikes))]
    [JsonDerivedType(typeof(RatingScaler))]
    public interface IRating: IV8Polymorphic
    {
        public RatingTypes Type { get; }

        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            int type = (int)obj.GetProperty("type");

            switch (type)
            {
                case (int)RatingTypes.Likes:
                    return typeof(RatingLikes);
                case (int)RatingTypes.Dislikes:
                    return typeof(RatingDislikes);
                case (int)RatingTypes.Scaler:
                    return typeof(RatingScaler);
            }

            return typeof(IRating);
        }
    }

    public class RatingLikes: IRating
    {
        public RatingTypes Type => RatingTypes.Likes;

        [V8Property("likes")]
        public int Likes { get; set; }
    }
    public class RatingDislikes : IRating
    {
        public RatingTypes Type => RatingTypes.Dislikes;

        [V8Property("likes")]
        public int Likes { get; set; }
        [V8Property("dislikes")]
        public int Dislikes { get; set; }
    }
    public class RatingScaler : IRating
    {
        public RatingTypes Type => RatingTypes.Scaler;

        [V8Property("value")]
        public float Value { get; set; }
    }
}

using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Capabilities
{
    public class ResultCapabilities
    {
        [V8Property("types")]
        public List<string> Types { get; set; }
        [V8Property("sorts", true)]
        public List<string> Sorts { get; set; }
        [V8Property("filters", true)]
        public List<FilterGroup> Filters { get; set; }



        public const string TYPE_VIDEOS = "VIDEOS";
        public const string TYPE_STREAMS = "STREAMS";
        public const string TYPE_LIVE = "LIVE";
        public const string TYPE_POSTS = "POSTS";
        public const string TYPE_MIXED = "MIXED";
        public const string TYPE_SUBSCRIPTIONS = "SUBSCRIPTIONS";

        public const string ORDER_CHONOLOGICAL = "CHRONOLOGICAL";

        public const string DATE_LAST_HOUR = "LAST_HOUR";
        public const string DATE_TODAY = "TODAY";
        public const string DATE_LAST_WEEK = "LAST_WEEK";
        public const string DATE_LAST_MONTH = "LAST_MONTH";
        public const string DATE_LAST_YEAR = "LAST_YEAR";

        public const string DURATION_SHORT = "SHORT";
        public const string DURATION_MEDIUM = "MEDIUM";
        public const string DURATION_LONG = "LONG";


        public bool HasType(string type)
        {
            return Types.Contains(type);
        }
    }

    public class FilterGroup
    {
        [V8Property("id")]
        public string? ID { get; set; }
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("isMultiSelect", true)]
        public bool IsMultiSelect { get; set; }
        [V8Property("filters")]
        public List<FilterCapability> Filters { get; set; }

        public string IDOrName => ID ?? Name;
    }

    public class FilterCapability
    {
        [V8Property("id", true)]
        public string? ID { get; set; }
        [V8Property("name")]
        public string Name { get; set; }
        [V8Property("value")]
        public string Value { get; set; }

        public string IDOrName => ID ?? Name;
    }
}

using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Buffers.Text;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PlatformID = Grayjay.Engine.Models.General.PlatformID;

namespace Grayjay.Engine.Models.Feed
{
    [JsonDerivedType(typeof(PlatformVideo))]
    [JsonDerivedType(typeof(PlatformContentPlaceholder))]
    [JsonDerivedType(typeof(PlatformPlaylist))]
    [JsonDerivedType(typeof(PlatformAuthorContent))]
    [JsonDerivedType(typeof(PlatformPost))]
    [JsonDerivedType(typeof(PlatformNestedMedia))]
    [JsonDerivedType(typeof(PlatformLockedContent))]
    [JsonDerivedType(typeof(PlatformPostDetails))]
    [JsonDerivedType(typeof(PlatformVideoDetails))]
    public class PlatformContent: IV8Polymorphic
    {
        private IJavaScriptObject _object;

        public virtual ContentType ContentType { get; } = ContentType.UNKNOWN;

        [V8Property("id")]
        public virtual PlatformID ID { get; set; }

        [V8Property("datetime", true)]
        [JsonConverter(typeof(UnixSupportedDateTimeConverter))]
        public virtual DateTime DateTime { get; set; }

        [V8Property("name")]
        public virtual string Name { get; set; }

        [V8Property("author")]
        public virtual PlatformAuthorLink Author { get; set; }

        [V8Property("url")]
        public virtual string Url { get; set; }

        [V8Property("shareUrl", true)]
        public virtual string ShareUrl { get; set; }

        public string BackendUrl { get; set; }
        public bool IsDetailObject => this is IPlatformContentDetails;

        public PlatformContent(IJavaScriptObject obj = null)
        {
            _object = obj;
        }


        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            int type = (int)obj.GetProperty("contentType");
            var pluginTypeProp = obj.GetProperty("plugin_type");
            string pluginType = (pluginTypeProp is string) ? (string)pluginTypeProp : null;

            switch (type)
            {
                case 0:
                    return typeof(PlatformContent);
                case 1:
                    if (pluginType == "PlatformVideoDetails")
                        return typeof(PlatformVideoDetails);
                    return typeof(PlatformVideo);
                case 4:
                    return typeof(PlatformPlaylist);
                case 2:
                    if (pluginType == "PlatformPostDetails")
                        return typeof(PlatformPostDetails);
                    return typeof(PlatformPost);
                case 11:
                    return typeof(PlatformNestedMedia);
                case 60:
                    return typeof(PlatformAuthorContent);
                case 70:
                    return typeof(PlatformLockedContent);
            }

            return typeof(PlatformContent);
        }

        public IJavaScriptObject GetUnderlyingObject()
        {
            return _object;
        }
    }
    public class UnixSupportedDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    var val = reader.GetInt64();
                    if (val < 0)
                        return DateTime.MinValue;
                    return DateTimeOffset.FromUnixTimeSeconds(val).DateTime;
                }
                else if (reader.TokenType == JsonTokenType.Null)
                    return DateTime.MinValue;
                else
                    return DateTime.Parse(reader.GetString());
            }
            catch(Exception ex)
            {
                throw new ArgumentException($"Failed to parse Datetime on type [{typeToConvert.Name}] with value type [{reader.TokenType}]", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value.Year > 9000)
                writer.WriteNumberValue(DateTimeOffset.MaxValue.ToUnixTimeSeconds());
            else if (value.Year < 1971)
                writer.WriteNumberValue(DateTimeOffset.MinValue.ToUnixTimeSeconds());
            else
                writer.WriteNumberValue(((DateTimeOffset)value).ToUnixTimeSeconds());
        }
    }
}

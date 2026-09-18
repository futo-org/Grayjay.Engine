using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Video.Sources
{
    [JsonDerivedType(typeof(VideoUrlSource))]
    [JsonDerivedType(typeof(VideoUrlWidevineSource))]
    [JsonDerivedType(typeof(VideoUrlRangeSource))]
    [JsonDerivedType(typeof(HLSManifestSource))]
    [JsonDerivedType(typeof(HLSManifestWidevineSource))]
    [JsonDerivedType(typeof(HLSVariantVideoUrlSource))]
    [JsonDerivedType(typeof(LocalVideoSource))]
    [JsonDerivedType(typeof(DashManifestSource))]
    [JsonDerivedType(typeof(DashManifestWidevineSource))]
    [JsonDerivedType(typeof(DashManifestRawSource))]
    [JsonDerivedType(typeof(VideoSourceDescription))]
    public interface IVideoSource: IV8Polymorphic
    {
        public string Type { get; }

        int Width { get; }
        int Height { get; }
        string Container { get; }
        string Codec { get; }
        string Name { get; }
        public int Duration { get; }
        public bool Priority { get; }

        public bool Original { get; set; }
        public string Language { get; set; }

        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            string type = (string)obj.GetProperty("plugin_type");

            switch (type)
            {
                case "VideoUrlSource":
                    return typeof(VideoUrlSource);
                case "VideoUrlWidevineSource":
                    return typeof(VideoUrlWidevineSource);
                case "VideoUrlRangeSource":
                    return typeof(VideoUrlRangeSource);
                case "HLSSource":
                    return typeof(HLSManifestSource);
                case "HLSWidevineSource":
                    return typeof(HLSManifestWidevineSource);
                case "DashSource":
                    return typeof(DashManifestSource);
                case "DashWidevineSource":
                    return typeof(DashManifestWidevineSource);
                case "DashRawSource":
                    return typeof(DashManifestRawSource);
                case "VideoSourceDescription":
                    return typeof(VideoSourceDescription);
            }

            throw new NotImplementedException($"IVideoSource Type [{type}] not implemented");
        }
    }
}

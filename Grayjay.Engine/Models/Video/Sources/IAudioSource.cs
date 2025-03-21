using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Video.Sources
{
    public interface IAudioSource: IV8Polymorphic
    {
        string Type { get; }
        string Container { get; }
        string Codec { get; }
        string Name { get; }
        int Bitrate { get; }
        int Duration { get; }
        bool Priority { get; }
        bool Original { get; }

        public string? Language { get; set; }

        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            string type = (string)obj.GetProperty("plugin_type");

            switch (type)
            {
                case "AudioUrlSource":
                    return typeof(AudioUrlSource);
                case "AudioUrlRangeSource":
                    return typeof(AudioUrlRangeSource);
                case "HLSSource":
                    return typeof(HLSManifestAudioSource);
                case "DashRawAudioSource":
                    return typeof(DashManifestRawAudioSource);
                case "AudioSourceDescription":
                    return typeof(AudioSourceDescription);
            }

            throw new NotImplementedException($"IAudioSource Type [{type}] not implemented");
        }
    }
}

using Grayjay.Engine.Models.Detail;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Video
{
    [JsonDerivedType(typeof(UnMuxedVideoDescriptor))]
    public class VideoDescriptor: IV8Polymorphic
    {
        [V8Property("videoSources")]
        public IVideoSource[] VideoSources { get; set; }


        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            bool isUnMuxed = (bool)obj.GetProperty("isUnMuxed");

            if (!isUnMuxed)
                return typeof(VideoDescriptor);
            else
                return typeof(UnMuxedVideoDescriptor);
        }

        public virtual bool HasAnySource() => VideoSources.Any();
    }
    public class UnMuxedVideoDescriptor : VideoDescriptor, IV8Polymorphic
    {
        [V8Property("audioSources")]
        public IAudioSource[] AudioSources { get; set; }


        public override bool HasAnySource() => VideoSources.Any() && AudioSources.Any();
    }
}

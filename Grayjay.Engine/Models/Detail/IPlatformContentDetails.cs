using Grayjay.Engine.Models.General;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;
using PlatformID = Grayjay.Engine.Models.General.PlatformID;

namespace Grayjay.Engine.Models.Detail
{

    public interface IPlatformContentDetails : IV8Polymorphic
    {
        public ContentType ContentType { get; }
        public PlatformID ID { get; }
        public DateTime DateTime { get; }
        public string Name { get; }
        public PlatformAuthorLink Author { get; }
        public string Url { get; }
        public string ShareUrl { get; }

        public static Type GetPolymorphicType(IJavaScriptObject obj)
        {
            string type = (string)obj.GetProperty("plugin_type");

            switch(type)
            {
                case "PlatformVideoDetails":
                    return typeof(PlatformVideoDetails);
                case "PlatformPostDetails":
                    return typeof(PlatformPostDetails);
                default:
                    throw new NotImplementedException($"{type} not implemented yet");
            }

            throw new NotImplementedException($"{type} not implemented yet");
        }

        IJavaScriptObject GetUnderlyingObject();
    }
}

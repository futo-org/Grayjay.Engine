using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Live;

[JsonDerivedType(typeof(LiveEventComment))]
[JsonDerivedType(typeof(LiveEventEmojis))]
[JsonDerivedType(typeof(LiveEventDonation))]
[JsonDerivedType(typeof(LiveEventViewCount))]
[JsonDerivedType(typeof(LiveEventRaid))]
public class PlatformLiveEvent : IV8Polymorphic
{
    private IJavaScriptObject _object;
    public virtual LiveEventType Type { get; } = LiveEventType.UNKNOWN;
    public virtual long Time { get; set; }

    public PlatformLiveEvent(IJavaScriptObject obj = null)
    {
        _object = obj;
    }

    public static Type GetPolymorphicType(IJavaScriptObject obj)
    {
        var t = (LiveEventType)Convert.ToInt32(obj.GetProperty("type"));
        return t switch
        {
            LiveEventType.COMMENT => typeof(LiveEventComment),
            LiveEventType.EMOJIS => typeof(LiveEventEmojis),
            LiveEventType.DONATION => typeof(LiveEventDonation),
            LiveEventType.VIEWCOUNT => typeof(LiveEventViewCount),
            LiveEventType.RAID => typeof(LiveEventRaid),
            _ => throw new NotImplementedException($"Unknown live event type: {t}")
        };
    }

    public IJavaScriptObject GetUnderlyingObject()
    {
        return _object;
    }
}
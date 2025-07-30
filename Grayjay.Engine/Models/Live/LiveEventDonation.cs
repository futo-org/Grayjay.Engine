using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;

namespace Grayjay.Engine.Models.Live;

public class LiveEventDonation : PlatformLiveEvent, ILiveEventChatMessage
{
    public override LiveEventType Type => LiveEventType.DONATION;

    private readonly long _creationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private bool _hasExpired;

    [V8Property("name")]
    public string Name { get; set; }

    [V8Property("thumbnail", true)]
    public string Thumbnail { get; set; }

    [V8Property("message")]
    public string Message { get; set; }

    [V8Property("amount")]
    public string Amount { get; set; }

    [V8Property("colorDonation", true)]
    public string ColorDonation { get; set; }

    [V8Property("expire", true)]
    public int Expire { get; set; } = 6000;

    [V8Property("time", true)]
    public override long Time { get; set; }

    public bool HasExpired()
    {
        _hasExpired = _hasExpired || (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _creationTimestamp) > Expire;
        return _hasExpired;
    }

    public LiveEventDonation(IJavaScriptObject obj) : base(obj)
    {

    }
    public LiveEventDonation() : base(null)
    {

    }
}
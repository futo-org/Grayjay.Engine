using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System.Collections.Generic;

namespace Grayjay.Engine.Models.Live;

public class LiveEventComment : PlatformLiveEvent, ILiveEventChatMessage
{
    public override LiveEventType Type => LiveEventType.COMMENT;

    [V8Property("name")]
    public string Name { get; set; }

    [V8Property("thumbnail", true)]
    public string Thumbnail { get; set; }

    [V8Property("message")]
    public string Message { get; set; }

    [V8Property("colorName", true)]
    public string ColorName { get; set; }

    [V8Property("badges", true)]
    public List<string> Badges { get; set; }

    [V8Property("time", true)]
    public override long Time { get; set; }

    public LiveEventComment(IJavaScriptObject obj) : base(obj)
    {

    }
    public LiveEventComment() : base(null)
    {

    }
}
using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System.Collections.Generic;

namespace Grayjay.Engine.Models.Live;

public class LiveEventEmojis : PlatformLiveEvent
{
    public override LiveEventType Type => LiveEventType.EMOJIS;

    [V8Property("emojis")]
    public Dictionary<string, string> Emojis { get; set; }

    public LiveEventEmojis(IJavaScriptObject obj) : base(obj)
    {

    }
    public LiveEventEmojis() : base(null)
    {

    }
}
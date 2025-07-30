using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Live;

public class LiveEventViewCount : PlatformLiveEvent
{
    public override LiveEventType Type => LiveEventType.VIEWCOUNT;

    [V8Property("viewCount")]
    public int ViewCount { get; set; }

    [V8Property("time", true)]
    public override long Time { get; set; }
    public LiveEventViewCount(IJavaScriptObject obj) : base(obj)
    {

    }
    public LiveEventViewCount() : base(null)
    {

    }
}
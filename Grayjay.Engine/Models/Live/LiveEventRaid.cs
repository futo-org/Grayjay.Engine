using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Live;

public class LiveEventRaid : PlatformLiveEvent
{
    override public LiveEventType Type => LiveEventType.RAID;

    [V8Property("targetName")]
    public string TargetName { get; set; }

    [V8Property("targetThumbnail")]
    public string TargetThumbnail { get; set; }

    [V8Property("targetUrl")]
    public string TargetUrl { get; set; }

    public LiveEventRaid(IJavaScriptObject obj) : base(obj)
    {

    }
    public LiveEventRaid() : base(null)
    {

    }
}